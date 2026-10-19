using System.Collections.Generic;
using OSistema.Crime;
using OSistema.World;
using UnityEngine;

namespace OSistema.Core
{
    /// <summary>
    /// Interiors follow fortune. Once an hour: a shop whose luck has changed is fitted out again for
    /// it (burned: wreckage; paying the pizzo: squeezed, the extras gone; free: ordinary; the
    /// family's front: done up), and as the family climbs, mamma does up the basso a little more.
    /// Nobody gets furniture dropped on their head: the cells people stand in are kept clear.
    /// </summary>
    public static class Interiors
    {
        public static int TierOf(Business b) => b.Status switch
        {
            BusinessStatus.Closed => 0,
            BusinessStatus.Paying => 1,
            BusinessStatus.Owned => 3,
            _ => 2,
        };

        public static void Hourly(Game game)
        {
            HashSet<Vector2Int> occupied = null;
            foreach (var b in game.Org.Businesses)
            {
                int tier = TierOf(b);
                if (b.Fitted == tier) continue;
                var prop = game.Properties.At(b.Cell);
                var counter = BuildingDefs.ById(b.CounterDefId);
                if (prop == null || counter == null) { b.Fitted = tier; continue; }
                occupied ??= Occupied(game);
                var at = RoomPlanner.FitShop(game.Map, prop, counter, tier, RoomPlanner.SeedOf(prop), occupied);
                if (at.HasValue) b.Cell = at.Value;
                b.Fitted = tier;
            }

            // A save from before the planner: everybody else's home is laid out properly, once.
            if (game.Org.DressedRank < 0) RefurnishHomes(game, occupied ??= Occupied(game));

            int rank = game.Org.Rank;
            if (rank > game.Org.DressedRank)
            {
                var home = game.PlayerBase.width > 0 ? game.Properties.At(Vector2Int.RoundToInt(game.PlayerBase.center)) : null;
                if (home != null)
                {
                    int added = RoomPlanner.DressFamily(game.Map, home, rank, RoomPlanner.SeedOf(home), occupied ?? Occupied(game));
                    if (added > 0 && game.Org.DressedRank >= 0)
                        game.Log.Add(added == 1 ? "Mammà has bought something new for the basso." : "Mammà has done up the basso: the family is going up in the world.", MessageKind.Good);
                }
                game.Org.DressedRank = rank;
            }
        }

        static void RefurnishHomes(Game game, HashSet<Vector2Int> occupied)
        {
            foreach (var p in game.Properties.All)
            {
                if (p.Kind != PropertyKind.Home || p.OwnedByPlayer || p.Operation != Operation.None) continue;
                var inner = p.Interior;
                if (inner.width * inner.height > 100 || inner.Overlaps(game.PlayerBase)) continue;   // warehouses, the family's own
                int seed = RoomPlanner.SeedOf(p);
                double f = new System.Random(seed).NextDouble();
                RoomPlanner.FurnishHome(game.Map, p, f < 0.25 ? 0 : f < 0.7 ? 1 : f < 0.95 ? 2 : 3, seed, occupied);
            }
        }

        static HashSet<Vector2Int> Occupied(Game game)
        {
            var cells = new HashSet<Vector2Int>();
            foreach (var p in game.Pawns.All) if (p.IsPresent) cells.Add(p.Cell);
            foreach (var n in game.Npcs.All) if (!n.Despawned) cells.Add(n.Mover.Cell);
            return cells;
        }
    }
}
