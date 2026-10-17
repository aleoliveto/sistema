using System;
using System.Collections.Generic;
using UnityEngine;

namespace OSistema.World
{
    /// <summary>Purely visual details placed by the generator.</summary>
    public enum MapDecor : byte { None, Laundry, WallLamp }

    /// <summary>Grid data model: the single source of truth for terrain and buildings.</summary>
    public sealed class Map
    {
        public readonly int Width;
        public readonly int Height;

        readonly TerrainType[] terrain;
        readonly BuildingDef[] buildings;
        readonly byte[] styles; // facade/floor palette per cell, chosen per building by the generator
        readonly byte[] decor;  // overhead or wall decoration: laundry lines, wall lamps (MapDecor)

        // Where each building type stands, so jobs can find the nearest stove or pizzeria quickly.
        readonly Dictionary<BuildingDef, HashSet<Vector2Int>> positions = new Dictionary<BuildingDef, HashSet<Vector2Int>>();
        static readonly HashSet<Vector2Int> None = new HashSet<Vector2Int>();

        public event Action<int, int> CellChanged;

        public Map(int width, int height)
        {
            Width = width;
            Height = height;
            terrain = new TerrainType[width * height];
            buildings = new BuildingDef[width * height];
            styles = new byte[width * height];
            decor = new byte[width * height];
        }

        public MapDecor GetDecor(int x, int y) => (MapDecor)decor[Index(x, y)];
        public void SetDecor(int x, int y, MapDecor d) => decor[Index(x, y)] = (byte)d;
        public byte[] RawDecor => decor;

        public byte GetStyle(int x, int y) => styles[Index(x, y)];
        public void SetStyle(int x, int y, byte style) => styles[Index(x, y)] = style;
        public byte[] RawStyles => styles;

        public int CellCount => Width * Height;
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool InBounds(Vector2Int c) => InBounds(c.x, c.y);
        public int Index(int x, int y) => y * Width + x;

        public TerrainType GetTerrain(int x, int y) => terrain[Index(x, y)];
        public TerrainType GetTerrainAt(int index) => terrain[index];

        public void SetTerrain(int x, int y, TerrainType t)
        {
            terrain[Index(x, y)] = t;
            CellChanged?.Invoke(x, y);
        }

        public BuildingDef GetBuilding(int x, int y) => buildings[Index(x, y)];
        public BuildingDef GetBuilding(Vector2Int c) => GetBuilding(c.x, c.y);
        public BuildingDef GetBuildingAt(int index) => buildings[index];

        public void SetBuilding(int x, int y, BuildingDef def)
        {
            int i = Index(x, y);
            var cell = new Vector2Int(x, y);
            var old = buildings[i];
            if (old != null && positions.TryGetValue(old, out var oldSet)) oldSet.Remove(cell);
            buildings[i] = def;
            if (def != null)
            {
                if (!positions.TryGetValue(def, out var set)) positions[def] = set = new HashSet<Vector2Int>();
                set.Add(cell);
            }
            CellChanged?.Invoke(x, y);
        }

        public IReadOnlyCollection<Vector2Int> PositionsOf(BuildingDef def) =>
            positions.TryGetValue(def, out var set) ? set : None;

        /// <summary>Nearest cell holding <paramref name="def"/>, by straight-line distance. False if none exists.</summary>
        public bool TryFindNearest(BuildingDef def, Vector2Int from, out Vector2Int found)
        {
            found = default;
            int best = int.MaxValue;
            foreach (var c in PositionsOf(def))
            {
                int d = (c - from).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    found = c;
                }
            }
            return best != int.MaxValue;
        }

        public bool IsPassable(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            int i = Index(x, y);
            if (terrain[i] == TerrainType.Water) return false;
            var b = buildings[i];
            return b == null || b.Passable;
        }

        public bool IsPassable(Vector2Int c) => IsPassable(c.x, c.y);

        public float MoveCost(int x, int y)
        {
            int i = Index(x, y);
            float cost = TerrainDefs.MoveCost(terrain[i]);
            var b = buildings[i];
            if (b != null) cost *= b.PathCost;
            return cost;
        }
    }
}
