using System.Collections.Generic;
using OSistema.Input;
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
                Def(BuildingDefs.Wall, "Drag to draw the outline of a room."),
                Def(BuildingDefs.Door, "A door. Crew walk through; sight doesn't."),
                Def(BuildingDefs.Bunker, "A hidden room for the boss's latitanza. Searches rarely find it."),
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
            };
            toolsByTab[DockTab.Crime] = new List<ToolEntry>
            {
                Def(BuildingDefs.BaggingTable, "Where 'a roba is cut and bagged into half-gram doses. Press E at it, or give someone the Bagger job."),
                Def(BuildingDefs.RollingTable, "Where ROSSE are rolled and boxed. Press E at it, or give someone the Roller job."),
                new ToolEntry { Label = "Piazza", Tool = Tool.Square, Swatch = new Color(0.8f, 0.25f, 0.9f), Tip = "Open a drug square on a sidewalk. Then assign pushers and lookouts and order product (B)." },
                new ToolEntry { Label = "Business", Swatch = new Color(0.91f, 0.76f, 0.42f), Action = () => ToggleWindow(Window.Business), Tip = "The business panel: crew, piazze, pizzo, supplier, arms, rivals (B)." },
            };
            toolsByTab[DockTab.Orders] = new List<ToolEntry>
            {
                new ToolEntry { Label = "Select", Tool = Tool.Select, Swatch = new Color(0.9f, 0.9f, 0.9f), Tip = "Select crew. Right-click to move, or to attack a rival (Esc)." },
                new ToolEntry { Label = "Demolish", Tool = Tool.Deconstruct, Swatch = new Color(0.9f, 0.3f, 0.25f), Tip = "Drag over buildings to tear them down." },
                new ToolEntry { Label = "Cancel", Tool = Tool.Cancel, Swatch = new Color(0.95f, 0.65f, 0.3f), Tip = "Drag to cancel blueprints or close a piazza." },
            };

            var dock = Ui.Box("dock").Into(hudLayer);
            dock.pickingMode = PickingMode.Ignore;
            buildHint = Ui.Text("", dock, "build-hint");
            dockTools = Ui.Box("panel", "dock__tools").Into(dock);
            var tabs = Ui.Box("panel", "dock__tabs").Into(dock);

            AddTab(tabs, DockTab.Build, "BUILD", IconKind.Build);
            AddTab(tabs, DockTab.Furnish, "FURNISH", IconKind.Furnish);
            AddTab(tabs, DockTab.Crime, "CRIME", IconKind.Crime);
            AddTab(tabs, DockTab.Orders, "ORDERS", IconKind.Orders);
            Ui.Box("divider").Into(tabs).style.height = 24;
            jobsButton = IconTab(tabs, "LAVORETTI", IconKind.Jobs, () => ToggleWindow(Window.Jobs));
            Tip(jobsButton, () => "Jobs from the neighbourhood's contacts (J).");
            phoneButton = IconTab(tabs, "PHONE", IconKind.Phone, () => ToggleWindow(Window.Phone));
            Tip(phoneButton, () => "The family phone (P): favours, gossip, warnings.");
            businessButton = IconTab(tabs, "BUSINESS", IconKind.Business, () => ToggleWindow(Window.Business));
            Tip(businessButton, () => "Crew, piazze, pizzo, supplies, property, rivals (B).");
            helpButton = IconTab(tabs, "", IconKind.Help, () => ToggleWindow(Window.Help));
            helpButton.style.minWidth = 36;
            Tip(helpButton, () => "Help (H)");

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

        static Button IconTab(VisualElement parent, string label, IconKind icon, System.Action onClick)
        {
            var b = Ui.Btn("", onClick, parent, "tab");
            new Icon(icon, 16).Into(b);
            if (label.Length > 0) Ui.Text(label, b, "tab__label");
            return b;
        }

        void RefreshDock()
        {
            foreach (var kv in tabButtons) kv.Value.Toggle("btn--on", kv.Key == openTab);
            businessButton.Toggle("btn--on", openWindow == Window.Business);
            helpButton.Toggle("btn--on", openWindow == Window.Help);

            bool building = controller.CurrentTool == Tool.Build && controller.CurrentDef != null;
            bool rotatable = building && controller.CurrentDef != BuildingDefs.Wall && controller.CurrentDef != BuildingDefs.Door;
            buildHint.Show(building);
            if (building)
                buildHint.text = $"<b>{controller.CurrentDef.Label}</b> · €{BuildingDef.CostOf(controller.CurrentDef)} each" +
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
            }
        }

        Button ToolButton(ToolEntry entry)
        {
            var b = Ui.Btn("", () =>
            {
                if (entry.Action != null) entry.Action();
                else controller.SetTool(entry.Tool, entry.Def);
                RefreshDock();
            }, dockTools, "tool");
            var swatch = Ui.Box("tool__swatch").Into(b);
            swatch.style.backgroundColor = entry.Swatch;
            swatch.pickingMode = PickingMode.Ignore;
            Ui.Text(entry.Label, b, "tool__label");
            Tip(b, () => entry.Def != null
                ? $"{entry.Tip}\nCost €{BuildingDef.CostOf(entry.Def)}{(entry.Def.Beauty > 0 ? $" · makes a home nicer (+{entry.Def.Beauty:0.#})" : "")}"
                : entry.Tip);
            return b;
        }
    }
}
