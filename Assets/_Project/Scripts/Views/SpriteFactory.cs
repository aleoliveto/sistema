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

        /// <summary>White disc with a dark outline, 1 unit wide, so tinting gives a coloured body.</summary>
        public static Sprite Disc => disc != null ? disc : disc = MakeCircle(0f, 0.5f, 0.42f);

        /// <summary>Thin white ring used for selection.</summary>
        public static Sprite Ring => ring != null ? ring : ring = MakeCircle(0.4f, 0.5f, -1f);

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
