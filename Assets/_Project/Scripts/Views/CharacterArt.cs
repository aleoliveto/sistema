using System.Collections.Generic;
using UnityEngine;

namespace OSistema.Views
{
    /// <summary>
    /// The painted character parts in Resources/Characters, cut from the art sheets: clothes, heads,
    /// hair, hats and chains seen from above (each 256x256, anchored at the centre), and portraits.
    /// If a part is missing, the old drawn shapes are used instead.
    /// </summary>
    public static class CharacterArt
    {
        // 256 px canvases; a body is about 205 px across. This makes it just under a cell wide.
        const float PixelsPerUnit = 230f;

        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        public static Texture2D Texture(string kind, int i)
        {
            if (i < 0) return null;
            string key = $"{kind}_{i:00}";
            if (textures.TryGetValue(key, out var tex)) return tex;
            tex = Resources.Load<Texture2D>("Characters/" + key);
            textures[key] = tex;
            return tex;
        }

        public static Sprite Sprite(string kind, int i)
        {
            if (i < 0) return null;
            string key = $"{kind}_{i:00}";
            if (sprites.TryGetValue(key, out var s)) return s;
            var tex = Texture(kind, i);
            s = tex != null ? UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), PixelsPerUnit) : null;
            sprites[key] = s;
            return s;
        }

        /// <summary>How many of a kind of part exist (body_00, body_01, ...).</summary>
        public static int Count(string kind)
        {
            if (counts.TryGetValue(kind, out int n)) return n;
            n = 0;
            while (Texture(kind, n) != null) n++;
            counts[kind] = n;
            return n;
        }

        public static bool Available => Count("body") > 0 && Count("head") > 0 && Count("hair") > 0;

        public static Texture2D Face(int i) => Texture("face", i);
    }
}
