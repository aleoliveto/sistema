using System;
using OSistema.Core;
using OSistema.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>
    /// The game's UI, built with UI Toolkit from code (styles in Resources/UI/OSistema.uss).
    /// Three layers: world labels (names, speech bubbles), the HUD, and modals (event cards).
    /// Split across partial files by area.
    /// </summary>
    public sealed partial class Hud : MonoBehaviour
    {
        const float RefreshSeconds = 0.2f;

        Game game;
        PlayerController controller;
        Camera cam;

        UIDocument document;
        VisualElement root, worldLayer, hudLayer, modalLayer;
        Label tooltip, alert;
        Func<string> tooltipText;
        VisualElement tooltipOwner;
        float refreshTimer, alertUntil;

        /// <summary>Raised when the player starts over from the game-over screen.</summary>
        public event Action NewGameRequested;

        public void Init(PlayerController pc, Camera c)
        {
            controller = pc;
            cam = c;
            BuildDocument();
            BuildTop();
            BuildCrewBar();
            BuildInspector();
            BuildFeed();
            BuildDock();
            BuildWindows();
            BuildJobsWindow();
            BuildPhone();
            BuildModals();
            BuildMenus();

            alert = Ui.Text("", hudLayer, "alert");
            alert.style.opacity = 0f;
            tooltip = Ui.Text("", root, "tooltip");
            tooltip.Show(false);
        }

        public void Bind(Game g)
        {
            game = g;
            ResetWorldLabels();
            popups.Clear();
            HookPopups(g);
            HookPhone(g);
            RefreshAll();
            ShowTitleOnce();
        }

        void BuildDocument()
        {
            // UI Toolkit routes pointer input through an EventSystem when one exists; with the
            // Input System as the only input backend this is the reliable path.
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(transform, false);
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/OSistemaTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1600, 900);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;

            var go = new GameObject("UI Document");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            document = go.AddComponent<UIDocument>();
            document.panelSettings = settings;
            go.SetActive(true);

            root = document.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/OSistema"));
            root.AddToClassList("root");
            root.pickingMode = PickingMode.Ignore;

            worldLayer = Layer();
            hudLayer = Layer();
            modalLayer = Layer();
        }

        VisualElement Layer()
        {
            var layer = Ui.Box("layer").Into(root);
            layer.pickingMode = PickingMode.Ignore;
            return layer;
        }

        /// <summary>Is the mouse over an interactive piece of UI (so clicks shouldn't reach the world)?</summary>
        public bool IsPointerOver(Vector2 screenPos)
        {
            if (game != null && (game.Events.Current != null || game.Org.GameOver)) return true;
            if (openMenu != Menu.None) return true;
            if (Time.unscaledTime - menuClosedAt < 0.3f) return true; // the click that closed a menu stays in the menu
            if (root?.panel == null) return false;
            var panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            return root.panel.Pick(panelPos) != null;
        }

        /// <summary>Big banner across the top, e.g. when a firefight starts.</summary>
        public void FlashAlert(string text)
        {
            alert.text = text;
            alert.style.opacity = 1f;
            alertUntil = Time.unscaledTime + 2.5f;
        }

        /// <summary>Hover help for any element.</summary>
        void Tip(VisualElement e, Func<string> text)
        {
            e.RegisterCallback<PointerEnterEvent>(_ =>
            {
                tooltipText = text;
                tooltipOwner = e;
                tooltip.text = text();
                tooltip.Show(!string.IsNullOrEmpty(tooltip.text));
            });
            e.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                tooltipText = null;
                tooltipOwner = null;
                tooltip.Show(false);
            });
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.hKey.wasPressedThisFrame) ToggleWindow(Window.Help);
                if (kb.bKey.wasPressedThisFrame) ToggleWindow(Window.Business);
                if (kb.jKey.wasPressedThisFrame) ToggleWindow(Window.Jobs);
                if (kb.pKey.wasPressedThisFrame) ToggleWindow(Window.Phone);
            }
            if (game == null) return;

            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer >= RefreshSeconds)
            {
                refreshTimer = 0f;
                RefreshAll();
            }

            if (alertUntil > 0f && Time.unscaledTime > alertUntil)
            {
                alert.style.opacity = 0f;
                alertUntil = 0f;
            }
        }

        void RefreshAll()
        {
            if (game == null) return;
            RefreshTop();
            RefreshCrewBar();
            RefreshInspector();
            RefreshFeed();
            RefreshDock();
            RefreshPhone();
            RefreshWindows();
            RefreshModals();
        }

        void LateUpdate()
        {
            HandleEscape();
            if (game == null || root?.panel == null) return;
            UpdateWorldLabels();
            UpdatePopups();

            if (tooltipText != null && Mouse.current != null)
            {
                var m = Mouse.current.position.ReadValue();
                var p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(m.x, Screen.height - m.y));
                // Windows rebuild their rows; a row removed under the mouse never gets a "leave".
                if (tooltipOwner == null || tooltipOwner.panel == null || !tooltipOwner.worldBound.Contains(p))
                {
                    tooltipText = null;
                    tooltipOwner = null;
                    tooltip.Show(false);
                    return;
                }
                tooltip.text = tooltipText();
                tooltip.style.left = p.x + 16f;
                tooltip.style.top = p.y + 16f;
            }
        }

        Vector2 WorldToPanel(Vector2 world) =>
            RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, new Vector3(world.x, world.y, 0f), cam);

        void FocusCamera(Vector2 at)
        {
            var p = cam.transform.position;
            cam.transform.position = new Vector3(at.x, at.y, p.z);
        }
    }
}
