using System.Collections.Generic;
using OSistema.AI;
using OSistema.Core;
using OSistema.Npcs;
using OSistema.Pawns;
using UnityEngine;

namespace OSistema.Crime
{
    public sealed class Raid
    {
        public DrugSquare Square;
        public readonly List<Npc> Cops = new List<Npc>();
        public bool Alerted;
        public bool Arrived;
    }

    /// <summary>
    /// Police pressure. Heat in a zone makes raids on its piazza likelier. Lookouts who spot the
    /// patrol in time let the pushers run; without them, the police walk right up to the spot.
    /// </summary>
    public sealed class Police
    {
        public const float ArrestDistance = 1.3f;

        public readonly List<Raid> Active = new List<Raid>();

        public bool IsRaiding(DrugSquare square) => Active.Exists(r => r.Square == square);

        public static bool IsExposed(Pawn p, DrugSquare square) =>
            p.CanAct &&
            ((p.CurrentJob is PostJob post && post.Square == square) ||
             (p.CurrentJob is FleeJob flee && flee.Square == square));

        public void HourlyCheck(Game game)
        {
            ReleasePrisoners(game);

            foreach (var sq in game.Org.Squares)
            {
                if (IsRaiding(sq)) continue;
                float heat = game.Territory.Zones[sq.ZoneId].Heat;
                float chance = Mathf.Clamp01((heat - 25f) / 75f) * 0.3f;
                if (game.Rng.NextDouble() < chance) StartRaid(sq, game);
            }
        }

        void StartRaid(DrugSquare square, Game game)
        {
            if (game.Territory.Entrances.Count == 0) return;
            var raid = new Raid { Square = square };
            float heat = game.Territory.Zones[square.ZoneId].Heat;
            int cops = heat > 70f ? 3 : 2;
            var entrance = NearbyEntrance(square, game);
            for (int i = 0; i < cops; i++) raid.Cops.Add(game.Npcs.SpawnPolice(raid, entrance, game));
            Active.Add(raid);
        }

        // One of the five closest entrances: close enough to be a threat, random enough to surprise.
        static Vector2Int NearbyEntrance(DrugSquare square, Game game)
        {
            var list = new List<Vector2Int>(game.Territory.Entrances);
            list.Sort((a, b) => (a - square.Cell).sqrMagnitude.CompareTo((b - square.Cell).sqrMagnitude));
            return list[game.Rng.Next(Mathf.Min(5, list.Count))];
        }

