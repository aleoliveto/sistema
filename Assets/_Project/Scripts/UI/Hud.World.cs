using System.Collections.Generic;
using OSistema.Crime;
using OSistema.Npcs;
using OSistema.Pawns;
using OSistema.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>
    /// Labels drawn over the world: names under people, speech bubbles with Neapolitan barks and
    /// subtitles, shop and piazza names. Only for what the crew can currently see (or remembers).
    /// </summary>
    public sealed partial class Hud
    {
        readonly List<Label> tags = new List<Label>();
        readonly List<VisualElement> bubbles = new List<VisualElement>();
        int tagsUsed, bubblesUsed;
        readonly List<(VisualElement box, Label label, VisualElement fill)> actions = new List<(VisualElement, Label, VisualElement)>();
        int actionsUsed;

        // "Eating... ███░░" over someone busy with something that takes time.
        void ActionBar(Vector2 panelPos, float unit, string text, float progress)
        {
            if (actionsUsed >= actions.Count)
            {
                var box = Ui.Box("action").Into(worldLayer);
                box.pickingMode = PickingMode.Ignore;
                var label = Ui.Text("", box, "action__label");
                var bar = Ui.Box("action__bar").Into(box);
                var fill = Ui.Box("action__fill").Into(bar);
                actions.Add((box, label, fill));
            }
            var a = actions[actionsUsed++];
            a.box.style.display = DisplayStyle.Flex;
            a.label.text = text;
            a.fill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
            a.box.style.left = panelPos.x;
            a.box.style.top = panelPos.y - unit * 1.15f;
        }

        void ResetWorldLabels()
        {
            worldLayer.Clear();
            tags.Clear();
            bubbles.Clear();
            actions.Clear();   // the bars went with the layer: build new ones
        }

        Label NextTag()
        {
            if (tagsUsed >= tags.Count) tags.Add(Ui.Text("", worldLayer, "tag"));
            var t = tags[tagsUsed++];
            t.style.display = DisplayStyle.Flex;
            t.ClearClassList();
            t.AddToClassList("tag");
            return t;
        }

        VisualElement NextBubble()
        {
            if (bubblesUsed >= bubbles.Count)
            {
                var b = Ui.Box("bubble").Into(worldLayer);
                b.pickingMode = PickingMode.Ignore;
                Ui.Text("", b, "bubble__nap");
                Ui.Text("", b, "bubble__en");
                bubbles.Add(b);
            }
            var bubble = bubbles[bubblesUsed++];
            bubble.style.display = DisplayStyle.Flex;
            return bubble;
        }

        void UpdateWorldLabels()
        {
            tagsUsed = 0;
            bubblesUsed = 0;
            actionsUsed = 0;
            float zoom = cam.orthographicSize;
            float unitPx = WorldToPanel(Vector2.up).y - WorldToPanel(Vector2.zero).y; // panel pixels per world unit (negative: y is down)
            float unit = Mathf.Abs(unitPx);
            bool close = zoom < 7.5f;      // names for everyone only when zoomed right in
            bool veryClose = zoom < 6.5f;  // shop names even closer
            var selected = controller.Selected;

            foreach (var p in game.Pawns.All)
            {
                if (p.InJail || p.InHospital || p.Departed) continue;
                bool rival = p.Faction == Faction.Rival;
                if (rival && !FogOfWar.CanSee(p.Position)) continue;
                if (rival && p.Dead) continue;
                if (!OnScreen(p.Position)) continue;

                var pos = WorldToPanel(p.Position);
                bool important = AlwaysShowNames || selected.Contains(p) || p.Drafted || p.Health.Downed || p.Dead || rival;
                if (close || important)
                {
                    var tag = NextTag();
                    string status = p.Dead ? " ✝" : p.Health.Downed ? " (down)" : p.Sleep != SleepState.Awake ? " zZ" : "";
                    tag.text = (rival ? p.Nickname : p.DisplayName) + status;
                    if (rival) tag.AddToClassList("tag--rival");
                    else if (p.IsBoss) tag.AddToClassList("tag--boss");
                    tag.style.left = pos.x;
                    tag.style.top = pos.y + unit * 0.42f;
                }

                // What they're busy doing, with progress; and the zZz of a sleeper.
                if (!rival && !p.Dead && p.CurrentJob != null)
                {
                    float prog = p.CurrentJob.Progress;
                    bool mine = p.Controlled || selected.Contains(p);
                    if (prog >= 0f && (mine || close)) ActionBar(pos, unit, p.CurrentJob.Label, prog);
                }
                if (!rival && !p.Dead && p.Sleep != SleepState.Awake)
                {
                    var z = NextTag();
                    z.AddToClassList("tag--sleep");
                    float t = (Time.unscaledTime * 0.7f + p.Id * 0.37f) % 1f;
                    z.text = t < 0.33f ? "z" : t < 0.66f ? "z z" : "z z Z";
                    z.style.left = pos.x + unit * (0.3f + t * 0.3f);
                    z.style.top = pos.y - unit * (0.5f + t * 0.5f);
                    z.style.opacity = 1f - t * 0.6f;
                }

                if (p.HasBark && !p.Dead && !rival && close)
                {
                    var bubble = NextBubble();
                    // A short shout stays in dialect; anything longer is said in English.
                    bool nap = OSistema.Story.Dialect.Show(p.Bark.Neapolitan);
                    ((Label)bubble[0]).text = nap ? p.Bark.Neapolitan : p.Bark.English;
                    ((Label)bubble[1]).text = nap ? p.Bark.English : "";
                    bubble.style.left = pos.x;
                    bubble.style.top = pos.y - unit * 0.55f;
                }
            }

            var reach = controller.ContactInReach(out _);

            // What E would do right now, over the player's head.
            var player = game.Player;
            var act = player != null && !MinigameOpen && game.Events.Current == null ? controller.Interaction() : null;
            if (act != null && reach == null)
            {
                var tag = NextTag();
                tag.AddToClassList("tag--work");
                tag.text = $"<b>[E]</b> {act.Label}";
                var pos = WorldToPanel(player.Position);
                tag.style.left = pos.x;
                tag.style.top = pos.y - unit * 1.1f;
            }
            foreach (var contact in game.Contacts.All)
            {
                if (!contact.Present || !FogOfWar.CanSee(contact.Position) || !OnScreen(contact.Position)) continue;
                bool work = game.Contacts.HasWork(contact, game);
                bool inReach = contact == reach;
                if (!work && !close && !inReach) continue;
                var tag = NextTag();
                tag.text = (work ? $"{contact.Name}  <b>!</b>" : contact.Name) + (inReach ? "   <b>[E]</b> talk" : "");
                tag.AddToClassList(work ? "tag--work" : "tag--place");
                var pos = WorldToPanel(contact.Position);
                tag.style.left = pos.x;
                tag.style.top = pos.y + unit * 0.42f;
            }

            // Someone coming to have a word with you.
            var v = game.Events.Visitor;
            if (v != null && FogOfWar.CanSee(v.Mover.Position) && OnScreen(v.Mover.Position))
            {
                var tag = NextTag();
                tag.AddToClassList("tag--work");
                tag.text = $"{game.Events.VisitorTitle} · wants a word";
                var pos = WorldToPanel(v.Mover.Position);
                tag.style.left = pos.x;
                tag.style.top = pos.y + unit * 0.42f;
            }

            foreach (var npc in game.Npcs.All)
            {
                if (npc.Kind != NpcKind.Police || !FogOfWar.CanSee(npc.Mover.Position) || !OnScreen(npc.Mover.Position)) continue;
                var tag = NextTag();
                tag.text = npc.IsPatrol ? "Police patrol" : "Police";
                tag.AddToClassList("tag--police");
                var pos = WorldToPanel(npc.Mover.Position);
                tag.style.left = pos.x;
                tag.style.top = pos.y + unit * 0.42f;
            }

            if (veryClose)
            {
                foreach (var b in game.Org.Businesses)
                {
                    if (!FogOfWar.HasExplored(b.Cell)) continue;
                    var at = new Vector2(b.Cell.x + 0.5f, b.Cell.y + 0.5f);
                    if (!OnScreen(at)) continue;
                    var tag = NextTag();
                    tag.AddToClassList("tag--place");
                    string state = b.Status == BusinessStatus.Paying ? " · pizzo" : b.Status == BusinessStatus.Refusing ? " · refused" : b.Status == BusinessStatus.Closed ? " · burned" : "";
                    tag.text = b.Name + state;
                    var pos = WorldToPanel(at);
                    tag.style.left = pos.x;
                    tag.style.top = pos.y - unit * 1.1f;
                }
            }

            foreach (var sq in game.Org.Squares) SquareTag(sq, "Piazza", "tag--boss");
            foreach (var clan in game.Clans)
                if (!clan.Destroyed)
                    foreach (var sq in clan.Squares)
                        if (FogOfWar.HasExplored(sq.Cell)) SquareTag(sq, $"{clan.Name} piazza", "tag--rival");

            for (int i = tagsUsed; i < tags.Count; i++) tags[i].style.display = DisplayStyle.None;
            for (int i = actionsUsed; i < actions.Count; i++) actions[i].box.style.display = DisplayStyle.None;
            for (int i = bubblesUsed; i < bubbles.Count; i++) bubbles[i].style.display = DisplayStyle.None;

            void SquareTag(DrugSquare sq, string text, string cls)
            {
                var at = new Vector2(sq.Cell.x + 0.5f, sq.Cell.y + 0.5f);
                if (!OnScreen(at)) return;
                var zone = game.Territory.Zones[sq.ZoneId];
                var tag = NextTag();
                tag.AddToClassList(cls);
                tag.text = sq.Rival ? text : $"{text} · heat {zone.Heat:0}";
                var pos = WorldToPanel(at);
                tag.style.left = pos.x;
                tag.style.top = pos.y + unit * 0.9f;
            }
        }

        bool OnScreen(Vector2 world)
        {
            var v = cam.WorldToViewportPoint(new Vector3(world.x, world.y, 0f));
            return v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }
    }
}
