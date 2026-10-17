using OSistema.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>Clock and speed (top left), resources (top centre), the family (top right).</summary>
    public sealed partial class Hud
    {
        static readonly string[] SpeedLabels = { "II", ">", ">>", ">>>" };

        Label dayLabel, timeLabel, debugSpeed;
        readonly Button[] speedButtons = new Button[4];
        Label cashValue, productValue, foodChip, medicineValue, respectValue;
        Label familyTitle, familyState;
        VisualElement evidenceFill, hostilityFill;

        void BuildTop()
        {
            // Clock: one compact row.
            var clock = Ui.Box("panel", "clock").Into(hudLayer);
            timeLabel = Ui.Text("", clock, "clock__time");
            dayLabel = Ui.Text("", clock, "clock__day");
            for (int i = 0; i < 4; i++)
            {
                int s = i;
                speedButtons[i] = Ui.Btn(SpeedLabels[i], () =>
                {
                    if (s == 0) game.Ticks.TogglePause();
                    else game.Ticks.SetSpeed(s);
                }, clock, "speed-btn");
                Tip(speedButtons[i], () => s == 0 ? "Pause (Space)" : $"Speed {s} (key {s})");
            }
            debugSpeed = Ui.Text("", clock, "bad", "small", "bold");

            // Resources
            var res = Ui.Box("resources").Into(hudLayer);
            res.pickingMode = PickingMode.Ignore;
            cashValue = Chip(res, new Color(0.91f, 0.76f, 0.42f), "", () => "The clan's treasury, 'a bacinella. Wages are paid from it every Sunday.");
            productValue = Chip(res, new Color(0.8f, 0.4f, 0.9f), "roba", () => "Units of product at the basso, for the piazze.");
            foodChip = Chip(res, new Color(0.9f, 0.55f, 0.3f), "food", () => $"Meals {game.Stock.Meals} · groceries {game.Stock.Groceries} (each cooks two meals).");
            medicineValue = Chip(res, new Color(0.45f, 0.8f, 0.7f), "medicine", () => "Medicine from the farmacia: better treatment for wounds.");
            respectValue = Chip(res, new Color(0.95f, 0.95f, 0.95f), "respect", () => "How much the rione fears and honours the family.");

            // Family: who leads, and the two numbers that can end the family.
            var fam = Ui.Box("panel", "family").Into(hudLayer);
            familyTitle = Ui.Text("", fam, "bold");
            familyState = Ui.Text("", fam, "small", "muted");
            var bars = Ui.Row(fam);
            var ev = Ui.Box("col", "grow").Into(bars);
            ev.style.marginRight = 6;
            Ui.Text("evidence", ev, "small", "muted");
            (_, evidenceFill) = Ui.Bar("", new Color(0.86f, 0.3f, 0.26f), ev, thin: true);
            var ho = Ui.Box("col", "grow").Into(bars);
            Ui.Text("rival hostility", ho, "small", "muted");
            (_, hostilityFill) = Ui.Bar("", new Color(0.95f, 0.55f, 0.25f), ho, thin: true);
            Tip(fam, () =>
                $"Evidence against the boss: {game.Org.Evidence:0}/100. At 100 an arrest warrant is issued.\n" +
                $"{game.Rival.Name}: hostility {game.Rival.Hostility:0}/100, {game.Rival.Soldiers} soldiers. Above 40: stese. Above 70: ambushes.");
        }

        Label Chip(VisualElement parent, Color dot, string label, System.Func<string> tip)
        {
            var chip = Ui.Box("chip").Into(parent);
            var d = Ui.Box("chip__dot").Into(chip);
            d.style.backgroundColor = dot;
            var value = Ui.Text("", chip, "chip__value");
            if (label.Length > 0) Ui.Text(label, chip, "chip__label");
            Tip(chip, tip);
            return value;
        }

        void RefreshTop()
        {
            var clock = game.Clock;
            timeLabel.text = clock.TimeString;
            dayLabel.text = $"Day {clock.Day} · {clock.Weekday.Substring(0, 3)}{(clock.IsPayday ? " · <color=#E8C26A>payday</color>" : "")}";
            for (int i = 0; i < 4; i++) speedButtons[i].Toggle("btn--on", game.Ticks.Speed == i);
            debugSpeed.text = game.Ticks.Speed >= TickManager.NormalSpeedCount ? $" ×{TickManager.SpeedMultipliers[game.Ticks.Speed]}" : "";

            var s = game.Stock;
            cashValue.text = $"€{s.Cash}";
            productValue.text = s.Product.ToString();
            foodChip.text = $"{s.Meals}+{s.Groceries * 2}";
            medicineValue.text = s.Medicine.ToString();
            respectValue.text = $"{game.Org.Respect:0}";

            var org = game.Org;
            var boss = org.Boss(game);
            familyTitle.text = boss != null ? $"<color=#E8C26A>{boss.DisplayName}</color> {boss.LastName}" : "<color=#F07A6E>No boss</color>";
            familyState.text = $"Generation {org.Generation}" +
                (org.Hiding ? $" · <color=#F2B866>latitanza day {clock.Day - org.HidingSinceDay + 1}</color>" :
                 org.WarrantActive ? " · <color=#F07A6E>arrest warrant</color>" : "") +
                (org.PendingPizzini > 0 ? $" · {org.PendingPizzini} pizzini" : "");
            Ui.SetFill(evidenceFill, org.Evidence / 100f);
            Ui.SetFill(hostilityFill, game.Rival.Destroyed ? 0f : game.Rival.Hostility / 100f);
        }
    }
}