        public void Tick(Game game)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var raid = Active[i];
                if (!raid.Alerted && game.Ticks.TicksGame % 30 == 0) CheckDetection(raid, game);
                CheckArrests(raid, game);
                if (raid.Cops.TrueForAll(c => c.Despawned)) Active.RemoveAt(i);
            }
        }

        void CheckDetection(Raid raid, Game game)
        {
            foreach (var pawn in game.Pawns.Crew)
            {
                if (!pawn.CanAct || !(pawn.CurrentJob is PostJob post) || post.Square != raid.Square || !post.AtPost) continue;
                // Vedette watch the approaches; a pusher only notices when the police are almost on him.
                float range = post.Role == CrewRole.Lookout ? 10f + 1.5f * pawn.AwarenessSkill : 3f + 0.3f * pawn.AwarenessSkill;
                foreach (var cop in raid.Cops)
                {
                    if (cop.Despawned || (cop.Mover.Position - pawn.Position).magnitude > range) continue;
                    RaiseAlarm(raid, pawn, post.Role != CrewRole.Lookout, game);
                    return;
                }
            }
        }

        void RaiseAlarm(Raid raid, Pawn spotter, bool late, Game game)
        {
            raid.Alerted = true;
            spotter.Say("police");
            var zone = game.Territory.Zones[raid.Square.ZoneId];
            game.Log.Add(late
                ? $"Police raid on the piazza in {zone.Name}! No lookout saw them coming."
                : $"Police raid on the piazza in {zone.Name}! {spotter.Nickname} raised the alarm.", MessageKind.Bad, raid.Square.Cell);

            foreach (var pawn in game.Pawns.Crew)
            {
                if (!pawn.CanAct || !(pawn.CurrentJob is PostJob post) || post.Square != raid.Square) continue;
                // Caught by surprise: anyone the police are already on top of may not get away.
                if (late && NearestCopDistance(raid, pawn) < 3.5f && game.Rng.NextDouble() < 0.6)
                {
                    var cop = raid.Cops.Find(c => !c.Despawned && !c.HasArrested);
                    if (cop != null)
                    {
                        Arrest(pawn, raid, game);
                        cop.HasArrested = true;
                        continue;
                    }
                }
                pawn.ForceJob(new FleeJob(pawn, raid.Square, game), game);
            }
        }

        static float NearestCopDistance(Raid raid, Pawn pawn)
        {
            float best = float.MaxValue;
            foreach (var cop in raid.Cops)
                if (!cop.Despawned) best = Mathf.Min(best, (cop.Mover.Position - pawn.Position).magnitude);
            return best;
        }

        void CheckArrests(Raid raid, Game game)
        {
            foreach (var cop in raid.Cops)
            {
                if (cop.Despawned || cop.HasArrested) continue;
                foreach (var pawn in game.Pawns.Crew)
                {
                    if (!IsExposed(pawn, raid.Square)) continue;
                    // The careful ones keep a step further off: they have to be cornered.
                    float reach = pawn.Character == Trait.Careful ? ArrestDistance * 0.55f : ArrestDistance;
                    if ((cop.Mover.Position - pawn.Position).magnitude > reach) continue;
                    Arrest(pawn, raid, game);
                    cop.HasArrested = true;
                    break;
                }
            }
        }

        void Arrest(Pawn pawn, Raid raid, Game game)
        {
            // They search him: what he carries is gone. The stash at the basso is another matter.
            int seized = pawn.PocketDoses;
            pawn.PocketDoses = pawn.PocketPacks = pawn.PocketFakes = 0;

            int days = game.Rng.Next(3, 10) + (pawn.Armed ? 5 : 0); // caught with a gun: longer inside
            long now = game.Ticks.TicksGame;
            game.StreetLife?.Gather(pawn.Position, 1200, "the arrest", game);   // the whole vicolo comes out to watch them take him
            pawn.SendToJail(now + days * GameClock.TicksPerDay, game);
            game.Org.Respect = Mathf.Max(0f, game.Org.Respect - 4f);

            string seizedText = seized > 0 ? $" {seized} doses were found on him." : "";
            string gunText = pawn.Armed ? " He was carrying a gun." : "";
            game.Log.Add($"{pawn.DisplayName} was arrested at the piazza and taken to Poggioreale for {days} days.{seizedText}{gunText}", MessageKind.Bad);

            // Every arrest is a chance to talk. Inside, the investigators lean on him every day.
            game.Org.AddEvidence(6f, game);
            if (pawn.Young)
            {
                // A sixteen-year-old pushing at a piazza: the papers, the prosecutors, the minister.
                game.Org.AddEvidence(6f, game);
                game.Newspaper.Report(game, 5, "Baby paranza: a minor arrested at a drug market", "He is sixteen. The juvenile court prosecutor speaks of children used as soldiers.");
            }
            if (!pawn.IsBoss) game.Informants.Of(pawn).Pressure += (pawn.Loyalty < 40f ? 30f : 12f) *
                (pawn.Character == Trait.Loyal ? 0.5f : pawn.Character == Trait.Chatty ? 1.6f : 1f);
            var zone = game.Territory.Zones[raid.Square.ZoneId];
            game.Newspaper.Report(game, 3, $"Arrest at a drug market in {zone.Name}",
                $"{pawn.FirstName} {pawn.LastName[0]}. was taken to Poggioreale after a raid.{(seized > 0 ? $" Police seized {seized} doses." : "")}{(pawn.Armed ? " He was carrying a pistol." : "")}");

            foreach (var other in game.Pawns.Crew)
                if (other != pawn && other.IsPresent)
                    other.Mood.AddMemory($"arrested_{pawn.Id}", $"{pawn.Nickname} got arrested", -6f, now, GameClock.TicksPerDay * 2);
        }

        /// <summary>Called by a cop when the patrol reaches the square.</summary>
        public void OnPatrolArrived(Raid raid, Game game)
        {
            if (raid.Arrived) return;
            raid.Arrived = true;
            var zone = game.Territory.Zones[raid.Square.ZoneId];
            zone.Heat = Mathf.Max(0f, zone.Heat - 25f);
            if (!raid.Alerted) game.Log.Add($"The police swept the piazza in {zone.Name}.", MessageKind.Bad);
        }

        void ReleasePrisoners(Game game)
        {
            long now = game.Ticks.TicksGame;
            foreach (var pawn in game.Pawns.Crew)
            {
                if (!pawn.InJail || pawn.Dead || now < pawn.JailedUntil) continue;
                var home = game.PlayerBase;
                var cell = Vector2Int.RoundToInt(home.center);
                for (int i = 0; i < 20; i++)
                {
                    var c = new Vector2Int(game.Rng.Next(home.xMin, home.xMax), game.Rng.Next(home.yMin, home.yMax));
                    if (game.Map.IsPassable(c))
                    {
                        cell = c;
                        break;
                    }
                }
                pawn.ReleaseFromJail(cell);
                pawn.Mood.AddMemory("freed", "Out of prison", 8f, now, GameClock.TicksPerDay);
                pawn.Say("freed");
                game.Log.Add($"{pawn.Nickname} is out of Poggioreale and back at the basso.", MessageKind.Good);
            }
        }
    }
}
