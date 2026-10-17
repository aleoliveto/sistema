using System.Collections.Generic;
using OSistema.Core;
using OSistema.Pawns;
using UnityEngine;

namespace OSistema.World
{
    /// <summary>A planned build or demolition waiting for a pawn.</summary>
    public sealed class Blueprint
    {
        public readonly Vector2Int Cell;
        public readonly BuildingDef Def;
        public readonly bool Deconstruct;
        public int Rotation;
        public float WorkLeft;
        public Pawn ClaimedBy;
        public long RetryAfterTick;

        public Blueprint(Vector2Int cell, BuildingDef def, bool deconstruct, float work)
        {
            Cell = cell;
            Def = def;
            Deconstruct = deconstruct;
            WorkLeft = work;
        }
    }

    public sealed class ConstructionManager
    {
        readonly Map map;
        readonly Dictionary<Vector2Int, Blueprint> byCell = new Dictionary<Vector2Int, Blueprint>();

        /// <summary>Bumped on every change so views know when to redraw.</summary>
        public int Version { get; private set; }

        public ConstructionManager(Map map) => this.map = map;

        public IEnumerable<Blueprint> All => byCell.Values;

        public Blueprint At(Vector2Int cell) => byCell.TryGetValue(cell, out var b) ? b : null;

        public bool IsValid(Blueprint bp) => byCell.TryGetValue(bp.Cell, out var b) && b == bp;

        public bool PlaceBuild(Vector2Int cell, BuildingDef def, int rotation = 0)
        {
            if (!map.InBounds(cell) || byCell.ContainsKey(cell) || map.GetBuilding(cell) != null) return false;
            byCell[cell] = new Blueprint(cell, def, false, def.WorkToBuild) { Rotation = rotation };
            Version++;
            return true;
        }

        public bool PlaceDeconstruct(Vector2Int cell)
        {
            if (!map.InBounds(cell) || byCell.ContainsKey(cell)) return false;
            var existing = map.GetBuilding(cell);
            if (existing == null) return false;
            byCell[cell] = new Blueprint(cell, existing, true, existing.WorkToBuild * 0.5f);
            Version++;
            return true;
        }

        /// <summary>Re-adds a blueprint from a save file.</summary>
        public void Restore(Vector2Int cell, BuildingDef def, bool deconstruct, float workLeft)
        {
            byCell[cell] = new Blueprint(cell, def, deconstruct, workLeft);
            Version++;
        }

        public void Cancel(Vector2Int cell)
        {
            if (byCell.Remove(cell)) Version++;
        }

        public bool HasAvailableWork(long now)
        {
            foreach (var bp in byCell.Values)
                if (bp.ClaimedBy == null && bp.RetryAfterTick <= now) return true;
            return false;
        }

        public void Complete(Blueprint bp, Game game)
        {
            if (!byCell.Remove(bp.Cell)) return;
            Version++;
            if (bp.Deconstruct)
            {
                map.SetBuilding(bp.Cell.x, bp.Cell.y, null);
                return;
            }
            map.SetRotation(bp.Cell.x, bp.Cell.y, bp.Rotation);
            map.SetBuilding(bp.Cell.x, bp.Cell.y, bp.Def);
            if (!bp.Def.Passable) game.Pawns.EvictFrom(bp.Cell, map);
        }
    }
}
