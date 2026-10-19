using UnityEngine;

namespace OSistema.Views
{
    /// <summary>
    /// Blood, drawn once at startup: a splash (a blot with flung droplets and streaks) and a pool
    /// (one irregular puddle). White, so the stain's colour is set by tinting.
    /// </summary>
    public static class BloodSprites
    {
        static Sprite splash, pool;

        public static Sprite Splash => splash != null ? splash : splash = Make(11, false);
        public static Sprite Pool => pool != null ? pool : pool = Make(23, true);

        static Sprite Make(int seed, bool isPool)
        {
            const int S = 96;
            var rng = new System.Random(seed);
            var px = new Color32[S * S];
            var a = new float[S * S];
            void Blob(float cx, float cy, float r, float strength)
            {
                for (int y = (int)(cy - r - 2); y <= cy + r + 2; y++)
                for (int x = (int)(cx - r - 2); x <= cx + r + 2; x++)
                {
                    if (x < 0 || y < 0 || x >= S || y >= S) continue;
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float wobble = Mathf.PerlinNoise(x * 0.18f + seed, y * 0.18f) * 0.45f * r;
                    float v = Mathf.Clamp01((r + wobble - d) / 1.6f) * strength;
                    a[y * S + x] = Mathf.Max(a[y * S + x], v);
                }
            }
            float c = S / 2f;
            if (isPool)
            {
                // A puddle: several overlapping lobes.
                for (int i = 0; i < 7; i++)
                    Blob(c + (float)(rng.NextDouble() - 0.5) * 26f, c + (float)(rng.NextDouble() - 0.5) * 18f, 10f + (float)rng.NextDouble() * 9f, 1f);
            }
            else
            {
                Blob(c, c, 9f + (float)rng.NextDouble() * 4f, 1f);
                // Droplets flung one way, getting smaller, and a few streaks.
                float dir = (float)rng.NextDouble() * Mathf.PI * 2f;
                for (int i = 0; i < 18; i++)
                {
                    float spread = dir + (float)(rng.NextDouble() - 0.5) * 1.4f;
                    float dist = 10f + (float)rng.NextDouble() * 32f;
                    float r = Mathf.Lerp(3.5f, 0.8f, dist / 42f) * (0.6f + (float)rng.NextDouble() * 0.6f);
                    Blob(c + Mathf.Cos(spread) * dist, c + Mathf.Sin(spread) * dist, r, 0.95f);
                }
                for (int k = 0; k < 4; k++)
                {
                    float spread = dir + (float)(rng.NextDouble() - 0.5) * 0.9f;
                    for (float t = 6f; t < 24f; t += 1.2f) Blob(c + Mathf.Cos(spread) * t, c + Mathf.Sin(spread) * t, Mathf.Lerp(2.2f, 0.6f, t / 24f), 0.85f);
                }
            }
            for (int i = 0; i < px.Length; i++)
            {
                // Darker at the edge where it dries first.
                byte v = (byte)(255 * Mathf.Lerp(0.75f, 1f, a[i]));
                px[i] = new Color32(v, v, v, (byte)(a[i] * 255));
            }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        }
    }
}
