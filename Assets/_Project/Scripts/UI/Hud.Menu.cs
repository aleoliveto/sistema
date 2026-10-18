using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>Title screen, pause menu (Esc) and settings. Settings persist in PlayerPrefs.</summary>
    public sealed partial class Hud
    {
        enum Menu { None, Title, Pause, Settings, Creator }

        public event Action SaveRequested;
        public event Action LoadRequested;

        Menu openMenu;
        Menu settingsReturnTo;
        VisualElement menuDim;
        int speedBeforeMenu = 1;
        bool titleShown;
        float menuClosedAt = -10f;

        /// <summary>Shows player names over everyone, not just when zoomed in.</summary>
        public static bool AlwaysShowNames { get; private set; }

        void BuildMenus()
        {
            menuDim = Ui.Box("dim").Into(modalLayer);
            menuDim.Show(false);
            ApplySettings();
        }

        public bool MenuOpen => openMenu != Menu.None;

        void OpenMenu(Menu menu)
        {
            if (menu != Menu.Creator) HookTyping(false);
            if (openMenu == Menu.None && game != null)
            {
                speedBeforeMenu = game.Ticks.Speed == 0 ? 1 : game.Ticks.Speed;
                game.Ticks.SetSpeed(0);
            }
            openMenu = menu;
            menuDim.Clear();
            menuDim.Show(true);
            switch (menu)
            {
                case Menu.Title: BuildTitle(); break;
                case Menu.Pause: BuildPause(); break;
                case Menu.Settings: BuildSettings(); break;
                case Menu.Creator: BuildCreator(); break;
            }
        }

        void CloseMenu()
        {
            HookTyping(false);
            openMenu = Menu.None;
            menuClosedAt = Time.unscaledTime;
            menuDim.Clear();
            menuDim.Show(false);
            if (game != null && game.Events.Current == null && !game.Org.GameOver) game.Ticks.SetSpeed(speedBeforeMenu);
        }

        VisualElement MenuCard(string subtitle)
        {
            var card = Ui.Box("panel", "card", "menu").Into(menuDim);
            var band = Ui.Box("card__band").Into(card);
            band.style.backgroundColor = new Color(0.16f, 0.09f, 0.06f);
            band.style.height = 110;
            Ui.Text("'O SISTEMA", band, "menu__title");
            Ui.Text(subtitle, band, "menu__sub");
            return Ui.Box("menu__body").Into(card);
        }

        void BuildTitle()
        {
            var body = MenuCard("A life in the System. Every choice has a price.");
            Ui.Btn("New family", () => OpenMenu(Menu.Creator), body, "menu-btn");
            var load = Ui.Btn("Load game", () => { CloseMenu(); LoadRequested?.Invoke(); }, body, "menu-btn");
            load.SetEnabled(Core.SaveSystem.HasSave(Core.SaveSystem.DefaultPath));
            Ui.Btn("Settings", () => { settingsReturnTo = Menu.Title; OpenMenu(Menu.Settings); }, body, "menu-btn");
            Ui.Btn("Quit", Application.Quit, body, "menu-btn");
            Ui.Text("A work of fiction. Every clan, person and business in this game is invented.", body, "small", "muted", "wrap").style.marginTop = 10;
        }

        void BuildPause()
        {
            var body = MenuCard("Paused");
            Ui.Btn("Resume", CloseMenu, body, "menu-btn");
            Ui.Btn("Save (F5)", () => { SaveRequested?.Invoke(); CloseMenu(); }, body, "menu-btn");
            var load = Ui.Btn("Load (F9)", () => { CloseMenu(); LoadRequested?.Invoke(); }, body, "menu-btn");
            load.SetEnabled(Core.SaveSystem.HasSave(Core.SaveSystem.DefaultPath));
            Ui.Btn("Settings", () => { settingsReturnTo = Menu.Pause; OpenMenu(Menu.Settings); }, body, "menu-btn");
            Ui.Btn("Help (H)", () => { CloseMenu(); ToggleWindow(Window.Help); }, body, "menu-btn");
            Ui.Btn("New family", () => OpenMenu(Menu.Creator), body, "menu-btn", "btn--danger");
            Ui.Btn("Quit", Application.Quit, body, "menu-btn");
        }

        void BuildSettings()
        {
            var body = MenuCard("Settings");

            var vol = new Slider(0f, 1f) { value = PlayerPrefs.GetFloat("volume", 0.8f) };
            vol.style.flexGrow = 1;
            vol.RegisterValueChangedCallback(e => { PlayerPrefs.SetFloat("volume", e.newValue); ApplySettings(); });
            Setting(body, "Volume", vol);

            var scale = new Slider(0.8f, 1.4f) { value = PlayerPrefs.GetFloat("ui_scale", 1f) };
            scale.style.flexGrow = 1;
            scale.RegisterValueChangedCallback(e => { PlayerPrefs.SetFloat("ui_scale", e.newValue); ApplySettings(); });
            Setting(body, "Interface size", scale);

            var auto = new Toggle { value = PlayerPrefs.GetInt("auto_move", 0) == 1 };
            auto.RegisterValueChangedCallback(e => { PlayerPrefs.SetInt("auto_move", e.newValue ? 1 : 0); ApplySettings(); });
            Setting(body, "Automatic movement (he eats, sleeps and works on his own when you leave him)", auto);

            var names = new Toggle { value = PlayerPrefs.GetInt("names_always", 0) == 1 };
            names.RegisterValueChangedCallback(e => { PlayerPrefs.SetInt("names_always", e.newValue ? 1 : 0); ApplySettings(); });
            Setting(body, "Always show names", names);

            Ui.Btn("Back", () => OpenMenu(settingsReturnTo), body, "menu-btn").style.marginTop = 10;
        }

        static void Setting(VisualElement parent, string label, VisualElement control)
        {
            var row = Ui.Box("setting").Into(parent);
            Ui.Text(label, row, "setting__label");
            row.Add(control);
        }

        void ApplySettings()
        {
            AudioListener.volume = PlayerPrefs.GetFloat("volume", 0.8f);
            if (document != null && document.panelSettings != null) document.panelSettings.scale = PlayerPrefs.GetFloat("ui_scale", 1f);
            AlwaysShowNames = PlayerPrefs.GetInt("names_always", 0) == 1;
            ApplyAutoMovement();
        }

        /// <summary>Esc closes the topmost thing; with nothing open it brings up the pause menu.</summary>
        void HandleEscape()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (controller.EscapeConsumedFrame == Time.frameCount) return;
            if (game != null && game.Events.Current != null) return;
            if (MinigameOpen)
            {
                CloseMinigame();
                return;
            }
            if (ShopOpen)
            {
                CloseShop();
                return;
            }
            if (mapOpen)
            {
                ToggleMap();
                return;
            }
            if (PaperOpen)
            {
                ClosePaper();
                return;
            }

            if (openMenu == Menu.Settings) OpenMenu(settingsReturnTo);
            else if (openMenu == Menu.Pause) CloseMenu();
            else if (openMenu == Menu.Title) { }
            else if (openMenu == Menu.Creator) OpenMenu(Menu.Title);
            else if (openWindow != Window.None) ToggleWindow(openWindow);
            else OpenMenu(Menu.Pause);
        }

        /// <summary>Automatic movement for the player's character: off unless ticked in Settings (or Z).</summary>
        void ApplyAutoMovement()
        {
            if (game == null) return;
            bool on = PlayerPrefs.GetInt("auto_move", 0) == 1;
            game.PlayerSelfCare = on;
            var p = game.Player;
            if (p != null) p.Autopilot = on;
        }

        public void ToggleAutoMovement()
        {
            bool on = PlayerPrefs.GetInt("auto_move", 0) != 1;
            PlayerPrefs.SetInt("auto_move", on ? 1 : 0);
            ApplyAutoMovement();
            game?.Log.Add(on ? "Automatic movement on: left alone, he eats, sleeps and works by himself (Z or Settings to turn off)." : "Automatic movement off: he only moves when you move him.", Core.MessageKind.Info);
        }

        bool devPiazza;

        void DevOpenPiazza()
        {
            // Right next to a clan's corner, the worst place to be: their hostility climbs every day.
            foreach (var rival in game.RivalSquares)
            foreach (var c in OSistema.World.GridUtil.CellsAround(rival.Cell, 14))
            {
                var zone = game.Territory.ZoneAt(c);
                if (zone == null || !game.Map.InBounds(c) || game.PlayerBase.Contains(c)) continue;
                if (game.Map.GetBuilding(c) != null || game.Map.GetTerrain(c.x, c.y) == OSistema.World.TerrainType.Floor) continue;
                if (System.Linq.Enumerable.Any(game.RivalSquares, s => (s.Cell - c).sqrMagnitude < 100)) continue;
                game.Org.RestoreSquare(new Crime.DrugSquare { Cell = c, ZoneId = zone.Id }, game.Map);
                Debug.Log($"[soak] dev piazza opened in {zone.Name}, next to the {game.Clans[rival.Clan].Name}");
                return;
            }
        }

        void ShowTitleOnce()
        {
            if (titleShown) return;
            titleShown = true;
            // Developer switch: a file named dev_autoplay in the save folder skips the title (for testing).
            var dev = System.IO.Path.Combine(Application.persistentDataPath, "dev_autoplay");
            Core.MessageLog.Trace = null;   // statics survive Play with domain reload off
            if (System.IO.File.Exists(dev))
            {
                // "soak" in the file: run at top speed and answer every scene, to shake out errors.
                string text = System.IO.File.ReadAllText(dev);
                devSoak = text.Contains("soak");
                if (devSoak && game != null)
                {
                    game.Ticks.SetSpeed(5);
                    Application.runInBackground = true;   // keep going while the editor isn't in front
                    Core.MessageLog.Trace = m => Debug.Log($"[soak] d{this.game?.Clock.Day} {this.game?.Clock.Hour:00}h | {m}");
                }
                // "piazza": the family starts with a piazza by the basso, so the clans have something to come for.
                devPiazza = text.Contains("piazza");
                if (devPiazza && game != null) DevOpenPiazza();
                // "night" jumps to the evening, "rain" brings the rain: to look at the lighting.
                if (text.Contains("night") && game != null) game.Ticks.Advance((int)(((21.5f - game.Clock.HourF + 24f) % 24f) * Core.GameClock.TicksPerHour));
                if (text.Contains("rain") && game != null) game.Weather.ForceRain();
                if (text.Contains("phone") && game != null) ToggleWindow(Window.Phone);
                // "travel": ride to the port straight away (and test saving two districts, with "roundtrip").
                if (text.Contains("travel") && game != null)
                {
                    try
                    {
                        game.TravelTo(Core.District.PortId, new System.Collections.Generic.List<OSistema.Pawns.Pawn>());
                        Debug.Log($"[O Sistema] TRAVEL now in {game.District.Name}: {game.Map.Width}x{game.Map.Height}, rioni {game.Territory.Zones.Count}, shops {game.Org.Businesses.Count}, clan piazze {System.Linq.Enumerable.Count(game.RivalSquares)}, contacts {game.Contacts.All.Count}, gates {game.District.Gates.Count}, pawns here {game.Pawns.All.Count}, parked at home {game.Districts[0].Dormant.Count}");
                    }
                    catch (Exception e) { Debug.LogError("[O Sistema] TRAVEL FAILED " + e); }
                }
                if (text.Contains("roundtrip") && game != null)
                {
                    try { Debug.Log("[O Sistema] " + Core.SaveSystem.DevRoundTrip(game)); }
                    catch (Exception e) { Debug.LogError("[O Sistema] ROUNDTRIP FAILED " + e); }
                }
                if (text.Contains("map") && game != null) { OSistema.Views.FogOfWar.RevealAllForTesting(); ToggleMap(); }
                return;
            }
            OpenMenu(Menu.Title);
        }
    }
}
