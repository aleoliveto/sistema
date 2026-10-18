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

        /// <summary>
        /// Indexes from 100 up pick from the later sheets: women (1xx), police (2xx), street people (3xx).
        /// Hair 1xx is women's, hats 2xx police, faces 1xx women and 2xx police.
        /// </summary>
        static string Key(string kind, int i)
        {
            int set = i / 100, n = i % 100;
            string prefix = set switch
            {
                1 => "f",
                2 => "p",
                3 => "s",
                _ => "",
            };
            return $"{prefix}{kind}_{n:00}";
        }

        public static Texture2D Texture(string kind, int i)
        {
            if (i < 0) return null;
            string key = Key(kind, i);
            // Unity throws loaded and created assets away between Play sessions: never trust a stale cache.
            if (textures.TryGetValue(key, out var tex) && tex != null) return tex;
            tex = Resources.Load<Texture2D>("Characters/" + key);
            textures[key] = tex;
            return tex;
        }

        public static Sprite Sprite(string kind, int i)
        {
            if (i < 0) return null;
            string key = Key(kind, i);
            if (sprites.TryGetValue(key, out var s) && s != null) return s;
            var tex = Texture(kind, i);
            s = tex != null ? UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), PixelsPerUnit) : null;
            sprites[key] = s;
            return s;
        }

        /// <summary>How many of a kind of part exist (body_00, body_01, ...).</summary>
        public static int Count(string kind)
        {
            if (counts.TryGetValue(kind, out int n) && n > 0) return n;
            n = 0;
            while (Texture(kind, n) != null) n++;
            counts[kind] = n;
            return n;
        }

        public static bool Available => Count("body") > 0 && Count("head") > 0 && Count("hair") > 0;

        public static Texture2D Face(int i) => Texture("face", i);
    }
}
