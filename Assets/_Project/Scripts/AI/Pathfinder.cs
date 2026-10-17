using System.Collections.Generic;
using OSistema.World;
using UnityEngine;

namespace OSistema.AI
{
    /// <summary>
    /// 8-directional A* over the map grid. No corner cutting past walls.
    /// Buffers are reused between searches; a stamp marks which entries belong to the current search.
    /// </summary>
    public sealed class Pathfinder
    {
        const int MaxExpanded = 40000;
        const float Diagonal = 1.41421356f;

        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        readonly Map map;
        readonly float[] g;
        readonly int[] parent;
        readonly int[] openStamp;
        readonly int[] closedStamp;
        readonly MinHeap heap = new MinHeap();
        int stamp;

        public Pathfinder(Map map)
        {
            this.map = map;
            int n = map.CellCount;
            g = new float[n];
            parent = new int[n];
            openStamp = new int[n];
            closedStamp = new int[n];
        }

        /// <summary>
        /// Returns the cells to walk through (excluding start), an empty list if already at the goal,
        /// or null if unreachable. With <paramref name="adjacent"/>, the goal is any cell orthogonally next to target.
        /// </summary>
        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int target, bool adjacent)
        {
            if (!map.InBounds(start) || !map.InBounds(target)) return null;
            if (!adjacent && !map.IsPassable(target)) return null;

            stamp++;
            heap.Clear();
            int w = map.Width;
            int s = map.Index(start.x, start.y);
            g[s] = 0f;
            parent[s] = -1;
            openStamp[s] = stamp;
            heap.Push(s, Heuristic(start.x, start.y, target));

            int expanded = 0;
            while (heap.Count > 0)
            {
                int cur = heap.Pop();
                if (closedStamp[cur] == stamp) continue;
                closedStamp[cur] = stamp;

                int cx = cur % w, cy = cur / w;
                if (IsGoal(cx, cy, target, adjacent)) return Reconstruct(cur);
                if (++expanded > MaxExpanded) return null;

                for (int i = 0; i < 8; i++)
                {
                    int nx = cx + DX[i], ny = cy + DY[i];
                    if (!map.IsPassable(nx, ny)) continue;
                    bool diag = i >= 4;
                    if (diag && (!map.IsPassable(nx, cy) || !map.IsPassable(cx, ny))) continue;

                    int n = ny * w + nx;
                    if (closedStamp[n] == stamp) continue;
                    float ng = g[cur] + map.MoveCost(nx, ny) * (diag ? Diagonal : 1f);
                    if (openStamp[n] != stamp || ng < g[n])
                    {
                        openStamp[n] = stamp;
                        g[n] = ng;
                        parent[n] = cur;
                        heap.Push(n, ng + Heuristic(nx, ny, target));
                    }
                }
            }
            return null;
        }

        static bool IsGoal(int x, int y, Vector2Int target, bool adjacent)
        {
            int d = Mathf.Abs(x - target.x) + Mathf.Abs(y - target.y);
            return adjacent ? d == 1 : d == 0;
        }

        static float Heuristic(int x, int y, Vector2Int t)
        {
            int dx = Mathf.Abs(x - t.x), dy = Mathf.Abs(y - t.y);
            return dx + dy + (Diagonal - 2f) * Mathf.Min(dx, dy);
        }

        List<Vector2Int> Reconstruct(int end)
        {
            var path = new List<Vector2Int>();
            int w = map.Width;
            for (int i = end; parent[i] != -1; i = parent[i])
                path.Add(new Vector2Int(i % w, i / w));
            path.Reverse();
            return path;
        }

        /// <summary>Binary min-heap with lazy deletion (stale entries are skipped via closedStamp).</summary>
        sealed class MinHeap
        {
            readonly List<int> items = new List<int>();
            readonly List<float> keys = new List<float>();

            public int Count => items.Count;

            public void Clear()
            {
                items.Clear();
                keys.Clear();
            }

            public void Push(int item, float key)
            {
                items.Add(item);
                keys.Add(key);
                int i = items.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (keys[p] <= keys[i]) break;
                    Swap(i, p);
                    i = p;
                }
            }

            public int Pop()
            {
                int top = items[0];
                int last = items.Count - 1;
                items[0] = items[last];
                keys[0] = keys[last];
                items.RemoveAt(last);
                keys.RemoveAt(last);

                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < items.Count && keys[l] < keys[m]) m = l;
                    if (r < items.Count && keys[r] < keys[m]) m = r;
                    if (m == i) break;
                    Swap(i, m);
                    i = m;
                }
                return top;
            }

            void Swap(int a, int b)
            {
                (items[a], items[b]) = (items[b], items[a]);
                (keys[a], keys[b]) = (keys[b], keys[a]);
            }
        }
    }
}
