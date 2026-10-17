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
        Label cashValue, productValue, mealsValue, groceriesValue, medicineValue, respectValue;
        Label familyTitle, familyState, rivalLabel;
        VisualElement evidenceFill, hostilityFill;
        Label evidenceValue, hostilityValue;

        void BuildTop()
        {
            // Clock
            var clock = Ui.Box("panel", "clock").Into(hudLayer);
            dayLabel = Ui.Text("", clock, "clock__day");
            var timeRow = Ui.Row(clock);
            timeLabel = Ui.Text("", timeRow, "clock__time");
            Ui.Box("spacer").Into(timeRow);
            debugSpeed = Ui.Text("", timeRow, "bad", "small", "bold");
            var speeds = Ui.Row(clock);
            for (int i = 0; i < 4; i++)
            {
                int s = i;
                speedButtons[i] = Ui.Btn(SpeedLabels[i], () =>
                {
                    if (s == 0) game.Ticks.TogglePause();
                    else game.Ticks.SetSpeed(s);
                }, speeds, "speed-btn");
                Tip(speedButtons[i], () => s == 0 ? "Pause (Space)" : $"Speed {s} (key {s})");
            }

            // Resources
            var res = Ui.Box("resources").Into(hudLayer);
            res.pickingMode = PickingMode.Ignore;
            cashValue = Chip(res, new Color(0.91f, 0.76f, 0.42f), "cash", () => "The clan's treasury, 'a bacinella. Wages are paid from it every Sunday.");
            productValue = Chip(res, new Color(0.8f, 0.4f, 0.9f), "product", () => "Units of product ('a roba) at the basso, for the piazze.");
            mealsValue = Chip(res, new Color(0.9f, 0.55f, 0.3f), "meals", () => "Cooked meals in the pantry.");
            groceriesValue = Chip(res, new Color(0.55f, 0.75f, 0.4f), "groceries", () => "Groceries: one unit cooks two meals.");
            medicineValue = Chip(res, new Color(0.45f, 0.8f, 0.7f), "medicine", () => "Medicine from the farmacia: better treatment for wounds.");
            respectValue = Chip(res, new Color(0.95f, 0.95f, 0.95f), "respect", () => "Respect: how much the rione fears and honours the family.");

            // Family
            var fam = Ui.Box("panel", "family").Into(hudLayer);
            familyTitle = Ui.Text("", fam, "title");
            familyState = Ui.Text("", fam, "small");
            (evidenceValue, evidenceFill) = Ui.Bar("Evidence against the boss", new Color(0.86f, 0.3f, 0.26f), fam);
            (hostilityValue, hostilityFill) = Ui.Bar("Rival hostility", new Color(0.95f, 0.55f, 0.25f), fam);
            rivalLabel = Ui.Text("", fam, "small", "muted");
            Tip(fam, () => "Evidence builds with arrests, informants, pizzo, arson and killings. At 100 a warrant is issued.\nRival hostility above 40 brings stese, above 70 ambushes.");
        }

        Label Chip(VisualElement parent, Color dot, string label, System.Func<string> tip)
        {
            var chip = Ui.Box("chip").Into(parent);
            var d = Ui.Box("chip__dot").Into(chip);
            d.style.backgroundColor = dot;
            var value = Ui.Text("", chip, "chip__value");
            Ui.Text(label, chip, "chip__label");
            Tip(chip, tip);
            return value;
        }

        void RefreshTop()
        {
            var clock = game.Clock;
            dayLabel.text = $"DAY {clock.Day}  ·  {clock.Weekday.ToUpper()}{(clock.IsPayday ? "  ·  PAYDAY" : "")}";
            timeLabel.text = $"{clock.TimeString}  <size=13><color=#A89F8E>{clock.Phase}</color></size>";
            for (int i = 0; i < 4; i++) speedButtons[i].Toggle("btn--on", game.Ticks.Speed == i);
            debugSpeed.text = game.Ticks.Speed >= TickManager.NormalSpeedCount ? $"DEBUG ×{TickManager.SpeedMultipliers[game.Ticks.Speed]}" : "";

            var s = game.Stock;
            cashValue.text = $"€{s.Cash}";
            productValue.text = s.Product.ToString();
            mealsValue.text = s.Meals.ToString();
            groceriesValue.text = s.Groceries.ToString();
            medicineValue.text = s.Medicine.ToString();
            respectValue.text = $"{game.Org.Respect:0}";

            var org = game.Org;
            var boss = org.Boss(game);
            familyTitle.text = boss != null ? $"{boss.DisplayName} {boss.LastName}" : "<color=#F07A6E>No boss</color>";
            familyState.text = $"Generation {org.Generation}" +
                (org.Hiding ? $"  ·  <color=#F2B866>In latitanza, day {clock.Day - org.HidingSinceDay + 1}</color>" :
                 org.WarrantActive ? "  ·  <color=#F07A6E>Arrest warrant</color>" : "") +
                (org.PendingPizzini > 0 ? $"  ·  {org.PendingPizzini} pizzini pending" : "");
            evidenceValue.text = $"{org.Evidence:0}";
            Ui.SetFill(evidenceFill, org.Evidence / 100f);

            var rival = game.Rival;
            hostilityValue.text = rival.Destroyed ? "—" : $"{rival.Hostility:0}";
            Ui.SetFill(hostilityFill, rival.Destroyed ? 0f : rival.Hostility / 100f);
            rivalLabel.text = rival.Destroyed ? $"{rival.Name}: destroyed" : rival.InTruce(game) ? $"{rival.Name}: truce until day {rival.TruceUntilDay}" : $"{rival.Name} · {rival.Soldiers} soldiers";
        }
    }
}
