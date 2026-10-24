using System;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>
    /// F2: a cheat panel for testing. Money, respect, heat, evidence, rank, lavoretti, favours, the clans,
    /// every kind of stock, the boss's body, the crew, time, Don Saverio. The game keeps running behind it.
    /// </summary>
    public sealed partial class Hud
    {
        VisualElement debugPanel, debugBody;
        Label debugStatus;

        void ToggleDebug()
        {
            if (game == null) return;
            if (debugPanel == null)
            {
                debugPanel = Ui.Box("panel", "debug-panel").Into(hudLayer);
                var head = Ui.Row(debugPanel);
                Ui.Text("DEBUG", head, "title", "gold");
                Ui.Box("spacer").Into(head);
                Ui.Btn("Close  [F2]", () => debugPanel.Show(false), head, "btn--small");
                debugStatus = Ui.Text("", debugPanel, "small", "wrap");
                var scroll = new ScrollView(ScrollViewMode.Vertical);
                scroll.AddToClassList("debug-scroll");
                debugPanel.Add(scroll);
                debugBody = scroll.contentContainer;
                debugPanel.Show(false);
            }
            bool show = debugPanel.style.display == DisplayStyle.None;
            debugPanel.Show(show);
            if (show) BuildDebug();
        }

        void DebugRefreshStatus()
        {
            if (debugPanel == null || debugPanel.style.display == DisplayStyle.None || game == null) return;
            var o = game.Org;
            var p = game.Player;
            var zone = p != null ? game.Territory.ZoneAt(p.Cell) : null;
            debugStatus.text = $"€{game.Stock.Cash} · pockets: {p?.PocketText ?? "-"} · respect {o.Respect:0} · {o.RankName} · lavoretti {game.Contracts.Completed} · evidence {o.Evidence:0}" +
                               (zone != null ? $" · heat in {zone.Name} {zone.Heat:0}" : "") + $" · crew {game.Pawns.Crew.Count}";
        }

        void BuildDebug()
        {
            debugBody.Clear();
            var o = game.Org;
            var s = game.Stock;

            void Section(string title) => Ui.Text(title, debugBody, "section");

            VisualElement Line(string label)
            {
                var row = Ui.Row(debugBody);
                row.AddToClassList("debug-row");
                Ui.Text(label, row, "small", "debug-label");
                return row;
            }

            void B(VisualElement row, string text, Action act) =>
                Ui.Btn(text, () => { act(); game.Log.Note($"[Debug] {text}"); DebugRefreshStatus(); RefreshAll(); }, row, "btn--small", "debug-btn");

            void Steps(string label, Action<int> add, params int[] steps)
            {
                var row = Line(label);
                foreach (int n in steps) B(row, n > 0 ? $"+{n}" : $"{n}", () => add(n));
            }

            Section("MONEY AND NAME");
            Steps("Cash €", n => s.Cash = Math.Max(0, s.Cash + n), -1000, -100, 100, 1000, 10000);
            Steps("Respect", n => o.Respect = Mathf.Clamp(o.Respect + n, 0f, 100f), -10, -1, 1, 10, 50);
            Steps("Lavoretti done", n => game.Contracts.Completed = Math.Max(0, game.Contracts.Completed + n), -1, 1, 5);
            var rank = Line("Rank");
            for (int r = 0; r < Organization.RankNames.Length; r++)
            {
                int rr = r;
                B(rank, Organization.RankNames[r], () =>
                {
                    o.DevSetRank(rr);
                    if (game.Player != null) game.Player.Titled = rr >= Organization.RankNames.Length - 1;
                });
            }

            Section("POLICE");
            Steps("Heat here", n =>
            {
                var z = game.Player != null ? game.Territory.ZoneAt(game.Player.Cell) : null;
                if (z != null) z.Heat = Mathf.Clamp(z.Heat + n, 0f, 100f);
            }, -20, 20);
            var heat = Line("Heat everywhere");
            B(heat, "Clear all", () => { foreach (var z in game.Territory.Zones) z.Heat = 0f; });
            B(heat, "+30 all", () => { foreach (var z in game.Territory.Zones) z.Heat = Mathf.Min(100f, z.Heat + 30f); });
            Steps("Evidence", n => o.Evidence = Mathf.Clamp(o.Evidence + n, 0f, 100f), -25, -5, 5, 25);

            Section("FAVOURS");
            var fav = Line("Neighbours owe you");
            B(fav, "+1 each", () => { foreach (var nb in game.Favours.S.Neighbours) nb.Owes++; });
            B(fav, "+5 each", () => { foreach (var nb in game.Favours.S.Neighbours) nb.Owes += 5; });
            B(fav, "Clear", () => { foreach (var nb in game.Favours.S.Neighbours) { nb.Owes = 0; nb.Grudge = 0; } });

            Section("CLANS");
            foreach (var clan in game.Clans)
            {
                var c = clan;
                var row = Line(c.Name);
                B(row, "Calm", () => c.Hostility = 0f);
                B(row, "Angry", () => c.Hostility = Mathf.Min(100f, c.Hostility + 40f));
                B(row, "+€5000", () => c.Cash += 5000);
                B(row, "−soldiers", () => c.Soldiers = Math.Max(0, c.Soldiers - 3));
            }
            if (!o.SaverioGone)
            {
                var sav = Line("Don Saverio");
                B(sav, "Hand me his corner", () => o.DevTakeSaverio(game));
            }

            Section("STOCK");
            Steps("Doses ready", n => s.Product = Math.Max(0, s.Product + n), -10, 10, 50);
            Steps("Product (g, unbagged)", n => s.BulkDg = Math.Max(0, s.BulkDg + n * 10), -10, 10, 100);
            Steps("Bags", n => s.Baggies = Math.Max(0, s.Baggies + n), 20, 100);
            Steps("Packs of ROSSE", n => s.Cigarettes = Math.Max(0, s.Cigarettes + n), -10, 10, 50);
            Steps("Tobacco g", n => s.TobaccoG = Math.Max(0, s.TobaccoG + n), 100, 500);
            Steps("Papers", n => s.Papers = Math.Max(0, s.Papers + n), 100, 500);
            Steps("Empty packs", n => s.EmptyPacks = Math.Max(0, s.EmptyPacks + n), 10, 50);
            Steps("Fakes", n => o.H.fakes = Math.Max(0, o.H.fakes + n), 10, 50);
            Steps("Pistols", n => s.Pistols = Math.Max(0, s.Pistols + n), -1, 1, 5);
            Steps("Scooters", n => s.Scooters = Math.Max(0, s.Scooters + n), -1, 1);
            Steps("Medicine", n => s.Medicine = Math.Max(0, s.Medicine + n), 5);
            Steps("Meals / groceries", n => { s.Meals += n; s.Groceries += n; }, 10);
            var pocket = Line("Your pockets");
            B(pocket, "Fill from stash", () => { if (game.Player != null) o.FillPockets(game.Player, game); });
            B(pocket, "+10 doses", () => { if (game.Player != null) game.Player.PocketDoses += 10; });
            B(pocket, "+10 packs", () => { if (game.Player != null) game.Player.PocketPacks += 10; });
            B(pocket, "Empty", () => { if (game.Player != null) game.Player.PocketDoses = game.Player.PocketPacks = game.Player.PocketFakes = 0; });
            var gear = Line("Gear");
            B(gear, "Digital scale", () => s.DigitalScale = !s.DigitalScale);
            B(gear, "Heat sealer", () => s.HeatSealer = !s.HeatSealer);
            B(gear, "Rolling machine", () => s.RollingMachine = !s.RollingMachine);
            var sup = Line("Supplier");
            B(sup, o.SupplierIntroduced ? "Forget the supplier" : "Introduce the supplier", () => o.SupplierIntroduced = !o.SupplierIntroduced);

            Section("THE BOSS");
            var me = Line("Body");
            B(me, "Heal", () => { var p = game.Player; if (p == null) return; p.Health.Injuries.Clear(); p.Health.BloodLoss = 0f; });
            B(me, "Fed and rested", () => { var p = game.Player; if (p == null) return; p.Needs.Food = 1f; p.Needs.Rest = 1f; });
            B(me, "Exhausted", () => { var p = game.Player; if (p != null) p.Needs.Rest = 0.02f; });
            B(me, "Vest", () => { var p = game.Player; if (p != null) p.Armor = Pawn.MaxArmor; });
            B(me, "Armed", () => { var p = game.Player; if (p == null) return; p.Armed = true; p.Ammo = Pawn.MagazineSize; });

            Section("CREW");
            var crew = Line("Crew");
            B(crew, "Recruit one (free)", () => { o.Recruit(game, out var err, free: true); if (err != null) game.Log.Add(err, MessageKind.Bad); });
            B(crew, "Heal everyone", () => { foreach (var c in game.Pawns.Crew) { c.Health.Injuries.Clear(); c.Health.BloodLoss = 0f; } });
            B(crew, "Feed and rest all", () => { foreach (var c in game.Pawns.Crew) { c.Needs.Food = 1f; c.Needs.Rest = 1f; } });

            Section("TIME");
            var time = Line("Skip ahead");
            B(time, "+1 hour", () => game.Ticks.Advance(GameClock.TicksPerHour));
            B(time, "+6 hours", () => game.Ticks.Advance(GameClock.TicksPerHour * 6));
            B(time, "+1 day", () => game.Ticks.Advance(GameClock.TicksPerDay));

            DebugRefreshStatus();
        }
    }
}
