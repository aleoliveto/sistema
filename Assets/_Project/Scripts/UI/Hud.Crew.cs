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

        ScrollView propertyScroll;
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
        Button roleButton, draftButton, armButton, homeButton;
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
            CrewRole.Capo => "Capo paranza",
            CrewRole.Parking => "Parcheggiatore (minds cars)",
            CrewRole.Vendor => "Stall (sells ROSSE and fakes)",
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
                if (role == CrewRole.Manager || role == CrewRole.Capo) continue;   // appointed from the property panel / the organigram
                var rr = role;
                var b = Ui.Btn(RoleLabel(role), () =>
                {
                    p.Role = rr;
                    if (Crime.Organization.PiazzaRole(rr)) game.Org.OrganiseShifts(game);
                    roleMenu.Show(false);
                    bizDirty = true;
                    RefreshAll();
                }, list, "btn--small", "role-menu__item");
                b.Toggle("btn--on", p.Role == role);
            }
        }

        /// <summary>
        /// The basso and the family's flats, with their beds and who lives there; pick one to move this person in.
        /// The beds of a house are shared, the day and night shifts taking turns.
        /// </summary>
        void ShowHouseMenu(Pawn p, VisualElement anchor)
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
            var mine = Core.Housing.HomeOf(p, game);
            foreach (var house in Core.Housing.Houses(game))
            {
                int beds = Core.Housing.BedsIn(house, game).Count;
                int people = Core.Housing.Residents(house, game).Count, shortBy = Core.Housing.BedsShort(house, game);
                var h = house;
                var label = $"{Core.Housing.Name(house)} · {beds} bed{(beds == 1 ? "" : "s")} · {people} live here" +
                            (shortBy > 0 ? $" · <color=#E07A5F>{shortBy} short</color>" : "");
                var item = Ui.Btn(label, () =>
                {
                    Core.Housing.MoveIn(p, h);
                    roleMenu.Show(false);
                    bizDirty = true;
                    RefreshAll();
                }, list, "btn--small", "role-menu__item");
                item.Toggle("btn--on", house == mine);
            }
            Ui.Text("Beds are shared: the day and night shifts take turns. With none free they sulk, then sleep on the floor. They eat at home, or at a pizzeria near their post on shift.",
                list, "small", "muted", "wrap");
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

        string HomeLabel(Pawn p) => $"Home: {Core.Housing.Name(Core.Housing.HomeOf(p, game))} ▾";

        static void CycleRole(Pawn p)
        {
            var roles = (CrewRole[])Enum.GetValues(typeof(CrewRole));
            // Managers are appointed from the property panel, so the cycle skips that role.
            do p.Role = roles[(Array.IndexOf(roles, p.Role) + 1) % roles.Length];
            while (p.Role == CrewRole.Manager || p.Role == CrewRole.Capo);
        }

        static Color MoodColor(float mood) =>
            mood < Mood.BreakBelow ? new Color(0.86f, 0.3f, 0.26f) : mood < 40f ? new Color(0.92f, 0.65f, 0.3f) : new Color(0.45f, 0.75f, 0.45f);

        // ---------- Crew bar ----------

        Label crewHead;
        VisualElement crewCards;

        void BuildCrewBar()
        {
            // Only the people who need you: hurt, bleeding, idle, in trouble, breaking down.
            // Everyone else is in Famiglia (Tab).
            crewBar = Ui.Box("crew-bar").Into(hudLayer);
            crewBar.pickingMode = PickingMode.Ignore;
            crewHead = Ui.Text("", crewBar, "crew-bar__head");
            crewHead.pickingMode = PickingMode.Position;
            crewHead.RegisterCallback<ClickEvent>(_ => { OpenFamigliaRoster(); });
            Tip(crewHead, () => "The whole family: click to open Famiglia (Tab).");
            crewCards = Ui.Box("col").Into(crewBar);
            crewCards.pickingMode = PickingMode.Ignore;
        }

        /// <summary>Why this person needs the boss right now, worst first; null when they're fine.</summary>
        string Attention(Pawn p, long now)
        {
            if (p.Dead || p.Controlled) return null;
            if (p.Health.Downed) return "<color=#F07A6E>Down</color>";
            if (p.Health.BleedRatePerDay > 0.01f && OSistema.Crime.Healing.NeedsCare(p)) return "<color=#F07A6E>Bleeding</color>";
            if (p.InMentalBreak(now)) return "<color=#F07A6E>Breakdown</color>";
            if (p.InJail) return "<color=#F07A6E>In Poggioreale</color>";
            if (p.Drafted) return "<color=#F07A6E>Fighting</color>";
            if (p.InHospital) return "<color=#F2B866>In hospital</color>";
            if (!p.Health.Healthy) return "<color=#F2B866>Hurt</color>";
            if (p.Mood.Value < Mood.BreakBelow + 8f) return "<color=#F2B866>On the edge</color>";
            if (p.Needs.Food < 0.1f) return "<color=#F2B866>Starving</color>";
            if (p.Loyalty < 25f) return "<color=#F2B866>Disloyal</color>";
            if (p.Role == CrewRole.General && !p.IsBoss && p.IsPresent) return "<color=#E8C26A>No job</color>";
            return null;
        }

        void RefreshCrewBar()
        {
            long now = game.Ticks.TicksGame;
            var crew = new List<Pawn>();
            int total = 0;
            foreach (var p in game.Pawns.Crew)
            {
                if (p.Dead) continue;
                total++;
                if (Attention(p, now) != null) crew.Add(p);
            }
            bool famigliaOpen = openWindow == Window.Business;
            crewBar.Show(!famigliaOpen);
            crewHead.text = total <= 1 ? "" : crew.Count == 0
                ? $"FAMIGLIA  {total}  <color=#8FD18A>·  all well</color>"
                : $"FAMIGLIA  {total}  <color=#F2B866>·  {crew.Count} need{(crew.Count == 1 ? "s" : "")} you</color>";
            crewHead.Show(total > 1);
            string signature = string.Join(",", crew.Select(p => p.Id));
            if (signature != crewSignature)
            {
                crewSignature = signature;
                crewCards.Clear();
                cards.Clear();
                foreach (var p in crew) cards[p.Id] = CreateCard(p);
            }

            foreach (var p in crew)
            {
                var card = cards[p.Id];
                card.Name.text = p.IsBoss ? $"<color=#E8C26A>{p.DisplayName}</color>" : p.DisplayName;
                card.Status.text = Attention(p, now);
                Ui.SetFill(card.MoodFill, p.Mood.Value / 100f);
                card.MoodFill.style.backgroundColor = MoodColor(p.Mood.Value);
                card.MoodFill.parent.Show(p.Mood.Value < 40f);
                float health = Mathf.Clamp01(1f - Mathf.Max(p.Health.Pain, p.Health.BloodLoss));
                Ui.SetFill(card.HealthFill, health);
                card.HealthFill.style.backgroundColor = health > 0.4f ? new Color(0.92f, 0.65f, 0.3f) : new Color(0.86f, 0.3f, 0.26f);
                card.HealthFill.parent.Show(health < 0.97f);
                card.Root.Toggle("crew-card--selected", controller.Selected.Contains(p));
                card.Root.Toggle("crew-card--drafted", p.Drafted);
                card.Root.style.opacity = p.InJail || p.InHospital ? 0.6f : 1f;
            }
        }

        CrewCard CreateCard(Pawn p)
        {
            var card = new CrewCard { Root = Ui.Box("crew-card").Into(crewCards) };
            card.Avatar = Avatar(p, card.Root, big: false);
            card.Avatar.AddToClassList("avatar--card");
            var col = Ui.Box("col", "grow").Into(card.Root);
            card.Name = Ui.Text("", col, "crew-card__name");
            card.Status = Ui.Text("", col, "crew-card__status");
            (_, card.MoodFill) = Ui.Bar("", Color.green, col, thin: true);
            (_, card.HealthFill) = Ui.Bar("", new Color(0.86f, 0.3f, 0.26f), col, thin: true);

            card.Root.RegisterCallback<ClickEvent>(e =>
            {
                if (p.InJail || p.InHospital || p.Dead) { OpenFamigliaPerson(p); return; }
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
            homeButton = Ui.Btn("", () => { if (Single(out var p)) ShowHouseMenu(p, homeButton); }, actions, "btn--small");
            Tip(homeButton, () => "Which house they live in: the basso or one of your flats. They sleep in whichever bed there is free, and eat there.");
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

            // A building's card can run long (a racket's options): it scrolls instead of running off the screen.
            propertyScroll = new ScrollView(ScrollViewMode.Vertical);
            propertyScroll.AddToClassList("insp-scroll");
            propertyScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            inspector.Add(propertyScroll);
            propertyPanel = Ui.Box("col").Into(propertyScroll);
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
            inspector.Show((count > 0 || property != null) && openWindow != Window.Business);
            propertyScroll.Show(count == 0 && property != null);
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
            homeButton.text = HomeLabel(p);
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
