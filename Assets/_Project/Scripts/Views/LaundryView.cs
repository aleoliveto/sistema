using System.Collections.Generic;
using OSistema.World;
using UnityEngine;

namespace OSistema.Views
{
    /// <summary>
    /// Washing lines strung across the vicoli, drawn above the street so people walk under them.
    /// The clothes sway a little in the breeze.
    /// </summary>
    public sealed class LaundryView : MonoBehaviour
    {
        static readonly Color[] Clothes =
        {
            new Color(0.95f, 0.95f, 0.92f), new Color(0.85f, 0.3f, 0.3f), new Color(0.3f, 0.45f, 0.75f),
            new Color(0.95f, 0.8f, 0.3f), new Color(0.6f, 0.8f, 0.9f), new Color(0.9f, 0.6f, 0.75f),
            new Color(0.4f, 0.65f, 0.4f),
        };

        readonly List<(Transform t, float phase)> swaying = new List<(Transform, float)>();

        public void Init(Map map)
        {
            var rng = new System.Random(map.Width * 31 + map.Height);
            for (int y = 0; y < map.Height; y++)
            {
                int x = 0;
                while (x < map.Width)
                {
                    if (map.GetDecor(x, y) != MapDecor.Laundry)
                    {
                        x++;
                        continue;
                    }
                    int start = x;
                    while (x < map.Width && map.GetDecor(x, y) == MapDecor.Laundry) x++;
                    Line(start, x, y, rng);
                }
            }
        }

        void Line(int x0, int x1, int y, System.Random rng)
        {
            float yPos = y + 0.6f;
            var line = Part(new Vector2((x0 + x1) / 2f, yPos), new Vector2(x1 - x0 + 0.4f, 0.04f), new Color(0.2f, 0.2f, 0.2f, 0.9f), 24);

            for (float x = x0 + 0.25f; x < x1 - 0.1f; x += 0.35f + (float)rng.NextDouble() * 0.3f)
            {
                var color = Clothes[rng.Next(Clothes.Length)];
                var size = new Vector2(0.18f + (float)rng.NextDouble() * 0.16f, 0.22f + (float)rng.NextDouble() * 0.2f);
                var garment = Part(new Vector2(x, yPos - size.y / 2f), size, color, 25);
                swaying.Add((garment.transform, (float)rng.NextDouble() * 6.28f));
            }
        }

        SpriteRenderer Part(Vector2 at, Vector2 scale, Color color, int order)
        {
            var go = new GameObject("Laundry");
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.CenteredPixel;
            sr.sharedMaterial = SpriteFactory.LitMaterial;
            sr.sortingOrder = order;
            sr.color = color;
            return sr;
        }

        void LateUpdate()
        {
            float t = Time.time;
            foreach (var (tr, phase) in swaying)
                tr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.3f + phase) * 6f);
        }
    }
}
