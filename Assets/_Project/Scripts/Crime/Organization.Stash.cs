using System.Collections.Generic;
using OSistema.Core;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// How a piazza really runs: the product sleeps in a nascondiglio, a flat near the piazza; the pusher keeps only
    /// a few doses on him; a runner (cavallino) brings more from the flat as he sells out, and keeps the flat stocked
    /// from the basso. Arrest the pusher and you find five doses; find the flat and you find the lot.
    /// And rentals: a flat for a few euro a week instead of thousands, with a landlord who notices things.
    /// </summary>
    public sealed partial class Organization
    {
        /// <summary>How far a nascondiglio serves a piazza (cells, door to piazza).</summary>
        public const int StashReach = 45;
        /// <summary>A pusher with a runner keeps this few on him; the runner tops him up.</summary>
        public const int PusherWithRunner = 5;
        /// <summary>What a runner carries from the stash for the pushers, and to restock a nascondiglio from the basso.</summary>
        public const int RunnerLoad = 15, RestockLoad = 40, RestockBelow = 20;

        // ---------- The stash ----------

        public IEnumerable<Property> Nascondigli(Game game)
        {
            foreach (var p in game.Properties.Owned) if (p.Operation == Operation.Nascondiglio) yield return p;
        }

        /// <summary>The nascondiglio that serves a piazza (the nearest in reach), or null: the stash at the basso.</summary>
        public Property StashFor(DrugSquare sq, Game game)
        {
            Property best = null;
            int bestD = StashReach + 1;
            if (sq == null) return null;
            foreach (var p in Nascondigli(game))
            {
                int d = GridUtil.Manhattan(p.Door, sq.Cell);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        public int DosesIn(Property stash, Game game) => stash == null ? game.Stock.Product : stash.StashDoses;

        /// <summary>Takes up to n doses out of a stash (null: the basso). Returns how many came out.</summary>
        public int TakeDoses(Property stash, int n, Game game)
        {
            n = Mathf.Min(n, DosesIn(stash, game));
            if (n <= 0) return 0;
            if (stash == null) game.Stock.Product -= n; else stash.StashDoses -= n;
            return n;
        }

        public void PutDoses(Property stash, int n, Game game)
        {
            if (n <= 0) return;
            if (stash == null) game.Stock.Product += n; else stash.StashDoses += n;
        }

        /// <summary>A cell inside a stash to walk to: by the safe if there is one.</summary>
        public Vector2Int StashCellOf(Property stash, Game game)
        {
            if (stash == null) return StashCell(game);
            var r = stash.Interior;
            foreach (var c in game.Map.PositionsOf(BuildingDefs.Safe)) if (r.Contains(c)) return c;
            foreach (var c in GridUtil.CellsAround(Vector2Int.RoundToInt(r.center), 4))
                if (r.Contains(c) && game.Map.IsPassable(c)) return c;
            return stash.Door;
        }

        /// <summary>The nascondiglio the boss is standing in, if any.</summary>
        public Property NascondiglioAt(Vector2Int c, Game game)
        {
            var p = game.Properties.At(c);
            return p != null && p.OwnedByPlayer && p.Operation == Operation.Nascondiglio && p.Interior.Contains(c) ? p : null;
        }

        /// <summary>A runner working this piazza right now.</summary>
        public Pawn RunnerFor(DrugSquare sq, Game game)
        {
            foreach (var p in game.Pawns.Crew)
                if (p.CanAct && p.Role == CrewRole.Runner && SquareOf(p, game) == sq && AI.JobGiver.OnShift(p, game)) return p;
            return null;
        }

        /// <summary>Every day: the police may find a nascondiglio. Hotter rioni and rented flats are riskier.</summary>
        void SearchNascondigli(Game game)
        {
            foreach (var p in new List<Property>(Nascondigli(game)))
            {
                if (p.StashDoses <= 0) continue;
                var zone = game.Territory.ZoneAt(p.Door);
                float chance = 0.01f + (zone != null ? zone.Heat / 100f * 0.08f : 0f) + (p.Rent > 0 ? 0.02f : 0f);
                if (game.Rng.NextDouble() >= chance) continue;
                int lost = p.StashDoses;
                p.StashDoses = 0;
                AddEvidence(4f, game);
                game.Territory.AddHeat(p.Door, 5f);
                game.Log.Add($"The police searched {p.Name}: {lost} doses found in the nascondiglio and taken. Nobody was there to arrest.", MessageKind.Bad, p.Door);
            }
        }

        // ---------- Rentals ----------

        public static int RentFor(Property p) => Mathf.Max(60, Mathf.RoundToInt(p.Price / 40f / 10f) * 10);

        public bool CanRent(Property p, Game game, out string error)
        {
            error = null;
            if (p.OwnedByPlayer) error = "Already yours.";
            else if (p.Kind != PropertyKind.Home) error = "Only homes are let.";
            else if (!CanExpandInto(game.Territory.ZoneAt(p.Door), game)) error = "Too far from your turf. Expand into the next rione first.";
            return error == null;
        }

        /// <summary>Rent a flat: the first week and a week's deposit now, then every Sunday.</summary>
        public bool RentProperty(Property p, Game game, out string error)
        {
            if (!CanRent(p, game, out error)) return false;
            int rent = RentFor(p);
            if (!Pay(rent * 2, game, out error)) return false;
            p.OwnedByPlayer = true;
            p.Rent = rent;
            p.RentLate = false;
            WarnIfRivalZone(game.Territory.ZoneAt(p.Door), game);
            game.Log.Add($"You rent {p.Name}: €{rent} a week, paid on Sundays. The landlord lives upstairs and notices who comes and goes.", MessageKind.Good, p.Door);
            return true;
        }

        /// <summary>The keys go back (or the locks are changed): the family's people move out, and whatever was hidden there is gone.</summary>
        public void GiveUpRental(Property p, Game game, string why)
        {
            if (p.Rent <= 0) return;
            int lost = p.StashDoses;
            if (why == null) PutDoses(null, lost, game);   // you leave on your own terms: the stash comes home
            p.StashDoses = 0;
            p.Rent = 0;
            p.RentLate = false;
            p.OwnedByPlayer = false;
            if (p.Manager != null && p.Manager.Role == CrewRole.Manager) p.Manager.Role = CrewRole.General;
            p.Manager = null;
            p.Operation = Operation.None;
            foreach (var c in game.Pawns.Crew)
                if (c.Home.HasValue && p.Rect.Contains(c.Home.Value)) Housing.MoveIn(c, null);
            if (why == null) game.Log.Add($"You hand back the keys to {p.Name}. Everyone sleeps at the basso again.", MessageKind.Info, p.Door);
            else game.Log.Add($"{why} Out of {p.Name}{(lost > 0 ? $", and {lost} doses with it" : "")}. Everyone who lived there is back at the basso.", MessageKind.Bad, p.Door);
        }

        /// <summary>Sunday: the rent. Miss one and he warns you; miss two and he changes the locks.</summary>
        void PayRents(Game game)
        {
            foreach (var p in new List<Property>(game.Properties.Owned))
            {
                if (p.Rent <= 0) continue;
                if (Pay(p.Rent, game, out _)) { p.RentLate = false; continue; }
                if (!p.RentLate)
                {
                    p.RentLate = true;
                    game.Log.Add($"No rent for {p.Name} this week (€{p.Rent}). The landlord wants it by next Sunday.", MessageKind.Bad, p.Door);
                }
                else GiveUpRental(p, game, "Two weeks without rent: the landlord changed the locks.");
            }
        }

        /// <summary>
        /// Every day: a landlord who sees too many young men on the stairs. Quiet flats rarely bother him; an
        /// operation does. He raises the rent, throws you out, or calls the police.
        /// </summary>
        void LandlordsDaily(Game game)
        {
            foreach (var p in new List<Property>(game.Properties.Owned))
            {
                if (p.Rent <= 0) continue;
                var zone = game.Territory.ZoneAt(p.Door);
                float chance = p.Operation != Operation.None ? 0.05f : 0.008f;
                chance += Housing.Residents(p, game).Count > 2 ? 0.01f : 0f;
                chance += zone != null ? zone.Heat / 100f * 0.03f : 0f;
                if (game.Rng.NextDouble() >= chance) continue;
                double r = game.Rng.NextDouble();
                if (r < 0.5 || p.Operation == Operation.None)
                {
                    int was = p.Rent;
                    p.Rent = Mathf.RoundToInt(p.Rent * 1.5f / 10f) * 10;
                    game.Log.Add($"The landlord of {p.Name} has noticed the comings and goings. To keep quiet, the rent goes from €{was} to €{p.Rent} a week.", MessageKind.Bad, p.Door);
                }
                else if (r < 0.78) GiveUpRental(p, game, "The landlord doesn't want \"that sort of thing\" in his building and wants you out by morning.");
                else
                {
                    AddEvidence(5f, game);
                    game.Territory.AddHeat(p.Door, 8f);
                    GiveUpRental(p, game, "The landlord called the police. They searched the flat and the lease is over.");
                }
            }
        }
    }
}
