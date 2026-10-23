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
        /// A rione is the family's if it's home, or if a piazza of the family's is working there (with a
        /// pusher of its own). A foothold (a house or a shop the family owns there) makes it harder to
        /// take away: it counts as guards (see Guards), but it isn't needed to hold the turf.
        /// </summary>
        public bool ControlsZone(Zone zone, Game game)
        {
            if (zone == null) return false;
            if (zone.Rect.Contains(Vector2Int.RoundToInt(game.PlayerBase.center))) return true;
            return HasWorkingPiazza(zone, game);
        }

        /// <summary>Why a rione with a piazza of yours isn't yours yet, for the Piazza screen; null if it is.</summary>
        public string WhyNotHeld(Zone zone, Game game)
        {
            if (zone == null || ControlsZone(zone, game)) return null;
            bool piazza = false;
            foreach (var sq in Squares) if (sq.ZoneId == zone.Id) piazza = true;
            if (!piazza) return "no piazza of yours there";
            return "its piazza has no pusher: put one on it";
        }

        public bool HasFoothold(Zone zone, Game game)
        {
            foreach (var p in game.Properties.Owned)
                if (zone.Rect.Contains(p.Door)) return true;
            foreach (var b in Businesses)
                if (b.Status == BusinessStatus.Owned && zone.Rect.Contains(b.Cell)) return true;
            return false;
        }

        /// <summary>A piazza in this rione with a pusher of its own (one placed there, or the only piazza's).</summary>
        public bool HasWorkingPiazza(Zone zone, Game game)
        {
            foreach (var c in game.Pawns.Crew)
            {
                if (!c.IsPresent || (c.Role != CrewRole.Pusher && c.Role != CrewRole.Capo)) continue;
                var sq = SquareOf(c, game);
                if (sq != null && sq.ZoneId == zone.Id) return true;
            }
            return false;
        }

        /// <summary>How well a rione of yours is held: lookouts and armed crew posted near its piazze.</summary>
        public int Guards(Zone zone, Game game)
        {
            int n = HasFoothold(zone, game) ? 2 : 0;   // a house or a shop of yours: neighbours who'll warn you, a door to run to
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
            // Shopkeepers pay a Clan, not a couple of guagliuni with ideas.
            if (Rank < 2) return "Pizzo is for a Clan: you're not one yet";
            var zone = game.Territory.ZoneAt(b.Cell);
            var clan = zone != null ? game.ClanOwning(zone.Id) : null;
            if (clan != null) return $"Pays the {clan.Name}";
            if (!ControlsZone(zone, game)) return "Outside your turf";
            return null;
        }
    }
}
