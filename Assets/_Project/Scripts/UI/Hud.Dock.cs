using System.Collections.Generic;
using OSistema.Input;
using OSistema.Views;
using OSistema.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>The bottom dock: tabs (Build, Furnish, Crime, Orders) opening a row of tools.</summary>
    public sealed partial class Hud
    {
        enum DockTab { None, Build, Furnish, Crime, Orders }

        sealed class ToolEntry
        {
            public string Label;
            public string Tip;
            public Color Swatch;
            public Tool Tool;
            public BuildingDef Def;
            public System.Action Action;  // for buttons that aren't tools
            public Button Button;
            public Label Price;
        }

        VisualElement dockTools;
        readonly Dictionary<DockTab, List<ToolEntry>> toolsByTab = new Dictionary<DockTab, List<ToolEntry>>();
        readonly Dictionary<DockTab, Button> tabButtons = new Dictionary<DockTab, Button>();
        DockTab openTab;
        DockTab builtTab = DockTab.None;
        Button businessButton, helpButton;
        Label buildHint;

        void BuildDock()
        {
            toolsByTab[DockTab.Build] = new List<ToolEntry>
            {
                new ToolEntry { Label = "Open piazza", Tool = Tool.Square, Swatch = new Color(0.8f, 0.25f, 0.9f), Tip = "Click a pavement in your turf to open a drug square. Give crew the pusher and lookout jobs, and keep doses in stock." },
            };
            toolsByTab[DockTab.Furnish] = new List<ToolEntry>
            {
                Def(BuildingDefs.Bed, "Everyone wants a bed of their own."),
                Def(BuildingDefs.Table, "Gives cover in a firefight, too."),
                Def(BuildingDefs.Stove, "Cook groceries into meals."),
                Def(BuildingDefs.Pantry, "Stores meals and supplies. Needed to cook and shop."),
                Def(BuildingDefs.Sofa, "Somewhere to collapse after a long day."),
                Def(BuildingDefs.Armchair, "For the boss."),
                Def(BuildingDefs.Television, "The crew watches Napoli on it in the afternoon. Good for morale."),
                Def(BuildingDefs.Wardrobe, "Somewhere to hang the good suit."),
                Def(BuildingDefs.KitchenCounter, "A proper kitchen."),
                Def(BuildingDefs.Fridge, "Cold beer, cold water."),
                Def(BuildingDefs.Plant, "A bit of green."),
                Def(BuildingDefs.Rug, "A rug from the market."),
                Def(BuildingDefs.Safe, "Where the money sleeps."),
                Def(BuildingDefs.HomeShrine, "A little altar to the Madonna. Everyone feels watched over."),
                Def(BuildingDefs.Bunker, "A hidden room under the floor for the boss's latitanza. Searches rarely find it."),
            };
            toolsByTab[DockTab.Crime] = new List<ToolEntry>
            {
                Def(BuildingDefs.BaggingTable, "Where 'a roba is cut and bagged into half-gram doses. E to work at it, or give someone the Bagger job."),
                Def(BuildingDefs.RollingTable, "Where ROSSE are rolled and boxed. E to work at it, or give someone the Roller job."),
            };
            toolsByTab[DockTab.Orders] = new List<ToolEntry>
            {
                new ToolEntry { Label = "Move", Tool = Tool.Move, Swatch = new Color(0.6f, 0.8f, 0.95f), Tip = "Click a piece of furniture in your home to pick it up, then click where it goes (Q / E to turn it). Free." },
                new ToolEntry { Label = "Put away", Tool = Tool.Deconstruct, Swatch = new Color(0.9f, 0.3f, 0.25f), Tip = "Click or drag over furniture in your homes to put it back in storage. Walls and doors are knocked down for half what they cost." },
                new ToolEntry { Label = "Close piazza", Tool = Tool.Cancel, Swatch = new Color(0.95f, 0.65f, 0.3f), Tip = "Click a piazza of yours to close it." },
            };

            var dock = Ui.Box("dock").Into(hudLayer);
            dock.pickingMode = PickingMode.Ignore;
            buildHint = Ui.Text("", dock, "build-hint");
            dockTools = Ui.Box("panel", "dock__tools").Into(dock);
            buildTabs = Ui.Box("panel", "dock__tabs").Into(dock);
            buildTitle = Ui.Text("", buildTabs, "small", "muted");
            buildTitle.style.marginRight = 10;
            AddTab(buildTabs, DockTab.Furnish, "HOME", IconKind.Furnish);
            AddTab(buildTabs, DockTab.Crime, "WORK", IconKind.Crime);
            AddTab(buildTabs, DockTab.Build, "PIAZZA", IconKind.Product);
            AddTab(buildTabs, DockTab.Orders, "MOVE", IconKind.Orders);
            buildTabs.style.marginBottom = 4;

            // The hotbar: every screen, with its key.
            var tabs = Ui.Box("panel", "dock__tabs").Into(dock);
            buildButton = IconTab(tabs, "BUILD", IconKind.Build, ToggleBuildMode, "B");
            Tip(buildButton, () => "Build and furnish your homes, place work tables, open a piazza (B).");
            businessButton = IconTab(tabs, "FAMIGLIA", IconKind.Business, () => ToggleWindow(Window.Business), "Tab");
            Tip(businessButton, () => "Crew and their jobs, piazze, pizzo, property, money, rivals (Tab).");
            jobsButton = IconTab(tabs, "TACCUINO", IconKind.Jobs, () => ToggleWindow(Window.Jobs), "J");
            Tip(jobsButton, () => "Who has work for you and where to find them (J).");
            phoneButton = IconTab(tabs, "PHONE", IconKind.Phone, () => ToggleWindow(Window.Phone), "P");
            Tip(phoneButton, () => "The family phone (P): favours, gossip, warnings.");
            mapButton = IconTab(tabs, "MAP", IconKind.House, () => ToggleMap(), "M");
            Tip(mapButton, () => "The map of the rione (M). Click anywhere on it to set a waypoint.");
            helpButton = IconTab(tabs, "", IconKind.Help, () => ToggleWindow(Window.Help), "H");
            helpButton.style.minWidth = 36;
            Tip(helpButton, () => "Controls and help (H)");

        }

        ToolEntry Def(BuildingDef def, string tip) =>
            new ToolEntry { Label = def.Label, Tool = Tool.Build, Def = def, Swatch = def.Color, Tip = tip };

        void AddTab(VisualElement parent, DockTab tab, string label, IconKind icon)
        {
            tabButtons[tab] = IconTab(parent, label, icon, () =>
            {
                openTab = openTab == tab ? DockTab.None : tab;
                if (openTab == DockTab.None) controller.SetTool(Tool.Select);
                RefreshDock();
            });
        }

        static Button IconTab(VisualElement parent, string label, IconKind icon, System.Action onClick, string key = null)
        {
            var b = Ui.Btn("", onClick, parent, "tab");
            new Icon(icon, 16).Into(b);
            if (label.Length > 0) Ui.Text(label, b, "tab__label");
            if (key != null) Ui.Text(key, b, "tab__key");
            return b;
        }

        VisualElement buildTabs;
        Label buildTitle;
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
            openTab = buildMode ? DockTab.Furnish : DockTab.None;
            if (!buildMode) controller.SetTool(Tool.Select);
            RefreshDock();
        }

        void RefreshDock()
        {
            if (!buildMode && openTab != DockTab.None) buildMode = true;
            buildTabs.Show(buildMode);
            buildButton.Toggle("btn--on", buildMode);
            mapButton.Toggle("btn--on", mapOpen);
            foreach (var kv in tabButtons) kv.Value.Toggle("btn--on", kv.Key == openTab);
            buildTitle.text = openTab switch
            {
                DockTab.Furnish => "Put down furniture from storage (buy it at the Mobilificio) · Q / E turn · right-click to stop",
                DockTab.Crime => "Work tables from storage (Nanninella sells them) · E to work at one, or give someone the job",
                DockTab.Build => "Open a piazza on a pavement in your turf",
                DockTab.Orders => "Move furniture · put it back in storage · close a piazza",
                _ => "",
            };
            businessButton.Toggle("btn--on", openWindow == Window.Business);
            helpButton.Toggle("btn--on", openWindow == Window.Help);

            bool building = controller.CurrentTool == Tool.Build && controller.CurrentDef != null;
            bool rotatable = building && controller.CurrentDef != BuildingDefs.Wall && controller.CurrentDef != BuildingDefs.Door;
            buildHint.Show(building);
            if (building)
                buildHint.text = $"<b>{controller.CurrentDef.Label}</b> · " +
                    (BuildingDef.BuiltInPlace(controller.CurrentDef) ? $"€{BuildingDef.CostOf(controller.CurrentDef)} each" : $"{game.Stock.StoredOf(controller.CurrentDef)} in storage") +
                    (rotatable ? $" · <color=#E8C26A>Q / E</color> rotate ({controller.PlacementRotation * 90}°)" : " · drag to draw") +
                    " · right-click to stop";

            dockTools.Show(openTab != DockTab.None);
            if (openTab == DockTab.None) return;

            if (builtTab != openTab)
            {
                builtTab = openTab;
                dockTools.Clear();
                foreach (var entry in toolsByTab[openTab]) entry.Button = ToolButton(entry);
            }

            foreach (var entry in toolsByTab[openTab])
            {
                bool active = entry.Action == null && controller.CurrentTool == entry.Tool && controller.CurrentDef == entry.Def;
                entry.Button.Toggle("btn--on", active);
                if (entry.Price != null && entry.Def != null) entry.Price.text = PriceTag(entry.Def);
            }
        }

        string PriceTag(BuildingDef d) => BuildingDef.BuiltInPlace(d) ? $"€{BuildingDef.CostOf(d)}"
            : game.Stock.StoredOf(d) > 0 ? $"×{game.Stock.StoredOf(d)}" : "<color=#7A7468>none</color>";

        Button ToolButton(ToolEntry entry)
        {
            var b = Ui.Btn("", () =>
            {
                if (entry.Action != null) entry.Action();
                else controller.SetTool(entry.Tool, entry.Def);
                RefreshDock();
            }, dockTools, "tool");
            var swatch = Ui.Box("tool__swatch").Into(b);
            swatch.pickingMode = PickingMode.Ignore;
            if (entry.Def != null && game != null)
            {
                swatch.AddToClassList("tool__picture");
                swatch.style.backgroundImage = new StyleBackground(PreviewOf(entry.Def));
            }
            else swatch.style.backgroundColor = entry.Swatch;
            Ui.Text(entry.Label, b, "tool__label");
            if (entry.Def != null) entry.Price = Ui.Text(PriceTag(entry.Def), b, "tool__price");
            Tip(b, () => entry.Def == null ? entry.Tip
                : BuildingDef.BuiltInPlace(entry.Def)
                    ? $"{entry.Tip}\nCost €{BuildingDef.CostOf(entry.Def)}, paid where you put it."
                    : $"{entry.Tip}\n{game.Stock.StoredOf(entry.Def)} in storage. " +
                      (entry.Def == BuildingDefs.BaggingTable || entry.Def == BuildingDefs.RollingTable ? "Nanninella sells them" : "Buy more at the Mobilificio (on the map)") +
                      $"{(entry.Def.Beauty > 0 ? $" · makes a home nicer (+{entry.Def.Beauty:0.#})" : "")}");
            return b;
        }
    }
}
