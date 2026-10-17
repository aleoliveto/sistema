using System.Collections.Generic;
using OSistema.Core;
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
        public bool ControlsZone(Zone zone, Game game)
        {
            if (zone == null) return false;
            if (zone.Rect.Contains(Vector2Int.RoundToInt(game.PlayerBase.center))) return true;
            foreach (var sq in Squares) if (sq.ZoneId == zone.Id) return true;
            foreach (var p in game.Properties.Owned)
                if (zone.Rect.Contains(p.Door)) return true;
            return false;
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
            if (zone == null || game.Rival.Destroyed || !game.Rival.OwnsZone(zone.Id)) return;
            game.Rival.Hostility = Mathf.Min(100f, game.Rival.Hostility + 25f);
            game.Log.Add($"You've moved into {zone.Name}, the {game.Rival.Name}'s turf. They'll take it as an insult.", MessageKind.Bad);
        }

        /// <summary>Why pizzo can't be imposed on this business, or null if it can.</summary>
        public string PizzoBlocker(Business b, Game game)
        {
            var zone = game.Territory.ZoneAt(b.Cell);
            if (zone != null && !game.Rival.Destroyed && game.Rival.OwnsZone(zone.Id)) return $"Pays the {game.Rival.Name}";
            if (!ControlsZone(zone, game)) return "Outside your turf";
            return null;
        }
    }
}
