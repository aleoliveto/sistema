using System.Collections.Generic;
using OSistema.World;
using UnityEngine;

namespace OSistema.Views
{
    /// <summary>
    /// Furniture, fittings, street props and parked vehicles that have painted art, drawn as sprites
    /// over the map at full detail. Kept in step with the map as things are placed, moved or removed.
    /// </summary>
    public sealed class PropLayer : MonoBehaviour
    {
        Map map;
        readonly Dictionary<int, SpriteRenderer> props = new Dictionary<int, SpriteRenderer>();
        readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();
        readonly HashSet<int> pending = new HashSet<int>();

        public void Init(Map m)
        {
            map = m;
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                Refresh(x, y);
            map.CellChanged += OnChanged;
        }

        void OnDestroy()
        {
            if (map != null) map.CellChanged -= OnChanged;
        }

        void OnChanged(int x, int y) => pending.Add(map.Index(x, y));

        void LateUpdate()
        {
            if (map == null || pending.Count == 0) return;
            foreach (int i in pending) Refresh(i % map.Width, i / map.Width);
            pending.Clear();
        }

        void Refresh(int x, int y)
        {
            int i = map.Index(x, y);
            var def = map.GetBuilding(x, y);
            var sprite = def != null ? PropArt.For(def, x, y) : null;
            props.TryGetValue(i, out var sr);
            if (sprite == null)
            {
                if (sr != null)
                {
                    sr.gameObject.SetActive(false);
                    spare.Push(sr);
                    props.Remove(i);
                }
                return;
            }
            if (sr == null)
            {
                sr = spare.Count > 0 ? spare.Pop() : Create();
                sr.gameObject.SetActive(true);
                props[i] = sr;
            }
            sr.sprite = sprite;
            bool vehicle = def == BuildingDefs.ParkedCar || def == BuildingDefs.ParkedScooter;
            sr.sortingOrder = vehicle ? 3 : def == BuildingDefs.Rug ? 1 : 2;
            var t = sr.transform;
            if (Footprint.IsBig(def))
            {
                // Fill the cells it covers, keeping the painting's proportions within reason.
                int rot = map.GetRotation(x, y);
                t.localScale = PropArt.FitScale(sprite, def.Size);
                var c = Footprint.Center(def, new Vector2Int(x, y), rot);
                t.localPosition = new Vector3(c.x, c.y, 0f);
                t.localRotation = Quaternion.Euler(0f, 0f, rot * 90f);
            }
            else
            {
                float size = PropArt.SizeOf(def);
                t.localScale = new Vector3(size, size, 1f);
                t.localPosition = new Vector3(x + 0.5f, y + 0.5f, 0f);
                t.localRotation = Quaternion.Euler(0f, 0f, Angle(def, x, y));
            }
            var shadow = t.GetChild(0).GetComponent<SpriteRenderer>();
            shadow.enabled = def != BuildingDefs.Rug;
            shadow.transform.localScale = vehicle ? new Vector3(0.62f, 0.95f, 1f) : new Vector3(0.95f, 0.95f, 1f);
        }

        float Angle(BuildingDef def, int x, int y)
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            if (def == BuildingDefs.ParkedCar)
            {
                // Along the kerb: nose up or down a vertical street, left or right along a horizontal one.
                bool vertical = IsSidewalk(x - 1, y) || IsSidewalk(x + 1, y);
                return (vertical ? 0f : 90f) + ((h & 1) == 0 ? 0f : 180f);
            }
            if (def == BuildingDefs.ParkedScooter)
                return (h % 4) * 90f + ((h >> 3) % 31) - 15f;   // parked any old how
            if (def.Generated) return ((h >> 2) % 4) * 90f * (def == BuildingDefs.Fountain ? 1f : 0f);
            return map.GetRotation(x, y) * 90f;
        }

        bool IsSidewalk(int x, int y) => map.InBounds(x, y) && map.GetTerrain(x, y) == TerrainType.Sidewalk;

        SpriteRenderer Create()
        {
            var go = new GameObject("Prop");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteFactory.LitMaterial;
            var sh = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            sh.transform.SetParent(go.transform, false);
            sh.transform.localPosition = new Vector3(0.05f, -0.06f, 0f);
            sh.sprite = SpriteFactory.SoftShadow;
            sh.sharedMaterial = SpriteFactory.LitMaterial;
            sh.color = new Color(0f, 0f, 0f, 0.38f);
            sh.sortingOrder = 1;
            return sr;
        }

        public static bool Hides(BuildingDef def) => PropArt.Has(def);
    }
}
