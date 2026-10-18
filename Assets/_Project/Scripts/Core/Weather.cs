using UnityEngine;

namespace OSistema.Core
{
    /// <summary>
    /// Sun or rain. Decided a few hours at a time: most days are dry, but autumn and winter in Naples
    /// bring sudden downpours that empty the streets (fewer customers, fewer patrols, more cover).
    /// Not saved: the sky starts fresh with each load.
    /// </summary>
    public sealed class Weather
    {
        /// <summary>0 dry .. 1 pouring. Eases towards <see cref="target"/> so rain starts and stops gently.</summary>
        public float Rain;
        float target;
        int nextChangeHour = -1;

        public bool Raining => Rain > 0.15f;

        /// <summary>For testing: start pouring now, for a few hours.</summary>
        public void ForceRain()
        {
            target = Rain = 0.9f;
            nextChangeHour = int.MaxValue;
        }

        /// <summary>For testing: clear skies for the rest of the day.</summary>
        public void ForceSun()
        {
            target = Rain = 0f;
            nextChangeHour = int.MaxValue;
        }

        public void Tick(Game game)
        {
            int hour = game.Clock.Day * 24 + game.Clock.Hour;
            if (hour >= nextChangeHour)
            {
                var rng = game.Rng;
                bool wet = rng.NextDouble() < (target > 0f ? 0.55 : 0.16);   // rain tends to stay a while
                target = wet ? 0.4f + (float)rng.NextDouble() * 0.6f : 0f;
                nextChangeHour = hour + 2 + rng.Next(5);
            }
            // About a game-hour to go from dry to pouring.
            Rain = Mathf.MoveTowards(Rain, target, 1f / GameClock.TicksPerHour);
        }
    }
}
