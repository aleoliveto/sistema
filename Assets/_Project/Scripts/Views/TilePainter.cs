using OSistema.World;
using UnityEngine;

namespace OSistema.Views
{
    /// <summary>
    /// Procedural pixel art for map cells (16x16 pixels each). Placeholder for hand-made tiles,
    /// but readable: paving, plaster facades with a fake 3/4 wall face, furniture, street props.
    /// Pixel (0,0) is a cell's bottom-left corner; y grows north.
    /// </summary>
    public static class TilePainter
    {
        public const int Size = 16;

        // Facade palettes by style: tuff yellow, terracotta, pink, ochre, concrete grey, pale blue.
        // Naples: tuff yellow, pompeian red, faded pink, ochre, grey stone, pale azure.
        static readonly Color32[] Facades =
        {
            new Color32(222, 186, 112, 255), new Color32(176, 84, 62, 255), new Color32(222, 160, 146, 255),
            new Color32(214, 150, 70, 255), new Color32(170, 166, 158, 255), new Color32(170, 196, 206, 255),
        };

        static readonly Color32[] FloorA =
        {
            new Color32(178, 156, 126, 255), new Color32(170, 120, 92, 255), new Color32(190, 182, 168, 255),
            new Color32(150, 160, 170, 255), new Color32(128, 126, 122, 255), new Color32(184, 170, 140, 255),
        };

        static readonly Color32[] FloorB =
        {
            new Color32(160, 138, 110, 255), new Color32(150, 104, 80, 255), new Color32(110, 140, 160, 255),
            new Color32(132, 142, 152, 255), new Color32(116, 114, 110, 255), new Color32(196, 120, 80, 255),
        };

        static readonly Color32[] CarColors =
        {
            new Color32(150, 36, 36, 255), new Color32(40, 64, 124, 255), new Color32(205, 205, 200, 255),
            new Color32(32, 32, 34, 255), new Color32(186, 164, 60, 255), new Color32(92, 112, 92, 255),
            new Color32(120, 30, 70, 255),
        };

        static readonly Color32[] Blankets =
        {
            new Color32(150, 44, 44, 255), new Color32(50, 80, 140, 255), new Color32(70, 110, 70, 255),
            new Color32(150, 110, 40, 255), new Color32(110, 60, 120, 255), new Color32(60, 120, 130, 255),
        };

        // ---------- Noise helpers ----------

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

        static Color32 Mix(Color32 a, Color32 b, float t) =>
            new Color32((byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t), (byte)(a.b + (b.b - a.b) * t), 255);

        // ---------- Entry point ----------

