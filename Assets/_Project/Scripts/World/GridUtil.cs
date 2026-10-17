using System.Collections.Generic;
using UnityEngine;

namespace OSistema.World
{
    public static class GridUtil
    {
        /// <summary>Yields cells in rings of increasing distance from center (center first).</summary>
        public static IEnumerable<Vector2Int> CellsAround(Vector2Int center, int maxRadius)
        {
            yield return center;
            for (int r = 1; r <= maxRadius; r++)
            {
                for (int x = -r; x <= r; x++)
                {
                    yield return new Vector2Int(center.x + x, center.y + r);
                    yield return new Vector2Int(center.x + x, center.y - r);
                }
                for (int y = -r + 1; y <= r - 1; y++)
                {
                    yield return new Vector2Int(center.x + r, center.y + y);
                    yield return new Vector2Int(center.x - r, center.y + y);
                }
            }
        }

        public static RectInt RectFromCorners(Vector2Int a, Vector2Int b)
        {
            int minX = Mathf.Min(a.x, b.x), minY = Mathf.Min(a.y, b.y);
            return new RectInt(minX, minY, Mathf.Abs(a.x - b.x) + 1, Mathf.Abs(a.y - b.y) + 1);
        }

        public static IEnumerable<Vector2Int> Cells(RectInt r, bool outlineOnly)
        {
            for (int y = r.yMin; y < r.yMax; y++)
            for (int x = r.xMin; x < r.xMax; x++)
            {
                bool edge = x == r.xMin || x == r.xMax - 1 || y == r.yMin || y == r.yMax - 1;
                if (!outlineOnly || edge) yield return new Vector2Int(x, y);
            }
        }

        public static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
