using System.Collections.Generic;
using OSistema.World;
using UnityEngine;

namespace OSistema.Views
{
    /// <summary>
    /// Furniture, fittings, street props and parked vehicles that have painted art, drawn as sprites
    /// over the map at full detail. Kept in step with the map as things are placed, moved or removed.
    /// </summary>
    public sealed class PropLayer : MonoBehaviour
    {
        Map map;
        readonly Dictionary<int, SpriteRenderer> props = new Dictionary<int, SpriteRenderer>();
        readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();
        readonly HashSet<int> pending = new HashSet<int>();
        readonly Dictionary<int, float> heights = new Dictionary<int, float>();
        Vector2 sunDrawn;
        float sunStrengthDrawn = -1f;

        public void Init(Map m)
        {
            map = m;
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                Refresh(x, y);
            map.CellChanged += OnChanged;
        }

        void OnDestroy()
        {
            if (map != null) map.CellChanged -= OnChanged;
        }

        void OnChanged(int x, int y) => pending.Add(map.Index(x, y));

        // ---------- Doors: painted, facing the street, open while someone stands in them ----------

        readonly HashSet<int> doorCells = new HashSet<int>();
        readonly HashSet<int> openDoors = new HashSet<int>();
        readonly HashSet<int> occupied = new HashSet<int>();
        float doorCheck;

        bool Indoorish(int x, int y)
        {
            if (!map.InBounds(x, y)) return false;
            var t = map.GetTerrain(x, y);
            return t == TerrainType.Floor || t == TerrainType.Marble;
        }

        bool WallAt(int x, int y) => map.InBounds(x, y) && (map.GetBuilding(x, y) == BuildingDefs.Wall || map.GetBuilding(x, y) == BuildingDefs.Door);

        /// <summary>Which painted door: the street door of a shop, a palazzo, a basso, the family's own, a church; or one inside.</summary>
        Sprite DoorSprite(int x, int y, bool open)
        {
            bool horizontal = WallAt(x - 1, y) || WallAt(x + 1, y);
            bool inside = horizontal ? Indoorish(x, y - 1) && Indoorish(x, y + 1) : Indoorish(x - 1, y) && Indoorish(x + 1, y);
            float h = TilePainter.Noise(x, y, 131);
            int style;
            if (inside) style = h < 0.5f ? 4 : 5;
            else
            {
                var game = OSistema.Core.GameRunner.Current;
                var cell = new Vector2Int(x, y);
                var prop = game?.Properties.All.Find(p => p.Door == cell);
                bool family = game != null && (game.PlayerBase.Contains(cell) || (prop != null && prop.OwnedByPlayer));
                if (family) style = 6;
                else if (prop == null) style = h < 0.5f ? 0 : 1;
                else if (prop.Kind == PropertyKind.Shop) style = h < 0.7f ? 3 : 2;
                else if (prop.Kind == PropertyKind.Civic) style = 7;
                else style = h < 0.45f ? 0 : h < 0.75f ? 1 : 5;
            }
            return PropArt.Load($"Doors/door_{style + (open ? 8 : 0):00}");
        }

        /// <summary>Doors turn so the threshold (the bottom of the painting) faces out, onto the street.</summary>
        float DoorAngle(int x, int y)
        {
            bool horizontal = WallAt(x - 1, y) || WallAt(x + 1, y);
            if (horizontal) return Indoorish(x, y - 1) && !Indoorish(x, y + 1) ? 180f : 0f;
            return Indoorish(x - 1, y) && !Indoorish(x + 1, y) ? 90f : -90f;
        }

