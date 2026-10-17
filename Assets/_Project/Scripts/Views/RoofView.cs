using System.Collections.Generic;
using OSistema.Core;
using OSistema.World;
using UnityEngine;

namespace OSistema.Views
{
    /// <summary>
    /// Naples from above: flat rooftop terraces (lastrici solari) over every building that isn't the
    /// family's, with water tanks, antennas, potted plants and washing. A roof fades away when a crew
    /// member is inside, or for good once the building is bought.
    /// </summary>
    public sealed class RoofView : MonoBehaviour
    {
        const int Tile = TilePainter.Size;
        const float FadeSpeed = 4f;

        sealed class Roof
        {
            public Property Property;     // null for buildings that aren't for sale (the Vele, odd corners)
            public RectInt Bounds;
            public bool[] Mask;            // which cells of Bounds this roof covers (null = all)
            public SpriteRenderer Sprite;
            public float Alpha = 1f;
            public bool Open;
        }

        Game game;
        readonly List<Roof> roofs = new List<Roof>();
        float checkTimer;

        public void Init(Game g)
        {
            game = g;
            foreach (var p in g.Properties.All)
            {
                var interior = p.Interior;
                if (interior.width < 1 || interior.height < 1) continue;
                roofs.Add(new Roof { Property = p, Bounds = interior, Sprite = CreateRoof(p, interior) });
            }
            AddLeftoverRoofs(g);
            UpdateOpen();
            foreach (var r in roofs)
            {
                r.Alpha = r.Open ? 0f : 1f;
                Apply(r);
            }
        }

        SpriteRenderer CreateRoof(Property p, RectInt interior)
        {
            int w = interior.width * Tile, h = interior.height * Tile;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            Paint(px, w, h, p.Id, p.Kind, p.Name);
            tex.SetPixels32(px);
            tex.Apply(false);

            var go = new GameObject($"Roof {p.Id}");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(interior.x, interior.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, Tile, 0, SpriteMeshType.FullRect);
            sr.sharedMaterial = SpriteFactory.LitMaterial;
            sr.sortingOrder = 6; // above floors and furniture, below people
            return sr;
        }

        /// <summary>
        /// Every indoor floor that isn't part of a property (the Vele, buildings cut by the market street)
        /// still gets a roof, so no interior shows unless the crew is inside.
        /// </summary>
        void AddLeftoverRoofs(Game g)
        {
            var map = g.Map;
            var covered = new bool[map.CellCount];
            foreach (var p in g.Properties.All)
                foreach (var c in GridUtil.Cells(p.Rect, false))
                    if (map.InBounds(c)) covered[map.Index(c.x, c.y)] = true;
            foreach (var c in GridUtil.Cells(g.PlayerBase, false)) covered[map.Index(c.x, c.y)] = true;

            bool Indoor(int x, int y)
            {
                if (!map.InBounds(x, y) || covered[map.Index(x, y)] || map.GetTerrain(x, y) != TerrainType.Floor) return false;
                var b = map.GetBuilding(x, y);
                return b != BuildingDefs.Wall && b != BuildingDefs.Door;
            }

            var seen = new bool[map.CellCount];
            var stack = new Stack<Vector2Int>();
            var cells = new List<Vector2Int>();
            int salt = 10000;
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                if (seen[map.Index(x, y)] || !Indoor(x, y)) continue;
                cells.Clear();
                stack.Push(new Vector2Int(x, y));
                seen[map.Index(x, y)] = true;
                int minX = x, minY = y, maxX = x, maxY = y;
                while (stack.Count > 0)
                {
                    var c = stack.Pop();
                    cells.Add(c);
                    minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                    minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
                    foreach (var n in new[] { c + Vector2Int.left, c + Vector2Int.right, c + Vector2Int.up, c + Vector2Int.down })
                    {
                        if (!Indoor(n.x, n.y) || seen[map.Index(n.x, n.y)]) continue;
                        seen[map.Index(n.x, n.y)] = true;
                        stack.Push(n);
                    }
                }
                if (cells.Count < 2) continue;
                var bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
                var mask = new bool[bounds.width * bounds.height];
                foreach (var c in cells) mask[(c.y - minY) * bounds.width + (c.x - minX)] = true;
                roofs.Add(new Roof { Bounds = bounds, Mask = mask, Sprite = CreateMaskedRoof(bounds, mask, salt++) });
            }
        }

