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
        /// <summary>How high the lines are strung, in cells: two floors up.</summary>
        const float Height = 2.6f;

        /// <summary>Painted garments in Resources/Laundry (laundry_00 ...), from Tools/ArtGen/laundry.py.</summary>
        const int GarmentCount = 16;

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
            var line = Part(new Vector2((x0 + x1) / 2f, yPos), new Vector2(x1 - x0 + 0.4f, 0.04f), new Color(0.2f, 0.2f, 0.2f, 0.9f), Overhead.Order);
            HeightShadow.Add(line, Height, 0.22f);
            var parts = new List<SpriteRenderer> { line };

            float x = x0 + 0.2f;
            while (x < x1 - 0.15f)
            {
                // A painted garment, pegged at its top edge and swinging from there.
                var art = PropArt.Load($"Laundry/laundry_{rng.Next(GarmentCount):00}");
                if (art == null) break;
                var size = new Vector2(art.texture.width, art.texture.height) / 128f;   // drawn at 128 px a cell
                if (x + size.x > x1 + 0.1f) break;
                var pivot = new GameObject("Peg").transform;
                pivot.SetParent(transform, false);
                pivot.position = new Vector3(x + size.x / 2f, yPos, 0f);
                var go = new GameObject("Garment");
                go.transform.SetParent(pivot, false);
                go.transform.localPosition = new Vector3(0f, -size.y / 2f + 0.03f, 0f);
                float s = Mathf.Max(size.x, size.y);
                go.transform.localScale = new Vector3(s, s, 1f);
                var garment = go.AddComponent<SpriteRenderer>();
                garment.sprite = art;
                garment.sharedMaterial = SpriteFactory.LitMaterial;
                garment.sortingOrder = Overhead.Order + 1;
                swaying.Add((pivot, (float)rng.NextDouble() * 6.28f));
                HeightShadow.Add(garment, Height - 0.3f);
                parts.Add(garment);
                x += size.x + 0.04f + (float)rng.NextDouble() * 0.12f;
            }
            Overhead.Register(new Rect(x0, yPos - 0.6f, x1 - x0, 0.7f), parts.ToArray());
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
