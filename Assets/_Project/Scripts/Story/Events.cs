using System;
using System.Collections.Generic;
using OSistema.Core;

namespace OSistema.Story
{
    public sealed class EventOption
    {
        public string Label;
        public string Hint;                 // consequence preview shown under the button
        public Action<Game> Apply;
        public Func<Game, bool> Available;  // null = always available
    }

    /// <summary>A decision card that pauses the game: a scene from life in the System.</summary>
    public sealed class GameEvent
    {
        public string Id;
        public string Title;
        public string Neapolitan;           // a line in dialect…
        public string English;              // …and its subtitle
        public string Body;
        public readonly List<EventOption> Options = new List<EventOption>();

        public GameEvent Option(string label, string hint, Action<Game> apply, Func<Game, bool> available = null)
        {
            Options.Add(new EventOption { Label = label, Hint = hint, Apply = apply, Available = available });
            return this;
        }
    }

    /// <summary>
    /// Shows one card at a time and pauses while the player decides. A light storyteller deals
    /// random cards twice a day, never the same card twice within a few days.
    /// </summary>
    public sealed class EventSystem
    {
        const int CardCooldownDays = 4;
        const double ChancePerDeal = 0.35;

        readonly Queue<GameEvent> queue = new Queue<GameEvent>();
        readonly Dictionary<string, int> lastShownDay = new Dictionary<string, int>();
        int resumeSpeed = 1;
        int lastAbsoluteHour = -1;

        public GameEvent Current { get; private set; }

        public void Raise(GameEvent e)
        {
            if (e != null) queue.Enqueue(e);
        }

        public void Tick(Game game)
        {
            if (Current == null && queue.Count > 0) Show(queue.Dequeue(), game);

            int absoluteHour = game.Clock.Day * 24 + game.Clock.Hour;
            if (lastAbsoluteHour < 0) lastAbsoluteHour = absoluteHour;
            if (absoluteHour == lastAbsoluteHour) return;
            lastAbsoluteHour = absoluteHour;

            int hour = game.Clock.Hour;
            if ((hour == 11 || hour == 19) && game.Rng.NextDouble() < ChancePerDeal) Deal(game);
        }

        void Deal(Game game)
        {
            var candidates = new List<GameEvent>();
            foreach (var card in EventCards.RandomDeck(game))
            {
                if (card == null) continue;
                if (lastShownDay.TryGetValue(card.Id, out int day) && game.Clock.Day - day < CardCooldownDays) continue;
                candidates.Add(card);
            }
            if (candidates.Count == 0) return;
            var pick = candidates[game.Rng.Next(candidates.Count)];
            lastShownDay[pick.Id] = game.Clock.Day;
            Raise(pick);
        }

        void Show(GameEvent e, Game game)
        {
            Current = e;
            resumeSpeed = game.Ticks.Speed == 0 ? 1 : game.Ticks.Speed;
            game.Ticks.SetSpeed(0);
        }

        public bool IsAvailable(EventOption option, Game game) => option.Available == null || option.Available(game);

        public void Choose(int index, Game game)
        {
            var e = Current;
            if (e == null || index < 0 || index >= e.Options.Count) return;
            var option = e.Options[index];
            if (!IsAvailable(option, game)) return;
            Current = null;
            option.Apply?.Invoke(game);
            if (!game.Org.GameOver) game.Ticks.SetSpeed(resumeSpeed);
        }
    }
}