        SpriteRenderer CreateMaskedRoof(RectInt bounds, bool[] mask, int salt)
        {
            int w = bounds.width * Tile, h = bounds.height * Tile;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            Paint(px, w, h, salt, PropertyKind.Home, null);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (!mask[(y / Tile) * bounds.width + x / Tile]) px[y * w + x] = new Color32(0, 0, 0, 0);
            tex.SetPixels32(px);
            tex.Apply(false);

            var go = new GameObject($"Roof {salt}");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(bounds.x, bounds.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, Tile, 0, SpriteMeshType.FullRect);
            sr.sharedMaterial = SpriteFactory.LitMaterial;
            sr.sortingOrder = 6;
            return sr;
        }

        // ---------- Painting ----------

        static uint Hash(int a, int b, int salt)
        {
            unchecked
            {
                uint h = (uint)a * 374761393u + (uint)b * 668265263u + (uint)salt * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        static float N(int a, int b, int salt) => (Hash(a, b, salt) & 0xFFFF) / 65535f;

        static Color32 Shade(Color32 c, float f) =>
            new Color32((byte)Mathf.Clamp(c.r * f, 0, 255), (byte)Mathf.Clamp(c.g * f, 0, 255), (byte)Mathf.Clamp(c.b * f, 0, 255), 255);

        static void Paint(Color32[] px, int w, int h, int id, PropertyKind kind, string name)
        {
            var rng = new System.Random(id * 7919 + 13);
            bool church = name == "The parish church";
            bool hospital = name == "Ospedale";

            // The terrace surface: terracotta tiles, grey cement, or whitewash.
            int surface = church ? 0 : hospital ? 2 : rng.Next(3);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float n = N(x, y, id);
                Color32 c;
                if (surface == 0)
                {
                    bool grout = x % 6 == 0 || y % 6 == 0;
                    c = grout ? new Color32(130, 72, 50, 255) : Shade(new Color32(178, 98, 66, 255), 0.88f + 0.18f * N(x / 6, y / 6, id + 1) + 0.05f * n);
                }
                else if (surface == 1)
                {
                    c = Shade(new Color32(150, 146, 138, 255), 0.9f + 0.1f * n);
                    if (N(x / 3, y, id + 2) < 0.015f) c = Shade(c, 0.75f); // cracks
                }
                else c = Shade(new Color32(222, 216, 204, 255), 0.93f + 0.07f * n);
                px[y * w + x] = c;
            }

            // Parapet around the edge.
            var parapet = surface == 0 ? new Color32(210, 190, 160, 255) : new Color32(200, 196, 188, 255);
            for (int x = 0; x < w; x++)
            for (int k = 0; k < 2; k++)
            {
                px[k * w + x] = Shade(parapet, 0.8f);
                px[(h - 1 - k) * w + x] = parapet;
            }
            for (int y = 0; y < h; y++)
            for (int k = 0; k < 2; k++)
            {
                px[y * w + k] = parapet;
                px[y * w + w - 1 - k] = Shade(parapet, 0.85f);
            }

            if (hospital)
            {
                // A red cross on the roof, for the helicopters that never come.
                int cx = w / 2, cy = h / 2;
                for (int y = -10; y <= 10; y++)
                for (int x = -3; x <= 3; x++)
                {
                    Set(px, w, h, cx + x, cy + y, new Color32(200, 40, 40, 255));
                    Set(px, w, h, cx + y, cy + x, new Color32(200, 40, 40, 255));
                }
                return;
            }
            if (church)
            {
                // A small dome.
                Disc(px, w, h, w / 2f, h * 0.6f, Mathf.Min(w, h) * 0.22f, new Color32(150, 160, 170, 255), id);
                Disc(px, w, h, w / 2f, h * 0.6f, 3f, new Color32(210, 180, 90, 255), id);
                return;
            }

            // The stairwell hut every terrace has.
            if (w >= 48 && h >= 48)
            {
                int hx = rng.Next(4, w - 30), hy = rng.Next(4, h - 30);
                Box(px, w, h, hx, hy, 26, 22, Shade(parapet, 0.9f));
                Box(px, w, h, hx + 2, hy + 2, 22, 18, Shade(parapet, 1.05f));
                Box(px, w, h, hx + 8, hy, 8, 3, new Color32(90, 60, 40, 255)); // door
            }

            int items = 2 + rng.Next(4);
            for (int i = 0; i < items; i++)
            {
                int x = rng.Next(6, Mathf.Max(7, w - 14)), y = rng.Next(6, Mathf.Max(7, h - 14));
                switch (rng.Next(5))
                {
                    case 0: // water tank
                        Disc(px, w, h, x + 5, y + 5, 5.5f, new Color32(60, 60, 64, 255), id);
                        Disc(px, w, h, x + 5, y + 5, 4.5f, new Color32(220, 220, 214, 255), id);
                        Disc(px, w, h, x + 5, y + 5, 1.5f, new Color32(170, 170, 166, 255), id);
                        break;
                    case 1: // TV antenna
                        for (int k = 0; k < 12; k++)
                        {
                            Set(px, w, h, x + k, y + 6, new Color32(50, 50, 50, 255));
                            if (k % 3 == 0) for (int j = 3; j < 10; j++) Set(px, w, h, x + k, y + j, new Color32(70, 70, 70, 255));
                        }
                        break;
                    case 2: // potted plants
                        for (int k = 0; k < 3; k++)
                        {
                            Disc(px, w, h, x + k * 5 + 2, y + 2, 2.5f, new Color32(150, 80, 50, 255), id);
                            Disc(px, w, h, x + k * 5 + 2, y + 3, 2.2f, new Color32(70, (byte)(130 + k * 15), 60, 255), id + k);
                        }
                        break;
                    case 3: // washing on a rack
                        for (int k = 0; k < 14; k++) Set(px, w, h, x + k, y + 4, new Color32(60, 60, 60, 255));
                        for (int k = 0; k < 4; k++)
                        {
                            var cloth = new[] { new Color32(240, 240, 235, 255), new Color32(210, 70, 70, 255), new Color32(80, 120, 200, 255), new Color32(240, 200, 80, 255) }[(k + id) % 4];
                            Box(px, w, h, x + k * 3 + 1, y, 2, 4, cloth);
                        }
                        break;
                    default: // a satellite dish
                        Disc(px, w, h, x + 4, y + 4, 4f, new Color32(200, 200, 200, 255), id);
                        Disc(px, w, h, x + 4, y + 4, 1.2f, new Color32(80, 80, 80, 255), id);
                        break;
                }
            }
        }

        static void Set(Color32[] px, int w, int h, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            px[y * w + x] = c;
        }

        static void Box(Color32[] px, int w, int h, int x0, int y0, int bw, int bh, Color32 c)
        {
            for (int y = y0; y < y0 + bh; y++)
            for (int x = x0; x < x0 + bw; x++)
                Set(px, w, h, x, y, c);
        }

        static void Disc(Color32[] px, int w, int h, float cx, float cy, float r, Color32 c, int salt)
        {
            for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++)
            for (int x = (int)(cx - r - 1); x <= cx + r + 1; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                if (dx * dx + dy * dy > r * r) continue;
                Set(px, w, h, x, y, Shade(c, 0.92f + 0.1f * N(x, y, salt)));
            }
        }

