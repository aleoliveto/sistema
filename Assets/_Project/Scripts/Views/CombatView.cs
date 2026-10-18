using System.Collections.Generic;
using OSistema.Combat;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OSistema.Views
{
    /// <summary>
    /// Everything you see and hear when a gun goes off: muzzle flash (which lights up the street),
    /// tracer, blood or dust at the impact, blood stains that stay on the stones, screen shake, sound.
    /// </summary>
    public sealed class CombatView : MonoBehaviour
    {
        const float TracerSeconds = 0.09f;
        const float FlashSeconds = 0.06f;
        const int MaxDecals = 300;

        static readonly Color TracerColor = new Color(1f, 0.92f, 0.6f, 1f);
        static readonly Color BloodColor = new Color(0.55f, 0.03f, 0.03f, 1f);
        static readonly Color DustColor = new Color(0.75f, 0.72f, 0.65f, 1f);

        sealed class Fx
        {
            public SpriteRenderer Sprite;
            public Vector2 Velocity;
            public float Born, Life;
            public Color Color;
            public bool Fade;
        }

        readonly List<Fx> active = new List<Fx>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        readonly Queue<SpriteRenderer> decals = new Queue<SpriteRenderer>();
        readonly List<(Light2D light, float until)> flashes = new List<(Light2D, float)>();
        CombatSystem combat;
        CameraShake shake;

        public static CombatView Instance { get; private set; }

        public void Init(CombatSystem system)
        {
            combat = system;
            combat.Shot += OnShot;
            combat.PlayerHit += OnPlayerHit;
            var cam = Camera.main;
            if (cam != null)
            {
                shake = cam.GetComponent<CameraShake>();
                if (shake == null) shake = cam.gameObject.AddComponent<CameraShake>();
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (combat != null)
            {
                combat.Shot -= OnShot;
                combat.PlayerHit -= OnPlayerHit;
            }
            if (Instance == this) Instance = null;
        }

        void OnShot(ShotEvent shot)
        {
            bool seen = FogOfWar.CanSee(shot.From) || FogOfWar.CanSee(shot.To);

            if (shot.Melee)
            {
                SoundManager.PlayAt(AudioSynth.Punch, shot.From, 0.7f);
                if (seen && shot.Hit) Burst(shot.To, BloodColor, 3, 1.5f, 0.25f);
                return;
            }

            // You hear gunfire even when you can't see it.
            SoundManager.PlayAt(AudioSynth.Gunshot, shot.From, 0.9f);
            if (shake != null) shake.Kick(shot.From, 0.25f);
            if (!seen) return;

            var dir = (shot.To - shot.From).normalized;
            Spawn(shot.From + dir * 0.4f, Vector2.zero, FlashSeconds, new Color(1f, 0.85f, 0.4f, 1f), 0.45f, 0f, false);
            MuzzleLight(shot.From + dir * 0.4f);
            Tracer(shot.From + dir * 0.4f, shot.To);

            if (shot.Hit)
            {
                Burst(shot.To, BloodColor, 6, 2.5f, 0.35f);
                Decal(shot.To + Random.insideUnitCircle * 0.25f, 0.35f + Random.value * 0.3f);
            }
            else Burst(shot.To, DustColor, shot.InCover ? 7 : 4, 1.8f, 0.3f);
        }

        /// <summary>Your bullet landed: a white X on them (red if they went down), and a tick.</summary>
        void OnPlayerHit(Vector2 at, bool downed)
        {
            SoundManager.PlayUi(AudioSynth.HitMarker, downed ? 0.6f : 0.4f);
            var c = downed ? new Color(1f, 0.25f, 0.2f, 1f) : new Color(1f, 1f, 1f, 1f);
            for (int k = 0; k < 2; k++)
            {
                var sr = Get();
                sr.transform.position = at + new Vector2(0f, 0.15f);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, k == 0 ? 45f : -45f);
                sr.transform.localScale = new Vector3(0.55f, 0.06f, 1f);
                sr.color = c;
                active.Add(new Fx { Sprite = sr, Born = Time.unscaledTime, Life = 0.28f, Color = c, Fade = true });
            }
            if (shake != null) shake.Kick(at, downed ? 0.2f : 0.08f);
        }

        void Tracer(Vector2 from, Vector2 to)
        {
            var sr = Get();
            var delta = to - from;
            sr.transform.position = (Vector3)(from + delta * 0.5f);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(delta.magnitude, 0.07f, 1f);
            sr.color = TracerColor;
            active.Add(new Fx { Sprite = sr, Born = Time.unscaledTime, Life = TracerSeconds, Color = TracerColor, Fade = true });
        }

        void Burst(Vector2 at, Color color, int count, float speed, float life)
        {
            for (int i = 0; i < count; i++)
                Spawn(at, Random.insideUnitCircle * speed, life * (0.6f + Random.value * 0.6f), color, 0.08f + Random.value * 0.08f, 0f, true);
        }

        void Spawn(Vector2 at, Vector2 velocity, float life, Color color, float size, float angle, bool fade)
        {
            var sr = Get();
            sr.transform.position = at;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            sr.transform.localScale = new Vector3(size, size, 1f);
            sr.color = color;
            active.Add(new Fx { Sprite = sr, Velocity = velocity, Born = Time.unscaledTime, Life = life, Color = color, Fade = fade });
        }

        // Blood stays on the basolato. The oldest stains fade away eventually.
        void Decal(Vector2 at, float size)
        {
            SpriteRenderer sr;
            if (decals.Count >= MaxDecals) sr = decals.Dequeue();
            else
            {
                var go = new GameObject("Blood");
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Disc;
                sr.sharedMaterial = SpriteFactory.LitMaterial;
                sr.sortingOrder = 2;
            }
            sr.transform.position = at;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Random.value * 360f);
            sr.transform.localScale = new Vector3(size, size * (0.6f + Random.value * 0.4f), 1f);
            sr.color = new Color(0.4f, 0.02f, 0.02f, 0.85f);
            decals.Enqueue(sr);
        }

        /// <summary>A pool of blood under someone who's down.</summary>
        public void Pool(Vector2 at) => Decal(at, 0.9f);

        void MuzzleLight(Vector2 at)
        {
            var go = new GameObject("Muzzle flash");
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(1f, 0.8f, 0.45f);
            light.intensity = 2.2f;
            light.pointLightOuterRadius = 4.5f;
            light.pointLightInnerRadius = 0.3f;
            flashes.Add((light, Time.unscaledTime + FlashSeconds * 1.5f));
        }

        SpriteRenderer Get()
        {
            if (pool.Count > 0)
            {
                var sr = pool.Pop();
                sr.gameObject.SetActive(true);
                return sr;
            }
            var go = new GameObject("Fx");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = SpriteFactory.CenteredPixel;
            r.sharedMaterial = SpriteFactory.UnlitMaterial;
            r.sortingOrder = 30;
            return r;
        }

        void LateUpdate()
        {
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var fx = active[i];
                float t = (now - fx.Born) / fx.Life;
                if (t >= 1f)
                {
                    fx.Sprite.gameObject.SetActive(false);
                    pool.Push(fx.Sprite);
                    active.RemoveAt(i);
                    continue;
                }
                if (fx.Velocity != Vector2.zero)
                {
                    fx.Sprite.transform.position += (Vector3)(fx.Velocity * dt);
                    fx.Velocity *= 1f - 6f * dt;
                }
                if (fx.Fade)
                {
                    var c = fx.Color;
                    c.a = 1f - t;
                    fx.Sprite.color = c;
                }
            }

            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                if (now < flashes[i].until) continue;
                if (flashes[i].light != null) Destroy(flashes[i].light.gameObject);
                flashes.RemoveAt(i);
            }
        }
    }

    /// <summary>Small screen shake for nearby gunfire. Applied on top of the camera's own movement.</summary>
    public sealed class CameraShake : MonoBehaviour
    {
        float trauma;
        Vector3 applied;

        public void Kick(Vector2 source, float amount)
        {
            var cam = GetComponent<Camera>();
            float dist = ((Vector2)transform.position - source).magnitude;
            float reach = cam != null ? cam.orthographicSize * 1.5f : 15f;
            if (dist > reach) return;
            trauma = Mathf.Min(1f, trauma + amount * (1f - dist / reach));
        }

        void LateUpdate()
        {
            transform.position -= applied;
            applied = Vector3.zero;
            if (trauma <= 0f) return;
            float s = trauma * trauma * 0.25f;
            applied = new Vector3((Random.value - 0.5f) * s, (Random.value - 0.5f) * s, 0f);
            transform.position += applied;
            trauma = Mathf.Max(0f, trauma - Time.unscaledDeltaTime * 2.5f);
        }
    }
}
