using System.Collections.Generic;
using UnityEngine;

namespace OSistema.World
{
    /// <summary>
    /// Every building that can change hands. Owned homes extend the crew's base (they sleep, hide
    /// and lie low there); owned shops are legitimate fronts that pay a weekly income and launder money.
    /// </summary>
    public sealed class PropertyManager
    {
        readonly Map map;
        readonly int[] index; // property id per cell, -1 for none

        public readonly List<Property> All;

        public PropertyManager(Map map, List<Property> properties)
        {
            this.map = map;
            All = properties;
            index = new int[map.CellCount];
            for (int i = 0; i < index.Length; i++) index[i] = -1;
            for (int i = 0; i < All.Count; i++)
            {
                All[i].Id = i;
                foreach (var c in GridUtil.Cells(All[i].Rect, false))
                    if (map.InBounds(c)) index[map.Index(c.x, c.y)] = i;
            }
        }

        public Property At(Vector2Int c)
        {
            if (!map.InBounds(c)) return null;
            int i = index[map.Index(c.x, c.y)];
            return i >= 0 ? All[i] : null;
        }

        public IEnumerable<Property> Owned
        {
            get
            {
                foreach (var p in All)
                    if (p.OwnedByPlayer) yield return p;
            }
        }

        public bool IsOwnedHome(Vector2Int c)
        {
            var p = At(c);
            return p != null && p.OwnedByPlayer && p.Interior.Contains(c);
        }

        public static int PriceFor(Property p, float demand) => p.Kind switch
        {
            PropertyKind.Shop => Mathf.RoundToInt((p.Rect.width * p.Rect.height * 140 + 3000) * demand / 100f) * 100,
            PropertyKind.Home => Mathf.RoundToInt((p.Rect.width * p.Rect.height * 85 + 800) * demand / 100f) * 100,
            _ => 0,
        };

        /// <summary>A random free cell inside one of the crew's homes, preferring the nearest ones.</summary>
        public Vector2Int? RandomHomeCell(Vector2Int near, System.Random rng)
        {
            Property best = null;
            float bestScore = float.MaxValue;
            foreach (var p in All)
            {
                if (!p.OwnedByPlayer || p.Kind != PropertyKind.Home) continue;
                float score = (p.Rect.center - (Vector2)near).sqrMagnitude * (0.6f + (float)rng.NextDouble() * 0.8f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            if (best == null) return null;
            var r = best.Interior;
            for (int i = 0; i < 15; i++)
            {
                var c = new Vector2Int(rng.Next(r.xMin, r.xMax), rng.Next(r.yMin, r.yMax));
                if (map.IsPassable(c) && map.GetBuilding(c) == null) return c;
            }
            return null;
        }
    }
}
