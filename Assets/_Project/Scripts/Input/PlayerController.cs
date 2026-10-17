using System.Collections.Generic;
using OSistema.AI;
using OSistema.Core;
using OSistema.Pawns;
using OSistema.UI;
using OSistema.Views;
using OSistema.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OSistema.Input
{
    public enum Tool { Select, Build, Deconstruct, Cancel }

    /// <summary>Camera, selection, orders and build tools. Turns player input into simulation commands.</summary>
    public sealed class PlayerController : MonoBehaviour
    {
        const float MinZoom = 4f;
        const float MaxZoom = 45f;
        const float ClickThresholdPixels = 6f;

        static readonly Color SelectPreview = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color BuildPreview = new Color(0.35f, 0.75f, 1f, 0.35f);
        static readonly Color DemolishPreview = new Color(1f, 0.3f, 0.25f, 0.35f);
        static readonly Color CancelPreview = new Color(1f, 0.65f, 0.2f, 0.35f);

        public static PlayerController Instance { get; private set; }

        public Tool CurrentTool { get; private set; } = Tool.Select;
        public BuildingDef CurrentDef { get; private set; }
        public readonly List<Pawn> Selected = new List<Pawn>();

        Game game;
        Camera cam;
        Hud hud;
        SpriteRenderer preview;

        bool dragging;
        Vector2Int dragStartCell;
        Vector2 dragStartScreen;

        public void Init(Camera c, Hud h)
        {
            Instance = this;
            cam = c;
            hud = h;

            preview = new GameObject("Cursor Preview").AddComponent<SpriteRenderer>();
            preview.sprite = SpriteFactory.Square;
            preview.sharedMaterial = SpriteFactory.UnlitMaterial;
            preview.sortingOrder = 20;
            preview.enabled = false;
        }

        /// <summary>Points the controller at a new or loaded game.</summary>
        public void Bind(Game g)
        {
            game = g;
            Selected.Clear();
            SetTool(Tool.Select);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetTool(Tool tool, BuildingDef def = null)
        {
            CurrentTool = tool;
            CurrentDef = def;
            dragging = false;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null || game == null) return;

            Selected.RemoveAll(p => p.Dead);

            Vector2 mousePos = mouse.position.ReadValue();
            bool overUi = hud.IsPointerOver(mousePos);

            HandleKeys(kb);
            HandleDebugKeys(kb);
            HandleCamera(kb, mouse, mousePos, overUi);
            HandleMouse(kb, mouse, mousePos, overUi);
            UpdatePreview(mousePos, overUi);
        }

        void HandleKeys(Keyboard kb)
        {
            if (kb.spaceKey.wasPressedThisFrame) game.Ticks.TogglePause();
            if (kb.digit1Key.wasPressedThisFrame) game.Ticks.SetSpeed(1);
            if (kb.digit2Key.wasPressedThisFrame) game.Ticks.SetSpeed(2);
            if (kb.digit3Key.wasPressedThisFrame) game.Ticks.SetSpeed(3);
            if (kb.rKey.wasPressedThisFrame) ToggleDraftSelected();

            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (CurrentTool != Tool.Select) SetTool(Tool.Select);
                else Selected.Clear();
            }
        }

        // Testing shortcuts until combat and the economy exist. Remove before release.
        void HandleDebugKeys(Keyboard kb)
        {
            if (kb.f6Key.wasPressedThisFrame)
            {
                foreach (var p in Selected)
                {
                    p.Health.AddInjury("Gunshot wound", "Torso", 0.35f, 0.9f);
                    p.Mood.AddMemory("shot", "Got shot", -10f, game.Ticks.TicksGame, GameClock.TicksPerDay * 2);
                    p.Say("hurt");
                    game.Log.Add($"[Debug] {p.Nickname} was shot.", MessageKind.Bad);
                }
            }
            if (kb.f7Key.wasPressedThisFrame)
            {
                game.Stock.Cash += 200;
                game.Log.Add("[Debug] +€200", MessageKind.Good);
            }
        }

        public void ToggleDraftSelected()
        {
            if (Selected.Count == 0) return;
            bool draft = Selected.Exists(p => !p.Drafted);
            long now = game.Ticks.TicksGame;
            foreach (var p in Selected)
                if (!p.InMentalBreak(now)) p.SetDrafted(draft, game);
        }

        void HandleCamera(Keyboard kb, Mouse mouse, Vector2 mousePos, bool overUi)
        {
            float size = cam.orthographicSize;
            var move = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
            cam.transform.position += (Vector3)(move * (size * 1.6f * Time.unscaledDeltaTime));

            if (mouse.middleButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                float unitsPerPixel = 2f * size / Screen.height;
                cam.transform.position -= new Vector3(delta.x, delta.y, 0f) * unitsPerPixel;
            }

            float zoom = 0f;
            if (!overUi) zoom = Mathf.Clamp(mouse.scroll.ReadValue().y * 0.12f, -0.4f, 0.4f);
            if (kb.equalsKey.isPressed) zoom += 1.5f * Time.unscaledDeltaTime;
            if (kb.minusKey.isPressed) zoom -= 1.5f * Time.unscaledDeltaTime;
            if (Mathf.Abs(zoom) > 0.0001f)
            {
                // Zoom towards the cursor.
                Vector3 before = ScreenToWorld(mousePos);
                cam.orthographicSize = Mathf.Clamp(size * (1f - zoom), MinZoom, MaxZoom);
                cam.transform.position += before - ScreenToWorld(mousePos);
            }

            var p = cam.transform.position;
            p.x = Mathf.Clamp(p.x, 0f, game.Map.Width);
            p.y = Mathf.Clamp(p.y, 0f, game.Map.Height);
            cam.transform.position = p;
        }

        void HandleMouse(Keyboard kb, Mouse mouse, Vector2 mousePos, bool overUi)
        {
            if (mouse.leftButton.wasPressedThisFrame && !overUi)
            {
                dragging = true;
                dragStartCell = ScreenToCell(mousePos);
                dragStartScreen = mousePos;
            }

            if (dragging && mouse.leftButton.wasReleasedThisFrame)
            {
                dragging = false;
                bool isClick = (mousePos - dragStartScreen).magnitude < ClickThresholdPixels;
                OnLeftRelease(dragStartCell, ScreenToCell(mousePos), isClick, mousePos, kb.leftShiftKey.isPressed);
            }

            if (mouse.rightButton.wasPressedThisFrame && !overUi)
            {
                if (CurrentTool != Tool.Select) SetTool(Tool.Select);
                else OrderMove(ScreenToCell(mousePos));
            }
        }

        void OnLeftRelease(Vector2Int start, Vector2Int end, bool isClick, Vector2 mousePos, bool additive)
        {
            var rect = GridUtil.RectFromCorners(start, end);
            switch (CurrentTool)
            {
                case Tool.Select:
                    if (!additive) Selected.Clear();
                    if (isClick)
                    {
                        var pawn = PawnAt(ScreenToWorld(mousePos));
                        if (pawn != null && !Selected.Contains(pawn)) Selected.Add(pawn);
                    }
                    else
                    {
                        foreach (var pawn in game.Pawns.All)
                            if (!pawn.Dead && rect.Contains(pawn.Cell) && !Selected.Contains(pawn)) Selected.Add(pawn);
                    }
                    break;

                case Tool.Build:
                    if (CurrentDef.PlaceMode == PlaceMode.Single)
                        game.Construction.PlaceBuild(start, CurrentDef);
                    else
                        foreach (var c in GridUtil.Cells(rect, CurrentDef.PlaceMode == PlaceMode.Outline))
                            game.Construction.PlaceBuild(c, CurrentDef);
                    break;

                case Tool.Deconstruct:
                    foreach (var c in GridUtil.Cells(rect, false)) game.Construction.PlaceDeconstruct(c);
                    break;

                case Tool.Cancel:
                    foreach (var c in GridUtil.Cells(rect, false)) game.Construction.Cancel(c);
                    break;
            }
        }

        void OrderMove(Vector2Int target)
        {
            if (Selected.Count == 0 || !game.Map.InBounds(target)) return;

            // Spread the group over free cells around the click.
            var destinations = new List<Vector2Int>();
            foreach (var c in GridUtil.CellsAround(target, 4))
            {
                if (destinations.Count >= Selected.Count) break;
                if (game.Map.IsPassable(c)) destinations.Add(c);
            }

            for (int i = 0; i < Selected.Count && i < destinations.Count; i++)
            {
                var pawn = Selected[i];
                if (!pawn.CanAct || pawn.InMentalBreak(game.Ticks.TicksGame)) continue; // sulking pawns ignore orders
                pawn.SetDrafted(true, game);
                pawn.ForceJob(new GotoJob(pawn, destinations[i]), game);
            }
            Selected[0].Say("order_move");
        }

        void UpdatePreview(Vector2 mousePos, bool overUi)
        {
            var cell = ScreenToCell(mousePos);
            RectInt rect;
            if (dragging)
            {
                bool single = CurrentTool == Tool.Build && CurrentDef.PlaceMode == PlaceMode.Single;
                rect = single ? new RectInt(dragStartCell, Vector2Int.one) : GridUtil.RectFromCorners(dragStartCell, cell);
            }
            else if (CurrentTool != Tool.Select && !overUi)
            {
                rect = new RectInt(cell, Vector2Int.one);
            }
            else
            {
                preview.enabled = false;
                return;
            }

            preview.enabled = true;
            preview.transform.position = new Vector3(rect.x, rect.y, 0f);
            preview.transform.localScale = new Vector3(rect.width, rect.height, 1f);
            preview.color = CurrentTool switch
            {
                Tool.Build => BuildPreview,
                Tool.Deconstruct => DemolishPreview,
                Tool.Cancel => CancelPreview,
                _ => SelectPreview,
            };
        }

        Pawn PawnAt(Vector2 world)
        {
            Pawn best = null;
            float bestDist = 0.6f * 0.6f;
            foreach (var pawn in game.Pawns.All)
            {
                if (pawn.Dead) continue;
                float d = (pawn.Position - world).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = pawn;
                }
            }
            return best;
        }

        Vector3 ScreenToWorld(Vector2 screen)
        {
            var w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            w.z = 0f;
            return w;
        }

        Vector2Int ScreenToCell(Vector2 screen)
        {
            var w = ScreenToWorld(screen);
            return new Vector2Int(Mathf.FloorToInt(w.x), Mathf.FloorToInt(w.y));
        }
    }
}
