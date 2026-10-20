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
        Label dirtyValue, cleanValue, productValue, foodChip, medicineValue, respectValue, scooterValue, cigaretteValue;
        VisualElement scooterChip, cigaretteChip, medicineChip;
        Label rankBadge, bossLabel, turfValue, piazzeValue, alarmBadge;
        VisualElement rankFill, evidenceFill, hostilityFill;

        void BuildTop()
        {
            var bar = Ui.Box("topbar").Into(hudLayer);
            topBar = bar;
            bar.style.backgroundImage = new StyleBackground(Ui.Gradient(new Color(0.14f, 0.11f, 0.08f, 0.97f), new Color(0.06f, 0.05f, 0.04f, 0.97f)));

            // Time and speed
            var left = Ui.Box("topbar__section").Into(bar);
            dayIcon = new Icon(IconKind.Sun, 20).Into(left);
            timeLabel = Ui.Text("", left, "clock__time");
            dayLabel = Ui.Text("", left, "clock__day");
            var speedIcons = new[] { IconKind.Pause, IconKind.Play, IconKind.Fast, IconKind.Faster };
            for (int i = 0; i < 4; i++)
            {
                int s = i;
                var b = Ui.Btn("", () =>
                {
                    if (s == 0) game.Ticks.TogglePause();
                    else game.Ticks.SetSpeed(s);
                }, left, "speed-btn");
                new Icon(speedIcons[i], 12).Into(b);
                speedButtons[i] = b;
                Tip(b, () => s == 0 ? "Pause (Space)" : $"Speed {s} (key {s})");
            }
            debugSpeed = Ui.Text("", left, "bad", "small", "bold");

            // Money
            var money = Group(bar, "MONEY", () =>
                $"Dirty €{game.Stock.Cash}: what crime pays. Fine for drugs, guns, wages and small things.\n" +
                $"Clean €{game.Stock.Clean}: legal income and laundered cash, needed for property and anything from €{OSistema.World.Prices.CleanMoneyThreshold} up.\n" +
                $"Sunday at 12:00 the crew's mesata comes out: €{game.Org.WeeklyWageBill(game)}.");
            dirtyValue = Value(money, IconKind.Dirty);
            cleanValue = Value(money, IconKind.Clean);

            // Reputation
            var rep = Group(bar, "REPUTATION", () =>
                $"{game.Org.RankName}. Respect {game.Org.Respect:0}: how much the rione fears and honours the family.\n" +
                (game.Org.NextRankGoal(game) ?? "The top of the System.") +
                $"\nEvidence against the boss {game.Org.Evidence:0}/100: at 100 an arrest warrant is issued.");
            rankBadge = Ui.Text("", rep, "badge");
            respectValue = Value(rep, IconKind.Respect);
            var repBars = Ui.Box("col").Into(rep);
            repBars.style.marginLeft = 6;
            rankFill = MiniBar(repBars, "RANK", new Color(0.91f, 0.76f, 0.42f));
            evidenceFill = MiniBar(repBars, "POLICE", new Color(0.86f, 0.3f, 0.26f));
            alarmBadge = Ui.Text("", rep, "badge", "badge--bad");

            // Territory
            var turf = Group(bar, "TERRITORY", () =>
            {
                var a = AngriestClan();
                return $"Your turf: {string.Join(", ", game.Org.Turf(game).Select(z => z.Name))}" +
                       $"\nPiazze {game.Org.Squares.Count}/{game.Org.MaxSquaresForRank} at your rank." +
                       (a == null || a.Destroyed ? "" : $"\n{a.Name}: hostility {a.Hostility:0}/100, {a.Soldiers} soldiers. Above 40: stese. Above 55: they come for your piazze. Above 70: ambushes.");
            });
            turfValue = Value(turf, IconKind.House);
            piazzeValue = Value(turf, IconKind.Product);
            var turfBars = Ui.Box("col").Into(turf);
            turfBars.style.marginLeft = 6;
            hostilityFill = MiniBar(turfBars, "CLANS", new Color(0.95f, 0.55f, 0.25f));

            // Resources
            var res = Group(bar, "RESOURCES", () =>
                $"Doses ready for the piazze: {game.Stock.Product}, and {game.Stock.BulkDg / 10f:0.0} g still to cut.\n" +
                $"ROSSE packs {game.Stock.Cigarettes} · tobacco {game.Stock.TobaccoG} g, papers {game.Stock.Papers}, empty packs {game.Stock.EmptyPacks}.\n" +
                $"Meals {game.Stock.Meals} · groceries {game.Stock.Groceries} (each cooks two meals) · medicine {game.Stock.Medicine} · scooters {game.Stock.Scooters}.");
            cigaretteValue = Value(res, IconKind.Business);
            cigaretteChip = cigaretteValue.parent;
            productValue = Value(res, IconKind.Product);
            foodChip = Value(res, IconKind.Food);
            medicineValue = Value(res, IconKind.Medicine);
            medicineChip = medicineValue.parent;
            scooterValue = Value(res, IconKind.Scooter);
            scooterChip = scooterValue.parent;

            Ui.Box("spacer").Into(bar);
            var right = Ui.Box("topbar__section").Into(bar);
            bossLabel = Ui.Text("", right, "small", "muted");
            Tip(right, () => $"Generation {game.Org.Generation}" + (game.Org.PendingPizzini > 0 ? $" · {game.Org.PendingPizzini} pizzini waiting" : ""));
        }

        /// <summary>A captioned group in the top bar; returns the row its values go in.</summary>
        VisualElement Group(VisualElement bar, string caption, System.Func<string> tip)
        {
            var g = Ui.Box("tb-group").Into(bar);
            Ui.Text(caption, g, "tb-cap");
            var row = Ui.Box("tb-row").Into(g);
            Tip(g, tip);
            return row;
        }

        static Label Value(VisualElement parent, IconKind icon)
        {
            var chip = Ui.Box("chip").Into(parent);
            new Icon(icon, 14).Into(chip);
            return Ui.Text("", chip, "chip__value");
        }

        static VisualElement MiniBar(VisualElement parent, string label, Color color)
        {
            var row = Ui.Box("mini-bar").Into(parent);
            Ui.Text(label, row, "mini-bar__label");
            var track = Ui.Box("mini-bar__track").Into(row);
            var fill = Ui.Box("bar__fill").Into(track);
            fill.style.backgroundColor = color;
            return fill;
        }

        static void Divider(VisualElement parent) => Ui.Box("divider").Into(parent);

        Label Chip(VisualElement parent, IconKind icon, System.Func<string> tip)
        {
            var chip = Ui.Box("chip").Into(parent);
            new Icon(icon, 16).Into(chip);
            var value = Ui.Text("", chip, "chip__value");
            Tip(chip, tip);
            return value;
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
            cleanValue.text = Money(s.Clean);
            dirtyValue.EnableInClassList("bad", s.Cash + s.Clean < org.WeeklyWageBill(game));

            rankBadge.text = org.RankName.ToUpper();
            respectValue.text = $"{org.Respect:0}";
            Ui.SetFill(rankFill, org.RankProgress(game));
            Ui.SetFill(evidenceFill, org.Evidence / 100f);
            string alarm = org.Hiding ? "LATITANZA" : org.WarrantActive ? "WARRANT" : null;
            alarmBadge.text = alarm ?? "";
            alarmBadge.Show(alarm != null);

            turfValue.text = $"{org.Turf(game).Count}";
            piazzeValue.text = $"{org.Squares.Count}";
            var angriest = AngriestClan();
            Ui.SetFill(hostilityFill, angriest == null || angriest.Destroyed ? 0f : angriest.Hostility / 100f);

            cigaretteValue.text = s.Cigarettes.ToString();
            productValue.text = s.Product.ToString();
            foodChip.text = $"{s.Meals + s.Groceries * 2}";
            medicineValue.text = s.Medicine.ToString();
            medicineChip.Show(s.Medicine > 0);
            scooterValue.text = s.Scooters.ToString();
            scooterChip.Show(s.Scooters > 0);

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
