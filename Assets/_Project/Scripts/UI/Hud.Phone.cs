using OSistema.Core;
using OSistema.Social;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>The family phone (P) and the Sunday paper.</summary>
    public sealed partial class Hud
    {
        VisualElement phoneWindow, phoneBody;

        public bool PhoneOpen => openWindow == Window.Phone;
        Label phoneLine;
        Button phoneButton;
        int phoneSignature = -1;
        float phoneTimer;
        Game phoneGame;

        VisualElement paperDim;
        int paperResumeSpeed = 1;

        // ---------- The burner: an old candybar phone with a green screen ----------

        enum PhoneScreen { Home, Menu, Inbox, Message, Contacts, Contact, Call, Sim }
        PhoneScreen phoneScreen;
        int phoneSel;
        PhoneMessage phoneMsg;
        OSistema.Crime.Contact phoneContact;
        PhoneCalls.Call phoneCall;
        VisualElement phoneLcd, phoneLcdBody;
        Label phoneSoftL, phoneSoftR, phoneStatusLine;
        readonly System.Collections.Generic.List<System.Action> phoneItems = new System.Collections.Generic.List<System.Action>();

        void BuildPhone()
        {
            phoneWindow = Ui.Box("nokia").Into(hudLayer);
            Ui.Box("nokia__ear").Into(phoneWindow);
            var brand = Ui.Text("RIONE", phoneWindow, "nokia__brand");
            brand.pickingMode = PickingMode.Ignore;
            phoneLcd = Ui.Box("nokia__screen").Into(phoneWindow);
            phoneStatusLine = Ui.Text("", phoneLcd, "lcd", "lcd__status");
            var scroll = new ScrollView(ScrollViewMode.Vertical).Cls("lcd__body").Into(phoneLcd);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;   // an old phone has no scrollbar: the arrows scroll
            phoneLcdBody = scroll.contentContainer;
            var soft = Ui.Row(phoneLcd);
            soft.AddToClassList("lcd__soft");
            phoneSoftL = Ui.Text("", soft, "lcd", "lcd__softkey");
            Ui.Box("spacer").Into(soft);
            phoneSoftR = Ui.Text("", soft, "lcd", "lcd__softkey");
            phoneLine = phoneStatusLine;
            phoneBody = phoneLcdBody;

            // Soft keys, the navigation rocker, call / end.
            var keys = Ui.Row(phoneWindow);
            keys.AddToClassList("nokia__navrow");
            Ui.Btn("", () => PhoneSoft(true), keys, "nokia__soft");
            var nav = Ui.Box("nokia__nav").Into(keys);
            Ui.Btn("▲", () => PhoneMove(-1), nav, "nokia__navkey");
            Ui.Btn("OK", PhoneSelect, nav, "nokia__navkey", "nokia__ok");
            Ui.Btn("▼", () => PhoneMove(1), nav, "nokia__navkey");
            Ui.Btn("", () => PhoneSoft(false), keys, "nokia__soft");
            var calls = Ui.Row(phoneWindow);
            calls.AddToClassList("nokia__callrow");
            Ui.Btn("", PhoneSelect, calls, "nokia__call", "nokia__call--green");
            Ui.Box("spacer").Into(calls);
            Ui.Btn("", PhoneBack, calls, "nokia__call", "nokia__call--red");

            var pad = Ui.Box("nokia__pad").Into(phoneWindow);
            string[] letters = { "", "abc", "def", "ghi", "jkl", "mno", "pqrs", "tuv", "wxyz", "", "+", "" };
            string[] digits = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "*", "0", "#" };
            for (int i = 0; i < 12; i++)
            {
                int n = i;
                var k = Ui.Btn("", () => { if (n < 9) PhoneDigit(n + 1); }, pad, "nokia__key");
                Ui.Text(digits[i], k, "nokia__digit").pickingMode = PickingMode.Ignore;
                Ui.Text(letters[i], k, "nokia__letters").pickingMode = PickingMode.Ignore;
            }
            Tip(phoneWindow, () => "↑ ↓ choose · Enter OK · Backspace back · 1–9 pick a line · P put it away");
            phoneWindow.Show(false);

            paperDim = Ui.Box("dim").Into(modalLayer);
            paperDim.Show(false);
        }

        /// <summary>Arrow keys, Enter, Backspace and the number row, while the phone is out.</summary>
        void PhoneKeys(UnityEngine.InputSystem.Keyboard kb)
        {
            if (openWindow != Window.Phone || game == null) return;
            if (kb.upArrowKey.wasPressedThisFrame) PhoneMove(-1);
            if (kb.downArrowKey.wasPressedThisFrame) PhoneMove(1);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) PhoneSelect();
            if (kb.backspaceKey.wasPressedThisFrame) PhoneBack();
            var row = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key, kb.digit6Key, kb.digit7Key, kb.digit8Key, kb.digit9Key };
            for (int i = 0; i < row.Length; i++) if (row[i].wasPressedThisFrame) PhoneDigit(i + 1);
        }

        void PhoneMove(int d)
        {
            if (phoneItems.Count == 0) return;
            phoneSel = (phoneSel + d + phoneItems.Count) % phoneItems.Count;
            RenderPhone();
        }

        void PhoneDigit(int n)
        {
            if (n - 1 < phoneItems.Count)
            {
                phoneSel = n - 1;
                PhoneSelect();
            }
        }

        void PhoneSelect()
        {
            if (phoneScreen == PhoneScreen.Home) { PhoneGo(PhoneScreen.Menu); return; }
            if (phoneSel >= 0 && phoneSel < phoneItems.Count) phoneItems[phoneSel]();
        }

        void PhoneSoft(bool left)
        {
            if (left) PhoneSelect();
            else PhoneBack();
        }

        void PhoneBack()
        {
            switch (phoneScreen)
            {
                case PhoneScreen.Home: ToggleWindow(Window.Phone); return;
                case PhoneScreen.Message: PhoneGo(PhoneScreen.Inbox); return;
                case PhoneScreen.Contact: PhoneGo(PhoneScreen.Contacts); return;
                case PhoneScreen.Call: PhoneGo(PhoneScreen.Contact); return;
                case PhoneScreen.Menu: PhoneGo(PhoneScreen.Home); return;
                default: PhoneGo(PhoneScreen.Menu); return;
            }
        }

        void PhoneGo(PhoneScreen s)
        {
            phoneScreen = s;
            phoneSel = 0;
            OSistema.Views.SoundManager.PlayUi(OSistema.Views.AudioSynth.Click, 0.15f);
            RenderPhone();
        }

        Label LcdRow(string text, System.Action act)
        {
            int index = phoneItems.Count;
            var l = Ui.Text(text, phoneLcdBody, "lcd", "lcd__row", "wrap");
            if (act != null)
            {
                phoneItems.Add(act);
                l.Toggle("lcd__row--sel", index == phoneSel);
                if (index == phoneSel) l.schedule.Execute(() => (phoneLcdBody.parent?.parent as ScrollView)?.ScrollTo(l));
                l.RegisterCallback<ClickEvent>(_ => { phoneSel = index; PhoneSelect(); });
            }
            return l;
        }

        void LcdText(string text, params string[] extra)
        {
            var l = Ui.Text(text, phoneLcdBody, "lcd", "wrap");
            foreach (var c in extra) l.AddToClassList(c);
        }

        void RenderPhone()
        {
            if (game == null || phoneLcdBody == null) return;
            long now = game.Ticks.TicksGame;
            var phone = game.Phone;
            phoneItems.Clear();
            phoneLcdBody.Clear();
            int bars = phone.Tapped(now) && game.Clock.Day % 2 == 0 ? 2 : 4;
            phoneStatusLine.text = $"{new string('▮', bars)}{new string('▯', 4 - bars)}  {(phone.HasBurner(now) ? "SIM ANON" : "RIONE GSM")}  ▭▭▭";

            switch (phoneScreen)
            {
                case PhoneScreen.Home:
                {
                    LcdText($"{game.Clock.Hour:00}:{game.Clock.Minute:00}", "lcd__clock");
                    LcdText($"Day {game.Clock.Day}", "lcd__center");
                    int unread = phone.Unread, waiting = phone.Waiting(now);
                    if (unread > 0) LcdText($"✉ {unread} new message{(unread == 1 ? "" : "s")}", "lcd__center");
                    else if (waiting > 0) LcdText($"✉ {waiting} waiting for an answer", "lcd__center");
                    if (phone.InTransit > 0) LcdText($"{phone.InTransit} order{(phone.InTransit == 1 ? "" : "s")} on the way", "lcd__center");
                    phoneSoftL.text = "Menu";
                    phoneSoftR.text = "Exit";
                    break;
                }
                case PhoneScreen.Menu:
                {
                    LcdText("MENU", "lcd__title");
                    int unread = phone.Unread;
                    LcdRow($"1 Messages{(unread > 0 ? $" ({unread})" : "")}", () => PhoneGo(PhoneScreen.Inbox));
                    LcdRow("2 Contacts", () => PhoneGo(PhoneScreen.Contacts));
                    LcdRow("3 SIM card", () => PhoneGo(PhoneScreen.Sim));
                    LcdRow("4 La Sirena", () => { if (game.Newspaper.Latest != null) ShowPaper(game.Newspaper.Latest, pause: false); });
                    phoneSoftL.text = "Select";
                    phoneSoftR.text = "Back";
                    break;
                }
                case PhoneScreen.Inbox:
                {
                    LcdText("MESSAGES", "lcd__title");
                    if (phone.Messages.Count == 0) LcdText("No messages.");
                    // Newest at the top.
                    for (int i = 0; i < phone.Messages.Count; i++)
                    {
                        var m = phone.Messages[i];
                        string mark = m.Open(now) ? "?" : !m.Read ? "•" : " ";
                        string preview = m.English ?? m.Body ?? "";
                        if (preview.Length > 26) preview = preview.Substring(0, 25) + "…";
                        var msg = m;
                        LcdRow($"{mark} {ShortName(m.From)}: {preview}", () => { phoneMsg = msg; msg.Read = true; PhoneGo(PhoneScreen.Message); });
                    }
                    if (phone.Messages.Count > 0)
                        LcdRow("× Delete all read", () => { game.Phone.DeleteRead(game.Ticks.TicksGame); phoneSel = 0; RenderPhone(); });
                    phoneSoftL.text = "Read";
                    phoneSoftR.text = "Back";
                    break;
                }
                case PhoneScreen.Message:
                {
                    var m = phoneMsg;
                    if (m == null) { phoneScreen = PhoneScreen.Inbox; RenderPhone(); return; }
                    LcdText($"{m.From}  {Phone.TimeOf(m)}", "lcd__title");
                    if (OSistema.Story.Dialect.Show(m.Neapolitan)) LcdText($"“{m.Neapolitan}”", "lcd__nap");
                    if (!string.IsNullOrEmpty(m.English)) LcdText(m.English);
                    if (!string.IsNullOrEmpty(m.Body)) LcdText(m.Body, "lcd__small");
                    if (m.Open(now))
                    {
                        if (m.Expires > 0) LcdText($"Answer within {Mathf.Max(1, (m.Expires - now) / GameClock.TicksPerHour)}h", "lcd__small");
                        for (int i = 0; i < m.Choices.Count; i++)
                        {
                            int choice = i;
                            var msg = m;
                            LcdRow($"{i + 1} {m.Choices[i]}", () =>
                            {
                                game.Phone.Answer(msg, choice, game);
                                RenderPhone();
                                RefreshAll();
                            });
                        }
                        if (m.Criminal && phone.Tapped(now)) LcdText("(the line clicks)", "lcd__small");
                    }
                    else if (m.Answer >= 0 && m.Answer < m.Choices.Count) LcdText($"→ {m.Choices[m.Answer]}", "lcd__small");
                    if (!m.Open(now))
                    {
                        var del = m;
                        LcdRow("× Delete", () => { game.Phone.Delete(del); phoneMsg = null; PhoneGo(PhoneScreen.Inbox); });
                    }
                    phoneSoftL.text = m.Open(now) ? "Reply" : "Delete";
                    phoneSoftR.text = "Back";
                    break;
                }
                case PhoneScreen.Contacts:
                {
                    LcdText("CONTACTS", "lcd__title");
                    int hour = game.Clock.Hour;
                    foreach (var c in game.Contacts.All)
                    {
                        if (!game.Contacts.Visible(c, game)) continue;
                        var cc = c;
                        LcdRow($"{ShortName(c.Name)}{(c.OpenAt(hour) ? "" : "  (off)")}", () => { phoneContact = cc; PhoneGo(PhoneScreen.Contact); });
                    }
                    phoneSoftL.text = "Open";
                    phoneSoftR.text = "Back";
                    break;
                }
                case PhoneScreen.Contact:
                {
                    var c = phoneContact;
                    if (c == null) { phoneScreen = PhoneScreen.Contacts; RenderPhone(); return; }
                    LcdText(c.Name, "lcd__title");
                    LcdText($"Around {c.Hours}", "lcd__small");
                    LcdRow("1 Call", () =>
                    {
                        phoneCall = PhoneCalls.Ring(c, game);
                        OSistema.Views.SoundManager.PlayUi(OSistema.Views.AudioSynth.Ring, 0.25f);
                        PhoneGo(PhoneScreen.Call);
                    });
                    LcdRow("2 Show on map", () => { waypoint = c.Position; ToggleWindow(Window.Phone); });
                    phoneSoftL.text = "Select";
                    phoneSoftR.text = "Back";
                    break;
                }
                case PhoneScreen.Call:
                {
                    var call = phoneCall;
                    LcdText(call != null && call.Answered ? "IN CALL" : "CALLING…", "lcd__title");
                    if (call != null)
                    {
                        if (OSistema.Story.Dialect.Show(call.Neapolitan)) LcdText($"“{call.Neapolitan}”", "lcd__nap");
                        if (!string.IsNullOrEmpty(call.English)) LcdText(call.English);
                        for (int i = 0; i < call.Options.Count; i++)
                        {
                            var o = call.Options[i];
                            LcdRow($"{i + 1} {o.label}", () => { o.act(game); PhoneGo(PhoneScreen.Contact); RefreshAll(); });
                        }
                    }
                    phoneSoftL.text = call != null && call.Options.Count > 0 ? "Order" : "";
                    phoneSoftR.text = "Hang up";
                    break;
                }
                case PhoneScreen.Sim:
                {
                    LcdText("SIM CARD", "lcd__title");
                    if (phone.HasBurner(now)) LcdText($"Anonymous SIM: {(phone.S.BurnerUntil - now) / GameClock.TicksPerDay + 1} days left. Nobody can listen.");
                    else LcdText(phone.Tapped(now) ? "Family SIM. The line has been crackling lately…" : "Family SIM, in mamma's name.");
                    LcdRow($"1 New anonymous SIM · €{OSistema.World.Prices.Burner}", () =>
                    {
                        game.Phone.BuyBurner(game, out var err);
                        if (err != null) game.Log.Add(err, MessageKind.Bad);
                        RenderPhone();
                    });
                    phoneSoftL.text = "Buy";
                    phoneSoftR.text = "Back";
                    break;
                }
            }
            if (phoneSel >= phoneItems.Count) phoneSel = Mathf.Max(0, phoneItems.Count - 1);
        }

        static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            return name.Length > 18 ? name.Substring(0, 17) + "…" : name;
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

            int sig = phone.S.NextId * 31 + waiting * 7 + unread * 3 + (phone.HasBurner(now) ? 1 : 0) + phone.InTransit * 13 + game.Clock.Minute;
            foreach (var m in phone.Messages) sig += m.Answer + 2;
            if (sig == phoneSignature) return;
            phoneSignature = sig;
            RenderPhone();
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
