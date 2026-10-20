using System.Collections.Generic;
using System.Linq;
using OSistema.AI;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>
    /// Famiglia's people: a searchable roster that copes with three guagliuni or thirty, one person's
    /// page (job, wage, bed, gun), and the two front pages. Early on the front page is personal: your
    /// guagliuni and your home. From Clan up it becomes the organisation's dashboard: capi, piazze,
    /// takings, the neighbourhood and the other clans.
    /// </summary>
    public sealed partial class Hud
    {
        enum RosterFilter { All, Available, Working, Hurt, Street }
        enum RosterGroup { None, Job, Piazza, Capo }

        RosterFilter rosterFilter;
        RosterGroup rosterGroup;
        string rosterSearch = "";
        Pawn famPerson;
        bool searchTyping, searchHooked;

        /// <summary>From Clan up the family is an organisation, and Famiglia shows it as one.</summary>
        bool BossView => game != null && game.Org.Rank >= 2;

        /// <summary>Typing in a search box: the keys aren't for the game.</summary>
        public bool Typing => searchTyping;

        string TabTitle(BizTab t) => TabName(t);

        /// <summary>
        /// The three screens on the left, each with its own button on the bottom bar: Casa (how things
        /// stand, the stash, the money), Famiglia (the people and who answers to whom) and Piazza (the
        /// street: piazze, pizzo, neighbours, clans). Each holds a few tabs of the one sidebar.
        /// </summary>
        enum FamSection { Casa, Famiglia, Piazza }

        static FamSection SectionOf(BizTab t) => t switch
        {
            BizTab.Overview or BizTab.Supply or BizTab.Property => FamSection.Casa,
            BizTab.Crew or BizTab.Organigram => FamSection.Famiglia,
            _ => FamSection.Piazza,
        };

        static BizTab FirstTab(FamSection s) => s switch
        {
            FamSection.Casa => BizTab.Overview,
            FamSection.Famiglia => BizTab.Crew,
            _ => BizTab.Piazze,
        };

        Icon famIcon;
        Label famTitle;

        bool SectionOpen(FamSection s) => openWindow == Window.Business && SectionOf(bizTab) == s;

        /// <summary>The bottom bar's Casa, Famiglia and Piazza buttons: open that screen, or close it if it's showing.</summary>
        void OpenSection(FamSection s)
        {
            if (SectionOpen(s)) { ToggleWindow(Window.Business); return; }
            if (openWindow != Window.Business) ToggleWindow(Window.Business);
            OpenBizTab(FirstTab(s));
        }

        void OpenFamigliaRoster()
        {
            if (openWindow != Window.Business) ToggleWindow(Window.Business);
            famPerson = null;
            bizTab = BizTab.Crew;
            bizDirty = true;
            RefreshWindows();
        }

        void OpenFamigliaPerson(Pawn p)
        {
            if (openWindow != Window.Business) ToggleWindow(Window.Business);
            bizTab = BizTab.Crew;
            famPerson = p;
            bizDirty = true;
            if (bizScroll != null) bizScroll.scrollOffset = Vector2.zero;
            RefreshWindows();
        }

        /// <summary>Tab order and names follow the rank: a boss looks at the organisation first.</summary>
        void RefreshNavOrder()
        {
            bool boss = BossView;
            var section = SectionOf(bizTab);
            famTitle.text = section switch { FamSection.Casa => "CASA", FamSection.Famiglia => "FAMIGLIA", _ => "PIAZZA" };
            famIcon.Set(section switch { FamSection.Casa => IconKind.House, FamSection.Famiglia => IconKind.Business, _ => IconKind.Product });
            famSubtitle.text = section switch
            {
                FamSection.Casa => boss ? $"{game.Org.RankName} · the organisation at a glance, the stash, the money" : $"{game.Org.RankName} · how things stand, the stash, the money",
                FamSection.Famiglia => boss ? "Your people, the capi, and who answers to whom" : "Your guagliuni: jobs, wages, beds",
                _ => "The street: piazze, pizzo, the neighbours and the clans",
            };
            bool organigram = game.Org.Squares.Count > 0 || boss;
            int shown = 0;
            foreach (var kv in bizTabButtons)
            {
                bizTabStatus[kv.Key].text = TabName(kv.Key);
                bool show = SectionOf(kv.Key) == section && (kv.Key != BizTab.Organigram || organigram);
                kv.Value.Show(show);
                if (show) shown++;
            }
            bizNav.Show(shown > 1);
        }

        // ---------- Search box (read straight from the keyboard, like the name fields) ----------

        void SearchBox(VisualElement parent)
        {
            var box = Ui.Box("namefield", "search").Into(parent);
            var text = Ui.Text("", box, "namefield__text");
            text.text = rosterSearch.Length == 0 && !searchTyping ? "<color=#7A7468>Search by name or job…</color>"
                : rosterSearch + (searchTyping ? "<color=#E8C26A>|</color>" : "");
            box.Toggle("namefield--on", searchTyping);
            box.RegisterCallback<PointerDownEvent>(e => { SetSearchTyping(true); e.StopPropagation(); });
            if (rosterSearch.Length > 0)
                Ui.Btn("✕", () => { rosterSearch = ""; bizDirty = true; RefreshWindows(); }, parent, "btn--small");
        }

        void SetSearchTyping(bool on)
        {
            searchTyping = on;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && on != searchHooked)
            {
                if (on) kb.onTextInput += OnSearchTyped; else kb.onTextInput -= OnSearchTyped;
                searchHooked = on;
            }
            bizDirty = true;
            if (game != null) RefreshWindows();
        }

        void OnSearchTyped(char c)
        {
            if (!searchTyping) return;
            if (c == '\b' || c == (char)127) { if (rosterSearch.Length > 0) rosterSearch = rosterSearch.Substring(0, rosterSearch.Length - 1); }
            else if (c == '\r' || c == '\n' || c == '\t' || c == (char)27) { SetSearchTyping(false); return; }
            else if (!char.IsControl(c) && rosterSearch.Length < 24) rosterSearch += c;
            bizDirty = true;
            RefreshWindows();
        }

        // ---------- The roster ----------

        bool AtHome(Pawn p) => game.PlayerBase.Contains(p.Cell) || game.Properties.IsOwnedHome(p.Cell);

        bool Matches(Pawn p, RosterFilter f) => f switch
        {
            RosterFilter.Available => p.CanAct && !p.Controlled && (p.Role == CrewRole.General || !JobGiver.OnShift(p, game)) && p.Health.Healthy,
            RosterFilter.Working => p.IsPresent && !p.Controlled && p.Role != CrewRole.General && JobGiver.OnShift(p, game) && p.Sleep == SleepState.Awake,
            RosterFilter.Hurt => !p.Health.Healthy || p.InHospital || p.Health.Downed,
            RosterFilter.Street => p.IsPresent && !AtHome(p),
            _ => true,
        };

        static string FilterName(RosterFilter f) => f switch
        {
            RosterFilter.Available => "Disponibili",
            RosterFilter.Working => "Al lavoro",
            RosterFilter.Hurt => "Feriti",
            RosterFilter.Street => "In strada",
            _ => "Tutti",
        };

        static string GroupName(RosterGroup g) => g switch
        {
            RosterGroup.Job => "by job",
            RosterGroup.Piazza => "by piazza",
            RosterGroup.Capo => "by capo",
            _ => "no groups",
        };

        string ZoneName(DrugSquare sq) => sq.ZoneId >= 0 && sq.ZoneId < game.Territory.Zones.Count ? game.Territory.Zones[sq.ZoneId].Name : "the piazza";

        /// <summary>The piazza this person works, if any: the one they're posted at, or the one their capo runs.</summary>
        DrugSquare PiazzaOf(Pawn p)
        {
            if (p.CurrentJob is PostJob post) return post.Square;
            return game.Org.SquareOf(p, game);
        }

        void RosterTab()
        {
            var org = game.Org;
            long now = game.Ticks.TicksGame;
            var everyone = game.Pawns.Crew.Where(p => !p.Dead).ToList();

            var top = Ui.Row(bizBody);
            SearchBox(top);

            var filters = Ui.Row(bizBody);
            filters.AddToClassList("filter-row");
            foreach (RosterFilter f in System.Enum.GetValues(typeof(RosterFilter)))
            {
                var ff = f;
                int n = everyone.Count(p => Matches(p, ff));
                var b = Ui.Btn($"{FilterName(f)} {n}", Do(() => rosterFilter = ff), filters, "pill");
                b.Toggle("pill--on", rosterFilter == f);
            }
            var groups = Ui.Row(bizBody);
            groups.AddToClassList("filter-row");
            Ui.Text("Group", groups, "tiny").style.marginRight = 4;
            foreach (RosterGroup g in System.Enum.GetValues(typeof(RosterGroup)))
            {
                if (g == RosterGroup.Piazza && org.Squares.Count == 0) continue;
                if (g == RosterGroup.Capo && org.Squares.Count < 2) continue;
                var gg = g;
                var b = Ui.Btn(GroupName(g), Do(() => rosterGroup = gg), groups, "pill", "pill--small");
                b.Toggle("pill--on", rosterGroup == g);
            }

            string q = rosterSearch.Trim().ToLowerInvariant();
            var shown = everyone.Where(p => Matches(p, rosterFilter) && (q.Length == 0 ||
                    p.FullName.ToLowerInvariant().Contains(q) || (p.Nickname ?? "").ToLowerInvariant().Contains(q) ||
                    RoleLabel(p.Role).ToLowerInvariant().Contains(q)))
                .OrderByDescending(p => p.Controlled).ThenByDescending(p => Attention(p, now) != null).ThenBy(p => p.DisplayName).ToList();

            if (shown.Count == 0) Ui.Text(everyone.Count <= 1 ? "Just you, for now. Respect brings the boys from the vicolo." : "Nobody matches.", bizBody, "muted", "small");

            if (rosterGroup == RosterGroup.None || shown.Count == 0)
                foreach (var p in shown) RosterRow(p, now);
            else
            {
                var byKey = shown.GroupBy(p => GroupKey(p)).OrderBy(g => g.Key.order);
                foreach (var g in byKey)
                {
                    Ui.Text($"{g.Key.name.ToUpper()}  <color=#A89F8E>{g.Count()}</color>", bizBody, "section");
                    foreach (var p in g) RosterRow(p, now);
                }
            }

            var r = Ui.Row(bizBody);
            r.style.marginTop = 10;
            var recruit = Ui.Btn($"Recruit a guaglione (€{Prices.RecruitBonus})", Do(() =>
            {
                if (game.Org.Recruit(game, out var error) == null) game.Log.Add(error, MessageKind.Bad);
            }), r, "btn--small");
            Tip(recruit, () => $"{everyone.Count}/{org.MaxCrewForRank} at your rank. Wages are paid on Sunday: €{org.WeeklyWageBill(game)} a week now.");
            Ui.Text($"  €{org.WeeklyWageBill(game)} a week in wages", r, "small", "muted");
        }

        (int order, string name) GroupKey(Pawn p)
        {
            if (p.Controlled || p.IsBoss) return (0, "The boss");
            switch (rosterGroup)
            {
                case RosterGroup.Piazza:
                {
                    var sq = PiazzaOf(p);
                    if (sq != null) return (1 + game.Org.Squares.IndexOf(sq), ZoneName(sq));
                    return (90, p.Role == CrewRole.General ? "No job" : "Home and other work");
                }
                case RosterGroup.Capo:
                {
                    var capo = game.Org.CapoOf(p, game);
                    if (p.Role == CrewRole.Capo) return (10 + p.Id % 1000, $"Capo {p.DisplayName}");
                    if (capo != null) return (10 + capo.Id % 1000, $"Capo {capo.DisplayName}");
                    return (2000, "Answer to the boss");
                }
                default:
                    return ((int)p.Role + 1, RoleGroupName(p.Role));
            }
        }

        static string RoleGroupName(CrewRole r) => r switch
        {
            CrewRole.General => "No job",
            CrewRole.Pusher => "Pushers",
            CrewRole.Lookout => "Lookouts",
            CrewRole.Collector => "Collectors",
            CrewRole.Manager => "Managers",
            CrewRole.Bagger => "Baggers",
            CrewRole.Roller => "Rollers",
            CrewRole.Bodyguard => "Bodyguards",
            CrewRole.Capo => "Capi paranza",
            _ => r.ToString(),
        };

        static string RoleShort(CrewRole r) => r switch
        {
            CrewRole.Pusher => "Pusher",
            CrewRole.Lookout => "Lookout",
            CrewRole.Collector => "Collector",
            CrewRole.Manager => "Manager",
            CrewRole.Bagger => "Bagger",
            CrewRole.Roller => "Roller",
            CrewRole.Bodyguard => "Bodyguard",
            CrewRole.Capo => "Capo",
            _ => "No job",
        };

        /// <summary>Where someone is, in two words and a colour: for the dot on their row.</summary>
        (string text, string css) Presence(Pawn p, long now)
        {
            if (p.InJail) return ("Inside", "dot-bad");
            if (p.InHospital) return ("Hospital", "dot-warn");
            if (p.Health.Downed) return ("Down", "dot-bad");
            if (p.Sleep != SleepState.Awake) return ("Asleep", "dot-off");
            if (!p.Controlled && p.Role != CrewRole.General && JobGiver.OnShift(p, game)) return ("On shift", "dot-warn");
            return AtHome(p) ? ("At home", "dot-good") : ("Out", "dot-good");
        }

        void RosterRow(Pawn p, long now)
        {
            var row = Ui.Box("roster-row").Into(bizBody);
            row.Toggle("roster-row--on", p == famPerson);
            Avatar(p, row, big: false).AddToClassList("avatar--row");
            var names = Ui.Box("col", "grow").Into(row);
            names.pickingMode = PickingMode.Ignore;
            Ui.Text(p.IsBoss ? $"<color=#E8C26A>{p.DisplayName}</color>" : p.DisplayName, names, "roster__name");
            string reason = Attention(p, now);
            Ui.Text(reason ?? (p.Controlled ? "You" : ShortStatus(p, now)), names, "roster__status");
            var (lv, lf) = Ui.Bar("", p.Loyalty < 30f ? new Color(0.88f, 0.4f, 0.3f) : new Color(0.45f, 0.75f, 0.45f), names, thin: true);
            Ui.SetFill(lf, (p.Controlled ? 100f : p.Loyalty) / 100f);
            lf.parent.AddToClassList("roster__bar");
            if (!p.Controlled)
            {
                var info = Ui.Box("col", "roster__info").Into(row);
                info.pickingMode = PickingMode.Ignore;
                Ui.Text(RoleShort(p.Role), info, "role-chip");
                Ui.Text($"€{p.Wage}/wk", info, "roster__wage");
            }
            var (ptext, pcss) = Presence(p, now);
            var pres = Ui.Box("presence").Into(row);
            pres.pickingMode = PickingMode.Ignore;
            Ui.Box("presence__dot", pcss).Into(pres).pickingMode = PickingMode.Ignore;
            Ui.Text(ptext, pres, "presence__text");
            row.style.opacity = p.InJail || p.InHospital ? 0.6f : 1f;
            var pawn = p;
            row.RegisterCallback<ClickEvent>(e =>
            {
                if (e.clickCount >= 2 && pawn.IsPresent) { FocusCamera(pawn.Position); return; }
                famPerson = pawn;
                bizDirty = true;
                if (bizScroll != null) bizScroll.scrollOffset = Vector2.zero;
                RefreshWindows();
            });
            Tip(row, () => "Click to open · double-click to find them in the city");
        }

        // ---------- One person ----------

        /// <summary>A line on someone's page: label on the left, value on the right, a chevron when it opens something.</summary>
        VisualElement Field(string label, string value, System.Action<VisualElement> onClick = null)
        {
            var row = Ui.Box("field").Into(bizBody);
            Ui.Text(label, row, "field__label");
            Ui.Text(value, row, "field__value");
            if (onClick != null)
            {
                Ui.Text("›", row, "field__chev");
                row.AddToClassList("field--click");
                row.RegisterCallback<ClickEvent>(e => { if (!(e.target is Button)) onClick(row); });
            }
            return row;
        }

        void Tile(VisualElement grid, string glyph, string label, System.Action act, bool danger = false)
        {
            var b = Ui.Btn("", act, grid, "tile");
            if (danger) b.AddToClassList("btn--danger");
            Ui.Text(glyph, b, "tile__glyph");
            Ui.Text(label, b, "tile__label");
        }

        void PersonDetail(Pawn p)
        {
            if (p.Dead || !game.Pawns.Crew.Contains(p)) { famPerson = null; RosterTab(); return; }
            long now = game.Ticks.TicksGame;
            var pawn = p;

            var head = Ui.Box("person-head").Into(bizBody);
            Avatar(p, head, big: true).AddToClassList("avatar--hero");
            var names = Ui.Box("col", "grow").Into(head);
            Ui.Text(p.IsBoss ? $"{p.DisplayName}" : p.FirstName + " " + p.LastName, names, "person__name");
            Ui.Text(p.Controlled ? "The boss" : $"“{p.Nickname}” · {RoleLabel(p.Role)}", names, "person__role");
            string job = p.CurrentJob?.Label ?? "Thinking";
            Ui.Text(Attention(p, now) ?? job, names, "person__status");
            Ui.Btn("✕", Do(() => famPerson = null), head, "btn--small", "person__close");

            var (mv, mf) = Ui.Bar("Morale", MoodColor(p.Mood.Value), bizBody);
            mv.text = $"{p.Mood.Value:0}% · {p.Mood.Description}";
            Ui.SetFill(mf, p.Mood.Value / 100f);
            if (!p.Controlled)
            {
                var (lv, lf) = Ui.Bar("Loyalty", new Color(0.45f, 0.75f, 0.45f), bizBody);
                lv.text = $"{p.Loyalty:0}%";
                Ui.SetFill(lf, p.Loyalty / 100f);

                Ui.Text("WORK", bizBody, "section");
                Field("Job", RoleLabel(p.Role), at => ShowRoleMenu(pawn, at));
                PersonShifts(p);
                Field("Bed", BedLabel(p).Replace(" ▾", ""), at => ShowBedMenu(pawn, at));
                var wage = Field("Weekly wage", $"€{p.Wage}");
                Ui.Btn("−", Do(() => pawn.Wage = Mathf.Max(0, pawn.Wage - 10)), wage, "btn--small", "field__btn");
                Ui.Btn("+", Do(() => pawn.Wage = Mathf.Min(1000, pawn.Wage + 10)), wage, "btn--small", "field__btn");
                Field("Gun", p.Armed ? "Pistol" : $"None · {game.Stock.Pistols} in the stash", _ => { ToggleArmed(pawn); bizDirty = true; RefreshWindows(); });
                if (p.Role == CrewRole.Bodyguard) GuardSettings(p);
            }

            Ui.Text("QUICK ACTIONS", bizBody, "section");
            var grid = Ui.Box("tiles").Into(bizBody);
            if (p.IsPresent) Tile(grid, "◎", "Show", () => FocusCamera(pawn.Position));
            if (p.CanAct && !p.Controlled)
            {
                Tile(grid, "☝", "Select", () =>
                {
                    controller.Selected.Clear();
                    controller.SelectProperty(null);
                    controller.Selected.Add(pawn);
                    FocusCamera(pawn.Position);
                    ToggleWindow(Window.Business);
                });
                Tile(grid, "↩", "Call over", Do(() =>
                {
                    var boss = game.Player;
                    if (boss != null && pawn.CanAct) pawn.ForceJob(new GotoJob(pawn, boss.Cell, "Coming over"), game);
                }));
            }
            if (p.Health.Downed && !game.Hospital.IsCalled(p)) Tile(grid, "✚", "Call 118", Do(() => game.Hospital.Call(pawn, game, byPasserby: false)), danger: true);
            if (!p.Health.Healthy && OSistema.Crime.Healing.NeedsCare(p) && game.Stock.Medicine > 0) Tile(grid, "✚", $"Medicine ({game.Stock.Medicine})", Do(() => TreatWithMedicine(pawn)));

            if (!p.Health.Healthy || p.Mood.AllThoughts.Any())
            {
                Ui.Text("ON THEIR MIND", bizBody, "section");
                if (!p.Health.Healthy)
                    foreach (var inj in p.Health.Injuries.Take(4))
                        Ui.Text($"{inj.Label} · {inj.Part} {inj.Severity * 100f:0}%{(inj.IsMalnutrition ? "" : inj.Tended ? " <color=#8FD18A>tended</color>" : " <color=#F07A6E>untended</color>")}", bizBody, "thought");
                foreach (var t in p.Mood.AllThoughts.OrderByDescending(t => Mathf.Abs(t.Value)).Take(4))
                    Ui.Text($"<color={(t.Value >= 0 ? "#8FD18A" : "#F07A6E")}>{t.Value:+0;-0}</color>  {t.Label}", bizBody, "thought");
            }

            Ui.Text("GOOD AT", bizBody, "section");
            Ui.Text($"Handiwork <b>{p.ConstructionSkill}</b> · Cooking <b>{p.CookingSkill}</b> · Medicine <b>{p.MedicineSkill}</b>\n" +
                    $"Intimidation <b>{p.IntimidationSkill}</b> · Awareness <b>{p.AwarenessSkill}</b> · Shooting <b>{p.ShootingSkill}</b>", bizBody, "small", "wrap");
        }

        // ---------- Front pages ----------

        void StatCards(params (string label, string value, string note)[] items)
        {
            var cards = Ui.Row(bizBody);
            cards.style.flexWrap = Wrap.Wrap;
            foreach (var (label, value, note) in items)
            {
                var c = Ui.Box("stat-card").Into(cards);
                Ui.Text(label, c, "tiny", "muted");
                Ui.Text(value, c, "stat-card__value");
                Ui.Text(note, c, "tiny", "muted", "wrap");
            }
        }

        void NeedsYou()
        {
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
        }

        /// <summary>The next rank as a checklist, and what it opens up.</summary>
        void RankCard()
        {
            var org = game.Org;
            var items = org.RankChecklist(game);
            if (items.Count == 0) return;
            var card = Ui.Box("rank-card").Into(bizBody);
            Ui.Text(Organization.RankNames[org.Rank + 1].ToUpper(), card, "rank-card__title");
            foreach (var (text, done) in items)
                Ui.Text(done ? $"<color=#8FD18A>✓</color>  {text}" : $"<color=#7A7468>○</color>  {text}", card, done ? "small" : "small");
            Ui.Text($"Opens up: {Organization.UnlocksAt(org.Rank + 1)}", card, "tiny", "wrap");
        }

        /// <summary>Early on: you, your guagliuni and the basso.</summary>
        void OverviewTab()
        {
            var st = game.Stock;
            var org = game.Org;
            int beds = game.Map.PositionsOf(BuildingDefs.Bed).Count(b => game.PlayerBase.Contains(b) || game.Properties.IsOwnedHome(b));
            int crew = game.Pawns.Crew.Count(p => !p.Dead);
            StatCards(("CASH", $"€{st.Cash}", $"+ €{st.Clean} clean"),
                      ("SUNDAY'S WAGES", $"€{org.WeeklyWageBill(game)}", "the mesata"),
                      ("HOME", $"{beds} bed{(beds == 1 ? "" : "s")}", $"{crew} to sleep · {st.Meals + st.Groceries * 2} meals"));
            NeedsYou();

            Ui.Text("YOUR GUAGLIUNI", bizBody, "section");
            long now = game.Ticks.TicksGame;
            foreach (var p in game.Pawns.Crew.Where(p => !p.Dead && !p.Controlled)) RosterRow(p, now);
            if (crew <= 1) Ui.Text("Nobody yet. Make a name for yourself and the boys from the vicolo will call.", bizBody, "small", "muted", "wrap");

            Ui.Text("WHAT YOU'RE WORKING ON", bizBody, "section");
            Ui.Text(Objectives.Current(game, null).Text ?? "Nothing in particular.", bizBody, "wrap");
            RankCard();
            Ui.Text("Lavoretti from contacts are in the Taccuino (J). Shops and contacts are on the map (M).", bizBody, "small", "muted", "wrap");
        }

        /// <summary>A boss's front page: capi and their piazze, the takings, the neighbourhood, the clans.</summary>
        void DashboardTab()
        {
            var st = game.Stock;
            var org = game.Org;
            int today = org.Squares.Sum(s => s.RevenueToday);
            int pizzo = org.Businesses.Where(b => b.Status == BusinessStatus.Paying).Sum(b => b.WeeklyPizzo);
            StatCards(("CASH", $"€{st.Cash}", $"+ €{st.Clean} clean"),
                      ("PIAZZE TODAY", $"€{today}", $"pizzo €{pizzo}/week"),
                      ("WAGES", $"€{org.WeeklyWageBill(game)}", "every Sunday"),
                      ("TERRITORY", $"{org.Turf(game).Count} rioni", $"{org.Squares.Count}/{org.MaxSquaresForRank} piazze"));
            NeedsYou();

            Ui.Text("PIAZZE AND CAPI", bizBody, "section");
            if (org.Squares.Count == 0) Ui.Text("No piazze. Open one from Build (B).", bizBody, "small", "muted");
            foreach (var sq in org.Squares)
            {
                var square = sq;
                var zone = game.Territory.Zones[sq.ZoneId];
                var capo = org.CapoFor(sq, game);
                var row = Ui.Box("roster-row").Into(bizBody);
                var col = Ui.Box("col", "grow").Into(row);
                col.pickingMode = PickingMode.Ignore;
                Ui.Text($"<b>{zone.Name}</b>  <color=#A89F8E>{sq.HoursLabel}</color>" + (game.Police.IsRaiding(sq) ? "  <color=#F07A6E>RAID</color>" : ""), col, "small");
                Ui.Text((capo != null ? $"Capo {capo.DisplayName}" : "<color=#F2B866>no capo</color>") +
                        $" · {org.CrewOf(sq, game).Count} on the crew · heat {zone.Heat:0}", col, "tiny");
                Ui.Text($"€{sq.RevenueToday}", row, "bold", "gold").pickingMode = PickingMode.Ignore;
                row.RegisterCallback<ClickEvent>(_ => OpenBizTab(BizTab.Organigram));
                Tip(row, () => $"Today: {square.SalesToday} sold, {square.LostToday} customers lost. Click for the organigram.");
            }

            Ui.Text("THE NEIGHBOURHOOD", bizBody, "section");
            int owed = game.Favours.TotalOwed;
            int witnesses = game.Favours.S.Neighbours.Count(n => n.Witness);
            int grudges = game.Favours.S.Neighbours.Count(n => n.Grudge > 0);
            var hood = Ui.Box("attention").Into(bizBody);
            Ui.Text($"{owed} favour{(owed == 1 ? "" : "s")} owed to you · {grudges} with a grudge" + (witnesses > 0 ? $" · <color=#F07A6E>{witnesses} talking to the police</color>" : ""), hood, "wrap", "grow").pickingMode = PickingMode.Ignore;
            Ui.Text("Favours ›", hood, "small", "gold").pickingMode = PickingMode.Ignore;
            hood.RegisterCallback<ClickEvent>(_ => OpenBizTab(BizTab.Favours));

            Ui.Text("THE OTHER CLANS", bizBody, "section");
            foreach (var c in game.Clans)
            {
                var row = Ui.Box("roster-row").Into(bizBody);
                var sw = Ui.Box().Into(row);
                sw.style.width = 10;
                sw.style.height = 10;
                sw.style.marginRight = 8;
                sw.style.backgroundColor = (Color)c.Colour;
                var col = Ui.Box("col", "grow").Into(row);
                col.pickingMode = PickingMode.Ignore;
                string state = c.Destroyed ? "<color=#8FD18A>finished</color>" : c.Allied ? "<color=#8FD18A>allied</color>"
                    : c.InTruce(game) ? "truce" : c.Hostility >= 70f ? "<color=#F07A6E>want blood</color>" : c.Hostility >= 40f ? "<color=#F2B866>angry</color>" : "watching";
                Ui.Text($"<b>{c.Name}</b> · {state}", col, "small");
                if (!c.Destroyed)
                {
                    var (_, fill) = Ui.Bar("", new Color(0.95f, 0.55f, 0.25f), col, thin: true);
                    Ui.SetFill(fill, c.Hostility / 100f);
                }
                Ui.Text(c.Destroyed ? "" : $"{c.Squares.Count} piazz{(c.Squares.Count == 1 ? "a" : "e")}", row, "tiny").pickingMode = PickingMode.Ignore;
                row.RegisterCallback<ClickEvent>(_ => OpenBizTab(BizTab.Rivals));
            }
        }

        // ---------- Pick menus ----------

        /// <summary>A small menu under a button: pick one line.</summary>
        void ShowPickMenu(VisualElement anchor, List<(string label, System.Action act, bool on)> items, string empty = null)
        {
            if (roleMenu == null)
            {
                roleMenu = Ui.Box("layer").Into(modalLayer);
                roleMenu.RegisterCallback<PointerDownEvent>(e => { if (e.target == roleMenu) roleMenu.Show(false); });
            }
            roleMenu.Clear();
            roleMenu.Show(true);
            roleMenu.pickingMode = PickingMode.Position;
            var list = Ui.Box("panel", "role-menu").Into(roleMenu);
            var r = anchor.worldBound;
            var local = roleMenu.WorldToLocal(new Vector2(r.xMin, r.yMax));
            list.style.left = local.x;
            list.style.top = local.y + 2;
            foreach (var (label, act, on) in items)
            {
                var a = act;
                var b = Ui.Btn(label, () => { roleMenu.Show(false); a(); bizDirty = true; RefreshAll(); }, list, "btn--small", "role-menu__item");
                b.Toggle("btn--on", on);
            }
            if (items.Count == 0 && empty != null) Ui.Text(empty, list, "small", "muted", "wrap");
        }

        // ---------- Shifts on a person's page ----------

        void PersonShifts(Pawn p)
        {
            var org = game.Org;
            var pawn = p;
            if (Organization.PiazzaRole(p.Role) && org.Squares.Count > 0)
            {
                var sq = org.SquareOf(p, game);
                if (org.Squares.Count > 1)
                    Field("Piazza", sq != null ? ZoneName(sq) : "not placed", at =>
                        ShowPickMenu(at, org.Squares.Select(q => ($"{ZoneName(q)} · {q.HoursLabel}", (System.Action)(() => org.MoveToPiazza(pawn, q, game)), q == sq)).ToList()));
                else if (sq != null) Field("Piazza", ZoneName(sq));
                if (sq == null) return;
                int team = org.TeamOf(p, game);
                var (from, to) = Organization.TeamHours(sq, team < 0 ? 0 : team);
                var capo = org.CapoFor(sq, game);
                string who = p.Role == CrewRole.Capo ? "you run it" : capo != null ? $"Capo {capo.DisplayName}" : org.IsRun(sq, game) ? "the boss" : "nobody";
                string teamName = team == 1 ? "Night" : "Day";
                Field("Shift", $"{teamName} ({from:00}:00–{to:00}:00) · <color=#A89F8E>{who}</color>", _ => OpenBizTab(BizTab.Organigram));
                if (!org.IsRun(sq, game))
                    Ui.Text("This piazza has no capo: nobody runs a night team. Appoint one in the Organigram.", bizBody, "small", "warn", "wrap");
                return;
            }
            var (f, t) = p.WorkHours;
            bool night = (f, t) == (21, 9);
            Field("Shift", night ? "Night (21:00–09:00)" : (f, t) == (9, 21) ? "Day (09:00–21:00)" : $"{f:00}:00–{t:00}:00",
                _ => { if (night) pawn.SetWorkHours(9, 21); else pawn.SetWorkHours(21, 9); bizDirty = true; RefreshWindows(); });
        }

        /// <summary>Where a bodyguard stands: a slot round the boss, or a post at home or at a piazza.</summary>
        void GuardSettings(Pawn p)
        {
            var pawn = p;
            Ui.Text("WHERE THEY STAND", bizBody, "section");
            var posts = Ui.Row(bizBody);
            posts.style.flexWrap = Wrap.Wrap;
            var home = BodyguardJob.HomePost(game, p);
            Ui.Btn("With the boss", Do(() => pawn.GuardPost = null), posts, "pill", !p.GuardPost.HasValue ? "pill--on" : "x");
            bool atHome = p.GuardPost.HasValue && game.PlayerBase.Contains(p.GuardPost.Value) == false && home.HasValue && GridUtil.Manhattan(p.GuardPost.Value, home.Value) <= 4 && game.Org.Squares.All(q => GridUtil.Manhattan(q.Cell, p.GuardPost.Value) > 3);
            var hb = Ui.Btn("At the basso's door", Do(() => pawn.GuardPost = BodyguardJob.HomePost(game, pawn)), posts, "pill", atHome ? "pill--on" : "x");
            hb.SetEnabled(home.HasValue);
            foreach (var sq in game.Org.Squares)
            {
                var square = sq;
                bool here = p.GuardPost.HasValue && GridUtil.Manhattan(p.GuardPost.Value, sq.Cell) <= 3;
                Ui.Btn($"At {ZoneName(sq)}", Do(() => pawn.GuardPost = BodyguardJob.PiazzaPost(game, pawn, square)), posts, "pill", here ? "pill--on" : "x");
            }
            if (p.GuardPost.HasValue)
            {
                Ui.Text("They hold the post through their shift and shoot at anyone who comes for it.", bizBody, "tiny", "wrap");
                return;
            }
            var slots = Ui.Row(bizBody);
            slots.style.flexWrap = Wrap.Wrap;
            Ui.Text("Formation", slots, "small", "muted").style.marginRight = 6;
            for (int i = 0; i < BodyguardJob.SlotNames.Length; i++)
            {
                int slot = i;
                Ui.Btn(BodyguardJob.SlotNames[i], Do(() => pawn.GuardSlot = slot), slots, "pill", "pill--small", p.GuardSlot == i ? "pill--on" : "x");
            }
            Ui.Text("Relative to the way the boss is walking. Close or Behind ride on the back of his scooter; the rest follow on their own.", bizBody, "tiny", "wrap");
        }

        // ---------- The organigram ----------

        void OrganigramTab()
        {
            var org = game.Org;
            long now = game.Ticks.TicksGame;
            var boss = org.Boss(game);

            // The boss at the top.
            var top = Ui.Box("org-node", "org-node--boss").Into(bizBody);
            if (boss != null) Avatar(boss, top, big: false);
            var bc = Ui.Box("col", "grow").Into(top);
            Ui.Text(boss != null ? $"<color=#E8C26A>{boss.DisplayName}</color> {boss.LastName}" : "No boss", bc, "bold");
            Ui.Text(org.Squares.Count == 0 ? "No piazze yet: open one from Build (B)." : org.CapoFor(org.Squares[0], game) == null ? $"Runs {ZoneName(org.Squares[0])} himself" : "Il capo", bc, "tiny");

            for (int i = 0; i < org.Squares.Count; i++)
            {
                var sq = org.Squares[i];
                var square = sq;
                var zone = game.Territory.Zones[sq.ZoneId];
                var branch = Ui.Box("org-branch").Into(bizBody);
                var head = Ui.Row(branch);
                Ui.Text(zone.Name.ToUpper(), head, "section").style.marginTop = 0;
                Ui.Box("spacer").Into(head);
                Ui.Text($"€{sq.RevenueToday} today · heat {zone.Heat:0}" + (game.Police.IsRaiding(sq) ? " · <color=#F07A6E>RAID</color>" : ""), head, "tiny");

                // Hours: the one thing the boss sets here.
                var hours = Ui.Row(branch);
                Ui.Text("Open", hours, "small", "muted");
                Ui.Btn("−", Do(() => { square.OpenHour = (square.OpenHour + 23) % 24; org.OrganiseShifts(game); }), hours, "btn--small");
                Ui.Text($"{sq.OpenHour:00}:00", hours, "bold");
                Ui.Btn("+", Do(() => { square.OpenHour = (square.OpenHour + 1) % 24; org.OrganiseShifts(game); }), hours, "btn--small");
                Ui.Text("  to", hours, "small", "muted");
                Ui.Btn("−", Do(() => { square.CloseHour = (square.CloseHour + 23) % 24; org.OrganiseShifts(game); }), hours, "btn--small");
                Ui.Text($"{sq.CloseHour:00}:00", hours, "bold");
                Ui.Btn("+", Do(() => { square.CloseHour = (square.CloseHour + 1) % 24; org.OrganiseShifts(game); }), hours, "btn--small");
                Ui.Box("spacer").Into(hours);
                Ui.Btn("Show", () => FocusCamera(Mover.CellCenter(square.Cell)), hours, "btn--small");

                // The capo.
                var capo = org.CapoFor(sq, game);
                var capoRow = Ui.Box("org-node").Into(branch);
                if (capo != null)
                {
                    Avatar(capo, capoRow, big: false);
                    var cc = Ui.Box("col", "grow").Into(capoRow);
                    Ui.Text($"<b>Capo {capo.DisplayName}</b>", cc, "small");
                    Ui.Text(ShortStatus(capo, now), cc, "tiny");
                    var c = capo;
                    capoRow.RegisterCallback<ClickEvent>(e => { if (!(e.target is Button)) OpenFamigliaPerson(c); });
                }
                else
                {
                    var cc = Ui.Box("col", "grow").Into(capoRow);
                    Ui.Text(i == 0 ? "<b>The boss runs it</b>" : "<color=#F2B866><b>No capo paranza</b></color>", cc, "small");
                    Ui.Text(i == 0 ? "A capo would free you of it." : "Without one there's no night team.", cc, "tiny");
                }
                Button appoint = null;
                appoint = Ui.Btn(capo == null ? "Appoint ▾" : "Replace ▾", () =>
                {
                    var candidates = game.Pawns.Crew.Where(p => p.IsPresent && !p.Controlled && !p.IsBoss && p != capo && p.Role != CrewRole.Capo && p.Role != CrewRole.Manager)
                        .OrderByDescending(p => p.Loyalty + p.IntimidationSkill * 4f + p.AwarenessSkill * 2f).ToList();
                    ShowPickMenu(appoint, candidates.Select(p => ($"{p.DisplayName} · {RoleLabel(p.Role)} · loyalty {p.Loyalty:0}", (System.Action)(() => org.AppointCapo(p, square, game)), false)).ToList(),
                        "Nobody free to put in charge. Recruit someone first.");
                }, capoRow, "btn--small");
                Tip(appoint, () => "A capo paranza runs the piazza: splits its crew into a day and a night team, and serves the customers himself when no pusher is on the spot. Loyal, hard men make the best capi.");

                // The crew, by team.
                var crew = org.CrewOf(sq, game);
                bool split = Organization.SplitsShifts(sq) && org.IsRun(sq, game);
                var teams = Ui.Row(branch);
                teams.style.alignItems = Align.FlexStart;
                for (int t = 0; t < (split ? 2 : 1); t++)
                {
                    var col = Ui.Box("org-team", "grow").Into(teams);
                    var (from, to) = Organization.TeamHours(sq, t);
                    Ui.Text(split ? $"{(t == 0 ? "DAY" : "NIGHT")}  {from:00}–{to:00}" : $"CREW  {from:00}–{to:00}", col, "tiny");
                    int tt = t;
                    foreach (var p in crew.Where(p => { int team = org.TeamOf(p, game); return !split || team == tt || team < 0; }))
                    {
                        var chip = Ui.Box("org-chip").Into(col);
                        Ui.Text($"{(p.Role == CrewRole.Pusher ? "◆" : "◎")} {p.DisplayName}" + (Attention(p, now) is string a ? $"  {a}" : ""), chip, "small");
                        var pp = p;
                        chip.RegisterCallback<ClickEvent>(_ => OpenFamigliaPerson(pp));
                        Tip(chip, () => $"{RoleLabel(pp.Role)} · {ShortStatus(pp, now)}" + (org.TeamOf(pp, game) < 0 && split ? "\nAlone in this job here: works the day shift." : ""));
                    }
                }
                var addRow = Ui.Row(branch);
                foreach (var role in new[] { CrewRole.Pusher, CrewRole.Lookout })
                {
                    var rr = role;
                    Button add = null;
                    add = Ui.Btn(role == CrewRole.Pusher ? "+ Pusher ▾" : "+ Lookout ▾", () =>
                    {
                        var free = game.Pawns.Crew.Where(p => p.IsPresent && !p.Controlled && !p.IsBoss && (p.Role == CrewRole.General || (Organization.PiazzaRole(p.Role) && p.Role != CrewRole.Capo && org.SquareOf(p, game) != square)))
                            .OrderBy(p => p.Role != CrewRole.General).ToList();
                        ShowPickMenu(add, free.Select(p => ($"{p.DisplayName} · {(p.Role == CrewRole.General ? "no job" : $"{RoleLabel(p.Role)} at {(org.SquareOf(p, game) is DrugSquare o ? ZoneName(o) : "?")}")}",
                            (System.Action)(() => { p.Role = rr; org.MoveToPiazza(p, square, game); }), false)).ToList(), "Nobody free. Recruit, or take someone off another job.");
                    }, addRow, "btn--small");
                }
                Ui.Text($"  {crew.Count(p => p.Role == CrewRole.Pusher)} pushers · {crew.Count(p => p.Role == CrewRole.Lookout)} lookouts", addRow, "tiny");
            }

            // Everyone else answers straight to the boss.
            var others = game.Pawns.Crew.Where(p => !p.Dead && !p.Controlled && !Organization.PiazzaRole(p.Role)).ToList();
            if (others.Count > 0)
            {
                var branch = Ui.Box("org-branch").Into(bizBody);
                Ui.Text("WITH THE BOSS", branch, "section").style.marginTop = 0;
                var wrap = Ui.Row(branch);
                wrap.style.flexWrap = Wrap.Wrap;
                foreach (var p in others.OrderBy(p => p.Role))
                {
                    var chip = Ui.Box("org-chip").Into(wrap);
                    Ui.Text($"{p.DisplayName} <color=#A89F8E>{RoleLabel(p.Role).Split(' ')[0].ToLower()}</color>", chip, "small");
                    var pp = p;
                    chip.RegisterCallback<ClickEvent>(_ => OpenFamigliaPerson(pp));
                }
            }
        }
    }
}
