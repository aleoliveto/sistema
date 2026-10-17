using System;
using System.Collections.Generic;
using System.Linq;
using OSistema.Pawns;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>The crew bar (portraits down the left) and the inspector for the selected crew member.</summary>
    public sealed partial class Hud
    {
        sealed class CrewCard
        {
            public VisualElement Root, Avatar, MoodFill, HealthFill;
            public Label Name, Status;
        }

        VisualElement crewBar;
        readonly Dictionary<int, CrewCard> cards = new Dictionary<int, CrewCard>();
        string crewSignature = "";

        VisualElement inspector, inspectorDetail, injuriesBox, thoughtsBox;
        VisualElement inspectorAvatar;
        Label inspName, inspSub, inspJob, inspSkills, inspHealth, inspMulti;
        Button roleButton, draftButton, armButton;
        VisualElement moodFill, foodFill, restFill, loyaltyFill;
        Label moodValue, foodValue, restValue, loyaltyValue;

        static string RoleLabel(CrewRole role) => role switch
        {
            CrewRole.Pusher => "Pusher",
            CrewRole.Lookout => "Lookout (vedetta)",
            CrewRole.Collector => "Collector (pizzo)",
            _ => "General",
        };

        static void CycleRole(Pawn p)
        {
            var roles = (CrewRole[])Enum.GetValues(typeof(CrewRole));
            p.Role = roles[(Array.IndexOf(roles, p.Role) + 1) % roles.Length];
        }

        static Color MoodColor(float mood) =>
            mood < Mood.BreakBelow ? new Color(0.86f, 0.3f, 0.26f) : mood < 40f ? new Color(0.92f, 0.65f, 0.3f) : new Color(0.45f, 0.75f, 0.45f);

        // ---------- Crew bar ----------

        void BuildCrewBar()
        {
            crewBar = Ui.Box("crew-bar").Into(hudLayer);
            crewBar.pickingMode = PickingMode.Ignore;
        }

        void RefreshCrewBar()
        {
            var crew = game.Pawns.Crew.Where(p => !p.Dead).ToList();
            string signature = string.Join(",", crew.Select(p => p.Id));
            if (signature != crewSignature)
            {
                crewSignature = signature;
                crewBar.Clear();
                cards.Clear();
                foreach (var p in crew) cards[p.Id] = CreateCard(p);
            }

            long now = game.Ticks.TicksGame;
            foreach (var p in crew)
            {
                var card = cards[p.Id];
                card.Name.text = p.IsBoss ? $"<color=#E8C26A>{p.DisplayName}</color>" : p.DisplayName;
                card.Status.text = ShortStatus(p, now);
                Ui.SetFill(card.MoodFill, p.Mood.Value / 100f);
                card.MoodFill.style.backgroundColor = MoodColor(p.Mood.Value);
                float health = Mathf.Clamp01(1f - Mathf.Max(p.Health.Pain, p.Health.BloodLoss));
                Ui.SetFill(card.HealthFill, health);
                card.HealthFill.style.backgroundColor = health > 0.75f ? new Color(0.78f, 0.76f, 0.7f) : health > 0.4f ? new Color(0.92f, 0.65f, 0.3f) : new Color(0.86f, 0.3f, 0.26f);
                card.Root.Toggle("crew-card--selected", controller.Selected.Contains(p));
                card.Root.Toggle("crew-card--drafted", p.Drafted);
                card.Root.style.opacity = p.InJail ? 0.45f : 1f;
            }
        }

        CrewCard CreateCard(Pawn p)
        {
            var card = new CrewCard { Root = Ui.Box("crew-card").Into(crewBar) };
            card.Avatar = Avatar(p, card.Root, big: false);
            var col = Ui.Box("col", "grow").Into(card.Root);
            card.Name = Ui.Text("", col, "crew-card__name");
            card.Status = Ui.Text("", col, "crew-card__status");
            (_, card.MoodFill) = Ui.Bar("", Color.green, col, thin: true);
            (_, card.HealthFill) = Ui.Bar("", new Color(0.86f, 0.3f, 0.26f), col, thin: true);

            card.Root.RegisterCallback<ClickEvent>(e =>
            {
                if (p.InJail || p.Dead) return;
                bool additive = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
                if (!additive) controller.Selected.Clear();
                if (!controller.Selected.Contains(p)) controller.Selected.Add(p);
                if (e.clickCount >= 2) FocusCamera(p.Position);
                RefreshAll();
            });
            Tip(card.Root, () => $"{p.FullName} · {RoleLabel(p.Role)}\nClick to select, double-click to find. Mood {p.Mood.Value:0} ({p.Mood.Description})");
            return card;
        }

        VisualElement Avatar(Pawn p, VisualElement parent, bool big)
        {
            var avatar = Ui.Box("avatar").Into(parent);
            if (big) avatar.AddToClassList("avatar--big");
            avatar.style.backgroundColor = (Color)p.ShirtColor;
            var initial = Ui.Text(p.FirstName.Substring(0, 1), avatar);
            initial.style.flexGrow = 1;
            initial.style.unityTextAlign = TextAnchor.MiddleCenter;
            avatar.pickingMode = PickingMode.Ignore;
            return avatar;
        }

        static string ShortStatus(Pawn p, long now)
        {
            if (p.InJail) return "<color=#F07A6E>In Poggioreale</color>";
            if (p.Health.Downed) return "<color=#F07A6E>Downed</color>";
            if (p.InMentalBreak(now)) return "<color=#F07A6E>Breakdown</color>";
            if (p.Sleep != SleepState.Awake) return "Asleep";
            if (p.Drafted) return "<color=#F07A6E>Drafted</color>";
            string job = p.CurrentJob?.Label ?? "Thinking";
            return job.Length > 26 ? job.Substring(0, 25) + "…" : job;
        }

        // ---------- Inspector ----------

        void BuildInspector()
        {
            inspector = Ui.Box("panel", "inspector").Into(hudLayer);

            inspMulti = Ui.Text("", inspector, "title");
            inspectorDetail = Ui.Box("col").Into(inspector);

            var head = Ui.Row(inspectorDetail);
            inspectorAvatar = Ui.Box().Into(head);
            var names = Ui.Box("col", "grow").Into(head);
            inspName = Ui.Text("", names, "title");
            inspSub = Ui.Text("", names, "subtitle");

            var actions = Ui.Row(inspectorDetail);
            roleButton = Ui.Btn("", () => { if (Single(out var p)) CycleRole(p); RefreshAll(); }, actions, "btn--small");
            Tip(roleButton, () => "Click to change role: General, Pusher, Lookout, Collector.");
            draftButton = Ui.Btn("", () => { controller.ToggleDraftSelected(); RefreshAll(); }, actions, "btn--small");
            Tip(draftButton, () => "Drafted crew take your orders and fight (R).\nRight-click a rival to focus fire.");
            armButton = Ui.Btn("", () => { if (Single(out var p)) ToggleArmed(p); RefreshAll(); }, actions, "btn--small");
            Tip(armButton, () => $"Give or take back a pistol. Pistols in the stash: {game.Stock.Pistols}.");

            inspJob = Ui.Text("", inspectorDetail, "small", "wrap");
            inspJob.style.marginTop = 4;
            inspJob.style.marginBottom = 4;

            (moodValue, moodFill) = Ui.Bar("Mood", Color.green, inspectorDetail);
            var needs = Ui.Row(inspectorDetail);
            var left = Ui.Box("col", "grow").Into(needs);
            left.style.marginRight = 8;
            var right = Ui.Box("col", "grow").Into(needs);
            (foodValue, foodFill) = Ui.Bar("Food", new Color(0.9f, 0.6f, 0.28f), left);
            (restValue, restFill) = Ui.Bar("Rest", new Color(0.42f, 0.56f, 0.92f), right);
            (loyaltyValue, loyaltyFill) = Ui.Bar("Loyalty", new Color(0.91f, 0.76f, 0.42f), inspectorDetail);

            Ui.Text("HEALTH", inspectorDetail, "section");
            inspHealth = Ui.Text("", inspectorDetail, "small", "wrap");
            injuriesBox = Ui.Box("col").Into(inspectorDetail);
            Ui.Text("THOUGHTS", inspectorDetail, "section");
            thoughtsBox = Ui.Box("col").Into(inspectorDetail);
            Ui.Text("SKILLS", inspectorDetail, "section");
            inspSkills = Ui.Text("", inspectorDetail, "small", "wrap");
        }

        bool Single(out Pawn pawn)
        {
            pawn = controller.Selected.Count == 1 ? controller.Selected[0] : null;
            return pawn != null;
        }

        void ToggleArmed(Pawn p)
        {
            if (p.Armed)
            {
                p.Armed = false;
                game.Stock.Pistols++;
            }
            else if (game.Stock.Pistols > 0 && p.IsPresent)
            {
                p.Armed = true;
                game.Stock.Pistols--;
            }
            else game.Log.Add("No pistols in the stash. Buy one in the business panel (B).", OSistema.Core.MessageKind.Bad);
        }

        Pawn inspected;

        void RefreshInspector()
        {
            int count = controller.Selected.Count;
            inspector.Show(count > 0);
            if (count == 0) return;

            bool single = Single(out var p);
            inspMulti.Show(!single);
            inspectorDetail.Show(single);
            if (!single)
            {
                inspMulti.text = $"{count} crew selected";
                return;
            }

            if (inspected != p)
            {
                inspected = p;
                inspectorAvatar.Clear();
                Avatar(p, inspectorAvatar, big: true);
            }

            long now = game.Ticks.TicksGame;
            inspName.text = p.IsBoss ? $"<color=#E8C26A>{p.DisplayName}</color> {p.LastName}" : p.FullName;
            inspSub.text = $"“{p.Nickname}”  ·  {RoleLabel(p.Role)}  ·  €{p.Wage}/week";
            roleButton.text = $"Role: {RoleLabel(p.Role)}";
            draftButton.text = p.Drafted ? "Release (R)" : "Draft (R)";
            draftButton.Toggle("btn--on", p.Drafted);
            armButton.text = p.Armed ? "Disarm" : "Arm";
            armButton.Toggle("btn--on", p.Armed);

            string job = p.CurrentJob?.Label ?? "Thinking";
            if (p.Carrying != null) job += $" (carrying {p.Carrying})";
            if (p.InMentalBreak(now)) job += "  <color=#F07A6E>· won't take orders</color>";
            inspJob.text = $"<color=#A89F8E>Doing:</color> {job}";

            moodValue.text = $"{p.Mood.Value:0} · {p.Mood.Description}";
            Ui.SetFill(moodFill, p.Mood.Value / 100f);
            moodFill.style.backgroundColor = MoodColor(p.Mood.Value);
            foodValue.text = $"{p.Needs.Food * 100f:0}%";
            Ui.SetFill(foodFill, p.Needs.Food);
            restValue.text = $"{p.Needs.Rest * 100f:0}%";
            Ui.SetFill(restFill, p.Needs.Rest);
            loyaltyValue.text = $"{p.Loyalty:0}";
            Ui.SetFill(loyaltyFill, p.Loyalty / 100f);

            var h = p.Health;
            var parts = new List<string>();
            if (h.Healthy) parts.Add("<color=#8FD18A>Healthy</color>");
            if (h.Downed) parts.Add("<color=#F07A6E>Downed</color>");
            if (h.BleedRatePerDay > 0.01f) parts.Add($"<color=#F07A6E>Bleeding {h.BleedRatePerDay * 100f:0}%/day</color>");
            if (h.BloodLoss > 0.01f) parts.Add($"Blood loss {h.BloodLoss * 100f:0}%");
            if (h.Pain > 0.01f) parts.Add($"Pain {h.Pain * 100f:0}%");
            inspHealth.text = string.Join("  ·  ", parts);

            injuriesBox.Clear();
            foreach (var inj in h.Injuries.Take(4))
            {
                string state = inj.IsMalnutrition ? "" : inj.Tended ? $" <color=#8FD18A>tended {inj.TendQuality * 100f:0}%</color>" : " <color=#F07A6E>untended</color>";
                Ui.Text($"{inj.Label} · {inj.Part} {inj.Severity * 100f:0}%{state}", injuriesBox, "thought");
            }

            thoughtsBox.Clear();
            foreach (var t in p.Mood.AllThoughts.OrderByDescending(t => Mathf.Abs(t.Value)).Take(5))
            {
                string color = t.Value >= 0 ? "#8FD18A" : "#F07A6E";
                Ui.Text($"<color={color}>{t.Value:+0;-0}</color>  {t.Label}", thoughtsBox, "thought");
            }

            inspSkills.text = $"Build {p.ConstructionSkill} · Cook {p.CookingSkill} · Medic {p.MedicineSkill} · Intimidate {p.IntimidationSkill} · Aware {p.AwarenessSkill} · Shoot {p.ShootingSkill}";
        }
    }
}
