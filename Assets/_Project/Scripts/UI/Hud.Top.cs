using OSistema.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>
    /// One continuous strip across the top: time and speed on the left, money and supplies in the middle,
    /// the family's rank and boss on the right. Below it on the right, a small card with the two numbers
    /// that can end everything: evidence against the boss and the rivals' hostility.
    /// </summary>
    public sealed partial class Hud
    {
        Label timeLabel, dayLabel, debugSpeed;
        Icon dayIcon;
        readonly Button[] speedButtons = new Button[4];
        Label dirtyValue, cleanValue, productValue, foodChip, medicineValue, respectValue, scooterValue, cigaretteValue;
        VisualElement scooterChip, cigaretteChip;
        Label rankBadge, bossLabel;
        Label familyState, rankLabel;
        VisualElement evidenceFill, hostilityFill;

        void BuildTop()
        {
            var bar = Ui.Box("topbar").Into(hudLayer);
            bar.style.backgroundImage = new StyleBackground(Ui.Gradient(new Color(0.14f, 0.11f, 0.08f, 0.97f), new Color(0.06f, 0.05f, 0.04f, 0.97f)));

            // Left: time and speed
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

            // Middle: money and supplies
            var mid = Ui.Box("topbar__section", "grow").Into(bar);
            mid.style.justifyContent = Justify.Center;
            dirtyValue = Chip(mid, IconKind.Dirty, () => "Dirty money: what crime pays. Fine for drugs, guns, wages and small things. Launder it to spend on anything big.");
            cleanValue = Chip(mid, IconKind.Clean, () => $"Clean money: legal income and laundered cash. Needed for property and anything from €{OSistema.World.Prices.CleanMoneyThreshold} up.");
            Divider(mid);
            productValue = Chip(mid, IconKind.Product, () => $"Doses ready for the piazze, and product still to cut ({game.Stock.BulkDg / 10f:0.0} g). Bought by the etto or kilo from Zi Tore, bagged at the table.");
            foodChip = Chip(mid, IconKind.Food, () => $"Meals {game.Stock.Meals} · groceries {game.Stock.Groceries} (each cooks two meals).");
            medicineValue = Chip(mid, IconKind.Medicine, () => "Medicine from the farmacia: better treatment for wounds.");
            scooterValue = Chip(mid, IconKind.Scooter, () => "The family's scooters. Crew ride them on long trips.");
            scooterChip = scooterValue.parent;
            cigaretteValue = Chip(mid, IconKind.Business, () => $"Packs of ROSSE ready to sell. Tobacco {game.Stock.TobaccoG} g, papers {game.Stock.Papers}, empty packs {game.Stock.EmptyPacks}.");
            cigaretteChip = cigaretteValue.parent;
            Divider(mid);
            respectValue = Chip(mid, IconKind.Respect, () => "Respect: how much the rione fears and honours the family.");

            // Right: rank and boss
            var right = Ui.Box("topbar__section").Into(bar);
            rankBadge = Ui.Text("", right, "badge");
            bossLabel = Ui.Text("", right, "bold");
            Tip(right, () => game.Org.NextRankGoal(game) ?? "The top of the System.");

            // The family card
            var fam = Ui.Box("panel", "family").Into(hudLayer);
            rankLabel = Ui.Text("", fam, "small", "wrap");
            familyState = Ui.Text("", fam, "small", "muted");
            var bars = Ui.Row(fam);
            var ev = Ui.Box("col", "grow").Into(bars);
            ev.style.marginRight = 8;
            Ui.Text("EVIDENCE", ev, "tiny");
            (_, evidenceFill) = Ui.Bar("", new Color(0.86f, 0.3f, 0.26f), ev, thin: true);
            var ho = Ui.Box("col", "grow").Into(bars);
            Ui.Text("RIVAL HOSTILITY", ho, "tiny");
            (_, hostilityFill) = Ui.Bar("", new Color(0.95f, 0.55f, 0.25f), ho, thin: true);
            Tip(fam, () =>
                $"Evidence against the boss: {game.Org.Evidence:0}/100. At 100 an arrest warrant is issued.\n" +
                $"{game.Rival.Name}: hostility {game.Rival.Hostility:0}/100, {game.Rival.Soldiers} soldiers. Above 40: stese. Above 70: ambushes.");
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

        void RefreshTop()
        {
            var clock = game.Clock;
            dayIcon.Set(clock.Daylight > 0.35f ? IconKind.Sun : IconKind.Moon);
            timeLabel.text = clock.TimeString;
            dayLabel.text = $"DAY {clock.Day} · {clock.Weekday.Substring(0, 3).ToUpper()}{(clock.IsPayday ? " · <color=#E8C26A>PAYDAY</color>" : "")}";
            for (int i = 0; i < 4; i++) speedButtons[i].Toggle("btn--on", game.Ticks.Speed == i);
            debugSpeed.text = game.Ticks.Speed >= TickManager.NormalSpeedCount ? $" ×{TickManager.SpeedMultipliers[game.Ticks.Speed]}" : "";

            var s = game.Stock;
            dirtyValue.text = $"€{s.Cash}";
            cleanValue.text = $"€{s.Clean}";
            productValue.text = s.BulkDg > 0 ? $"{s.Product} · {s.BulkDg / 10}g" : s.Product.ToString();
            foodChip.text = $"{s.Meals}+{s.Groceries * 2}";
            medicineValue.text = s.Medicine.ToString();
            scooterValue.text = s.Scooters.ToString();
            scooterChip.Show(s.Scooters > 0);
            cigaretteValue.text = s.Cigarettes.ToString();
            cigaretteChip.Show(s.Cigarettes > 0 || s.TobaccoG > 0);
            respectValue.text = $"{game.Org.Respect:0}";

            var org = game.Org;
            var boss = org.Boss(game);
            rankBadge.text = org.RankName.ToUpper();
            bossLabel.text = boss != null ? $"{boss.DisplayName} {boss.LastName}" : "<color=#F07A6E>No boss</color>";

            string goal = org.NextRankGoal(game);
            rankLabel.text = goal != null ? $"<color=#E8C26A>{goal}</color>" : "<color=#E8C26A>At the top of the System.</color>";
            familyState.text = $"Generation {org.Generation} · turf: {org.Turf(game).Count} rioni" +
                (org.Hiding ? $" · <color=#F2B866>latitanza day {clock.Day - org.HidingSinceDay + 1}</color>" :
                 org.WarrantActive ? " · <color=#F07A6E>arrest warrant</color>" : "") +
                (org.PendingPizzini > 0 ? $" · {org.PendingPizzini} pizzini" : "");
            Ui.SetFill(evidenceFill, org.Evidence / 100f);
            Ui.SetFill(hostilityFill, game.Rival.Destroyed ? 0f : game.Rival.Hostility / 100f);
        }
    }
}
