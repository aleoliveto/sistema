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

        /// <summary>Pixels (of 32) of a wall cell given to its south face: tall palazzi show a lot of face.</summary>
        public const int FaceHeight = 20;

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
                buf[(oy + py) * stride + ox + px] = terrain == TerrainType.Water
                    ? TerrainPixel(map, terrain, x, y, px / 2, py / 2, style)
                    : TerrainPixelHi(map, terrain, x, y, px, py);
            AmbientOcclusion(map, x, y, buf, stride, ox, oy, Hi);
            if (!indoor) CastShadows(map, x, y, buf, stride, ox, oy, Hi);
            Weather(terrain, x, y, buf, stride, ox, oy, false, Hi);
            if (building == null) return;
            if (building.Filler || PropArt.Has(building)) return;   // drawn as a sprite on top (PropLayer)
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

        // Baked ground materials (Resources/Ground, drawn by OSistema/MapLit): the pixel carries only
        // light (grey 128 = as lit) and, in alpha, which material goes under it.
        public const byte GroundAsphalt = 1, GroundBasoli = 2, GroundKerbH = 3, GroundKerbV = 4, GroundSlabs = 5,
            GroundCoppi = 6, GroundTar = 7, GroundCotto = 8, GroundCement = 9, GroundPlaster = 10,
            GroundGrass = 11, GroundEarth = 12, GroundTiles = 13, GroundMarble = 14;

        /// <summary>A baked material tinted: the plaster takes each building's colour. Half the tint reads as the tint, as lit.</summary>
        public static Color32 Tinted(byte id, Color32 tint, float light)
        {
            float k = 0.5f * light;
            return new Color32((byte)Mathf.Clamp(tint.r * k, 0, 255), (byte)Mathf.Clamp(tint.g * k, 0, 255), (byte)Mathf.Clamp(tint.b * k, 0, 255), (byte)(255 - id));
        }

        /// <summary>The painter's hash noise, so views placing things agree with what's painted.</summary>
        public static float Noise(int a, int b, int salt) => N(a, b, salt);

        public static Color32 Ground(byte id, float light)
        {
            byte v = (byte)Mathf.Clamp(128f * light, 0f, 255f);
            return new Color32(v, v, v, (byte)(255 - id));
        }

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
                    // Asphalt (baked, sharp): here only the light on it, darker in the gutter by the kerb.
                    // A manhole cover now and then, painted as it is.
                    if (N(x, y, 77) < 0.015f)
                    {
                        float dx = px - 15.5f, dy = py - 15.5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d < 9f) return d > 8f ? new Color32(36, 36, 38, 255) : ((px + py) % 4 < 2 ? new Color32(70, 68, 66, 255) : new Color32(52, 50, 50, 255));
                    }
                    return Ground(GroundAsphalt, Near(map, x, y, TerrainType.Sidewalk, px, py, 3) ? 0.78f : 1f);
                }
                case TerrainType.Sidewalk:
                {
                    // Kerbstones of grey piperno along the road, running the way the road runs.
                    if (Near(map, x, y, TerrainType.Road, px, py, 5))
                    {
                        bool alongX = (py < 5 && IsT(map, x, y - 1, TerrainType.Road)) || (py >= Hi - 5 && IsT(map, x, y + 1, TerrainType.Road));
                        return Ground(alongX ? GroundKerbH : GroundKerbV, 1f);
                    }
                    return Ground(GroundBasoli, 1f);
                }
                case TerrainType.Paving:
                    return Ground(GroundSlabs, 1f);
                case TerrainType.Grass:
                    return Ground(GroundGrass, 1f);
                case TerrainType.Floor:
                {
                    // Glazed tiles in the home's two colours, a checker of half-cell tiles.
                    byte style = map.GetStyle(x, y);
                    bool alt = ((gx / 16) + (gy / 16)) % 2 == 0;
                    return TilePainter.Tinted(GroundTiles, alt ? FloorA[style % FloorA.Length] : FloorB[style % FloorB.Length], 1.05f);
                }
                case TerrainType.Marble:
                    return Ground(GroundMarble, 1f);
                default:
                    return Ground(GroundEarth, 1f);   // packed earth with grit and pebbles
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

        static void PaintWallHi(Map map, int x, int y, byte style, Color32[] buf, int stride, int ox, int oy)
        {
            var plaster = Facades[style % Facades.Length];
            bool wallN = IsWallish(map, x, y + 1), wallS = IsWallish(map, x, y - 1);
            bool wallE = IsWallish(map, x + 1, y), wallW = IsWallish(map, x - 1, y);
            const int faceH = FaceHeight;   // the south face, seen at an angle

            for (int py = 0; py < Hi; py++)
            for (int px = 0; px < Hi; px++)
            {
                int gx = x * Hi + px, gy = y * Hi + py;
                Color32 c;
                bool face = !wallS && py < faceH;
                if (face)
                {
                    // The south face, in shade: baked plaster in the building's colour (streaks,
                    // peeling to tuff), on a base of grey piperno, under the shadow of the cornice.
                    c = TilePainter.Tinted(GroundPlaster, plaster, 0.66f);
                    if (py < 4) c = Ground(GroundKerbH, 0.7f);
                    if (py == faceH - 1) c = TilePainter.Tinted(GroundPlaster, plaster, 0.48f);
                }
                else
                {
                    // The top of the wall, in the sun.
                    c = TilePainter.Tinted(GroundPlaster, plaster, 0.9f + 0.14f * Smooth(gx / 10f, gy / 10f, 94));
                }
                // Soft edges where the wall ends.
                if ((!wallN && py >= Hi - 2) || (!wallW && px < 2) || (!wallE && px >= Hi - 2)) c = Shade(c, 0.72f);
                buf[(oy + py) * stride + ox + px] = c;
            }
            // Windows and balconies are sprites (FacadeView).
        }
    }
}
