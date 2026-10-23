using System.Collections.Generic;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Core
{
    /// <summary>
    /// Where each of the crew lives: the family basso, or one of the homes the family owns. Nobody owns
    /// a bed. The beds of a house are shared by whoever lives there, so the day team sleeps in the beds
    /// the night team has just got out of. A pawn's <see cref="Pawn.Bed"/> is only the bed they're in now.
    /// </summary>
    public static class Housing
    {
        /// <summary>The owned home this person lives in, or null for the basso (also when that home was sold).</summary>
        public static Property HomeOf(Pawn p, Game game)
        {
            if (!p.Home.HasValue) return null;
            var prop = game.Properties.At(p.Home.Value);
            return prop != null && prop.OwnedByPlayer && prop.Kind == PropertyKind.Home ? prop : null;
        }

        /// <summary>The floor of a house: an owned home's interior, or the basso (null).</summary>
        public static RectInt Rooms(Property house, Game game) => house != null ? house.Interior : game.PlayerBase;

        public static bool InHouse(Vector2Int c, Property house, Game game) => Rooms(house, game).Contains(c);

        public static string Name(Property house) => house != null ? house.Name ?? "the flat" : "the basso";

        /// <summary>Every house the crew can live in: the basso first (null), then the homes the family owns.</summary>
        public static IEnumerable<Property> Houses(Game game)
        {
            yield return null;
            foreach (var p in game.Properties.Owned)
                if (p.Kind == PropertyKind.Home) yield return p;
        }

        /// <summary>Moves someone into a house; null is the basso.</summary>
        public static void MoveIn(Pawn p, Property house)
        {
            var r = house?.Interior ?? default;
            p.Home = house != null ? new Vector2Int(r.x + r.width / 2, r.y + r.height / 2) : (Vector2Int?)null;
        }

        public static List<Vector2Int> BedsIn(Property house, Game game)
        {
            var list = new List<Vector2Int>();
            var rooms = Rooms(house, game);
            foreach (var bed in game.Map.PositionsOf(BuildingDefs.Bed))
                if (rooms.Contains(bed)) list.Add(bed);
            return list;
        }

        public static List<Pawn> Residents(Property house, Game game)
        {
            var list = new List<Pawn>();
            foreach (var c in game.Pawns.Crew)
                if (!c.Dead && !c.Departed && HomeOf(c, game) == house) list.Add(c);
            return list;
        }

        /// <summary>Someone else is lying in this bed, or on their way to it.</summary>
        public static bool BedTaken(Vector2Int bed, Pawn except, Game game)
        {
            foreach (var c in game.Pawns.Crew)
                if (c != except && !c.Dead && c.Bed == bed) return true;
            return false;
        }

        /// <summary>The nearest bed in this person's house that nobody is sleeping in right now, or null.</summary>
        public static Vector2Int? FreeBed(Pawn p, Game game)
        {
            Vector2Int? best = null;
            int bestDist = int.MaxValue;
            foreach (var bed in BedsIn(HomeOf(p, game), game))
            {
                if (BedTaken(bed, p, game)) continue;
                int d = GridUtil.Manhattan(p.Cell, bed);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = bed;
                }
            }
            return best;
        }

        /// <summary>
        /// The most of a house's people asleep in the same hour, by their schedules: day and night teams
        /// share the beds, so this is how many beds the house really needs.
        /// </summary>
        public static int BedsNeeded(Property house, Game game)
        {
            var people = Residents(house, game);
            int most = 0;
            for (int h = 0; h < 24; h++)
            {
                int n = 0;
                foreach (var c in people) if (c.Schedule[h] == Pawn.Shift.Sleep) n++;
                most = Mathf.Max(most, n);
            }
            return most;
        }

        /// <summary>How many beds short the house is at its busiest hour (0 when there are enough).</summary>
        public static int BedsShort(Property house, Game game) => Mathf.Max(0, BedsNeeded(house, game) - BedsIn(house, game).Count);

        /// <summary>The nearest piece of this furniture in this person's own house.</summary>
        public static bool TryFindInHouse(Pawn p, BuildingDef def, Game game, out Vector2Int found)
        {
            found = default;
            var rooms = Rooms(HomeOf(p, game), game);
            int best = int.MaxValue;
            foreach (var c in game.Map.PositionsOf(def))
            {
                if (!rooms.Contains(c)) continue;
                int d = (c - p.Cell).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    found = c;
                }
            }
            return best != int.MaxValue;
        }

        /// <summary>A free cell somewhere in this person's house, to hang around or lie down in.</summary>
        public static Vector2Int? RandomCellAtHome(Pawn p, Game game)
        {
            var r = Rooms(HomeOf(p, game), game);
            for (int i = 0; i < 20; i++)
            {
                var c = new Vector2Int(game.Rng.Next(r.xMin, r.xMax), game.Rng.Next(r.yMin, r.yMax));
                if (game.Map.IsPassable(c) && game.Map.GetBuilding(c) == null) return c;
            }
            return null;
        }
    }
}
