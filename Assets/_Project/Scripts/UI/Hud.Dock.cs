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
            };
            toolsByTab[DockTab.Crime] = new List<ToolEntry>
            {
                new ToolEntry { Label = "Piazza", Tool = Tool.Square, Swatch = new Color(0.8f, 0.25f, 0.9f), Tip = "Open a drug square on a sidewalk. Then assign pushers and lookouts and order product (B)." },
                new ToolEntry { Label = "Business", Swatch = new Color(0.91f, 0.76f, 0.42f), Action = () => ToggleWindow(Window.Business), Tip = "The business panel: crew, piazze, pizzo, supplier, arms, rivals (B)." },
            };
            toolsByTab[DockTab.Orders] = new List<ToolEntry>
            {
                new ToolEntry { Label = "Select", Tool = Tool.Select, Swatch = new Color(0.9f, 0.9f, 0.9f), Tip = "Select crew. Right-click to move, or to attack a rival (Esc)." },
                new ToolEntry { Label = "Draft", Swatch = new Color(0.94f, 0.48f, 0.43f), Action = () => controller.ToggleDraftSelected(), Tip = "Draft or release the selected crew (R)." },
                new ToolEntry { Label = "Demolish", Tool = Tool.Deconstruct, Swatch = new Color(0.9f, 0.3f, 0.25f), Tip = "Drag over buildings to tear them down." },
                new ToolEntry { Label = "Cancel", Tool = Tool.Cancel, Swatch = new Color(0.95f, 0.65f, 0.3f), Tip = "Drag to cancel blueprints or close a piazza." },
            };

            var dock = Ui.Box("dock").Into(hudLayer);
            dock.pickingMode = PickingMode.Ignore;
            dockTools = Ui.Box("panel", "dock__tools").Into(dock);
            var tabs = Ui.Box("panel", "dock__tabs").Into(dock);

            AddTab(tabs, DockTab.Build, "BUILD");
            AddTab(tabs, DockTab.Furnish, "FURNISH");
            AddTab(tabs, DockTab.Crime, "CRIME");
            AddTab(tabs, DockTab.Orders, "ORDERS");
            Ui.Box().Into(tabs).style.width = 18;
            businessButton = Ui.Btn("Business (B)", () => ToggleWindow(Window.Business), tabs, "tab");
            helpButton = Ui.Btn("Help (H)", () => ToggleWindow(Window.Help), tabs, "tab");
        }

        ToolEntry Def(BuildingDef def, string tip) =>
            new ToolEntry { Label = def.Label, Tool = Tool.Build, Def = def, Swatch = def.Color, Tip = tip };

        void AddTab(VisualElement parent, DockTab tab, string label)
        {
            tabButtons[tab] = Ui.Btn(label, () =>
            {
                openTab = openTab == tab ? DockTab.None : tab;
                if (openTab == DockTab.None) controller.SetTool(Tool.Select);
                RefreshDock();
            }, parent, "tab");
        }

        void RefreshDock()
        {
            foreach (var kv in tabButtons) kv.Value.Toggle("btn--on", kv.Key == openTab);
            businessButton.Toggle("btn--on", openWindow == Window.Business);
            helpButton.Toggle("btn--on", openWindow == Window.Help);

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
            Tip(b, () => entry.Def != null ? $"{entry.Tip}\nWork: {entry.Def.WorkToBuild}" : entry.Tip);
            return b;
        }
    }
}
