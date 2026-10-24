using System.Linq;
using OSistema.Pawns;
using OSistema.Story;
using UnityEngine;

namespace OSistema.Core
{
    /// <summary>
    /// The city's calendar. The game starts on 10 September. Every Sunday Napoli plays at three: the streets
    /// empty, even the police are in front of a television, and if they win the city goes out to celebrate (and
    /// buy). San Gennaro's blood on 19 September; the boss's Christmas envelope on the 24th of December; at
    /// midnight on New Year's Eve the fireworks cover anything, a gunshot included.
    /// </summary>
    public sealed class Calendar
    {
        static readonly int[] MonthDays = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
        static readonly string[] Months = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        const int StartMonth = 8, StartDay = 10;   // 10 September

        int lastHour = -1;
        int resultDay = -1;
        bool won;

        /// <summary>The date of a game day (day of month 1.., month 0..11).</summary>
        public static (int day, int month) DateOf(int gameDay)
        {
            int d = StartDay, m = StartMonth;
            for (int i = 1; i < gameDay; i++)
            {
                if (++d > MonthDays[m]) { d = 1; m = (m + 1) % 12; }
            }
            return (d, m);
        }

        public static string DateString(int gameDay)
        {
            var (d, m) = DateOf(gameDay);
            return $"{d} {Months[m]}";
        }

        static bool Sunday(GameClock c) => c.IsPayday;

        /// <summary>Napoli are playing: from three to five on a Sunday.</summary>
        public bool MatchOn(Game game) => Sunday(game.Clock) && game.Clock.Hour >= 15 && game.Clock.Hour < 17;

        /// <summary>Napoli won today, and the city is out celebrating (until two in the morning).</summary>
        public bool Celebrating(Game game)
        {
            var c = game.Clock;
            if (Sunday(c) && c.Hour >= 17) return resultDay == c.Day && won;
            // The small hours of Monday still belong to Sunday's party.
            return c.Hour < 2 && resultDay == c.Day - 1 && won;
        }

        /// <summary>New Year's Eve, from eleven to one: fireworks in every street. Nobody calls the police for a bang.</summary>
        public bool Fireworks(Game game)
        {
            var c = game.Clock;
            var (d, m) = DateOf(c.Day);
            return (d == 31 && m == 11 && c.Hour >= 23) || (d == 1 && m == 0 && c.Hour < 1);
        }

        /// <summary>Did Napoli win today? Decided at the final whistle (once a Sunday), shared with the betting book.</summary>
        public bool NapoliWon(Game game)
        {
            if (resultDay != game.Clock.Day)
            {
                resultDay = game.Clock.Day;
                won = game.Rng.NextDouble() < 0.5;
            }
            return won;
        }

        /// <summary>How many more buyers come out: a win, the festivities.</summary>
        public float DemandFactor(Game game)
        {
            if (Celebrating(game) || Fireworks(game)) return 1.45f;
            if (MatchOn(game)) return 0.5f;
            var (d, m) = DateOf(game.Clock.Day);
            if (m == 11 && d >= 20) return 1.2f;   // Christmas week: money about, families out
            return 1f;
        }

        /// <summary>How many people are out in the street compared with an ordinary day.</summary>
        public float StreetFactor(Game game) => MatchOn(game) ? 0.35f : Celebrating(game) || Fireworks(game) ? 1.3f : 1f;

