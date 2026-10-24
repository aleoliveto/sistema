using System.Collections.Generic;
using OSistema.Core;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// The family's scooters are things in the street: parked somewhere, ridden away, left where the rider gets
    /// off. Nobody pulls one out of a pocket. The boss walks up to one; the crew take one only if it's parked
    /// near them, and otherwise walk. Stock.Scooters is how many the family owns; this keeps the street in step.
    /// </summary>
    public static class FamilyScooters
    {
        /// <summary>How close a parked scooter has to be for someone to get on it (cells).</summary>
        public const float PlayerReach = 2.2f;
        const int CrewReach = 15;

        public static IReadOnlyCollection<Vector2Int> Parked(Game game) => game.Map.PositionsOf(BuildingDefs.FamilyScooter);

        public static bool Nearest(Game game, Vector2 from, float maxDist, out Vector2Int at)
        {
            at = default;
            float best = maxDist * maxDist;
            bool found = false;
            foreach (var c in Parked(game))
            {
                float d = (Mover.CellCenter(c) - from).sqrMagnitude;
                if (d <= best) { best = d; at = c; found = true; }
            }
            return found;
        }

        static bool Outdoor(Map map, Vector2Int c, bool roadOk)
        {
            if (!map.InBounds(c) || !map.IsPassable(c) || map.GetBuilding(c.x, c.y) != null || map.IsSteps(c.x, c.y)) return false;
            var t = map.GetTerrain(c.x, c.y);
            return t == TerrainType.Sidewalk || t == TerrainType.Paving || (roadOk && t == TerrainType.Road);
        }

        /// <summary>Leave a scooter at the kerb nearest to a spot (a pavement first, the road if there's nothing else).</summary>
        public static bool Park(Game game, Vector2Int near, Inventory seat = null)
        {
            for (int pass = 0; pass < 2; pass++)
                foreach (var c in GridUtil.CellsAround(near, 8))
                    if (Outdoor(game.Map, c, pass == 1) && !Busy(game, c))
                    {
                        game.Map.SetBuilding(c.x, c.y, BuildingDefs.FamilyScooter);
                        game.Seats.Drop(game, c, seat);   // what's under the seat stays with it
                        return true;
                    }
            EmptySeat(game, seat);
            return false;
        }

        /// <summary>A scooter that's gone (sold, or nowhere to leave it): what was under the seat turns up at home.</summary>
        public static void EmptySeat(Game game, Inventory seat)
        {
            if (seat == null) return;
            var s = game.Stock;
            foreach (var slot in seat.Slots)
            {
                if (slot.Empty) continue;
                switch (slot.Item)
                {
                    case Item.Doses: s.Product += slot.Count; break;
                    case Item.Packs: s.Cigarettes += slot.Count; break;
                    case Item.Fakes: game.Org.H.fakes += slot.Count; break;
                    case Item.Medicine: s.Medicine += slot.Count; break;
                    case Item.Meal: s.Meals += slot.Count; break;
                    default: if (Items.IsGun(slot.Item)) s.AddGun(Items.GunOf(slot.Item), slot.Count); break;
                }
            }
            for (int i = 0; i < seat.Slots.Length; i++) seat.Slots[i] = default;
        }

        // Someone standing there: don't park on his feet.
        static bool Busy(Game game, Vector2Int c)
        {
            foreach (var p in game.Pawns.Crew) if (!p.Dead && p.Cell == c) return true;
            return false;
        }

        /// <summary>Outside the basso's front door: where new and spare scooters wait.</summary>
        public static Vector2Int Home(Game game)
        {
            var b = game.PlayerBase;
            var center = Vector2Int.RoundToInt(b.center);
            Vector2Int best = center;
            int bestD = int.MaxValue;
            foreach (var d in game.Map.PositionsOf(BuildingDefs.Door))
            {
                if (d.x < b.xMin - 1 || d.x > b.xMax || d.y < b.yMin - 1 || d.y > b.yMax) continue;
                // The door on the edge that opens to the street.
                foreach (var n in new[] { d + Vector2Int.up, d + Vector2Int.down, d + Vector2Int.left, d + Vector2Int.right })
                    if (!b.Contains(n) && Outdoor(game.Map, n, true))
                    {
                        int dd = GridUtil.Manhattan(d, center);
                        if (dd < bestD) { bestD = dd; best = n; }
                    }
            }
            return best;
        }

        public static void Take(Game game, Vector2Int at, Pawn rider)
        {
            game.Map.SetBuilding(at.x, at.y, null);
            rider.HasFamilyScooter = true;
            rider.Seat = game.Seats.Lift(game, at) ?? new Inventory(Bag.SeatSize);
        }

        /// <summary>Riders whose trip is over leave the scooter where they are; crew setting off take one parked near them.</summary>
        public static void Tick(Game game)
        {
            PropArtLook(game);
            int held = 0;
            var crew = game.Pawns.Crew;
            for (int i = 0; i < crew.Count; i++)
            {
                var p = crew[i];
                bool gone = p.Dead || p.Departed || p.InJail || p.InHospital;
                if (p.HasFamilyScooter && (gone || !p.OnScooter || p.Pillion))
                {
                    p.HasFamilyScooter = false;
                    if (!p.Departed && !p.InHospital) Park(game, p.Cell, p.Seat); else EmptySeat(game, p.Seat);
                    p.Seat = null;   // left in the street where he got off (or fell)
                    continue;
                }
                if (p.HasFamilyScooter) { held++; continue; }
                // Setting off on a scooter he doesn't have: one parked near him, or he walks.
                if (!gone && p.OnScooter && !p.Pillion && !p.StolenScooter && !p.Controlled)
                {
                    if (Nearest(game, p.Position, CrewReach, out var at)) { Take(game, at, p); held++; }
                    else p.OnScooter = p.Riding = false;
                }
            }
            // Keep the street in step with what the family owns: bought (or lost) ones turn up (or vanish) at home.
            if (game.Ticks.TicksGame % 120 != 0) return;
            int parked = Parked(game).Count;
            int want = game.Stock.Scooters - held;
            if (parked < want)
            {
                var home = Home(game);
                for (int k = parked; k < want; k++) if (!Park(game, home)) break;
            }
            else if (parked > want && want >= 0)
            {
                var spare = new List<Vector2Int>(Parked(game));
                for (int k = 0; k < parked - want && k < spare.Count; k++)
                {
                    game.Map.SetBuilding(spare[k].x, spare[k].y, null);
                    EmptySeat(game, game.Seats.Lift(game, spare[k]));
                }
            }
        }

        static void PropArtLook(Game game)
        {
            Views.PropArt.FamilyModel = game.Stock.ScooterModel;
            Views.PropArt.FamilyColour = game.Stock.ScooterColour;
        }

        /// <summary>A line for the log: where the nearest one is parked, as a direction.</summary>
        public static string WhereIs(Game game, Vector2 from)
        {
            if (!Nearest(game, from, 9999f, out var at)) return null;
            var d = Mover.CellCenter(at) - from;
            int cells = Mathf.RoundToInt(d.magnitude);
            string dir = Mathf.Abs(d.x) > Mathf.Abs(d.y) * 2f ? (d.x > 0 ? "east" : "west")
                       : Mathf.Abs(d.y) > Mathf.Abs(d.x) * 2f ? (d.y > 0 ? "north" : "south")
                       : (d.y > 0 ? "north" : "south") + (d.x > 0 ? "-east" : "-west");
            return $"The nearest family scooter is parked {cells} steps {dir}.";
        }
    }
}
