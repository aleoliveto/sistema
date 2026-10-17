using System.Collections.Generic;
using System.Linq;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>The Lavoretti window (jobs on offer and in progress) and floating money popups.</summary>
    public sealed partial class Hud
    {
        VisualElement jobsWindow, jobsBody;
        ScrollView jobsScroll;
        Button jobsButton;
        int jobsVersion = -1;
        float jobsTimer;

        readonly List<(Label label, Vector2 world, float born)> popups = new List<(Label, Vector2, float)>();
        Game popupGame;

        void BuildJobsWindow()
        {
            jobsWindow = Ui.Box("panel", "window").Into(hudLayer);
            jobsWindow.style.width = 620;
            var header = Ui.Box("window__header").Into(jobsWindow);
            Ui.Text("TACCUINO", header, "title", "gold").style.marginRight = 12;
            Ui.Text("Who has work, and where to find them. Work is offered face to face.", header, "small", "muted");
            Ui.Box("spacer").Into(header);
            Ui.Btn("✕", () => ToggleWindow(Window.Jobs), header, "btn--small");
            jobsScroll = new ScrollView(ScrollViewMode.Vertical).Cls("window__body").Into(jobsWindow);
            jobsBody = jobsScroll.contentContainer;
            jobsWindow.Show(false);
        }

        void RefreshJobs()
        {
            if (jobsButton != null)
            {
                int offerCount = game.Contracts.OfferCount;
                var label = jobsButton.Q<Label>(className: "tab__label");
                if (label != null) label.text = offerCount > 0 ? $"LAVORETTI · {offerCount}" : "LAVORETTI";
                jobsButton.Toggle("btn--on", openWindow == Window.Jobs || offerCount > 0);
            }
            if (openWindow != Window.Jobs) return;

            jobsTimer += RefreshSeconds;
            if (game.Contracts.Version == jobsVersion && jobsTimer < 1f) return;
            jobsVersion = game.Contracts.Version;
            jobsTimer = 0f;

            var scroll = jobsScroll.scrollOffset;
            jobsBody.Clear();
            var org = game.Org;
            Ui.Text($"<b>{org.RankName}</b>  ·  {org.NextRankGoal(game) ?? "The top of the System."}", jobsBody, "gold");

            var active = game.Contracts.All.Where(c => c.State == ContractState.Active).ToList();
            if (active.Count > 0)
            {
                Ui.Text("UNDER WAY", jobsBody, "section");
                foreach (var c in active)
                {
                    var row = Ui.Box("table-row").Into(jobsBody);
                    Ui.Text($"<b>{c.Assignee.DisplayName}</b> · {c.Title} for {c.Giver}", row).style.width = 380;
                    Ui.Text(c.Assignee.CurrentJob?.Label ?? "", row, "small", "muted");
                    var p = c.Assignee;
                    Ui.Box("spacer").Into(row);
                    Ui.Btn("Show", () => FocusCamera(p.Position), row, "btn--small");
                }
            }

            long now = game.Ticks.TicksGame;
            Ui.Text("PEOPLE WITH WORK", jobsBody, "section");
            Ui.Text("Select someone from the crew and right-click a contact to go and talk to them.", jobsBody, "small", "muted");
            int hour = game.Clock.Hour;
            foreach (var contact in game.Contacts.All)
            {
                var row = Ui.Box("table-row").Into(jobsBody);
                var col = Ui.Box("col").Into(row);
                col.style.width = 420;
                bool work = game.Contacts.HasWork(contact, game);
                string state = !contact.OpenAt(hour) ? $"<color=#A89F8E>not around · {contact.Hours}</color>"
                    : work ? "<color=#E8C26A>has work for you</color>" : "<color=#A89F8E>nothing right now</color>";
                Ui.Text($"<b>{contact.Name}</b> · {state}", col);
                Ui.Text(contact.Who, col, "small", "muted", "wrap");
                var ct = contact;
                Ui.Box("spacer").Into(row);
                Ui.Btn("Show", () => FocusCamera(ct.Position), row, "btn--small");
            }

            // Shipments are the family's own business: anyone can be sent from here.
            var offers = game.Contracts.Offers.Where(c => c.IsShipment).ToList();
            if (offers.Count > 0) Ui.Text("SHIPMENTS TO COLLECT", jobsBody, "section");
            foreach (var c in offers)
            {
                var card = Ui.Box("panel").Into(jobsBody);
                card.style.marginBottom = 6;
                Ui.Text($"<b>{c.Title}</b>  <color=#A89F8E>· {c.Giver}</color>", card);
                Ui.Text($"“{c.Neapolitan}”", card, "gold", "small");
                Ui.Text(c.English, card, "small", "muted");
                Ui.Text(c.Text, card, "small", "wrap");
                long hours = (c.ExpiresTick - now) / GameClock.TicksPerHour;
                Ui.Text($"<color=#8FD18A>€{c.Reward} · respect +{c.RespectReward:0.#}</color>   <color=#F2B866>Risk: {c.Risk}</color>   <color=#A89F8E>expires in {hours}h</color>", card, "small", "wrap");

                var row = Ui.Row(card);
                Ui.Text("Send:", row, "small", "muted");
                foreach (var p in game.Pawns.Crew)
                {
                    if (!p.CanAct || game.Contracts.ActiveFor(p) != null) continue;
                    var pawn = p;
                    var contract = c;
                    var b = Ui.Btn(p.DisplayName, () =>
                    {
                        if (!game.Contracts.Assign(contract, pawn, game, out var error)) game.Log.Add(error, MessageKind.Bad);
                        jobsVersion = -1;
                        RefreshAll();
                    }, row, "btn--small");
                    Tip(b, () => SkillHint(contract, pawn));
                }
                if (c.From.HasValue)
                {
                    var at = c.From.Value;
                    Ui.Box("spacer").Into(row);
                    Ui.Btn("Show", () => FocusCamera(Mover.CellCenter(at)), row, "btn--small");
                }
            }
            jobsScroll.scrollOffset = scroll;
        }

        static string SkillHint(Contract c, Pawn p) => c.Kind switch
        {
            ContractKind.Debt => $"Intimidation {p.IntimidationSkill}: the better, the likelier the debtor pays.",
            ContractKind.ScooterTheft => $"Awareness {p.AwarenessSkill}. A stolen scooter is fast on the way back.",
            ContractKind.Smuggling => $"Awareness {p.AwarenessSkill}: helps spot the Guardia di Finanza.",
            _ => $"Awareness {p.AwarenessSkill}: helps avoid being stopped by the police.",
        };

        // ---------- Floating money ----------

        void HookPopups(Game g)
        {
            if (popupGame != null) popupGame.Popup -= OnPopup;
            popupGame = g;
            g.Popup += OnPopup;
        }

        void OnPopup(Vector2 at, string text)
        {
            if (!OSistema.Views.FogOfWar.CanSee(at)) return;
            var label = Ui.Text(text, worldLayer, "tag", text.StartsWith("-") ? "tag--rival" : "popup");
            popups.Add((label, at, Time.unscaledTime));
        }

        void UpdatePopups()
        {
            const float life = 1.6f;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var (label, world, born) = popups[i];
                float t = (Time.unscaledTime - born) / life;
                if (t >= 1f)
                {
                    label.RemoveFromHierarchy();
                    popups.RemoveAt(i);
                    continue;
                }
                var pos = WorldToPanel(world + Vector2.up * (0.6f + t * 1.2f));
                label.style.left = pos.x;
                label.style.top = pos.y;
                label.style.opacity = 1f - t * t;
            }
        }
    }
}
