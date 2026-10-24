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
        enum BizTab { Overview, Crew, Organigram, Piazze, Pizzo, Supply, Property, Favours, Rivals, Rackets }

        Window openWindow;
        BizTab bizTab;
        VisualElement businessWindow, helpWindow, bizBody;
        ScrollView bizScroll;
        readonly System.Collections.Generic.Dictionary<BizTab, VisualElement> bizTabButtons = new System.Collections.Generic.Dictionary<BizTab, VisualElement>();
        readonly System.Collections.Generic.Dictionary<BizTab, Label> bizTabStatus = new System.Collections.Generic.Dictionary<BizTab, Label>();
        Label bizTitle, bizBlurb, famSubtitle;
        VisualElement bizNav;
        float bizRefreshTimer;
        bool bizDirty = true;

        void BuildWindows()
        {
            // Famiglia: a sidebar down the left, so the city stays in view while you run the family.
            // Sections as tabs along the top; their order follows the family's rank.
            businessWindow = Ui.Box("panel", "sidebar", "fit").Into(hudLayer);
            var header = Ui.Box("window__header", "panel-head").Into(businessWindow);
            famIcon = new Icon(IconKind.Business, 30).Into(header);
            famIcon.AddToClassList("panel-head__icon");
            var titles = Ui.Box("col", "grow").Into(header);
            famTitle = Ui.Text("FAMIGLIA", titles, "panel-head__title");
            famSubtitle = Ui.Text("", titles, "panel-head__tagline");
            Ui.Btn("✕", () => ToggleWindow(Window.Business), header, "btn--small");
            Tip(header, () => "Esc to close");

            bizNav = Ui.Box("sidebar__nav").Into(businessWindow);
            foreach (BizTab tab in System.Enum.GetValues(typeof(BizTab)))
            {
                var t = tab;
                var item = Ui.Box("sidebar__tab").Into(bizNav);
                bizTabStatus[t] = Ui.Text(TabName(t), item, "sidebar__tabname");
                item.RegisterCallback<ClickEvent>(_ => OpenBizTab(t));
                Tip(item, () => TabStatusText(t));
                bizTabButtons[t] = item;
            }

            var main = Ui.Box("famiglia__main", "fit").Into(businessWindow);
            var head = Ui.Box("famiglia__head").Into(main);
            bizTitle = Ui.Text("", head, "famiglia__title");
            bizBlurb = Ui.Text("", head, "small", "muted", "wrap");
            bizScroll = new ScrollView(ScrollViewMode.Vertical).Cls("window__body").Into(main);
            bizScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            bizBody = bizScroll.contentContainer;
            businessWindow.Show(false);

            // Help
            helpWindow = Ui.Box("panel", "window", "fit").Into(hudLayer);
            helpWindow.style.width = 520;
            var hh = Ui.Box("window__header").Into(helpWindow);
            Ui.Text("HOW TO PLAY", hh, "title", "gold");
            Ui.Box("spacer").Into(hh);
            Ui.Btn("✕", () => ToggleWindow(Window.Help), hh, "btn--small");
            var hb = Ui.Scroll(helpWindow, "window__body");
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
            if (searchTyping) SetSearchTyping(false);
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
            foreach (var kv in bizTabButtons) kv.Value.Toggle("sidebar__tab--on", kv.Key == bizTab);

            bizRefreshTimer += RefreshSeconds;
            // Don't rebuild under the mouse while someone is painting shifts.
            bool holding = UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;
            if (!bizDirty && (bizRefreshTimer < 1f || holding)) return;
            bizRefreshTimer = 0f;
            bizDirty = false;

            var scroll = bizScroll.scrollOffset;
            bizBody.Clear();
            RefreshBizNav();
            RefreshNavOrder();
            bizTitle.text = TabTitle(bizTab).ToUpper();
            bizBlurb.text = TabBlurb(bizTab);
            bool detail = bizTab == BizTab.Crew && famPerson != null;
            bizTitle.parent.Show(!detail);
            businessWindow.Toggle("sidebar--wide", detail);
            switch (bizTab)
            {
                case BizTab.Overview: if (BossView) DashboardTab(); else OverviewTab(); break;
                case BizTab.Crew:
                    if (detail)
                    {
                        // The roster stays on the left, the person's page opens beside it.
                        var cols = Ui.Row(bizBody);
                        cols.style.alignItems = Align.FlexStart;
                        var left = Ui.Box("col", "roster-col").Into(cols);
                        var right = Ui.Box("col", "detail-col").Into(cols);
                        var keep = bizBody;
                        bizBody = left;
                        RosterTab();
                        bizBody = right;
                        PersonDetail(famPerson);
                        bizBody = keep;
                    }
                    else RosterTab();
                    break;
                case BizTab.Organigram: OrganigramTab(); break;
                case BizTab.Piazze: PiazzeTab(); break;
                case BizTab.Pizzo: PizzoTab(); break;
                case BizTab.Supply: SupplyTab(); break;
                case BizTab.Favours: FavoursTab(); break;
                case BizTab.Property: PropertyTab(); break;
                case BizTab.Rivals: RivalsTab(); break;
                case BizTab.Rackets: RacketsTab(); break;
            }
            bizScroll.scrollOffset = scroll;
        }

        void OpenBizTab(BizTab t)
        {
            if (t != BizTab.Crew || bizTab == BizTab.Crew) famPerson = null;
            bizTab = t;
            bizDirty = true;
            if (bizScroll != null) bizScroll.scrollOffset = Vector2.zero;
            RefreshWindows();
        }

        string TabName(BizTab t) => t switch
        {
            BizTab.Overview => BossView ? "Dashboard" : "Overview",
            BizTab.Crew => "Crew",
            BizTab.Organigram => "Organigram",
            BizTab.Piazze => "Piazze",
            BizTab.Pizzo => "Pizzo",
            BizTab.Supply => "Stash & guns",
            BizTab.Property => "Money & property",
            BizTab.Favours => "Neighbours",
            BizTab.Rackets => "Rackets",
            _ => "Clans",
        };

        static string TabBlurb(BizTab t) => t switch
        {
            BizTab.Rackets => "The street work and the rackets: from minding cars and a stall of ROSSE to the lotto, the book on Napoli, loans and machines.",
            BizTab.Overview => "How the family is doing, and what needs you now. Click a line to go there.",
            BizTab.Crew => "Everyone in the family. Click someone to set their job, wage and home.",
            BizTab.Organigram => "Who answers to whom. Each piazza has a capo paranza who runs its pushers and lookouts, day and night.",
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
            SetStatus(BizTab.Organigram, org.Squares.Count == 0 ? "no piazze yet" : $"{org.Squares.Count} piazz{(org.Squares.Count == 1 ? "a" : "e")}");
            SetStatus(BizTab.Overview, problems.Count == 0 ? "<color=#8FD18A>all quiet</color>" : $"<color=#F2B866>{problems.Count} to look at</color>");
            SetStatus(BizTab.Crew, crew == 0 ? "just you" : $"{crew} guagliun{(crew == 1 ? "e" : "i")}" + (idle > 0 ? $" · <color=#F2B866>{idle} without a job</color>" : ""));
            SetStatus(BizTab.Piazze, org.Squares.Count > 0 ? $"{org.Squares.Count} open"
                : org.Sav.feud || org.Sav.dead ? "<color=#F07A6E>at war with Don Saverio</color>"
                : org.SaverioContact(game) != null && !org.Sav.taken ? "Don Saverio holds the corner" : "none yet");
            SetStatus(BizTab.Pizzo, paying == 0 ? "nobody pays yet" : $"{paying} paying");
            SetStatus(BizTab.Supply, $"{game.Stock.Product} doses · {game.Stock.Cigarettes} packs");
            SetStatus(BizTab.Property, $"€{game.Stock.Cash} dirty · €{game.Stock.Clean} clean");
            SetStatus(BizTab.Favours, "the neighbours");
            SetStatus(BizTab.Rackets, (org.H.lotto ? "lotto · " : "") + (org.H.betting ? "book · " : "") + $"{org.H.loans.Count} loans · {org.H.poker} machines");
            int clansLeft = game.Clans.Count(c => !c.Destroyed);
            var angry = AngriestClan();
            SetStatus(BizTab.Rivals, clansLeft == 0 ? "the city is yours" : $"{clansLeft} clans · " + (angry.Hostility >= 70f ? "<color=#F07A6E>one wants blood</color>" : angry.Hostility >= 40f ? "<color=#F2B866>one is angry</color>" : "watching"));
        }

        readonly System.Collections.Generic.Dictionary<BizTab, string> bizStatusText = new System.Collections.Generic.Dictionary<BizTab, string>();

        void SetStatus(BizTab t, string text) => bizStatusText[t] = text;

        string TabStatusText(BizTab t) => bizStatusText.TryGetValue(t, out var s) ? $"{TabTitle(t)}: {s}" : TabTitle(t);

        /// <summary>Things that need the boss, most urgent first, each pointing at the section that fixes it.</summary>
        System.Collections.Generic.List<(string text, BizTab tab, bool bad)> Problems()
        {
            var list = new System.Collections.Generic.List<(string, BizTab, bool)>();
            var org = game.Org;
            var st = game.Stock;
            int crew = 0, idle = 0, hungry = 0;
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
            if (org.SaverioCutRunning && st.Cash + st.Clean < wages + Organization.SaverioCutWeekly)
                list.Add(($"Sunday's cut for Don Saverio (€{Organization.SaverioCutWeekly}) isn't there. Miss two and he takes the corner back.", BizTab.Piazze, true));
            if (st.Cash + st.Clean < wages) list.Add(($"Not enough money for Sunday's wages (€{wages}). Unpaid crew lose loyalty.", BizTab.Property, true));
            if (org.Evidence >= 60f) list.Add(($"Evidence against you is at {org.Evidence:0}/100. Lie low, launder, keep your hands clean.", BizTab.Property, true));
            // Beds are shared by a house's day and night shifts: short only where more sleep at once than there are beds.
            foreach (var house in Housing.Houses(game))
            {
                int shortBy = Housing.BedsShort(house, game);
                if (shortBy > 0) list.Add(($"{Housing.Name(house)} is {shortBy} bed{(shortBy == 1 ? "" : "s")} short at night. Buy one at the Mobilificio, or move someone to another house.", BizTab.Crew, true));
            }
            if (hungry > 0) list.Add(($"{hungry} of the crew {(hungry == 1 ? "is" : "are")} hungry. Groceries and a stove, or pizza money.", BizTab.Supply, true));
            if (idle > 0) list.Add(($"{idle} of the crew {(idle == 1 ? "has" : "have")} no job. Give them a role.", BizTab.Crew, false));
            if (org.Squares.Count > 0 && !pusher) list.Add(("Your piazza has no pusher: nobody is selling.", BizTab.Organigram, true));
            if (org.Squares.Count > 0 && !lookout) list.Add(("No lookout: nobody will whistle when the police come.", BizTab.Crew, false));
            int noCapo = org.PiazzeWithoutCapo(game);
            if (noCapo > 0) list.Add(($"{noCapo} piazz{(noCapo == 1 ? "a has" : "e have")} no capo paranza: nobody runs a night team there.", BizTab.Organigram, false));
            if (org.Squares.Count > 0 && st.Product <= 0) list.Add(("No doses left to sell. Buy from the supplier and bag them.", BizTab.Supply, false));
            foreach (var sq in org.Squares)
            {
                var zone = sq.ZoneId >= 0 && sq.ZoneId < game.Territory.Zones.Count ? game.Territory.Zones[sq.ZoneId] : null;
                if (zone != null && zone.Heat >= 60f) list.Add(($"The police are watching {zone.Name} (heat {zone.Heat:0}). Close it for a while, or add lookouts.", BizTab.Piazze, true));
            }
            int due = org.Businesses.Count(b => b.Status == BusinessStatus.Paying && org.IsDue(b, Organization.WeekOf(game.Clock)));
            if (due > 0 && !collector) list.Add(($"{due} shop{(due == 1 ? "" : "s")} owe this week's pizzo and nobody collects. Go yourself, or name a Collector.", BizTab.Pizzo, false));
            foreach (var c in game.Clans)
                if (!c.Destroyed && c.Hostility >= 70f) list.Add(($"The {c.Name} want blood. Arm the crew, keep bodyguards and lookouts on your piazze.", BizTab.Rivals, true));
            return list;
        }

        /// <summary>Runs an action from a window button and redraws the window straight away.</summary>
        System.Action Do(System.Action a) => () =>
        {
            a();
            bizDirty = true;
            RefreshWindows();
        };

        // ---------- Tabs ----------


        void PiazzeTab()
        {
            var org = game.Org;
            SaverioSection();
            if (org.Squares.Count == 0)
            {
                Ui.Text("No piazze yet. Take Don Saverio's corner, or open Build (B) and use Open piazza on a pavement in your turf.", bizBody, "muted", "wrap");
                return;
            }
            int pushers = game.Pawns.Crew.Count(p => p.IsPresent && p.Role == CrewRole.Pusher);
            int lookouts = game.Pawns.Crew.Count(p => p.IsPresent && p.Role == CrewRole.Lookout);
            if (pushers == 0) Ui.Text("No pushers: nobody sells. Add them in Famiglia › Organigram.", bizBody, "bad", "small");
            if (lookouts == 0) Ui.Text("No lookouts: the police will walk right up to the spot.", bizBody, "warn", "small");

            foreach (var sq in org.Squares)
            {
                var why = org.WhyNotHeld(game.Territory.Zones[sq.ZoneId], game);
                if (why != null) Ui.Text($"{game.Territory.Zones[sq.ZoneId].Name} isn't your turf yet: {why}.", bizBody, "warn", "small", "wrap");
            }

            // One line per piazza, so ten of them still fit.
            var head = Ui.Box("pz-row", "pz-row--head").Into(bizBody);
            foreach (var (t, cls) in new[] { ("RIONE", "pz-name"), ("HOURS", "pz-hours"), ("HEAT", "pz-heat"), ("TODAY", "pz-today"), ("CREW", "pz-crew"), ("", "pz-go") })
                Ui.Text(t, head, "tiny", cls);
            foreach (var sq in org.Squares)
            {
                var square = sq;
                var zone = game.Territory.Zones[sq.ZoneId];
                var row = Ui.Box("pz-row").Into(bizBody);
                bool raid = game.Police.IsRaiding(sq);
                Ui.Text(zone.Name + (raid ? "  <color=#F07A6E>RAID</color>" : ""), row, "pz-name", "bold");
                var hours = Ui.Box("pz-hours").Into(row);
                Ui.Btn("‹", Do(() => { square.OpenHour = (square.OpenHour + 23) % 24; org.OrganiseShifts(game); }), hours, "btn--small", "pz-step");
                Ui.Text($"{sq.OpenHour:00}–{sq.CloseHour:00}", hours, "pz-hourtext");
                Ui.Btn("›", Do(() => { square.CloseHour = (square.CloseHour + 1) % 24; org.OrganiseShifts(game); }), hours, "btn--small", "pz-step");
                var heat = Ui.Box("pz-heat").Into(row);
                var track = Ui.Box("fig__track").Into(heat);
                var fill = Ui.Box("bar__fill").Into(track);
                fill.style.backgroundColor = zone.Heat >= 60f ? new Color(0.88f, 0.3f, 0.26f) : new Color(0.9f, 0.6f, 0.3f);
                Ui.SetFill(fill, zone.Heat / 100f);
                Ui.Text($"€{sq.RevenueToday}", row, "pz-today", "gold");
                Ui.Text($"{org.CrewOf(sq, game).Count}", row, "pz-crew");
                Ui.Btn("Show", () => FocusCamera(Mover.CellCenter(square.Cell)), row, "btn--small", "pz-go");
                Tip(row, () => $"{zone.Name}: open {square.HoursLabel} (‹ opens an hour earlier, › closes an hour later; the rest in Famiglia › Organigram).\n" +
                               $"Today {square.SalesToday} sold, {square.LostToday} customers lost. Heat {zone.Heat:0}. Demand {zone.Demand:0.0}." +
                               (game.Clans.Any(c => !c.Destroyed && c.NearRivalTerritory(square)) ? "\nCompeting with a clan nearby." : ""));
            }
        }

        /// <summary>The first corner: Don Saverio's until the family takes it.</summary>
        void SaverioSection()
        {
            var org = game.Org;
            var c = org.SaverioContact(game);
            if (c == null || (org.Sav.taken && !org.SaverioCutRunning)) return;
            Ui.Text("DON SAVERIO'S CORNER", bizBody, "section");
            bool war = org.Sav.feud || org.Sav.dead;
            Ui.Text(org.SaverioStatus(game), bizBody, "small", "wrap", org.Sav.taken ? "muted" : war ? "bad" : "warn");
            var row = Ui.Row(bizBody);
            if (!org.Sav.taken && !war)
            {
                var call = Ui.Btn($"Call him with an offer ▸ (€{org.SaverioPrice})", () => { game.Events.Raise(OSistema.Story.EventCards.SaverioOffer(game)); ToggleWindow(Window.Business); }, row, "btn--small");
                Tip(call, () => org.CanBuyOutSaverio(game, out var why) ? "Pay him to retire, all at once or a cut every Sunday." : why);
            }
            Ui.Btn("Show", () => FocusCamera(c.Present ? c.Position : Mover.CellCenter(c.Spot)), row, "btn--small");
        }

        void PizzoTab()
        {
            var org = game.Org;
            if (org.Rank < 2)
                Ui.Text("Shopkeepers pay a Clan, not a couple of guagliuni with ideas. Pizzo opens when the family becomes a Clan.", bizBody, "warn", "wrap");
            int weekly = org.Businesses.Where(b => b.Status == BusinessStatus.Paying).Sum(b => b.WeeklyPizzo);
            Ui.Text($"Paying businesses bring in <b>€{weekly}</b> a week. You can only squeeze the shops on your own turf ({string.Join(", ", org.Turf(game).Select(z => z.Name))}); the rest pay someone else, or nobody yet.", bizBody, "wrap");
            // One heading per rione: your turf first and open, the rest folded.
            var turf = new System.Collections.Generic.HashSet<Crime.Zone>(org.Turf(game));
            var groups = org.Businesses.GroupBy(x => game.Territory.ZoneAt(x.Cell))
                .OrderByDescending(g => g.Key != null && turf.Contains(g.Key))
                .ThenBy(g => g.Key?.Name);
            foreach (var group in groups)
            {
                var zoneName = group.Key?.Name ?? "Elsewhere";
                bool mine = group.Key != null && turf.Contains(group.Key);
                if (!pizzoOpen.ContainsKey(zoneName)) pizzoOpen[zoneName] = mine;
                bool open = pizzoOpen[zoneName];
                int paying = group.Count(x => x.Status == BusinessStatus.Paying);
                int income = group.Where(x => x.Status == BusinessStatus.Paying).Sum(x => x.WeeklyPizzo);
                var head = Ui.Box("attention", mine ? "attention--warn" : "x").Into(bizBody);
                head.style.marginTop = 6;
                Ui.Text($"{(open ? "▾" : "▸")}  <b>{zoneName}</b>{(mine ? "  <color=#E8C26A>your turf</color>" : "")}", head, "grow").pickingMode = PickingMode.Ignore;
                Ui.Text($"{group.Count()} shops · {paying} paying · €{income}/week", head, "small", "muted").pickingMode = PickingMode.Ignore;
                var zn = zoneName;
                head.RegisterCallback<ClickEvent>(_ => { pizzoOpen[zn] = !pizzoOpen[zn]; bizDirty = true; RefreshWindows(); });
                if (!open) continue;
                foreach (var b in group)
                {
                    var row = Ui.Box("table-row").Into(bizBody);
                    row.style.paddingLeft = 14;
                    var col = Ui.Box("col").Into(row);
                    col.style.width = 236;
                    Ui.Text($"<b>{b.Name}</b>", col);
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
        }

        readonly System.Collections.Generic.Dictionary<string, bool> pizzoOpen = new System.Collections.Generic.Dictionary<string, bool>();

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
            Ui.Text($"Tobacco and papers from the tabaccaio, empty packs from Nanninella. Roll them at a rolling table (E), or give someone the Roller job. Zio Totonno buys up to {Crime.Organization.TotonnoDailyPacks} packs a day; a deposito sells many more.", bizBody, "small", "muted", "wrap");
            Ui.Text("GEAR", bizBody, "section");
            Ui.Text($"{(st.DigitalScale ? "✓" : "✗")} digital scale   {(st.HeatSealer ? "✓" : "✗")} heat sealer   {(st.RollingMachine ? "✓" : "✗")} rolling machine   · Nanninella sells them.", bizBody, "small");

            Ui.Text("SCOOTERS", bizBody, "section");
            Ui.Text($"The family has <b>{st.Scooters}</b>. They stand parked in the street: walk up to one and press R. Crew take one parked near them for a long trip, and leave it where they get off.", bizBody, "small", "wrap");
            Ui.Text($"Your ride: <b>{Prices.ScooterNames[Mathf.Clamp(st.ScooterModel, 0, 3)]}</b>. Buy more, or a better one, at the scooter dealer (on the map).", bizBody, "small", "muted", "wrap");

            Ui.Text("ARMS", bizBody, "section");
            Ui.Text($"In the stash: <b>{st.Pistols}</b> pistols, <b>{st.Berettas}</b> Berettas, <b>{st.Kalash}</b> Kalashnikovs. Armed crew get the best one there; they defend themselves and fight when drafted, but get longer sentences if arrested.", bizBody, "small", "wrap");
            foreach (var g in OSistema.Combat.Guns.All)
            {
                var gun = g;
                var stats = OSistema.Combat.Guns.Of(g);
                var gunRow = Ui.Row(bizBody);
                Ui.Btn($"Buy a {stats.Name} (€{stats.Price})", Do(() =>
                {
                    if (!org.RequireRank(stats.Rank, $"A {stats.Name}", game, out var rankError)) game.Log.Add(rankError, MessageKind.Bad);
                    else if (!org.Pay(stats.Price, game, out var error)) game.Log.Add(error, MessageKind.Bad);
                    else
                    {
                        st.AddGun(gun, 1);
                        game.Log.Add($"A {stats.Name}, wrapped in a cloth, now sits in the stash.");
                    }
                }), gunRow, "btn--small");
                Ui.Text(stats.Note, gunRow, "small", "muted", "wrap");
            }
            var me = game.Player;
            if (me != null)
            {
                var carry = Ui.Row(bizBody);
                Ui.Text($"You carry: <b>{(me.Armed ? me.GunStats.Name : "nothing")}</b>", carry, "small");
                foreach (var g in OSistema.Combat.Guns.All)
                {
                    var gun = g;
                    if (me.Armed && me.Gun == g) continue;
                    var swap = Ui.Btn($"Take the {OSistema.Combat.Guns.Of(g).Name}", Do(() =>
                    {
                        if (st.GunsOf(gun) <= 0) return;
                        if (me.Armed) st.AddGun(me.Gun, 1);
                        st.AddGun(gun, -1);
                        me.Armed = true;
                        me.Gun = gun;
                        me.Ammo = me.MagSize;
                    }), carry, "btn--small");
                    swap.SetEnabled(st.GunsOf(g) > 0);
                }
            }
            var boss = game.Player;
            Ui.Text($"Your vest: <b>{(boss == null || boss.Armor <= 0.5f ? "none" : $"{boss.Armor / Pawn.MaxArmor:P0}")}</b>. It soaks the first hits and doesn't mend: once shot through, buy another.", bizBody, "small", "wrap");
            Ui.Btn($"Buy a bulletproof vest (€{Prices.Vest})", Do(() =>
            {
                if (boss == null) return;
                if (boss.Armor >= Pawn.MaxArmor - 0.5f) game.Log.Add("You're already wearing a good vest.", MessageKind.Info);
                else if (!org.RequireRank(1, "A bulletproof vest", game, out var rankError)) game.Log.Add(rankError, MessageKind.Bad);
                else if (!org.Pay(Prices.Vest, game, out var error)) game.Log.Add(error, MessageKind.Bad);
                else
                {
                    boss.Armor = Pawn.MaxArmor;
                    game.Log.Add("A vest under the jacket. Heavy, hot, and worth it.");
                }
            }), bizBody, "btn--small").style.alignSelf = Align.FlexStart;
        }

        void RivalsTab()
        {
            // The city: who holds what. The game is to put it back together under one family: yours.
            int total = game.Territory.Zones.Count;
            int mine = game.Territory.Zones.Count(z => game.Org.ControlsZone(z, game));
            Ui.Text($"The city is in pieces. You hold <b>{mine}</b> of {total} rioni; the clans hold {game.Clans.Where(c => !c.Destroyed).Sum(c => c.Squares.Select(q => q.ZoneId).Distinct().Count())}. The map's Territory view shows who holds what.", bizBody, "wrap");
            Ui.Text("A rione is yours with a house or a shop of yours there and a working piazza. Clans come to take undefended piazze: lookouts warn you, armed crew hold the corner. Hold an empty rival piazza for a minute or two and it's yours.", bizBody, "small", "muted", "wrap");

            foreach (var c in game.Clans)
            {
                var clan = c;
                var head = Ui.Row(bizBody);
                head.style.marginTop = 12;
                var swatch = Ui.Box().Into(head);
                swatch.style.width = 12;
                swatch.style.height = 12;
                swatch.style.marginRight = 8;
                swatch.style.backgroundColor = (Color)clan.Colour;
                Ui.Text(clan.Name.ToUpper() + (clan.Allied ? "  <color=#8FD18A>· allied</color>" : ""), head, "section").style.marginTop = 0;
                if (clan.Destroyed)
                {
                    Ui.Text("Finished. Their rioni are open.", bizBody, "good", "small");
                    continue;
                }
                bool knowBoss = clan.Intel >= RivalClan.IntelForBoss, knowMen = clan.Intel >= RivalClan.IntelForLieutenant;
                var rioni = clan.Squares.Select(q => game.Territory.Zones[q.ZoneId].Name).Distinct();
                Ui.Text($"Boss: {(knowBoss ? clan.BossName : "<color=#A89F8E>nobody will say</color>")} · soldiers: {(knowMen ? clan.Soldiers.ToString() : "<color=#A89F8E>a few</color>")} · rioni: {string.Join(", ", rioni)}", bizBody, "small", "wrap");
                var bars = Ui.Row(bizBody);
                var left = Ui.Box("col", "grow").Into(bars);
                left.style.marginRight = 10;
                var right = Ui.Box("col", "grow").Into(bars);
                var (hv, hf) = Ui.Bar("Hostility", new Color(0.95f, 0.55f, 0.25f), left);
                hv.text = $"{clan.Hostility:0}";
                Ui.SetFill(hf, clan.Hostility / 100f);
                var (iv, iff) = Ui.Bar("What you know", new Color(0.55f, 0.75f, 0.95f), right);
                iv.text = $"{clan.Intel:0}";
                Ui.SetFill(iff, clan.Intel / 100f);
                string mood = clan.InTruce(game) ? $"Truce until day {clan.TruceUntilDay}."
                    : clan.Hostility >= RivalClan.HitHostility ? "<color=#F07A6E>They want blood: ambushes and assaults on your piazze.</color>"
                    : clan.Hostility >= 55f ? "<color=#F2B866>They'll come for an undefended piazza.</color>"
                    : clan.Hostility >= RivalClan.StesaHostility ? "<color=#F2B866>Angry: expect shots over your piazze.</color>"
                    : "Watching.";
                Ui.Text(mood, bizBody, "small", "wrap");
                var actions = Ui.Row(bizBody);
                actions.style.flexWrap = Wrap.Wrap;
                Ui.Btn("Gift €500 (hostility −25)", Do(() => { if (!clan.SendGift(500, 25f, game)) game.Log.Add("Not enough cash for a gift.", MessageKind.Bad); }), actions, "btn--small");
                if (clan.Allied)
                    Ui.Btn("Break the pact", Do(() => clan.BreakAlliance(game, "you broke it")), actions, "btn--small", "btn--danger");
                else
                {
                    var pact = Ui.Btn($"Propose a pact · €{RivalClan.AlliancePrice} ({clan.AllianceChance(game) * 100f:0}%)", Do(() => clan.ProposeAlliance(game)), actions, "btn--small");
                    pact.SetEnabled(clan.Hostility < 30f);
                    Tip(pact, () => clan.Hostility < 30f ? "Allies leave you alone and go after the clans that hate you. Stay off their turf, and never shoot their people." : "They hate you too much to talk. Bring their hostility under 30 first (gifts, time).");
                }
                var stesa = Ui.Btn("Order a stesa ▸", () => { game.Events.Raise(OSistema.Story.OperationCards.Stesa(clan, game)); ToggleWindow(Window.Business); }, actions, "btn--small");
                stesa.SetEnabled(clan.Squares.Count > 0);
                Tip(stesa, () => "Armed crew ride to one of their piazze and shoot over it.");
                var hit = Ui.Btn("Order a hit ▸", () => { game.Events.Raise(OSistema.Story.OperationCards.Hit(clan, game)); ToggleWindow(Window.Business); }, actions, "btn--small", "btn--danger");
                hit.SetEnabled(knowMen);
                Tip(hit, () => knowMen ? "Send a hitman for one of their lieutenants, or the boss if you know enough." : $"You don't know enough about them yet ({clan.Intel:0}/{RivalClan.IntelForLieutenant:0}). Lookouts near their turf and Nanninella tell you more.");
                if (clan.Squares.Count > 0)
                {
                    var first = clan.Squares[0];
                    Ui.Btn("Show their piazza", () => FocusCamera(Mover.CellCenter(first.Cell)), actions, "btn--small");
                }
            }
        }
    }
}
