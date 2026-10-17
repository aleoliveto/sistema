using System.Collections.Generic;
using OSistema.World;
using UnityEngine;

namespace OSistema.Pawns
{
    public enum MoveResult { Moving, Arrived, Blocked }

    /// <summary>Grid movement along a path. Shared by crew pawns and NPCs (customers, police).</summary>
    public sealed class Mover
    {
        public readonly float BaseSpeed;
        public float SpeedFactor = 1f;

        public Vector2Int Cell { get; private set; }
        public Vector2 Position { get; private set; }
        public bool IsMoving => path != null;

        List<Vector2Int> path;
        int pathIndex;
        float progress;

        public Mover(Vector2Int cell, float baseSpeed)
        {
            BaseSpeed = baseSpeed;
            Teleport(cell);
        }

        public static Vector2 CellCenter(Vector2Int c) => new Vector2(c.x + 0.5f, c.y + 0.5f);

        public void SetPath(List<Vector2Int> newPath)
        {
            path = newPath;
            pathIndex = 0;
            progress = 0f;
            Position = CellCenter(Cell);
        }

        public void Stop()
        {
            path = null;
            progress = 0f;
            Position = CellCenter(Cell);
        }

        public void Teleport(Vector2Int cell)
        {
            Cell = cell;
            Stop();
        }

        public MoveResult Tick(Map map)
        {
            if (path == null || pathIndex >= path.Count)
            {
                path = null;
                return MoveResult.Arrived;
            }

            var next = path[pathIndex];
            if (!map.IsPassable(next))
            {
                Stop();
                return MoveResult.Blocked;
            }

            bool diagonal = next.x != Cell.x && next.y != Cell.y;
            float cost = map.MoveCost(next.x, next.y) * (diagonal ? 1.41421356f : 1f);
            progress += BaseSpeed * SpeedFactor / cost;

            if (progress < 1f)
            {
                Position = Vector2.Lerp(CellCenter(Cell), CellCenter(next), progress);
                return MoveResult.Moving;
            }

            Cell = next;
            progress = 0f;
            Position = CellCenter(Cell);
            pathIndex++;
            if (pathIndex < path.Count) return MoveResult.Moving;
            path = null;
            return MoveResult.Arrived;
        }
    }
}
