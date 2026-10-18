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

        static AudioClip hitMarker;

        /// <summary>The sharp tick when your shot lands.</summary>
        public static AudioClip HitMarker => hitMarker != null ? hitMarker : hitMarker = MakeHitMarker();

        static AudioClip MakeHitMarker()
        {
            int n = Rate / 12;
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                d[i] = (Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 3600f * t) * 0.4f) * Mathf.Exp(-t * 60f);
            }
            return Clip("hitmarker", d);
        }

        static AudioClip rain;

        /// <summary>Rain on stone and shutters: a soft hiss with drops and gutters, looping.</summary>
        public static AudioClip Rain => rain != null ? rain : rain = MakeRain();

        static AudioClip MakeRain()
        {
            var r = new System.Random(23);
            int n = Rate * 6;
            var d = new float[n];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                lp += (Noise(r) - lp) * 0.35f;
                lp2 += (lp - lp2) * 0.5f;
                d[i] = lp2 * 0.6f;
            }
            // Drops: tiny clicks with a short ring, hundreds of them.
            for (int k = 0; k < 900; k++)
            {
                int at = r.Next(n - 800);
                float pitch = 1800f + (float)r.NextDouble() * 2600f, amp = 0.15f + (float)r.NextDouble() * 0.35f;
                for (int j = 0; j < 700; j++)
                    d[at + j] += Mathf.Sin(2f * Mathf.PI * pitch * j / Rate) * amp * Mathf.Exp(-j / 90f);
            }
            int fade = Rate / 2;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                d[i] = d[i] * k + d[n - fade + i] * (1f - k);
            }
            var clipped = new float[n - fade];
            System.Array.Copy(d, 0, clipped, 0, clipped.Length);
            return Clip("rain", clipped);
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
        // A two-stroke moped: exhaust pulses at the firing rate, rung through a resonant pipe,
        // with a low body thump. Not a saw wave (that's what made them sound like flies).
        static float[] Engine(int n, System.Func<float, float> firingHz, int seed, System.Func<float, float> envelope)
        {
            var d = new float[n];
            var rng = new System.Random(seed);
            float phase = 0f, sinceFire = 1f, y1 = 0f, y2 = 0f, z1 = 0f, z2 = 0f;
            float r = 0.985f, th = 2f * Mathf.PI * 340f / Rate, a1 = 2f * r * Mathf.Cos(th), a2 = r * r;
            float r2 = 0.99f, th2 = 2f * Mathf.PI * 120f / Rate, b1 = 2f * r2 * Mathf.Cos(th2), b2 = r2 * r2;
            float jitter = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = firingHz(t) * (1f + jitter);
                phase += f / Rate;
                sinceFire += 1f / Rate;
                if (phase >= 1f)
                {
                    phase -= 1f;
                    sinceFire = 0f;
                    jitter = ((float)rng.NextDouble() - 0.5f) * 0.06f;   // a little uneven, like a real engine
                }
                float pulse = Mathf.Exp(-sinceFire / 0.004f) * (0.7f + 0.6f * (float)rng.NextDouble());
                float pipe = a1 * y1 - a2 * y2 + pulse * 0.08f;
                y2 = y1; y1 = pipe;
                float body = b1 * z1 - b2 * z2 + pulse * 0.02f;
                z2 = z1; z1 = body;
                d[i] = (pipe * 0.8f + body * 1.4f) * envelope(t);
            }
            return d;
        }

        static AudioClip MakeScooter()
        {
            const float len = 2.4f;
            // Passing by: the note drops as it goes (Doppler) and it swells and fades.
            var d = Engine((int)(Rate * len), t => Mathf.Lerp(58f, 44f, t / len), 7, t =>
            {
                float pass = t / len;
                float e = Mathf.Sin(Mathf.PI * pass);
                return e * e;
            });
            return Clip("scooter", d);
        }

        static AudioClip whistle;

        /// <summary>A lookout's two-finger whistle: up, then a long high note.</summary>
        public static AudioClip Whistle => whistle != null ? whistle : whistle = MakeWhistle();

        static AudioClip MakeWhistle()
        {
            int n = (int)(Rate * 0.9f);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                bool first = t < 0.22f, second = t > 0.32f && t < 0.85f;
                if (!first && !second) continue;
                float f = first ? Mathf.Lerp(1700f, 2400f, t / 0.22f) : 2550f + 40f * Mathf.Sin(t * 40f);
                phase += f / Rate;
                float env = first ? Mathf.Sin(Mathf.PI * t / 0.22f) : Mathf.Min(1f, (t - 0.32f) * 20f) * Mathf.Min(1f, (0.85f - t) * 10f);
                d[i] = Mathf.Sin(phase * 2f * Mathf.PI) * env * 0.6f;
            }
            return Clip("whistle", d);
        }

        static AudioClip engine;

        /// <summary>A seamless loop of an engine at a steady rate, for the player's own scooter (pitch it by speed).</summary>
        public static AudioClip EngineLoop => engine != null ? engine : engine = Clip("engine", Engine(Rate, t => 50f, 11, t => 1f));

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
