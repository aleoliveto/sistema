using System.Collections.Generic;
using OSistema.AI;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.UI;
using OSistema.Views;
using OSistema.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OSistema.Input
{
    public enum Tool { Select, Build, Deconstruct, Cancel, Square }

    /// <summary>Camera, selection, orders and build tools. Turns player input into simulation commands.</summary>
    public sealed class PlayerController : MonoBehaviour
    {
        const float MinZoom = 4f;
        const float MaxZoom = 45f;
        const float ClickThresholdPixels = 6f;

        static readonly Color SelectPreview = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color BuildPreview = new Color(0.35f, 0.75f, 1f, 0.35f);
        static readonly Color DemolishPreview = new Color(1f, 0.3f, 0.25f, 0.35f);
        static readonly Color SquarePreview = new Color(0.8f, 0.25f, 0.9f, 0.45f);
        static readonly Color CancelPreview = new Color(1f, 0.65f, 0.2f, 0.35f);

        public static PlayerController Instance { get; private set; }

        public Tool CurrentTool { get; private set; } = Tool.Select;
        public BuildingDef CurrentDef { get; private set; }

        /// <summary>Quarter turns for furniture being placed (Q / E).</summary>
        public int PlacementRotation { get; private set; }
        public readonly List<Pawn> Selected = new List<Pawn>();

        /// <summary>A building clicked on the map (shown in the inspector), or null.</summary>
        public Property SelectedProperty { get; private set; }

        public void SelectProperty(Property p)
        {
            SelectedProperty = p;
            if (p != null) Selected.Clear();
        }

        /// <summary>Frame on which Escape was used to cancel a tool or a selection (so menus don't open too).</summary>
        public int EscapeConsumedFrame { get; private set; } = -1;

        Game game;
        Camera cam;
        Hud hud;
        SpriteRenderer preview;
        SpriteRenderer ghost;
        Texture2D ghostTex;
        readonly Color32[] ghostPixels = new Color32[TilePainter.Size * TilePainter.Size];
        (BuildingDef def, int rot, Vector2Int cell) ghostKey;

        Pawn follow;   // the camera keeps this pawn in view until the player pans away
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

            // A see-through copy of the furniture under the cursor, so you can see which way it faces.
            ghostTex = new Texture2D(TilePainter.Size, TilePainter.Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            ghost = new GameObject("Build Ghost").AddComponent<SpriteRenderer>();
            ghost.sprite = Sprite.Create(ghostTex, new Rect(0, 0, TilePainter.Size, TilePainter.Size), Vector2.zero, TilePainter.Size);
            ghost.sharedMaterial = SpriteFactory.UnlitMaterial;
            ghost.sortingOrder = 21;
            ghost.color = new Color(1f, 1f, 1f, 0.8f);
            ghost.enabled = false;
        }

        void UpdateGhost(Vector2Int cell, bool show)
        {
            show &= CurrentTool == Tool.Build && CurrentDef != null && CurrentDef.PlaceMode == PlaceMode.Single
                    && CurrentDef != BuildingDefs.Door && game.Map.InBounds(cell.x, cell.y);
            ghost.enabled = show;
            if (!show) return;
            ghost.transform.position = new Vector3(cell.x, cell.y, 0f);
            var key = (CurrentDef, PlacementRotation, cell);
            if (key == ghostKey) return;
            ghostKey = key;
            TilePainter.PaintPreview(game.Map, CurrentDef, cell.x, cell.y, PlacementRotation, ghostPixels);
            ghostTex.SetPixels32(ghostPixels);
            ghostTex.Apply();
        }

        /// <summary>Points the controller at a new or loaded game.</summary>
        public void Bind(Game g)
        {
            game = g;
            Selected.Clear();
            SelectedProperty = null;
            SetTool(Tool.Select);
            // Start on the boss, and keep him in view.
            var boss = g.Org.Boss(g);
            if (boss != null)
            {
                Selected.Add(boss);
                follow = boss;
                cam.transform.position = new Vector3(boss.Position.x, boss.Position.y, cam.transform.position.z);
            }
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

            Selected.RemoveAll(p => p.Dead || p.InJail || p.InHospital);

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
            if (kb.digit4Key.wasPressedThisFrame) game.Ticks.SetSpeed(4); // debug ×30
            if (kb.digit5Key.wasPressedThisFrame) game.Ticks.SetSpeed(5); // debug ×120
            if (kb.rKey.wasPressedThisFrame) ToggleDraftSelected();
            if (kb.fKey.wasPressedThisFrame) follow = follow == null && Selected.Count > 0 ? Selected[0] : null;
            if (CurrentTool == Tool.Build && kb.qKey.wasPressedThisFrame) PlacementRotation = (PlacementRotation + 1) % 4;
            if (CurrentTool == Tool.Build && kb.eKey.wasPressedThisFrame) PlacementRotation = (PlacementRotation + 3) % 4;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (CurrentTool != Tool.Select) SetTool(Tool.Select);
                else if (Selected.Count > 0 || SelectedProperty != null)
                {
                    Selected.Clear();
                    SelectedProperty = null;
                }
                else return;
                EscapeConsumedFrame = Time.frameCount;
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
            if (kb.f10Key.wasPressedThisFrame)
            {
                game.Rival.DebugHit(game);
                game.Log.Add("[Debug] A rival hit squad is on its way (needs a crew member outside the basso).");
            }
            if (kb.f11Key.wasPressedThisFrame)
            {
                if (game.Org.Squares.Count == 0) game.Log.Add("[Debug] A stesa needs one of your piazze to target.");
                else game.Rival.DebugStesa(game);
            }
            if (kb.f8Key.wasPressedThisFrame)
            {
                game.Ticks.Advance(GameClock.TicksPerHour * 6);
                game.Log.Add("[Debug] Skipped 6 hours.");
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
            if (move != Vector2.zero || mouse.middleButton.isPressed) follow = null;
            if (follow != null && follow.IsPresent)
            {
                var c = cam.transform.position;
                var t = new Vector3(follow.Position.x, follow.Position.y, c.z);
                cam.transform.position = Vector3.Lerp(c, t, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            }

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
                else if (Selected.Count > 0 && EnemyAt(ScreenToWorld(mousePos)) is Pawn enemy) OrderAttack(enemy);
                else if (Selected.Count > 0 && game.Contacts.At(ScreenToWorld(mousePos)) is Contact contact) OrderTalk(contact);
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
                        // Clicking a building (that the crew knows about) shows it in the inspector.
                        SelectedProperty = null;
                        if (pawn == null && OSistema.Views.FogOfWar.HasExplored(start))
                        {
                            var p = game.Properties.At(start);
                            if (p != null) SelectProperty(p);
                        }
                    }
                    else
                    {
                        SelectedProperty = null;
                        foreach (var pawn in game.Pawns.Crew)
                            if (pawn.IsPresent && rect.Contains(pawn.Cell) && !Selected.Contains(pawn)) Selected.Add(pawn);
                    }
                    break;

                case Tool.Build:
                {
                    // Materials and furniture cost money, paid up front.
                    int cost = BuildingDef.CostOf(CurrentDef), spent = 0;
                    bool broke = false;
                    var cells = CurrentDef.PlaceMode == PlaceMode.Single ? new List<Vector2Int> { start }
                        : new List<Vector2Int>(GridUtil.Cells(rect, CurrentDef.PlaceMode == PlaceMode.Outline));
                    foreach (var c in cells)
                    {
                        if (game.Stock.Cash < cost) { broke = true; break; }
                        if (!game.Construction.PlaceBuild(c, CurrentDef, PlacementRotation)) continue;
                        game.Stock.Cash -= cost;
                        spent += cost;
                    }
                    if (spent > 0) game.PopupAt(ScreenToWorld(mousePos), $"-€{spent}");
                    if (broke) game.Log.Add($"Not enough cash: a {CurrentDef.Label.ToLower()} costs €{cost}.", MessageKind.Bad);
                    break;
                }

                case Tool.Deconstruct:
                    foreach (var c in GridUtil.Cells(rect, false)) game.Construction.PlaceDeconstruct(c);
                    break;

                case Tool.Cancel:
                    foreach (var c in GridUtil.Cells(rect, false))
                    {
                        var bp = game.Construction.At(c);
                        if (bp != null && !bp.Deconstruct) game.Stock.Cash += BuildingDef.CostOf(bp.Def); // refund
                        game.Construction.Cancel(c);
                        if (game.Org.CloseSquareAt(c)) game.Log.Add("Closed the piazza.");
                    }
                    break;

                case Tool.Square:
                    if (!game.Org.TryOpenSquare(start, game, out var error)) game.Log.Add(error, MessageKind.Bad);
                    else SetTool(Tool.Select);
                    break;
            }
        }

        /// <summary>A visible hostile under the cursor, for focus-fire orders.</summary>
        Pawn EnemyAt(Vector2 world)
        {
            Pawn best = null;
            float bestDist = 0.8f * 0.8f;
            foreach (var r in game.Pawns.Rivals)
            {
                if (!r.IsPresent || r.Health.Downed || !OSistema.Views.FogOfWar.CanSee(r.Position)) continue;
                float d = (r.Position - world).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = r;
                }
            }
            return best;
        }

        void OrderAttack(Pawn enemy)
        {
            long now = game.Ticks.TicksGame;
            bool any = false;
            foreach (var pawn in Selected)
            {
                if (!pawn.CanAct || pawn.InMentalBreak(now)) continue;
                pawn.SetDrafted(true, game);
                pawn.ForcedTarget = enemy;
                pawn.ForceJob(new EngageJob(pawn, enemy), game);
                any = true;
            }
            if (any) Selected[0].Say("fight");
        }

        // Only one goes to talk: the first selected who's free.
        void OrderTalk(Contact contact)
        {
            long now = game.Ticks.TicksGame;
            var pawn = Selected.Find(p => p.CanAct && !p.InMentalBreak(now));
            if (pawn == null) return;
            pawn.SetDrafted(false, game);
            pawn.ForceJob(new TalkJob(pawn, contact), game);
            pawn.Say("order_move");
        }

        void OrderMove(Vector2Int target)
        {
            if (Selected.Count == 0 || !game.Map.InBounds(target)) return;
            foreach (var p in Selected) p.ForcedTarget = null;

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
                bool single = CurrentTool == Tool.Square || (CurrentTool == Tool.Build && CurrentDef.PlaceMode == PlaceMode.Single);
                rect = single ? new RectInt(dragStartCell, Vector2Int.one) : GridUtil.RectFromCorners(dragStartCell, cell);
            }
            else if (CurrentTool != Tool.Select && !overUi)
            {
                rect = new RectInt(cell, Vector2Int.one);
            }
            else
            {
                preview.enabled = false;
                UpdateGhost(cell, false);
                return;
            }
            UpdateGhost(new Vector2Int(rect.x, rect.y), true);

            preview.enabled = true;
            preview.transform.position = new Vector3(rect.x, rect.y, 0f);
            preview.transform.localScale = new Vector3(rect.width, rect.height, 1f);
            preview.color = CurrentTool switch
            {
                Tool.Build => BuildPreview,
                Tool.Deconstruct => DemolishPreview,
                Tool.Cancel => CancelPreview,
                Tool.Square => SquarePreview,
                _ => SelectPreview,
            };
        }

        Pawn PawnAt(Vector2 world)
        {
            Pawn best = null;
            float bestDist = 0.6f * 0.6f;
            foreach (var pawn in game.Pawns.Crew)
            {
                if (!pawn.IsPresent) continue;
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
