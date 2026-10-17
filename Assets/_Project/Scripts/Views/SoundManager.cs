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
        readonly HashSet<int> heardScooters = new HashSet<int>();

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

            // The city is livelier by day; zoomed out you hear more of it.
            float day = game.Clock.Daylight;
            ambience.volume = Mathf.Lerp(0.1f, 0.28f, day);
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
                Play(AudioSynth.Scooter, npc.Mover.Position, 0.18f);
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
