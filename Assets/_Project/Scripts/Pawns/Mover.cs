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
            Snap();
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

        /// <summary>Walks freely (the player's WASD), sliding along walls. Radius in cells.</summary>
        /// <summary>
        /// Walks freely (the player's WASD), sliding along walls. Returns which axes were blocked.
        /// Outdoors only for a scooter: it can't go through doors or inside.
        /// </summary>
        public (bool x, bool y) Slide(Vector2 delta, Map map, float radius = 0.28f, bool outdoorsOnly = false)
        {
            path = null;
            progress = 0f;
            var pos = Position;
            bool bx = false, by = false;
            var tryX = new Vector2(pos.x + delta.x, pos.y);
            if (Free(tryX, map, radius, outdoorsOnly)) pos = tryX; else bx = delta.x != 0f;
            var tryY = new Vector2(pos.x, pos.y + delta.y);
            if (Free(tryY, map, radius, outdoorsOnly)) pos = tryY; else by = delta.y != 0f;
            Position = pos;
            Cell = new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
            return (bx, by);
        }

        static bool Free(Vector2 p, Map map, float r, bool outdoorsOnly)
        {
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector2Int(Mathf.FloorToInt(p.x + (i % 2 == 0 ? -r : r)), Mathf.FloorToInt(p.y + (i < 2 ? -r : r)));
                if (!map.InBounds(c) || !map.IsPassable(c)) return false;
                if (outdoorsOnly)
                {
                    var t = map.GetTerrain(c.x, c.y);
                    if (t == TerrainType.Floor || t == TerrainType.Marble || map.GetBuilding(c.x, c.y) == BuildingDefs.Door) return false;
                    if (map.IsSteps(c.x, c.y)) return false;   // gradoni: no scooter goes up those
                }
            }
            return true;
        }

        /// <summary>
        /// If the mover is wedged against a wall (getting off a scooter hard by a wall), steps it out
        /// to the nearest spot where it fits.
        /// </summary>
        public void Unstick(Map map, float radius = 0.28f)
        {
            if (Free(Position, map, radius, false)) return;
            for (float r = 0.15f; r <= 2.5f; r += 0.15f)
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f;
                var at = Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (!Free(at, map, radius, false)) continue;
                Place(at);
                return;
            }
        }

        /// <summary>Puts the mover exactly somewhere (a passenger on the back of a scooter).</summary>
        public void Place(Vector2 pos)
        {
            path = null;
            progress = 0f;
            Position = pos;
            Cell = new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
        }

        /// <summary>Back to the centre of the current cell before following a path.</summary>
        void Snap() => Position = CellCenter(Cell);

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
