using System.Collections.Generic;
using System.Linq;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Npcs;
using OSistema.Input;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OSistema.UI
{
    /// <summary>
    /// Prototype HUD drawn with IMGUI. Throwaway on purpose: it gets replaced by UI Toolkit
    /// once the screens are settled.
    /// </summary>
    public sealed partial class Hud : MonoBehaviour
    {
        const float TopBarHeight = 34f;
        const float BottomBarHeight = 46f;
        const float PanelWidth = 340f;
        const float PanelHeight = 350f;
        const int MessageTicksVisible = 6000;

        static readonly string[] SpeedLabels = { "II", ">", ">>", ">>>" };
        static readonly Color Gold = new Color(1f, 0.85f, 0.4f);
        static readonly Color GoodColor = new Color(0.55f, 0.9f, 0.5f);
        static readonly Color BadColor = new Color(1f, 0.5f, 0.45f);

        Game game;
        PlayerController controller;
        Camera cam;
        bool showHelp = true;

        GUIStyle nameStyle, barkStyle, subtitleStyle, titleStyle, textStyle, smallStyle, shopStyle, smallButtonStyle, headerStyle;

        public void Init(PlayerController pc, Camera c)
        {
            controller = pc;
            cam = c;
        }

        public void Bind(Game g) => game = g;

        // Scale the UI with screen height so it stays readable on Retina displays.
        static float Scale => Mathf.Max(1f, Screen.height / 900f);
        static float Width => Screen.width / Scale;
        static float Height => Screen.height / Scale;

        static Rect TopBar => new Rect(0, 0, Width, TopBarHeight);
        static Rect BottomBar => new Rect(0, Height - BottomBarHeight, Width, BottomBarHeight);
        static Rect PawnPanel => new Rect(8, Height - BottomBarHeight - PanelHeight - 8, PanelWidth, PanelHeight);
        static Rect HelpPanel => new Rect(Width - 300, TopBarHeight + 8, 292, 310);

        public bool IsPointerOver(Vector2 screenPos)
        {
            var p = new Vector2(screenPos.x / Scale, (Screen.height - screenPos.y) / Scale);
            if (TopBar.Contains(p) || BottomBar.Contains(p)) return true;
            if (controller != null && controller.Selected.Count > 0 && PawnPanel.Contains(p)) return true;
            if (showBusiness && BusinessPanel.Contains(p)) return true;
            return showHelp && HelpPanel.Contains(p);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.hKey.wasPressedThisFrame) showHelp = !showHelp;
            if (kb.bKey.wasPressedThisFrame) showBusiness = !showBusiness;
        }

        void OnGUI()
        {
            if (game == null) return;
            EnsureStyles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));

            DrawShopLabels();
            DrawSquareLabels();
            DrawNpcLabels();
            DrawPawnLabels();
            DrawTopBar();
            DrawMessages();
            DrawBottomBar();
            DrawPawnPanel();
            if (showBusiness) DrawBusinessPanel();
            else if (showHelp) DrawHelp();
        }

        // ---------- Top bar ----------

        void DrawTopBar()
        {
            GUI.Box(TopBar, GUIContent.none);
            var clock = game.Clock;
            string payday = clock.IsPayday ? "  ·  Payday" : "";
            GUI.Label(new Rect(10, 6, 420, 24), $"Day {clock.Day}  ·  {clock.Weekday}  {clock.TimeString}  ·  {clock.Phase}{payday}", titleStyle);

            var s = game.Stock;
            GUI.Label(new Rect(420, 7, 640, 24),
                $"<b>€{s.Cash}</b>    Product {s.Product}    Meals {s.Meals}    Groceries {s.Groceries}    Medicine {s.Medicine}    Respect {game.Org.Respect:0}", textStyle);

            float x = Width - 4 * 46 - 8;
            for (int i = 0; i < SpeedLabels.Length; i++)
            {
                GUI.color = game.Ticks.Speed == i ? Gold : Color.white;
                if (GUI.Button(new Rect(x + i * 46, 5, 42, 24), SpeedLabels[i]))
                {
                    if (i == 0) game.Ticks.TogglePause();
                    else game.Ticks.SetSpeed(i);
                }
            }
            GUI.color = Color.white;
        }

        // ---------- Message feed ----------

        void DrawMessages()
        {
            long now = game.Ticks.TicksGame;
            var recent = game.Log.All.Where(m => now - m.Tick < MessageTicksVisible).Reverse().Take(6).ToList();
            float y = TopBarHeight + 8;
            foreach (var m in recent)
            {
                textStyle.normal.textColor = m.Kind == MessageKind.Good ? GoodColor : m.Kind == MessageKind.Bad ? BadColor : Color.white;
                ShadowLabel(new Rect(10, y, 520, 20), m.Text, textStyle);
                y += 20;
            }
            textStyle.normal.textColor = Color.white;
        }

        // ---------- Bottom bar ----------

        void DrawBottomBar()
        {
            var bar = BottomBar;
            GUI.Box(bar, GUIContent.none);
            float x = 8f, y = bar.y + 8f, w = 84f, h = 30f;

            ToolButton(ref x, y, w, h, "Select", Tool.Select, null);
            foreach (var def in BuildingDefs.Buildable)
                ToolButton(ref x, y, w, h, def.Label, Tool.Build, def);
            x += 12f;
            ToolButton(ref x, y, w, h, "Demolish", Tool.Deconstruct, null);
            ToolButton(ref x, y, w, h, "Cancel", Tool.Cancel, null);
            x += 12f;
            ToolButton(ref x, y, w, h, "Piazza", Tool.Square, null);
            GUI.color = showBusiness ? Gold : Color.white;
            if (GUI.Button(new Rect(x, y, 110, h), "Business (B)")) showBusiness = !showBusiness;
            GUI.color = Color.white;
        }

        void ToolButton(ref float x, float y, float w, float h, string label, Tool tool, BuildingDef def)
        {
            bool active = controller.CurrentTool == tool && controller.CurrentDef == def;
            GUI.color = active ? Gold : Color.white;
            if (GUI.Button(new Rect(x, y, w, h), label)) controller.SetTool(tool, def);
            GUI.color = Color.white;
            x += w + 4f;
        }

        // ---------- Selected pawn ----------

        void DrawPawnPanel()
        {
            if (controller.Selected.Count == 0) return;
            var r = PawnPanel;
            GUI.Box(r, GUIContent.none);
            float x = r.x + 10, w = r.width - 20, y = r.y + 6;

            if (controller.Selected.Count > 1)
            {
                GUI.Label(new Rect(x, y, w, 24), $"{controller.Selected.Count} crew selected", titleStyle);
                DrawDraftButton(r);
                return;
            }

            var p = controller.Selected[0];
            long now = game.Ticks.TicksGame;
            GUI.Label(new Rect(x, y, w, 24), $"{p.FullName}  “{p.Nickname}”", titleStyle);
            y += 24;

            string job = p.CurrentJob != null ? p.CurrentJob.Label : "Thinking";
            if (p.Carrying != null) job += $"  (carrying {p.Carrying})";
            GUI.Label(new Rect(x, y, w, 20), $"Doing: {job}", textStyle);
            y += 22;

            Bar(x, y, w, $"Mood: {p.Mood.Value:0} ({p.Mood.Description})", p.Mood.Value / 100f, MoodColor(p.Mood.Value));
            y += 20;
            Bar(x, y, w, "Food", p.Needs.Food, new Color(0.85f, 0.6f, 0.25f));
            y += 20;
            Bar(x, y, w, "Rest", p.Needs.Rest, new Color(0.4f, 0.55f, 0.9f));
            y += 24;

            GUI.Label(new Rect(x, y, w, 20), "<b>Health</b>" + HealthSummary(p), textStyle);
            y += 20;
            foreach (var inj in p.Health.Injuries.Take(3))
            {
                string state = inj.IsMalnutrition ? "" : inj.Tended ? $"  tended {inj.TendQuality:P0}" : "  <color=#ff7a70>untended</color>";
                GUI.Label(new Rect(x + 8, y, w, 18), $"{inj.Label} ({inj.Part}) {inj.Severity:P0}{state}", smallStyle);
                y += 17;
            }

            y += 4;
            GUI.Label(new Rect(x, y, w, 20), "<b>Thoughts</b>", textStyle);
            y += 20;
            foreach (var t in p.Mood.AllThoughts.OrderByDescending(t => Mathf.Abs(t.Value)).Take(4))
            {
                string color = t.Value >= 0 ? "#8fe07f" : "#ff7a70";
                GUI.Label(new Rect(x + 8, y, w, 18), $"<color={color}>{t.Value:+0;-0}</color>  {t.Label}", smallStyle);
                y += 17;
            }

            GUI.Label(new Rect(x, r.yMax - 92, w, 18),
                $"Loyalty {p.Loyalty:0}  ·  Wage €{p.Wage}/week", smallStyle);
            GUI.Label(new Rect(x, r.yMax - 74, w, 18),
                $"Build {p.ConstructionSkill} · Cook {p.CookingSkill} · Medic {p.MedicineSkill} · Intimidate {p.IntimidationSkill} · Aware {p.AwarenessSkill}", smallStyle);
            if (GUI.Button(new Rect(x, r.yMax - 54, 200, 20), $"Role: {RoleLabel(p.Role)}  (click to change)", smallButtonStyle))
                CycleRole(p);
            if (p.InMentalBreak(now))
                GUI.Label(new Rect(x + 170, r.yMax - 34, w - 170, 28), "<color=#ff7a70>Won't take orders</color>", textStyle);
            DrawDraftButton(r);
        }

        static string HealthSummary(Pawn p)
        {
            var h = p.Health;
            if (p.Dead) return $": dead ({p.DeathCause})";
            if (h.Healthy) return ": fine";
            var parts = new List<string>();
            if (h.Downed) parts.Add("<color=#ff7a70>downed</color>");
            if (h.BleedRatePerDay > 0.01f) parts.Add($"bleeding {h.BleedRatePerDay:P0}/day");
            if (h.BloodLoss > 0.01f) parts.Add($"blood loss {h.BloodLoss:P0}");
            if (h.Pain > 0.01f) parts.Add($"pain {h.Pain:P0}");
            return ": " + string.Join(", ", parts);
        }

        void DrawDraftButton(Rect r)
        {
            bool anyUndrafted = controller.Selected.Exists(p => !p.Drafted);
            if (GUI.Button(new Rect(r.x + 10, r.yMax - 36, 150, 28), anyUndrafted ? "Draft (R)" : "Release (R)"))
                controller.ToggleDraftSelected();
        }

        void Bar(float x, float y, float w, string label, float fill, Color color)
        {
            var back = new Rect(x, y + 2, w, 16);
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(back, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(back.x, back.y, back.width * Mathf.Clamp01(fill), back.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 6, y + 1, w, 18), label, smallStyle);
        }

        static Color MoodColor(float mood) =>
            mood < Mood.BreakBelow ? new Color(0.85f, 0.3f, 0.25f) :
            mood < 40f ? new Color(0.85f, 0.65f, 0.25f) :
            new Color(0.35f, 0.7f, 0.4f);

        // ---------- World labels ----------

        void DrawPawnLabels()
        {
            long now = game.Ticks.TicksGame;
            float unit = GuiUnitsPerWorldUnit;
            foreach (var p in game.Pawns.All)
            {
                if (p.InJail || !WorldToGui(p.Position, out var gui)) continue;

                string status = p.Dead ? " (dead)"
                    : p.Health.Downed ? " (downed)"
                    : p.InMentalBreak(now) ? " (sulking)"
                    : p.Sleep != SleepState.Awake ? " Zzz"
                    : p.CurrentJob is OSistema.AI.FleeJob ? " (running!)"
                    : "";
                nameStyle.normal.textColor = p.Dead ? Color.gray : p.Drafted ? BadColor : Color.white;
                ShadowLabel(new Rect(gui.x - 90, gui.y + unit * 0.45f, 180, 18), p.Nickname + status, nameStyle);

                if (p.HasBark && !p.Dead)
                {
                    ShadowLabel(new Rect(gui.x - 140, gui.y - unit * 0.5f - 40, 280, 20), p.Bark.Neapolitan, barkStyle);
                    ShadowLabel(new Rect(gui.x - 140, gui.y - unit * 0.5f - 22, 280, 18), p.Bark.English, subtitleStyle);
                }
            }
        }

        void DrawShopLabels()
        {
            if (cam.orthographicSize > 30f) return; // too zoomed out to read
            float unit = GuiUnitsPerWorldUnit;
            foreach (var b in game.Org.Businesses)
            {
                if (!WorldToGui(new Vector2(b.Cell.x + 0.5f, b.Cell.y + 0.5f), out var gui)) continue;
                shopStyle.normal.textColor = b.Status == BusinessStatus.Paying ? GoodColor : b.Status == BusinessStatus.Refusing ? BadColor : Gold;
                string tag = b.Status == BusinessStatus.Paying ? " (pizzo)" : b.Status == BusinessStatus.Refusing ? " (refused)" : "";
                ShadowLabel(new Rect(gui.x - 90, gui.y - unit * 0.5f - 18, 180, 18), b.Name + tag, shopStyle);
            }
            shopStyle.normal.textColor = Gold;
        }

        float GuiUnitsPerWorldUnit => Screen.height / (2f * cam.orthographicSize) / Scale;

        bool WorldToGui(Vector2 world, out Vector2 gui)
        {
            var screen = cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
            gui = new Vector2(screen.x / Scale, (Screen.height - screen.y) / Scale);
            return screen.z >= 0f && screen.x > -100 && screen.x < Screen.width + 100 && screen.y > -100 && screen.y < Screen.height + 100;
        }

        // ---------- Help ----------

        void DrawHelp()
        {
            var r = HelpPanel;
            GUI.Box(r, GUIContent.none);
            GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, r.height - 12),
                "<b>Controls</b>  (H to hide)\n" +
                "WASD / arrows · middle-drag   pan\n" +
                "Scroll · + / -   zoom\n" +
                "Left click / drag   select crew\n" +
                "Right click   move (drafts them)\n" +
                "R   draft / release\n" +
                "Space   pause  ·  1 2 3   speed\n" +
                "Bottom bar   build, demolish\n" +
                "Right click / Esc   cancel tool\n" +
                "B   business panel  ·  Piazza tool: open a square\n" +
                "F5 save  ·  F9 load\n" +
                "<color=#aaaaaa>Debug: F6 shoot selected · F7 +€200</color>\n\n" +
                "The crew eats, sleeps, cooks, shops and patches each other up on their own.",
                textStyle);
        }

        // ---------- Helpers ----------

        static void ShadowLabel(Rect r, string text, GUIStyle style)
        {
            var color = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        void EnsureStyles()
        {
            if (nameStyle != null) return;
            nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 12, fontStyle = FontStyle.Bold };
            barkStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 14, fontStyle = FontStyle.Bold };
            barkStyle.normal.textColor = new Color(1f, 0.92f, 0.7f);
            subtitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 11, fontStyle = FontStyle.Italic };
            subtitleStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            shopStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 11, fontStyle = FontStyle.Bold };
            shopStyle.normal.textColor = Gold;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, richText = true };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = true };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, richText = true };
            smallButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 11 };
            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, richText = true };
            headerStyle.normal.textColor = Gold;
        }
    }
}
