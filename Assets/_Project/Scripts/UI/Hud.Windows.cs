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
        enum Window { None, Business, Help }
        enum BizTab { Crew, Piazze, Pizzo, Supply, Rivals }

        Window openWindow;
        BizTab bizTab;
        VisualElement businessWindow, helpWindow, bizBody;
        ScrollView bizScroll;
        readonly System.Collections.Generic.Dictionary<BizTab, Button> bizTabButtons = new System.Collections.Generic.Dictionary<BizTab, Button>();
        float bizRefreshTimer;
        bool bizDirty = true;

        void BuildWindows()
        {
            // Business
            businessWindow = Ui.Box("panel", "window").Into(hudLayer);
            var header = Ui.Box("window__header").Into(businessWindow);
            Ui.Text("'O SISTEMA", header, "title", "gold").style.marginRight = 16;
            foreach (BizTab tab in System.Enum.GetValues(typeof(BizTab)))
            {
                var t = tab;
                bizTabButtons[t] = Ui.Btn(tab == BizTab.Supply ? "Supply & arms" : tab.ToString(), () =>
                {
                    bizTab = t;
                    bizDirty = true;
                    if (bizScroll != null) bizScroll.scrollOffset = Vector2.zero;
                    RefreshWindows();
                }, header, "btn--small");
            }
            Ui.Box("spacer").Into(header);
            Ui.Btn("✕", () => ToggleWindow(Window.Business), header, "btn--small");

            bizScroll = new ScrollView(ScrollViewMode.Vertical).Cls("window__body").Into(businessWindow);
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
                "<b>Camera</b>  WASD / arrows or middle-drag to pan · scroll or + / − to zoom\n" +
                "<b>Crew</b>  click or drag to select · click a portrait, double-click to find them\n" +
                "<b>Orders</b>  right-click to move (drafts them) · R to draft or release\n" +
                "<b>Fighting</b>  draft armed crew, right-click a rival to focus fire. Drafted crew take cover on their own. Cover (walls, cars, trees, benches, stalls) halves the chance of being hit.\n" +
                "<b>Time</b>  Space pause · 1 2 3 speed · debug: 4 ×30, 5 ×120, F8 skip 6 hours\n" +
                "<b>Debug</b>  F6 shoot selected · F7 +€200 · F10 rival hit squad · F11 stesa on your piazza\n" +
                "<b>Build</b>  open the BUILD / FURNISH tabs at the bottom. Walls drag as a room outline.\n" +
                "<b>Business</b>  B opens the panel: roles, wages, piazze hours, pizzo, supplier, pistols, rivals.\n" +
                "<b>Save</b>  F5 save · F9 load\n\n" +
                "<color=#A89F8E>You only see what your crew can see. Gunfire can be heard from anywhere; civilians run from it, and it brings the police.</color>",
                hb, "wrap");
            helpWindow.Show(false);
        }

        void ToggleWindow(Window w)
        {
            openWindow = openWindow == w ? Window.None : w;
            businessWindow.Show(openWindow == Window.Business);
            helpWindow.Show(openWindow == Window.Help);
            bizDirty = true;
            if (game != null) RefreshWindows();
        }

        void RefreshWindows()
        {
            if (openWindow != Window.Business) return;
            foreach (var kv in bizTabButtons) kv.Value.Toggle("btn--on", kv.Key == bizTab);

            bizRefreshTimer += RefreshSeconds;
            if (!bizDirty && bizRefreshTimer < 1f) return;
            bizRefreshTimer = 0f;
            bizDirty = false;

            var scroll = bizScroll.scrollOffset;
            bizBody.Clear();
            switch (bizTab)
            {
                case BizTab.Crew: CrewTab(); break;
                case BizTab.Piazze: PiazzeTab(); break;
                case BizTab.Pizzo: PizzoTab(); break;
                case BizTab.Supply: SupplyTab(); break;
                case BizTab.Rivals: RivalsTab(); break;
            }
            bizScroll.scrollOffset = scroll;
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
            Ui.Text($"Treasury <b>€{game.Stock.Cash}</b> · weekly mesata and family support <b>€{org.WeeklyWageBill(game)}</b>, paid Sunday at 12:00", bizBody, "wrap");
            long now = game.Ticks.TicksGame;
            foreach (var p in game.Pawns.Crew.Where(p => !p.Dead))
            {
                var row = Ui.Box("table-row").Into(bizBody);
                Avatar(p, row, big: false);
                var names = Ui.Box("col").Into(row);
                names.style.width = 150;
                Ui.Text(p.IsBoss ? $"<color=#E8C26A>{p.DisplayName}</color>" : $"<b>{p.DisplayName}</b>", names);
                Ui.Text(ShortStatus(p, now), names, "small", "muted");
                var pawn = p;
                Ui.Btn(RoleLabel(p.Role), Do(() => CycleRole(pawn)), row, "btn--small").style.width = 120;
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
            Ui.Text($"Paying businesses bring in <b>€{weekly}</b> a week. Your collector visits them; refusals can be punished.", bizBody, "wrap");
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
                if (b.Status == BusinessStatus.Free) Ui.Btn($"Impose €{b.WeeklyPizzo}", Do(() => org.Target(biz)), row, "btn--small");
                if (b.Status == BusinessStatus.Refusing)
                {
                    Ui.Btn("Ask again", Do(() => org.Target(biz)), row, "btn--small");
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
            Ui.Text("SUPPLIER", bizBody, "section");
            Ui.Text($"Product at the basso: <b>{game.Stock.Product}</b> units · wholesale €{Prices.ProductWholesale}, street €{Prices.ProductStreet}", bizBody);
            if (org.PendingOrderUnits > 0)
                Ui.Text($"An order of {org.PendingOrderUnits} units arrives on day {org.PendingOrderDay} at 08:00.", bizBody, "muted");
            else
            {
                var row = Ui.Row(bizBody);
                foreach (int units in new[] { 20, 50, 100 })
                {
                    int u = units;
                    Ui.Btn($"Order {u} (€{u * Prices.ProductWholesale})", Do(() =>
                    {
                        if (!org.OrderProduct(u, game, out var error)) game.Log.Add(error, MessageKind.Bad);
                    }), row);
                }
            }

            Ui.Text("ARMS", bizBody, "section");
            Ui.Text($"Pistols in the stash: <b>{game.Stock.Pistols}</b>. Armed crew defend themselves and fight when drafted, but get longer sentences if arrested.", bizBody, "wrap");
            Ui.Btn($"Buy a pistol (€{Prices.Pistol})", Do(() =>
            {
                if (game.Stock.Cash < Prices.Pistol) game.Log.Add($"A pistol costs €{Prices.Pistol}.", MessageKind.Bad);
                else
                {
                    game.Stock.Cash -= Prices.Pistol;
                    game.Stock.Pistols++;
                    game.Log.Add("A pistol, wrapped in a cloth, now sits in the stash.");
                }
            }), bizBody).style.alignSelf = Align.FlexStart;
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