        public void Tick(Game game)
        {
            var c = game.Clock;
            // Car horns and the odd firework while the city celebrates.
            if ((Celebrating(game) || Fireworks(game)) && game.Ticks.TicksGame % 240 == 0 && game.Player != null)
            {
                var at = game.Player.Position + new Vector2((float)game.Rng.NextDouble() * 30f - 15f, (float)game.Rng.NextDouble() * 30f - 15f);
                OSistema.Views.SoundManager.PlayAt(Fireworks(game) || game.Rng.NextDouble() < 0.3 ? OSistema.Views.AudioSynth.GunshotFar : OSistema.Views.AudioSynth.Horn, at, 0.25f);
            }
            int hourKey = c.Day * 24 + c.Hour;
            if (hourKey == lastHour) return;
            lastHour = hourKey;
            var (d, m) = DateOf(c.Day);
            long now = game.Ticks.TicksGame;

            if (Sunday(c) && c.Hour == 15)
                game.Log.Add("Napoli kick off. The vicoli empty: everyone's in front of a television, the police included.", MessageKind.Info);
            if (Sunday(c) && c.Hour == 17)
            {
                bool w = NapoliWon(game);
                foreach (var p in game.Pawns.Crew)
                    if (p.IsPresent) p.Mood.AddMemory("napoli", w ? "Napoli won" : "Napoli lost", w ? 4f : -2f, now, GameClock.TicksPerDay / 2);
                if (w)
                {
                    game.Org.Respect = Mathf.Min(100f, game.Org.Respect + 0.5f);
                    game.Log.Add("Napoli won! Horns, flags out of the windows, the whole city in the street till late. Business will be good tonight.", MessageKind.Good);
                }
                else game.Log.Add("Napoli lost. A quiet, sulky evening in the vicoli.", MessageKind.Info);
            }
            if (d == 19 && m == 8 && c.Hour == 10) SanGennaro(game, now);
            if (d == 24 && m == 11 && c.Hour == 19) game.Events.Raise(ChristmasEnvelope(game));
            if (d == 31 && m == 11 && c.Hour == 23)
                game.Log.Add("Midnight is coming: fireworks in every street. For an hour, nobody will call the police over a bang.", MessageKind.Info);
        }

        static void SanGennaro(Game game, long now)
        {
            bool miracle = game.Rng.NextDouble() < 0.85;
            foreach (var p in game.Pawns.Crew)
                if (p.IsPresent) p.Mood.AddMemory("gennaro", miracle ? "San Gennaro's blood melted" : "San Gennaro's blood stayed solid", miracle ? 4f : -5f, now, GameClock.TicksPerDay * 2);
            game.Log.Add(miracle
                ? "San Gennaro's day: in the Duomo the blood has melted. The city breathes out; the crew are in good spirits."
                : "San Gennaro's day: the blood did not melt. A bad omen, everyone says so, and the crew are uneasy.", miracle ? MessageKind.Good : MessageKind.Bad);
        }

        static GameEvent ChristmasEnvelope(Game game)
        {
            var crew = game.Pawns.Crew.Where(p => p.IsPresent && !p.Controlled).ToList();
            int each = 50 + 10 * game.Org.Rank;
            int total = each * crew.Count;
            return new GameEvent
            {
                Id = "christmas_envelope",
                Title = "Christmas Eve",
                Neapolitan = "Natale cu 'e tuoje.",
                English = "Christmas with your own.",
                Body = $"The capitone is on the stove and the presepe lit. The crew expect the boss's envelope: €{each} each, €{total} for all of them.",
            }
            .Option($"An envelope for everyone (€{total})", "Loyalty +10 and a good Christmas for all.", g =>
            {
                g.Stock.Cash -= total;
                foreach (var p in crew) { p.Loyalty = Mathf.Min(100f, p.Loyalty + 10f); p.Mood.AddMemory("christmas", "The boss's Christmas envelope", 6f, g.Ticks.TicksGame, GameClock.TicksPerDay * 2); }
            }, g => g.Stock.Cash >= total)
            .Option("Times are hard this year", "Nothing paid. Loyalty −6: they'll remember.", g =>
            {
                foreach (var p in crew) { p.Loyalty = Mathf.Max(0f, p.Loyalty - 6f); p.Mood.AddMemory("christmas", "No envelope at Christmas", -5f, g.Ticks.TicksGame, GameClock.TicksPerDay * 2); }
            });
        }
    }
}
