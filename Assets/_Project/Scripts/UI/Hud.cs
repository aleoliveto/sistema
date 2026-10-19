using System;
using OSistema.Core;
using OSistema.Input;
using OSistema.Pawns;
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
        public event Action<CharacterSetup> NewGameRequested;

        public void Init(PlayerController pc, Camera c)
        {
            controller = pc;
            cam = c;
            BuildDocument();
            BuildTop();
            BuildMap();
            BuildVitals();
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
            ApplyAutoMovement();
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
            // Red at the edges of the screen when you're hit, deeper as the guard goes.
            damageOverlay = Ui.Box("layer").Into(root);
            damageOverlay.pickingMode = PickingMode.Ignore;
            damageOverlay.style.backgroundImage = new StyleBackground(DamageVignette());
            damageOverlay.style.opacity = 0f;
            hudLayer = Layer();
            modalLayer = Layer();
        }

        VisualElement damageOverlay;
        float damageFlash;

        static Texture2D vignette;

        static Texture2D DamageVignette()
        {
            if (vignette != null) return vignette;
            const int n = 128;
            vignette = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f);
                float a = Mathf.Clamp01((r - 0.55f) / 0.6f);
                px[y * n + x] = new Color32(150, 8, 8, (byte)(a * a * 230));
            }
            vignette.SetPixels32(px);
            vignette.Apply();
            return vignette;
        }

        /// <summary>A hit on the player: a flash of red, and an arrow towards whoever fired.</summary>
        public void OnPlayerHurt(Vector2 dir)
        {
            damageFlash = 1f;
            hurtFrom = -dir;
            hurtArrowTime = 1f;
        }

        Vector2 hurtFrom;
        float hurtArrowTime;
        VisualElement combatBar, armorFill, healthFill, hurtArrow;

        void BuildCombatBar()
        {
            combatBar = Ui.Box("combat-bar").Into(hudLayer);
            combatBar.pickingMode = PickingMode.Ignore;
            var vest = Ui.Box("combat-bar__seg").Into(combatBar);
            armorFill = Ui.Box("combat-bar__segfill").Into(vest);
            var hp = Ui.Box("combat-bar__hp").Into(combatBar);
            healthFill = Ui.Box("combat-bar__hpfill").Into(hp);
            hurtArrow = Ui.Text("▲", hudLayer, "hurt-arrow");
            hurtArrow.pickingMode = PickingMode.Ignore;
        }

        void UpdateDamageOverlay()
        {
            var p = game?.Player;
            float hp = p != null ? p.Hp / Pawn.MaxHp : 1f;
            damageFlash = Mathf.MoveTowards(damageFlash, 0f, Time.unscaledDeltaTime * 3f);
            float a = (1f - hp) * 0.75f + damageFlash * 0.4f;
            damageOverlay.style.opacity = Mathf.Clamp01(a);

            // Health and the vest live in the top bar now (Hud.Vitals); only the hurt arrow is built here.
            if (combatBar == null) BuildCombatBar();
            combatBar.Show(false);
            if (p == null) return;

            // Where the shot came from: an arrow round the middle of the screen, fading.
            hurtArrowTime = Mathf.MoveTowards(hurtArrowTime, 0f, Time.unscaledDeltaTime * 1.2f);
            hurtArrow.Show(hurtArrowTime > 0f && hurtFrom.sqrMagnitude > 0.001f);
            if (hurtArrowTime > 0f)
            {
                float ang = Mathf.Atan2(hurtFrom.x, hurtFrom.y) * Mathf.Rad2Deg;
                var size = root.layout.size;
                var c = size / 2f + new Vector2(hurtFrom.x, -hurtFrom.y).normalized * Mathf.Min(size.x, size.y) * 0.22f;
                hurtArrow.style.left = c.x - 16;
                hurtArrow.style.top = c.y - 16;
                hurtArrow.style.rotate = new Rotate(Angle.Degrees(ang));
                hurtArrow.style.opacity = hurtArrowTime;
            }
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
            if (damageOverlay != null) UpdateDamageOverlay();
            var kb = Keyboard.current;
            if (kb != null)
            {
                bool typing = MinigameOpen || openMenu != Menu.None;
                if (!typing)
                {
                    if (kb.hKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame) ToggleWindow(Window.Help);
                    if (kb.tabKey.wasPressedThisFrame) ToggleWindow(Window.Business);
                    if (kb.bKey.wasPressedThisFrame) ToggleBuildMode();
                    if (kb.jKey.wasPressedThisFrame) ToggleWindow(Window.Jobs);
                    if (kb.pKey.wasPressedThisFrame) ToggleWindow(Window.Phone);
                    if (kb.mKey.wasPressedThisFrame) ToggleMap();
                    PhoneKeys(kb);
                }
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
            RefreshVitals();
            UpdatePoliceAlerts();
            RefreshWindows();
            RefreshModals();
        }

        void LateUpdate()
        {
            UpdateMinigame();
            HandleEscape();
            if (game == null || root?.panel == null) return;
            UpdateWorldLabels();
            UpdateMap();
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

        /// <summary>"Show": drop a pin on the map there, and look at it until you move.</summary>
        void FocusCamera(Vector2 at)
        {
            var p = cam.transform.position;
            cam.transform.position = new Vector3(at.x, at.y, p.z);
            controller.StopFollowing();
            waypoint = at;
        }
    }
}
