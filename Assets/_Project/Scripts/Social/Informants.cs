using System;
using System.Collections.Generic;
using OSistema.Core;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Social
{
    [Serializable]
    public sealed class InformantData
    {
        public int Pawn;
        public float Pressure;    // hidden: how hard the investigators lean on them, and how ready they are to talk
        public bool Flipped;      // hidden: talking to the police
        public float Suspicion;   // visible: what the family has heard about them
        public bool Confirmed;    // caught for sure
        public bool Cleared;      // passed a test
        public bool Feeding;      // the family knows, and feeds them lies
    }

    [Serializable]
    public sealed class InformantState
    {
        public List<InformantData> Crew = new List<InformantData>();
        public List<Vector2Int> Bugs = new List<Vector2Int>();   // doors of bugged properties
        public int TestPawn, TestDay;
        public int LastClueDay;
        public int Day;
    }

    /// <summary>
    /// The rat in the family, and the microphones in the walls. Neither is ever shown directly:
    /// the player hears gossip (some of it wrong), sets traps, and decides whom to trust.
    /// </summary>
    public sealed class Informants
    {
        public InformantState S = new InformantState();

        static readonly (string from, string nap, string eng)[] Clues =
        {
            ("Zì Nunziatina", "Aggio visto a {0} saglì 'ncopp' a na machina senza targa, vicino 'a Questura.",
                "I saw {0} getting into an unmarked car near the Questura."),
            ("'O Barbiere", "{0} va giranno cu 'e scarpe nove e 'nu telefono nuovo. 'E sorde 'a do' 'e piglia?",
                "{0}'s walking around in new shoes with a new phone. Where's the money from?"),
            ("'O Marenaro", "{0} m'ha addimannato addò sbarca 'a roba. Pecché 'o vo' sapé?",
                "{0} asked me where the load comes ashore. Why does he want to know?"),
            ("Avvocato Ruocco", "'Nu cliente mio dice ca 'e guardie sanno cose ca sape sulo {0}.",
                "A client of mine says the cops know things only {0} knows."),
            ("Zì Nunziatina", "{0} 'a sera stuta 'o telefono e nun se sape addò va.",
                "In the evening {0} switches off his phone and nobody knows where he goes."),
        };

        public InformantData Of(Pawn p)
        {
            var d = S.Crew.Find(x => x.Pawn == p.Id);
            if (d == null)
            {
                d = new InformantData { Pawn = p.Id };
                S.Crew.Add(d);
            }
            return d;
        }

        public bool AnyFlipped(Game game)
        {
            foreach (var d in S.Crew)
            {
                var p = game.Pawns.ById(d.Pawn);
                if (d.Flipped && !d.Feeding && p != null && !p.Dead && !p.Departed) return true;
            }
            return false;
        }

        public bool PropertyBugged(Vector2Int door) => S.Bugs.Contains(door);
        public int BugCount(Game game) => S.Bugs.Count + (game.Phone.S.Bugged ? 1 : 0);

        // ---------- Time ----------

        public void Hourly(Game game)
        {
            var clock = game.Clock;
            if (clock.Hour == 1 && S.Day != clock.Day)
            {
                S.Day = clock.Day;
                Daily(game);
            }
            if (clock.Hour == 9 && S.TestPawn != 0 && clock.Day >= S.TestDay) ResolveTest(game);
        }

        void Daily(Game game)
        {
            var rng = game.Rng;
            foreach (var p in game.Pawns.Crew)
            {
                if (p.Dead || p.Departed || p.IsBoss) continue;
                var d = Of(p);
                float push = p.Loyalty < 45f ? (45f - p.Loyalty) / 6f : -1.5f;
                if (p.Mood.Value < 30f) push += 1.5f;
                if (p.Wage < 100) push += 1f;
                if (p.InJail) push += 6f;   // in Poggioreale, the investigators visit every day
                d.Pressure = Mathf.Clamp(d.Pressure + push, 0f, 100f);
                d.Suspicion = Mathf.Max(0f, d.Suspicion - 3f);

                if (!d.Flipped && !d.Cleared && d.Pressure >= 25f && rng.NextDouble() < (d.Pressure - 20f) / 220f)
                    d.Flipped = true; // nobody is told

                if (!d.Flipped) continue;
                if (d.Feeding)
                {
                    // Fed lies, the rat sends the investigators the wrong way... until he works it out.
                    game.Org.Evidence = Mathf.Max(0f, game.Org.Evidence - 1.5f);
                    if (rng.NextDouble() < 0.08) RatRealises(p, d, game);
                }
                else game.Org.AddEvidence(p.InJail ? 4f : 2.5f, game);
            }

            // Gossip: true when someone has flipped, now and then a false alarm anyway.
            if (game.Clock.Day - S.LastClueDay >= 2)
            {
                if (AnyFlipped(game) && rng.NextDouble() < 0.45) SendClue(game, rng.NextDouble() < 0.7);
                else if (rng.NextDouble() < 0.05) SendClue(game, false);
            }

            Wiretaps(game);
        }

        Pawn RandomSuspect(Game game, bool truthful)
        {
            var pool = new List<Pawn>();
            foreach (var p in game.Pawns.Crew)
            {
                if (p.Dead || p.Departed || p.IsBoss) continue;
                var d = Of(p);
                if (d.Confirmed) continue;
                if (truthful == (d.Flipped && !d.Feeding)) pool.Add(p);
            }
            return pool.Count == 0 ? null : pool[game.Rng.Next(pool.Count)];
        }

        void SendClue(Game game, bool truthful)
        {
            var suspect = RandomSuspect(game, truthful) ?? RandomSuspect(game, !truthful);
            if (suspect == null) return;
            S.LastClueDay = game.Clock.Day;
            Of(suspect).Suspicion = Mathf.Min(100f, Of(suspect).Suspicion + 25f);
            var (from, nap, eng) = Clues[game.Rng.Next(Clues.Length)];
            var m = game.Phone.Send(game, MsgKind.RatClue, from, string.Format(nap, suspect.Nickname), string.Format(eng, suspect.Nickname),
                $"Gossip about {suspect.DisplayName}. It may be true. It may be envy.", 24f);
            m.Pawn = suspect.Id;
            m.Choices.Add("Set a trap: a false shipment");
            m.Hints.Add($"Tell only {suspect.Nickname} about a load that doesn't exist. If the Finanza turns up, you'll know. 2 days.");
            m.Choices.Add("Keep an eye on him");
            m.Hints.Add("Do nothing for now. Suspicion stays.");
        }

        public void AskAround(Neighbour n, Game game)
        {
            bool truthful = game.Rng.NextDouble() < 0.75;
            var suspect = AnyFlipped(game) ? RandomSuspect(game, truthful) : null;
            if (suspect == null)
            {
                game.Phone.Send(game, MsgKind.Info, n.Name,
                    "Aggio spiato a tutte quante. D''e guagliune vuoste nisciuno parla.",
                    "I asked everyone. Nobody's heard of your boys talking.",
                    "As far as the vicolo knows, the crew is clean.");
                return;
            }
            Of(suspect).Suspicion = Mathf.Min(100f, Of(suspect).Suspicion + 35f);
            var m = game.Phone.Send(game, MsgKind.RatClue, n.Name,
                $"'O nomme ca gira è chillo 'e {suspect.Nickname}. Io nun v'aggio ditto niente.",
                $"The name going round is {suspect.Nickname}. I never told you anything.",
                $"{n.Name} asked around the bar and the market. Whispers point at {suspect.DisplayName}.", 24f);
            m.Pawn = suspect.Id;
            m.Choices.Add("Set a trap: a false shipment");
            m.Hints.Add($"Tell only {suspect.Nickname} about a load that doesn't exist. 2 days.");
            m.Choices.Add("Keep an eye on him");
            m.Hints.Add("Do nothing for now.");
        }

        // ---------- Tests and confrontations ----------

        public bool StartTest(Pawn p, Game game, out string error)
        {
            error = null;
            if (S.TestPawn != 0) error = "A trap is already set. Wait for it.";
            else if (p == null || p.Dead || p.Departed) error = "Nobody to test.";
            if (error != null) return false;
            S.TestPawn = p.Id;
            S.TestDay = game.Clock.Day + 2;
            game.Log.Add($"The boss tells {p.Nickname}, and only him, about a load coming in at the Molo in two days.");
            return true;
        }

        void ResolveTest(Game game)
        {
            var p = game.Pawns.ById(S.TestPawn);
            S.TestPawn = 0;
            if (p == null || p.Dead) return;
            var d = Of(p);
            PhoneMessage m;
            if (d.Flipped && !d.Feeding)
            {
                d.Confirmed = true;
                d.Suspicion = 100f;
                m = game.Phone.Send(game, MsgKind.TestResult, "'O Marenaro",
                    "'A Finanza è venuta 'ncopp' 'a varca vacante. Tre machine, 'e cane, tutte cose.",
                    "The Finanza came for the empty boat. Three cars, the dogs, everything.",
                    $"Only {p.DisplayName} knew about that load. He's talking to the investigators.", 0f);
                m.Pawn = p.Id;
                m.Choices.Add("Make him disappear");
                m.Hints.Add("Lupara bianca. Evidence −10, heat +15, Respect +5. The crew will fear the boss.");
                m.Choices.Add("Throw him out");
                m.Hints.Add("He leaves the family and becomes a pentito: evidence +15.");
                m.Choices.Add("Keep him, feed him lies");
                m.Hints.Add("He doesn't know you know. Each day, his reports mislead the investigators: evidence −1.5. If he works it out...");
                game.Newspaper.Report(game, 3, "Finanza raid at the Molo finds an empty boat", "An anonymous tip sent three patrols and a dog unit to the port. They found nets and a sleeping cat.");
            }
            else
            {
                d.Cleared = true;
                d.Suspicion = 0f;
                d.Pressure = Mathf.Max(0f, d.Pressure - 10f);
                m = game.Phone.Send(game, MsgKind.TestResult, "'O Marenaro",
                    "Niente, Don. 'O mare era calmo e 'a Molo nun s'è visto nisciuno.",
                    "Nothing, Don. The sea was calm and nobody showed up at the Molo.",
                    $"{p.DisplayName} kept his mouth shut. Whatever people say, it wasn't him.");
            }
        }

        public void OnAnswer(PhoneMessage m, int choice, Game game)
        {
            var p = game.Pawns.ById(m.Pawn);
            switch (m.KindOf)
            {
                case MsgKind.RatClue:
                    if (choice == 0 && !StartTest(p, game, out var error)) game.Log.Add(error, MessageKind.Bad);
                    break;
                case MsgKind.TestResult:
                    if (p == null || p.Dead) return;
                    if (choice == 0) Disappear(p, game);
                    else if (choice == 1) Expel(p, game);
                    else
                    {
                        Of(p).Feeding = true;
                        game.Log.Add($"{p.Nickname} hears only what the boss wants him to hear now.", MessageKind.Good);
                    }
                    break;
                case MsgKind.BugClue:
                    if (choice == 0 && !Bonifica(game, out var err)) game.Log.Add(err, MessageKind.Bad);
                    break;
            }
        }

        public void Confront(Pawn p, Game game)
        {
            var d = Of(p);
            var boss = game.Org.Boss(game);
            int intimidation = boss != null ? boss.IntimidationSkill : 3;
            long now = game.Ticks.TicksGame;
            if (d.Flipped && !d.Feeding)
            {
                if (game.Rng.NextDouble() < 0.3 + intimidation * 0.04)
                {
                    d.Confirmed = true;
                    d.Suspicion = 100f;
                    game.Log.Add($"{p.Nickname} broke down in the back room and confessed: he's been talking to the police.", MessageKind.Bad);
                }
                else
                {
                    d.Suspicion = Mathf.Max(0f, d.Suspicion - 15f);
                    game.Log.Add($"{p.Nickname} swore on his mother he never said a word.");
                }
                return;
            }
            // Innocent, and accused.
            p.Loyalty = Mathf.Max(0f, p.Loyalty - 15f);
            d.Pressure += 10f;
            d.Suspicion = Mathf.Max(0f, d.Suspicion - 25f);
            p.Mood.AddMemory("accused", "The boss called me a rat", -12f, now, GameClock.TicksPerDay * 3);
            foreach (var o in game.Pawns.Crew)
                if (o != p && o.IsPresent && !o.IsBoss) o.Loyalty = Mathf.Max(0f, o.Loyalty - 2f);
            game.Log.Add($"{p.Nickname} swore on his mother he never said a word. He looked hurt. The others noticed.", MessageKind.Bad);
        }

        public void Disappear(Pawn p, Game game)
        {
            var d = Of(p);
            bool guilty = d.Flipped;
            long now = game.Ticks.TicksGame;
            var zone = game.Territory.ZoneAt(p.Cell);
            p.Die("disappearance", game);
            ReleaseRoles(p, game);
            game.Org.Respect = Mathf.Min(100f, game.Org.Respect + 5f);
            game.Territory.AddHeat(Vector2Int.RoundToInt(game.PlayerBase.center), 15f);
            if (guilty) game.Org.Evidence = Mathf.Max(0f, game.Org.Evidence - 10f);
            else game.Org.AddEvidence(6f, game);
            foreach (var o in game.Pawns.Crew)
            {
                if (!o.IsPresent || o.IsBoss) continue;
                o.Mood.AddMemory($"gone_{p.Id}", $"{p.Nickname} vanished", -6f, now, GameClock.TicksPerDay * 3);
                o.Loyalty = Mathf.Clamp(o.Loyalty + (guilty && d.Confirmed ? 4f : -12f), 0f, 100f);
                Of(o).Pressure = Mathf.Max(0f, Of(o).Pressure - 10f); // fear works, for a while
            }
            game.Log.Add($"{p.DisplayName} went out one evening and never came home.", MessageKind.Bad);
            if (!guilty) game.Log.Add($"Weeks later, the vicolo whispers that {p.Nickname} never talked to anyone.", MessageKind.Bad);
            game.Newspaper.Report(game, 5, $"Young man missing in {zone?.Name ?? "the rione"}",
                $"{p.FirstName} {p.LastName} has not been seen for days. His mother has hung his photo on the door of the church. Investigators fear a lupara bianca.");
        }

        public void Expel(Pawn p, Game game)
        {
            var d = Of(p);
            ReleaseRoles(p, game);
            p.Departed = true;
            if (d.Flipped)
            {
                game.Org.AddEvidence(15f, game);
                game.Log.Add($"{p.DisplayName} left the family and walked straight into the prosecutor's office. A new pentito.", MessageKind.Bad);
                game.Newspaper.Report(game, 6, "A new pentito in the rione", $"{p.FirstName} {p.LastName[0]}. is said to be filling notebooks for the anti-mafia prosecutors. The family he served has gone quiet.");
            }
            else game.Log.Add($"{p.DisplayName} was told to leave the family. He went without a word.", MessageKind.Bad);
        }

        void RatRealises(Pawn p, InformantData d, Game game)
        {
            d.Feeding = false;
            ReleaseRoles(p, game);
            p.Departed = true;
            game.Org.AddEvidence(18f, game);
            game.Log.Add($"{p.DisplayName} worked out he was being played and ran to the investigators. Everything he knows, they know.", MessageKind.Bad);
            game.Newspaper.Report(game, 6, "Informer under police protection", "A young man close to a local family has entered the witness protection programme, sources say.");
        }

        static void ReleaseRoles(Pawn p, Game game)
        {
            p.SetDrafted(false, game);
            foreach (var prop in game.Properties.All)
                if (prop.Manager == p) prop.Manager = null;
            if (game.Org.Courier == p) game.Org.Courier = null;
        }

        // ---------- Wiretaps ----------

        void Wiretaps(Game game)
        {
            var rng = game.Rng;
            double chance = game.Org.Evidence / 300.0 + (AnyFlipped(game) ? 0.08 : 0.0);
            if (rng.NextDouble() < chance)
            {
                var owned = new List<Vector2Int>();
                foreach (var prop in game.Properties.Owned)
                    if (!S.Bugs.Contains(prop.Door)) owned.Add(prop.Door);
                if (!game.Phone.S.Bugged && (owned.Count == 0 || rng.NextDouble() < 0.35)) game.Phone.S.Bugged = true;
                else if (owned.Count > 0) S.Bugs.Add(owned[rng.Next(owned.Count)]);
            }

            // Every bugged room hears something.
            game.Org.AddEvidence(S.Bugs.Count * 1f, game);

            if (BugCount(game) > 0 && rng.NextDouble() < 0.25)
            {
                bool phone = game.Phone.S.Bugged && (S.Bugs.Count == 0 || rng.NextDouble() < 0.5);
                var m = game.Phone.Send(game, MsgKind.BugClue, "'O Tecnico",
                    phone ? "Quanno ve chiammo se sente 'nu fruscio. Nun me piace."
                          : "Ce sta 'nu furgone d''a Telecom fermo sotto 'o palazzo vuosto 'a tre ghiuorne. 'A Telecom nun manna maje a nisciuno.",
                    phone ? "When I call you there's a hiss on the line. I don't like it."
                          : "A Telecom van's been parked under your building for three days. Telecom never sends anyone.",
                    "Someone may be listening. A sweep finds most bugs.", 24f);
                m.Choices.Add($"Call for a sweep (€{Prices.Bonifica})");
                m.Hints.Add("'O Tecnico checks every property and the phone.");
                m.Choices.Add("Ignore it");
                m.Hints.Add("Maybe it's nothing.");
            }
        }

        public bool Bonifica(Game game, out string error)
        {
            if (!game.Org.Pay(Prices.Bonifica, game, out error)) return false;
            int found = 0;
            for (int i = S.Bugs.Count - 1; i >= 0; i--)
            {
                if (game.Rng.NextDouble() < 0.85)
                {
                    S.Bugs.RemoveAt(i);
                    found++;
                }
            }
            if (game.Phone.S.Bugged && game.Rng.NextDouble() < 0.9)
            {
                game.Phone.S.Bugged = false;
                found++;
            }
            game.Log.Add(found > 0
                ? $"'O Tecnico found {found} bug{(found > 1 ? "s" : "")}, small as a button. Someone was listening."
                : "'O Tecnico swept everything and found nothing.", found > 0 ? MessageKind.Bad : MessageKind.Good);
            if (found > 0) game.Newspaper.Report(game, 2, "Bugs found, investigation exposed?", "Sources in the prosecutor's office are furious: months of surveillance in the rione may have been wasted.");
            return true;
        }
    }
}
