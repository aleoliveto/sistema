using System.Linq;
using OSistema.Core;
using OSistema.Crime;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>
    /// The strip across the top, on one line: time and speed, the family's figures as painted icons and
    /// numbers, Gennaro's body, and the boss with the family's rank. Exact figures and what they mean are on hover.
    /// </summary>
    public sealed partial class Hud
    {
        Label timeLabel, dayLabel, debugSpeed;
        VisualElement topBar, topVitals;
        Icon dayIcon;
        readonly Button[] speedButtons = new Button[4];
        Label dirtyValue, respectValue, heatValue, crewValue, turfValue, piazzeValue, packsValue, dosesValue;
        Label bossLabel, alarmBadge, rankBadge;
        VisualElement rankFill, hostilityFill, packsChip, dosesChip;

        /// <summary>
        /// One line across the top: the clock, then each figure as a painted icon and a number (or a short
        /// bar), then Gennaro's body, then the boss with the family's rank. What each one means is on hover.
        /// </summary>
        void BuildTop()
        {
            var bar = Ui.Box("topbar").Into(hudLayer);
            topBar = bar;

            var clock = Ui.Box("tgroup").Into(bar);
            dayIcon = new Icon(IconKind.Sun, 22).Into(clock);
            timeLabel = Ui.Text("", clock, "t-time");
            dayLabel = Ui.Text("", clock, "t-day");
            var speedIcons = new[] { IconKind.Pause, IconKind.Play, IconKind.Fast, IconKind.Faster };
            for (int i = 0; i < 4; i++)
            {
                int s = i;
                var b = Ui.Btn("", () =>
                {
                    if (s == 0) game.Ticks.TogglePause();
                    else game.Ticks.SetSpeed(s);
                }, clock, "speed-btn");
                new Icon(speedIcons[i], 11).Into(b);
                speedButtons[i] = b;
                Tip(b, () => s == 0 ? "Pause (Space)" : $"Speed {s} (key {s})");
            }
            debugSpeed = Ui.Text("", clock, "bad", "small", "bold");

            var figures = Ui.Box("tgroup").Into(bar);
            dirtyValue = Fig(figures, "icon_money", IconKind.Dirty, () =>
                $"Dirty €{game.Stock.Cash}: what crime pays. Clean €{game.Stock.Clean}: legal and laundered, for anything from €{OSistema.World.Prices.CleanMoneyThreshold} up.\nSunday's mesata: €{game.Org.WeeklyWageBill(game)}.");
            respectValue = Fig(figures, "icon_respect", IconKind.Respect, () => $"Respect {game.Org.Respect:0}: how much the rione fears and honours the family.");
            heatValue = Fig(figures, "icon_heat", IconKind.Paper, () => $"Evidence against the boss {game.Org.Evidence:0}/100. At 100, an arrest warrant.");
            crewValue = Fig(figures, "icon_crew", IconKind.Business, () => $"People in the family, and how many your rank can keep. Wages €{game.Org.WeeklyWageBill(game)} a week.");
            turfValue = Fig(figures, "icon_territory", IconKind.House, () => $"Rioni you hold: {string.Join(", ", game.Org.Turf(game).Select(z => z.Name))}");
            piazzeValue = Fig(figures, "icon_dose", IconKind.Product, () => $"Piazze open: {game.Org.Squares.Count} of {game.Org.MaxSquaresForRank} at your rank.");
            piazzeValue.parent.style.display = DisplayStyle.None;   // folded into territory below
            packsValue = Fig(figures, "icon_rosse", IconKind.Business, () =>
                $"Packs of ROSSE: {game.Stock.Cigarettes} in the stash, {game.Player?.PocketPacks ?? 0} in your pockets. Tobacco {game.Stock.TobaccoG} g, papers {game.Stock.Papers}, empty packs {game.Stock.EmptyPacks}.");
            packsChip = packsValue.parent;
            dosesValue = Fig(figures, "icon_dose", IconKind.Product, () => $"Doses: {game.Stock.Product} in the stash, {game.Player?.PocketDoses ?? 0} in your pockets, and {game.Stock.BulkDg / 10f:0.0} g still to cut.\nFill your pockets with E at home; you sell from them, and the police take them if you're caught.");
            dosesChip = dosesValue.parent;
            hostilityFill = FigBar(figures, "icon_clans", IconKind.Pistol, new Color(0.95f, 0.55f, 0.25f), () =>
            {
                var a = AngriestClan();
                return a == null || a.Destroyed ? "No clan left to fear." :
                    $"{a.Name}: hostility {a.Hostility:0}/100. Above 40: stese. Above 55: they come for your piazze. Above 70: ambushes.";
            });

            topVitals = Ui.Box("tgroup").Into(bar);   // Gennaro's body, filled in by Hud.Vitals

            Ui.Box("spacer").Into(bar);
            var right = Ui.Box("tgroup", "tgroup--last").Into(bar);
            alarmBadge = Ui.Text("", right, "badge", "badge--bad");
            bossLabel = Ui.Text("", right, "t-boss");
            var rank = Ui.Box("rank").Into(right);
            rankBadge = Ui.Text("", rank, "rank__name");
            var track = Ui.Box("rank__track").Into(rank);
            rankFill = Ui.Box("bar__fill").Into(track);
            rankFill.style.backgroundColor = new Color(0.91f, 0.76f, 0.42f);
            Tip(rank, () =>
            {
                var org = game.Org;
                if (org.Rank >= Organization.RankNames.Length - 1) return org.RankName.ToUpper();
                return $"{Organization.RankNames[org.Rank + 1].ToUpper()}  {org.RankProgress(game) * 100f:0}%";
            });
        }

        /// <summary>A painted icon and a number, one line.</summary>
        Label Fig(VisualElement parent, string art, IconKind fallback, System.Func<string> tip)
        {
            var chip = Ui.Box("fig").Into(parent);
            if (Ui.ArtIcon(art, 26, chip) == null) new Icon(fallback, 20).Into(chip);
            var v = Ui.Text("", chip, "fig__value");
            Tip(chip, tip);
            return v;
        }

        /// <summary>A painted icon and a short bar, one line.</summary>
        VisualElement FigBar(VisualElement parent, string art, IconKind fallback, Color color, System.Func<string> tip)
        {
            var chip = Ui.Box("fig").Into(parent);
            if (Ui.ArtIcon(art, 26, chip) == null) new Icon(fallback, 20).Into(chip);
            var track = Ui.Box("fig__track").Into(chip);
            var fill = Ui.Box("bar__fill").Into(track);
            fill.style.backgroundColor = color;
            Tip(chip, tip);
            return fill;
        }

        static string Money(int v) => v >= 100000 ? $"€{v / 1000}k" : v >= 10000 ? $"€{v / 1000f:0.0}k" : $"€{v}";

        void RefreshTop()
        {
            var clock = game.Clock;
            dayIcon.Set(clock.Daylight > 0.35f ? IconKind.Sun : IconKind.Moon);
            timeLabel.text = clock.TimeString;
            dayLabel.text = $"DAY {clock.Day} · {clock.Weekday.Substring(0, 3).ToUpper()}{(clock.IsPayday ? " · <color=#E8C26A>PAYDAY</color>" : "")}";
            for (int i = 0; i < 4; i++) speedButtons[i].Toggle("btn--on", game.Ticks.Speed == i);
            debugSpeed.text = game.Ticks.Speed >= TickManager.NormalSpeedCount ? $" ×{TickManager.SpeedMultipliers[game.Ticks.Speed]}" : "";

            var s = game.Stock;
            var org = game.Org;
            dirtyValue.text = Money(s.Cash) + (s.Clean > 0 ? $" <color=#9C9180>+{Money(s.Clean)}</color>" : "");
            dirtyValue.EnableInClassList("bad", s.Cash + s.Clean < org.WeeklyWageBill(game));
            respectValue.text = $"{org.Respect:0}";
            heatValue.text = $"{org.Evidence:0}%";
            heatValue.EnableInClassList("bad", org.Evidence >= 60f);
            int crew = 0;
            foreach (var p in game.Pawns.Crew) if (!p.Dead && !p.Departed) crew++;
            crewValue.text = $"{crew}/{org.MaxCrewForRank}";
            turfValue.text = org.Squares.Count > 0 ? $"{org.Turf(game).Count} <color=#9C9180>· {org.Squares.Count}P</color>" : $"{org.Turf(game).Count}";
            // Stash, then what's on you.
            var me = game.Player;
            packsValue.text = me != null && me.PocketPacks > 0 ? $"{s.Cigarettes}+{me.PocketPacks}" : $"{s.Cigarettes}";
            dosesValue.text = me != null && me.PocketDoses > 0 ? $"{s.Product}+{me.PocketDoses}" : $"{s.Product}";
            dosesChip.Show(s.Product > 0 || s.BulkDg > 0 || org.Squares.Count > 0);
            var angriest = AngriestClan();
            Ui.SetFill(hostilityFill, angriest == null || angriest.Destroyed ? 0f : angriest.Hostility / 100f);

            string alarm = org.Hiding ? "LATITANZA" : org.WarrantActive ? "WARRANT" : null;
            alarmBadge.text = alarm ?? "";
            alarmBadge.Show(alarm != null);
            var boss = org.Boss(game);
            bossLabel.text = boss != null ? $"{boss.DisplayName} {boss.LastName}" + (org.PendingPizzini > 0 ? $"  <color=#E8C26A>✉ {org.PendingPizzini}</color>" : "")
                                          : "<color=#F07A6E>No boss</color>";
            rankBadge.text = org.RankName.ToUpper();
            Ui.SetFill(rankFill, org.RankProgress(game));
        }
        /// <summary>The clan most set against the family right now.</summary>
        OSistema.Crime.RivalClan AngriestClan()
        {
            OSistema.Crime.RivalClan best = game.Rival;
            foreach (var c in game.Clans)
                if (!c.Destroyed && (best.Destroyed || c.Hostility > best.Hostility)) best = c;
            return best;
        }
    }
}
