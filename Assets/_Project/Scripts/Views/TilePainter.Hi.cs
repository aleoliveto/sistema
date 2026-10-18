using OSistema.World;
using UnityEngine;

namespace OSistema.Views
{
    /// <summary>
    /// The map at twice the detail (32 px per cell): streets, pavements and facades are painted here
    /// at full resolution; furniture and props are painted at 16 px and doubled on top.
    /// </summary>
    public static partial class TilePainter
    {
        public const int Hi = 32;

        static readonly Color32[] Full16 = new Color32[Size * Size];
        static readonly Color32[] Bare16 = new Color32[Size * Size];

        public static void PaintCellHi(Map map, int x, int y, Color32[] buf, int stride, int ox, int oy)
        {
            var terrain = map.GetTerrain(x, y);
            var building = map.GetBuilding(x, y);
            byte style = map.GetStyle(x, y);
            bool wall = building == BuildingDefs.Wall;

            if (wall)
            {
                PaintWallHi(map, x, y, style, buf, stride, ox, oy);
                Weather(terrain, x, y, buf, stride, ox, oy, true, Hi);
                OverlayDecor(map, x, y, buf, stride, ox, oy);
                return;
            }

            bool indoor = Indoor(terrain);
            for (int py = 0; py < Hi; py++)
            for (int px = 0; px < Hi; px++)
                buf[(oy + py) * stride + ox + px] = indoor || terrain == TerrainType.Water
                    ? TerrainPixel(map, terrain, x, y, px / 2, py / 2, style)
                    : TerrainPixelHi(map, terrain, x, y, px, py);
            AmbientOcclusion(map, x, y, buf, stride, ox, oy, Hi);
            if (!indoor) CastShadows(map, x, y, buf, stride, ox, oy, Hi);
            Weather(terrain, x, y, buf, stride, ox, oy, false, Hi);
            if (building == null) return;
            if (PropArt.Has(building)) return;   // drawn as a sprite on top (PropLayer)
            if (HasHiProp(building))
            {
                PaintPropHi(map, building, x, y, style, buf, stride, ox, oy);
                return;
            }

            // Anything else: paint the cell at 16 px with and without it, and double what differs.
            PaintCell(map, x, y, Full16, Size, 0, 0);
            PaintGround16(map, x, y, Bare16);
            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                var a = Full16[py * Size + px];
                var b = Bare16[py * Size + px];
                if (a.r == b.r && a.g == b.g && a.b == b.b) continue;
                int i = (oy + py * 2) * stride + ox + px * 2;
                buf[i] = a;
                buf[i + 1] = a;
                buf[i + stride] = a;
                buf[i + stride + 1] = a;
            }
        }

        static void PaintGround16(Map map, int x, int y, Color32[] buf)
        {
            var terrain = map.GetTerrain(x, y);
            byte style = map.GetStyle(x, y);
            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
                buf[py * Size + px] = TerrainPixel(map, terrain, x, y, px, py, style);
            AmbientOcclusion(map, x, y, buf, Size, 0, 0);
            if (!Indoor(terrain)) CastShadows(map, x, y, buf, Size, 0, 0);
            Weather(terrain, x, y, buf, Size, 0, 0, false);
        }

