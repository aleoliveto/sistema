using OSistema.World;
using UnityEngine;

namespace OSistema.Views
{
    /// <summary>
    /// Furniture and street props at 32 px per cell: rounded, shaded from the top-left, each casting a
    /// soft shadow to the north-east like the buildings. Anything not drawn here is doubled from 16 px.
    /// </summary>
    public static partial class TilePainter
    {
        static readonly Color32[] Scratch32 = new Color32[Hi * Hi];

        /// <summary>A small drawing kit over one 32x32 cell. y grows north; Transpose swaps axes.</summary>
        sealed class Pen
        {
            readonly Color32[] buf;
            readonly int stride, ox, oy;
            public bool Transpose;

            public Pen(Color32[] buf, int stride, int ox, int oy)
            {
                this.buf = buf;
                this.stride = stride;
                this.ox = ox;
                this.oy = oy;
            }

            int Index(int x, int y)
            {
                if (Transpose) (x, y) = (y, x);
                if (x < 0 || y < 0 || x >= Hi || y >= Hi) return -1;
                return (oy + y) * stride + ox + x;
            }

            public void Px(int x, int y, Color32 c)
            {
                int i = Index(x, y);
                if (i >= 0) buf[i] = c;
            }

            public void Dark(int x, int y, float f)
            {
                int i = Index(x, y);
                if (i < 0) return;
                var c = buf[i];
                buf[i] = new Color32((byte)(c.r * f * 0.94f), (byte)(c.g * f * 0.97f), (byte)Mathf.Min(255, c.b * f * 1.04f), 255);
            }

            /// <summary>A rounded box lit from the top-left, darker at the rim: reads as a solid object.</summary>
            public void Body(int x0, int y0, int x1, int y1, int r, Color32 c, float dome = 0.25f, int salt = 0, float grain = 0.04f)
            {
                float cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f, hw = (x1 - x0) / 2f + 0.5f, hh = (y1 - y0) / 2f + 0.5f;
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int dx = Mathf.Max(0, Mathf.Max(x0 + r - x, x - (x1 - r))), dy = Mathf.Max(0, Mathf.Max(y0 + r - y, y - (y1 - r)));
                    if (r > 0 && dx * dx + dy * dy > r * r) continue;
                    float u = (x - cx) / hw, v = (y - cy) / hh;
                    float rim = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                    float f = 1f + dome * (-0.35f * u + 0.35f * v) - dome * 0.6f * rim * rim * rim;
                    f *= 1f - grain + 2f * grain * N(x, y, salt);
                    Px(x, y, Shade(c, f));
                }
            }

            public void Disc(float cx, float cy, float r, Color32 c, float dome = 0.3f, int salt = 0)
            {
                for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++)
                for (int x = (int)(cx - r - 1); x <= cx + r + 1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d2 = dx * dx + dy * dy;
                    if (d2 > r * r) continue;
                    float d = Mathf.Sqrt(d2) / r;
                    float f = 1f + dome * (-0.4f * dx / r + 0.4f * dy / r) - dome * 0.5f * d * d;
                    Px(x, y, Shade(c, f * (0.96f + 0.08f * N(x, y, salt))));
                }
            }

