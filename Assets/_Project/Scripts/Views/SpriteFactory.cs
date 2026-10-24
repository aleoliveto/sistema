using UnityEngine;

namespace OSistema.Views
{
    /// <summary>Placeholder sprites generated at runtime, so the prototype needs no art assets.</summary>
    public static class SpriteFactory
    {
        static Sprite square, disc, ring;
        static Material unlit;

        /// <summary>1x1 white square, pivot bottom-left, 1 unit wide.</summary>
        public static Sprite Square
        {
            get
            {
                if (square == null)
                {
                    var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    square = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
                }
                return square;
            }
        }

        static Sprite centeredPixel;

        /// <summary>1x1 white square with a centred pivot, for tracers and lines.</summary>
        public static Sprite CenteredPixel
        {
            get
            {
                if (centeredPixel == null)
                {
                    var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    centeredPixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
                }
                return centeredPixel;
            }
        }

        /// <summary>White disc with a dark outline, 1 unit wide, so tinting gives a coloured body.</summary>
        public static Sprite Disc => disc != null ? disc : disc = MakeCircle(0f, 0.5f, 0.42f);

        /// <summary>Thin white ring used for selection.</summary>
        public static Sprite Ring => ring != null ? ring : ring = MakeCircle(0.4f, 0.5f, -1f);

        static Sprite shoulders, headSprite, hairSprite;

        /// <summary>Top-down shoulders and arms, white with a dark outline (tinted with the shirt colour).</summary>
        public static Sprite Shoulders => shoulders != null ? shoulders : shoulders = MakeEllipse(0.36f, 0.22f, outline: true, cloth: true);

        /// <summary>A head seen from above (tinted with the skin colour).</summary>
        public static Sprite Head => headSprite != null ? headSprite : headSprite = MakeEllipse(0.2f, 0.2f, outline: true);

        /// <summary>Hair covering the back of the head; the face side (north) stays visible.</summary>
        public static Sprite Hair => hairSprite != null ? hairSprite : hairSprite = MakeHair();

        static Sprite MakeEllipse(float rx, float ry, bool outline, bool cloth = false)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = ((x + 0.5f) / size - 0.5f) / rx, dy = ((y + 0.5f) / size - 0.5f) / ry;
                float d = dx * dx + dy * dy;
                if (d > 1f) continue;
                // Rounded volume: lit from the top-left, falling off towards the rim, no cartoon line.
                float rim = Mathf.Sqrt(d);
                float shade = 1.04f - 0.1f * (((x + 0.5f) / size - 0.5f) - ((y + 0.5f) / size - 0.5f)) * 2f - 0.32f * rim * rim * rim;
                if (cloth)
                {
                    // Folds and creases across the shoulders.
                    float fold = Mathf.Sin(x * 0.55f + Mathf.Sin(y * 0.3f) * 2f) * 0.035f + (Hash(x, y) - 0.5f) * 0.05f;
                    shade += fold;
                }
                byte v = (byte)Mathf.Clamp(255 * shade, 0, 255);
                byte a = (byte)(outline ? Mathf.Clamp01((1f - d) * 10f) * 255 : 255);   // soft edge instead of an outline
                px[y * size + x] = new Color32(v, v, v, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeHair()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                float r2 = dx * dx + dy * dy;
                if (r2 > 0.19f * 0.19f || dy > 0.07f) continue; // leave the face uncovered
                // Strands radiating from the crown, darker at the roots.
                float ang = Mathf.Atan2(dy, dx);
                float strand = 0.85f + 0.15f * Mathf.Sin(ang * 22f + Hash(x / 3, y / 3) * 2f);
                float v = strand * (1.05f - Mathf.Sqrt(r2) * 1.6f);
                byte b = (byte)Mathf.Clamp(255 * v, 0, 255);
                byte a = (byte)(Mathf.Clamp01((0.19f * 0.19f - r2) * 900f) * 255);
                px[y * size + x] = new Color32(b, b, b, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        static Sprite scooterSprite;

        /// <summary>A Vespa-style scooter from above, nose up. The body is white so it can be tinted.</summary>
        public static Sprite ScooterBody
        {
            get
            {
                if (scooterSprite != null) return scooterSprite;
                const int w = 32, h = 72;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                var px = new Color32[w * h];
                var tyre = new Color32(22, 22, 24, 255);
                var seat = new Color32(34, 30, 30, 255);
                var chrome = new Color32(70, 70, 74, 255);
                void Ellipse(float cx, float cy, float rx, float ry, Color32 c, bool shade)
                {
                    for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry, d = dx * dx + dy * dy;
                        if (d > 1f) continue;
                        float f = shade ? 1.05f - 0.25f * d - 0.12f * dx : 1f;
                        px[y * w + x] = new Color32((byte)Mathf.Clamp(c.r * f, 0, 255), (byte)Mathf.Clamp(c.g * f, 0, 255), (byte)Mathf.Clamp(c.b * f, 0, 255), (byte)(Mathf.Clamp01((1f - d) * 8f) * 255));
                    }
                }
                Ellipse(16, 8, 3.2f, 7f, tyre, false);                 // rear wheel
                Ellipse(16, 62, 3f, 7f, tyre, false);                  // front wheel
                Ellipse(16, 26, 11f, 19f, new Color32(255, 255, 255, 255), true);   // the rounded rear body
                Ellipse(16, 48, 6f, 12f, new Color32(240, 240, 240, 255), true);    // floorboard and leg shield
                Ellipse(16, 25, 5.5f, 11f, seat, true);                // the seat
                for (int x = 3; x < 29; x++) for (int y = 56; y < 59; y++) px[y * w + x] = chrome;   // handlebar
                Ellipse(16, 58, 3.5f, 2.6f, new Color32(250, 246, 220, 255), false); // headlight
                Ellipse(6, 1, 1.6f, 1.6f, chrome, false);
                tex.SetPixels32(px);
                tex.Apply();
                return scooterSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.42f), 48);
            }
        }

