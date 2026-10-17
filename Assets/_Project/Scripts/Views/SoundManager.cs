using System.Collections.Generic;
using OSistema.Core;
using OSistema.Npcs;
using UnityEngine;

namespace OSistema.Views
{
    /// <summary>
    /// Plays sounds relative to the camera: louder and centred when close, panned and muffled when
    /// far. Runs the city ambience, the church bells and passing scooters.
    /// </summary>
    public sealed class SoundManager : MonoBehaviour
    {
        const int PoolSize = 16;
        const float HearingRange = 40f;

        static SoundManager instance;

        readonly List<AudioSource> pool = new List<AudioSource>();
        AudioSource ambience;
        Camera cam;
        Game game;
        int lastBellHour = -1;
        float hornTimer = 5f;
        readonly HashSet<int> heardScooters = new HashSet<int>();
        AudioSource engineSource;

        // The player's own scooter: idles low, revs with speed.
        void UpdateEngine()
        {
            var p = game?.Player;
            bool riding = p != null && p.Riding && game.Ticks.Speed > 0;
            if (riding && !engineSource.isPlaying) engineSource.Play();
            float speed = riding ? p.Velocity.magnitude / 11.7f : 0f;
            engineSource.pitch = Mathf.Lerp(engineSource.pitch, 0.75f + 1.1f * speed, Time.unscaledDeltaTime * 4f);
            engineSource.volume = Mathf.MoveTowards(engineSource.volume, riding ? 0.16f + 0.1f * speed : 0f, Time.unscaledDeltaTime);
            if (!riding && engineSource.volume <= 0.001f && engineSource.isPlaying) engineSource.Stop();
        }

        public static void PlayAt(AudioClip clip, Vector2 position, float volume = 1f)
        {
            if (instance != null) instance.Play(clip, position, volume);
        }

        public static void PlayUi(AudioClip clip, float volume = 0.4f)
        {
            if (instance == null) return;
            var src = instance.Next();
            src.panStereo = 0f;
            src.pitch = 1f;
            src.PlayOneShot(clip, volume);
        }

        public void Init(Game g)
        {
            instance = this;
            game = g;
            cam = Camera.main;
            if (cam != null && cam.GetComponent<AudioListener>() == null && FindAnyObjectByType<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();

            for (int i = 0; i < PoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                pool.Add(src);
            }
            ambience = gameObject.AddComponent<AudioSource>();
            ambience.clip = AudioSynth.Ambience;
            ambience.loop = true;
            ambience.volume = 0.25f;
            ambience.Play();

            engineSource = gameObject.AddComponent<AudioSource>();
            engineSource.clip = AudioSynth.EngineLoop;
            engineSource.loop = true;
            engineSource.volume = 0f;
            engineSource.playOnAwake = false;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        int next;
        AudioSource Next()
        {
            var src = pool[next];
            next = (next + 1) % pool.Count;
            return src;
        }

        void Play(AudioClip clip, Vector2 position, float volume)
        {
            if (cam == null) cam = Camera.main;
            var offset = position - (Vector2)cam.transform.position;
            float dist = offset.magnitude;
            if (dist > HearingRange * 2.5f) return;

            bool far = dist > HearingRange;
            var src = Next();
            src.panStereo = Mathf.Clamp(offset.x / (cam.orthographicSize * cam.aspect + 4f), -0.8f, 0.8f);
            src.pitch = 0.94f + Random.value * 0.12f;
            float attenuation = far ? 0.35f * (1f - (dist - HearingRange) / (HearingRange * 1.5f)) : 1f - dist / HearingRange * 0.6f;
            var actual = clip == AudioSynth.Gunshot && far ? AudioSynth.GunshotFar : clip;
            src.PlayOneShot(actual, Mathf.Clamp01(volume * attenuation));
        }

        void Update()
        {
            if (game == null) return;
            UpdateEngine();

            // The city is livelier by day, and louder where there are people around you.
            float day = game.Clock.Daylight;
            int crowd = 0;
            var view = (Vector2)cam.transform.position;
            float reach = cam.orthographicSize * 1.2f;
            foreach (var npc in game.Npcs.All)
                if ((npc.Mover.Position - view).sqrMagnitude < reach * reach && FogOfWar.CanSee(npc.Mover.Position)) crowd++;
            ambience.volume = Mathf.Lerp(0.1f, 0.26f, day) + Mathf.Min(0.2f, crowd * 0.006f);

            // Now and then a scooter in view leans on its horn.
            hornTimer -= Time.unscaledDeltaTime;
            if (hornTimer <= 0f && !game.Ticks.Paused)
            {
                hornTimer = Random.Range(4f, 12f);
                foreach (var npc in game.Npcs.All)
                {
                    if (npc.Kind != NpcKind.Scooter || !FogOfWar.CanSee(npc.Mover.Position)) continue;
                    if ((npc.Mover.Position - view).sqrMagnitude > reach * reach) continue;
                    Play(AudioSynth.Horn, npc.Mover.Position, 0.22f);
                    break;
                }
            }
            ambience.pitch = game.Ticks.Paused ? 0.9f : 1f;

            // Church bells at noon and at the evening Angelus.
            int hour = game.Clock.Hour;
            if ((hour == 12 || hour == 19) && hour != lastBellHour && !game.Ticks.Paused)
            {
                lastBellHour = hour;
                var church = game.Map.PositionsOf(World.BuildingDefs.Altar);
                Vector2 at = cam.transform.position;
                foreach (var c in church)
                {
                    at = new Vector2(c.x, c.y);
                    break;
                }
                StartCoroutine(Bells(at));
            }
            else if (hour != 12 && hour != 19) lastBellHour = -1;

            // A scooter buzzing past the camera.
            foreach (var npc in game.Npcs.All)
            {
                if (npc.Kind != NpcKind.Scooter || heardScooters.Contains(npc.Id)) continue;
                // Only scooters your crew can actually see, close to where you're looking.
                if (!FogOfWar.CanSee(npc.Mover.Position)) continue;
                if ((npc.Mover.Position - (Vector2)cam.transform.position).magnitude > cam.orthographicSize * 0.9f) continue;
                heardScooters.Add(npc.Id);
                Play(AudioSynth.Scooter, npc.Mover.Position, 0.1f);
            }
            if (heardScooters.Count > 200) heardScooters.Clear();
        }

        System.Collections.IEnumerator Bells(Vector2 at)
        {
            for (int i = 0; i < 4; i++)
            {
                Play(AudioSynth.Bell, at, 0.5f);
                yield return new WaitForSecondsRealtime(1.6f);
            }
        }
    }
}