        static void OverlayDecor(Map map, int x, int y, Color32[] buf, int stride, int ox, int oy)
        {
            if (map.GetDecor(x, y) != MapDecor.Shrine && map.GetDecor(x, y) != MapDecor.WallLamp) return;
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < Full16.Length; i++) Full16[i] = clear;
            PaintWallDecor(map, x, y, IsWallish(map, x, y - 1), Full16, Size, 0, 0);
            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                var a = Full16[py * Size + px];
                if (a.a == 0) continue;
                int i = (oy + py * 2) * stride + ox + px * 2;
                buf[i] = a;
                buf[i + 1] = a;
                buf[i + stride] = a;
                buf[i + stride + 1] = a;
            }
        }

        // ---------- Streets ----------

        static bool Near(Map map, int x, int y, TerrainType t, int px, int py, int reach)
        {
            return (px < reach && IsT(map, x - 1, y, t)) || (px >= Hi - reach && IsT(map, x + 1, y, t)) ||
                   (py < reach && IsT(map, x, y - 1, t)) || (py >= Hi - reach && IsT(map, x, y + 1, t));
        }

        static Color32 TerrainPixelHi(Map map, TerrainType t, int x, int y, int px, int py)
        {
            int gx = x * Hi + px, gy = y * Hi + py;
            float fine = N(gx, gy, 71);
            switch (t)
            {
                case TerrainType.Road:
                {
                    // Worn asphalt: aggregate, repairs, cracks, oil, and a gutter along the kerb.
                    var c = Shade(new Color32(60, 60, 65, 255), (0.9f + 0.12f * fine) * (0.9f + 0.18f * Smooth(gx / 30f, gy / 30f, 72)));
                    if (N(gx, gy, 73) < 0.04f) c = Shade(c, 1.25f);                       // pale stones in the mix
                    if (Smooth(gx / 46f, gy / 46f, 74) > 0.7f) c = Shade(c, 0.86f);        // a patch of newer tar
                    if (Mathf.Abs(Smooth(gx / 13f, gy / 13f, 75) - 0.5f) < 0.012f) c = Shade(c, 0.6f); // cracks
                    float oil = Smooth(gx / 9f, gy / 9f, 76);
                    if (oil > 0.8f) c = Shade(c, 0.78f);
                    if (Near(map, x, y, TerrainType.Sidewalk, px, py, 3)) c = Shade(new Color32(46, 46, 50, 255), 0.9f + 0.15f * fine);
                    // A manhole cover now and then.
                    if (N(x, y, 77) < 0.015f)
                    {
                        float dx = px - 15.5f, dy = py - 15.5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d < 9f) c = d > 8f ? new Color32(36, 36, 38, 255) : ((px + py) % 4 < 2 ? new Color32(70, 68, 66, 255) : new Color32(52, 50, 50, 255));
                    }
                    return c;
                }
                case TerrainType.Sidewalk:
                {
                    // Kerb of grey piperno along the road.
                    if (Near(map, x, y, TerrainType.Road, px, py, 5))
                    {
                        var k = Shade(new Color32(128, 126, 120, 255), 0.88f + 0.16f * Smooth(gx / 5f, gy / 5f, 78) + 0.06f * fine);
                        if (N(gx, gy, 79) < 0.08f) k = Shade(k, 0.7f);   // dark speckles in the stone
                        return k;
                    }
                    return Basolato(gx, gy, fine);
                }
                case TerrainType.Paving:
                {
                    // Big pale slabs, polished by feet.
                    const int sz = 22;
                    int col = gx / sz, row = (gy + (col % 2) * 11) / sz;
                    int lx = gx % sz, ly = (gy + (col % 2) * 11) % sz;
                    if (lx == 0 || ly == 0) return new Color32(96, 92, 86, 255);
                    var c = Shade(new Color32(162, 156, 146, 255), (0.84f + 0.24f * N(col, row, 80)) * (0.94f + 0.08f * fine));
                    if (ly == sz - 1 || lx == 1) c = Shade(c, 1.08f);
                    if (ly == 1 || lx == sz - 1) c = Shade(c, 0.88f);
                    return c;
                }
                case TerrainType.Grass:
                {
                    float clump = Smooth(gx / 6f, gy / 6f, 81);
                    var c = Shade(new Color32(70, 104, 52, 255), 0.75f + 0.4f * clump);
                    if (N(gx, gy, 82) < 0.2f) c = Shade(c, 1.2f + 0.2f * fine);    // blades catching the light
                    if (Smooth(gx / 18f, gy / 18f, 83) > 0.72f) c = Mix(c, new Color32(120, 104, 70, 255), 0.5f); // worn to dirt
                    return c;
                }
                default:
                {
                    // Packed earth with pebbles.
                    var c = Shade(new Color32(116, 100, 78, 255), 0.84f + 0.24f * Smooth(gx / 5f, gy / 5f, 84) + 0.06f * fine);
                    if (N(gx, gy, 85) < 0.025f) c = new Color32(150, 140, 124, 255);
                    return c;
                }
            }
        }

        /// <summary>The basolato: big blocks of dark Vesuvian lava stone in irregular rows, worn smooth.</summary>
        /// <summary>A square of basolato, for backdrops (the character creator).</summary>
        public static Texture2D BasolatoTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var c = Basolato(x + 37, y + 11, N(x, y, 71));
                // A pool of warm light in the middle, dark at the edges.
                float dx = (x - size / 2f) / size, dy = (y - size / 2f) / size;
                float light = 1.15f - 1.4f * (dx * dx + dy * dy);
                px[y * size + x] = Shade(c, light);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static Color32 Basolato(int gx, int gy, float fine)
        {
            const int rowH = 13;
            int row = gy / rowH, ly = gy % rowH;
            int offset = (int)(N(row, 0, 86) * 23f);
            int u = gx + offset;
            int len = 20 + (int)(N(row, 1, 87) * 9f);
            int stone = u / len, lx = u % len;
            bool joint = ly == 0 || lx == 0 || (lx == 1 && N(stone, row, 88) < 0.4f);
            if (joint) return Shade(new Color32(40, 40, 46, 255), 0.9f + 0.2f * fine);
            float tone = 0.78f + 0.34f * N(stone, row, 89);
            var c = Shade(new Color32(80, 79, 88, 255), tone * (0.95f + 0.07f * fine));
            // Each block is slightly domed: lit on its upper-left edge, darker lower-right.
            if (ly >= rowH - 2 || lx <= 2) c = Shade(c, 1.1f);
            if (ly <= 1 || lx >= len - 2) c = Shade(c, 0.86f);
            // Polished where feet go.
            float polish = Smooth(gx / 16f, gy / 16f, 90);
            if (polish > 0.62f) c = Shade(c, 1f + (polish - 0.62f) * 0.5f);
            return c;
        }

        // ---------- Facades ----------

        static readonly Color32[] Shutters =
        {
            new Color32(58, 96, 70, 255), new Color32(64, 104, 74, 255), new Color32(112, 74, 44, 255), new Color32(150, 146, 136, 255),
        };

        static void PaintWallHi(Map map, int x, int y, byte style, Color32[] buf, int stride, int ox, int oy)
        {
            var plaster = Facades[style % Facades.Length];
            bool wallN = IsWallish(map, x, y + 1), wallS = IsWallish(map, x, y - 1);
            bool wallE = IsWallish(map, x + 1, y), wallW = IsWallish(map, x - 1, y);
            const int faceH = 12;   // the south face, seen at an angle
            var tuff = Mix(new Color32(176, 160, 122, 255), plaster, 0.35f);

            for (int py = 0; py < Hi; py++)
            for (int px = 0; px < Hi; px++)
            {
                int gx = x * Hi + px, gy = y * Hi + py;
                float fine = N(gx, gy, 91);
                Color32 c;
                bool face = !wallS && py < faceH;
                if (face)
                {
                    // Plaster in shade, streaked by years of rain running down from the top.
                    float streak = 0.9f + 0.14f * N(gx / 2, 0, 92) * Smooth(gx / 3f, gy / 12f, 93);
                    c = Shade(plaster, 0.64f * streak * (0.95f + 0.06f * fine));
                    if (py < 3) c = Shade(new Color32(98, 94, 88, 255), 0.9f + 0.15f * fine);      // a base of piperno
                    if (py == faceH - 1) c = Shade(plaster, 0.48f);                                // the shadow under the cornice
                }
                else
                {
                    // The top of the wall, in the sun.
                    c = Shade(plaster, (0.86f + 0.16f * Smooth(gx / 10f, gy / 10f, 94)) * (0.96f + 0.06f * fine));
                    // Plaster fallen away, showing the yellow tuff blocks underneath.
                    float peel = Smooth(gx / 11f, gy / 11f, 95) * (0.7f + 0.3f * Smooth(gx / 40f, gy / 40f, 99));
                    if (peel > 0.7f)
                    {
                        bool mortar = gy % 7 == 0 || (gx + (gy / 7 % 2) * 6) % 12 == 0;
                        c = mortar ? Shade(tuff, 0.7f) : Shade(tuff, 0.88f + 0.2f * N(gx / 12, gy / 7, 96));
                    }
                    else if (peel > 0.68f) c = Shade(c, 0.86f);   // the broken edge of the plaster
                }
                // Soft edges where the wall ends.
                if ((!wallN && py >= Hi - 2) || (!wallW && px < 2) || (!wallE && px >= Hi - 2)) c = Shade(c, 0.72f);
                buf[(oy + py) * stride + ox + px] = c;
            }

            if (wallS || N(x, y, 19) >= 0.5f) return;

            // A window with wooden persiane, often a little iron balcony with plants.
            void Px(int px, int py, Color32 c)
            {
                if (px < 0 || py < 0 || px >= Hi || py >= Hi) return;
                buf[(oy + py) * stride + ox + px] = c;
            }
            void Box(int x0, int y0, int x1, int y1, Color32 c)
            {
                for (int yy = y0; yy <= y1; yy++)
                for (int xx = x0; xx <= x1; xx++) Px(xx, yy, c);
            }

            bool lit = N(x, y, 21) < 0.5f;
            var shutter = Shutters[(int)(N(x, y, 97) * Shutters.Length) % Shutters.Length];
            Box(10, 3, 21, 10, Shade(plaster, 0.42f));                                   // the recess
            Box(11, 4, 20, 10, lit ? new Color32(214, 182, 112, 255) : new Color32(36, 44, 58, 255));
            Box(15, 4, 16, 10, new Color32(70, 60, 50, 255));                            // the mullion
            bool open = N(x, y, 98) < 0.6f;
            if (open)
            {
                for (int yy = 4; yy <= 10; yy++)
                {
                    var slat = yy % 2 == 0 ? shutter : Shade(shutter, 0.72f);
                    Box(6, yy, 9, yy, slat);
                    Box(22, yy, 25, yy, slat);
                }
            }
            else
            {
                for (int yy = 4; yy <= 10; yy++) Box(11, yy, 20, yy, yy % 2 == 0 ? shutter : Shade(shutter, 0.72f));
                Box(15, 4, 16, 10, Shade(shutter, 0.6f));
            }
            Box(9, 3, 22, 3, Shade(plaster, 1.08f));                                      // the sill

            if (N(x, y, 24) < 0.55f)
            {
                var iron = new Color32(34, 32, 32, 255);
                Box(5, 0, 26, 0, iron);
                Box(5, 2, 26, 2, iron);
                for (int xx = 5; xx <= 26; xx += 3) Px(xx, 1, iron);
                if (N(x, y, 25) < 0.6f)
                {
                    Box(6, 1, 8, 2, new Color32(150, 82, 54, 255));
                    Box(6, 3, 8, 4, new Color32(64, 120, 56, 255));
                    Px(7, 5, new Color32(204, 54, 64, 255));                                 // geraniums
                    Box(23, 1, 25, 2, new Color32(150, 82, 54, 255));
                    Box(23, 3, 25, 4, new Color32(70, 128, 60, 255));
                }
            }
        }
    }
}
