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

        OSistema.Core.Game game;

        public void Init(CombatSystem system, OSistema.Core.Game g = null)
        {
            game = g;
            combat = system;
            combat.Shot += OnShot;
            combat.PlayerHit += OnPlayerHit;
            combat.Damaged += OnDamaged;
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
                combat.Damaged -= OnDamaged;
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
            Spawn(shot.From + dir * 0.45f, Vector2.zero, FlashSeconds, new Color(1f, 0.85f, 0.4f, 1f), 0.6f, Random.value * 90f, false);
            Spawn(shot.From + dir * 0.75f, Vector2.zero, FlashSeconds * 0.8f, new Color(1f, 0.95f, 0.7f, 1f), 0.3f, 45f, false);
            MuzzleLight(shot.From + dir * 0.4f);
            Tracer(shot.From + dir * 0.4f, shot.To);
            // A brass casing flicks out to the right of the gun.
            var right = new Vector2(dir.y, -dir.x);
            Spawn(shot.From + dir * 0.2f, right * (2.2f + Random.value) + Random.insideUnitCircle * 0.6f, 0.45f, new Color(0.9f, 0.72f, 0.3f, 1f), 0.07f, Random.value * 90f, true);

            if (shot.Hit)
            {
                // Blood sprays out the far side, the way the bullet was going.
                for (int i = 0; i < 9; i++)
                    Spawn(shot.To, dir * (1.5f + Random.value * 3f) + Random.insideUnitCircle * 1.2f, 0.25f + Random.value * 0.3f, BloodColor, 0.07f + Random.value * 0.09f, 0f, true);
                Decal(shot.To + dir * (0.3f + Random.value * 0.4f), 0.55f + Random.value * 0.3f);
                SoundManager.PlayAt(AudioSynth.HitFlesh, shot.To, 0.55f);
            }
            else
            {
                Burst(shot.To, DustColor, shot.InCover ? 9 : 5, 2.2f, 0.3f);
                // Sparks off stone.
                for (int i = 0; i < 3; i++)
                    Spawn(shot.To, -dir * (1f + Random.value * 2f) + Random.insideUnitCircle * 1.5f, 0.15f, new Color(1f, 0.85f, 0.5f, 1f), 0.04f, 0f, true);
            }

            // A miss that passes close to the player: the crack of it going by your ear.
            var me = game?.Player;
            if (me != null && !shot.Hit && (shot.From - me.Position).sqrMagnitude > 1f)
            {
                var seg = shot.To - shot.From;
                float t = Mathf.Clamp01(Vector2.Dot(me.Position - shot.From, seg) / Mathf.Max(0.0001f, seg.sqrMagnitude));
                if ((shot.From + seg * t - me.Position).sqrMagnitude < 1.4f * 1.4f)
                {
                    SoundManager.PlayUi(AudioSynth.Whiz, 0.5f);
                    if (shake != null) shake.Kick(me.Position, 0.12f);
                }
            }
        }

        /// <summary>
        /// The numbers and the crunch: your hits float their damage (blue on armour, white on flesh,
        /// yellow to the head); armour that gives way shatters with a crunch; a man you drop gets the
        /// kill-confirm. Your own armour breaking crunches too.
        /// </summary>
        void OnDamaged(CombatSystem.Damage d)
        {
            var at = d.Target.Position;
            bool mine = d.Attacker != null && d.Attacker.Controlled;
            if (d.ArmorBroke)
            {
                for (int i = 0; i < 10; i++)
                    Spawn(at, Random.insideUnitCircle * 3.5f, 0.4f + Random.value * 0.2f, new Color(0.55f, 0.85f, 1f, 1f), 0.06f + Random.value * 0.05f, Random.value * 90f, true);
                if (mine || d.Target.Controlled) SoundManager.PlayUi(AudioSynth.ArmorBreak, 0.55f);
                else SoundManager.PlayAt(AudioSynth.ArmorBreak, at, 0.4f);
            }
            if (mine && game != null)
            {
                if (d.Down) SoundManager.PlayUi(AudioSynth.KillConfirm, 0.6f);
            }
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
            if (shake != null) shake.Kick(at, downed ? 0.3f : 0.08f);
            if (downed && game != null) game.PopupAt(at + new Vector2(0f, 0.6f), "DOWN");
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

        // Blood on the basolato: irregular splashes, not circles. It darkens and dries over a day,
        // fades over the next two, and the rain washes it off within the hour.
        readonly Dictionary<SpriteRenderer, (long born, float alpha)> stains = new Dictionary<SpriteRenderer, (long, float)>();

        void Decal(Vector2 at, float size, bool pool = false)
        {
            SpriteRenderer sr;
            if (decals.Count >= MaxDecals) sr = decals.Dequeue();
            else
            {
                var go = new GameObject("Blood");
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = SpriteFactory.LitMaterial;
                sr.sortingOrder = 2;
            }
            sr.gameObject.SetActive(true);
            sr.sprite = pool ? BloodSprites.Pool : BloodSprites.Splash;
            sr.transform.position = at;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Random.value * 360f);
            sr.transform.localScale = new Vector3(size, size * (0.75f + Random.value * 0.35f), 1f);
            float alpha = pool ? 0.8f : 0.7f;
            sr.color = new Color(0.45f, 0.03f, 0.03f, alpha);
            stains[sr] = (game != null ? game.Ticks.TicksGame : 0, alpha);
            decals.Enqueue(sr);
        }

        /// <summary>A pool of blood under someone who's down.</summary>
        public void Pool(Vector2 at) => Decal(at, 0.75f, pool: true);

        float stainTimer;

        void AgeStains()
        {
            if (game == null) return;
            stainTimer += Time.unscaledDeltaTime;
            if (stainTimer < 0.5f) return;
            stainTimer = 0f;
            long now = game.Ticks.TicksGame;
            float rain = game.Weather.Rain;
            float day = OSistema.Core.GameClock.TicksPerDay;
            var gone = new List<SpriteRenderer>();
            foreach (var kv in stains)
            {
                var sr = kv.Key;
                if (sr == null) { gone.Add(sr); continue; }
                float age = (now - kv.Value.born) / day;                         // in days
                float fade = age < 1f ? 1f - age * 0.35f : Mathf.Max(0f, 0.65f - (age - 1f) * 0.33f);
                // Rain washes it away: each tick of rain takes some of it.
                float washed = rain > 0.15f ? Mathf.Clamp01(1f - rain * 0.02f) : 1f;
                var c = sr.color;
                // Dried blood goes brown.
                float dry = Mathf.Clamp01(age);
                c.r = Mathf.Lerp(0.45f, 0.28f, dry);
                c.g = Mathf.Lerp(0.03f, 0.06f, dry);
                c.b = Mathf.Lerp(0.03f, 0.04f, dry);
                c.a = Mathf.Min(c.a * washed, kv.Value.alpha * fade);
                sr.color = c;
                if (c.a < 0.03f) { sr.gameObject.SetActive(false); gone.Add(sr); }
            }
            foreach (var sr in gone) stains.Remove(sr);
        }

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
            AgeStains();
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
