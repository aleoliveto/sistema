using OSistema.Core;
using OSistema.Social;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>The family phone (P) and the Sunday paper.</summary>
    public sealed partial class Hud
    {
        VisualElement phoneWindow, phoneBody, phoneStatus;
        Label phoneLine;
        Button phoneButton;
        int phoneSignature = -1;
        float phoneTimer;
        Game phoneGame;

        VisualElement paperDim;
        int paperResumeSpeed = 1;

        void BuildPhone()
        {
            phoneWindow = Ui.Box("phone").Into(hudLayer);
            var head = Ui.Box("phone__head").Into(phoneWindow);
            new Icon(IconKind.Phone, 16).Into(head);
            Ui.Text("TELEFONINO", head, "phone__title");
            Ui.Box("spacer").Into(head);
            var paper = Ui.Btn("", () => { if (game?.Newspaper.Latest != null) ShowPaper(game.Newspaper.Latest, pause: false); }, head, "btn--small");
            new Icon(IconKind.Paper, 14).Into(paper);
            Tip(paper, () => game?.Newspaper.Latest != null ? "Read this Sunday's La Sirena." : "La Sirena comes out on Sunday morning.");
            Ui.Btn("✕", () => ToggleWindow(Window.Phone), head, "btn--small");
            phoneLine = Ui.Text("", phoneWindow, "phone__line");
            var scroll = new ScrollView(ScrollViewMode.Vertical).Cls("phone__body").Into(phoneWindow);
            phoneBody = scroll.contentContainer;
            phoneWindow.Show(false);

            paperDim = Ui.Box("dim").Into(modalLayer);
            paperDim.Show(false);
        }

        void HookPhone(Game g)
        {
            if (phoneGame != null)
            {
                phoneGame.Phone.Ring -= OnRing;
                phoneGame.Newspaper.Published -= OnPaper;
            }
            phoneGame = g;
            g.Phone.Ring += OnRing;
            g.Newspaper.Published += OnPaper;
            phoneSignature = -1;
            paperDim.Show(false);
        }

        void OnRing(PhoneMessage m)
        {
            OSistema.Views.SoundManager.PlayUi(OSistema.Views.AudioSynth.Ring, 0.35f);
            if (openWindow == Window.Phone) m.Read = true;
        }

        void OnPaper(Edition e) => ShowPaper(e, pause: true);

        bool PaperOpen => paperDim != null && paperDim.style.display.value == DisplayStyle.Flex;

        void ClosePaper()
        {
            paperDim.Show(false);
            if (paperResumeSpeed > 0 && game != null && !game.Org.GameOver) game.Ticks.SetSpeed(paperResumeSpeed);
            paperResumeSpeed = -1;
        }

        void RefreshPhone()
        {
            long now = game.Ticks.TicksGame;
            var phone = game.Phone;
            int unread = phone.Unread, waiting = phone.Waiting(now);
            var label = phoneButton.Q<Label>(className: "tab__label");
            if (label != null) label.text = unread > 0 ? $"PHONE · {unread}" : "PHONE";
            phoneButton.Toggle("btn--on", openWindow == Window.Phone);
            phoneButton.Toggle("tab--alert", unread > 0 || waiting > 0);

            if (openWindow != Window.Phone) return;
            phone.MarkAllRead();

            int sig = phone.S.NextId * 31 + waiting * 7 + (phone.HasBurner(now) ? 1 : 0);
            foreach (var m in phone.Messages) sig += m.Answer + 2;
            phoneTimer += RefreshSeconds;
            if (sig == phoneSignature && phoneTimer < 2f) return;
            phoneSignature = sig;
            phoneTimer = 0f;

            // A tapped line sounds a little wrong, some days.
            bool hiss = phone.Tapped(now) && game.Clock.Day % 2 == 0;
            phoneLine.text = phone.HasBurner(now)
                ? $"<color=#8FD18A>Burner SIM · {(phone.S.BurnerUntil - now) / GameClock.TicksPerDay + 1} days left</color>"
                : hiss ? "<color=#F2B866>…the line crackles.</color>" : "Family line";

            phoneBody.Clear();
            if (phone.Messages.Count == 0)
                Ui.Text("No messages. In the rione, that never lasts.", phoneBody, "small", "muted", "wrap");
            foreach (var m in phone.Messages) Bubble(m, now);
        }

        void Bubble(PhoneMessage m, long now)
        {
            bool open = m.Open(now);
            var b = Ui.Box("msg").Into(phoneBody);
            if (open) b.AddToClassList("msg--open");
            var head = Ui.Row(b);
            Ui.Text(m.From, head, "msg__from");
            Ui.Box("spacer").Into(head);
            Ui.Text($"day {m.Day} · {Phone.TimeOf(m)}", head, "tiny");
            if (!string.IsNullOrEmpty(m.Neapolitan))
            {
                Ui.Text($"“{m.Neapolitan}”", b, "msg__nap", "wrap");
                Ui.Text(m.English, b, "small", "muted", "wrap");
            }
            if (!string.IsNullOrEmpty(m.Body)) Ui.Text(m.Body, b, "small", "wrap").style.marginTop = 3;

            if (open)
            {
                if (m.Expires > 0)
                    Ui.Text($"Answer within {Mathf.Max(1, (m.Expires - now) / GameClock.TicksPerHour)}h", b, "tiny");
                for (int i = 0; i < m.Choices.Count; i++)
                {
                    int choice = i;
                    var msg = m;
                    var btn = Ui.Btn(m.Choices[i], () =>
                    {
                        game.Phone.Answer(msg, choice, game);
                        phoneSignature = -1;
                        RefreshAll();
                    }, b, "btn--small", "msg__choice");
                    string hint = i < m.Hints.Count ? m.Hints[i] : null;
                    if (!string.IsNullOrEmpty(hint)) Tip(btn, () => hint + (msg.Criminal && choice == 0 && game.Phone.Tapped(game.Ticks.TicksGame) ? "\n(If anyone's listening, they heard that.)" : ""));
                }
            }
            else if (m.Answer >= 0 && m.Answer < m.Choices.Count)
                Ui.Text($"→ {m.Choices[m.Answer]}", b, "msg__answer");
        }

        // ---------- La Sirena ----------

        void ShowPaper(Edition e, bool pause)
        {
            paperDim.Clear();
            if (pause && game.Ticks.Speed != 0)
            {
                paperResumeSpeed = game.Ticks.Speed;
                game.Ticks.SetSpeed(0);
            }
            else if (pause) paperResumeSpeed = 1;
            else paperResumeSpeed = -1;

            var paper = Ui.Box("paper").Into(paperDim);
            var close = Ui.Btn("✕  Fold the paper  [Esc]", ClosePaper, paper, "btn--small", "paper__close");
            var mast = Ui.Box("paper__mast").Into(paper);
            Ui.Text(Newspaper.Name, mast, "paper__name");
            Ui.Text($"Quotidiano di Napoli e provincia · No. {e.Number} · Domenica, day {e.Day} · €1.50", mast, "paper__dateline");

            Ui.Text(e.Lead.Headline.ToUpper(), paper, "paper__headline", "wrap");
            Ui.Text(e.Lead.Text, paper, "paper__lead", "wrap");

            var cols = Ui.Box("paper__cols").Into(paper);
            foreach (var c in e.Columns)
            {
                var col = Ui.Box("paper__col").Into(cols);
                Ui.Text(c.Headline, col, "paper__colhead", "wrap");
                Ui.Text(c.Text, col, "paper__text", "wrap");
            }

            var foot = Ui.Box("paper__foot").Into(paper);
            Ui.Text(e.Sidebar, foot, "paper__text", "paper__sidebar", "wrap");
            Ui.Text(e.Brief, foot, "paper__text", "wrap");

            paperDim.Show(true);
            OSistema.Views.SoundManager.PlayUi(OSistema.Views.AudioSynth.Click, 0.3f);
        }
    }
}
