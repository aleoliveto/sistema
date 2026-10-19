using System.Collections.Generic;
using OSistema.World;
using UnityEngine;

namespace OSistema.Views
{
    /// <summary>
    /// The faces of the palazzi: a window with persiane on many cells of each south face, and on
    /// most of those an iron balcony sticking out over the lane: above people's heads, so it goes
    /// see-through when someone walks under it, and it throws a shadow on the street.
    /// Rebuilt cell by cell as walls go up or come down.
    /// </summary>
    public sealed class FacadeView : MonoBehaviour
    {
        const float FaceTop = TilePainter.FaceHeight / (float)TilePainter.Hi;   // the painted south face (TilePainter)

        Map map;
        readonly Dictionary<int, List<GameObject>> built = new Dictionary<int, List<GameObject>>();
        readonly HashSet<int> pending = new HashSet<int>();

        public void Init(Map m)
        {
            map = m;
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                Build(x, y);
            map.CellChanged += OnChanged;
        }

        void OnDestroy()
        {
            if (map != null) map.CellChanged -= OnChanged;
        }

        // A wall's face shows only with no wall south of it: a change can uncover the face above.
        void OnChanged(int x, int y)
        {
            pending.Add(map.Index(x, y));
            if (map.InBounds(x, y + 1)) pending.Add(map.Index(x, y + 1));
        }

        void LateUpdate()
        {
            if (map == null || pending.Count == 0) return;
            foreach (int i in pending) Build(i % map.Width, i / map.Width);
            pending.Clear();
        }

        bool Wallish(int x, int y)
        {
            if (!map.InBounds(x, y)) return false;
            var b = map.GetBuilding(x, y);
            return b == BuildingDefs.Wall || b == BuildingDefs.Door;
        }

        void Build(int x, int y)
        {
            int i = map.Index(x, y);
            if (built.TryGetValue(i, out var old))
            {
                foreach (var go in old) Destroy(go);
                built.Remove(i);
            }
            if (map.GetBuilding(x, y) != BuildingDefs.Wall || Wallish(x, y - 1)) return;
            // Only faces that look onto the street: a wall seen from inside a room has no window.
            if (!map.InBounds(x, y - 1)) return;
            var below = map.GetTerrain(x, y - 1);
            if (below == TerrainType.Floor || below == TerrainType.Marble) return;
            if (TilePainter.Noise(x, y, 19) >= 0.5f) return;

            var list = new List<GameObject>();
            bool green = TilePainter.Noise(x, y, 97) < 0.6f, open = TilePainter.Noise(x, y, 98) < 0.6f;
            var window = Sprite(list, $"Facade/facade_window_{(green ? "green" : "brown")}_{(open ? "open" : "shut")}",
                new Vector2(x + 0.5f, y + FaceTop * 0.62f), 1);
            if (window == null) return;

            // A balcony over the lane, if there's lane under it.
            if (TilePainter.Noise(x, y, 24) < 0.55f && map.InBounds(x, y - 1) && map.IsPassable(new Vector2Int(x, y - 1)))
            {
                float r = TilePainter.Noise(x, y, 25);
                string art = r < 0.4f ? "balcony_pots" : r < 0.75f ? "balcony_pots2" : "balcony_chair";
                var balcony = Sprite(list, "Facade/facade_" + art, new Vector2(x + 0.5f, y - 0.16f), Overhead.Order - 1);
                if (balcony != null)
                {
                    list.Add(HeightShadow.Add(balcony, 1.4f, 0.32f).gameObject);
                    Overhead.Register(new Rect(x, y - 0.4f, 1f, 0.45f), balcony);
                }
            }
            built[i] = list;
        }

        SpriteRenderer Sprite(List<GameObject> list, string path, Vector2 at, int order)
        {
            var sprite = PropArt.Load(path);
            if (sprite == null) return null;
            var go = new GameObject(path);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            float size = Mathf.Max(sprite.texture.width, sprite.texture.height) / 128f;   // drawn at 128 px a cell
            go.transform.localScale = new Vector3(size, size, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = SpriteFactory.LitMaterial;
            sr.sortingOrder = order;
            list.Add(go);
            return sr;
        }
    }
}
