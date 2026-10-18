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
    public enum Tool { Select, Build, Deconstruct, Cancel, Square, Move }

    /// <summary>Camera, selection, orders and build tools. Turns player input into simulation commands.</summary>
    public sealed class PlayerController : MonoBehaviour
    {
        const float MinZoom = 3f;
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
        LineRenderer aimLine;
        Texture2D ghostTex;
        Sprite ghostSprite;
        readonly Color32[] ghostPixels = new Color32[TilePainter.Hi * TilePainter.Hi];
        (BuildingDef def, int rot, Vector2Int cell) ghostKey;

        Pawn follow;   // the camera keeps this pawn in view until the player pans away

        /// <summary>Let the camera rest somewhere else until the player moves again.</summary>
        public void StopFollowing() => follow = null;
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

            aimLine = new GameObject("Aim").AddComponent<LineRenderer>();
            aimLine.sharedMaterial = SpriteFactory.UnlitMaterial;
            aimLine.startWidth = 0.05f;
            aimLine.endWidth = 0.02f;
            aimLine.sortingOrder = 30;
            aimLine.positionCount = 2;
            aimLine.enabled = false;

            // A see-through copy of the furniture under the cursor, so you can see which way it faces.
            ghostTex = new Texture2D(TilePainter.Hi, TilePainter.Hi, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            ghost = new GameObject("Build Ghost").AddComponent<SpriteRenderer>();
            ghostSprite = Sprite.Create(ghostTex, new Rect(0, 0, TilePainter.Hi, TilePainter.Hi), Vector2.zero, TilePainter.Hi);
            ghost.sprite = ghostSprite;
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
            var art = PropArt.For(CurrentDef, cell.x, cell.y);
            if (art != null)
            {
                float size = PropArt.SizeOf(CurrentDef);
                ghost.sprite = art;
                ghost.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
                ghost.transform.localScale = new Vector3(size, size, 1f);
                ghost.transform.rotation = Quaternion.Euler(0f, 0f, PlacementRotation * 90f);
            }
            else
            {
                ghost.sprite = ghostSprite;
                ghost.transform.position = new Vector3(cell.x, cell.y, 0f);
                ghost.transform.localScale = Vector3.one;
                ghost.transform.rotation = Quaternion.identity;
            }
            ghost.color = CanPlace(cell, out _) ? new Color(1f, 1f, 1f, 0.85f) : new Color(1f, 0.35f, 0.3f, 0.6f);
            var key = (CurrentDef, PlacementRotation, cell);
            if (key == ghostKey || art != null) return;
            ghostKey = key;
            TilePainter.PaintPreviewHi(game.Map, CurrentDef, cell.x, cell.y, PlacementRotation, ghostPixels);
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
            // Start on the player's character, and keep him in view.
            var boss = g.Player;
            if (boss != null)
            {
                follow = boss;
                cam.transform.position = new Vector3(boss.Position.x, boss.Position.y, cam.transform.position.z);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Moving furniture: what's in your hands and where it came from.
        BuildingDef carried;
        Vector2Int carriedFrom;
        int carriedRotation;

        public void SetTool(Tool tool, BuildingDef def = null)
        {
            // Changed your mind halfway through a move: it goes back where it was.
            if (carried != null && !(tool == Tool.Build && def == carried))
            {
                if (game.Map.GetBuilding(carriedFrom.x, carriedFrom.y) == null)
                {
                    game.Map.SetRotation(carriedFrom.x, carriedFrom.y, carriedRotation);
                    game.Map.SetBuilding(carriedFrom.x, carriedFrom.y, carried);
                }
                carried = null;
            }
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

            if (hud.MenuOpen) return;   // typing a name, or in a menu: the keys aren't for the game

            var player = game.Player;
            // Working at a table or shopping counts as doing something: don't nod off at it.
            if (player != null && (hud.MinigameOpen || hud.ShopOpen || mouse.leftButton.wasPressedThisFrame)) player.LastInputTick = game.Ticks.TicksGame;
            if (hud.MinigameOpen || hud.ShopOpen)
            {
                Aiming = false;
                aimLine.enabled = false;
                if (player != null) player.AimPoint = null;
                return;
            }

            HandleKeys(kb);
            HandleDebugKeys(kb);
            HandlePlayer(kb, mouse, mousePos, overUi);
            HandleCamera(kb, mouse, mousePos, overUi);
            if (!Aiming) HandleMouse(kb, mouse, mousePos, overUi);
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
            if (kb.eKey.wasPressedThisFrame && CurrentTool != Tool.Build) Interaction()?.Act();
            if (kb.fKey.wasPressedThisFrame) follow = game.Player;
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

        void ToggleDraftSelected()
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
            // Arrow keys look around; WASD moves the character and brings the camera back to him.
            var move = Vector2.zero;
            if (kb.upArrowKey.isPressed) move.y += 1f;
            if (kb.downArrowKey.isPressed) move.y -= 1f;
            if (kb.leftArrowKey.isPressed) move.x -= 1f;
            if (kb.rightArrowKey.isPressed) move.x += 1f;
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

            // Right-click cancels a tool; with the select tool it's the aim button (see HandlePlayer).
            if (mouse.rightButton.wasPressedThisFrame && !overUi && CurrentTool != Tool.Select) SetTool(Tool.Select);
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
                    break;

                case Tool.Build:
                {
                    // Bought, carried in, put down: no building work.
                    if (!CanPlace(start, out var why))
                    {
                        if (why != null) game.Log.Add(why, MessageKind.Bad);
                        break;
                    }
                    if (carried == CurrentDef && carried != null)
                    {
                        // Putting down something you're moving: free.
                        game.Map.SetRotation(start.x, start.y, (byte)PlacementRotation);
                        game.Map.SetBuilding(start.x, start.y, CurrentDef);
                        carried = null;
                        SetTool(Tool.Move);
                        break;
                    }
                    if (!BuildingDef.BuiltInPlace(CurrentDef))
                    {
                        // Furniture comes out of storage: bought at the Mobilificio (or from Nanninella).
                        if (game.Stock.StoredOf(CurrentDef) <= 0)
                        {
                            game.Log.Add(CurrentDef == BuildingDefs.BaggingTable || CurrentDef == BuildingDefs.RollingTable
                                ? $"You don't have a {CurrentDef.Label.ToLower()}. Nanninella can get you one."
                                : $"You don't have a {CurrentDef.Label.ToLower()}. Buy one at the Mobilificio.", MessageKind.Bad);
                            break;
                        }
                        game.Stock.Store(CurrentDef, -1);
                        game.Map.SetRotation(start.x, start.y, (byte)PlacementRotation);
                        game.Map.SetBuilding(start.x, start.y, CurrentDef);
                        OSistema.Views.SoundManager.PlayUi(OSistema.Views.AudioSynth.Click, 0.3f);
                        if (game.Stock.StoredOf(CurrentDef) <= 0) SetTool(Tool.Select);
                        break;
                    }
                    int cost = BuildingDef.CostOf(CurrentDef);
                    if (!game.Org.Pay(cost, game, out var payError))
                    {
                        game.Log.Add(payError, MessageKind.Bad);
                        break;
                    }
                    game.Map.SetRotation(start.x, start.y, (byte)PlacementRotation);
                    game.Map.SetBuilding(start.x, start.y, CurrentDef);
                    game.PopupAt(ScreenToWorld(mousePos), $"-€{cost}");
                    OSistema.Views.SoundManager.PlayUi(OSistema.Views.AudioSynth.Click, 0.3f);
                    break;
                }

                case Tool.Move:
                {
                    var b = game.Map.GetBuilding(start.x, start.y);
                    if (b == null || !InOwnedHome(start) || b == BuildingDefs.Wall || b == BuildingDefs.Door) break;
                    carriedFrom = start;
                    carriedRotation = game.Map.GetRotation(start.x, start.y);
                    game.Map.SetBuilding(start.x, start.y, null);
                    PlacementRotation = carriedRotation;
                    SetTool(Tool.Build, b);
                    carried = b;
                    break;
                }

                case Tool.Deconstruct:
                    foreach (var c in GridUtil.Cells(rect, false))
                    {
                        var b = game.Map.GetBuilding(c.x, c.y);
                        if (b == null || !InOwnedHome(c) || b == BuildingDefs.Wall || b == BuildingDefs.Door) continue;
                        game.Map.SetBuilding(c.x, c.y, null);
                        if (BuildingDef.BuiltInPlace(b))
                        {
                            int refund = BuildingDef.CostOf(b) / 2;
                            game.Stock.Cash += refund;
                            if (refund > 0) game.PopupAt(Mover.CellCenter(c), $"+€{refund}");
                        }
                        else
                        {
                            // Back into storage, to put down somewhere else.
                            game.Stock.Store(b);
                            game.PopupAt(Mover.CellCenter(c), "stored");
                        }
                    }
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

        bool InOwnedHome(Vector2Int c) => game.PlayerBase.Contains(c) || game.Properties.IsOwnedHome(c);

        /// <summary>Furniture goes on a free floor tile inside a home the family owns.</summary>
        public bool CanPlace(Vector2Int c, out string why)
        {
            why = null;
            if (CurrentDef == null || !game.Map.InBounds(c)) return false;
            if (!InOwnedHome(c)) { why = "You can only furnish homes the family owns. Buy a place first (Famiglia, Property)."; return false; }
            var t = game.Map.GetTerrain(c.x, c.y);
            if (t != TerrainType.Floor && t != TerrainType.Marble) { why = "Put it indoors."; return false; }
            if (game.Map.GetBuilding(c.x, c.y) != null) { why = "Something is already there."; return false; }
            foreach (var p in game.Pawns.All)
                if (p.IsPresent && p.Cell == c) { why = "Someone is standing there."; return false; }
            return true;
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

        /// <summary>The contact the selected pawn could talk to right now (E), if any.</summary>
        public Contact ContactInReach(out Pawn pawn)
        {
            pawn = game.Player;
            if (pawn == null || !pawn.CanAct) return null;
            Contact best = null;
            float bestD = TalkReach * TalkReach;
            foreach (var c in game.Contacts.All)
            {
                if (!c.Present || !game.Contacts.Visible(c, game)) continue;
                float d = (c.Position - pawn.Position).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = c;
                }
            }
            return best;
        }

        public const float TalkReach = 3.5f;


        // ---------- The player's character ----------

        public bool Aiming { get; private set; }

        void HandlePlayer(Keyboard kb, Mouse mouse, Vector2 mousePos, bool overUi)
        {
            var p = game.Player;
            if (p == null || !p.CanAct || game.Org.Hiding) { Aiming = false; aimLine.enabled = false; return; }
            long now = game.Ticks.TicksGame;

            var dir = Vector2.zero;
            if (kb.wKey.isPressed) dir.y += 1f;
            if (kb.sKey.isPressed) dir.y -= 1f;
            if (kb.aKey.isPressed) dir.x -= 1f;
            if (kb.dKey.isPressed) dir.x += 1f;
            float dt = Time.unscaledDeltaTime;
            bool running = false;
            bool live = game.Ticks.Speed > 0 && game.Events.Current == null;

            // Z: let him get on with things on his own (eat, sleep, do the lavoretto), or not.
            if (kb.zKey.wasPressedThisFrame) hud.ToggleAutoMovement();

            // R: get on or off your scooter.
            if (kb.rKey.wasPressedThisFrame && live) ToggleScooter(p);

            if (p.Riding && live)
            {
                // Riding: W throttle, S brake (and a slow reverse), A/D lean into the turn.
                // It needs speed to turn, slides a little in corners, and bangs into walls.
                float top = Pawn.BaseSpeed * TickManager.TicksPerSecond * 2.6f;
                bool gas = kb.wKey.isPressed, brake = kb.sKey.isPressed;
                float steer = (kb.aKey.isPressed ? 1f : 0f) - (kb.dKey.isPressed ? 1f : 0f);
                if (gas) p.ScooterSpeed = Mathf.MoveTowards(p.ScooterSpeed, top, 8f * dt);
                else if (brake) p.ScooterSpeed = Mathf.MoveTowards(p.ScooterSpeed, -2.5f, (p.ScooterSpeed > 0f ? 16f : 4f) * dt);
                else p.ScooterSpeed = Mathf.MoveTowards(p.ScooterSpeed, 0f, 3f * dt);
                float grip = Mathf.Clamp01(Mathf.Abs(p.ScooterSpeed) / 2.5f);
                float turnRate = 190f * grip * (1f - 0.4f * Mathf.Clamp01(p.ScooterSpeed / top));
                p.Heading += steer * turnRate * dt * (p.ScooterSpeed < 0f ? -1f : 1f);
                p.Velocity = Vector2.Lerp(p.Velocity, p.Forward * p.ScooterSpeed, Mathf.Clamp01(6f * dt));
                if (p.Velocity.sqrMagnitude > 0.0001f || gas || brake || steer != 0f)
                {
                    p.LastInputTick = now;
                    if (p.CurrentJob != null && !(p.CurrentJob is DownedJob)) p.EndCurrentJob(game);
                    RideStep(p, dt);
                    follow = p;
                }
            }
            else if (dir != Vector2.zero && live)
            {
                p.LastInputTick = now;
                if (p.CurrentJob != null && !(p.CurrentJob is DownedJob)) p.EndCurrentJob(game);
                if (p.Sleep != SleepState.Awake) p.Sleep = SleepState.Awake;
                // On foot, in real time whatever the game speed: a walk, a jog, never as fast as a scooter.
                float step = Pawn.BaseSpeed * p.SpeedFactor * TickManager.TicksPerSecond * dt;
                // Tired or hungry legs are slower.
                if (p.Needs.Rest < Needs.ExhaustedBelow * 2f || p.Needs.Food < Needs.RavenousBelow) step *= 0.8f;
                running = kb.leftShiftKey.isPressed && !p.Winded;
                if (running) step *= 1.5f;
                p.Mover.Slide(dir.normalized * step, game.Map);
                p.Heading = Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg;   // so a scooter starts off the way you were walking
                follow = p;
            }
            // Breath: about five seconds of running; once it's gone you can't run until it's back past a third.
            float recover = (p.Needs.Food < 0.25f || p.Needs.Rest < 0.25f) ? 0.05f : 0.09f;
            p.Stamina = Mathf.Clamp01(p.Stamina + (running ? -0.2f : recover * (dir == Vector2.zero ? 1.6f : 1f)) * dt);
            if (p.Stamina <= 0.001f) p.Winded = true;
            else if (p.Winded && p.Stamina >= 0.35f) p.Winded = false;

            // Hold the right button to aim, click the left to shoot (or punch, unarmed).
            Aiming = mouse.rightButton.isPressed && !overUi && CurrentTool == Tool.Select;
            p.AimPoint = Aiming ? (Vector2)ScreenToWorld(mousePos) : (Vector2?)null;
            aimLine.enabled = Aiming;
            if (Aiming)
            {
                var to = (Vector2)ScreenToWorld(mousePos);
                var aim = to - p.Position;
                float range = p.Armed ? Combat.CombatSystem.PistolRange : Combat.CombatSystem.MeleeRange;
                if (aim.magnitude > range) to = p.Position + aim.normalized * range;
                var col = p.Armed ? new Color(1f, 0.25f, 0.2f, 0.75f) : new Color(1f, 0.9f, 0.6f, 0.6f);
                aimLine.startColor = col;
                aimLine.endColor = new Color(col.r, col.g, col.b, 0.1f);
                aimLine.SetPosition(0, new Vector3(p.Position.x, p.Position.y, 0f));
                aimLine.SetPosition(1, new Vector3(to.x, to.y, 0f));
            }
            if (Aiming)
            {
                p.LastInputTick = now;
                if (mouse.leftButton.wasPressedThisFrame && game.Ticks.Speed > 0) game.Combat.PlayerAttack(p, ScreenToWorld(mousePos), game);
            }
        }

        /// <summary>
        /// Moves the scooter with a slim hitbox. Grazing a wall doesn't stop you: the blocked part of the
        /// motion is dropped, the rest carries on, and the scooter turns to follow the wall. Clipping a
        /// corner or a lamppost nudges you round it. Only a head-on hit really slows you down.
        /// </summary>
        void RideStep(Pawn p, float dt)
        {
            const float radius = 0.2f;
            var map = game.Map;
            var move = p.Velocity * dt;
            var before = p.Position;
            var hit = p.Mover.Slide(move, map, radius, outdoorsOnly: true);
            if (!hit.x && !hit.y) return;

            // Corner assist: try slipping sideways a little to get round the edge.
            var blockedPart = new Vector2(hit.x ? move.x : 0f, hit.y ? move.y : 0f);
            if (blockedPart.sqrMagnitude > 0f)
            {
                var side = new Vector2(-blockedPart.y, blockedPart.x).normalized;
                foreach (float s in new[] { 0.12f, -0.12f, 0.24f, -0.24f })
                {
                    var probe = p.Position + side * s + blockedPart;
                    if (!Clear(probe, radius, map)) continue;
                    p.Mover.Place(probe);
                    return;
                }
            }

            // Glance off: keep the speed along the wall, turn the nose to follow it.
            var moved = p.Position - before;
            float along = move.sqrMagnitude > 0f ? moved.magnitude / move.magnitude : 0f;
            if (hit.x) p.Velocity.x = 0f;
            if (hit.y) p.Velocity.y = 0f;
            if (p.Velocity.sqrMagnitude > 0.04f)
            {
                float target = Mathf.Atan2(-p.Velocity.x, p.Velocity.y) * Mathf.Rad2Deg;
                if (p.ScooterSpeed < 0f) target += 180f;
                p.Heading = Mathf.MoveTowardsAngle(p.Heading, target, 360f * dt);
            }
            float keep = Mathf.Lerp(0.35f, 0.97f, along);
            if (keep < 0.5f && Mathf.Abs(p.ScooterSpeed) > 6f) OSistema.Views.SoundManager.PlayAt(OSistema.Views.AudioSynth.Punch, p.Position, 0.25f);
            p.ScooterSpeed *= keep;
        }

        static bool Clear(Vector2 at, float r, Map map)
        {
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector2Int(Mathf.FloorToInt(at.x + (i % 2 == 0 ? -r : r)), Mathf.FloorToInt(at.y + (i < 2 ? -r : r)));
                if (!map.InBounds(c) || !map.IsPassable(c)) return false;
                var t = map.GetTerrain(c.x, c.y);
                if (t == TerrainType.Floor || t == TerrainType.Marble || map.GetBuilding(c.x, c.y) == BuildingDefs.Door) return false;
            }
            return true;
        }

        void ToggleScooter(Pawn p)
        {
            if (p.Riding && p.StolenScooter)
            {
                game.Log.Add("Not here: take it to Nanninella's garage.", MessageKind.Info);
                return;
            }
            if (p.Riding)
            {
                p.Riding = p.OnScooter = false;
                p.Velocity = Vector2.zero;
                p.ScooterSpeed = 0f;
                return;
            }
            int inUse = 0;
            foreach (var c in game.Pawns.Crew) if (c != p && c.OnScooter) inUse++;
            if (game.Stock.Scooters - inUse <= 0)
            {
                game.Log.Add(game.Stock.Scooters == 0 ? "The family has no scooter. Buy one (Famiglia, Supply)." : "Every scooter is out with the crew.", MessageKind.Info);
                return;
            }
            var t = game.Map.GetTerrain(p.Cell.x, p.Cell.y);
            if (t == TerrainType.Floor || t == TerrainType.Marble)
            {
                game.Log.Add("Get outside first.", MessageKind.Info);
                return;
            }
            p.Riding = p.OnScooter = true;
            p.Velocity = Vector2.zero;
            p.ScooterSpeed = 0f;
            p.EndCurrentJob(game);
            game.Log.Add("W to go, S to brake, A / D to steer. R to get off.", MessageKind.Info);
        }

        /// <summary>Something the player can do with E right now.</summary>
        public sealed class Action
        {
            public string Label;
            public System.Action Do;
            public void Act() => Do();
        }

        public Action Interaction()
        {
            var p = game.Player;
            if (p == null || !p.CanAct) return null;
            var customer = game.Org.WaitingCustomer(game);
            if (customer != null && game.Stock.Product > 0)
                return new Action { Label = $"Sell a dose · €{Prices.ProductStreet}", Do = () => game.Org.SellToCustomer(customer, game) };

            var contact = ContactInReach(out _);
            if (contact != null) return new Action { Label = $"Talk to {contact.Name}", Do = () => OrderTalk(contact) };

            // A lavoretto in hand: do the next step yourself when you're there.
            var job = game.Contracts.ActiveFor(p);
            if (job != null && !(p.CurrentJob is ContractJob))
            {
                var at = Contracts.StepTarget(job);
                if ((Mover.CellCenter(at) - p.Position).sqrMagnitude < 2.4f * 2.4f)
                {
                    if (job.Kind == ContractKind.Debt)
                        return new Action { Label = "Knock on the door", Do = () => game.Events.Raise(Story.StreetScenes.Knock(job, p, game)) };
                    return new Action { Label = Contracts.StepLabel(job), Do = () =>
                    {
                        p.LastInputTick = game.Ticks.TicksGame;
                        ContractJob.ArriveNow(job);   // you're there: start now, no walking
                        p.ForceJob(new ContractJob(p, job), game);
                    } };
                }
            }

            if (Near(p, BuildingDefs.BaggingTable, out var bagT))
            {
                var w = game.WorkerAt(bagT);
                return w != null ? new Action { Label = $"{w.Nickname} is working at this table", Do = () => { } }
                                 : new Action { Label = "Cut and bag doses", Do = () => hud.OpenBagging() };
            }
            if (Near(p, BuildingDefs.RollingTable, out var rollT))
            {
                var w = game.WorkerAt(rollT);
                return w != null ? new Action { Label = $"{w.Nickname} is working at this table", Do = () => { } }
                                 : new Action { Label = "Roll a pack of ROSSE", Do = () => hud.OpenRolling() };
            }

            foreach (var b in game.Org.Businesses)
            {
                if (b.Status == BusinessStatus.Closed) continue;
                // Inside, at the counter.
                if ((Mover.CellCenter(b.Cell) - p.Position).sqrMagnitude > 2.4f * 2.4f) continue;
                var here = game.Map.GetTerrain(p.Cell.x, p.Cell.y);
                if (here != TerrainType.Floor && here != TerrainType.Marble) continue;
                int hour = game.Clock.Hour;
                bool shopKind = b.Kind == ShopKind.Tabaccheria || b.Kind == ShopKind.Salumeria || b.Kind == ShopKind.Mobili || b.Kind == ShopKind.Lusso || b.Kind == ShopKind.Farmacia || b.Kind == ShopKind.Macelleria;
                if (!shopKind && !Story.ShopCards.PizzoActionable(b, game)) continue;
                if (hour < 8 || hour >= 21) return new Action { Label = $"{b.Name} is closed", Do = () => { } };
                var shop = b;
                if (shopKind) return new Action { Label = $"Buy at the counter", Do = () => hud.OpenShop(shop) };
                string what = b.Status == BusinessStatus.Paying ? "Collect the pizzo at" : "Have a word with the owner of";
                return new Action { Label = $"{what} {b.Name}", Do = () => game.Events.Raise(Story.ShopCards.Owner(shop, game)) };
            }

            // A pizza at the counter, eaten on the spot.
            if (p.Needs.Food < 0.85f && Near(p, BuildingDefs.PizzeriaCounter, out var pizzeria))
                return new Action { Label = $"Buy a pizza · €{Prices.Pizza}", Do = () => { p.ForceJob(new EatJob(p, pizzeria, atShop: true), game); } };

            if (Near(p, BuildingDefs.Bed, out var bed) && (game.PlayerBase.Contains(bed) || game.Properties.IsOwnedHome(bed)))
            {
                // Your own bed, or a free one (which becomes yours). Not someone else's.
                Pawn owner = null;
                foreach (var c in game.Pawns.Crew) if (c != p && !c.Dead && c.Bed == bed) owner = c;
                if (owner != null && p.Bed != bed) return new Action { Label = $"{owner.Nickname}'s bed (assign beds in Famiglia)", Do = () => { } };
                return new Action { Label = "Sleep", Do = () => { p.Bed = bed; p.LastInputTick = -1000000; p.ForceJob(new SleepJob(p, bed, null), game); } };
            }
            if (game.Stock.Meals > 0 && p.Needs.Food < 0.8f && Near(p, BuildingDefs.Pantry, out var pantry))
                return new Action { Label = "Eat something", Do = () => { p.LastInputTick = -1000000; p.ForceJob(new EatJob(p, pantry, atShop: false), game); } };

            var corner = game.Org.DealingCorner;
            if (corner != null) return new Action { Label = "Leave the corner", Do = () => game.Org.StopDealing(game) };
            if (game.Org.CanDealHere(p, game))
                return new Action { Label = $"Deal here ({game.Stock.Product} doses on you)", Do = () => game.Org.StartDealing(game) };
            return null;
        }

        bool Near(Pawn p, BuildingDef def, out Vector2Int at)
        {
            at = default;
            foreach (var c in GridUtil.CellsAround(p.Cell, 1))
            {
                if (!game.Map.InBounds(c) || game.Map.GetBuilding(c) != def) continue;
                if ((Mover.CellCenter(c) - p.Position).sqrMagnitude > 1.8f * 1.8f) continue;
                at = c;
                return true;
            }
            return false;
        }

        // The player's character goes to talk.
        void OrderTalk(Contact contact)
        {
            var pawn = game.Player;
            if (pawn == null || !pawn.CanAct) return;
            pawn.SetDrafted(false, game);
            pawn.ForceJob(new TalkJob(pawn, contact), game);
            pawn.Say("order_move");
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
