using System.Collections.Generic;
using UnityEngine;

namespace OSistema.Pawns
{
    public sealed class Thought
    {
        public string Id;
        public string Label;
        public float Value;
        public long ExpiresTick; // -1 for situational thoughts, which are rebuilt every check
    }

    /// <summary>
    /// Mood = 50 + the sum of thoughts. Memories ("had a pizza") expire; situational thoughts
    /// ("hungry") are recomputed from the pawn's current state.
    /// </summary>
    public sealed class Mood
    {
        public const float BreakBelow = 20f;
        const float Base = 50f;

        public readonly List<Thought> Memories = new List<Thought>();
        readonly List<Thought> situational = new List<Thought>();

        public float Value { get; private set; } = Base;

        public IEnumerable<Thought> AllThoughts
        {
            get
            {
                foreach (var t in situational) yield return t;
                foreach (var t in Memories) yield return t;
            }
        }

        public string Description => Value switch
        {
            < 10f => "Broken",
            < 25f => "Stressed",
            < 40f => "Unhappy",
            < 60f => "Content",
            < 80f => "Happy",
            _ => "Thriving",
        };

        /// <summary>Adds a memory, or refreshes it if the pawn already has one with the same id.</summary>
        public void AddMemory(string id, string label, float value, long now, int durationTicks)
        {
            var existing = Memories.Find(m => m.Id == id);
            if (existing != null)
            {
                existing.ExpiresTick = now + durationTicks;
                return;
            }
            Memories.Add(new Thought { Id = id, Label = label, Value = value, ExpiresTick = now + durationTicks });
        }

        public void Recalculate(Pawn pawn, long now)
        {
            Memories.RemoveAll(m => m.ExpiresTick <= now);

            situational.Clear();
            var needs = pawn.Needs;
            if (needs.Food <= 0f) AddSituational("Starving", -20f);
            else if (needs.Food < Needs.RavenousBelow) AddSituational("Ravenous", -12f);
            else if (needs.Hungry) AddSituational("Hungry", -6f);

            if (needs.Rest < Needs.ExhaustedBelow) AddSituational("Exhausted", -10f);
            else if (needs.Tired) AddSituational("Tired", -4f);

            float pain = pawn.Health.Pain;
            if (pain > 0.05f) AddSituational(pain > 0.5f ? "In agony" : "In pain", -Mathf.Round(pain * 20f));

            float total = Base;
            foreach (var t in AllThoughts) total += t.Value;
            Value = Mathf.Clamp(total, 0f, 100f);
        }

        void AddSituational(string label, float value) =>
            situational.Add(new Thought { Id = label, Label = label, Value = value, ExpiresTick = -1 });
    }
}
