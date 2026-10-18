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
        Label goalLabel, goalMarker, pinMarker;
        readonly List<Label> bigLabels = new List<Label>();
        int bigLabelsUsed;
        readonly List<VisualElement> miniDots = new List<VisualElement>();
        int miniDotsUsed;

        readonly List<Label> miniIcons = new List<Label>(), bigIcons = new List<Label>();
        int miniIconsUsed, bigIconsUsed;

        /// <summary>A place worth a map icon: a letter in a coloured disc, like GTA's blips.</summary>
        struct Place
        {
            public Vector2 At;
            public string Glyph, Css, Name;
        }

        readonly List<Place> places = new List<Place>();

        void CollectPlaces()
        {
            places.Clear();
            places.Add(new Place { At = game.PlayerBase.center, Glyph = "C", Css = "icon--home", Name = "Mamma's basso" });
            foreach (var pr in game.Properties.Owned)
                if (!pr.Rect.Overlaps(game.PlayerBase)) places.Add(new Place { At = pr.Rect.center, Glyph = "C", Css = "icon--home", Name = pr.Name });
            foreach (var pr in game.Properties.All)
            {
                if (!FogOfWar.HasExplored(pr.Door)) continue;
                if (pr.Name == "Ospedale") places.Add(new Place { At = pr.Rect.center, Glyph = "H", Css = "icon--hospital", Name = "Ospedale" });
                else if (pr.Name == "The parish church") places.Add(new Place { At = pr.Rect.center, Glyph = "†", Css = "icon--church", Name = "Church" });
            }
            foreach (var b in game.Org.Businesses)
            {
                if (!FogOfWar.HasExplored(b.Cell) || b.Status == BusinessStatus.Closed) continue;
                var (g, css, what) = b.Kind switch
                {
                    ShopKind.Pizzeria => ("P", "icon--pizza", "Pizzeria"),
                    ShopKind.Bar => ("B", "icon--bar", "Bar"),
                    ShopKind.Tabaccheria => ("T", "icon--tabacchi", "Tabaccaio"),
                    ShopKind.Salumeria => ("S", "icon--salumeria", "Salumeria"),
                    ShopKind.Farmacia => ("+", "icon--farmacia", "Farmacia"),
                    ShopKind.Macelleria => ("M", "icon--macelleria", "Macelleria"),
                    ShopKind.Mobili => ("F", "icon--mobili", "Mobilificio (furniture)"),
                    ShopKind.Lusso => ("◆", "icon--lusso", "Gioielleria (luxury)"),
                    _ => ("·", "icon--shop", "Shop"),
                };
                places.Add(new Place { At = Mover.CellCenter(b.Cell), Glyph = g, Css = css, Name = $"{what} · {b.Name}" });
            }
            foreach (var c in game.Contacts.All)
                if (game.Contacts.Visible(c, game))
                    places.Add(new Place { At = c.Position, Glyph = Initial(c.Name),
                        Css = game.Contacts.HasWork(c, game) ? "icon--work" : "icon--contact", Name = $"{c.Name} · {c.Hours}" });
        }

        // "Don Peppe 'o Strozzino" -> P, "'O Zio Totonno" -> T
        static string Initial(string name)
        {
            foreach (var w in name.Split(' '))
            {
                string t = w.Trim('\'');
                if (t.Length == 0 || t == "O" || t == "o" || t == "Don" || t == "Zì" || t == "Zio") continue;
                return t.Substring(0, 1).ToUpper();
            }
            return "?";
        }

        static Label NextIcon(List<Label> pool, ref int used, VisualElement parent)
        {
            if (used >= pool.Count)
            {
                var l = Ui.Text("", parent, "map-icon");
                l.pickingMode = PickingMode.Ignore;
                pool.Add(l);
            }
            var e = pool[used++];
            e.style.display = DisplayStyle.Flex;
            return e;
        }

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
            pinMarker = Ui.Text("●", worldLayer, "goal-marker", "pin-marker");
            pinMarker.pickingMode = PickingMode.Ignore;

            mapDim = Ui.Box("dim").Into(modalLayer);
            var panel = Ui.Box("panel", "bigmap").Into(mapDim);
            var head = Ui.Row(panel);
            Ui.Text("THE RIONE", head, "title", "gold");
            Ui.Text("   click to set a waypoint · click it again to clear", head, "small", "muted");
            Ui.Box("spacer").Into(head);
            Ui.Btn("Close  [M]", ToggleMap, head, "btn--small");
            var mapBody = Ui.Row(panel);
            mapBody.style.alignItems = Align.FlexStart;
            bigImage = Ui.Box("bigmap__image").Into(mapBody);
            bigLayer = Ui.Box("layer").Into(bigImage);
            bigLayer.pickingMode = PickingMode.Ignore;
            // The legend: what each icon means. Hover an icon on the map for its name.
            var legend = Ui.Box("map-legend").Into(mapBody);
            Ui.Text("LEGEND", legend, "section");
            foreach (var (glyph, cls, text) in new[]
            {
                ("C", "icon--home", "Home"), ("P", "icon--pizza", "Pizzeria"), ("B", "icon--bar", "Bar"), ("T", "icon--tabacchi", "Tabaccaio"),
                ("S", "icon--salumeria", "Salumeria"), ("+", "icon--farmacia", "Farmacia"), ("M", "icon--macelleria", "Macelleria"),
                ("F", "icon--mobili", "Furniture shop"), ("◆", "icon--lusso", "Jeweller"),
                ("H", "icon--hospital", "Hospital"), ("†", "icon--church", "Church"), ("A", "icon--work", "Someone with work for you"),
                ("A", "icon--contact", "Someone you know"),
            })
            {
                var row = Ui.Row(legend);
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 3;
                var ic = Ui.Text(glyph, row, "map-icon", "map-icon--legend", cls);
                Ui.Text(text, row, "small");
            }
            foreach (var (cls, text) in new[] { ("dot--you", "You"), ("dot--goal", "Objective"), ("dot--pin", "Your pin"), ("dot--crew", "Your crew"), ("dot--piazza", "Piazza"), ("dot--police", "Police in sight") })
            {
                var row = Ui.Row(legend);
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 3;
                var d = Ui.Box("dot", cls).Into(row);
                d.style.position = Position.Relative;
                d.style.marginLeft = 4;
                d.style.marginRight = 10;
                Ui.Text(text, row, "small");
            }
            Ui.Text("Hover an icon for its name and hours. Click the map to drop a pin.", legend, "tiny").style.marginTop = 6;
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

            goal = Objectives.Current(game, null);
            if (Time.frameCount % 15 == 0 || places.Count == 0) CollectPlaces();
            // A pin you reach is done.
            if (waypoint.HasValue && game.Player != null && (game.Player.Position - waypoint.Value).sqrMagnitude < 1.2f * 1.2f) waypoint = null;
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
            miniIconsUsed = 0;
            foreach (var pl in places)
            {
                var pos = Mini(pl.At);
                if (pos.x < 6 || pos.y < 6 || pos.x > MiniW - 6 || pos.y > MiniH - 6) continue;
                var ic = NextIcon(miniIcons, ref miniIconsUsed, miniLayer);
                ic.ClearClassList();
                ic.AddToClassList("map-icon");
                ic.AddToClassList(pl.Css);
                ic.text = pl.Glyph;
                ic.style.left = pos.x;
                ic.style.top = pos.y;
            }
            for (int i = miniIconsUsed; i < miniIcons.Count; i++) miniIcons[i].style.display = DisplayStyle.None;
            if (waypoint.HasValue) MiniDot(waypoint.Value, "dot--pin", clamp: true);
            if (goal.Target.HasValue) MiniDot(goal.Target.Value, "dot--goal", clamp: true);
            if (player != null) MiniDot(player.Position, "dot--you");
            for (int i = miniDotsUsed; i < miniDots.Count; i++) miniDots[i].style.display = DisplayStyle.None;

            // The objective line.
            string dist = goal.Target.HasValue && player != null ? $"  ·  {Mathf.RoundToInt((goal.Target.Value - player.Position).magnitude * 3f)} m" : "";
            goalLabel.text = string.IsNullOrEmpty(goal.Text) ? "" : $"<color=#E8C26A>▸</color> {goal.Text}{dist}";
            goalLabel.Show(!string.IsNullOrEmpty(goal.Text));

            // The markers in the world, or arrows at the edge of the screen pointing to them.
            UpdateMarker(goalMarker, goal.Target, player, "◆");
            UpdateMarker(pinMarker, waypoint, player, "●");

            if (mapOpen) UpdateBigMap(player);
        }

        void Markers(System.Action<Vector2, string, bool> dot)
        {
            foreach (var p in game.Pawns.Crew)
                if (p.IsPresent && !p.Controlled) dot(p.Position, "dot--crew", false);
            foreach (var sq in game.Org.Squares) dot(Mover.CellCenter(sq.Cell), "dot--piazza", false);
            foreach (var n in game.Npcs.All)
                if (n.Kind == NpcKind.Police && !n.Despawned && FogOfWar.CanSee(n.Mover.Position)) dot(n.Mover.Position, "dot--police", false);
        }

        void UpdateMarker(Label goalMarker, Vector2? where, Pawn player, string glyph)
        {
            if (!where.HasValue || player == null || root.panel == null)
            {
                goalMarker.Show(false);
                return;
            }
            goalMarker.Show(true);
            var target = where.Value;
            var v = cam.WorldToViewportPoint(new Vector3(target.x, target.y, 0f));
            bool onScreen = v.x > 0.03f && v.x < 0.97f && v.y > 0.08f && v.y < 0.9f;
            var size = root.layout.size;
            if (onScreen)
            {
                var pos = WorldToPanel(target + Vector2.up * 0.9f);
                goalMarker.text = glyph;
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
            bigLabelsUsed = 0;
            void BigLabel(Vector2 w, string text, string cls = null)
            {
                if (bigLabelsUsed >= bigLabels.Count)
                {
                    var l = Ui.Text("", bigLayer, "map-label");
                    l.pickingMode = PickingMode.Ignore;
                    bigLabels.Add(l);
                }
                var label = bigLabels[bigLabelsUsed++];
                label.style.display = DisplayStyle.Flex;
                label.text = text;
                label.style.left = w.x * bigScale + 6;
                label.style.top = (map.Height - w.y) * bigScale - 7;
            }
            // Every place you know, with its icon and name: people, shops, home.
            bigIconsUsed = 0;
            foreach (var pl in places)
            {
                var ic = NextIcon(bigIcons, ref bigIconsUsed, bigLayer);
                ic.ClearClassList();
                ic.AddToClassList("map-icon");
                ic.AddToClassList("map-icon--big");
                ic.AddToClassList(pl.Css);
                ic.text = pl.Glyph;
                ic.style.left = pl.At.x * bigScale;
                ic.style.top = (map.Height - pl.At.y) * bigScale;
                ic.userData = pl.Name;
                if (ic.pickingMode != PickingMode.Position)
                {
                    ic.pickingMode = PickingMode.Position;
                    var icon = ic;
                    Tip(icon, () => icon.userData as string);
                }
            }
            for (int i = bigIconsUsed; i < bigIcons.Count; i++) bigIcons[i].style.display = DisplayStyle.None;
            if (waypoint.HasValue) BigDot(waypoint.Value, "dot--pin", false);
            if (goal.Target.HasValue) BigDot(goal.Target.Value, "dot--goal", false);
            if (player != null) BigDot(player.Position, "dot--you", false);
            for (int i = bigDotsUsed; i < bigDots.Count; i++) bigDots[i].style.display = DisplayStyle.None;
            for (int i = bigLabelsUsed; i < bigLabels.Count; i++) bigLabels[i].style.display = DisplayStyle.None;
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
