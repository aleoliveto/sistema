using System;
using UnityEngine;

namespace OSistema.Core
{
    /// <summary>
    /// Fixed-step simulation clock. All game logic runs on ticks, never on frame time,
    /// so speed changes, pausing and save/load stay deterministic.
    /// </summary>
    public sealed class TickManager
    {
        public const int TicksPerSecond = 60;
        public static readonly int[] SpeedMultipliers = { 0, 1, 3, 6, 30, 120 }; // 4 and 5 are debug speeds
        public const int NormalSpeedCount = 4;

        // Caps catch-up work after a frame hitch so the game never spirals.
        const int MaxTicksPerFrame = 3000;

        public long TicksGame { get; private set; }
        public int Speed { get; private set; } = 1;
        public bool Paused => Speed == 0;

        public event Action OnTick;

        int speedBeforePause = 1;
        double accumulator;

        public void SetSpeed(int speed)
        {
            speed = Mathf.Clamp(speed, 0, SpeedMultipliers.Length - 1);
            if (speed > 0) speedBeforePause = speed;
            Speed = speed;
        }

        public void TogglePause() => SetSpeed(Paused ? speedBeforePause : 0);

        /// <summary>Debug: runs the simulation forward immediately.</summary>
        public void Advance(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                TicksGame++;
                OnTick?.Invoke();
            }
        }

        /// <summary>Sets the clock when loading a save.</summary>
        public void Restore(long ticks)
        {
            TicksGame = ticks;
            accumulator = 0;
        }

        public void Update(float realDeltaTime)
        {
            if (Paused)
            {
                accumulator = 0;
                return;
            }

            accumulator += realDeltaTime * TicksPerSecond * SpeedMultipliers[Speed];
            int done = 0;
            while (accumulator >= 1d && done < MaxTicksPerFrame)
            {
                accumulator -= 1d;
                TicksGame++;
                OnTick?.Invoke();
                done++;
            }
            if (done >= MaxTicksPerFrame) accumulator = 0;
        }
    }
}
