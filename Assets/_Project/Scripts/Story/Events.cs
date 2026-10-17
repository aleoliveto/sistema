using System;
using System.Collections.Generic;
using OSistema.Core;
using OSistema.Npcs;
using UnityEngine;

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
        public int Portrait = -1;           // face shown on the card (Resources/Characters/face_NN)
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
            TickVisit(game);
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
            StartVisit(pick, game);
        }

        // ---------- Someone comes to find you ----------

        GameEvent visit;
        Npc visitor;
        long visitSince;
        int visitRepath;

        /// <summary>Who's on their way to talk to the player, for the label over their head.</summary>
        public Npc Visitor => visit != null ? visitor : null;
        public string VisitorTitle => visit?.Title;

        /// <summary>
        /// Instead of a card out of nowhere, a person walks up to the player and the scene plays when
        /// they reach him. If he's inside, in hospital or can't be found, it comes as a phone call.
        /// </summary>
        void StartVisit(GameEvent e, Game game)
        {
            var p = game.Player;
            if (visit != null || p == null || !p.CanAct || game.StreetLife == null)
            {
                Raise(e);
                return;
            }
            Vector2Int from = p.Cell;
            for (int i = 0; i < 12; i++)
            {
                var c = game.StreetLife.RandomDestinationNear(p.Cell, game);
                if ((c - p.Cell).sqrMagnitude >= 36 && game.Map.IsPassable(c)) { from = c; break; }
            }
            if (from == p.Cell)
            {
                Raise(e);
                return;
            }
            var rng = game.Rng;
            visitor = game.Npcs.Spawn(NpcKind.Civilian, from, 0.07f, (Color32)Color.HSVToRGB((float)rng.NextDouble(), 0.35f, 0.55f));
            visitor.IsFixture = true;
            visitor.ContactName = "visitor";
            visitor.State = 5;
            visit = e;
            visitSince = game.Ticks.TicksGame;
            visitRepath = 0;
        }

        void TickVisit(Game game)
        {
            if (visit == null) return;
            var p = game.Player;
            bool lost = visitor == null || visitor.Despawned || p == null || !p.IsPresent || p.InJail
                        || game.Ticks.TicksGame - visitSince > GameClock.TicksPerHour * 3;
            if (lost)
            {
                // Couldn't find him: they ring instead.
                var e = visit;
                EndVisit(game);
                Raise(e);
                return;
            }
            if ((visitor.Mover.Position - p.Position).sqrMagnitude < 1.7f * 1.7f)
            {
                var e = visit;
                EndVisit(game);
                Raise(e);
                return;
            }
            if (visitor.Walker == null || ++visitRepath % 60 == 0) visitor.Walker = new AI.Walker(p.Cell, adjacent: true);
            visitor.Walker.Tick(visitor.Mover, 1f, game);
        }

        void EndVisit(Game game)
        {
            visit = null;
            if (visitor == null) return;
            visitor.ContactName = null;
            visitor.IsFixture = false;
            visitor.State = 2;   // off home
            visitor.Walker = null;
            if (game.Territory.Entrances.Count > 0) visitor.Exit = game.Territory.RandomEntrance(game.Rng);
            visitor = null;
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
