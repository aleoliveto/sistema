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

        Label vHealth, vFood, vRest, vBreath, vAmmo;

        void BuildVitals()
        {
            // Gennaro's body, in the top bar next to the clock.
            vitals = Ui.Box("topbar__section", "vitals-strip").Into(topBar);
            topBar.Insert(1, vitals);
            vitalsName = Ui.Text("", vitals, "tiny");
            vHealth = VitalChip(vitals, "♥", "#F07A6E");
            vFood = VitalChip(vitals, "FAME", "#F2B866");
            vRest = VitalChip(vitals, "SONNO", "#8FB4F0");
            vBreath = VitalChip(vitals, "FIATO", "#D8D4C8");
            vAmmo = VitalChip(vitals, "COLPI", "#E8C26A");
            vitalsState = Ui.Text("", vitals, "tiny");
            Tip(vitals, () =>
            {
                var p = game?.Player;
                if (p == null) return "";
                return "♥ health: wounds and blood loss. FAME: hunger, eat at home (E at the pantry) or a pizza.\n" +
                       "SONNO: energy, sleep in your bed (E). FIATO: breath, running with Shift uses it up.\n" +
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

        Label VitalChip(VisualElement parent, string label, string color)
        {
            var chip = Ui.Box("chip").Into(parent);
            Ui.Text($"<color={color}>{label}</color>", chip, "tiny");
            var v = Ui.Text("", chip, "chip__value");
            v.style.marginLeft = 4;
            return v;
        }

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
            vitalsName.text = "";
            vitalsState.text = (p.Riding ? "ON THE SCOOTER" : game.Org.DealingCorner != null ? "DEALING" : game.Org.CarriedDg > 0 ? $"CARRYING {game.Org.CarriedDg / 10} G" : p.Carrying != null ? $"CARRYING {p.Carrying.ToUpper()}" : "")
                + (p.Autopilot ? "  · AUTO" : "");
            float health = 1f - Mathf.Max(p.Health.BloodLoss / 0.6f, p.Health.Pain / 0.8f);
            vHealth.text = Pct(health);
            vFood.text = Pct(p.Needs.Food);
            vRest.text = Pct(p.Needs.Rest);
            vBreath.text = p.Winded ? "<color=#F07A6E>out of breath</color>" : Pct(p.Stamina);
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
                text = $"{p.DisplayName} is inside for {days} more day{(days == 1 ? "" : "s")}. The family carries on without him: your crew still work their jobs, and the phone still rings.";
                b1 = "Let the days pass";
                statusAction = () => game.Ticks.SetSpeed(4);
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
