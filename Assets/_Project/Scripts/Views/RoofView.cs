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
        const int Tile = 32;   // roof texture pixels per cell: twice the ground's, for detail
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
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
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
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
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

        static float Smooth(float fx, float fy, int salt)
        {
            int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
            float tx = fx - ix, ty = fy - iy;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = N(ix, iy, salt), b = N(ix + 1, iy, salt), c = N(ix, iy + 1, salt), d = N(ix + 1, iy + 1, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static Color32 Mix(Color32 a, Color32 b, float t) =>
            new Color32((byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t), (byte)(a.b + (b.b - a.b) * t), 255);

        // The sun is low in the south-west: everything throws its shadow up and to the right.
        const int ShadowDx = 5, ShadowDy = 4;

        static void Darken(Color32[] px, int w, int h, int x, int y, float f)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            var c = px[y * w + x];
            px[y * w + x] = new Color32((byte)(c.r * f * 0.95f), (byte)(c.g * f * 0.97f), (byte)Mathf.Min(255, c.b * f * 1.03f), 255);
        }

        static void ShadowBox(Color32[] px, int w, int h, int x0, int y0, int bw, int bh, int len, float f = 0.62f)
        {
            // The box swept towards the north-east, minus the box itself.
            for (int t = 1; t <= len; t++)
            {
                int sx = x0 + t, sy = y0 + (t * ShadowDy) / ShadowDx;
                for (int y = sy; y < sy + bh; y++)
                for (int x = sx; x < sx + bw; x++)
                {
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    if (x >= x0 && x < x0 + bw && y >= y0 && y < y0 + bh) continue;
                    if (px[y * w + x].a == 254) continue;
                    Darken(px, w, h, x, y, f);
                    var c = px[y * w + x];
                    c.a = 254;
                    px[y * w + x] = c;
                }
            }
        }

        static void ShadowDisc(Color32[] px, int w, int h, float cx, float cy, float r, float len, float f = 0.6f)
        {
            for (float t = 1f; t <= len; t += 1f)
            {
                float sx = cx + t * ShadowDx / 5f, sy = cy + t * ShadowDy / 5f;
                for (int y = (int)(sy - r); y <= sy + r; y++)
                for (int x = (int)(sx - r); x <= sx + r; x++)
                {
                    float dx = x + 0.5f - sx, dy = y + 0.5f - sy;
                    if (dx * dx + dy * dy > r * r) continue;
                    float ox = x + 0.5f - cx, oy = y + 0.5f - cy;
                    if (ox * ox + oy * oy <= r * r) continue;   // not under the object itself
                    if (x < 0 || y < 0 || x >= w || y >= h || px[y * w + x].a == 254) continue;
                    Darken(px, w, h, x, y, f);
                    var c = px[y * w + x];
                    c.a = 254;
                    px[y * w + x] = c;
                }
            }
        }

        static void Paint(Color32[] px, int w, int h, int id, PropertyKind kind, string name)
        {
            var rng = new System.Random(id * 7919 + 13);
            bool church = name == "The parish church";
            bool hospital = name == "Ospedale";

            // 0: coppi, the old curved terracotta tiles. 1: a tarred lastrico solare. 2: cotto pavers.
            int surface = church ? 0 : hospital ? 1 : rng.NextDouble() < 0.33 ? 0 : rng.NextDouble() < 0.6 ? 1 : 2;
            var terracotta = new Color32(184, 104, 70, 255);
            var tar = new Color32(118, 114, 108, 255);
            var cotto = new Color32(170, 112, 82, 255);
            var lichen = new Color32(118, 118, 86, 255);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float big = Smooth(x / 40f, y / 40f, id), mid = Smooth(x / 9f, y / 9f, id + 3);
                Color32 c;
                if (surface == 0)
                {
                    // Columns of tiles running down the slope; every other column is a channel.
                    const int col = 10, row = 14;
                    int cx = x / col, cy = (y + (cx % 2) * 5) / row;
                    float t = (x % col + 0.5f) / col;
                    bool cap = cx % 2 == 0;
                    float profile = Mathf.Sin(t * Mathf.PI);
                    float light = cap ? 0.78f + 0.3f * profile - 0.12f * t : 0.62f + 0.12f * profile;
                    float tileVar = 0.86f + 0.26f * N(cx, cy, id + 1);
                    c = Shade(terracotta, light * tileVar * (0.92f + 0.16f * mid));
                    int ry = (y + (cx % 2) * 5) % row;
                    if (ry < 2) c = Shade(c, 0.7f);                       // the lip of the tile above
                    if (N(cx, cy, id + 9) < 0.05f) c = Shade(c, 1.18f);    // a newer replacement tile
                    float l = Smooth(x / 14f, y / 14f, id + 5);
                    if (l > 0.66f) c = Mix(c, lichen, Mathf.Clamp01((l - 0.66f) * 3f) * 0.6f);
                }
                else if (surface == 1)
                {
                    c = Shade(tar, 0.86f + 0.2f * big + 0.08f * mid + 0.05f * N(x, y, id + 4));
                    // Rain leaves darker tide marks where water stood.
                    float puddle = Smooth(x / 22f, y / 22f, id + 7);
                    if (puddle > 0.68f) c = Shade(c, 0.86f);
                    if (puddle > 0.68f && puddle < 0.7f) c = Shade(c, 0.9f);
                }
                else
                {
                    const int sz = 16;
                    int px0 = x / sz, py0 = (y + (px0 % 2) * 8) / sz;
                    bool grout = x % sz == 0 || (y + (px0 % 2) * 8) % sz == 0;
                    c = grout ? new Color32(150, 140, 128, 255) : Shade(cotto, (0.85f + 0.25f * N(px0, py0, id + 1)) * (0.9f + 0.14f * mid));
                }
                // Grime and sun-bleaching in big patches.
                c = Shade(c, 0.88f + 0.22f * big);
                px[y * w + x] = c;
            }

            if (surface == 1)
            {
                // Repairs: patches of fresher tar, and cracks.
                for (int i = 0; i < 2 + rng.Next(4); i++)
                {
                    int bx = rng.Next(w), by = rng.Next(h), bw = 14 + rng.Next(30), bh = 10 + rng.Next(24);
                    for (int y = by; y < by + bh; y++)
                    for (int x = bx; x < bx + bw; x++)
                        if (x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = Shade(px[y * w + x], 0.84f);
                }
                for (int i = 0; i < 3 + rng.Next(5); i++)
                {
                    float x = rng.Next(w), y = rng.Next(h), a = (float)rng.NextDouble() * 6.28f;
                    for (int k = 0; k < 30 + rng.Next(40); k++)
                    {
                        a += ((float)rng.NextDouble() - 0.5f) * 0.9f;
                        x += Mathf.Cos(a);
                        y += Mathf.Sin(a);
                        int ix = (int)x, iy = (int)y;
                        if (ix >= 0 && iy >= 0 && ix < w && iy < h) px[iy * w + ix] = Shade(px[iy * w + ix], 0.62f);
                    }
                }
            }

            // Parapet: a low wall all round, lit on top, casting a shadow onto the terrace from the south and west.
            const int pw = 4;
            var parapet = new Color32(198, 186, 164, 255);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int dl = x, dr = w - 1 - x, db = y, dt = h - 1 - y;
                int edge = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dt));
                if (edge < pw)
                {
                    var c = Shade(parapet, 0.92f + 0.12f * Smooth(x / 6f, y / 6f, id + 11));
                    if (edge == 0) c = Shade(c, 0.72f);
                    else if (edge == pw - 1) c = Shade(c, 0.86f);
                    else if (db < pw || dl < pw) c = Shade(c, 1.06f);
                    px[y * w + x] = c;
                    continue;
                }
                // Shadows from the south and west parapets, a soft band on the terrace.
                float sw = Mathf.Max(1f - (dl - pw) / 7f, 1f - (db - pw) / 6f);
                float ao = Mathf.Max(1f - (dr - pw) / 3f, 1f - (dt - pw) / 3f);
                float f = 1f - 0.34f * Mathf.Clamp01(sw) - 0.1f * Mathf.Clamp01(ao);
                if (f < 0.999f) px[y * w + x] = Shade(px[y * w + x], f);
            }

            if (hospital)
            {
                int cx = w / 2, cy = h / 2;
                for (int y = -18; y <= 18; y++)
                for (int x = -6; x <= 6; x++)
                {
                    Set(px, w, h, cx + x, cy + y, new Color32(186, 44, 40, 255));
                    Set(px, w, h, cx + y, cy + x, new Color32(186, 44, 40, 255));
                }
                return;
            }
            if (church)
            {
                // A dome in lead sheet, ribbed, with a gilded lantern.
                float r = Mathf.Min(w, h) * 0.24f, dcx = w / 2f, dcy = h * 0.58f;
                ShadowDisc(px, w, h, dcx, dcy, r, 10f, 0.55f);
                for (int y = (int)(dcy - r); y <= dcy + r; y++)
                for (int x = (int)(dcx - r); x <= dcx + r; x++)
                {
                    float dx = x + 0.5f - dcx, dy = y + 0.5f - dcy, d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    if (d > 1f) continue;
                    float rib = Mathf.Abs(Mathf.Sin(Mathf.Atan2(dy, dx) * 8f)) < 0.08f ? 0.8f : 1f;
                    float lightDome = 1.1f - 0.45f * d - 0.25f * (dx - dy) / (2f * r);
                    Set(px, w, h, x, y, Shade(new Color32(128, 140, 136, 255), lightDome * rib));
                }
                Disc(px, w, h, dcx, dcy, 5f, new Color32(200, 168, 90, 255), id);
                return;
            }

            // The stairwell hut: every terrace has one.
            if (w >= 96 && h >= 96)
            {
                int bw = 44 + rng.Next(16), bh = 38 + rng.Next(14);
                int hx = rng.Next(pw + 2, w - bw - pw - 2), hy = rng.Next(pw + 2, h - bh - pw - 2);
                ShadowBox(px, w, h, hx, hy, bw, bh, 12, 0.55f);
                Box(px, w, h, hx, hy, bw, bh, new Color32(176, 168, 152, 255));
                for (int y = hy + 2; y < hy + bh - 2; y++)
                for (int x = hx + 2; x < hx + bw - 2; x++)
                    Set(px, w, h, x, y, Shade(new Color32(132, 128, 120, 255), 0.9f + 0.15f * Smooth(x / 8f, y / 8f, id + 21)));
                Box(px, w, h, hx + 10, hy - 1, 14, 4, new Color32(92, 64, 44, 255)); // the door, at the foot
            }

            int items = 3 + rng.Next(5);
            for (int i = 0; i < items; i++)
            {
                int x = rng.Next(pw + 8, Mathf.Max(pw + 9, w - 34)), y = rng.Next(pw + 8, Mathf.Max(pw + 9, h - 30));
                switch (rng.Next(8))
                {
                    case 0:
                    case 1: // the black plastic water tank on every Neapolitan roof
                    {
                        float r = 9f + rng.Next(3);
                        ShadowDisc(px, w, h, x + r, y + r, r, 9f);
                        for (int yy = (int)(y); yy <= y + 2 * r; yy++)
                        for (int xx = (int)(x); xx <= x + 2 * r; xx++)
                        {
                            float dx = xx + 0.5f - (x + r), dy = yy + 0.5f - (y + r), d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                            if (d > 1f) continue;
                            float l = 0.9f + 0.6f * Mathf.Clamp01(-(dx - dy) / (2f * r)) - 0.3f * d;
                            Set(px, w, h, xx, yy, Shade(new Color32(40, 40, 44, 255), l));
                        }
                        Disc(px, w, h, x + r, y + r, r * 0.38f, new Color32(58, 58, 62, 255), id);
                        break;
                    }
                    case 2: // TV antenna on a mast
                    {
                        for (int k = 0; k < 18; k++)
                        {
                            Darken(px, w, h, x + k + 3, y + 10 + 3, 0.7f);
                            Set(px, w, h, x + k, y + 10, new Color32(70, 70, 72, 255));
                            if (k % 4 == 0)
                                for (int j = 3; j < 18; j++)
                                {
                                    Darken(px, w, h, x + k + 3, y + j + 3, 0.78f);
                                    Set(px, w, h, x + k, y + j, new Color32(96, 96, 98, 255));
                                }
                        }
                        break;
                    }
                    case 3: // potted lemon tree and geraniums
                    {
                        ShadowDisc(px, w, h, x + 8, y + 8, 8f, 8f, 0.65f);
                        Disc(px, w, h, x + 8, y + 8, 5f, new Color32(150, 82, 54, 255), id);
                        for (int k = 0; k < 5; k++)
                            Disc(px, w, h, x + 5 + rng.Next(7), y + 5 + rng.Next(7), 4f + (float)rng.NextDouble() * 2f, new Color32(56, (byte)(96 + rng.Next(30)), 50, 255), id + k);
                        for (int k = 0; k < 3; k++) Disc(px, w, h, x + 4 + rng.Next(9), y + 4 + rng.Next(9), 1.4f, new Color32(226, 196, 60, 255), id);
                        break;
                    }
                    case 4: // washing on a line between two poles
                    {
                        int len = 26 + rng.Next(12);
                        for (int k = 0; k < len; k++)
                        {
                            Set(px, w, h, x + k, y + 8, new Color32(70, 70, 70, 255));
                            Darken(px, w, h, x + k + 3, y + 11, 0.85f);
                        }
                        var cloths = new[] { new Color32(232, 232, 226, 255), new Color32(170, 62, 60, 255), new Color32(70, 98, 150, 255), new Color32(210, 180, 90, 255), new Color32(140, 150, 160, 255) };
                        for (int k = 2; k < len - 4; k += 5 + rng.Next(3))
                        {
                            var cl = cloths[rng.Next(cloths.Length)];
                            ShadowBox(px, w, h, x + k, y + 3, 4, 6, 3, 0.75f);
                            Box(px, w, h, x + k, y + 3, 4, 6, cl);
                            Box(px, w, h, x + k, y + 3, 4, 1, Shade(cl, 0.8f));
                        }
                        break;
                    }
                    case 5: // solar water heater: panel and tank
                    {
                        ShadowBox(px, w, h, x, y, 24, 16, 8, 0.6f);
                        Box(px, w, h, x, y, 24, 12, new Color32(40, 52, 70, 255));
                        for (int k = 0; k < 24; k += 4) Box(px, w, h, x + k, y, 1, 12, new Color32(120, 130, 140, 255));
                        Box(px, w, h, x, y + 12, 24, 5, new Color32(206, 206, 200, 255));
                        Box(px, w, h, x, y + 16, 24, 1, new Color32(160, 160, 156, 255));
                        break;
                    }
                    case 6: // satellite dish
                    {
                        ShadowDisc(px, w, h, x + 6, y + 6, 6f, 6f, 0.65f);
                        for (int yy = y; yy <= y + 12; yy++)
                        for (int xx = x; xx <= x + 12; xx++)
                        {
                            float dx = xx + 0.5f - (x + 6), dy = yy + 0.5f - (y + 6), d = Mathf.Sqrt(dx * dx + dy * dy) / 6f;
                            if (d > 1f) continue;
                            Set(px, w, h, xx, yy, Shade(new Color32(214, 214, 210, 255), 0.8f + 0.25f * d));
                        }
                        Box(px, w, h, x + 5, y + 5, 3, 3, new Color32(60, 60, 60, 255));
                        break;
                    }
                    default: // plastic chairs round a table, for summer evenings
                    {
                        ShadowDisc(px, w, h, x + 10, y + 10, 6f, 5f, 0.7f);
                        Disc(px, w, h, x + 10, y + 10, 6f, new Color32(222, 220, 212, 255), id);
                        foreach (var (ax, ay) in new[] { (0, 8), (16, 8), (8, 0), (8, 17) })
                        {
                            ShadowBox(px, w, h, x + ax, y + ay, 5, 5, 3, 0.75f);
                            Box(px, w, h, x + ax, y + ay, 5, 5, new Color32(206, 204, 196, 255));
                        }
                        break;
                    }
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
