using System.Collections.Generic;
using OSistema.Story;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>One block of the district. Heat is how much attention the police pay to it (0..100).</summary>
    public sealed class Zone
    {
        public int Id;
        public string Name;
        public RectInt Rect;
        public float Heat;
        public float Demand; // how many customers a piazza here attracts (0.6..1.4)
    }

    public sealed class Territory
    {
        const float HeatDecayPerHour = 6f / 24f;

        public readonly List<Zone> Zones = new List<Zone>();
        readonly int[] zoneIndex; // per cell, -1 for roads
        readonly Map map;

        /// <summary>Road cells on the map edge: where customers and police arrive from.</summary>
        public readonly List<Vector2Int> Entrances = new List<Vector2Int>();

        public Territory(Map map, IEnumerable<Zone> zones)
        {
            this.map = map;
            zoneIndex = new int[map.CellCount];
            for (int i = 0; i < zoneIndex.Length; i++) zoneIndex[i] = -1;

            foreach (var z in zones)
            {
                z.Id = Zones.Count;
                Zones.Add(z);
                foreach (var c in GridUtil.Cells(z.Rect, false))
                    if (map.InBounds(c)) zoneIndex[map.Index(c.x, c.y)] = z.Id;
            }

            for (int x = 0; x < map.Width; x++)
            {
                AddEntrance(x, 0);
                AddEntrance(x, map.Height - 1);
            }
            for (int y = 1; y < map.Height - 1; y++)
            {
                AddEntrance(0, y);
                AddEntrance(map.Width - 1, y);
            }
        }

        void AddEntrance(int x, int y)
        {
            if (map.GetTerrain(x, y) == TerrainType.Road && map.IsPassable(x, y)) Entrances.Add(new Vector2Int(x, y));
        }

        public static List<Zone> FromBlocks(List<RectInt> blocks, System.Random rng)
        {
            var zones = new List<Zone>();
            for (int i = 0; i < blocks.Count; i++)
            {
                string name = Names.Zones[i % Names.Zones.Length];
                if (i >= Names.Zones.Length) name += " II";
                zones.Add(new Zone { Name = name, Rect = blocks[i], Demand = 0.6f + (float)rng.NextDouble() * 0.8f });
            }
            return zones;
        }

        public Zone ZoneAt(Vector2Int c)
        {
            if (!map.InBounds(c)) return null;
            int i = zoneIndex[map.Index(c.x, c.y)];
            return i >= 0 ? Zones[i] : null;
        }

        public void AddHeat(Vector2Int at, float amount)
        {
            var z = ZoneAt(at);
            if (z != null) z.Heat = Mathf.Clamp(z.Heat + amount, 0f, 100f);
        }

        public void HourlyDecay()
        {
            foreach (var z in Zones) z.Heat = Mathf.Max(0f, z.Heat - HeatDecayPerHour);
        }

        public Vector2Int RandomEntrance(System.Random rng) => Entrances[rng.Next(Entrances.Count)];
    }
}
