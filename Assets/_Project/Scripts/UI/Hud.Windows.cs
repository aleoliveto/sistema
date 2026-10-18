using System.Linq;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>The business window (B) with its tabs, and the help window (H).</summary>
    public sealed partial class Hud
    {
        enum Window { None, Business, Help, Jobs, Phone }
        enum BizTab { Overview, Crew, Piazze, Pizzo, Supply, Property, Favours, Rivals }

        Window openWindow;
        BizTab bizTab;
        VisualElement businessWindow, helpWindow, bizBody;
        ScrollView bizScroll;
        readonly System.Collections.Generic.Dictionary<BizTab, VisualElement> bizTabButtons = new System.Collections.Generic.Dictionary<BizTab, VisualElement>();
        readonly System.Collections.Generic.Dictionary<BizTab, Label> bizTabStatus = new System.Collections.Generic.Dictionary<BizTab, Label>();
        Label bizTitle, bizBlurb;
        float bizRefreshTimer;
        bool bizDirty = true;

        void BuildWindows()
        {
            // Business
            // A sidebar of sections, each with a one-line status; the open section on the right,
            // headed by what it's for.
            businessWindow = Ui.Box("panel", "window", "window--famiglia").Into(hudLayer);
            var header = Ui.Box("window__header").Into(businessWindow);
            Ui.Text("FAMIGLIA", header, "title", "gold").style.marginRight = 16;
            Ui.Text("Tab to close", header, "tiny", "muted");
            Ui.Box("spacer").Into(header);
            Ui.Btn("✕", () => ToggleWindow(Window.Business), header, "btn--small");

            var split = Ui.Box("famiglia").Into(businessWindow);
            var nav = Ui.Box("famiglia__nav").Into(split);
            foreach (BizTab tab in System.Enum.GetValues(typeof(BizTab)))
            {
                var t = tab;
                var item = Ui.Box("famiglia__item").Into(nav);
                Ui.Text(TabName(t), item, "famiglia__name").pickingMode = PickingMode.Ignore;
                bizTabStatus[t] = Ui.Text("", item, "famiglia__status");
                bizTabStatus[t].pickingMode = PickingMode.Ignore;
                item.RegisterCallback<ClickEvent>(_ => OpenBizTab(t));
                bizTabButtons[t] = item;
            }

            var main = Ui.Box("famiglia__main").Into(split);
            var head = Ui.Box("famiglia__head").Into(main);
            bizTitle = Ui.Text("", head, "famiglia__title");
            bizBlurb = Ui.Text("", head, "small", "muted", "wrap");
            bizScroll = new ScrollView(ScrollViewMode.Vertical).Cls("window__body").Into(main);
            bizBody = bizScroll.contentContainer;
            businessWindow.Show(false);

            // Help
            helpWindow = Ui.Box("panel", "window").Into(hudLayer);
            helpWindow.style.width = 520;
            var hh = Ui.Box("window__header").Into(helpWindow);
            Ui.Text("HOW TO PLAY", hh, "title", "gold");
            Ui.Box("spacer").Into(hh);
            Ui.Btn("✕", () => ToggleWindow(Window.Help), hh, "btn--small");
            var hb = Ui.Box("window__body").Into(helpWindow);
            Ui.Text(
                "<b>Walk</b>  WASD · Shift to run\n" +
                "<b>Use</b>  E: talk, shop, work at a table, do the next step of a lavoretto, eat, sleep\n" +
                "<b>Fight</b>  hold the right mouse button to aim, left click to shoot (or punch, unarmed). Cover halves the chance of being hit. Bodyguards fight on their own.\n" +
                "<b>Where to go</b>  the line under the minimap says what to do next; the gold marker or arrow shows where. M opens the map: click to set a waypoint.\n" +
                "<b>Camera</b>  follows you · arrow keys or middle-drag to look around, F to come back · scroll or + / − to zoom\n" +
                "<b>Screens</b>  B build · Tab famiglia (crew jobs, piazze, pizzo, money) · J taccuino · P phone · M map · H help · Esc menu\n" +
                "<b>Time</b>  Space pause · 1 2 3 speed · debug: 4 ×30, 5 ×120, F8 skip 6 hours\n" +
                "<b>Building</b>  B, pick a tab and an item, click to place, Q / E to turn it, right-click to stop\n" +
                "<b>Save</b>  F5 save · F9 load\n\n" +
                "<color=#A89F8E>Leave the keys alone for a while and Gennaro looks after himself: he eats, sleeps and gets on with his lavoretto. Your crew work the jobs you give them in Famiglia.</color>",
                hb, "wrap");
            helpWindow.Show(false);
        }

        void ToggleWindow(Window w)
        {
            openWindow = openWindow == w ? Window.None : w;
            businessWindow.Show(openWindow == Window.Business);
            helpWindow.Show(openWindow == Window.Help);
            jobsWindow.Show(openWindow == Window.Jobs);
            phoneWindow.Show(openWindow == Window.Phone);
            phoneSignature = -1;
            jobsVersion = -1;
            bizDirty = true;
            if (game != null) RefreshWindows();
        }

        void RefreshWindows()
        {
            RefreshJobs();
            if (openWindow != Window.Business) return;
            foreach (var kv in bizTabButtons) kv.Value.Toggle("famiglia__item--on", kv.Key == bizTab);

            bizRefreshTimer += RefreshSeconds;
            // Don't rebuild under the mouse while someone is painting shifts.
            bool holding = UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;
            if (!holding) paintingShifts = false;
            if (!bizDirty && (bizRefreshTimer < 1f || holding)) return;
            bizRefreshTimer = 0f;
            bizDirty = false;

            var scroll = bizScroll.scrollOffset;
            bizBody.Clear();
            RefreshBizNav();
            bizTitle.text = TabName(bizTab).ToUpper();
            bizBlurb.text = TabBlurb(bizTab);
            switch (bizTab)
            {
                case BizTab.Overview: OverviewTab(); break;
                case BizTab.Crew: CrewTab(); break;
                case BizTab.Piazze: PiazzeTab(); break;
                case BizTab.Pizzo: PizzoTab(); break;
                case BizTab.Supply: SupplyTab(); break;
                case BizTab.Favours: FavoursTab(); break;
                case BizTab.Property: PropertyTab(); break;
                case BizTab.Rivals: RivalsTab(); break;
            }
            bizScroll.scrollOffset = scroll;
        }

        void OpenBizTab(BizTab t)
        {
            bizTab = t;
            bizDirty = true;
            if (bizScroll != null) bizScroll.scrollOffset = Vector2.zero;
            RefreshWindows();
        }

        static string TabName(BizTab t) => t switch
        {
            BizTab.Overview => "Overview",
            BizTab.Crew => "Crew",
            BizTab.Piazze => "Piazze",
            BizTab.Pizzo => "Pizzo",
            BizTab.Supply => "Goods & gear",
            BizTab.Property => "Property & money",
            BizTab.Favours => "Favours",
            _ => "Rivals",
        };

        static string TabBlurb(BizTab t) => t switch
        {
            BizTab.Overview => "How the family is doing, and what needs you now. Click a line to go there.",
            BizTab.Crew => "Who works for you: give each guaglione a job, a bed and shifts. Wages are paid on Sunday.",
            BizTab.Piazze => "Your drug squares: pushers sell, lookouts whistle when the police come. Watch the heat.",
            BizTab.Pizzo => "Shops on your turf pay for protection. A Collector does the rounds, or you go yourself.",
            BizTab.Supply => "What's in the basso: doses, cigarettes, tools, scooters and guns.",
            BizTab.Property => "Dirty and clean money, homes, fronts and laundering.",
            BizTab.Favours => "The neighbourhood: who owes you, who trusts you, and what you can ask for.",
            _ => "The other clan: how angry they are and where they sell.",
        };

        /// <summary>The one-line status under each section name.</summary>
        void RefreshBizNav()
        {
            var org = game.Org;
            int crew = 0, idle = 0;
            foreach (var c in game.Pawns.Crew)
            {
                if (c.Dead || c.Controlled) continue;
                crew++;
                if (c.Role == CrewRole.General) idle++;
            }
            int paying = org.Businesses.Count(b => b.Status == BusinessStatus.Paying);
            var problems = Problems();
            SetStatus(BizTab.Overview, problems.Count == 0 ? "<color=#8FD18A>all quiet</color>" : $"<color=#F2B866>{problems.Count} to look at</color>");
            SetStatus(BizTab.Crew, crew == 0 ? "just you" : $"{crew} guagliun{(crew == 1 ? "e" : "i")}" + (idle > 0 ? $" · <color=#F2B866>{idle} without a job</color>" : ""));
            SetStatus(BizTab.Piazze, org.Squares.Count == 0 ? "none yet" : $"{org.Squares.Count} open");
            SetStatus(BizTab.Pizzo, paying == 0 ? "nobody pays yet" : $"{paying} paying");
            SetStatus(BizTab.Supply, $"{game.Stock.Product} doses · {game.Stock.Cigarettes} packs");
            SetStatus(BizTab.Property, $"€{game.Stock.Cash} dirty · €{game.Stock.Clean} clean");
            SetStatus(BizTab.Favours, "the neighbours");
            SetStatus(BizTab.Rivals, game.Rival.Destroyed ? "destroyed" : game.Rival.Hostility >= 70f ? "<color=#F07A6E>out for blood</color>" : game.Rival.Hostility >= 40f ? "<color=#F2B866>angry</color>" : "watching");
        }

        void SetStatus(BizTab t, string text)
        {
            if (bizTabStatus.TryGetValue(t, out var l)) l.text = text;
        }

        /// <summary>Things that need the boss, most urgent first, each pointing at the section that fixes it.</summary>
        System.Collections.Generic.List<(string text, BizTab tab, bool bad)> Problems()
        {
            var list = new System.Collections.Generic.List<(string, BizTab, bool)>();
            var org = game.Org;
            var st = game.Stock;
            int crew = 0, idle = 0, hungry = 0, beds = game.Map.PositionsOf(BuildingDefs.Bed).Count(b => game.PlayerBase.Contains(b) || game.Properties.IsOwnedHome(b));
            bool pusher = false, lookout = false, collector = false;
            foreach (var c in game.Pawns.Crew)
            {
                if (c.Dead) continue;
                crew++;
                if (c.Controlled) continue;
                if (c.Role == CrewRole.General) idle++;
                if (c.Needs.Hungry) hungry++;
                pusher |= c.Role == CrewRole.Pusher;
                lookout |= c.Role == CrewRole.Lookout;
                collector |= c.Role == CrewRole.Collector;
            }
            int wages = org.WeeklyWageBill(game);
            if (st.Cash + st.Clean < wages) list.Add(($"Not enough money for Sunday's wages (€{wages}). Unpaid crew lose loyalty.", BizTab.Property, true));
            if (org.Evidence >= 60f) list.Add(($"Evidence against you is at {org.Evidence:0}/100. Lie low, launder, keep your hands clean.", BizTab.Property, true));
            if (beds < crew) list.Add(($"{crew - beds} of the crew {(crew - beds == 1 ? "has" : "have")} no bed. Buy one at the Mobilificio and put it down at home.", BizTab.Crew, true));
            if (hungry > 0) list.Add(($"{hungry} of the crew {(hungry == 1 ? "is" : "are")} hungry. Groceries and a stove, or pizza money.", BizTab.Supply, true));
            if (idle > 0) list.Add(($"{idle} of the crew {(idle == 1 ? "has" : "have")} no job. Give them a role.", BizTab.Crew, false));
            if (org.Squares.Count > 0 && !pusher) list.Add(("Your piazza has no pusher: nobody is selling.", BizTab.Crew, true));
            if (org.Squares.Count > 0 && !lookout) list.Add(("No lookout: nobody will whistle when the police come.", BizTab.Crew, false));
            if (org.Squares.Count > 0 && st.Product <= 0) list.Add(("No doses left to sell. Buy from the supplier and bag them.", BizTab.Supply, false));
            foreach (var sq in org.Squares)
            {
                var zone = sq.ZoneId >= 0 && sq.ZoneId < game.Territory.Zones.Count ? game.Territory.Zones[sq.ZoneId] : null;
                if (zone != null && zone.Heat >= 60f) list.Add(($"The police are watching {zone.Name} (heat {zone.Heat:0}). Close it for a while, or add lookouts.", BizTab.Piazze, true));
            }
            int due = org.Businesses.Count(b => b.Status == BusinessStatus.Paying && org.IsDue(b, Organization.WeekOf(game.Clock)));
            if (due > 0 && !collector) list.Add(($"{due} shop{(due == 1 ? "" : "s")} owe this week's pizzo and nobody collects. Go yourself, or name a Collector.", BizTab.Pizzo, false));
            if (!game.Rival.Destroyed && game.Rival.Hostility >= 70f) list.Add(("The rivals are planning a hit. Arm the crew, keep bodyguards close.", BizTab.Rivals, true));
            return list;
        }

        void OverviewTab()
        {
            var st = game.Stock;
            var org = game.Org;
            var cards = Ui.Row(bizBody);
            cards.style.flexWrap = Wrap.Wrap;
            void Card(string label, string value, string note)
            {
                var c = Ui.Box("stat-card").Into(cards);
                Ui.Text(label, c, "tiny", "muted");
                Ui.Text(value, c, "stat-card__value");
                Ui.Text(note, c, "tiny", "muted", "wrap");
            }
            Card("CASH", $"€{st.Cash}", $"+ €{st.Clean} clean");
            Card("WAGES ON SUNDAY", $"€{org.WeeklyWageBill(game)}", "mesata and family support");
            Card("RESPECT", $"{org.Respect:0}", "opens doors, scares shopkeepers");
            Card("EVIDENCE", $"{org.Evidence:0}/100", "at 100, a warrant");

            Ui.Text("NEEDS YOU", bizBody, "section");
            var problems = Problems();
            if (problems.Count == 0) Ui.Text("Nothing urgent. Go and make some money.", bizBody, "good");
            foreach (var (text, tab, bad) in problems)
            {
                var row = Ui.Box("attention").Into(bizBody);
                row.AddToClassList(bad ? "attention--bad" : "attention--warn");
                Ui.Text(text, row, "wrap", "grow").pickingMode = PickingMode.Ignore;
                Ui.Text($"{TabName(tab)} ›", row, "small", "gold").pickingMode = PickingMode.Ignore;
                var t = tab;
                row.RegisterCallback<ClickEvent>(_ => OpenBizTab(t));
            }

            Ui.Text("WHAT YOU'RE WORKING ON", bizBody, "section");
            Ui.Text(Objectives.Current(game, null).Text ?? "Nothing in particular.", bizBody, "wrap");
            Ui.Text("Lavoretti from contacts are in the Taccuino (J). Shops and contacts are on the map (M).", bizBody, "small", "muted", "wrap");
        }

        /// <summary>Runs an action from a window button and redraws the window straight away.</summary>
        System.Action Do(System.Action a) => () =>
        {
            a();
            bizDirty = true;
            RefreshWindows();
        };

        // ---------- Tabs ----------

        void CrewTab()
        {
            var org = game.Org;
            Ui.Text($"Dirty cash <b>€{game.Stock.Cash}</b> · weekly mesata and family support <b>€{org.WeeklyWageBill(game)}</b>, paid Sunday at 12:00", bizBody, "wrap");
            long now = game.Ticks.TicksGame;

            // The shifts: paint each person's day, hour by hour, like a timetable.
            var brushes = Ui.Row(bizBody);
            brushes.style.marginTop = 6;
            Ui.Text("SHIFTS  ", brushes, "section");
            foreach (var (shift, label) in new[] { (Pawn.Shift.Work, "Work"), (Pawn.Shift.Sleep, "Sleep"), (Pawn.Shift.Free, "Free time") })
            {
                var sh = shift;
                var b = Ui.Btn(label, Do(() => shiftBrush = sh), brushes, "btn--small", ShiftClass(shift));
                b.Toggle("btn--on", shiftBrush == shift);
            }
            Ui.Text("  pick a brush, then click or drag across the hours", brushes, "small", "muted");
            var hours = Ui.Row(bizBody);
            Ui.Box().Into(hours).style.width = 182;
            for (int h = 0; h < 24; h++) Ui.Text(h % 3 == 0 ? h.ToString() : "", hours, "tiny", "shift-hour");
            foreach (var p in game.Pawns.Crew.Where(p => !p.Dead && !p.Controlled))
            {
                var trow = Ui.Row(bizBody);
                Ui.Text(p.DisplayName, trow, "small").style.width = 182;
                var pawn = p;
                for (int h = 0; h < 24; h++)
                {
                    int hour = h;
                    var cell = Ui.Box("shift-cell", ShiftClass(p.Schedule[h])).Into(trow);
                    if (h == game.Clock.Hour) cell.AddToClassList("shift-cell--now");
                    void Paint()
                    {
                        pawn.Schedule[hour] = shiftBrush;
                        cell.ClearClassList();
                        cell.AddToClassList("shift-cell");
                        cell.AddToClassList(ShiftClass(shiftBrush));
                    }
                    cell.RegisterCallback<PointerDownEvent>(_ => { paintingShifts = true; Paint(); });
                    cell.RegisterCallback<PointerEnterEvent>(_ => { if (paintingShifts && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed) Paint(); });
                }
            }
            Ui.Text("CREW", bizBody, "section");
            foreach (var p in game.Pawns.Crew.Where(p => !p.Dead))
            {
                var row = Ui.Box("table-row").Into(bizBody);
                Avatar(p, row, big: false);
                var names = Ui.Box("col").Into(row);
                names.style.width = 150;
                Ui.Text(p.IsBoss ? $"<color=#E8C26A>{p.DisplayName}</color>" : $"<b>{p.DisplayName}</b>", names);
                Ui.Text(ShortStatus(p, now), names, "small", "muted");
                var pawn = p;
                Button roleBtn = null;
                roleBtn = Ui.Btn(RoleLabel(p.Role) + "  ▾", () => ShowRoleMenu(pawn, roleBtn), row, "btn--small");
                roleBtn.style.width = 150;
                roleBtn.SetEnabled(!p.Controlled);
                Button bedBtn = null;
                bedBtn = Ui.Btn(BedLabel(p), () => ShowBedMenu(pawn, bedBtn), row, "btn--small");
                bedBtn.style.width = 80;
                Ui.Btn("−", Do(() => pawn.Wage = Mathf.Max(0, pawn.Wage - 10)), row, "btn--small");
                Ui.Text($"€{p.Wage}", row).style.width = 44;
                Ui.Btn("+", Do(() => pawn.Wage = Mathf.Min(1000, pawn.Wage + 10)), row, "btn--small");
                Ui.Text($"Loyalty {p.Loyalty:0}", row, "small", p.Loyalty < 30 ? "bad" : "muted").style.width = 80;
                Ui.Btn(p.Armed ? "Disarm" : "Arm", Do(() => ToggleArmed(pawn)), row, "btn--small", p.Armed ? "btn--on" : "x");
            }
            var r = Ui.Row(bizBody);
            r.style.marginTop = 8;
            Ui.Btn($"Recruit a guaglione (€{Prices.RecruitBonus})", Do(() =>
            {
                if (game.Org.Recruit(game, out var error) == null) game.Log.Add(error, MessageKind.Bad);
            }), r);
            if (!game.Pawns.Crew.Any(p => p.IsPresent && p.Role == CrewRole.Collector))
                Ui.Text("Nobody is collecting pizzo. Give someone the Collector role.", bizBody, "bad", "small");
        }

        Pawn.Shift shiftBrush = Pawn.Shift.Work;
        bool paintingShifts;

        static string ShiftClass(Pawn.Shift s) => s == Pawn.Shift.Work ? "shift--work" : s == Pawn.Shift.Sleep ? "shift--sleep" : "shift--free";

        void PiazzeTab()
        {
            var org = game.Org;
            if (org.Squares.Count == 0)
            {
                Ui.Text("No piazze yet. Open the CRIME tab at the bottom, pick Piazza and click a sidewalk.", bizBody, "muted", "wrap");
                return;
            }
            int pushers = game.Pawns.Crew.Count(p => p.IsPresent && p.Role == CrewRole.Pusher);
            int lookouts = game.Pawns.Crew.Count(p => p.IsPresent && p.Role == CrewRole.Lookout);
            if (pushers == 0) Ui.Text("No pushers assigned: nobody sells. Set roles in the Crew tab.", bizBody, "bad", "small");
            if (lookouts == 0) Ui.Text("No lookouts: the police will walk right up to the spot.", bizBody, "warn", "small");

            foreach (var sq in org.Squares)
            {
                var zone = game.Territory.Zones[sq.ZoneId];
                Ui.Text(zone.Name.ToUpper() + (game.Police.IsRaiding(sq) ? "  ·  RAID" : ""), bizBody, "section");
                var (hv, hf) = Ui.Bar("Heat", new Color(0.86f, 0.3f, 0.26f), bizBody);
                hv.text = $"{zone.Heat:0}";
                Ui.SetFill(hf, zone.Heat / 100f);
                Ui.Text($"Today: {sq.SalesToday} sold (€{sq.RevenueToday}) · {sq.LostToday} customers lost · demand {zone.Demand:0.0}" +
                        (game.Rival.NearRivalTerritory(sq) ? " · <color=#F2B866>competing with the rivals</color>" : ""), bizBody, "small", "wrap");
                var hours = Ui.Row(bizBody);
                Ui.Text($"Open {sq.HoursLabel}", hours).style.width = 150;
                var square = sq;
                Ui.Text("opens", hours, "small", "muted");
                Ui.Btn("−", Do(() => square.OpenHour = (square.OpenHour + 23) % 24), hours, "btn--small");
                Ui.Btn("+", Do(() => square.OpenHour = (square.OpenHour + 1) % 24), hours, "btn--small");
                Ui.Text("  closes", hours, "small", "muted");
                Ui.Btn("−", Do(() => square.CloseHour = (square.CloseHour + 23) % 24), hours, "btn--small");
                Ui.Btn("+", Do(() => square.CloseHour = (square.CloseHour + 1) % 24), hours, "btn--small");
                Ui.Box("spacer").Into(hours);
                Ui.Btn("Show", () => FocusCamera(Mover.CellCenter(square.Cell)), hours, "btn--small");
            }
        }

        void PizzoTab()
        {
            var org = game.Org;
            int weekly = org.Businesses.Where(b => b.Status == BusinessStatus.Paying).Sum(b => b.WeeklyPizzo);
            Ui.Text($"Paying businesses bring in <b>€{weekly}</b> a week. You can only squeeze the shops on your own turf ({string.Join(", ", org.Turf(game).Select(z => z.Name))}); the rest pay someone else, or nobody yet.", bizBody, "wrap");
            foreach (var b in org.Businesses)
            {
                var row = Ui.Box("table-row").Into(bizBody);
                var zone = game.Territory.ZoneAt(b.Cell);
                var col = Ui.Box("col").Into(row);
                col.style.width = 250;
                Ui.Text($"<b>{b.Name}</b>", col);
                Ui.Text(zone?.Name ?? "", col, "small", "muted");
                string status = b.Status == BusinessStatus.Closed
                    ? $"Burned · reopens in {(b.ClosedUntil - game.Ticks.TicksGame) / GameClock.TicksPerDay + 1}d"
                    : b.StatusLabel;
                Ui.Text(status, row, "small", b.Status == BusinessStatus.Paying ? "good" : b.Status == BusinessStatus.Refusing ? "bad" : "muted").style.width = 170;
                var biz = b;
                string blocker = b.Status == BusinessStatus.Free ? org.PizzoBlocker(b, game) : null;
                if (blocker != null) Ui.Text(blocker, row, "small", "muted");
                else if (b.Status == BusinessStatus.Free) Ui.Btn($"Impose €{b.WeeklyPizzo}", Do(() => org.Target(biz, game)), row, "btn--small");
                if (b.Status == BusinessStatus.Refusing)
                {
                    Ui.Btn("Ask again", Do(() => org.Target(biz, game)), row, "btn--small");
                    Ui.Btn("Scare", Do(() => org.OrderRetaliation(biz, Retaliation.Scare, game)), row, "btn--small", "btn--danger");
                    Ui.Btn("Burn", Do(() => org.OrderRetaliation(biz, Retaliation.Burn, game)), row, "btn--small", "btn--danger");
                }
                Ui.Box("spacer").Into(row);
                Ui.Btn("Show", () => FocusCamera(Mover.CellCenter(biz.Cell)), row, "btn--small");
            }
        }

        void SupplyTab()
        {
            var org = game.Org;
            var st = game.Stock;
            Ui.Text($"Dirty <b>€{st.Cash}</b> · clean <b>€{st.Clean}</b>. Drugs, guns and small things can be paid in cash; anything from €{Prices.CleanMoneyThreshold} up (and every property) needs clean money.", bizBody, "wrap");

            Ui.Text("LAUNDERING", bizBody, "section");
            Ui.Text(org.FrontCount > 0
                ? $"Your {org.FrontCount} front(s) wash up to <b>€{org.WeeklyLaunderingCapacity}</b> every Sunday for a {Organization.FrontLaunderingFee:P0} cut."
                : "Buy a shop to use it as a front: it washes dirty money every week for a small cut.", bizBody, "small", "wrap");
            Ui.Text($"The commercialista does it today, for {Organization.CommercialistaFee:P0}, and every middleman adds to the evidence.", bizBody, "small", "muted", "wrap");
            var lr = Ui.Row(bizBody);
            foreach (int amount in new[] { 500, 2000, 5000 })
            {
                int a = amount;
                var b = Ui.Btn($"Wash €{a} → €{Mathf.RoundToInt(a * (1f - Organization.CommercialistaFee))}", Do(() =>
                {
                    if (!org.LaunderViaCommercialista(a, game, out var error)) game.Log.Add(error, MessageKind.Bad);
                }), lr, "btn--small");
                b.SetEnabled(st.Cash >= a);
            }

            Ui.Text("PRODUCT", bizBody, "section");
            Ui.Text($"Doses ready: <b>{st.Product}</b> · to cut: <b>{st.BulkDg / 10f:0.0} g</b> · bags: {st.Baggies} · street price €{Prices.ProductStreet} a dose · honesty of the doses {st.DoseQuality * 100f:0}%", bizBody, "small", "wrap");
            if (org.CarriedDg > 0) Ui.Text($"{org.CarriedDg / 10} g on the boss, not yet home.", bizBody, "warn", "small");
            if (!org.SupplierIntroduced)
                Ui.Text($"Nobody sells product to a stranger. {org.SupplierProgress}", bizBody, "warn", "small", "wrap");
            else
            {
                var tore = game.Contacts.ByName(ContactBook.Supplier);
                var row = Ui.Row(bizBody);
                Ui.Text($"Buy by the etto, half kilo or kilo from {ContactBook.Supplier}, in person, {tore?.Hours}. Cut it into doses at a bagging table (E), or give someone the Bagger job.", row, "small", "wrap").style.flexShrink = 1;
                if (tore != null) Ui.Btn("Pin on map", () => FocusCamera(tore.Position), row, "btn--small");
            }

            Ui.Text("ROSSE · CIGARETTES", bizBody, "section");
            Ui.Text($"Packs ready: <b>{st.Cigarettes}</b> (quality {st.PackQuality * 100f:0}%, about €{org.PackPrice(game)} each) · tobacco {st.TobaccoG} g · papers {st.Papers} · empty packs {st.EmptyPacks}", bizBody, "small", "wrap");
            Ui.Text("Tobacco and papers from the tabaccaio, empty packs from Nanninella. Roll them at a rolling table (E), or give someone the Roller job. Zio Totonno buys up to 20 packs a day; a deposito sells many more.", bizBody, "small", "muted", "wrap");
            Ui.Text("GEAR", bizBody, "section");
            Ui.Text($"{(st.DigitalScale ? "✓" : "✗")} digital scale   {(st.HeatSealer ? "✓" : "✗")} heat sealer   {(st.RollingMachine ? "✓" : "✗")} rolling machine   · Nanninella sells them.", bizBody, "small");

            Ui.Text("SCOOTERS", bizBody, "section");
            Ui.Text($"The family has <b>{st.Scooters}</b>. Crew take a free scooter for any long trip: far faster across the city.", bizBody, "small", "wrap");
            Ui.Btn($"Buy a scooter (€{Prices.Scooter})", Do(() =>
            {
                if (!org.Pay(Prices.Scooter, game, out var error)) game.Log.Add(error, MessageKind.Bad);
                else
                {
                    st.Scooters++;
                    game.Log.Add("A second-hand Liberty, no questions asked. It's parked outside the basso.", MessageKind.Good);
                }
            }), bizBody, "btn--small").style.alignSelf = Align.FlexStart;

            Ui.Text("ARMS", bizBody, "section");
            Ui.Text($"Pistols in the stash: <b>{st.Pistols}</b>. Armed crew defend themselves and fight when drafted, but get longer sentences if arrested.", bizBody, "small", "wrap");
            Ui.Btn($"Buy a pistol (€{Prices.Pistol})", Do(() =>
            {
                if (!org.RequireRank(1, "Buying guns", game, out var rankError)) game.Log.Add(rankError, MessageKind.Bad);
                else if (!org.Pay(Prices.Pistol, game, out var error)) game.Log.Add(error, MessageKind.Bad);
                else
                {
                    st.Pistols++;
                    game.Log.Add("A pistol, wrapped in a cloth, now sits in the stash.");
                }
            }), bizBody, "btn--small").style.alignSelf = Align.FlexStart;
        }

        void RivalsTab()
        {
            var rival = game.Rival;
            Ui.Text(rival.Name.ToUpper(), bizBody, "section");
            if (rival.Destroyed)
            {
                Ui.Text("Destroyed. The district is yours for the taking.", bizBody, "good");
                return;
            }
            Ui.Text($"Boss: {rival.BossName} · soldiers: {rival.Soldiers} · piazze: {rival.Squares.Count}", bizBody);
            var (hv, hf) = Ui.Bar("Hostility", new Color(0.95f, 0.55f, 0.25f), bizBody);
            hv.text = $"{rival.Hostility:0}";
            Ui.SetFill(hf, rival.Hostility / 100f);
            string mood = rival.InTruce(game) ? $"Truce until day {rival.TruceUntilDay}."
                : rival.Hostility >= RivalClan.HitHostility ? "<color=#F07A6E>They want blood: ambushes are likely.</color>"
                : rival.Hostility >= RivalClan.StesaHostility ? "<color=#F2B866>Angry: expect shots over your piazze.</color>"
                : "Watching you.";
            Ui.Text(mood, bizBody, "wrap");
            Ui.Text("Your piazze near their territory anger them every day. To take a piazza by force, draft armed crew and right-click their pusher. Every soldier you kill is one they can't replace.", bizBody, "small", "muted", "wrap");
            Ui.Btn("Send a gift (€500, hostility −25)", Do(() =>
            {
                if (!rival.SendGift(500, 25f, game)) game.Log.Add("Not enough cash for a gift.", MessageKind.Bad);
            }), bizBody).style.alignSelf = Align.FlexStart;
            foreach (var sq in rival.Squares)
            {
                var row = Ui.Box("table-row").Into(bizBody);
                Ui.Text($"Their piazza in {game.Territory.Zones[sq.ZoneId].Name}", row).style.width = 320;
                var square = sq;
                Ui.Btn("Show", () => FocusCamera(Mover.CellCenter(square.Cell)), row, "btn--small");
            }
        }
    }
}
