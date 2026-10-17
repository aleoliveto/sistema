using UnityEngine;

namespace OSistema.Views
{
    /// <summary>
    /// Sound effects synthesised at startup, so the prototype has a voice before real audio exists.
    /// Crude but effective: noise bursts, thumps, a bell, a city hum.
    /// </summary>
    public static class AudioSynth
    {
        const int Rate = 44100;

        static AudioClip gunshot, gunshotFar, punch, ambience, bell, scooter, click, siren, horn;

        public static AudioClip Gunshot => gunshot != null ? gunshot : gunshot = MakeGunshot("gunshot", 0.5f, 0f);
        public static AudioClip GunshotFar => gunshotFar != null ? gunshotFar : gunshotFar = MakeGunshot("gunshot_far", 0.7f, 0.92f);
        public static AudioClip Punch => punch != null ? punch : punch = MakePunch();
        public static AudioClip Ambience => ambience != null ? ambience : ambience = MakeAmbience();
        public static AudioClip Bell => bell != null ? bell : bell = MakeBell();
        public static AudioClip Scooter => scooter != null ? scooter : scooter = MakeScooter();
        public static AudioClip Click => click != null ? click : click = MakeClick();
        public static AudioClip Siren => siren != null ? siren : siren = MakeSiren();
        public static AudioClip Horn => horn != null ? horn : horn = MakeHorn();
        public static AudioClip Ring => ring != null ? ring : ring = MakeRing();

        static AudioClip ring;

        // A mobile's double chirp.
        static AudioClip MakeRing()
        {
            int n = (int)(Rate * 0.9f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                bool on = t < 0.3f || (t > 0.45f && t < 0.75f);
                if (!on) continue;
                float f = (Mathf.Sin(2f * Mathf.PI * 22f * t) > 0f) ? 1320f : 990f;
                float env = Mathf.Min(1f, (t % 0.45f) * 40f);
                d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * 0.35f * env;
            }
            return Clip("ring", d);
        }

        // A scooter's thin, impatient beep-beep.
        static AudioClip MakeHorn()
        {
            int n = (int)(Rate * 0.45f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                bool on = t < 0.16f || (t > 0.22f && t < 0.4f);
                if (!on) continue;
                float a = Mathf.Sin(2f * Mathf.PI * 420f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 530f * t);
                d[i] = Mathf.Clamp(a * 1.6f, -1f, 1f) * 0.6f;
            }
            return Clip("horn", d);
        }

        // The Italian two-tone siren: "bitonale".
        static AudioClip MakeSiren()
        {
            int n = (int)(Rate * 1.6f);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float freq = (t % 0.8f) < 0.4f ? 660f : 880f;
                phase += freq / Rate;
                float square = (phase % 1f) < 0.5f ? 1f : -1f;
                d[i] = square * 0.4f + Mathf.Sin(2f * Mathf.PI * phase) * 0.6f;
            }
            return Clip("siren", d);
        }

        static AudioClip Clip(string name, float[] data)
        {
            float peak = 0.0001f;
            foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < data.Length; i++) data[i] = data[i] / peak * 0.9f;
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Noise(System.Random r) => (float)r.NextDouble() * 2f - 1f;

        /// <param name="muffle">0 = close and sharp; towards 1 = heavily low-passed (far away, behind walls).</param>
        static AudioClip MakeGunshot(string name, float seconds, float muffle)
        {
            var r = new System.Random(7);
            int n = (int)(Rate * seconds);
            var d = new float[n];
            float lp = 0f, tail = 0f;
            float a = Mathf.Lerp(0.6f, 0.05f, muffle);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float crack = Noise(r) * Mathf.Exp(-t * 55f);
                float body = Noise(r) * Mathf.Exp(-t * 12f) * 0.6f;
                float thump = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 22f) * 0.9f;
                tail += (Noise(r) - tail) * 0.02f; // echo off the buildings
                float s = crack + body + thump + tail * Mathf.Exp(-t * 4f) * 2.5f;
                lp += (s - lp) * a;
                d[i] = lp;
            }
            return Clip(name, d);
        }

        static AudioClip MakePunch()
        {
            var r = new System.Random(3);
            int n = (int)(Rate * 0.14f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float s = Noise(r) * Mathf.Exp(-t * 60f) + Mathf.Sin(2f * Mathf.PI * 110f * t) * Mathf.Exp(-t * 35f);
                lp += (s - lp) * 0.15f;
                d[i] = lp;
            }
            return Clip("punch", d);
        }

        // A low urban hum with slow swells: distant traffic, voices, life. Loops seamlessly.
        static AudioClip MakeAmbience()
        {
            var r = new System.Random(11);
            int n = Rate * 8;
            var d = new float[n];
            float brown = 0f, murmur = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                brown = (brown + Noise(r) * 0.02f) * 0.998f;
                murmur += (Noise(r) - murmur) * 0.08f;
                float swell = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t / 8f) * Mathf.Sin(2f * Mathf.PI * t * 3f / 8f);
                d[i] = brown * 3f + murmur * 0.25f * swell;
            }
            // Crossfade the ends so the loop has no click.
            int fade = Rate / 2;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                d[i] = d[i] * k + d[n - fade + i] * (1f - k);
            }
            var clipped = new float[n - fade];
            System.Array.Copy(d, 0, clipped, 0, clipped.Length);
            return Clip("ambience", clipped);
        }

        static AudioClip MakeBell()
        {
            int n = Rate * 4;
            var d = new float[n];
            float[] partials = { 1f, 2.0f, 2.76f, 5.4f, 8.9f };
            float[] amps = { 1f, 0.6f, 0.45f, 0.25f, 0.12f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float s = 0f;
                for (int k = 0; k < partials.Length; k++)
                    s += amps[k] * Mathf.Sin(2f * Mathf.PI * 196f * partials[k] * t) * Mathf.Exp(-t * (0.8f + k * 0.5f));
                d[i] = s * Mathf.Min(1f, t * 200f);
            }
            return Clip("bell", d);
        }

        // A two-stroke buzzing past: rising, then falling pitch.
        static AudioClip MakeScooter()
        {
            int n = (int)(Rate * 2.2f);
            var d = new float[n];
            float phase = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float pass = t / 2.2f;
                float freq = Mathf.Lerp(150f, 95f, pass) + 6f * Mathf.Sin(t * 40f);
                phase += freq / Rate;
                float saw = (phase % 1f) * 2f - 1f;
                float env = Mathf.Sin(Mathf.PI * pass);
                lp += (saw - lp) * 0.2f;
                d[i] = lp * env * env;
            }
            return Clip("scooter", d);
        }

        static AudioClip MakeClick()
        {
            int n = (int)(Rate * 0.03f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                d[i] = Mathf.Sin(2f * Mathf.PI * 1600f * t) * Mathf.Exp(-t * 180f);
            }
            return Clip("click", d);
        }
    }
}
