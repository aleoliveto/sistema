using System.Collections.Generic;
using OSistema.Core;
using UnityEngine;

namespace OSistema.Views
{
    /// <summary>
    /// Where the sun is: which way shadows fall and how long they are, per cell of height: short
    /// at noon, long at dawn and dusk, gone at night and faint under rain. Set each frame by <see cref="WorldLighting"/>.
    /// </summary>
    public static class Sun
    {
        /// <summary>Shadow offset, in cells, for each cell of height.</summary>
        public static Vector2 Offset = new Vector2(0.3f, 0.25f);
        /// <summary>0 at night, 1 in clear sun.</summary>
        public static float Strength = 1f;

        public static void Update(float hour, float daylight, float rain)
        {
            float t = Mathf.Clamp01((hour - 6f) / 14f);   // 06:00 → 20:00
            float arc = Mathf.Sin(t * Mathf.PI);           // 0 at the horizon, 1 at noon
            // The buildings' shadows are painted into the map falling north-east, so the sun keeps
            // that bearing for now; only the length changes. (A live ground shadow can swing it.)
            var dir = new Vector2(0.83f, 0.55f);
            float length = 0.35f + 0.9f * (1f - arc);      // long at dawn and dusk
            Offset = dir * length;
            Strength = Mathf.Clamp01((daylight - 0.25f) * 2f) * (1f - rain * 0.75f);
        }
    }

    /// <summary>
    /// A shadow for a sprite that stands above the ground: the same shape, dark and soft, thrown
    /// along <see cref="Sun.Offset"/> by its height. Follows the sprite as it moves and sways.
    /// </summary>
    public sealed class HeightShadow : MonoBehaviour
    {
        SpriteRenderer source, shadow;
        float height, alpha;

        public static HeightShadow Add(SpriteRenderer source, float height, float alpha = 0.32f, int order = 4)
        {
            var go = new GameObject("Shadow");
            go.transform.SetParent(source.transform.parent, false);
            var s = go.AddComponent<HeightShadow>();
            s.source = source;
            s.height = height;
            s.alpha = alpha;
            s.shadow = go.AddComponent<SpriteRenderer>();
            s.shadow.sprite = source.sprite;
            s.shadow.sharedMaterial = SpriteFactory.UnlitMaterial;
            s.shadow.sortingOrder = order;
            s.LateUpdate();
            return s;
        }

        void LateUpdate()
        {
            if (source == null) { Destroy(gameObject); return; }
            var t = source.transform;
            bool on = source.enabled && source.gameObject.activeInHierarchy && Sun.Strength > 0.01f;
            shadow.enabled = on;
            if (!on) return;
            shadow.sprite = source.sprite;
            transform.position = t.position + (Vector3)(Sun.Offset * height);
            transform.rotation = t.rotation;
            transform.localScale = t.localScale;
            // The washing's own fade doesn't lighten its shadow: the cloth is still there.
            shadow.color = new Color(0.02f, 0.02f, 0.05f, alpha * Sun.Strength);
        }
    }

    /// <summary>
    /// Things above people's heads (washing, wall lamps, balconies, awnings): drawn over everyone,
    /// and see-through while somebody is underneath, so you never lose sight of who's there.
    /// </summary>
    public sealed class Overhead : MonoBehaviour
    {
        sealed class Entry
        {
            public Rect Area;
            public SpriteRenderer[] Parts;
            public float[] BaseAlpha;
            public float Alpha = 1f;
        }

        public const int Order = 26;   // above people (10-15) and the aim line's floor, below fog and rain
        const float Seen = 0.28f;

        static Overhead instance;
        readonly List<Entry> entries = new List<Entry>();
        readonly List<Vector2> under = new List<Vector2>();
        Game game;
        Camera cam;

        public static void Init(Game g, Transform parent)
        {
            if (instance != null) Destroy(instance.gameObject);
            var go = new GameObject("Overhead");
            go.transform.SetParent(parent, false);
            instance = go.AddComponent<Overhead>();
            instance.game = g;
        }

        /// <summary>Registers sprites covering <paramref name="area"/> (world cells) as overhead.</summary>
        public static void Register(Rect area, params SpriteRenderer[] parts)
        {
            if (instance == null) return;
            var e = new Entry { Area = area, Parts = parts, BaseAlpha = new float[parts.Length] };
            for (int i = 0; i < parts.Length; i++) e.BaseAlpha[i] = parts[i].color.a;
            instance.entries.Add(e);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void LateUpdate()
        {
            if (game == null) return;
            if (cam == null) cam = Camera.main;
            under.Clear();
            foreach (var p in game.Pawns.All)
                if (p.IsPresent) under.Add(p.Position);
            foreach (var n in game.Npcs.All)
                if (!n.Despawned) under.Add(n.Mover.Position);

            var view = ViewRect();
            float dt = Time.unscaledDeltaTime;
            foreach (var e in entries)
            {
                if (!e.Area.Overlaps(view)) continue;
                var near = new Rect(e.Area.x - 0.45f, e.Area.y - 0.45f, e.Area.width + 0.9f, e.Area.height + 0.9f);
                bool someone = false;
                foreach (var u in under)
                    if (near.Contains(u)) { someone = true; break; }
                float target = someone ? Seen : 1f;
                if (Mathf.Approximately(e.Alpha, target)) continue;
                e.Alpha = Mathf.MoveTowards(e.Alpha, target, dt * 3f);
                for (int i = 0; i < e.Parts.Length; i++)
                {
                    if (e.Parts[i] == null) continue;
                    var c = e.Parts[i].color;
                    c.a = e.BaseAlpha[i] * e.Alpha;
                    e.Parts[i].color = c;
                }
            }
        }

        Rect ViewRect()
        {
            if (cam == null) return new Rect(-1e5f, -1e5f, 2e5f, 2e5f);
            float h = cam.orthographicSize + 2f, w = h * cam.aspect + 2f;
            var p = cam.transform.position;
            return new Rect(p.x - w, p.y - h, w * 2f, h * 2f);
        }
    }
}
