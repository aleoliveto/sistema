using System.Collections.Generic;
using OSistema.Core;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// Turf. The family controls the rioni where it lives, owns property or runs a piazza. Pizzo can
    /// only be imposed there; you grow by opening a piazza or buying a house in a neighbouring rione.
    /// Moving into the rivals' rioni is an act of war.
    /// </summary>
    public sealed partial class Organization
    {
        /// <summary>
        /// A rione is the family's if it's home, or if the family has a foothold there (a house or a
        /// shop it owns) and a piazza working there (with a pusher on the payroll). Lose either and
        /// it's just a place you happen to know.
        /// </summary>
        public bool ControlsZone(Zone zone, Game game)
        {
            if (zone == null) return false;
            if (zone.Rect.Contains(Vector2Int.RoundToInt(game.PlayerBase.center))) return true;
            return HasFoothold(zone, game) && HasWorkingPiazza(zone, game);
        }

        public bool HasFoothold(Zone zone, Game game)
        {
            foreach (var p in game.Properties.Owned)
                if (zone.Rect.Contains(p.Door)) return true;
            foreach (var b in Businesses)
                if (b.Status == BusinessStatus.Owned && zone.Rect.Contains(b.Cell)) return true;
            return false;
        }

        public bool HasWorkingPiazza(Zone zone, Game game)
        {
            bool pusher = false;
            foreach (var c in game.Pawns.Crew) if (c.IsPresent && c.Role == CrewRole.Pusher) pusher = true;
            if (!pusher) return false;
            foreach (var sq in Squares) if (sq.ZoneId == zone.Id) return true;
            return false;
        }

        /// <summary>How well a rione of yours is held: lookouts and armed crew posted near its piazze.</summary>
        public int Guards(Zone zone, Game game)
        {
            int n = 0;
            foreach (var c in game.Pawns.Crew)
            {
                if (!c.IsPresent || game.Territory.ZoneAt(c.Cell) != zone) continue;
                if (c.Role == CrewRole.Lookout || c.Role == CrewRole.Bodyguard || c.Role == CrewRole.Capo || c.Armed) n++;
            }
            return n;
        }

        public List<Zone> Turf(Game game)
        {
            var list = new List<Zone>();
            foreach (var z in game.Territory.Zones) if (ControlsZone(z, game)) list.Add(z);
            return list;
        }

        static bool Adjacent(Zone a, Zone b)
        {
            var r = new RectInt(a.Rect.x - 4, a.Rect.y - 4, a.Rect.width + 8, a.Rect.height + 8);
            return r.Overlaps(b.Rect);
        }

        /// <summary>Your own rioni and the ones bordering them.</summary>
        public bool CanExpandInto(Zone zone, Game game)
        {
            if (zone == null) return false;
            if (ControlsZone(zone, game)) return true;
            foreach (var z in game.Territory.Zones)
                if (ControlsZone(z, game) && Adjacent(z, zone)) return true;
            return false;
        }

        void WarnIfRivalZone(Zone zone, Game game)
        {
            var owner = zone != null ? game.ClanOwning(zone.Id) : null;
            if (owner == null) return;
            owner.BreakAlliance(game, $"you moved into {zone.Name}");
            owner.Hostility = Mathf.Min(100f, owner.Hostility + 25f);
            game.Log.Add($"You've moved into {zone.Name}, the {owner.Name}'s turf. They'll take it as an insult.", MessageKind.Bad);
        }

        /// <summary>Why pizzo can't be imposed on this business, or null if it can.</summary>
        public string PizzoBlocker(Business b, Game game)
        {
            var zone = game.Territory.ZoneAt(b.Cell);
            var clan = zone != null ? game.ClanOwning(zone.Id) : null;
            if (clan != null) return $"Pays the {clan.Name}";
            if (!ControlsZone(zone, game)) return "Outside your turf";
            return null;
        }
    }
}