            public void Box(int x0, int y0, int x1, int y1, Color32 c)
            {
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) Px(x, y, c);
            }

            /// <summary>Soft shadow of a box, cast towards the north-east. Draw it before the object.</summary>
            public void Shadow(int x0, int y0, int x1, int y1, int len = 3, float f = 0.62f)
            {
                for (int y = y0 + 1; y <= y1 + len; y++)
                for (int x = x0 + 1; x <= x1 + len; x++)
                {
                    bool edge = x > x1 + len - 1 || y > y1 + len - 1 || x == x0 + 1 || y == y0 + 1;
                    Dark(x, y, edge ? 1f - (1f - f) * 0.5f : f);
                }
            }

            public void ShadowDisc(float cx, float cy, float r, float len = 3f, float f = 0.6f)
            {
                float sx = cx + len, sy = cy + len * 0.8f;
                for (int y = (int)(sy - r - 1); y <= sy + r + 1; y++)
                for (int x = (int)(sx - r - 1); x <= sx + r + 1; x++)
                {
                    float dx = x + 0.5f - sx, dy = y + 0.5f - sy, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > r) continue;
                    Dark(x, y, d > r - 1.5f ? 1f - (1f - f) * 0.5f : f);
                }
            }
        }

        static readonly Color32 Glass = new Color32(46, 58, 70, 255);
        static readonly Color32 Rubber = new Color32(24, 24, 26, 255);
        static readonly Color32 Iron = new Color32(38, 36, 36, 255);
        static readonly Color32 Linen = new Color32(232, 228, 216, 255);

        static bool HasHiProp(BuildingDef b) =>
            b == BuildingDefs.Tree || b == BuildingDefs.ParkedCar || b == BuildingDefs.ParkedScooter || b == BuildingDefs.Bench ||
            b == BuildingDefs.Bin || b == BuildingDefs.StreetLamp || b == BuildingDefs.MarketStall || b == BuildingDefs.PlasticChair ||
            b == BuildingDefs.Fountain || b == BuildingDefs.Bed || b == BuildingDefs.Table || b == BuildingDefs.Sofa ||
            b == BuildingDefs.Armchair || b == BuildingDefs.Stove || b == BuildingDefs.Fridge || b == BuildingDefs.KitchenCounter ||
            b == BuildingDefs.Wardrobe || b == BuildingDefs.Plant || b == BuildingDefs.Rug || b == BuildingDefs.Television ||
            b == BuildingDefs.Pantry || b == BuildingDefs.Safe;

        /// <summary>Paints a prop over the ground already in buf, turned as placed.</summary>
        static void PaintPropHi(Map map, BuildingDef b, int x, int y, byte style, Color32[] buf, int stride, int ox, int oy)
        {
            int rot = map.InBounds(x, y) ? map.GetRotation(x, y) : 0;
            if (rot == 0)
            {
                DrawProp(map, b, x, y, style, new Pen(buf, stride, ox, oy));
                return;
            }
            for (int py = 0; py < Hi; py++)
            for (int px = 0; px < Hi; px++)
            {
                var (sx, sy) = RotateHi(px, py, rot);
                Scratch32[py * Hi + px] = buf[(oy + sy) * stride + ox + sx];
            }
            DrawProp(map, b, x, y, style, new Pen(Scratch32, Hi, 0, 0));
            for (int py = 0; py < Hi; py++)
            for (int px = 0; px < Hi; px++)
            {
                var (sx, sy) = RotateHi(px, py, rot);
                buf[(oy + sy) * stride + ox + sx] = Scratch32[py * Hi + px];
            }
        }

        /// <summary>The build ghost: the prop alone on a transparent 32 px tile, turned as it will be placed.</summary>
        public static void PaintPreviewHi(Map map, BuildingDef def, int x, int y, int rot, Color32[] buf)
        {
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < Hi * Hi; i++) Scratch32[i] = clear;
            if (HasHiProp(def))
            {
                var pen = new Pen(Scratch32, Hi, 0, 0);
                DrawProp(map, def, x, y, map.InBounds(x, y) ? map.GetStyle(x, y) : (byte)0, pen);
                // Shadows drawn on nothing come out as dark smudges: keep them faint.
                for (int i = 0; i < Hi * Hi; i++)
                    if (Scratch32[i].a == 255 && Scratch32[i].r < 8 && Scratch32[i].g < 8) Scratch32[i] = new Color32(0, 0, 0, 70);
            }
            else
            {
                var small = new Color32[Size * Size];
                PaintPreview(map, def, x, y, 0, small);
                for (int py = 0; py < Hi; py++)
                for (int px = 0; px < Hi; px++) Scratch32[py * Hi + px] = small[(py / 2) * Size + px / 2];
            }
            for (int py = 0; py < Hi; py++)
            for (int px = 0; px < Hi; px++)
            {
                var (sx, sy) = RotateHi(px, py, rot);
                buf[sy * Hi + sx] = Scratch32[py * Hi + px];
            }
        }

        static (int, int) RotateHi(int x, int y, int quarterTurns) => quarterTurns switch
        {
            1 => (Hi - 1 - y, x),
            2 => (Hi - 1 - x, Hi - 1 - y),
            3 => (y, Hi - 1 - x),
            _ => (x, y),
        };

        static void DrawProp(Map map, BuildingDef b, int x, int y, byte style, Pen p)
        {
            float h = N(x, y, 11);
            int salt = x * 31 + y;

            if (b == BuildingDefs.Tree)
            {
                // A plane tree's crown: clumps of leaves, lit on top, darker underneath.
                p.ShadowDisc(15f, 15f, 14f, 5f, 0.55f);
                var leaf = Shade(new Color32(78, 118, 58, 255), 0.9f + 0.25f * h);
                p.Disc(15.5f, 15.5f, 13.5f, Shade(leaf, 0.78f), 0.2f, salt);
                for (int k = 0; k < 22; k++)
                {
                    float a = N(salt, k, 1) * 6.283f, d = N(salt, k, 2) * 10f;
                    float cx = 15.5f + Mathf.Cos(a) * d, cy = 15.5f + Mathf.Sin(a) * d;
                    float lift = 0.85f + 0.35f * Mathf.Clamp01((cy - cx + 16f) / 32f);   // brighter towards the top-left
                    p.Disc(cx, cy, 3.2f + N(salt, k, 3) * 2.6f, Shade(leaf, lift * (0.9f + 0.25f * N(salt, k, 4))), 0.45f, salt + k);
                }
                return;
            }

            if (b == BuildingDefs.ParkedCar)
            {
                // Seen from above, nose to the north; turned to lie along the kerb.
                p.Transpose = !(IsT(map, x - 1, y, TerrainType.Sidewalk) || IsT(map, x + 1, y, TerrainType.Sidewalk));
                var body = CarColors[(int)(h * CarColors.Length) % CarColors.Length];
                p.Shadow(6, 0, 25, 30, 3, 0.55f);
                p.Body(6, 0, 25, 31, 5, body, 0.3f, salt, 0.015f);
                p.Body(9, 9, 22, 22, 3, Shade(body, 1.1f), 0.2f, salt, 0.01f);                 // roof
                for (int yy = 22; yy <= 25; yy++) p.Box(9 + (25 - yy) / 2, yy, 22 - (25 - yy) / 2, yy, Shade(Glass, 0.9f + 0.05f * (yy - 22)));
                for (int yy = 5; yy <= 8; yy++) p.Box(9 + (yy - 5) / 2, yy, 22 - (yy - 5) / 2, yy, Shade(Glass, 0.85f));
                p.Box(7, 12, 8, 20, Shade(Glass, 0.8f));
                p.Box(23, 12, 24, 20, Shade(Glass, 0.8f));
                p.Px(5, 22, Shade(body, 0.7f));
                p.Px(26, 22, Shade(body, 0.7f));                                               // mirrors
                p.Box(8, 30, 10, 31, new Color32(236, 230, 200, 255));
                p.Box(21, 30, 23, 31, new Color32(236, 230, 200, 255));                        // headlights
                p.Box(8, 0, 10, 0, new Color32(150, 30, 30, 255));
                p.Box(21, 0, 23, 0, new Color32(150, 30, 30, 255));                            // tail lights
                return;
            }

            if (b == BuildingDefs.ParkedScooter)
            {
                var body = CarColors[(int)(h * CarColors.Length) % CarColors.Length];
                p.Shadow(12, 3, 19, 28, 2, 0.6f);
                p.Body(14, 3, 17, 8, 2, Rubber, 0.2f);                                          // rear wheel
                p.Body(14, 24, 17, 29, 2, Rubber, 0.2f);                                        // front wheel
                p.Body(12, 7, 19, 20, 3, body, 0.35f, salt, 0.01f);                            // body
                p.Body(13, 9, 18, 17, 3, new Color32(30, 28, 28, 255), 0.25f);                 // the seat
                p.Body(12, 20, 19, 24, 2, Shade(body, 1.08f), 0.3f);                           // leg shield
                p.Box(9, 25, 22, 25, new Color32(150, 150, 154, 255));                         // handlebar
                p.Px(9, 26, Rubber);
                p.Px(22, 26, Rubber);
                return;
            }

            if (b == BuildingDefs.Bench)
            {
                var wood = new Color32(128, 94, 60, 255);
                p.Shadow(3, 9, 28, 22, 3, 0.6f);
                p.Box(4, 9, 6, 22, Iron);
                p.Box(25, 9, 27, 22, Iron);
                for (int k = 0; k < 4; k++)
                    p.Body(3, 10 + k * 3, 28, 11 + k * 3, 0, Shade(wood, 0.9f + 0.1f * N(salt, k, 5)), 0.12f, salt + k, 0.08f);
                return;
            }

            if (b == BuildingDefs.Bin)
            {
                p.Shadow(8, 8, 23, 25, 3, 0.6f);
                p.Body(8, 8, 23, 25, 2, new Color32(52, 82, 64, 255), 0.3f, salt);
                p.Box(8, 22, 23, 22, new Color32(34, 56, 44, 255));                            // lid hinge
                if (h < 0.6f)
                {
                    // The bags that never fit inside.
                    p.ShadowDisc(26f, 9f, 4f, 2f);
                    p.Disc(26f, 9f, 4.5f, new Color32(26, 26, 28, 255), 0.6f, salt);
                    if (h < 0.3f) p.Disc(5f, 27f, 4f, new Color32(40, 40, 44, 255), 0.6f, salt + 1);
                }
                return;
            }

            if (b == BuildingDefs.StreetLamp)
            {
                p.ShadowDisc(16f, 16f, 3f, 6f, 0.6f);
                p.Box(16, 16, 26, 17, new Color32(46, 46, 50, 255));                            // the arm
                p.Disc(16f, 16f, 3.5f, new Color32(44, 44, 48, 255), 0.4f);
                p.Disc(26f, 16.5f, 3f, new Color32(255, 232, 170, 255), 0.2f);                  // the lamp
                return;
            }

            if (b == BuildingDefs.MarketStall)
            {
                // A striped awning over crates of fruit.
                bool red = (x + y) % 2 == 0;
                var canvas = red ? new Color32(186, 58, 48, 255) : new Color32(48, 104, 70, 255);
                var white = new Color32(232, 226, 210, 255);
                p.Shadow(0, 6, 31, 31, 3, 0.55f);
                for (int yy = 6; yy < Hi; yy++)
                for (int xx = 0; xx < Hi; xx++)
                {
                    var c = (xx / 4) % 2 == 0 ? canvas : white;
                    float fold = 0.88f + 0.16f * Mathf.Sin((xx % 4) / 4f * Mathf.PI);
                    float sag = 1f - 0.08f * Mathf.Sin((yy - 6) / 26f * Mathf.PI);
                    p.Px(xx, yy, Shade(c, fold * sag * (0.97f + 0.06f * N(xx, yy, salt))));
                }
                for (int xx = 0; xx < Hi; xx++) p.Px(xx, 6, Shade((xx / 4) % 2 == 0 ? canvas : white, 0.62f)); // the scalloped edge
                var fruit = new[] { new Color32(232, 132, 36, 255), new Color32(236, 206, 64, 255), new Color32(196, 36, 32, 255), new Color32(110, 150, 50, 255) };
                for (int k = 0; k < 4; k++)
                {
                    p.Body(k * 8 + 1, 0, k * 8 + 6, 5, 0, new Color32(150, 112, 70, 255), 0.1f, salt + k, 0.08f);
                    var f = fruit[(k + (int)(h * 4)) % fruit.Length];
                    for (int j = 0; j < 4; j++) p.Disc(k * 8 + 2.5f + (j % 2) * 3f, 1.5f + (j / 2) * 2.5f, 1.6f, f, 0.6f, salt + j);
                }
                return;
            }

            if (b == BuildingDefs.PlasticChair)
            {
                var white = new Color32(226, 224, 216, 255);
                p.Shadow(9, 8, 22, 24, 2, 0.65f);
                p.Body(9, 8, 22, 20, 3, white, 0.25f, salt, 0.01f);
                p.Body(9, 20, 22, 24, 2, Shade(white, 0.92f), 0.25f);                          // the back
                for (int xx = 11; xx <= 20; xx += 3) p.Px(xx, 22, Shade(white, 0.7f));
                return;
            }

            if (b == BuildingDefs.Fountain)
            {
                p.ShadowDisc(16f, 16f, 14f, 3f, 0.6f);
                p.Disc(16f, 16f, 14.5f, new Color32(176, 170, 160, 255), 0.25f, salt);
                p.Disc(16f, 16f, 11.5f, new Color32(60, 98, 116, 255), 0.1f, salt + 1);
                for (int k = 0; k < 6; k++) p.Disc(10f + N(salt, k, 6) * 12f, 10f + N(salt, k, 7) * 12f, 0.9f, new Color32(190, 220, 230, 255), 0f);
                p.Disc(16f, 16f, 3f, new Color32(166, 160, 150, 255), 0.4f);
                return;
            }

            if (b == BuildingDefs.Bed)
            {
                var blanket = Blankets[style % Blankets.Length];
                p.Shadow(3, 0, 28, 31, 2, 0.7f);
                p.Body(3, 0, 28, 31, 2, new Color32(96, 66, 42, 255), 0.15f, salt, 0.06f);   // frame
                p.Body(4, 1, 27, 30, 2, Linen, 0.15f, salt);                                    // sheet
                p.Body(7, 23, 24, 29, 3, new Color32(246, 244, 238, 255), 0.4f, salt);         // pillow
                // The blanket, with folds.
                for (int yy = 1; yy <= 18; yy++)
                for (int xx = 4; xx <= 27; xx++)
                {
                    float fold = 0.9f + 0.1f * Mathf.Sin(xx * 0.5f + Mathf.Sin(yy * 0.4f) * 1.5f) - (xx == 4 || xx == 27 ? 0.15f : 0f);
                    p.Px(xx, yy, Shade(blanket, fold * (0.97f + 0.06f * N(xx, yy, salt))));
                }
                for (int xx = 4; xx <= 27; xx++)
                {
                    p.Px(xx, 19, Shade(Linen, 0.95f));
                    p.Px(xx, 18, Shade(blanket, 1.15f));                                        // turned-down edge
                }
                return;
            }

            if (b == BuildingDefs.Table)
            {
                p.Shadow(2, 3, 29, 28, 3, 0.65f);
                if (h < 0.4f)
                {
                    // A checked tablecloth.
                    for (int yy = 3; yy <= 28; yy++)
                    for (int xx = 2; xx <= 29; xx++)
                    {
                        bool check = ((xx / 4) + (yy / 4)) % 2 == 0;
                        float ripple = 0.95f + 0.05f * Mathf.Sin(xx * 0.7f);
                        p.Px(xx, yy, Shade(check ? new Color32(176, 52, 46, 255) : new Color32(236, 230, 218, 255), ripple));
                    }
                    p.Disc(10f, 18f, 3.5f, new Color32(240, 240, 236, 255), 0.4f);
                    p.Disc(21f, 12f, 3.5f, new Color32(240, 240, 236, 255), 0.4f);
                    return;
                }
                var wood = new Color32(138, 98, 62, 255);
                for (int yy = 3; yy <= 28; yy++)
                for (int xx = 2; xx <= 29; xx++)
                {
                    float grain = 0.92f + 0.08f * Mathf.Sin(yy * 1.7f + Mathf.Sin(xx * 0.15f + salt) * 2f);
                    float edge = xx == 2 || yy == 28 ? 1.1f : xx == 29 || yy == 3 ? 0.8f : 1f;
                    p.Px(xx, yy, Shade(wood, grain * edge));
                }
                return;
            }

            if (b == BuildingDefs.Sofa || b == BuildingDefs.Armchair)
            {
                var cloth = b == BuildingDefs.Sofa ? new Color32(118, 44, 52, 255) : new Color32(150, 112, 74, 255);
                p.Shadow(1, 2, 30, 29, 3, 0.65f);
                p.Body(1, 2, 30, 29, 4, Shade(cloth, 0.85f), 0.3f, salt);                     // frame
                p.Body(2, 22, 29, 29, 3, Shade(cloth, 0.95f), 0.4f, salt + 1);                 // backrest
                p.Body(1, 3, 6, 22, 3, Shade(cloth, 0.92f), 0.4f, salt + 2);                   // arms
                p.Body(25, 3, 30, 22, 3, Shade(cloth, 0.92f), 0.4f, salt + 3);
                if (b == BuildingDefs.Sofa)
                {
                    p.Body(7, 4, 15, 21, 2, cloth, 0.45f, salt + 4);
                    p.Body(16, 4, 24, 21, 2, cloth, 0.45f, salt + 5);
                }
                else p.Body(7, 4, 24, 21, 3, cloth, 0.45f, salt + 4);
                return;
            }

            if (b == BuildingDefs.Stove)
            {
                p.Shadow(2, 2, 29, 29, 2, 0.7f);
                p.Body(2, 2, 29, 29, 2, new Color32(214, 212, 206, 255), 0.15f, salt, 0.01f);
                foreach (var (cx, cy) in new[] { (9.5f, 9.5f), (22.5f, 9.5f), (9.5f, 22.5f), (22.5f, 22.5f) })
                {
                    p.Disc(cx, cy, 5f, new Color32(30, 30, 32, 255), 0.1f);
                    p.Disc(cx, cy, 2.2f, new Color32(66, 66, 70, 255), 0.3f);
                    for (int k = -4; k <= 4; k++)
                    {
                        p.Px((int)cx + k, (int)cy, Iron);
                        p.Px((int)cx, (int)cy + k, Iron);
                    }
                }
                if (h < 0.5f) p.Disc(22.5f, 22.5f, 4.5f, new Color32(150, 150, 154, 255), 0.5f);   // the moka pot's pan
                return;
            }

            if (b == BuildingDefs.Fridge)
            {
                p.Shadow(3, 3, 28, 28, 4, 0.6f);
                p.Body(3, 3, 28, 28, 3, new Color32(226, 228, 228, 255), 0.25f, salt, 0.01f);
                p.Box(4, 6, 27, 6, new Color32(176, 178, 180, 255));                           // door seal
                return;
            }

            if (b == BuildingDefs.KitchenCounter)
            {
                p.Shadow(0, 2, 31, 29, 2, 0.7f);
                for (int yy = 2; yy <= 29; yy++)
                for (int xx = 0; xx <= 31; xx++)
                {
                    float vein = Mathf.Abs(Mathf.Sin(xx * 0.21f + yy * 0.13f + Smooth(xx / 6f, yy / 6f, salt) * 3f));
                    var c = Shade(new Color32(222, 218, 208, 255), vein < 0.06f ? 0.8f : 0.97f + 0.04f * N(xx, yy, salt));
                    if (yy == 2) c = Shade(c, 0.75f);
                    p.Px(xx, yy, c);
                }
                if (h < 0.35f)
                {
                    p.Body(9, 9, 22, 22, 3, new Color32(170, 174, 178, 255), -0.3f, salt);    // a sink
                    p.Disc(15.5f, 15.5f, 1.2f, new Color32(60, 60, 64, 255), 0f);
                }
                return;
            }

            if (b == BuildingDefs.Wardrobe)
            {
                var wood = new Color32(108, 74, 46, 255);
                p.Shadow(1, 4, 30, 30, 4, 0.55f);
                for (int yy = 4; yy <= 30; yy++)
                for (int xx = 1; xx <= 30; xx++)
                {
                    float grain = 0.9f + 0.1f * Mathf.Sin(xx * 1.3f + Mathf.Sin(yy * 0.2f + salt) * 2f);
                    float edge = xx == 1 || yy == 30 ? 1.12f : xx == 30 || yy == 4 ? 0.75f : 1f;
                    p.Px(xx, yy, Shade(wood, grain * edge));
                }
                return;
            }

            if (b == BuildingDefs.Plant)
            {
                p.ShadowDisc(16f, 16f, 9f, 3f, 0.6f);
                p.Disc(16f, 16f, 8f, new Color32(158, 86, 56, 255), 0.4f, salt);
                for (int k = 0; k < 9; k++)
                {
                    float a = k / 9f * 6.283f + h * 3f;
                    float cx = 16f + Mathf.Cos(a) * 6f, cy = 16f + Mathf.Sin(a) * 6f;
                    p.Disc(cx, cy, 3.4f, new Color32(60, (byte)(110 + k * 5), 54, 255), 0.5f, salt + k);
                }
                p.Disc(16f, 16f, 3f, new Color32(80, 140, 66, 255), 0.5f);
                return;
            }

            if (b == BuildingDefs.Rug)
            {
                var c0 = new Color32(140, 46, 40, 255);
                for (int yy = 3; yy <= 28; yy++)
                for (int xx = 2; xx <= 29; xx++)
                {
                    bool border = xx < 5 || xx > 26 || yy < 6 || yy > 25;
                    bool motif = !border && ((xx + yy) % 6 == 0 || (xx - yy + 64) % 6 == 0);
                    var c = border ? new Color32(48, 52, 92, 255) : motif ? new Color32(214, 176, 100, 255) : c0;
                    p.Px(xx, yy, Shade(c, 0.92f + 0.1f * N(xx, yy, salt)));
                }
                for (int xx = 2; xx <= 29; xx += 2)
                {
                    p.Px(xx, 2, Linen);
                    p.Px(xx, 29, Linen);
                }
                return;
            }

            if (b == BuildingDefs.Television)
            {
                p.Shadow(3, 10, 28, 20, 3, 0.6f);
                p.Body(5, 12, 26, 15, 1, new Color32(64, 52, 40, 255), 0.2f);                  // the cabinet
                p.Body(3, 15, 28, 18, 1, new Color32(22, 22, 24, 255), 0.3f);                  // the set
                p.Box(4, 18, 27, 18, new Color32(60, 70, 90, 255));                            // screen glow at the edge
                return;
            }

            if (b == BuildingDefs.Pantry)
            {
                var wood = new Color32(120, 86, 54, 255);
                p.Shadow(1, 6, 30, 30, 3, 0.6f);
                p.Body(1, 6, 30, 30, 1, wood, 0.15f, salt, 0.06f);
                var jars = new[] { new Color32(200, 60, 40, 255), new Color32(230, 200, 80, 255), new Color32(90, 140, 60, 255), new Color32(220, 220, 210, 255) };
                for (int k = 0; k < 12; k++)
                    p.Disc(4.5f + (k % 6) * 4.5f, 12f + (k / 6) * 9f, 1.9f, jars[(k + style) % jars.Length], 0.6f, salt + k);
                return;
            }

            if (b == BuildingDefs.Safe)
            {
                p.Shadow(5, 5, 26, 26, 3, 0.55f);
                p.Body(5, 5, 26, 26, 2, new Color32(64, 68, 74, 255), 0.35f, salt, 0.02f);
                p.Disc(16f, 16f, 4f, new Color32(150, 150, 140, 255), 0.5f);
                p.Disc(16f, 16f, 1.5f, new Color32(40, 40, 40, 255), 0f);
            }
        }
    }
}
