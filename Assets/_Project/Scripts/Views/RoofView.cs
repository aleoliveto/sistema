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
            public List<SpriteRenderer> Items = new List<SpriteRenderer>();
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
                var sprite = CreateRoof(p, interior, out var things);
                roofs.Add(new Roof { Property = p, Bounds = interior, Sprite = sprite, Items = things });
            }
            AddLeftoverRoofs(g);
            UpdateOpen();
            foreach (var r in roofs)
            {
                r.Alpha = r.Open ? 0f : 1f;
                Apply(r);
            }
        }

        SpriteRenderer CreateRoof(Property p, RectInt interior, out List<SpriteRenderer> things)
        {
            int w = interior.width * Tile, h = interior.height * Tile;
            // Point-sampled: alpha tags the baked material per pixel, and must not blend.
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            var items = new List<(string, Vector2, int)>();
            Paint(px, w, h, p.Id, p.Kind, p.Name, items);
            tex.SetPixels32(px);
            tex.Apply(false);

            var go = new GameObject($"Roof {p.Id}");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(interior.x, interior.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, Tile, 0, SpriteMeshType.FullRect);
            sr.sharedMaterial = SpriteFactory.MapMaterial;
            sr.sortingOrder = 6; // above floors and furniture, below people
            things = Furnish(go.transform, items, interior, null);
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
                var sprite = CreateMaskedRoof(bounds, mask, salt++, out var things);
                roofs.Add(new Roof { Bounds = bounds, Mask = mask, Sprite = sprite, Items = things });
            }
        }

        SpriteRenderer CreateMaskedRoof(RectInt bounds, bool[] mask, int salt, out List<SpriteRenderer> things)
        {
            int w = bounds.width * Tile, h = bounds.height * Tile;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            var items = new List<(string, Vector2, int)>();
            Paint(px, w, h, salt, PropertyKind.Home, null, items);
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
            sr.sharedMaterial = SpriteFactory.MapMaterial;
            sr.sortingOrder = 6;
            things = Furnish(go.transform, items, bounds, mask);
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

        // Both keep alpha: on a roof surface it tags the baked material (TilePainter.Ground).
        static Color32 Shade(Color32 c, float f) =>
            new Color32((byte)Mathf.Clamp(c.r * f, 0, 255), (byte)Mathf.Clamp(c.g * f, 0, 255), (byte)Mathf.Clamp(c.b * f, 0, 255), c.a);

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
            new Color32((byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t), (byte)(a.b + (b.b - a.b) * t), a.a);

        // Pixels already in a shadow (so overlapping shadows don't stack), for the roof being painted.
        static bool[] shaded = new bool[0];

        // The sun is low in the south-west: everything throws its shadow up and to the right.
        const int ShadowDx = 5, ShadowDy = 4;

        static void Darken(Color32[] px, int w, int h, int x, int y, float f)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            var c = px[y * w + x];
            px[y * w + x] = new Color32((byte)(c.r * f * 0.95f), (byte)(c.g * f * 0.97f), (byte)Mathf.Min(255, c.b * f * 1.03f), c.a);
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
                    if (shaded[y * w + x]) continue;
                    Darken(px, w, h, x, y, f);
                    shaded[y * w + x] = true;
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
                    if (x < 0 || y < 0 || x >= w || y >= h || shaded[y * w + x]) continue;
                    Darken(px, w, h, x, y, f);
                    shaded[y * w + x] = true;
                }
            }
        }

        static void Paint(Color32[] px, int w, int h, int id, PropertyKind kind, string name, List<(string art, Vector2 at, int rot)> items)
        {
            var rng = new System.Random(id * 7919 + 13);
            shaded = new bool[w * h];
            bool church = name == "The parish church";
            bool hospital = name == "Ospedale";

            // 0: coppi, the old curved terracotta tiles. 1: a tarred lastrico solare. 2: cotto pavers.
            // 3: grey cement tiles. The surfaces are baked materials; here only the light on them.
            int surface = church ? 0 : hospital ? 1 : rng.NextDouble() < 0.3 ? 0 : rng.NextDouble() < 0.45 ? 1 : rng.NextDouble() < 0.6 ? 2 : 3;
            byte material = surface switch { 0 => TilePainter.GroundCoppi, 1 => TilePainter.GroundTar, 2 => TilePainter.GroundCotto, _ => TilePainter.GroundCement };
            var terracotta = new Color32(184, 104, 70, 255);
            var tar = new Color32(118, 114, 108, 255);
            var cotto = new Color32(170, 112, 82, 255);
            var lichen = new Color32(118, 118, 86, 255);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float big = Smooth(x / 40f, y / 40f, id), mid = Smooth(x / 9f, y / 9f, id + 3);
                Color32 c = TilePainter.Ground(material, 0.94f + 0.12f * mid);
                if (surface == 1)
                {
                    // Rain leaves darker tide marks where water stood.
                    float puddle = Smooth(x / 22f, y / 22f, id + 7);
                    if (puddle > 0.68f) c = Shade(c, 0.86f);
                    if (puddle > 0.68f && puddle < 0.7f) c = Shade(c, 0.9f);
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

            // Things on the terrace, as sprites standing on it (see Furnish): the stairwell hut first.
            var taken = new List<Rect>();
            void Place(string art, float pw_, float ph_, int rot)
            {
                if (rot % 180 != 0) (pw_, ph_) = (ph_, pw_);
                for (int tries = 0; tries < 14; tries++)
                {
                    int hiX = (int)(w - pw - 2 - pw_), hiY = (int)(h - pw - 2 - ph_);
                    if (hiX <= pw + 2 || hiY <= pw + 2) return;
                    float x = rng.Next(pw + 2, hiX), y = rng.Next(pw + 2, hiY);
                    var r = new Rect(x - 3, y - 3, pw_ + 6, ph_ + 6);
                    if (taken.Exists(t => t.Overlaps(r))) continue;
                    taken.Add(r);
                    items.Add((art, new Vector2(x + pw_ / 2f, y + ph_ / 2f), rot));
                    return;
                }
            }
            if (w >= 96 && h >= 96) Place("torrino", 46, 38, 0);
            int count = 3 + rng.Next(5);
            for (int i = 0; i < count; i++)
            {
                int rot = rng.Next(2) * 90;
                switch (rng.Next(10))
                {
                    case 0:
                    case 1: Place(rng.NextDouble() < 0.75 ? "tank_black" : "tank_white", 19, 19, 0); break;   // on every Neapolitan roof
                    case 2: Place("antenna", 22, 22, 0); break;
                    case 3: Place(rng.NextDouble() < 0.5 ? "lemon_pot" : "geraniums", 21, 21, 0); break;
                    case 4: Place("rack", 33, 17, rot); break;
                    case 5: Place("solar", 26, 19, 0); break;
                    case 6: Place("dish", 15, 15, 0); break;
                    case 7: Place("table", 29, 29, 0); break;
                    default: Place("ac_unit", 17, 12, rot); break;
                }
            }
        }

        static float HeightOf(string art) => art switch
        {
            "torrino" => 0.9f, "antenna" => 1.2f, "tank_black" or "tank_white" => 0.5f, "dish" => 0.45f,
            "solar" => 0.35f, "rack" => 0.35f, _ => 0.25f,
        };

        /// <summary>Puts the terrace's things on the roof: painted sprites with shadows, fading with it.</summary>
        static List<SpriteRenderer> Furnish(Transform roof, List<(string art, Vector2 at, int rot)> items, RectInt bounds, bool[] mask)
        {
            var list = new List<SpriteRenderer>();
            foreach (var (art, at, rot) in items)
            {
                var cell = new Vector2Int((int)(at.x / Tile), (int)(at.y / Tile));
                if (mask != null && (cell.x >= bounds.width || cell.y >= bounds.height || !mask[cell.y * bounds.width + cell.x])) continue;
                var sprite = PropArt.Load("Roof/roof_" + art);
                if (sprite == null) continue;
                var go = new GameObject(art);
                go.transform.SetParent(roof, false);
                go.transform.localPosition = new Vector3(at.x / Tile, at.y / Tile, 0f);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
                float size = Mathf.Max(sprite.texture.width, sprite.texture.height) / 128f;   // drawn at 128 px a cell
                go.transform.localScale = new Vector3(size, size, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sharedMaterial = SpriteFactory.LitMaterial;
                sr.sortingOrder = 8;
                HeightShadow.Add(sr, HeightOf(art), 0.4f, 7);
                list.Add(sr);
            }
            return list;
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
            bool on = r.Alpha > 0.01f;
            var c = Color.white;
            c.a = r.Alpha;
            r.Sprite.enabled = on;
            r.Sprite.color = c;
            foreach (var item in r.Items)
            {
                item.enabled = on;
                item.color = c;
            }
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
