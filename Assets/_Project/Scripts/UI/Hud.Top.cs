using System.Linq;
using OSistema.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>
    /// The strip across the top, in clearly separate groups: time and speed, then MONEY, REPUTATION,
    /// TERRITORY and RESOURCES, each with a small caption. Exact figures and what they mean are on hover;
    /// the bar itself stays readable at a glance.
    /// </summary>
    public sealed partial class Hud
    {
        Label timeLabel, dayLabel, debugSpeed;
        VisualElement topBar;
        Icon dayIcon;
        readonly Button[] speedButtons = new Button[4];
        Label dirtyValue, cleanValue, respectValue, rankCaption, heatValue, crewValue, turfValue, turfSub, threatValue, suppliesValue, suppliesSub;
        Label bossLabel, alarmBadge;
        VisualElement rankFill, evidenceFill, hostilityFill;

        void BuildTop()
        {
            var bar = Ui.Box("topbar").Into(hudLayer);
            topBar = bar;
            bar.style.backgroundImage = new StyleBackground(Ui.Gradient(new Color(0.13f, 0.10f, 0.07f, 0.98f), new Color(0.05f, 0.04f, 0.03f, 0.98f)));

            // Time and speed
            var clock = Ui.Box("stat", "stat--clock").Into(bar);
            dayIcon = new Icon(IconKind.Sun, 26).Into(clock);
            var cc = Ui.Box("stat__col").Into(clock);
            timeLabel = Ui.Text("", cc, "stat__time");
            dayLabel = Ui.Text("", cc, "stat__cap");
            var speeds = Ui.Box("speeds").Into(clock);
            var speedIcons = new[] { IconKind.Pause, IconKind.Play, IconKind.Fast, IconKind.Faster };
            for (int i = 0; i < 4; i++)
            {
                int s = i;
                var b = Ui.Btn("", () =>
                {
                    if (s == 0) game.Ticks.TogglePause();
                    else game.Ticks.SetSpeed(s);
                }, speeds, "speed-btn");
                new Icon(speedIcons[i], 13).Into(b);
                speedButtons[i] = b;
                Tip(b, () => s == 0 ? "Pause (Space)" : $"Speed {s} (key {s})");
            }
            debugSpeed = Ui.Text("", clock, "bad", "small", "bold");

            dirtyValue = Stat(bar, IconKind.Dirty, "MONEY", out var moneyCol, () =>
                $"Dirty €{game.Stock.Cash}: what crime pays. Fine for drugs, guns, wages and small things.\n" +
                $"Clean €{game.Stock.Clean}: legal income and laundered cash, needed for property and anything from €{OSistema.World.Prices.CleanMoneyThreshold} up.\n" +
                $"Sunday at 12:00 the crew's mesata comes out: €{game.Org.WeeklyWageBill(game)}.");
            cleanValue = Ui.Text("", moneyCol, "stat__sub");

            respectValue = Stat(bar, IconKind.Respect, "RESPECT", out var repCol, () =>
                $"{game.Org.RankName}. Respect {game.Org.Respect:0}: how much the rione fears and honours the family.\n" + (game.Org.NextRankGoal(game) ?? "The top of the System."));
            rankCaption = repCol.Q<Label>(className: "stat__cap");
            rankFill = StatBar(repCol, new Color(0.91f, 0.76f, 0.42f));

            heatValue = Stat(bar, IconKind.Paper, "HEAT", out var heatCol, () =>
                $"Evidence against the boss: {game.Org.Evidence:0}/100. At 100 an arrest warrant is issued. Lie low, launder, keep your hands clean.");
            evidenceFill = StatBar(heatCol, new Color(0.88f, 0.32f, 0.26f));

            crewValue = Stat(bar, IconKind.Business, "CREW", out _, () =>
                $"People in the family, and how many your rank can keep. Wages €{game.Org.WeeklyWageBill(game)} a week.");

            turfValue = Stat(bar, IconKind.House, "TERRITORY", out var turfCol, () =>
                $"Your turf: {string.Join(", ", game.Org.Turf(game).Select(z => z.Name))}\nPiazze {game.Org.Squares.Count}/{game.Org.MaxSquaresForRank} at your rank.");
            turfSub = Ui.Text("", turfCol, "stat__sub");

            threatValue = Stat(bar, IconKind.Pistol, "CLANS", out var threatCol, () =>
            {
                var a = AngriestClan();
                return a == null || a.Destroyed ? "No clan left to fear." :
                    $"{a.Name}: hostility {a.Hostility:0}/100, {a.Soldiers} soldiers. Above 40: stese. Above 55: they come for your piazze. Above 70: ambushes.";
            });
            hostilityFill = StatBar(threatCol, new Color(0.95f, 0.55f, 0.25f));

            suppliesValue = Stat(bar, IconKind.Product, "SUPPLIES", out var supCol, () =>
                $"ROSSE packs {game.Stock.Cigarettes} · tobacco {game.Stock.TobaccoG} g, papers {game.Stock.Papers}, empty packs {game.Stock.EmptyPacks}.\n" +
                $"Doses {game.Stock.Product}, and {game.Stock.BulkDg / 10f:0.0} g still to cut.\n" +
                $"Meals {game.Stock.Meals} · groceries {game.Stock.Groceries} · medicine {game.Stock.Medicine} · scooters {game.Stock.Scooters}.");
            suppliesSub = Ui.Text("", supCol, "stat__sub");

            Ui.Box("spacer").Into(bar);
            var right = Ui.Box("stat", "stat--right").Into(bar);
            alarmBadge = Ui.Text("", right, "badge", "badge--bad");
            bossLabel = Ui.Text("", right, "stat__boss");
            Tip(right, () => $"Generation {game.Org.Generation}" + (game.Org.PendingPizzini > 0 ? $" · {game.Org.PendingPizzini} pizzini waiting" : ""));
        }

        /// <summary>One block of the top bar: an icon, a small caption, a big value. Returns the value label.</summary>
        Label Stat(VisualElement bar, IconKind icon, string caption, out VisualElement col, System.Func<string> tip)
        {
            var block = Ui.Box("stat").Into(bar);
            new Icon(icon, 24).Into(block).AddToClassList("stat__icon");
            col = Ui.Box("stat__col").Into(block);
            Ui.Text(caption, col, "stat__cap");
            var value = Ui.Text("", col, "stat__value");
            Tip(block, tip);
            return value;
        }

        static VisualElement StatBar(VisualElement col, Color color)
        {
            var track = Ui.Box("stat__bar").Into(col);
            var fill = Ui.Box("bar__fill").Into(track);
            fill.style.backgroundColor = color;
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
            dirtyValue.text = Money(s.Cash);
            dirtyValue.EnableInClassList("bad", s.Cash + s.Clean < org.WeeklyWageBill(game));
            cleanValue.text = s.Clean > 0 ? $"{Money(s.Clean)} clean" : $"wages {Money(org.WeeklyWageBill(game))}/wk";

            rankCaption.text = $"RESPECT · {org.RankName.ToUpper()}";
            respectValue.text = $"{org.Respect:0}";
            Ui.SetFill(rankFill, org.RankProgress(game));
            heatValue.text = $"{org.Evidence:0}%";
            Ui.SetFill(evidenceFill, org.Evidence / 100f);
            int crew = 0;
            foreach (var p in game.Pawns.Crew) if (!p.Dead && !p.Departed) crew++;
            crewValue.text = $"{crew}/{org.MaxCrewForRank}";
            turfValue.text = $"{org.Turf(game).Count}";
            turfSub.text = $"{org.Squares.Count} piazz{(org.Squares.Count == 1 ? "a" : "e")}";
            var angriest = AngriestClan();
            float h = angriest == null || angriest.Destroyed ? 0f : angriest.Hostility;
            threatValue.text = h >= 70f ? "<color=#F07A6E>Blood</color>" : h >= 40f ? "<color=#F2B866>Angry</color>" : "Calm";
            Ui.SetFill(hostilityFill, h / 100f);
            suppliesValue.text = $"{s.Cigarettes}";
            suppliesSub.text = $"packs · {s.Product} doses";

            string alarm = org.Hiding ? "LATITANZA" : org.WarrantActive ? "WARRANT" : null;
            alarmBadge.text = alarm ?? "";
            alarmBadge.Show(alarm != null);
            var boss = org.Boss(game);
            bossLabel.text = boss != null ? $"{boss.DisplayName} {boss.LastName}" + (org.PendingPizzini > 0 ? $"  <color=#E8C26A>✉ {org.PendingPizzini}</color>" : "")
                                          : "<color=#F07A6E>No boss</color>";
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