        static Sprite softShadow;

        /// <summary>A blurred contact shadow: dark in the middle, fading to nothing.</summary>
        public static Sprite SoftShadow
        {
            get
            {
                if (softShadow != null) return softShadow;
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 0.5f;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
                tex.SetPixels32(px);
                tex.Apply();
                return softShadow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
        }

        static Material lit;

        /// <summary>Sprites lit by URP 2D lights (day/night, street lamps).</summary>
        public static Material LitMaterial
        {
            get
            {
                if (lit == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    lit = new Material(shader);
                }
                return lit;
            }
        }

        static Material map;

        /// <summary>
        /// The map's chunks: lit like sprites, with the baked ground materials (Resources/Ground) laid
        /// under the pixels the painter tagged. Plain lit sprites if the shader is missing.
        /// </summary>
        public static Material MapMaterial
        {
            get
            {
                if (map != null) return map;
                var shader = Shader.Find("OSistema/MapLit");
                if (shader == null) return LitMaterial;
                map = new Material(shader);
                string[] names = { "asphalt", "basoli", "kerb_h", "kerb_v", "slabs", "coppi", "tar", "cotto", "cement", "plaster", "grass", "earth", "tiles", "marble",
                    "vicolo", "vicolo_h", "piazza", "marciapiede", "graniglia", "basoli_wet" };
                for (int i = 0; i < names.Length; i++)
                {
                    var t = Resources.Load<Texture2D>("Ground/ground_" + names[i]);
                    if (t != null) map.SetTexture("_Mat" + (i + 1), t);
                }
                map.SetFloat("_Period", 8f);
                return map;
            }
        }

        /// <summary>Sprites that ignore 2D lights. Lighting comes from WorldLight.Tint for now.</summary>
        public static Material UnlitMaterial
        {
            get
            {
                if (unlit == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    unlit = new Material(shader);
                }
                return unlit;
            }
        }

        // Radii are fractions of the sprite width. outlineFrom < 0 disables the outline.
        static Sprite MakeCircle(float inner, float outer, float outlineFrom)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            var white = new Color32(255, 255, 255, 255);
            var outline = new Color32(40, 40, 40, 255);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > outer || d < inner) continue;
                px[y * size + x] = outlineFrom >= 0f && d > outlineFrom ? outline : white;
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }

    /// <summary>Global day/night colour applied to every world sprite.</summary>
    public static class WorldLight
    {
        public static Color Tint = Color.white;
    }
}
