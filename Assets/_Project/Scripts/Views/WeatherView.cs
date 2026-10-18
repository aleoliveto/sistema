using OSistema.Core;
using UnityEngine;

namespace OSistema.Views
{
    /// <summary>
    /// Rain over whatever the camera sees: slanting streaks, little splashes on the stones, and the
    /// hiss of it on the shutters. How much depends on <see cref="Weather.Rain"/>.
    /// </summary>
    public sealed class WeatherView : MonoBehaviour
    {
        const int Drops = 260, Splashes = 50;
        static readonly Vector2 Fall = new Vector2(-0.22f, -1f);

        Game game;
        Camera cam;
        readonly SpriteRenderer[] drops = new SpriteRenderer[Drops];
        readonly Vector2[] dropPos = new Vector2[Drops];
        readonly float[] dropSpeed = new float[Drops];
        readonly SpriteRenderer[] splashes = new SpriteRenderer[Splashes];
        readonly float[] splashAge = new float[Splashes];
        AudioSource hiss;
        System.Random rng = new System.Random(5);

        public void Init(Game g)
        {
            game = g;
            cam = Camera.main;
            var angle = Mathf.Atan2(Fall.y, Fall.x) * Mathf.Rad2Deg + 90f;
            for (int i = 0; i < Drops; i++)
            {
                var sr = new GameObject("Drop").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sprite = SpriteFactory.Square;
                sr.sharedMaterial = SpriteFactory.UnlitMaterial;
                sr.sortingOrder = 45;
                sr.color = new Color(0.78f, 0.84f, 0.95f, 0.28f);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                sr.transform.localScale = new Vector3(0.025f, 0.45f + (float)rng.NextDouble() * 0.3f, 1f);
                sr.enabled = false;
                drops[i] = sr;
                dropSpeed[i] = 20f + (float)rng.NextDouble() * 8f;
                dropPos[i] = new Vector2(float.MaxValue, 0f);
            }
            for (int i = 0; i < Splashes; i++)
            {
                var sr = new GameObject("Splash").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sprite = SpriteFactory.Ring;
                sr.sharedMaterial = SpriteFactory.UnlitMaterial;
                sr.sortingOrder = 4;
                sr.enabled = false;
                splashes[i] = sr;
                splashAge[i] = 1f;
            }
            hiss = gameObject.AddComponent<AudioSource>();
            hiss.clip = AudioSynth.Rain;
            hiss.loop = true;
            hiss.volume = 0f;
            hiss.spatialBlend = 0f;
        }

        void LateUpdate()
        {
            if (game == null) return;
            if (cam == null) cam = Camera.main;
            float rain = game.Weather.Rain;
            int active = Mathf.RoundToInt(Drops * Mathf.Clamp01(rain));
            float dt = Time.deltaTime;
            float h = cam.orthographicSize, w = h * cam.aspect;
            var c = (Vector2)cam.transform.position;
            var min = c - new Vector2(w + 2f, h + 2f);
            var max = c + new Vector2(w + 2f, h + 3f);

            for (int i = 0; i < Drops; i++)
            {
                var sr = drops[i];
                if (i >= active)
                {
                    if (sr.enabled) sr.enabled = false;
                    continue;
                }
                var p = dropPos[i];
                // Out of view (or new): start again somewhere along the top or the right edge.
                if (p.x < min.x || p.x > max.x || p.y < min.y || p.y > max.y)
                    p = new Vector2(Mathf.Lerp(min.x, max.x + 3f, (float)rng.NextDouble()), Mathf.Lerp(min.y, max.y, (float)rng.NextDouble()));
                p += Fall * dropSpeed[i] * dt;
                if (p.y < min.y)
                {
                    Splash(p + new Vector2(0f, h * (float)rng.NextDouble()));
                    p.y = max.y;
                }
                dropPos[i] = p;
                sr.transform.position = new Vector3(p.x, p.y, 0f);
                if (!sr.enabled) sr.enabled = true;
            }

            // Splashes: a quick ring that grows and fades. More of them the harder it rains.
            if (rain > 0.1f && rng.NextDouble() < rain * 0.9f)
                Splash(new Vector2(Mathf.Lerp(c.x - w, c.x + w, (float)rng.NextDouble()), Mathf.Lerp(c.y - h, c.y + h, (float)rng.NextDouble())));
            for (int i = 0; i < Splashes; i++)
            {
                if (splashAge[i] >= 1f) continue;
                splashAge[i] += dt * 3.5f;
                var sr = splashes[i];
                float a = splashAge[i];
                sr.transform.localScale = Vector3.one * (0.08f + a * 0.22f);
                sr.color = new Color(0.8f, 0.86f, 0.95f, (1f - a) * 0.35f);
                if (a >= 1f) sr.enabled = false;
            }

            if (!hiss.isPlaying && rain > 0.01f) hiss.Play();
            hiss.volume = Mathf.Lerp(hiss.volume, rain * 0.45f, dt * 2f);
            if (hiss.isPlaying && rain <= 0.01f && hiss.volume < 0.01f) hiss.Stop();
        }

        int nextSplash;

        void Splash(Vector2 at)
        {
            int i = nextSplash++ % Splashes;
            splashAge[i] = 0f;
            splashes[i].transform.position = new Vector3(at.x, at.y, 0f);
            splashes[i].enabled = true;
        }
    }
}
