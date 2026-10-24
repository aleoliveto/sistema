using System.Collections.Generic;
using OSistema.Input;
using OSistema.Views;
using OSistema.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>
    /// The bottom of the screen: the hotbar (every screen, with its key), and Build (B) as a drawer that
    /// slides up above it: category tabs, big cards with a picture and a price, and the few tools
    /// (piazza, move, put away) at the side.
    /// </summary>
    public sealed partial class Hud
    {
        enum BuildCat { Tutto, Vita, Cucina, Magazzino, Sicurezza, Decorazioni }

        sealed class ToolEntry
        {
            public string Label;
            public string Tip;
            public Color Swatch;
            public Tool Tool;
            public BuildingDef Def;
            public BuildCat Cat;
            public Button Button;
            public Label Price, Stock;
        }

        readonly List<ToolEntry> buildItems = new List<ToolEntry>();
        readonly List<ToolEntry> buildTools = new List<ToolEntry>();
        readonly Dictionary<BuildCat, Button> catButtons = new Dictionary<BuildCat, Button>();
        BuildCat buildCat;
        BuildCat? builtCat;
        VisualElement drawer, drawerCards, dockRoot;
        Button businessButton, helpButton, casaButton, piazzaButton;
        Label buildHint, drawerNote;

        void BuildDock()
        {
            void Item(BuildingDef def, BuildCat cat, string tip) =>
                buildItems.Add(new ToolEntry { Label = def.Label, Tool = Tool.Build, Def = def, Swatch = def.Color, Cat = cat, Tip = tip });
            Item(BuildingDefs.Bed, BuildCat.Vita, "A bed for each who sleeps at once: the day and night shifts take turns.");
            Item(BuildingDefs.Sofa, BuildCat.Vita, "Somewhere to collapse after a long day.");
            Item(BuildingDefs.Armchair, BuildCat.Vita, "For the boss.");
            Item(BuildingDefs.Television, BuildCat.Vita, "The crew watches Napoli on it in the afternoon. Good for morale.");
            Item(BuildingDefs.Wardrobe, BuildCat.Vita, "Somewhere to hang the good suit.");
            Item(BuildingDefs.Stove, BuildCat.Cucina, "Cook groceries into meals.");
            Item(BuildingDefs.Pantry, BuildCat.Cucina, "Stores meals and supplies. Needed to cook and shop.");
            Item(BuildingDefs.KitchenCounter, BuildCat.Cucina, "A proper kitchen.");
            Item(BuildingDefs.Fridge, BuildCat.Cucina, "Cold beer, cold water.");
            Item(BuildingDefs.Table, BuildCat.Cucina, "Gives cover in a firefight, too.");
            Item(BuildingDefs.RollingTable, BuildCat.Magazzino, "Where ROSSE are rolled and boxed. E to work at it, or give someone the Roller job.");
            Item(BuildingDefs.BaggingTable, BuildCat.Magazzino, "Where 'a roba is cut and bagged into half-gram doses. E to work at it, or give someone the Bagger job.");
            Item(BuildingDefs.Safe, BuildCat.Sicurezza, "Where the money sleeps.");
            Item(BuildingDefs.Bunker, BuildCat.Sicurezza, "A hidden room under the floor for the boss's latitanza. Searches rarely find it.");
            Item(BuildingDefs.Plant, BuildCat.Decorazioni, "A bit of green.");
            Item(BuildingDefs.Rug, BuildCat.Decorazioni, "A rug from the market.");
            Item(BuildingDefs.HomeShrine, BuildCat.Decorazioni, "A little altar to the Madonna. Everyone feels watched over.");

            buildTools.Add(new ToolEntry { Label = "Open piazza", Tool = Tool.Square, Swatch = new Color(0.8f, 0.25f, 0.9f), Tip = "Click a pavement in your turf to open a drug square. Give crew the pusher and lookout jobs, and keep doses in stock." });
            buildTools.Add(new ToolEntry { Label = "Close piazza", Tool = Tool.Cancel, Swatch = new Color(0.95f, 0.65f, 0.3f), Tip = "Click a piazza of yours to close it." });
            buildTools.Add(new ToolEntry { Label = "Move", Tool = Tool.Move, Swatch = new Color(0.6f, 0.8f, 0.95f), Tip = "Click a piece of furniture in your home to pick it up, then click where it goes (Q / E to turn it). Free." });
            buildTools.Add(new ToolEntry { Label = "Put away", Tool = Tool.Deconstruct, Swatch = new Color(0.9f, 0.3f, 0.25f), Tip = "Click or drag over furniture in your homes to put it back in storage. Walls and doors are knocked down for half what they cost." });

            dockRoot = Ui.Box("dock").Into(hudLayer);
            dockRoot.pickingMode = PickingMode.Ignore;
            buildHint = Ui.Text("", dockRoot, "build-hint");

            drawer = Ui.Box("panel", "drawer").Into(dockRoot);
            var head = Ui.Box("drawer__head").Into(drawer);
            Ui.ArtIcon("icon_build", 30, head)?.AddToClassList("panel-head__icon");
            Ui.Text("COSTRUISCI", head, "drawer__title").style.marginRight = 14;
            foreach (BuildCat c in System.Enum.GetValues(typeof(BuildCat)))
            {
                var cat = c;
                catButtons[c] = Ui.Btn(c.ToString(), () => { buildCat = cat; RefreshDock(); }, head, "pill");
            }
            Ui.Box("spacer").Into(head);
            foreach (var t in buildTools)
            {
                var entry = t;
                t.Button = Ui.Btn(t.Label, () => { controller.SetTool(entry.Tool); RefreshDock(); }, head, "btn--small", "drawer__tool");
                Tip(t.Button, () => entry.Tip);
            }
            Ui.Btn("✕", ToggleBuildMode, head, "btn--small").style.marginLeft = 8;
            var scroll = new ScrollView(ScrollViewMode.Horizontal).Cls("drawer__scroll").Into(drawer);
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            drawerCards = scroll.contentContainer;
            drawerCards.AddToClassList("drawer__cards");
            drawerNote = Ui.Text("", drawer, "tiny");
            drawerNote.Show(false);   // the same advice is on each card's tooltip
            drawer.Show(false);

            // Gennaro's bars sit just above the hotbar.

            // The bar along the bottom: every screen, with its key, and the news on the right.
            var tabs = Ui.Box("hotbar").Into(dockRoot);
            tabs.style.backgroundImage = new StyleBackground(Ui.Gradient(new Color(0.07f, 0.055f, 0.04f, 0.99f), new Color(0.03f, 0.025f, 0.02f, 0.99f)));
            casaButton = IconTab(tabs, "CASA", IconKind.House, () => OpenSection(FamSection.Casa), null, "how things stand", "icon_casa");
            Tip(casaButton, () => "How the family is doing, the stash, the money and property.");
            businessButton = IconTab(tabs, "FAMIGLIA", IconKind.Business, () => OpenSection(FamSection.Famiglia), "Tab", "your people", "icon_crew");
            Tip(businessButton, () => "Your people: jobs, wages, homes, and the organigram (Tab).");
            piazzaButton = IconTab(tabs, "PIAZZA", IconKind.Product, () => OpenSection(FamSection.Piazza), null, "the street", "icon_piazza");
            Tip(piazzaButton, () => "The street: your piazze, the pizzo, the neighbours and the other clans.");
            // The taccuino has no button for now (J still opens it).
            jobsButton = IconTab(new VisualElement(), "TACCUINO", IconKind.Jobs, () => ToggleWindow(Window.Jobs), "J", "lavoretti");
            Tip(jobsButton, () => "Who has work for you and where to find them (J).");
            phoneButton = IconTab(tabs, "PHONE", IconKind.Phone, () => ToggleWindow(Window.Phone), "P", "contacts", "icon_phone");
            Tip(phoneButton, () => "The family phone (P): favours, gossip, warnings.");
            mapButton = IconTab(tabs, "MAP", IconKind.Paper, () => ToggleMap(), "M", "the city", "icon_map");
            Tip(mapButton, () => "The map of the rione (M). Click anywhere on it to set a waypoint.");
            buildButton = IconTab(tabs, "BUILD", IconKind.Build, ToggleBuildMode, "B", "your home", "icon_build");
            Tip(buildButton, () => "Build and furnish your homes, place work tables, open a piazza (B).");
            helpButton = IconTab(tabs, "", IconKind.Help, () => ToggleWindow(Window.Help), "H", null, "icon_help");
            helpButton.AddToClassList("tab--icon");
            Tip(helpButton, () => "Controls and help (H)");
            Ui.Box("spacer").Into(tabs);
            var news = Ui.Btn("", () => { diaryOpen = !diaryOpen; diaryVersion = -1; RefreshFeed(); }, tabs, "news");
            Ui.Box("news__dot").Into(news).pickingMode = PickingMode.Ignore;
            var nc = Ui.Box("col", "grow").Into(news);
            nc.pickingMode = PickingMode.Ignore;
            newsTitle = Ui.Text("NEWS", nc, "news__title");
            newsText = Ui.Text("", nc, "news__text");
            Tip(news, () => "What has happened lately. Click to open the diary.");
        }

        static Button IconTab(VisualElement parent, string label, IconKind icon, System.Action onClick, string key = null, string sub = null, string art = null)
        {
            var b = Ui.Btn("", onClick, parent, "tab");
            if (art == null || Ui.ArtIcon(art, 32, b) == null) new Icon(icon, sub != null ? 20 : 16).Into(b);
            if (label.Length > 0)
            {
                var col = Ui.Box("col").Into(b);
                col.pickingMode = PickingMode.Ignore;
                Ui.Text(label, col, "tab__label");
                if (sub != null) Ui.Text(sub, col, "tab__sub");
            }
            if (key != null) Ui.Text(key, b, "tab__key");
            return b;
        }

        readonly Dictionary<BuildingDef, Texture2D> previews = new Dictionary<BuildingDef, Texture2D>();

        Texture2D PreviewOf(BuildingDef def)
        {
            if (previews.TryGetValue(def, out var tex) && tex != null) return tex;
            var art = PropArt.For(def, 0, 0);
            if (art != null) return previews[def] = art.texture;
            var px = new Color32[TilePainter.Hi * TilePainter.Hi];
            TilePainter.PaintPreviewHi(game.Map, def, 0, 0, 0, px);
            tex = new Texture2D(TilePainter.Hi, TilePainter.Hi, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixels32(px);
            tex.Apply();
            previews[def] = tex;
            return tex;
        }
        Button buildButton, mapButton;
        bool buildMode;

        void ToggleBuildMode()
        {
            buildMode = !buildMode;
            if (!buildMode) controller.SetTool(Tool.Select);
            RefreshDock();
        }

        Tool dockTool;

        void RefreshDock()
        {
            // While placing something the drawer gets out of the way; right-click stops and it comes back.
            drawer.Show(buildMode && controller.CurrentTool == Tool.Select);
            buildButton.Toggle("btn--on", buildMode);
            mapButton.Toggle("btn--on", mapOpen);
            casaButton.Toggle("btn--on", SectionOpen(FamSection.Casa));
            businessButton.Toggle("btn--on", SectionOpen(FamSection.Famiglia));
            piazzaButton.Toggle("btn--on", SectionOpen(FamSection.Piazza));
            helpButton.Toggle("btn--on", openWindow == Window.Help);

            bool building = controller.CurrentTool == Tool.Build && controller.CurrentDef != null;
            bool rotatable = building && controller.CurrentDef != BuildingDefs.Wall && controller.CurrentDef != BuildingDefs.Door;
            buildHint.Show(building);
            if (building)
                buildHint.text = $"<b>{controller.CurrentDef.Label}</b> · " +
                    (BuildingDef.BuiltInPlace(controller.CurrentDef) ? $"€{BuildingDef.CostOf(controller.CurrentDef)} each" : $"{game.Stock.StoredOf(controller.CurrentDef)} in storage") +
                    (rotatable ? $" · <color=#E8C26A>Q / E</color> rotate ({controller.PlacementRotation * 90}°)" : " · drag to draw") +
                    " · right-click for the menu";

            if (!buildMode) return;
            foreach (var kv in catButtons) kv.Value.Toggle("pill--on", kv.Key == buildCat);
            foreach (var t in buildTools) t.Button.Toggle("btn--on", controller.CurrentTool == t.Tool);

            if (builtCat != buildCat || game == null)
            {
                builtCat = buildCat;
                drawerCards.Clear();
                foreach (var entry in buildItems)
                    if (buildCat == BuildCat.Tutto || entry.Cat == buildCat) entry.Button = ItemCard(entry);
                    else entry.Button = null;
            }
            drawerNote.text = "Furniture comes from the Mobilificio, work tables from Nanninella; what you've bought waits in storage. Q / E turn · right-click to stop.";
            foreach (var entry in buildItems)
            {
                if (entry.Button == null) continue;
                bool active = controller.CurrentTool == entry.Tool && controller.CurrentDef == entry.Def;
                entry.Button.Toggle("bcard--on", active);
                int stored = game.Stock.StoredOf(entry.Def);
                bool inPlace = BuildingDef.BuiltInPlace(entry.Def);
                entry.Button.Toggle("bcard--none", !inPlace && stored == 0);
                entry.Stock.text = inPlace ? "build" : stored > 0 ? $"×{stored}" : "0";
                entry.Price.text = $"€{BuildingDef.CostOf(entry.Def)}";
            }
        }

        Button ItemCard(ToolEntry entry)
        {
            var b = Ui.Btn("", () =>
            {
                controller.SetTool(entry.Tool, entry.Def);
                RefreshDock();
            }, drawerCards, "bcard");
            var pic = Ui.Box("bcard__pic").Into(b);
            pic.pickingMode = PickingMode.Ignore;
            if (game != null) pic.style.backgroundImage = new StyleBackground(PreviewOf(entry.Def));
            else pic.style.backgroundColor = entry.Swatch;
            entry.Stock = Ui.Text("", b, "bcard__stock");
            Ui.Text(entry.Label, b, "bcard__name");
            var foot = Ui.Box("bcard__foot").Into(b);
            foot.pickingMode = PickingMode.Ignore;
            entry.Price = Ui.Text("", foot, "bcard__price");
            var sz = entry.Def.Size;
            Ui.Text($"{sz.x}×{sz.y}", foot, "tiny");
            string perk = entry.Def.Beauty > 0 ? $"Comfort +{entry.Def.Beauty:0.#}" : entry.Def.Cover ? "Cover in a fight" : entry.Cat == BuildCat.Magazzino ? "Work station" : null;
            if (perk != null) Ui.Text(perk, b, "bcard__perk");
            Tip(b, () => BuildingDef.BuiltInPlace(entry.Def)
                ? $"{entry.Tip}\nCost €{BuildingDef.CostOf(entry.Def)}, paid where you put it."
                : $"{entry.Tip}\n{game.Stock.StoredOf(entry.Def)} in storage. " +
                  (entry.Def == BuildingDefs.BaggingTable || entry.Def == BuildingDefs.RollingTable ? "Nanninella sells them" : "Buy more at the Mobilificio (on the map)") +
                  $" for €{BuildingDef.CostOf(entry.Def)}{(entry.Def.Beauty > 0 ? $" · makes a home nicer (+{entry.Def.Beauty:0.#})" : "")}");
            return b;
        }
    }
}
