using System.Collections.Generic;
using UnityEngine;

namespace OSistema.World
{
    /// <summary>
    /// Things bigger than one cell. The piece itself sits on its anchor cell (the lower-left corner of
    /// what it covers, as turned); its other cells hold a filler (solid or walkable) so nobody walks
    /// through the sofa and nothing else is put on the bed. Pick up, sell or remove any cell and the
    /// whole piece goes.
    /// </summary>
    public static class Footprint
    {
        /// <summary>Width and height in cells once turned by <paramref name="rot"/> quarter turns.</summary>
        public static Vector2Int SizeOf(BuildingDef d, int rot) =>
            d == null ? Vector2Int.one : (rot & 1) == 1 ? new Vector2Int(d.Size.y, d.Size.x) : d.Size;

        public static bool IsBig(BuildingDef d) => d != null && (d.Size.x > 1 || d.Size.y > 1);

        public static IEnumerable<Vector2Int> Cells(BuildingDef d, Vector2Int anchor, int rot)
        {
            var s = SizeOf(d, rot);
            for (int y = 0; y < s.y; y++)
            for (int x = 0; x < s.x; x++)
                yield return anchor + new Vector2Int(x, y);
        }

        /// <summary>The middle of what it covers, in world units: where its picture goes.</summary>
        public static Vector2 Center(BuildingDef d, Vector2Int anchor, int rot)
        {
            var s = SizeOf(d, rot);
            return new Vector2(anchor.x + s.x * 0.5f, anchor.y + s.y * 0.5f);
        }

        /// <summary>True if every cell is on the map and free (and indoors, if asked).</summary>
        public static bool Fits(Map map, BuildingDef d, Vector2Int anchor, int rot, bool indoors)
        {
            foreach (var c in Cells(d, anchor, rot))
            {
                if (!map.InBounds(c) || map.GetBuilding(c) != null) return false;
                if (indoors)
                {
                    var t = map.GetTerrain(c.x, c.y);
                    if (t != TerrainType.Floor && t != TerrainType.Marble) return false;
                }
            }
            return true;
        }

        public static void Place(Map map, BuildingDef d, Vector2Int anchor, int rot)
        {
            map.SetRotation(anchor.x, anchor.y, (byte)rot);
            var filler = d.Passable ? BuildingDefs.PartSoft : BuildingDefs.PartSolid;
            foreach (var c in Cells(d, anchor, rot))
                if (c != anchor) map.SetBuilding(c.x, c.y, filler);
            map.SetBuilding(anchor.x, anchor.y, d);
        }

        /// <summary>The cell holding the piece that covers <paramref name="cell"/> (itself, if it's the piece).</summary>
        public static Vector2Int? AnchorOf(Map map, Vector2Int cell)
        {
            if (!map.InBounds(cell)) return null;
            var b = map.GetBuilding(cell);
            if (b == null) return null;
            if (!b.Filler) return cell;
            for (int dy = 0; dy <= 2; dy++)
            for (int dx = 0; dx <= 2; dx++)
            {
                var a = cell - new Vector2Int(dx, dy);
                if (!map.InBounds(a)) continue;
                var d = map.GetBuilding(a);
                if (!IsBig(d)) continue;
                var s = SizeOf(d, map.GetRotation(a.x, a.y));
                if (dx < s.x && dy < s.y) return a;
            }
            return null;
        }

        /// <summary>Removes the whole piece covering <paramref name="cell"/>. Returns what it was, or null.</summary>
        public static BuildingDef Remove(Map map, Vector2Int cell, out Vector2Int anchor, out int rot)
        {
            anchor = cell;
            rot = 0;
            var a = AnchorOf(map, cell);
            if (a == null)
            {
                // A stray filler with nothing to belong to: just clear it.
                if (map.InBounds(cell) && map.GetBuilding(cell) is BuildingDef f && f.Filler) map.SetBuilding(cell.x, cell.y, null);
                return null;
            }
            anchor = a.Value;
            var d = map.GetBuilding(anchor);
            rot = map.GetRotation(anchor.x, anchor.y);
            if (IsBig(d))
                foreach (var c in Cells(d, anchor, rot))
                    if (map.InBounds(c) && map.GetBuilding(c) is BuildingDef b && b.Filler) map.SetBuilding(c.x, c.y, null);
            map.SetBuilding(anchor.x, anchor.y, null);
            return d;
        }
    }
}