        /// <summary>Anyone standing in a doorway: that door is open.</summary>
        void UpdateDoors()
        {
            doorCheck -= Time.deltaTime;
            if (doorCheck > 0f || doorCells.Count == 0) return;
            doorCheck = 0.12f;
            var game = OSistema.Core.GameRunner.Current;
            if (game == null) return;
            occupied.Clear();
            foreach (var p in game.Pawns.All) if (!p.Dead && p.IsPresent) Mark(p.Position);
            foreach (var n in game.Npcs.All) if (!n.Despawned) Mark(n.Mover.Position);
            foreach (int i in doorCells)
            {
                bool open = occupied.Contains(i);
                if (open == openDoors.Contains(i)) continue;
                if (open) openDoors.Add(i); else openDoors.Remove(i);
                pending.Add(i);
            }

            void Mark(Vector2 pos)
            {
                int x = Mathf.FloorToInt(pos.x), y = Mathf.FloorToInt(pos.y);
                // In the doorway, or a step either side of it.
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!map.InBounds(x + dx, y + dy)) continue;
                    if (dx != 0 && dy != 0) continue;
                    var c = new Vector2(x + dx + 0.5f, y + dy + 0.5f);
                    if ((c - pos).sqrMagnitude < 0.75f * 0.75f) occupied.Add(map.Index(x + dx, y + dy));
                }
            }
        }

        void LateUpdate()
        {
            if (map == null) return;
            UpdateDoors();
            if (pending.Count > 0)
            {
                foreach (int i in pending) Refresh(i % map.Width, i / map.Width);
                pending.Clear();
            }
            // The sun has moved enough to notice: every shadow follows.
            if ((Sun.Offset - sunDrawn).sqrMagnitude > 0.0004f || Mathf.Abs(Sun.Strength - sunStrengthDrawn) > 0.02f)
            {
                sunDrawn = Sun.Offset;
                sunStrengthDrawn = Sun.Strength;
                foreach (var kv in props) PlaceShadow(kv.Value, heights[kv.Key]);
            }
        }

        /// <summary>How tall a thing stands, in cells, for the length of its shadow.</summary>
        static float HeightOf(BuildingDef def) => def.Id switch
        {
            "parked_car" or "market_stall" or "wardrobe" or "fridge" or "fountain" => 0.45f,
            "parked_scooter" or "bicycle" or "bin" or "potted_plants" or "plant" => 0.3f,
            "rug" => 0f,
            _ => 0.2f,
        };

        void PlaceShadow(SpriteRenderer sr, float h)
        {
            var sh = sr.transform.GetChild(0);
            // A little contact shadow even with no sun, so things never float.
            sh.position = sr.transform.position + (Vector3)(new Vector2(0.04f, -0.04f) + Sun.Offset * h);
            sh.GetComponent<SpriteRenderer>().color = new Color(0f, 0f, 0f, 0.22f + 0.2f * Sun.Strength);
        }

        void Refresh(int x, int y)
        {
            int i = map.Index(x, y);
            var def = map.GetBuilding(x, y);
            bool door = def == BuildingDefs.Door;
            if (door) doorCells.Add(i); else { doorCells.Remove(i); openDoors.Remove(i); }
            var sprite = door ? DoorSprite(x, y, openDoors.Contains(i)) : def != null ? PropArt.For(def, x, y) : null;
            props.TryGetValue(i, out var sr);
            if (sprite == null)
            {
                if (sr != null)
                {
                    sr.gameObject.SetActive(false);
                    spare.Push(sr);
                    props.Remove(i);
                }
                return;
            }
            if (sr == null)
            {
                sr = spare.Count > 0 ? spare.Pop() : Create();
                sr.gameObject.SetActive(true);
                props[i] = sr;
            }
            sr.sprite = sprite;
            bool vehicle = def == BuildingDefs.ParkedCar || def == BuildingDefs.ParkedScooter;
            sr.sortingOrder = vehicle ? 3 : def == BuildingDefs.Rug ? 1 : 2;
            var t = sr.transform;
            if (Footprint.IsBig(def))
            {
                // Fill the cells it covers, keeping the painting's proportions within reason.
                int rot = map.GetRotation(x, y);
                t.localScale = PropArt.FitScale(sprite, def.Size);
                var c = Footprint.Center(def, new Vector2Int(x, y), rot);
                t.localPosition = new Vector3(c.x, c.y, 0f);
                t.localRotation = Quaternion.Euler(0f, 0f, rot * 90f);
            }
            else
            {
                float size = PropArt.SizeOf(def);
                t.localScale = new Vector3(size, size, 1f);
                t.localPosition = new Vector3(x + 0.5f, y + 0.5f, 0f);
                t.localRotation = Quaternion.Euler(0f, 0f, Angle(def, x, y));
            }
            var shadow = t.GetChild(0).GetComponent<SpriteRenderer>();
            shadow.enabled = def != BuildingDefs.Rug && def != BuildingDefs.Door;
            heights[i] = HeightOf(def);
            PlaceShadow(sr, heights[i]);
            shadow.transform.localScale = vehicle ? new Vector3(0.62f, 0.95f, 1f) : new Vector3(0.95f, 0.95f, 1f);
        }

        float Angle(BuildingDef def, int x, int y)
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            if (def == BuildingDefs.ParkedCar)
            {
                // Along the kerb: nose up or down a vertical street, left or right along a horizontal one.
                bool vertical = IsSidewalk(x - 1, y) || IsSidewalk(x + 1, y);
                return (vertical ? 0f : 90f) + ((h & 1) == 0 ? 0f : 180f);
            }
            if (def == BuildingDefs.ParkedScooter)
                return (h % 4) * 90f + ((h >> 3) % 31) - 15f;   // parked any old how
            if (def == BuildingDefs.Door) return DoorAngle(x, y);
            if (def.Generated) return ((h >> 2) % 4) * 90f * (def == BuildingDefs.Fountain ? 1f : 0f);
            return map.GetRotation(x, y) * 90f;
        }

        bool IsSidewalk(int x, int y) => map.InBounds(x, y) && map.GetTerrain(x, y) == TerrainType.Sidewalk;

        SpriteRenderer Create()
        {
            var go = new GameObject("Prop");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteFactory.LitMaterial;
            var sh = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            sh.transform.SetParent(go.transform, false);
            sh.transform.localPosition = new Vector3(0.05f, -0.06f, 0f);
            sh.sprite = SpriteFactory.SoftShadow;
            sh.sharedMaterial = SpriteFactory.LitMaterial;
            sh.color = new Color(0f, 0f, 0f, 0.38f);
            sh.sortingOrder = 1;
            return sr;
        }

        public static bool Hides(BuildingDef def) => PropArt.Has(def);
    }
}
