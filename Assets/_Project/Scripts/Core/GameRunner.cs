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
            hud.NewGameRequested += () => StartGame(Game.NewGame(UnityEngine.Random.Range(0, int.MaxValue)));

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
        }

        void StartGame(Game game)
        {
            StopGame();
            Game = game;

            worldRoot = new GameObject("World").transform;
            var mapGo = new GameObject("Map");
            mapGo.transform.SetParent(worldRoot, false);
            mapGo.AddComponent<MapRenderer>().Init(game);

            foreach (var pawn in game.Pawns.All) CreatePawnView(pawn);
            game.Pawns.Added += CreatePawnView;
            foreach (var npc in game.Npcs.All) CreateNpcView(npc);
            var combatGo = new GameObject("Combat");
            combatGo.transform.SetParent(worldRoot, false);
            combatGo.AddComponent<CombatView>().Init(game.Combat);
            game.Npcs.Added += CreateNpcView;

            controller.Bind(game);
            hud.Bind(game);

            var c = game.PlayerBase.center;
            cam.transform.position = new Vector3(c.x, c.y, -10f);
        }

        void StopGame()
        {
            if (Game != null)
            {
                Game.Pawns.Added -= CreatePawnView;
                Game.Npcs.Added -= CreateNpcView;
            }
            if (worldRoot != null) Destroy(worldRoot.gameObject);
            worldRoot = null;
            Game = null;
        }

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
            cam.orthographicSize = 12f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(14, 14, 18, 255);
            return cam;
        }
    }
}
