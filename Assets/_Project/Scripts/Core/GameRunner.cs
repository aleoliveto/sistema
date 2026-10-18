using System;
using OSistema.Input;
using OSistema.Npcs;
using OSistema.Pawns;
using OSistema.UI;
using OSistema.Views;
using OSistema.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OSistema.Core
{
    /// <summary>
    /// The only scene object the game needs. It boots automatically when you press Play,
    /// builds the simulation and creates every view from code. F5 saves, F9 loads.
    /// </summary>
    public sealed class GameRunner : MonoBehaviour
    {
        public Game Game { get; private set; }

        /// <summary>The map in play, for views that need a look at their surroundings. Reset with each game.</summary>
        public static OSistema.World.Map CurrentMap { get; private set; }

        Camera cam;
        Hud hud;
        PlayerController controller;
        Transform worldRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (FindAnyObjectByType<GameRunner>() != null) return;
            new GameObject("O Sistema").AddComponent<GameRunner>();
        }

        void Awake()
        {
            cam = SetupCamera();
            hud = gameObject.AddComponent<Hud>();
            controller = gameObject.AddComponent<PlayerController>();
            controller.Init(cam, hud);
            hud.Init(controller, cam);
            hud.NewGameRequested += setup => StartGame(Game.NewGame(UnityEngine.Random.Range(0, int.MaxValue), setup));
            hud.SaveRequested += SaveGame;
            hud.LoadRequested += LoadGame;

            int seed = UnityEngine.Random.Range(0, int.MaxValue);
            Debug.Log($"[O Sistema] New game, seed {seed}");
            StartGame(Game.NewGame(seed));
        }

        void OnDestroy() => StopGame();

        void Update()
        {
            // Unity recompiled scripts during Play and wiped the game state: stop Play and press it again.
            if (Game == null) return;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f5Key.wasPressedThisFrame) SaveGame();
                if (kb.f9Key.wasPressedThisFrame) LoadGame();
            }
            Game.Ticks.Update(Time.unscaledDeltaTime);
            if (districtChanged)
            {
                districtChanged = false;
                var g = Game;
                StartGame(g);
            }
        }

        void StartGame(Game game)
        {
            StopGame();
            Game = game;
            CurrentMap = game.Map;

            worldRoot = new GameObject("World").transform;
            Overhead.Init(game, worldRoot);
            var mapGo = new GameObject("Map");
            mapGo.transform.SetParent(worldRoot, false);
            mapGo.AddComponent<MapRenderer>().Init(game);
            var roofsGo = new GameObject("Roofs");
            roofsGo.transform.SetParent(worldRoot, false);
            roofsGo.AddComponent<RoofView>().Init(game);

            var laundryGo = new GameObject("Laundry");
            laundryGo.transform.SetParent(worldRoot, false);
            laundryGo.AddComponent<LaundryView>().Init(game.Map);

            var fogGo = new GameObject("Fog of war");
            fogGo.transform.SetParent(worldRoot, false);
            fogGo.AddComponent<FogOfWar>().Init(game);

            foreach (var pawn in game.Pawns.All) CreatePawnView(pawn);
            game.Pawns.Added += CreatePawnView;
            foreach (var npc in game.Npcs.All) CreateNpcView(npc);
            var combatGo = new GameObject("Combat");
            combatGo.transform.SetParent(worldRoot, false);
            combatGo.AddComponent<CombatView>().Init(game.Combat, game);
            game.Combat.PlayerHurt += hud.OnPlayerHurt;
            combatGo.AddComponent<SoundManager>().Init(game);
            game.Combat.EngagementStarted += OnEngagement;
            game.Response.Sirens += OnSirens;
            game.Npcs.Added += CreateNpcView;
            game.DistrictChanged += OnDistrictChanged;

            controller.Bind(game);
            hud.Bind(game);

            var c = game.Player != null && game.PlayerBase.width == 0 ? game.Player.Position : game.PlayerBase.center;
            if (game.Player != null && !game.PlayerBase.Contains(game.Player.Cell)) c = game.Player.Position;
            cam.transform.position = new Vector3(c.x, c.y, -10f);
        }

        void StopGame()
        {
            if (Game != null)
            {
                Game.Combat.EngagementStarted -= OnEngagement;
                Game.Combat.PlayerHurt -= hud.OnPlayerHurt;
                Game.Response.Sirens -= OnSirens;
                Game.Pawns.Added -= CreatePawnView;
                Game.Npcs.Added -= CreateNpcView;
                Game.DistrictChanged -= OnDistrictChanged;
            }
            if (worldRoot != null) Destroy(worldRoot.gameObject);
            worldRoot = null;
            Game = null;
        }

        bool districtChanged;

        // The family rode into another district: rebuild every view for its map (next frame, not mid-click).
        void OnDistrictChanged() => districtChanged = true;

        void SaveGame()
        {
            try
            {
                SaveSystem.Save(Game, SaveSystem.DefaultPath);
                Game.Log.Add("Game saved.", MessageKind.Good);
                Debug.Log($"[O Sistema] Saved to {SaveSystem.DefaultPath}");
            }
            catch (Exception e)
            {
                Game.Log.Add("Saving failed. See the console.", MessageKind.Bad);
                Debug.LogException(e);
            }
        }

        void LoadGame()
        {
            if (!SaveSystem.HasSave(SaveSystem.DefaultPath))
            {
                Game.Log.Add("No save found. Press F5 to save first.", MessageKind.Bad);
                return;
            }
            try
            {
                StartGame(SaveSystem.Load(SaveSystem.DefaultPath));
            }
            catch (Exception e)
            {
                Game?.Log.Add("Loading failed. See the console.", MessageKind.Bad);
                Debug.LogException(e);
            }
        }

        void CreatePawnView(Pawn pawn)
        {
            var go = new GameObject($"Pawn {pawn.Nickname}");
            go.transform.SetParent(worldRoot, false);
            go.AddComponent<PawnView>().Init(pawn);
        }

        // Shots fired: drop out of fast-forward so the player can react, and say where.
        void OnSirens(string message) => hud.FlashAlert("SIRENE! POLICE ON THE WAY");

        void OnEngagement(Vector2 at)
        {
            if (Game.Ticks.Speed > 1) Game.Ticks.SetSpeed(1);
            var zone = Game.Territory.ZoneAt(Vector2Int.FloorToInt(at));
            Game.Log.Add($"Sparatoria! Shots fired{(zone != null ? " in " + zone.Name : "")}.", MessageKind.Bad, Vector2Int.FloorToInt(at));
            hud.FlashAlert("SPARATORIA!");
        }

        void CreateNpcView(Npc npc)
        {
            var go = new GameObject($"{npc.Kind} {npc.Id}");
            go.transform.SetParent(worldRoot, false);
            go.AddComponent<NpcView>().Init(npc);
        }

        static Camera SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.orthographic = true;
            cam.orthographicSize = 6.5f;   // close enough to see faces and hands; scroll out for the map
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(14, 14, 18, 255);
            PostFx.Setup(cam);
            return cam;
        }
    }
}
