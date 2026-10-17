using System.Collections.Generic;
using OSistema.Crime;
using OSistema.Npcs;
using OSistema.Pawns;
using OSistema.Views;
using OSistema.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>
    /// The minimap (top right), the full map (M) with waypoints, the objective line, and the marker
    /// that points the way on screen.
    /// </summary>
    public sealed partial class Hud
    {
        const float MiniScale = 3f;      // minimap pixels per cell
        const float MiniW = 290f, MiniH = 170f;

        Texture2D mapTex;
        Color32[] mapPixels;
        float mapTexTimer;
        VisualElement miniFrame, miniImage, miniLayer;
        Label goalLabel, goalMarker;
        readonly List<VisualElement> miniDots = new List<VisualElement>();
        int miniDotsUsed;

        bool mapOpen;
        VisualElement mapDim, bigImage, bigLayer;
        readonly List<VisualElement> bigDots = new List<VisualElement>();
        int bigDotsUsed;
        float bigScale;

        Vector2? waypoint;
        Goal goal;

        void BuildMap()
        {
            miniFrame = Ui.Box("panel", "minimap").Into(hudLayer);
            miniImage = Ui.Box("minimap__image").Into(miniFrame);
            miniImage.pickingMode = PickingMode.Ignore;
            miniLayer = Ui.Box("layer").Into(miniFrame);
            miniLayer.pickingMode = PickingMode.Ignore;
            miniFrame.RegisterCallback<ClickEvent>(_ => ToggleMap());
            Tip(miniFrame, () => "Click, or press M, for the full map.");

            goalLabel = Ui.Text("", hudLayer, "goal");
            goalMarker = Ui.Text("◆", worldLayer, "goal-marker");
            goalMarker.pickingMode = PickingMode.Ignore;

            mapDim = Ui.Box("dim").Into(modalLayer);
            var panel = Ui.Box("panel", "bigmap").Into(mapDim);
            var head = Ui.Row(panel);
            Ui.Text("THE RIONE", head, "title", "gold");
            Ui.Text("   click to set a waypoint · click it again to clear", head, "small", "muted");
            Ui.Box("spacer").Into(head);
            Ui.Btn("Close  [M]", ToggleMap, head, "btn--small");
            bigImage = Ui.Box("bigmap__image").Into(panel);
            bigLayer = Ui.Box("layer").Into(bigImage);
            bigLayer.pickingMode = PickingMode.Ignore;
            var legend = Ui.Row(panel);
            legend.style.marginTop = 6;
            foreach (var (cls, text) in new[] { ("dot--you", "you"), ("dot--crew", "crew"), ("dot--work", "work for you"), ("dot--goal", "objective"), ("dot--home", "your homes"), ("dot--piazza", "piazza"), ("dot--police", "police") })
            {
                var d = Ui.Box("dot", cls).Into(legend);
                d.style.position = Position.Relative;
                d.style.marginRight = 4;
                Ui.Text(text, legend, "small", "muted").style.marginRight = 12;
            }
            bigImage.RegisterCallback<PointerDownEvent>(e =>
            {
                var local = (Vector2)e.localPosition;
                var cell = new Vector2(local.x / bigScale, game.Map.Height - local.y / bigScale);
                waypoint = waypoint.HasValue && (waypoint.Value - cell).magnitude < 4f ? (Vector2?)null : cell;
            });
            mapDim.Show(false);
        }

        void ToggleMap()
        {
            mapOpen = !mapOpen;
            mapDim.Show(mapOpen);
            mapTexTimer = 99f;
        }

        // ---------- The map texture ----------

        void RedrawMapTexture()
        {
            var map = game.Map;
            if (mapTex == null || mapTex.width != map.Width || mapTex.height != map.Height)
            {
                mapTex = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                mapPixels = new Color32[map.Width * map.Height];
            }
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                Color32 c;
                var cell = new Vector2Int(x, y);
                if (!FogOfWar.HasExplored(cell)) c = new Color32(18, 17, 16, 255);
                else
                {
                    var b = map.GetBuilding(x, y);
                    var t = map.GetTerrain(x, y);
                    if (b == BuildingDefs.Wall) c = new Color32(150, 128, 104, 255);
                    else if (t == TerrainType.Floor || t == TerrainType.Marble) c = game.Properties.IsOwnedHome(cell) || game.PlayerBase.Contains(cell) ? new Color32(96, 140, 92, 255) : new Color32(128, 84, 64, 255);
                    else c = t switch
                    {
                        TerrainType.Road => new Color32(46, 46, 52, 255),
                        TerrainType.Sidewalk => new Color32(72, 72, 80, 255),
                        TerrainType.Paving => new Color32(150, 144, 132, 255),
                        TerrainType.Grass => new Color32(70, 104, 56, 255),
                        TerrainType.Water => new Color32(40, 82, 120, 255),
                        _ => new Color32(104, 92, 72, 255),
                    };
                }
                mapPixels[y * map.Width + x] = c;
            }
            mapTex.SetPixels32(mapPixels);
            mapTex.Apply(false);
            miniImage.style.backgroundImage = new StyleBackground(mapTex);
            bigImage.style.backgroundImage = new StyleBackground(mapTex);
        }

        // ---------- Every frame ----------

        void UpdateMap()
        {
            if (game == null || miniFrame == null) return;
            var map = game.Map;
            mapTexTimer += Time.unscaledDeltaTime;
            if (mapTexTimer > 3f)
            {
                mapTexTimer = 0f;
                RedrawMapTexture();
            }

            goal = Objectives.Current(game, waypoint);
            var player = game.Player;
            Vector2 centre = player != null ? player.Position : (Vector2)cam.transform.position;

            // Minimap: the whole map image, slid so the player is in the middle.
            miniImage.style.width = map.Width * MiniScale;
            miniImage.style.height = map.Height * MiniScale;
            miniImage.style.left = MiniW / 2f - centre.x * MiniScale;
            miniImage.style.top = MiniH / 2f - (map.Height - centre.y) * MiniScale;

            miniDotsUsed = 0;
            Vector2 Mini(Vector2 w) => new Vector2(MiniW / 2f + (w.x - centre.x) * MiniScale, MiniH / 2f - (w.y - centre.y) * MiniScale);
            void MiniDot(Vector2 world, string cls, bool clamp = false)
            {
                var pos = Mini(world);
                if (!clamp && (pos.x < 0 || pos.y < 0 || pos.x > MiniW || pos.y > MiniH)) return;
                pos.x = Mathf.Clamp(pos.x, 4f, MiniW - 4f);
                pos.y = Mathf.Clamp(pos.y, 4f, MiniH - 4f);
                var d = NextDot(miniDots, ref miniDotsUsed, miniLayer);
                d.ClearClassList();
                d.AddToClassList("dot");
                d.AddToClassList(cls);
                d.style.left = pos.x;
                d.style.top = pos.y;
            }
            Markers(MiniDot);
            if (goal.Target.HasValue) MiniDot(goal.Target.Value, "dot--goal", clamp: true);
            if (player != null) MiniDot(player.Position, "dot--you");
            for (int i = miniDotsUsed; i < miniDots.Count; i++) miniDots[i].style.display = DisplayStyle.None;

            // The objective line.
            string dist = goal.Target.HasValue && player != null ? $"  ·  {Mathf.RoundToInt((goal.Target.Value - player.Position).magnitude * 3f)} m" : "";
            goalLabel.text = string.IsNullOrEmpty(goal.Text) ? "" : $"<color=#E8C26A>▸</color> {goal.Text}{dist}";
            goalLabel.Show(!string.IsNullOrEmpty(goal.Text));

            // The marker in the world, or an arrow at the edge of the screen pointing to it.
            UpdateGoalMarker(player);

            if (mapOpen) UpdateBigMap(player);
        }

        void Markers(System.Action<Vector2, string, bool> dot)
        {
            foreach (var p in game.Pawns.Crew)
                if (p.IsPresent && !p.Controlled) dot(p.Position, "dot--crew", false);
            foreach (var c in game.Contacts.All)
                if (c.Present && game.Contacts.HasWork(c, game) && FogOfWar.HasExplored(c.Spot)) dot(c.Position, "dot--work", false);
            foreach (var sq in game.Org.Squares) dot(Mover.CellCenter(sq.Cell), "dot--piazza", false);
            foreach (var n in game.Npcs.All)
                if (n.Kind == NpcKind.Police && !n.Despawned && FogOfWar.CanSee(n.Mover.Position)) dot(n.Mover.Position, "dot--police", false);
        }

        void UpdateGoalMarker(Pawn player)
        {
            if (!goal.Target.HasValue || player == null || root.panel == null)
            {
                goalMarker.Show(false);
                return;
            }
            goalMarker.Show(true);
            var target = goal.Target.Value;
            var v = cam.WorldToViewportPoint(new Vector3(target.x, target.y, 0f));
            bool onScreen = v.x > 0.03f && v.x < 0.97f && v.y > 0.08f && v.y < 0.9f;
            var size = root.layout.size;
            if (onScreen)
            {
                var pos = WorldToPanel(target + Vector2.up * 0.9f);
                goalMarker.text = "◆";
                goalMarker.style.left = pos.x;
                goalMarker.style.top = pos.y;
                goalMarker.style.rotate = new Rotate(0f);
                goalMarker.style.opacity = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f);
                return;
            }
            // Off screen: an arrow on the edge, pointing the way.
            var from = new Vector2(0.5f, 0.5f);
            var dir = new Vector2(v.x, v.y) - from;
            float k = Mathf.Min(0.42f / Mathf.Max(0.0001f, Mathf.Abs(dir.x)), 0.36f / Mathf.Max(0.0001f, Mathf.Abs(dir.y)));
            var edge = from + dir * k;
            goalMarker.text = "▲";
            goalMarker.style.left = edge.x * size.x;
            goalMarker.style.top = (1f - edge.y) * size.y;
            goalMarker.style.rotate = new Rotate(Angle.Degrees(Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg));
            goalMarker.style.opacity = 1f;
        }

        void UpdateBigMap(Pawn player)
        {
            var map = game.Map;
            bigScale = Mathf.Min(820f / map.Width, 560f / map.Height);
            bigImage.style.width = map.Width * bigScale;
            bigImage.style.height = map.Height * bigScale;
            bigDotsUsed = 0;
            void BigDot(Vector2 w, string cls, bool _)
            {
                var d = NextDot(bigDots, ref bigDotsUsed, bigLayer);
                d.ClearClassList();
                d.AddToClassList("dot");
                d.AddToClassList(cls);
                d.style.left = w.x * bigScale;
                d.style.top = (map.Height - w.y) * bigScale;
            }
            Markers(BigDot);
            foreach (var pr in game.Properties.Owned) BigDot(pr.Rect.center, "dot--home", false);
            if (goal.Target.HasValue) BigDot(goal.Target.Value, "dot--goal", false);
            if (player != null) BigDot(player.Position, "dot--you", false);
            for (int i = bigDotsUsed; i < bigDots.Count; i++) bigDots[i].style.display = DisplayStyle.None;
        }

        static VisualElement NextDot(List<VisualElement> pool, ref int used, VisualElement parent)
        {
            if (used >= pool.Count)
            {
                var d = Ui.Box("dot").Into(parent);
                d.pickingMode = PickingMode.Ignore;
                pool.Add(d);
            }
            var e = pool[used++];
            e.style.display = DisplayStyle.Flex;
            return e;
        }
    }
}
