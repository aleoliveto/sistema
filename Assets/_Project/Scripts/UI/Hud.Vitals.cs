using OSistema.Core;
using OSistema.Pawns;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>
    /// Gennaro's body, bottom left: health, hunger, energy, breath. And the moments when he can't
    /// play on: down in the street, in hospital, in Poggioreale. Plus the chase banner.
    /// </summary>
    public sealed partial class Hud
    {
        VisualElement vitals, statusBox, wanted, wantedFill;
        Label vitalsName, vitalsState, statusTitle, statusText;
        Button statusButton, statusButton2;

        Label vHealth, vFood, vRest, vBreath, vAmmo, vArmor;

        void BuildVitals()
        {
            // Gennaro's body, in the top bar: painted icons and short bars. The figures are on hover.
            vitals = topVitals;
            vitalsName = Ui.Text("", vitals, "tiny");
            vitalsName.Show(false);
            vHealth = VitalChip(vitals, "♥", "#F07A6E", "icon_health", out vHealthFill);
            vArmor = VitalChip(vitals, "VEST", "#9DB8D8", null, out vArmorFill);
            vFood = VitalChip(vitals, "FAME", "#F2B866", "icon_food", out vFoodFill);
            vRest = VitalChip(vitals, "SONNO", "#8FB4F0", "icon_sleep", out vRestFill);
            vBreath = VitalChip(vitals, "FIATO", "#D8D4C8", null, out vBreathFill);
            var ammo = Ui.Box("fig").Into(vitals);
            Ui.ArtIcon("icon_ammo", 24, ammo);
            vAmmo = Ui.Text("", ammo, "fig__value");
            vitalsState = Ui.Text("", vitals, "t-day");
            Tip(vitals, () =>
            {
                var p = game?.Player;
                if (p == null) return "";
                return $"♥ health {VitalHealth(p) * 100f:0}%: wounds and blood loss.\nFAME {p.Needs.Food * 100f:0}%: hunger, eat at home (E at the pantry) or a pizza.\n" +
                       $"SONNO {p.Needs.Rest * 100f:0}%: energy, sleep in your bed (E). FIATO {p.Stamina * 100f:0}%: breath, running with Shift uses it up.\n" +
                       (p.Autopilot ? "Z: Gennaro looks after himself when you leave him be." : "Z: let Gennaro look after himself when you leave him be.") +
                       (p.Health.NeedsTending ? "\nYou're bleeding: someone has to tend the wound." : "");
            });

            statusBox = Ui.Box("panel", "status-box").Into(modalLayer);
            statusTitle = Ui.Text("", statusBox, "title", "gold");
            statusText = Ui.Text("", statusBox, "small", "wrap");
            var row = Ui.Row(statusBox);
            row.style.marginTop = 6;
            statusButton = Ui.Btn("", null, row, "btn--small");
            statusButton2 = Ui.Btn("", null, row, "btn--small");
            statusBox.Show(false);

            wanted = Ui.Box("wanted").Into(hudLayer);
            Ui.Text("FERMO! POLIZIA!", wanted, "wanted__title");
            Ui.Text("Run (Shift), get out of sight, or jump on a scooter (R).", wanted, "tiny");
            var bar = Ui.Box("bar", "bar--thin").Into(wanted);
            wantedFill = Ui.Box("bar__fill").Into(bar);
            wantedFill.style.backgroundColor = new Color(0.55f, 0.85f, 0.5f);
            wanted.pickingMode = PickingMode.Ignore;
            wanted.Show(false);
        }

        VisualElement vHealthFill, vArmorFill, vFoodFill, vRestFill, vBreathFill;

        Label VitalChip(VisualElement parent, string label, string color, string art, out VisualElement fill)
        {
            var chip = Ui.Box("fig").Into(parent);
            Label l = null;
            if (art == null || Ui.ArtIcon(art, 26, chip) == null) l = Ui.Text($"<color={color}>{label}</color>", chip, "tiny");
            else l = Ui.Text("", chip, "tiny");
            var track = Ui.Box("fig__track").Into(chip);
            fill = Ui.Box("bar__fill").Into(track);
            fill.style.backgroundColor = Ui.Parse(color);
            return l;
        }

        static float VitalHealth(Pawn p) =>
            Mathf.Min(1f - Mathf.Max(p.Health.BloodLoss / 0.6f, p.Health.Pain / 0.8f), p.Hp / Pawn.MaxHp);

        static Color VitalColor(float v, Color normal) =>
            v < 0.2f ? new Color(0.94f, 0.33f, 0.28f) : v < 0.4f ? new Color(0.95f, 0.72f, 0.4f) : normal;

        static string Pct(float v)
        {
            int pct = Mathf.RoundToInt(Mathf.Clamp01(v) * 100f);
            string c = pct < 20 ? "#F07A6E" : pct < 40 ? "#F2B866" : "#EDE6D6";
            return $"<color={c}>{pct}%</color>";
        }

        System.Action statusAction, statusAction2;

        void RefreshVitals()
        {
            var p = game.Player;
            vitals.Show(p != null);
            if (p == null) return;
            vitals.Show(true);
            vitalsState.text = (p.Riding ? "ON THE SCOOTER" : game.Org.DealingCorner != null ? "DEALING" : game.Org.CarriedDg > 0 ? $"CARRYING {game.Org.CarriedDg / 10} G" : p.Carrying != null ? $"CARRYING {p.Carrying.ToUpper()}" : "")
                + (p.Autopilot ? "  · AUTO" : "");
            vitalsState.Show(vitalsState.text.Length > 0);
            float health = VitalHealth(p);
            Ui.SetFill(vHealthFill, health);
            vHealthFill.style.backgroundColor = VitalColor(health, new Color(0.94f, 0.48f, 0.43f));
            vArmor.parent.Show(p.Armor > 0.5f);
            Ui.SetFill(vArmorFill, p.Armor / Pawn.MaxArmor);
            Ui.SetFill(vFoodFill, p.Needs.Food);
            vFoodFill.style.backgroundColor = VitalColor(p.Needs.Food, new Color(0.95f, 0.72f, 0.4f));
            Ui.SetFill(vRestFill, p.Needs.Rest);
            vRestFill.style.backgroundColor = VitalColor(p.Needs.Rest, new Color(0.56f, 0.71f, 0.94f));
            // Breath only matters while running.
            vBreath.parent.Show(p.Stamina < 0.98f || p.Winded);
            Ui.SetFill(vBreathFill, p.Stamina);
            vBreathFill.style.backgroundColor = p.Winded ? new Color(0.94f, 0.33f, 0.28f) : new Color(0.85f, 0.83f, 0.78f);
            vAmmo.parent.Show(p.Armed);
            vAmmo.text = p.ReloadUntil > Time.unscaledTime ? "<color=#A89F8E>reloading</color>"
                : p.Ammo == 0 ? "<color=#F07A6E>empty · R</color>" : $"{p.Ammo}/{Pawn.MagazineSize}";

            wanted.Show(game.Pursuit.Active);
            if (game.Pursuit.Active) Ui.SetFill(wantedFill, game.Pursuit.EscapeProgress);

            // When the player can't play on, say why and what can be done.
            long now = game.Ticks.TicksGame;
            string title = null, text = null, b1 = null, b2 = null;
            statusAction = statusAction2 = null;
            if (p.InJail)
            {
                long days = (p.JailedUntil - now) / GameClock.TicksPerDay + 1;
                title = "POGGIOREALE";
                int dayOfWeek = (game.Clock.Day - 1) % 7;          // 0 Monday … 5 Saturday
                int toSaturday = (5 - dayOfWeek + 7) % 7;
                bool sentToday = game.Org.LastPizzinoDay == game.Clock.Day;
                text = $"Inside for {days} more day{(days == 1 ? "" : "s")}. The crew carry on outside, but without product the piazze run dry: send word.\n" +
                       $"<color=#A89F8E>A pizzino a day goes out through a guard. Colloquio on Saturday{(toSaturday == 0 ? " (today, at 10)" : $" (in {toSaturday} day{(toSaturday == 1 ? "" : "s")})")}.</color>";
                b1 = sentToday ? "Pizzino sent today" : "Send a pizzino";
                statusAction = () =>
                {
                    if (game.Org.LastPizzinoDay == game.Clock.Day) return;
                    game.Org.LastPizzinoDay = game.Clock.Day;
                    // A tapped family gets its notes read now and then.
                    if (game.Phone.Tapped(game.Ticks.TicksGame) && game.Rng.NextDouble() < 0.25)
                    {
                        game.Org.AddEvidence(2f, game);
                        game.Log.Add("A guard was searched on his way out. Your pizzino is now in a magistrate's file.", MessageKind.Bad);
                    }
                    game.Events.Raise(OSistema.Story.JailCards.Orders(game, visit: false));
                };
            }
            else if (p.InHospital)
            {
                long hours = (p.HospitalUntil - now) / GameClock.TicksPerHour + 1;
                title = "OSPEDALE";
                text = $"The doctors are patching {p.DisplayName} up. Out in about {hours} hours. They report gunshot wounds to the police.";
                b1 = "Let the time pass";
                statusAction = () => game.Ticks.SetSpeed(3);
            }
            else if (p.Health.Downed && !p.Dead)
            {
                title = "A TERRA";
                text = p.Health.BleedRatePerDay > 0.01f
                    ? "You're down and bleeding. Someone has to stop the bleeding, or an ambulance has to come."
                    : "You're down. You can't get up yet.";
                if (!game.Hospital.IsCalled(p))
                {
                    b1 = "Call 118 yourself";
                    statusAction = () => game.Hospital.Call(p, game, byPasserby: false);
                }
                else text += " The ambulance is on its way.";
            }
            if (title != null && game.Ticks.Speed >= 3 && (p.InJail || p.InHospital))
            {
                b2 = "Back to normal speed";
                statusAction2 = () => game.Ticks.SetSpeed(1);
            }
            else if (p.InJail)
            {
                b2 = "Let the days pass";
                statusAction2 = () => game.Ticks.SetSpeed(4);
            }
            // In jail the box sits at the side, out of the way of the city you're still watching.
            statusBox.EnableInClassList("status-box--side", p.InJail);

            statusBox.Show(title != null);
            if (title == null) return;
            statusTitle.text = title;
            statusText.text = text;
            statusButton.text = b1 ?? "";
            statusButton.Show(b1 != null);
            statusButton2.text = b2 ?? "";
            statusButton2.Show(b2 != null);
            if (statusButton.userData == null)
            {
                statusButton.clicked += () => statusAction?.Invoke();
                statusButton2.clicked += () => statusAction2?.Invoke();
                statusButton.userData = true;
            }
        }
    }
}