        public static void PaintCell(Map map, int x, int y, Color32[] buf, int stride, int ox, int oy)
        {
            var terrain = map.GetTerrain(x, y);
            var building = map.GetBuilding(x, y);
            byte style = map.GetStyle(x, y);

            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
                buf[(oy + py) * stride + ox + px] = TerrainPixel(map, terrain, x, y, px, py, style);

            bool wallLike = building == BuildingDefs.Wall;
            if (!wallLike)
            {
                AmbientOcclusion(map, x, y, buf, stride, ox, oy);
                if (!Indoor(terrain)) CastShadows(map, x, y, buf, stride, ox, oy);
            }
            Weather(terrain, x, y, buf, stride, ox, oy, wallLike);
            if (building == null) return;

            int rot = map.GetRotation(x, y);
            if (rot == 0 || wallLike || building == BuildingDefs.Door)
            {
                PaintBuilding(map, building, x, y, style, buf, stride, ox, oy);
                return;
            }
            // Rotated furniture: un-rotate the floor into a scratch tile, paint upright, rotate back.
            var scratch = Scratch;
            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                var (sx, sy) = Rotate(px, py, rot);
                scratch[py * Size + px] = buf[(oy + sy) * stride + ox + sx];
            }
            PaintBuilding(map, building, x, y, style, scratch, Size, 0, 0);
            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                var (sx, sy) = Rotate(px, py, rot);
                buf[(oy + sy) * stride + ox + sx] = scratch[py * Size + px];
            }
        }

        static readonly Color32[] Scratch = new Color32[Size * Size];

        /// <summary>Paints a piece of furniture alone on a transparent tile, turned as it will be placed.</summary>
        public static void PaintPreview(Map map, BuildingDef def, int x, int y, int rot, Color32[] buf)
        {
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < Size * Size; i++) Scratch[i] = clear;
            PaintBuilding(map, def, x, y, map.InBounds(x, y) ? map.GetStyle(x, y) : (byte)0, Scratch, Size, 0, 0);
            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                var (sx, sy) = Rotate(px, py, rot);
                buf[sy * Size + sx] = Scratch[py * Size + px];
            }
        }

        // Quarter turns counter-clockwise inside a 16x16 tile.
        static (int, int) Rotate(int x, int y, int quarterTurns) => quarterTurns switch
        {
            1 => (Size - 1 - y, x),
            2 => (Size - 1 - x, Size - 1 - y),
            3 => (y, Size - 1 - x),
            _ => (x, y),
        };

        // ---------- Terrain ----------

        static Color32 TerrainPixel(Map map, TerrainType t, int x, int y, int px, int py, byte style)
        {
            int gx = x * Size + px, gy = y * Size + py;
            float n = N(gx, gy, 1);
            switch (t)
            {
                case TerrainType.Road:
                {
                    var c = Shade(new Color32(56, 56, 62, 255), 0.92f + 0.12f * n);
                    if (N(gx, gy, 2) < 0.025f) c = Shade(c, 0.8f);
                    // Gutters along the kerb.
                    if ((px == 0 && IsT(map, x - 1, y, TerrainType.Sidewalk)) || (px == Size - 1 && IsT(map, x + 1, y, TerrainType.Sidewalk)) ||
                        (py == 0 && IsT(map, x, y - 1, TerrainType.Sidewalk)) || (py == Size - 1 && IsT(map, x, y + 1, TerrainType.Sidewalk)))
                        c = new Color32(40, 40, 44, 255);
                    return c;
                }
                case TerrainType.Sidewalk:
                {
                    // Basolato: dark volcanic stones in a running bond.
                    int row = gy / 4;
                    int offset = (row & 1) * 4;
                    bool mortar = gy % 4 == 0 || (gx + offset) % 8 == 0;
                    Color32 c = mortar ? new Color32(54, 54, 60, 255)
                        : Shade(new Color32(86, 86, 94, 255), 0.82f + 0.3f * N((gx + offset) / 8, row, 3) + 0.05f * n);
                    // Lighter kerb stones next to the road.
                    if ((px <= 1 && IsT(map, x - 1, y, TerrainType.Road)) || (px >= Size - 2 && IsT(map, x + 1, y, TerrainType.Road)) ||
                        (py <= 1 && IsT(map, x, y - 1, TerrainType.Road)) || (py >= Size - 2 && IsT(map, x, y + 1, TerrainType.Road)))
                        c = Shade(new Color32(132, 132, 136, 255), 0.9f + 0.1f * n);
                    return c;
                }
                case TerrainType.Paving:
                {
                    bool mortar = gx % 8 == 0 || gy % 8 == 0;
                    return mortar ? new Color32(112, 106, 98, 255) : Shade(new Color32(156, 150, 140, 255), 0.88f + 0.16f * N(gx / 8, gy / 8, 4) + 0.04f * n);
                }
                case TerrainType.Floor:
                {
                    bool grout = gx % 8 == 0 || gy % 8 == 0;
                    if (grout) return Shade(FloorA[style % FloorA.Length], 0.7f);
                    bool alt = ((gx / 8) + (gy / 8)) % 2 == 0;
                    var c = alt ? FloorA[style % FloorA.Length] : FloorB[style % FloorB.Length];
                    return Shade(c, 0.95f + 0.07f * n);
                }
                case TerrainType.Marble:
                {
                    float v = Mathf.Sin(gx * 0.31f + gy * 0.17f + N(gx / 6, gy / 6, 5) * 3f);
                    var c = Shade(new Color32(228, 223, 214, 255), 0.96f + 0.05f * n);
                    return Mathf.Abs(v) < 0.07f ? Shade(c, 0.82f) : c;
                }
                case TerrainType.Grass:
                {
                    var c = Shade(new Color32(68, 108, 52, 255), 0.82f + 0.3f * N(gx / 3, gy / 3, 6));
                    return N(gx, gy, 7) < 0.12f ? Shade(c, 1.25f) : c;
                }
                case TerrainType.Water:
                {
                    // The gulf: deep blue with sparkles and soft swell lines.
                    float wave = Mathf.Sin(gx * 0.18f + gy * 0.05f + N(gx / 8, gy / 8, 22) * 4f);
                    var c = Shade(new Color32(34, 86, 128, 255), 0.9f + 0.12f * wave + 0.05f * n);
                    if (N(gx, gy, 23) < 0.01f) c = new Color32(190, 220, 235, 255);
                    return c;
                }
                default:
                {
                    var c = Shade(new Color32(112, 98, 76, 255), 0.86f + 0.22f * N(gx / 2, gy / 2, 8));
                    return N(gx, gy, 9) < 0.035f ? new Color32(144, 134, 118, 255) : c;
                }
            }
        }

        static bool Indoor(TerrainType t) => t == TerrainType.Floor || t == TerrainType.Marble;

        // Buildings are tall: anything with walls or a roof blocks the sun.
        static bool Mass(Map map, int x, int y)
        {
            if (!map.InBounds(x, y)) return false;
            var b = map.GetBuilding(x, y);
            return b == BuildingDefs.Wall || b == BuildingDefs.Door || Indoor(map.GetTerrain(x, y));
        }

        /// <summary>
        /// The afternoon sun comes from the south-west, low over the gulf: buildings throw soft shadows
        /// up and to the right across the street. Sampled per pixel so the edge is soft.
        /// </summary>
        static void CastShadows(Map map, int x, int y, Color32[] buf, int stride, int ox, int oy)
        {
            // Quick reject: no building mass within reach.
            bool near = false;
            for (int dy = -2; dy <= 0 && !near; dy++)
            for (int dx = -2; dx <= 0 && !near; dx++)
                if ((dx != 0 || dy != 0) && Mass(map, x + dx, y + dy)) near = true;
            if (!near) return;

            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                int gx = x * Size + px, gy = y * Size + py;
                int hits = 0;
                for (int s = 1; s <= 4; s++)
                {
                    int d = s * 6;
                    int cx = (gx - d) / Size, cy = (gy - (d * 3) / 4) / Size;
                    if (Mass(map, cx, cy)) hits++;
                }
                if (hits == 0) continue;
                float f = 1f - 0.11f * hits;    // up to ~45% darker, softer further out
                int i = (oy + py) * stride + ox + px;
                var c = buf[i];
                // Shadows are a little cooler than the sunlit stone.
                buf[i] = new Color32((byte)(c.r * f * 0.94f), (byte)(c.g * f * 0.97f), (byte)Mathf.Min(255, c.b * f * 1.04f), 255);
            }
        }

        /// <summary>Grime, stains and sun-bleaching in big soft patches, so no surface is a flat colour.</summary>
        static void Weather(TerrainType t, int x, int y, Color32[] buf, int stride, int ox, int oy, bool wall)
        {
            float strength = wall ? 0.16f : Indoor(t) ? 0.06f : t == TerrainType.Water ? 0f : 0.12f;
            if (strength <= 0f) return;
            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                int gx = x * Size + px, gy = y * Size + py;
                // Two octaves of blotchy noise, bilinear so they read as stains rather than pixels.
                float big = Smooth(gx / 24f, gy / 24f, 31), mid = Smooth(gx / 7f, gy / 7f, 32);
                float g = (big - 0.5f) * 1.4f + (mid - 0.5f) * 0.6f;
                float f = 1f + g * strength;
                int i = (oy + py) * stride + ox + px;
                var c = buf[i];
                // Dirt is brownish, not grey.
                buf[i] = new Color32((byte)Mathf.Clamp(c.r * f, 0, 255), (byte)Mathf.Clamp(c.g * (f * 0.98f + 0.02f), 0, 255), (byte)Mathf.Clamp(c.b * (f * 0.95f + 0.05f) - g * 6f, 0, 255), 255);
            }
        }

        static float Smooth(float fx, float fy, int salt)
        {
            int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
            float tx = fx - ix, ty = fy - iy;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = N(ix, iy, salt), b = N(ix + 1, iy, salt), c = N(ix, iy + 1, salt), d = N(ix + 1, iy + 1, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static bool IsT(Map map, int x, int y, TerrainType t) => map.InBounds(x, y) && map.GetTerrain(x, y) == t;
        static bool IsWall(Map map, int x, int y) => map.InBounds(x, y) && map.GetBuilding(x, y) == BuildingDefs.Wall;
        static bool IsWallish(Map map, int x, int y)
        {
            if (!map.InBounds(x, y)) return false;
            var b = map.GetBuilding(x, y);
            return b == BuildingDefs.Wall || b == BuildingDefs.Door;
        }

        // Soft shadows at the foot of walls sell the height.
        static void AmbientOcclusion(Map map, int x, int y, Color32[] buf, int stride, int ox, int oy)
        {
            bool n = IsWall(map, x, y + 1), s = IsWall(map, x, y - 1), e = IsWall(map, x + 1, y), w = IsWall(map, x - 1, y);
            if (!n && !s && !e && !w) return;
            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                float f = 1f;
                if (n) f *= Mathf.Lerp(0.55f, 1f, Mathf.Clamp01((Size - 1 - py) / 5f));
                if (w) f *= Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(px / 3f));
                if (e) f *= Mathf.Lerp(0.8f, 1f, Mathf.Clamp01((Size - 1 - px) / 2f));
                if (s) f *= Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(py / 2f));
                if (f >= 0.999f) continue;
                int i = (oy + py) * stride + ox + px;
                buf[i] = Shade(buf[i], f);
            }
        }

        // ---------- Buildings and props ----------

        static void Set(Color32[] buf, int stride, int ox, int oy, int px, int py, Color32 c)
        {
            if (px < 0 || py < 0 || px >= Size || py >= Size) return;
            buf[(oy + py) * stride + ox + px] = c;
        }

        static void Rect(Color32[] buf, int stride, int ox, int oy, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int py = y0; py <= y1; py++)
            for (int px = x0; px <= x1; px++)
                Set(buf, stride, ox, oy, px, py, c);
        }

        static void Disc(Color32[] buf, int stride, int ox, int oy, float cx, float cy, float r, Color32 c, int salt = 0, float noise = 0f)
        {
            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                float dx = px + 0.5f - cx, dy = py + 0.5f - cy;
                if (dx * dx + dy * dy > r * r) continue;
                var col = noise > 0f ? Shade(c, 1f - noise + 2f * noise * N(px + ox, py + oy, salt)) : c;
                Set(buf, stride, ox, oy, px, py, col);
            }
        }

        static void Darken(Color32[] buf, int stride, int ox, int oy, float cx, float cy, float r, float f)
        {
            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                float dx = px + 0.5f - cx, dy = py + 0.5f - cy;
                if (dx * dx + dy * dy > r * r) continue;
                int i = (oy + py) * stride + ox + px;
                buf[i] = Shade(buf[i], f);
            }
        }

        static void PaintBuilding(Map map, BuildingDef b, int x, int y, byte style, Color32[] buf, int stride, int ox, int oy)
        {
            float h = N(x, y, 11);
            if (b == BuildingDefs.Wall) { PaintWall(map, x, y, style, buf, stride, ox, oy); return; }

            if (b == BuildingDefs.Door)
            {
                bool horizontal = IsWallish(map, x - 1, y) || IsWallish(map, x + 1, y);
                var wood = new Color32(112, 72, 42, 255);
                var frame = new Color32(66, 42, 24, 255);
                if (horizontal)
                {
                    Rect(buf, stride, ox, oy, 0, 4, 15, 11, frame);
                    Rect(buf, stride, ox, oy, 2, 5, 13, 10, wood);
                    for (int px = 2; px <= 13; px += 3) Rect(buf, stride, ox, oy, px, 5, px, 10, Shade(wood, 0.78f));
                }
                else
                {
                    Rect(buf, stride, ox, oy, 4, 0, 11, 15, frame);
                    Rect(buf, stride, ox, oy, 5, 2, 10, 13, wood);
                    for (int py = 2; py <= 13; py += 3) Rect(buf, stride, ox, oy, 5, py, 10, py, Shade(wood, 0.78f));
                }
                return;
            }

            if (b == BuildingDefs.Bed)
            {
                Rect(buf, stride, ox, oy, 1, 0, 14, 15, new Color32(84, 58, 38, 255));
                Rect(buf, stride, ox, oy, 2, 1, 13, 14, new Color32(232, 228, 218, 255));
                Rect(buf, stride, ox, oy, 3, 11, 12, 13, new Color32(250, 250, 246, 255));
                var blanket = Blankets[style % Blankets.Length];
                Rect(buf, stride, ox, oy, 2, 1, 13, 9, blanket);
                Rect(buf, stride, ox, oy, 2, 9, 13, 9, Shade(blanket, 1.25f));
                return;
            }

            if (b == BuildingDefs.Table || b == BuildingDefs.Pew || b == BuildingDefs.Bench)
            {
                var wood = b == BuildingDefs.Table ? new Color32(134, 96, 60, 255) : b.Color;
                int y0 = b == BuildingDefs.Table ? 1 : 5, y1 = b == BuildingDefs.Table ? 14 : 10;
                Darken(buf, stride, ox, oy, 9f, (y0 + y1) / 2f - 1.5f, 7f, 0.8f);
                Rect(buf, stride, ox, oy, 1, y0, 14, y1, Shade(wood, 0.7f));
                Rect(buf, stride, ox, oy, 2, y0 + 1, 13, y1 - 1, wood);
                for (int py = y0 + 2; py < y1; py += 2) Rect(buf, stride, ox, oy, 2, py, 13, py, Shade(wood, 0.85f));
                return;
            }

            if (b == BuildingDefs.Stove)
            {
                Rect(buf, stride, ox, oy, 1, 1, 14, 14, new Color32(52, 52, 56, 255));
                Rect(buf, stride, ox, oy, 2, 2, 13, 13, new Color32(78, 78, 84, 255));
                foreach (var (cx, cy) in new[] { (5f, 5f), (11f, 5f), (5f, 11f), (11f, 11f) })
                {
                    Disc(buf, stride, ox, oy, cx, cy, 2.6f, new Color32(20, 20, 22, 255));
                    Disc(buf, stride, ox, oy, cx, cy, 1.2f, new Color32(60, 60, 64, 255));
                }
                return;
            }

            if (b == BuildingDefs.Pantry)
            {
                Rect(buf, stride, ox, oy, 1, 1, 14, 14, new Color32(92, 64, 40, 255));
                for (int py = 2; py <= 13; py++)
                for (int px = 2; px <= 13; px++)
                {
                    if (py % 4 == 1) { Set(buf, stride, ox, oy, px, py, new Color32(70, 48, 30, 255)); continue; }
                    float j = N(px / 2, py / 4, 12);
                    var jar = j < 0.33f ? new Color32(190, 70, 50, 255) : j < 0.66f ? new Color32(220, 200, 120, 255) : new Color32(90, 140, 80, 255);
                    Set(buf, stride, ox, oy, px, py, px % 2 == 0 ? jar : Shade(jar, 0.8f));
                }
                return;
            }

            if (b.Shop != ShopKind.None)
            {
                // Counter with a striped awning in the shop's colour.
                Rect(buf, stride, ox, oy, 0, 0, 15, 7, new Color32(120, 86, 56, 255));
                Rect(buf, stride, ox, oy, 0, 6, 15, 7, new Color32(160, 120, 80, 255));
                for (int px = 0; px < Size; px++)
                    Rect(buf, stride, ox, oy, px, 8, px, 15, (px / 2) % 2 == 0 ? b.Color : new Color32(240, 236, 226, 255));
                Rect(buf, stride, ox, oy, 0, 8, 15, 8, Shade(b.Color, 0.6f));
                return;
            }

            if (b == BuildingDefs.BurnedShop)
            {
                for (int py = 0; py < Size; py++)
                for (int px = 0; px < Size; px++)
                {
                    float n = N(px + ox, py + oy, 13);
                    var c = n < 0.05f ? new Color32(210, 90, 30, 255) : Shade(new Color32(34, 28, 24, 255), 0.7f + 0.6f * n);
                    Set(buf, stride, ox, oy, px, py, c);
                }
                return;
            }

            if (b == BuildingDefs.Bunker)
            {
                Rect(buf, stride, ox, oy, 3, 3, 12, 12, new Color32(48, 42, 36, 255));
                Rect(buf, stride, ox, oy, 4, 4, 11, 11, new Color32(70, 62, 52, 255));
                Disc(buf, stride, ox, oy, 8f, 8f, 1.8f, new Color32(140, 130, 110, 255));
                return;
            }

            if (b == BuildingDefs.Tree)
            {
                Darken(buf, stride, ox, oy, 9.5f, 6.5f, 6.8f, 0.6f);
                var leaf = Shade(new Color32(58, 100, 48, 255), 0.85f + 0.3f * h);
                Disc(buf, stride, ox, oy, 7.5f, 8.5f, 6.8f, leaf, 14, 0.18f);
                Disc(buf, stride, ox, oy, 6f, 10f, 3.5f, Shade(leaf, 1.22f), 15, 0.12f);
                return;
            }

            if (b == BuildingDefs.ParkedCar)
            {
                bool vertical = IsT(map, x - 1, y, TerrainType.Sidewalk) || IsT(map, x + 1, y, TerrainType.Sidewalk);
                var body = CarColors[(int)(h * CarColors.Length) % CarColors.Length];
                var glass = new Color32(40, 52, 64, 255);
                if (vertical)
                {
                    Rect(buf, stride, ox, oy, 4, 0, 13, 15, new Color32(30, 30, 32, 255)); // shadow
                    Rect(buf, stride, ox, oy, 3, 1, 12, 14, body);
                    Rect(buf, stride, ox, oy, 4, 10, 11, 11, glass);
                    Rect(buf, stride, ox, oy, 4, 3, 11, 4, glass);
                    Rect(buf, stride, ox, oy, 4, 5, 11, 9, Shade(body, 1.15f));
                }
                else
                {
                    Rect(buf, stride, ox, oy, 0, 2, 15, 11, new Color32(30, 30, 32, 255));
                    Rect(buf, stride, ox, oy, 1, 3, 14, 12, body);
                    Rect(buf, stride, ox, oy, 10, 4, 11, 11, glass);
                    Rect(buf, stride, ox, oy, 3, 4, 4, 11, glass);
                    Rect(buf, stride, ox, oy, 5, 4, 9, 11, Shade(body, 1.15f));
                }
                return;
            }

            if (b == BuildingDefs.ParkedScooter)
            {
                var body = CarColors[(int)(h * CarColors.Length) % CarColors.Length];
                Rect(buf, stride, ox, oy, 7, 2, 8, 13, new Color32(25, 25, 25, 255));
                Rect(buf, stride, ox, oy, 6, 5, 9, 11, body);
                Rect(buf, stride, ox, oy, 5, 12, 10, 12, new Color32(160, 160, 165, 255));
                return;
            }

            if (b == BuildingDefs.Bin)
            {
                Darken(buf, stride, ox, oy, 9f, 6f, 5.5f, 0.7f);
                Disc(buf, stride, ox, oy, 8f, 8f, 5f, new Color32(48, 76, 58, 255));
                Disc(buf, stride, ox, oy, 8f, 8f, 4f, new Color32(66, 100, 78, 255));
                return;
            }

            if (b == BuildingDefs.StreetLamp)
            {
                Disc(buf, stride, ox, oy, 8f, 8f, 2.2f, new Color32(40, 40, 44, 255));
                Disc(buf, stride, ox, oy, 8f, 8f, 1.2f, new Color32(255, 230, 160, 255));
                return;
            }

            if (b == BuildingDefs.MarketStall)
            {
                var canvas = (x + y) % 2 == 0 ? new Color32(196, 56, 46, 255) : new Color32(46, 110, 70, 255);
                for (int px = 0; px < Size; px++)
                    Rect(buf, stride, ox, oy, px, 1, px, 14, (px / 3) % 2 == 0 ? canvas : new Color32(236, 232, 220, 255));
                Rect(buf, stride, ox, oy, 0, 1, 15, 1, Shade(canvas, 0.6f));
                return;
            }

            if (b == BuildingDefs.Shrine)
            {
                // Edicola votiva: an arched stone niche, the Madonna in her blue mantle under a gold
                // halo, red votive candles and flowers at her feet.
                var stone = new Color32(212, 202, 182, 255);
                Rect(buf, stride, ox, oy, 2, 1, 13, 12, stone);
                Rect(buf, stride, ox, oy, 3, 13, 12, 13, stone);
                Rect(buf, stride, ox, oy, 5, 14, 10, 14, stone);
                var niche = new Color32(28, 34, 70, 255);
                Rect(buf, stride, ox, oy, 4, 4, 11, 11, niche);
                Rect(buf, stride, ox, oy, 5, 12, 10, 12, niche);
                // Mantle: a robe widening towards the feet.
                for (int py = 4; py <= 10; py++)
                {
                    int half = (11 - py) / 2 + 1;
                    Rect(buf, stride, ox, oy, 8 - half, py, 7 + half, py, new Color32(58, 98, 196, 255));
                }
                Set(buf, stride, ox, oy, 7, 10, new Color32(236, 206, 176, 255)); // face
                Set(buf, stride, ox, oy, 8, 10, new Color32(236, 206, 176, 255));
                Rect(buf, stride, ox, oy, 6, 12, 9, 12, new Color32(226, 186, 70, 255)); // halo
                Set(buf, stride, ox, oy, 5, 11, new Color32(226, 186, 70, 255));
                Set(buf, stride, ox, oy, 10, 11, new Color32(226, 186, 70, 255));
                // Candles and flowers on the ledge.
                Rect(buf, stride, ox, oy, 3, 2, 3, 3, new Color32(190, 30, 30, 255));
                Rect(buf, stride, ox, oy, 12, 2, 12, 3, new Color32(190, 30, 30, 255));
                Set(buf, stride, ox, oy, 3, 4, new Color32(255, 214, 110, 255));
                Set(buf, stride, ox, oy, 12, 4, new Color32(255, 214, 110, 255));
                Set(buf, stride, ox, oy, 5, 2, new Color32(240, 90, 120, 255));
                Set(buf, stride, ox, oy, 7, 2, new Color32(250, 250, 240, 255));
                Set(buf, stride, ox, oy, 9, 2, new Color32(240, 200, 60, 255));
                Set(buf, stride, ox, oy, 10, 2, new Color32(240, 90, 120, 255));
                return;
            }

            if (b == BuildingDefs.Altar)
            {
                Rect(buf, stride, ox, oy, 1, 3, 14, 12, new Color32(236, 232, 222, 255));
                Rect(buf, stride, ox, oy, 1, 12, 14, 12, new Color32(210, 172, 70, 255));
                return;
            }

            if (b == BuildingDefs.Fountain)
            {
                Disc(buf, stride, ox, oy, 8f, 8f, 7.5f, new Color32(150, 146, 138, 255));
                Disc(buf, stride, ox, oy, 8f, 8f, 5.8f, new Color32(70, 128, 166, 255), 16, 0.1f);
                Disc(buf, stride, ox, oy, 8f, 8f, 1.6f, new Color32(170, 166, 158, 255));
                return;
            }

            if (b == BuildingDefs.Sofa || b == BuildingDefs.Armchair)
            {
                var cloth = b.Color;
                int x1 = b == BuildingDefs.Sofa ? 15 : 11, x0 = b == BuildingDefs.Sofa ? 0 : 4;
                Darken(buf, stride, ox, oy, 9f, 6f, 7f, 0.8f);
                Rect(buf, stride, ox, oy, x0, 3, x1, 12, Shade(cloth, 0.65f));         // frame
                Rect(buf, stride, ox, oy, x0, 10, x1, 12, Shade(cloth, 0.8f));         // back
                Rect(buf, stride, ox, oy, x0 + 2, 4, x1 - 2, 9, cloth);                // seat
                if (b == BuildingDefs.Sofa) Rect(buf, stride, ox, oy, 7, 4, 7, 9, Shade(cloth, 0.75f)); // cushion seam
                return;
            }

            if (b == BuildingDefs.Television)
            {
                Rect(buf, stride, ox, oy, 2, 6, 13, 9, new Color32(60, 50, 40, 255));   // cabinet
                Rect(buf, stride, ox, oy, 3, 9, 12, 12, new Color32(20, 20, 24, 255));  // screen
                Rect(buf, stride, ox, oy, 4, 10, 11, 11, new Color32(40, 90, 170, 255)); // Napoli on
                return;
            }

            if (b == BuildingDefs.Wardrobe)
            {
                Rect(buf, stride, ox, oy, 1, 2, 14, 14, Shade(b.Color, 0.7f));
                Rect(buf, stride, ox, oy, 2, 3, 13, 13, b.Color);
                Rect(buf, stride, ox, oy, 7, 3, 8, 13, Shade(b.Color, 0.65f));
                Set(buf, stride, ox, oy, 6, 8, new Color32(210, 180, 90, 255));
                Set(buf, stride, ox, oy, 9, 8, new Color32(210, 180, 90, 255));
                return;
            }

            if (b == BuildingDefs.KitchenCounter)
            {
                Rect(buf, stride, ox, oy, 0, 2, 15, 13, new Color32(150, 120, 90, 255));
                Rect(buf, stride, ox, oy, 0, 10, 15, 13, b.Color);
                Rect(buf, stride, ox, oy, 4, 11, 9, 12, new Color32(170, 175, 180, 255)); // sink
                return;
            }

            if (b == BuildingDefs.Fridge)
            {
                Rect(buf, stride, ox, oy, 3, 1, 12, 14, new Color32(170, 172, 176, 255));
                Rect(buf, stride, ox, oy, 4, 2, 11, 13, b.Color);
                Rect(buf, stride, ox, oy, 4, 9, 11, 9, new Color32(160, 162, 166, 255));
                Set(buf, stride, ox, oy, 10, 11, new Color32(90, 90, 90, 255));
                return;
            }

            if (b == BuildingDefs.Plant)
            {
                Disc(buf, stride, ox, oy, 8f, 6f, 3.5f, new Color32(150, 80, 50, 255));
                Disc(buf, stride, ox, oy, 8f, 9f, 5f, new Color32(60, 120, 60, 255), 31, 0.2f);
                Disc(buf, stride, ox, oy, 6.5f, 10.5f, 2f, new Color32(90, 150, 80, 255));
                return;
            }

            if (b == BuildingDefs.Rug)
            {
                Rect(buf, stride, ox, oy, 0, 1, 15, 14, Shade(b.Color, 0.8f));
                Rect(buf, stride, ox, oy, 2, 3, 13, 12, b.Color);
                Rect(buf, stride, ox, oy, 5, 6, 10, 9, new Color32(210, 170, 90, 255));
                return;
            }

            if (b == BuildingDefs.Safe)
            {
                Rect(buf, stride, ox, oy, 3, 3, 12, 12, new Color32(40, 42, 46, 255));
                Rect(buf, stride, ox, oy, 4, 4, 11, 11, b.Color);
                Disc(buf, stride, ox, oy, 8f, 8f, 2f, new Color32(190, 190, 195, 255));
                return;
            }

            if (b == BuildingDefs.HomeShrine)
            {
                Rect(buf, stride, ox, oy, 3, 4, 12, 13, new Color32(110, 76, 46, 255));
                Rect(buf, stride, ox, oy, 5, 6, 10, 12, new Color32(58, 98, 196, 255));
                Rect(buf, stride, ox, oy, 6, 11, 9, 11, new Color32(226, 186, 70, 255));
                Set(buf, stride, ox, oy, 4, 4, new Color32(255, 214, 110, 255));
                Set(buf, stride, ox, oy, 11, 4, new Color32(255, 214, 110, 255));
                return;
            }

            if (b == BuildingDefs.Railing)
            {
                Rect(buf, stride, ox, oy, 0, 6, 15, 7, new Color32(40, 42, 46, 255));
                for (int px = 1; px < Size; px += 4) Rect(buf, stride, ox, oy, px, 3, px, 9, new Color32(52, 54, 58, 255));
                return;
            }

            if (b == BuildingDefs.HospitalDesk)
            {
                Rect(buf, stride, ox, oy, 1, 2, 14, 13, new Color32(236, 236, 232, 255));
                Rect(buf, stride, ox, oy, 6, 4, 9, 11, new Color32(205, 40, 40, 255));
                Rect(buf, stride, ox, oy, 4, 6, 11, 9, new Color32(205, 40, 40, 255));
                return;
            }

            if (b == BuildingDefs.PlasticChair)
            {
                var white = new Color32(238, 238, 232, 255);
                Darken(buf, stride, ox, oy, 9f, 7f, 4.5f, 0.75f);
                Rect(buf, stride, ox, oy, 5, 4, 11, 10, white);
                Rect(buf, stride, ox, oy, 5, 10, 11, 12, Shade(white, 0.85f));
                return;
            }

            if (b == BuildingDefs.Rubble)
            {
                for (int i = 0; i < 6; i++)
                {
                    int rx = (int)(N(x, y, 20 + i) * 13), ry = (int)(N(x, y, 30 + i) * 13);
                    var stone = Shade(new Color32(130, 124, 112, 255), 0.75f + 0.4f * N(x, y, 40 + i));
                    Rect(buf, stride, ox, oy, rx, ry, rx + 2, ry + 1, stone);
                }
                return;
            }

            // Fallback: the def's flat colour.
            Rect(buf, stride, ox, oy, 1, 1, 14, 14, b.Color);
        }

        static void PaintWall(Map map, int x, int y, byte style, Color32[] buf, int stride, int ox, int oy)
        {
            var top = Facades[style % Facades.Length];
            bool wallN = IsWallish(map, x, y + 1), wallS = IsWallish(map, x, y - 1);
            bool wallE = IsWallish(map, x + 1, y), wallW = IsWallish(map, x - 1, y);
            var outline = Shade(top, 0.42f);
            var face = Shade(top, 0.62f);

            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                var c = Shade(top, 0.94f + 0.08f * N(x * Size + px, y * Size + py, 17));
                // The south face of the wall, seen at an angle.
                if (!wallS && py < 6) c = py == 5 ? Shade(top, 0.5f) : Shade(face, 0.95f + 0.06f * N(px, py, 18));
                if ((!wallN && py == Size - 1) || (!wallW && px == 0) || (!wallE && px == Size - 1)) c = outline;
                Set(buf, stride, ox, oy, px, py, c);
            }

            // Windows with green shutters on the visible face; many with a little iron balcony and plants.
            if (!wallS && N(x, y, 19) < 0.5f)
            {
                bool lit = N(x, y, 21) < 0.5f;
                var glass = lit ? new Color32(230, 200, 120, 255) : new Color32(40, 50, 70, 255);
                Rect(buf, stride, ox, oy, 5, 2, 10, 4, glass);
                Rect(buf, stride, ox, oy, 3, 2, 4, 4, new Color32(64, 104, 74, 255));
                Rect(buf, stride, ox, oy, 11, 2, 12, 4, new Color32(64, 104, 74, 255));
                if (N(x, y, 24) < 0.55f)
                {
                    var iron = new Color32(36, 34, 34, 255);
                    Rect(buf, stride, ox, oy, 3, 0, 12, 0, iron);
                    for (int px = 3; px <= 12; px += 2) Set(buf, stride, ox, oy, px, 1, iron);
                    if (N(x, y, 25) < 0.6f)
                    {
                        Set(buf, stride, ox, oy, 4, 1, new Color32(70, 140, 60, 255));
                        Set(buf, stride, ox, oy, 11, 1, new Color32(200, 60, 70, 255)); // geraniums
                    }
                }
            }
            // An edicola votiva hung on the wall: a little arched niche with the Madonna and a candle.
            if (map.GetDecor(x, y) == MapDecor.Shrine)
            {
                int baseY = !wallS ? 0 : 5;
                var stone = new Color32(214, 204, 184, 255);
                Rect(buf, stride, ox, oy, 4, baseY, 11, baseY + 7, stone);
                Rect(buf, stride, ox, oy, 5, baseY + 8, 10, baseY + 8, stone);
                Rect(buf, stride, ox, oy, 5, baseY + 2, 10, baseY + 7, new Color32(28, 34, 70, 255));
                Rect(buf, stride, ox, oy, 6, baseY + 2, 9, baseY + 5, new Color32(58, 98, 196, 255));
                Rect(buf, stride, ox, oy, 7, baseY + 6, 8, baseY + 6, new Color32(236, 206, 176, 255));
                Rect(buf, stride, ox, oy, 6, baseY + 7, 9, baseY + 7, new Color32(226, 186, 70, 255));
                Set(buf, stride, ox, oy, 5, baseY + 1, new Color32(190, 30, 30, 255));
                Set(buf, stride, ox, oy, 10, baseY + 1, new Color32(240, 90, 120, 255));
            }
            if (map.GetDecor(x, y) == MapDecor.WallLamp)
            {
                Set(buf, stride, ox, oy, 7, 5, new Color32(40, 40, 40, 255));
                Set(buf, stride, ox, oy, 8, 5, new Color32(40, 40, 40, 255));
                Rect(buf, stride, ox, oy, 7, 6, 8, 7, new Color32(255, 220, 150, 255));
            }
        }
    }
}
