using UnityEngine;

namespace OSistema.Core
{
    public enum DayPhase { Night, Morning, Afternoon, Evening }

    /// <summary>In-game calendar derived from the tick count. A day is 60,000 ticks (~17 real minutes at 1x).</summary>
    public sealed class GameClock
    {
        public const int TicksPerDay = 60000;
        public const int TicksPerHour = TicksPerDay / 24;
        const int StartHour = 8;

        static readonly string[] Weekdays = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

        readonly TickManager ticks;

        public GameClock(TickManager ticks) => this.ticks = ticks;

        long AbsoluteTicks => ticks.TicksGame + StartHour * TicksPerHour;

        public int Day => (int)(AbsoluteTicks / TicksPerDay) + 1;
        public float HourF => (AbsoluteTicks % TicksPerDay) / (float)TicksPerHour;
        public int Hour => (int)HourF;
        public int Minute => (int)((HourF - Hour) * 60f);
        public string Weekday => Weekdays[(Day - 1) % 7];
        public bool IsPayday => (Day - 1) % 7 == 6; // Sunday: the mesata

        public DayPhase Phase
        {
            get
            {
                float h = HourF;
                if (h >= 6f && h < 12f) return DayPhase.Morning;
                if (h >= 12f && h < 18f) return DayPhase.Afternoon;
                if (h >= 18f && h < 22f) return DayPhase.Evening;
                return DayPhase.Night;
            }
        }

        /// <summary>0 = deep night, 1 = full daylight.</summary>
        public float Daylight
        {
            get
            {
                float h = HourF;
                if (h < 5f || h >= 21f) return 0f;
                if (h < 7f) return Mathf.InverseLerp(5f, 7f, h);
                if (h < 18f) return 1f;
                return 1f - Mathf.InverseLerp(18f, 21f, h);
            }
        }

        public string TimeString => $"{Hour:00}:{Minute:00}";
    }
}