        // ---------- Showing and hiding ----------

        void UpdateOpen()
        {
            foreach (var r in roofs)
            {
                bool open = r.Property != null && r.Property.OwnedByPlayer;
                if (!open)
                {
                    foreach (var pawn in game.Pawns.Crew)
                    {
                        if (!pawn.IsPresent || !Covers(r, pawn.Cell)) continue;
                        open = true;
                        break;
                    }
                }
                r.Open = open;
            }
        }

        static bool Covers(Roof r, Vector2Int c)
        {
            if (!r.Bounds.Contains(c)) return false;
            return r.Mask == null || r.Mask[(c.y - r.Bounds.y) * r.Bounds.width + (c.x - r.Bounds.x)];
        }

        void Apply(Roof r)
        {
            r.Sprite.enabled = r.Alpha > 0.01f;
            var c = Color.white;
            c.a = r.Alpha;
            r.Sprite.color = c;
        }

        void LateUpdate()
        {
            if (game == null) return;
            checkTimer += Time.unscaledDeltaTime;
            if (checkTimer > 0.2f)
            {
                checkTimer = 0f;
                UpdateOpen();
            }
            float step = Time.unscaledDeltaTime * FadeSpeed;
            foreach (var r in roofs)
            {
                float target = r.Open ? 0f : 1f;
                if (Mathf.Approximately(r.Alpha, target)) continue;
                r.Alpha = Mathf.MoveTowards(r.Alpha, target, step);
                Apply(r);
            }
        }
    }
}
