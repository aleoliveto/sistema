using System.Linq;
using OSistema.Core;
using OSistema.Story;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>Notification feed, event cards and the game-over screen.</summary>
    public sealed partial class Hud
    {
        const int FeedTicksVisible = 5000;
        const int FeedMax = 4;

        static readonly Color[] CardBands =
        {
            new Color(0.24f, 0.14f, 0.09f), new Color(0.12f, 0.16f, 0.24f), new Color(0.2f, 0.09f, 0.11f), new Color(0.12f, 0.18f, 0.13f),
        };

        VisualElement feed;
        int feedVersion = -1;
        long feedTick = -1;

        VisualElement eventDim, gameOverDim;
        GameEvent shownEvent;

        void BuildFeed()
        {
            feed = Ui.Box("feed").Into(hudLayer);
            feed.pickingMode = PickingMode.Ignore;
        }

        void RefreshFeed()
        {
            long now = game.Ticks.TicksGame;
            // Rebuild when there's news, or now and then so old toasts drop off.
            if (game.Log.Version == feedVersion && now - feedTick < 600) return;
            feedVersion = game.Log.Version;
            feedTick = now;

            feed.Clear();
            var recent = game.Log.All.Where(m => now - m.Tick < FeedTicksVisible).Reverse().Take(FeedMax).ToList();
            foreach (var m in recent)
            {
                var toast = Ui.Text(m.Text, feed, "toast");
                toast.pickingMode = PickingMode.Position;
                if (m.Kind == MessageKind.Good) toast.AddToClassList("toast--good");
                if (m.Kind == MessageKind.Bad) toast.AddToClassList("toast--bad");
                float age = (now - m.Tick) / (float)FeedTicksVisible;
                toast.style.opacity = Mathf.Clamp01(1.4f - age * 1.2f);
                if (m.Location.HasValue)
                {
                    var at = m.Location.Value;
                    toast.RegisterCallback<ClickEvent>(_ => FocusCamera(new Vector2(at.x + 0.5f, at.y + 0.5f)));
                    Tip(toast, () => "Click to go there.");
                }
            }
        }

        // ---------- Modals ----------

        void BuildModals()
        {
            eventDim = Ui.Box("dim").Into(modalLayer);
            eventDim.Show(false);
            gameOverDim = Ui.Box("dim").Into(modalLayer);
            gameOverDim.Show(false);
        }

        void RefreshModals()
        {
            var current = game.Events.Current;
            if (current != shownEvent)
            {
                shownEvent = current;
                eventDim.Clear();
                if (current != null) BuildCard(current);
            }
            eventDim.Show(current != null && !game.Org.GameOver);

            if (game.Org.GameOver && gameOverDim.childCount == 0) BuildGameOver();
            gameOverDim.Show(game.Org.GameOver);
            if (!game.Org.GameOver && gameOverDim.childCount > 0) gameOverDim.Clear();
        }

        void BuildCard(GameEvent e)
        {
            var card = Ui.Box("panel", "card").Into(eventDim);
            var band = Ui.Box("card__band").Into(card);
            band.style.backgroundColor = CardBands[Mathf.Abs(e.Id.GetHashCode()) % CardBands.Length];
            Ui.Text(e.Title, band, "card__title");

            var body = Ui.Box("card__body").Into(card);
            Ui.Text($"“{e.Neapolitan}”", body, "card__quote");
            Ui.Text(e.English, body, "card__sub");
            Ui.Text(e.Body, body, "card__text");

            for (int i = 0; i < e.Options.Count; i++)
            {
                int index = i;
                var option = e.Options[i];
                bool available = game.Events.IsAvailable(option, game);
                var b = Ui.Btn("", () =>
                {
                    game.Events.Choose(index, game);
                    RefreshAll();
                }, body, "option");
                b.SetEnabled(available);
                Ui.Text(option.Label, b, "option__label");
                Ui.Text(available ? option.Hint : "<color=#F07A6E>Not possible right now.</color>", b, "option__hint");
            }
        }

        void BuildGameOver()
        {
            var card = Ui.Box("panel", "card").Into(gameOverDim);
            var band = Ui.Box("card__band").Into(card);
            band.style.backgroundColor = new Color(0.12f, 0.05f, 0.05f);
            Ui.Text("The family is finished", band, "card__title");
            var body = Ui.Box("card__body").Into(card);
            Ui.Text(game.Org.GameOverReason, body, "card__text");
            Ui.Text($"Survived {game.Clock.Day} days across {game.Org.Generation} generation(s).", body, "card__sub");
            Ui.Btn("Start a new family", () => NewGameRequested?.Invoke(), body, "option");
        }
    }
}
