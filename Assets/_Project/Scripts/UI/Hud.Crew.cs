using System;
using System.Collections.Generic;
using System.Linq;
using OSistema.Pawns;
using OSistema.World;
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

        VisualElement inspector, inspectorDetail, injuriesBox, thoughtsBox, propertyPanel, inspBody;
        Button callButton, treatButton;
        enum InspTab { Needs, Health, Mind, Skills }
        InspTab inspTab;
        bool inspCollapsed;
        readonly Dictionary<InspTab, Button> inspTabButtons = new Dictionary<InspTab, Button>();
        readonly Dictionary<InspTab, VisualElement> inspTabBodies = new Dictionary<InspTab, VisualElement>();
        Label inspCollapse;
        VisualElement inspectorAvatar;
        Label inspName, inspSub, inspJob, inspSkills, inspHealth, inspMulti;
        Button roleButton, draftButton, armButton, bedButton;
        VisualElement moodFill, foodFill, restFill, loyaltyFill;
        Label moodValue, foodValue, restValue, loyaltyValue;

        static string RoleLabel(CrewRole role) => role switch
        {
            CrewRole.Pusher => "Pusher",
            CrewRole.Lookout => "Lookout (vedetta)",
            CrewRole.Collector => "Collector (pizzo)",
            CrewRole.Manager => "Manager",
            CrewRole.Bagger => "Bagger (cuts and bags doses)",
            CrewRole.Roller => "Roller (makes ROSSE)",
            CrewRole.Bodyguard => "Bodyguard",
            _ => "General",
        };

        VisualElement roleMenu;

        /// <summary>A dropdown of jobs, opened under the button that was clicked.</summary>
        void ShowRoleMenu(Pawn p, VisualElement anchor)
        {
            if (roleMenu == null)
            {
                roleMenu = Ui.Box("layer").Into(modalLayer);
                roleMenu.RegisterCallback<PointerDownEvent>(e => { if (e.target == roleMenu) roleMenu.Show(false); });
            }
            roleMenu.Clear();
            roleMenu.Show(true);
            roleMenu.pickingMode = PickingMode.Position;
            var list = Ui.Box("panel", "role-menu").Into(roleMenu);
            var r = anchor.worldBound;
            var local = roleMenu.WorldToLocal(new Vector2(r.xMin, r.yMax));
            list.style.left = local.x;
            list.style.top = local.y + 2;
            foreach (CrewRole role in Enum.GetValues(typeof(CrewRole)))
            {
                if (role == CrewRole.Manager) continue;   // appointed from the property panel
                var rr = role;
                var b = Ui.Btn(RoleLabel(role), () =>
                {
                    p.Role = rr;
                    roleMenu.Show(false);
                    bizDirty = true;
                    RefreshAll();
                }, list, "btn--small", "role-menu__item");
                b.Toggle("btn--on", p.Role == role);
            }
        }

        /// <summary>Beds in the family's homes, with whose they are; pick one to give it to this person.</summary>
        void ShowBedMenu(Pawn p, VisualElement anchor)
        {
            if (roleMenu == null)
            {
                roleMenu = Ui.Box("layer").Into(modalLayer);
                roleMenu.RegisterCallback<PointerDownEvent>(e => { if (e.target == roleMenu) roleMenu.Show(false); });
            }
            roleMenu.Clear();
            roleMenu.Show(true);
            roleMenu.pickingMode = PickingMode.Position;
            var list = Ui.Box("panel", "role-menu").Into(roleMenu);
            var r = anchor.worldBound;
            var local = roleMenu.WorldToLocal(new Vector2(r.xMin, r.yMax));
            list.style.left = local.x;
            list.style.top = local.y + 2;
            int n = 0;
            foreach (var bed in game.Map.PositionsOf(BuildingDefs.Bed))
            {
                if (!game.PlayerBase.Contains(bed) && !game.Properties.IsOwnedHome(bed)) continue;
                n++;
                Pawn owner = null;
                foreach (var c in game.Pawns.Crew) if (!c.Dead && c.Bed == bed) owner = c;
                var home = game.PlayerBase.Contains(bed) ? "the basso" : game.Properties.At(bed)?.Name ?? "home";
                var b = bed;
                var item = Ui.Btn($"Bed {n} · {home} · {(owner == null ? "free" : owner.Nickname)}", () =>
                {
                    // Swap: whoever had it takes this person's old bed.
                    if (owner != null && owner != p) owner.Bed = p.Bed;
                    p.Bed = b;
                    roleMenu.Show(false);
                    bizDirty = true;
                    RefreshAll();
                }, list, "btn--small", "role-menu__item");
                item.Toggle("btn--on", p.Bed == bed);
            }
            if (n == 0) Ui.Text("No beds yet. Buy one at the furniture shop and place it at home.", list, "small", "muted");
        }

        /// <summary>A grid of the 24 hours under a button; pick one.</summary>
        void ShowHourMenu(VisualElement anchor, Action<int> pick)
        {
            if (roleMenu == null)
            {
                roleMenu = Ui.Box("layer").Into(modalLayer);
                roleMenu.RegisterCallback<PointerDownEvent>(e => { if (e.target == roleMenu) roleMenu.Show(false); });
            }
            roleMenu.Clear();
            roleMenu.Show(true);
            roleMenu.pickingMode = PickingMode.Position;
            var list = Ui.Box("panel", "role-menu", "hour-menu").Into(roleMenu);
            var r = anchor.worldBound;
            var local = roleMenu.WorldToLocal(new Vector2(r.xMin, r.yMax));
            list.style.left = local.x;
            list.style.top = local.y + 2;
            for (int h = 0; h < 24; h++)
            {
                int hour = h;
                Ui.Btn($"{h:00}:00", () => { roleMenu.Show(false); pick(hour); }, list, "btn--small", "hour-menu__item");
            }
        }

        string BedLabel(Pawn p)
        {
            if (!p.Bed.HasValue) return "Bed: none ▾";
            int n = 0;
            foreach (var bed in game.Map.PositionsOf(BuildingDefs.Bed))
            {
                if (!game.PlayerBase.Contains(bed) && !game.Properties.IsOwnedHome(bed)) continue;
                n++;
                if (bed == p.Bed.Value) return $"Bed {n} ▾";
            }
            return "Bed: none ▾";
        }

        static void CycleRole(Pawn p)
        {
            var roles = (CrewRole[])Enum.GetValues(typeof(CrewRole));
            // Managers are appointed from the property panel, so the cycle skips that role.
            do p.Role = roles[(Array.IndexOf(roles, p.Role) + 1) % roles.Length];
            while (p.Role == CrewRole.Manager);
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
                card.HealthFill.style.backgroundColor = health > 0.4f ? new Color(0.92f, 0.65f, 0.3f) : new Color(0.86f, 0.3f, 0.26f);
                card.HealthFill.parent.Show(health < 0.97f);
                card.Root.Toggle("crew-card--selected", controller.Selected.Contains(p));
                card.Root.Toggle("crew-card--drafted", p.Drafted);
                card.Root.style.opacity = p.InJail || p.InHospital ? 0.45f : 1f;
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
                if (p.InJail || p.InHospital || p.Dead) return;
                bool additive = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
                if (!additive) controller.Selected.Clear();
                controller.SelectProperty(null);
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
            var face = p.Look != null ? OSistema.Views.CharacterArt.Face(p.Look.Face) : null;
            if (face != null)
            {
                avatar.style.backgroundImage = new StyleBackground(face);
                avatar.AddToClassList("avatar--portrait");
            }
            else
            {
                var initial = Ui.Text(p.FirstName.Substring(0, 1), avatar);
                initial.style.flexGrow = 1;
                initial.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            avatar.pickingMode = PickingMode.Ignore;
            return avatar;
        }

        static string ShortStatus(Pawn p, long now)
        {
            if (p.InJail) return "<color=#F07A6E>In Poggioreale</color>";
            if (p.InHospital) return "<color=#F2B866>In hospital</color>";
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
            // A compact card: who, what they're doing, a row of actions, then one tab at a time.
            // Click the header to fold it down to just the name.
            inspector = Ui.Box("panel", "inspector", "fit").Into(hudLayer);

            inspMulti = Ui.Text("", inspector, "title");
            inspectorDetail = Ui.Box("col", "fit").Into(inspector);

            var head = Ui.Row(inspectorDetail);
            head.AddToClassList("insp-head");
            head.RegisterCallback<ClickEvent>(e => { if (e.target is Button) return; inspCollapsed = !inspCollapsed; RefreshAll(); });
            inspectorAvatar = Ui.Box().Into(head);
            var names = Ui.Box("col", "grow").Into(head);
            inspName = Ui.Text("", names, "insp-name");
            inspSub = Ui.Text("", names, "subtitle");
            inspJob = Ui.Text("", names, "small", "muted");
            inspCollapse = Ui.Text("▾", head, "insp-chevron");
            Tip(head, () => inspCollapsed ? "Click to open" : "Click to fold away");

            inspBody = Ui.Box("col", "fit").Into(inspectorDetail);
            var actions = Ui.Row(inspBody);
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginTop = 4;
            roleButton = Ui.Btn("", () => { if (Single(out var p)) ShowRoleMenu(p, roleButton); }, actions, "btn--small");
            Tip(roleButton, () => "Change job: general, pusher, lookout, collector, bagger, roller, bodyguard.");
            bedButton = Ui.Btn("", () => { if (Single(out var p)) ShowBedMenu(p, bedButton); }, actions, "btn--small");
            Tip(bedButton, () => "Which bed is theirs. Pick one to give it to them; whoever had it swaps.");
            armButton = Ui.Btn("", () => { if (Single(out var p)) ToggleArmed(p); RefreshAll(); }, actions, "btn--small");
            Tip(armButton, () => $"Give or take back a pistol. Pistols in the stash: {game.Stock.Pistols}.");
            draftButton = Ui.Btn("Call over", () =>
            {
                var boss = game.Player;
                if (Single(out var p) && boss != null && !p.Controlled && p.CanAct) p.ForceJob(new OSistema.AI.GotoJob(p, boss.Cell, "Coming over"), game);
                RefreshAll();
            }, actions, "btn--small");
            Tip(draftButton, () => "Call them over to where you are.");
            callButton = Ui.Btn("Call 118", () => { if (Single(out var p)) game.Hospital.Call(p, game, byPasserby: false); RefreshAll(); }, actions, "btn--small", "btn--danger");
            Tip(callButton, () => "Send for an ambulance. The hospital will patch them up, and report any gunshot wound to the police.");

            var tabs = Ui.Row(inspBody);
            tabs.AddToClassList("insp-tabs");
            var pages = Ui.Scroll(inspBody);   // a long tab scrolls inside the card instead of squashing it
            foreach (InspTab t in Enum.GetValues(typeof(InspTab)))
            {
                var tt = t;
                inspTabButtons[t] = Ui.Btn(t.ToString(), () => { inspTab = tt; RefreshAll(); }, tabs, "insp-tab");
                inspTabBodies[t] = Ui.Box("col", "insp-page").Into(pages);
            }

            // Needs
            var needsPage = inspTabBodies[InspTab.Needs];
            (moodValue, moodFill) = Ui.Bar("Mood", Color.green, needsPage);
            var needs = Ui.Row(needsPage);
            var left = Ui.Box("col", "grow").Into(needs);
            left.style.marginRight = 8;
            var right = Ui.Box("col", "grow").Into(needs);
            (foodValue, foodFill) = Ui.Bar("Food", new Color(0.9f, 0.6f, 0.28f), left);
            (restValue, restFill) = Ui.Bar("Rest", new Color(0.42f, 0.56f, 0.92f), right);
            (loyaltyValue, loyaltyFill) = Ui.Bar("Loyalty", new Color(0.91f, 0.76f, 0.42f), needsPage);

            // Health
            var healthPage = inspTabBodies[InspTab.Health];
            inspHealth = Ui.Text("", healthPage, "small", "wrap");
            injuriesBox = Ui.Box("col").Into(healthPage);
            treatButton = Ui.Btn("", () => { if (Single(out var p)) TreatWithMedicine(p); RefreshAll(); }, healthPage, "btn--small");
            treatButton.style.alignSelf = Align.FlexStart;
            treatButton.style.marginTop = 4;
            Tip(treatButton, () => "Use a box of medicine from home: stops the bleeding and tends the wounds. The pharmacy sells it; the hospital does it properly, but asks questions about bullets.");

            // Mind
            thoughtsBox = Ui.Box("col").Into(inspTabBodies[InspTab.Mind]);

            // Skills
            inspSkills = Ui.Text("", inspTabBodies[InspTab.Skills], "small", "wrap");

            propertyPanel = Ui.Box("col").Into(inspector);
        }

        void TreatWithMedicine(Pawn p) => OSistema.Crime.Healing.WithMedicine(game, p);

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
            var property = controller.SelectedProperty;
            inspector.Show(count > 0 || property != null);
            propertyPanel.Show(count == 0 && property != null);
            if (count == 0)
            {
                inspMulti.Show(false);
                inspectorDetail.Show(false);
                if (property != null) RefreshPropertyPanel(property);
                return;
            }

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
            roleButton.text = $"Job: {RoleLabel(p.Role)}  ▾";
            draftButton.Show(!p.Controlled);
            roleButton.Show(!p.Controlled);
            armButton.text = p.Armed ? "Disarm" : "Arm";
            bedButton.text = BedLabel(p);
            armButton.Toggle("btn--on", p.Armed);
            callButton.Show(p.Health.Downed && !game.Hospital.IsCalled(p));
            inspBody.Show(!inspCollapsed);
            inspCollapse.text = inspCollapsed ? "▸" : "▾";
            bool hurt = !p.Health.Healthy;
            foreach (var kv in inspTabButtons)
            {
                kv.Value.Toggle("insp-tab--on", kv.Key == inspTab);
                inspTabBodies[kv.Key].Show(kv.Key == inspTab);
            }
            inspTabButtons[InspTab.Health].text = hurt ? "Health <color=#F07A6E>●</color>" : "Health";
            treatButton.text = $"Treat with medicine ({game.Stock.Medicine} at home)";
            treatButton.Show(hurt);
            treatButton.SetEnabled(game.Stock.Medicine > 0 && OSistema.Crime.Healing.NeedsCare(p));

            string job = p.CurrentJob?.Label ?? "Thinking";
            if (p.Carrying != null) job += $" (carrying {p.Carrying})";
            if (p.InMentalBreak(now)) job += "  <color=#F07A6E>· won't take orders</color>";
            inspJob.text = job;

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
            foreach (var inj in h.Injuries.Take(6))
            {
                string state = inj.IsMalnutrition ? "" : inj.Tended ? $" <color=#8FD18A>tended {inj.TendQuality * 100f:0}%</color>" : " <color=#F07A6E>untended</color>";
                Ui.Text($"{inj.Label} · {inj.Part} {inj.Severity * 100f:0}%{state}", injuriesBox, "thought");
            }

            thoughtsBox.Clear();
            foreach (var t in p.Mood.AllThoughts.OrderByDescending(t => Mathf.Abs(t.Value)).Take(8))
            {
                string color = t.Value >= 0 ? "#8FD18A" : "#F07A6E";
                Ui.Text($"<color={color}>{t.Value:+0;-0}</color>  {t.Label}", thoughtsBox, "thought");
            }

            inspSkills.text = $"Handiwork  <b>{p.ConstructionSkill}</b>\nCooking  <b>{p.CookingSkill}</b>\nMedicine  <b>{p.MedicineSkill}</b>\nIntimidation  <b>{p.IntimidationSkill}</b>\nAwareness  <b>{p.AwarenessSkill}</b>\nShooting  <b>{p.ShootingSkill}</b>";
        }
    }
}
