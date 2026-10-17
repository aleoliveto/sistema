using UnityEngine;

namespace OSistema.Pawns
{
    /// <summary>Food and rest, each from 0 (empty) to 1 (full).</summary>
    public sealed class Needs
    {
        public const float HungryBelow = 0.3f;
        public const float RavenousBelow = 0.12f;
        public const float TiredBelow = 0.3f;
        public const float ExhaustedBelow = 0.1f;

        // Per-tick rates. A day is 60,000 ticks.
        const float FoodDecay = 1f / 48000f;       // empty in ~0.8 days
        const float RestDecay = 1f / 56000f;       // awake for ~0.9 days
        const float RestInBed = 1f / 18000f;       // a full night's sleep is ~7 hours
        const float RestOnFloor = 1f / 26000f;

        public float Food = 0.8f;
        public float Rest = 0.9f;

        public bool Hungry => Food < HungryBelow;
        public bool Tired => Rest < TiredBelow;

        public void Tick(Pawn pawn)
        {
            Food = Mathf.Max(0f, Food - FoodDecay);
            if (pawn.Sleep == SleepState.InBed) Rest = Mathf.Min(1f, Rest + RestInBed);
            else if (pawn.Sleep == SleepState.OnFloor) Rest = Mathf.Min(1f, Rest + RestOnFloor);
            else Rest = Mathf.Max(0f, Rest - RestDecay);
        }

        public void Eat(float nutrition) => Food = Mathf.Min(1f, Food + nutrition);
    }
}
