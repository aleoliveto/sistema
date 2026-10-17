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
        const int FeedMax = 3;

        static readonly Color[] CardBands =
        {
            new Color(0.24f, 0.14f, 0.09f), new Color(0.12f, 0.16f, 0.24f), new Color(0.2f, 0.09f, 0.11f), new Color(0.12f, 0.18f, 0.13f),
        };

        VisualElement feed;
        int feedVersion = -1;

        VisualElement eventDim, gameOverDim;
        GameEvent shownEvent;

        void BuildFeed()
        {
            feed = Ui.Box("feed").Into(hudLayer);
            feed.pickingMode = PickingMode.Ignore;
        }

        // Messages queue up and come in one at a time, each staying long enough to read,
        // however fast the game is running. Click a message to dismiss it (or go where it happened).
        const float ToastSeconds = 9f, ToastGap = 1.2f;
        const int ToastQueueMax = 8;
        readonly System.Collections.Generic.Queue<Message> toastQueue = new System.Collections.Generic.Queue<Message>();
        readonly System.Collections.Generic.List<(Message msg, float shown, Label label)> toasts = new System.Collections.Generic.List<(Message, float, Label)>();
        float lastToastAt;
        Game feedGame;

        void RefreshFeed()
        {
            if (feedGame != game)
            {
                feedGame = game;
                feedVersion = game.Log.Version;
                toastQueue.Clear();
                foreach (var t in toasts) t.label.RemoveFromHierarchy();
                toasts.Clear();
                // A new or loaded game: show its opening lines.
                foreach (var m in game.Log.All.Skip(System.Math.Max(0, game.Log.All.Count - 3))) toastQueue.Enqueue(m);
            }
            if (game.Log.Version != feedVersion)
            {
                int fresh = System.Math.Min(game.Log.Version - feedVersion, game.Log.All.Count);
                feedVersion = game.Log.Version;
                for (int i = game.Log.All.Count - fresh; i < game.Log.All.Count; i++) toastQueue.Enqueue(game.Log.All[i]);
                // Too much at once: keep the bad news, drop the oldest small talk.
                while (toastQueue.Count > ToastQueueMax)
                {
                    var items = toastQueue.ToList();
                    int drop = items.FindIndex(m => m.Kind == MessageKind.Info);
                    items.RemoveAt(drop >= 0 ? drop : 0);
                    toastQueue.Clear();
                    foreach (var m in items) toastQueue.Enqueue(m);
                }
            }

            float now = Time.unscaledTime;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                float age = now - toasts[i].shown;
                if (age > ToastSeconds)
                {
                    toasts[i].label.RemoveFromHierarchy();
                    toasts.RemoveAt(i);
                    continue;
                }
                toasts[i].label.style.opacity = Mathf.Clamp01((ToastSeconds - age) / 1.5f) * Mathf.Clamp01(age * 4f);
            }

            if (toastQueue.Count > 0 && toasts.Count < FeedMax && now - lastToastAt > ToastGap)
            {
                lastToastAt = now;
                var m = toastQueue.Dequeue();
                var toast = Ui.Text(m.Text, null, "toast");
                feed.Insert(0, toast);
                toast.pickingMode = PickingMode.Position;
                if (m.Kind == MessageKind.Good) toast.AddToClassList("toast--good");
                if (m.Kind == MessageKind.Bad) toast.AddToClassList("toast--bad");
                toast.style.opacity = 0f;
                var entry = (m, now, toast);
                toasts.Add(entry);
                var at = m.Location;
                toast.RegisterCallback<ClickEvent>(_ =>
                {
                    if (at.HasValue) FocusCamera(new Vector2(at.Value.x + 0.5f, at.Value.y + 0.5f));
                    toast.RemoveFromHierarchy();
                    toasts.RemoveAll(t => t.label == toast);
                });
                Tip(toast, () => at.HasValue ? "Click to go there." : "Click to dismiss.");
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

        bool devSoak;

        void RefreshModals()
        {
            if (devSoak)
            {
                // Soak test: answer every scene with its last choice, fold every paper, keep the clock running.
                if (game.Events.Current != null) game.Events.Choose(game.Events.Current.Options.Count - 1, game);
                if (PaperOpen) ClosePaper();
                if (game.Ticks.Speed != 5 && !game.Org.GameOver) game.Ticks.SetSpeed(5);
                foreach (var m in game.Phone.Messages)
                    if (m.Open(game.Ticks.TicksGame)) game.Phone.Answer(m, game.Rng.Next(m.Choices.Count), game);
            }
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
            var face = OSistema.Views.CharacterArt.Face(e.Portrait);
            if (face != null)
            {
                band.style.flexDirection = FlexDirection.Row;
                band.style.alignItems = Align.Center;
                var img = Ui.Box("card__portrait").Into(band);
                img.style.backgroundImage = new StyleBackground(face);
            }
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
            Ui.Btn("Start a new family", () => OpenMenu(Menu.Creator), body, "option");
        }
    }
}
