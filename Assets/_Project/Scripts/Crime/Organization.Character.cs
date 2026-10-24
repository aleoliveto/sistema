using System.Collections.Generic;
using System.Linq;
using OSistema.Core;
using OSistema.Pawns;
using OSistema.Story;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// The crew's characters at work, once a day: the loyal grow more so, the hothead starts fights at the piazza,
    /// the greedy skims and sometimes gets caught, the one who talks too much is heard by the wrong people, and
    /// the ambitious capo with no love left for the family may take his piazza over to a rival clan.
    /// </summary>
    public sealed partial class Organization
    {
        readonly Dictionary<int, int> skimmed = new Dictionary<int, int>();       // pawn id → euro taken since last caught
        readonly Dictionary<int, int> splitWarned = new Dictionary<int, int>();   // pawn id → day the family heard of his plans

        void CharacterDaily(Game game)
        {
            var rng = game.Rng;
            long now = game.Ticks.TicksGame;
            foreach (var p in game.Pawns.Crew.ToList())
            {
                if (!p.IsPresent || p.Controlled || p.IsBoss) continue;
                var sq = SquareOf(p, game);
                switch (p.Character)
                {
                    case Trait.Loyal:
                        p.Loyalty = Mathf.Min(100f, p.Loyalty + 0.6f);
                        break;
                    case Trait.Ambitious:
                        if (p.Wage < 120 + 40 * Rank)
                            p.Mood.AddMemory("underpaid", "Paid less than he's worth", -4f, now, GameClock.TicksPerDay);
                        if (p.Role == CrewRole.Capo) AmbitiousCapo(p, sq, game);
                        break;
                    case Trait.Hothead:
                        if (sq != null && rng.NextDouble() < 0.12)
                        {
                            game.Territory.AddHeat(sq.Cell, 4f);
                            Respect = Mathf.Min(100f, Respect + 0.5f);
                            game.Log.Note($"{p.DisplayName} beat up a customer at the piazza who talked back. People saw.", MessageKind.Info, sq.Cell);
                        }
                        break;
                    case Trait.Greedy:
                        Skim(p, sq, game);
                        break;
                    case Trait.Chatty:
                        if (p.Loyalty < 55f)
                        {
                            game.Informants.Of(p).Pressure += 2f;
                            LeakRisk += 1f;
                        }
                        break;
                }
            }
        }

        void Skim(Pawn p, DrugSquare sq, Game game)
        {
            var rng = game.Rng;
            bool handsOnMoney = p.Role == CrewRole.Pusher || p.Role == CrewRole.Capo || p.Role == CrewRole.Collector || p.Role == CrewRole.Manager || p.Role == CrewRole.Vendor;
            if (!handsOnMoney || rng.NextDouble() > 0.6) return;
            int take = sq != null ? Mathf.Max(10, sq.RevenueToday / 25) : rng.Next(10, 40);
            take = Mathf.Min(take, game.Stock.Cash);
            if (take <= 0) return;
            game.Stock.Cash -= take;
            skimmed.TryGetValue(p.Id, out int sum);
            skimmed[p.Id] = sum += take;
            if (sum >= 250 && rng.NextDouble() < 0.35) game.Events.Raise(CaughtSkimming(p, sum, game));
        }

        GameEvent CaughtSkimming(Pawn p, int sum, Game game)
        {
            skimmed[p.Id] = 0;
            return new GameEvent
            {
                Id = "skimming",
                Title = $"{p.Nickname}'s pockets",
                Neapolitan = "'E cunte nun tornano, Don.",
                English = "The numbers don't add up, Don.",
                Body = $"The takings have been short for days. {p.FullName} has a new watch and a new phone. Somebody counted: about €{sum} gone.",
            }
            .Option("Let it go this time", "He knows you know. Loyalty +5; he'll be more careful.", g => p.Loyalty = Mathf.Min(100f, p.Loyalty + 5f))
            .Option("Make him pay it back, with interest", $"€{sum} back. Loyalty −10, mood −8. The others see the rules hold.", g =>
            {
                g.Stock.Cash += sum;
                p.Loyalty = Mathf.Max(0f, p.Loyalty - 10f);
                p.Mood.AddMemory("caught_skimming", "Caught with his hand in the till", -8f, g.Ticks.TicksGame, GameClock.TicksPerDay * 2);
            })
            .Option("Throw him out of the family", "He goes, and he's bitter: evidence +4.", g =>
            {
                p.EndCurrentJob(g);
                p.Departed = true;
                g.Org.AddEvidence(4f, g);
                g.Log.Add($"{p.FullName} has been thrown out of the family.", MessageKind.Info);
            });
        }

        /// <summary>An ambitious capo with little love for the family: first word reaches you, then, if nothing changes, he goes.</summary>
        void AmbitiousCapo(Pawn capo, DrugSquare sq, Game game)
        {
            if (sq == null) return;
            int day = game.Clock.Day;
            if (capo.Loyalty >= 45f)
            {
                splitWarned.Remove(capo.Id);
                return;
            }
            if (!splitWarned.TryGetValue(capo.Id, out int heard))
            {
                if (game.Rng.NextDouble() < 0.3)
                {
                    splitWarned[capo.Id] = day;
                    game.Events.Raise(CapoPlans(capo, sq, game));
                }
                return;
            }
            if (day - heard >= 3 && game.Rng.NextDouble() < 0.5) SplitOff(capo, sq, game);
        }

        GameEvent CapoPlans(Pawn capo, DrugSquare sq, Game game)
        {
            string zone = game.Territory.Zones[sq.ZoneId].Name;
            int raise = 100;
            return new GameEvent
            {
                Id = "capo_plans",
                Title = $"{capo.Nickname} has plans of his own",
                Neapolitan = "Chillo s'è fatto 'e cunte suoje, Don.",
                English = "That one has done his own sums, Don.",
                Body = $"Word from the vicolo: {capo.FullName}, who runs the piazza in {zone}, has been seen talking with another clan's people. He thinks the piazza would do better without you.",
            }
            .Option($"Raise his wage by €{raise} a week", "Loyalty +15. Ambition is expensive.", g =>
            {
                capo.Wage += raise;
                capo.Loyalty = Mathf.Min(100f, capo.Loyalty + 15f);
            })
            .Option("A cut of the piazza's takings", "Loyalty +25, respect −2: the others hear he got a share.", g =>
            {
                capo.Loyalty = Mathf.Min(100f, capo.Loyalty + 25f);
                g.Org.Respect = Mathf.Max(0f, g.Org.Respect - 2f);
            })
            .Option("Remind him who's boss", game.Org.Respect >= 50f ? "Your name is enough: loyalty +8." : "Your name may not be enough yet. He could go now.", g =>
            {
                if (g.Org.Respect >= 50f) capo.Loyalty = Mathf.Min(100f, capo.Loyalty + 8f);
                else g.Org.SplitOff(capo, sq, g);
            })
            .Option("Do nothing", "See what he does.", g => { });
        }

        /// <summary>The capo walks, and his piazza and the people loyal to him go to the nearest clan.</summary>
        void SplitOff(Pawn capo, DrugSquare sq, Game game)
        {
            splitWarned.Remove(capo.Id);
            if (!Squares.Contains(sq) || !capo.IsPresent) return;
            var followers = CrewOf(sq, game).Where(p => p != capo && p.Loyalty < 55f && !p.Controlled).ToList();
            followers.Add(capo);
            string zone = game.Territory.Zones[sq.ZoneId].Name;
            RivalClan clan = null;
            float best = float.MaxValue;
            foreach (var c in game.Clans)
            {
                if (c.Destroyed) continue;
                foreach (var their in c.Squares)
                {
                    float d = (their.Cell - sq.Cell).sqrMagnitude;
                    if (d < best) { best = d; clan = c; }
                }
            }
            CloseSquareAt(sq.Cell);
            foreach (var p in followers)
            {
                p.EndCurrentJob(game);
                p.Departed = true;
            }
            Respect = Mathf.Max(0f, Respect - 6f);
            if (clan != null)
            {
                clan.Squares.Add(new DrugSquare { Cell = sq.Cell, ZoneId = sq.ZoneId, Rival = true, OpenHour = 0, CloseHour = 0, Clan = clan.Id });
                clan.Soldiers += followers.Count;
                clan.Hostility = Mathf.Min(100f, clan.Hostility + 15f);
                game.Log.Add($"{capo.FullName} has gone over to the {clan.Name}, and taken the piazza in {zone} with him{(followers.Count > 1 ? $", and {followers.Count - 1} of your people" : "")}.", MessageKind.Bad, sq.Cell);
            }
            else game.Log.Add($"{capo.FullName} has walked out with {followers.Count - 1} of your people. The piazza in {zone} is empty.", MessageKind.Bad, sq.Cell);
            game.Newspaper.Report(game, 4, $"A split in the clan of {zone}", $"Investigators note a new face in charge of the drug market in {zone}: a former lieutenant who has changed sides.");
        }
    }
}
