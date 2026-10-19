using System.Collections.Generic;
using UnityEngine;

namespace OSistema.World
{
    /// <summary>
    /// Furnishes a home or a shop the way people would: each room is read from its door. The wall
    /// opposite is the back wall (the bed, the oven, the shelves of stock); a shop's counter stands
    /// in front of it with room behind for whoever serves; tables go in the middle with chairs round
    /// them; the kitchen runs along one wall; pieces face into the room (the painted art faces the
    /// viewer, so a piece on the north wall reads best). Nothing goes by a door, and every placement
    /// is undone if it would cut off part of the room. The tier is the place's fortune: 0 burned or
    /// destitute, 1 poor or squeezed by the pizzo, 2 ordinary, 3 doing well.
    /// </summary>
    public static class RoomPlanner
    {
        const int N = 0, E = 1, S = 2, W = 3;

        // ---------- Entry points ----------

        /// <summary>A building's own seed: the same building is always laid out the same way for the same fortune.</summary>
        public static int SeedOf(Property p) => p.Rect.x * 7919 + p.Rect.y * 104729 + p.Rect.width * 31 + p.Rect.height;

        /// <summary>A home someone else lives in. Partitioned: a living room by the door, a bedroom behind.</summary>
        public static void FurnishHome(Map map, Property prop, int tier, int seed, ICollection<Vector2Int> occupied = null)
        {
            Clear(map, prop, null);
            var rooms = Rooms(map, prop);
            foreach (var room in rooms)
            {
                var p = new Planner(map, room, new System.Random(seed * 31 + room.R.x * 7 + room.R.y), occupied);
                if (rooms.Count == 1) Basso(p, tier);
                else if (room.Front) Living(p, tier);
                else Bedroom(p, tier);
            }
        }

        /// <summary>
        /// Fits out a shop for its trade and fortune, and returns where its counter stands (null if it
        /// wouldn't go). Burned (tier 0): a charred counter, wreckage and blackened shelves.
        /// </summary>
        public static Vector2Int? FitShop(Map map, Property prop, BuildingDef counter, int tier, int seed, ICollection<Vector2Int> occupied = null)
        {
            Clear(map, prop, null);
            Vector2Int? at = null;
            var rooms = Rooms(map, prop);
            foreach (var room in rooms)
            {
                var p = new Planner(map, room, new System.Random(seed * 31 + room.R.x * 7 + room.R.y), occupied);
                if (room.Front || rooms.Count == 1) at = ShopFloor(p, counter, tier);
                else Storeroom(p, counter, tier);
            }
            return at;
        }

        /// <summary>The family's own basso as it starts: three beds, the stove and the pantry, a table, the Madonna.</summary>
        public static void FurnishFamily(Map map, Property prop, int seed)
        {
            Clear(map, prop, null);
            foreach (var room in Rooms(map, prop))
            {
                var p = new Planner(map, room, new System.Random(seed), null);
                for (int i = 0; i < 3; i++) p.Wall(Back(p), BuildingDefs.Bed, use: true);
                p.Wall(SideOf(p), BuildingDefs.Stove, use: true);
                p.Run(BuildingDefs.Pantry, use: true);
                p.Centre(BuildingDefs.Table, use: true);
                p.Wall(OtherSide(p), BuildingDefs.HomeShrine, use: true);
            }
        }

        /// <summary>
        /// As the family rises, the basso fills up: only ever adds, into free space, what isn't there
        /// yet; never moves or takes away what the family put down themselves.
        /// </summary>
        public static int DressFamily(Map map, Property prop, int rank, int seed, ICollection<Vector2Int> occupied)
        {
            int added = 0;
            foreach (var room in Rooms(map, prop))
            {
                var p = new Planner(map, room, new System.Random(seed + rank * 101), occupied) { AddOnly = true };
                if (rank >= 1) { added += p.Once(BuildingDefs.Television, () => p.Wall(SideOf(p), BuildingDefs.Television)); added += p.Once(BuildingDefs.Fridge, () => p.Wall(Back(p), BuildingDefs.Fridge, use: true)); }
                if (rank >= 1) added += p.Once(BuildingDefs.Sofa, () => p.Wall(OtherSide(p), BuildingDefs.Sofa));
                if (rank >= 2) { added += p.Once(BuildingDefs.WashingMachine, () => p.Wall(Back(p), BuildingDefs.WashingMachine)); added += p.Once(BuildingDefs.Plant, () => p.Corner(BuildingDefs.Plant)); }
                if (rank >= 2) added += p.Once(BuildingDefs.Rug, () => p.Centre(BuildingDefs.Rug));
                if (rank >= 3) { added += p.Once(BuildingDefs.Sideboard, () => p.Wall(SideOf(p), BuildingDefs.Sideboard)); added += p.Once(BuildingDefs.Armchair, () => p.Wall(OtherSide(p), BuildingDefs.Armchair)); }
                if (rank >= 3) { added += p.Once(BuildingDefs.FloorLamp, () => p.Corner(BuildingDefs.FloorLamp)); added += p.Once(BuildingDefs.PottedPalm, () => p.Corner(BuildingDefs.PottedPalm)); }
                if (rank >= 4) { added += p.Once(BuildingDefs.Bookcase, () => p.Wall(SideOf(p), BuildingDefs.Bookcase)); added += p.Once(BuildingDefs.Dresser, () => p.Wall(OtherSide(p), BuildingDefs.Dresser)); }
            }
            return added;
        }

        // ---------- Rooms ----------

        static int Back(Planner p) => (p.Room.DoorSide + 2) % 4;
        static int SideOf(Planner p) => p.SideA;
        static int OtherSide(Planner p) => (p.SideA + 2) % 4;

        static void Basso(Planner p, int t)
        {
            // One room for everything: the bed against the back wall, the kitchen down one side,
            // the table in the middle, the sofa and the TV if there's money for them.
            if (t >= 2 && p.Wall(Back(p), BuildingDefs.DoubleBed, use: true)) { p.Beside(BuildingDefs.Nightstand); }
            else { p.Wall(Back(p), BuildingDefs.Bed, use: true); if (t >= 1) p.Wall(Back(p), BuildingDefs.Bed, use: true); }
            if (t >= 1) p.Wall(Back(p), BuildingDefs.Wardrobe);
            p.Wall(SideOf(p), BuildingDefs.Stove, use: true);
            p.Run(BuildingDefs.KitchenCounter);
            if (t >= 1) p.Run(BuildingDefs.Fridge);
            p.Run(BuildingDefs.Pantry);
            if (p.Centre(t >= 1 ? BuildingDefs.CheckedTable : BuildingDefs.Table, use: true) && t >= 1) p.Chairs(t >= 2 ? 3 : 2);
            if (t >= 1) p.Wall(OtherSide(p), BuildingDefs.Television);
            if (t >= 2) p.Wall(OtherSide(p), BuildingDefs.Sofa);
            if (p.Rng.NextDouble() < 0.7) p.Wall(OtherSide(p), BuildingDefs.HomeShrine);
            if (t >= 2) p.Corner(BuildingDefs.Plant);
            if (t >= 3) { p.Corner(BuildingDefs.FloorLamp); p.Wall(Back(p), BuildingDefs.Dresser); }
            if (t >= 1) p.WallFill(t >= 2 ? new[] { BuildingDefs.Wardrobe, BuildingDefs.Bookcase, BuildingDefs.Plant } : new[] { BuildingDefs.Bed, BuildingDefs.Pantry }, t == 1 ? 0.3f : 0.4f);
        }

        static void Living(Planner p, int t)
        {
            p.Wall(Back(p), BuildingDefs.KitchenCounter);
            p.Run(BuildingDefs.Stove, use: true);
            if (t >= 1) p.Run(BuildingDefs.Fridge);
            p.Run(BuildingDefs.Pantry);
            if (t >= 2 && p.Rng.NextDouble() < 0.7) p.Run(BuildingDefs.WashingMachine);
            if (p.Centre(t >= 1 ? BuildingDefs.CheckedTable : BuildingDefs.Table, use: true) && t >= 1) p.Chairs(t >= 2 ? 3 : 2);
            if (t >= 1) { p.Wall(SideOf(p), BuildingDefs.Sofa); p.Wall(OtherSide(p), BuildingDefs.Television); }
            if (t >= 2) p.Wall(SideOf(p), BuildingDefs.Sideboard);
            if (t >= 3) p.Wall(OtherSide(p), BuildingDefs.Armchair);
            if (p.Rng.NextDouble() < 0.6) p.Wall(OtherSide(p), BuildingDefs.HomeShrine);
            if (t >= 1) p.Corner(BuildingDefs.Plant);
            if (t >= 3) p.Corner(BuildingDefs.PottedPalm);
            if (t >= 1) p.WallFill(t >= 2 ? new[] { BuildingDefs.Bookcase, BuildingDefs.Plant, BuildingDefs.Armchair } : new[] { BuildingDefs.Pantry, BuildingDefs.Plant }, t == 1 ? 0.3f : 0.4f);
        }

        static void Bedroom(Planner p, int t)
        {
            if (t >= 1 && p.Wall(Back(p), BuildingDefs.DoubleBed, use: true))
            {
                p.Beside(BuildingDefs.Nightstand);
                if (t >= 2) p.Beside(BuildingDefs.Nightstand);
            }
            else p.Wall(Back(p), BuildingDefs.Bed, use: true);
            if (p.Rng.NextDouble() < 0.6) p.Wall(Back(p), BuildingDefs.Bed, use: true);   // the children's
            p.Wall(SideOf(p), BuildingDefs.Wardrobe);
            if (t >= 2) p.Wall(OtherSide(p), BuildingDefs.Dresser);
            if (t >= 3) p.Wall(SideOf(p), BuildingDefs.Bookcase);
            if (p.Rng.NextDouble() < 0.4) p.Wall(OtherSide(p), BuildingDefs.HomeShrine);
            if (t >= 1) p.Centre(BuildingDefs.Rug);
            if (t >= 2) p.Corner(BuildingDefs.FloorLamp);
            if (t >= 3) p.Corner(BuildingDefs.Plant);
            if (t >= 1) p.WallFill(new[] { BuildingDefs.Bed, BuildingDefs.Nightstand, BuildingDefs.Wardrobe }, t == 1 ? 0.3f : 0.4f);
        }

        sealed class Trade
        {
            public BuildingDef[] Back;      // stock along the back wall, in order of importance
            public BuildingDef[] Run;       // beside the counter
            public BuildingDef[] Floor;     // out where the customers are
            public BuildingDef[] Side;      // along the side walls
            public BuildingDef Stool;       // in front of the counter (a bar)
            public BuildingDef[] Store;     // in the back room
            public BuildingDef[] Fill;      // stock to line the walls of a big shop with (never a second oven)
            public bool FloorRepeats;       // the floor pieces fill the room (tables, scooters), not one of each
        }

        static Trade TradeFor(BuildingDef counter)
        {
            if (counter == BuildingDefs.PizzeriaCounter) return new Trade
            {
                FloorRepeats = true,
                Fill = new[] { BuildingDefs.FlourSacks, BuildingDefs.WineRack, BuildingDefs.ShopShelf },
                Back = new[] { BuildingDefs.PizzaOven, BuildingDefs.Fridge, BuildingDefs.FlourSacks, BuildingDefs.WineRack },
                Run = new[] { BuildingDefs.Till }, Floor = new[] { BuildingDefs.CafeTable, BuildingDefs.CafeTable, BuildingDefs.CafeTable, BuildingDefs.CafeTable, BuildingDefs.CafeTable, BuildingDefs.CafeTable },
                Side = new BuildingDef[0], Store = new[] { BuildingDefs.FlourSacks, BuildingDefs.Crates, BuildingDefs.Fridge },
            };
            if (counter == BuildingDefs.BarCounter) return new Trade
            {
                FloorRepeats = true,
                Fill = new[] { BuildingDefs.WineRack, BuildingDefs.ShopShelf, BuildingDefs.DrinksFridge },
                Back = new[] { BuildingDefs.WineRack, BuildingDefs.DrinksFridge, BuildingDefs.ShopShelf, BuildingDefs.WineRack },
                Run = new[] { BuildingDefs.Till, BuildingDefs.IceCream }, Floor = new[] { BuildingDefs.CafeTable, BuildingDefs.CafeTable, BuildingDefs.CafeTable, BuildingDefs.CafeTable },
                Side = new[] { BuildingDefs.SlotMachine, BuildingDefs.Television }, Stool = BuildingDefs.BarStool,
                Store = new[] { BuildingDefs.Crates, BuildingDefs.DrinksFridge },
            };
            if (counter == BuildingDefs.SalumeriaCounter) return new Trade
            {
                Fill = new[] { BuildingDefs.Pantry, BuildingDefs.ShopShelf, BuildingDefs.BreadRack, BuildingDefs.WineRack },
                Back = new[] { BuildingDefs.Pantry, BuildingDefs.ShopShelf, BuildingDefs.BreadRack, BuildingDefs.WineRack, BuildingDefs.Pantry },
                Run = new[] { BuildingDefs.GlassVitrine, BuildingDefs.Till }, Floor = new[] { BuildingDefs.FruitStall, BuildingDefs.FlourSacks, BuildingDefs.IceCream },
                Side = new[] { BuildingDefs.ShopShelf }, Store = new[] { BuildingDefs.Crates, BuildingDefs.FlourSacks, BuildingDefs.Fridge },
            };
            if (counter == BuildingDefs.TabaccheriaCounter) return new Trade
            {
                Fill = new[] { BuildingDefs.CigStock, BuildingDefs.ShopShelf },
                Back = new[] { BuildingDefs.CigStock, BuildingDefs.ShopShelf, BuildingDefs.CigStock },
                Run = new[] { BuildingDefs.Till }, Floor = new[] { BuildingDefs.Newsstand },
                Side = new[] { BuildingDefs.SlotMachine, BuildingDefs.SlotMachine }, Store = new[] { BuildingDefs.CigStock, BuildingDefs.Crates },
            };
            if (counter == BuildingDefs.FarmaciaCounter) return new Trade
            {
                Fill = new[] { BuildingDefs.PharmacyCabinet },
                Back = new[] { BuildingDefs.PharmacyCabinet, BuildingDefs.PharmacyCabinet, BuildingDefs.PharmacyCabinet },
                Run = new[] { BuildingDefs.Till }, Floor = new[] { BuildingDefs.ShopShelf },
                Side = new[] { BuildingDefs.PharmacyCabinet, BuildingDefs.ShopShelf }, Store = new[] { BuildingDefs.Crates, BuildingDefs.ShopShelf },
            };
            if (counter == BuildingDefs.MacelleriaCounter) return new Trade
            {
                Fill = new[] { BuildingDefs.Fridge, BuildingDefs.KitchenCounter },
                Back = new[] { BuildingDefs.Fridge, BuildingDefs.KitchenCounter, BuildingDefs.Fridge },
                Run = new[] { BuildingDefs.Till }, Floor = new BuildingDef[0],
                Side = new[] { BuildingDefs.Fridge }, Store = new[] { BuildingDefs.Fridge, BuildingDefs.Crates },
            };
            if (counter == BuildingDefs.MobiliCounter) return new Trade
            {
                FloorRepeats = true,
                Fill = new[] { BuildingDefs.Wardrobe, BuildingDefs.Bookcase, BuildingDefs.Dresser, BuildingDefs.Sideboard },
                Back = new[] { BuildingDefs.Wardrobe, BuildingDefs.Bookcase, BuildingDefs.Dresser },
                Run = new[] { BuildingDefs.Till }, Floor = new[] { BuildingDefs.Sofa, BuildingDefs.Armchair, BuildingDefs.Table, BuildingDefs.GreenSofa, BuildingDefs.DoubleBed, BuildingDefs.FloorLamp },
                Side = new[] { BuildingDefs.Sideboard, BuildingDefs.Wardrobe }, Store = new[] { BuildingDefs.Crates, BuildingDefs.Crates },
            };
            if (counter == BuildingDefs.LussoCounter) return new Trade
            {
                FloorRepeats = true,
                Fill = new[] { BuildingDefs.ClothesRail, BuildingDefs.ClothesRack },
                Back = new[] { BuildingDefs.Safe, BuildingDefs.ClothesRail, BuildingDefs.ClothesRail },
                Run = new[] { BuildingDefs.JewelCase }, Floor = new[] { BuildingDefs.JewelCase, BuildingDefs.ClothesRack, BuildingDefs.Armchair },
                Side = new[] { BuildingDefs.ClothesRack }, Store = new[] { BuildingDefs.Safe, BuildingDefs.Crates },
            };
            if (counter == BuildingDefs.MotoCounter) return new Trade
            {
                FloorRepeats = true,
                Fill = new[] { BuildingDefs.TyreStack, BuildingDefs.PartsBench, BuildingDefs.Crates },
                Back = new[] { BuildingDefs.PartsBench, BuildingDefs.TyreStack, BuildingDefs.Crates },
                Run = new[] { BuildingDefs.Till }, Floor = new[] { BuildingDefs.ParkedScooter, BuildingDefs.ParkedScooter, BuildingDefs.ParkedScooter, BuildingDefs.ParkedScooter },
                Side = new[] { BuildingDefs.TyreStack }, Store = new[] { BuildingDefs.TyreStack, BuildingDefs.PartsBench },
            };
            return new Trade
            {
                Fill = new[] { BuildingDefs.ShopShelf },
                Back = new[] { BuildingDefs.ShopShelf, BuildingDefs.ShopShelf }, Run = new[] { BuildingDefs.Till },
                Floor = new BuildingDef[0], Side = new BuildingDef[0], Store = new[] { BuildingDefs.Crates },
            };
        }

        /// <summary>How much of each list a shop of this fortune has.</summary>
        static int Take(int listLength, int tier, float share1 = 0.4f, float share2 = 0.7f) =>
            listLength == 0 || tier <= 0 ? 0 : Mathf.Min(listLength,
                tier == 1 ? Mathf.Max(1, Mathf.RoundToInt(listLength * share1)) : tier == 2 ? Mathf.CeilToInt(listLength * share2) : listLength);

        static Vector2Int? ShopFloor(Planner p, BuildingDef counter, int t)
        {
            var trade = TradeFor(counter);
            var at = p.Counter(t <= 0 ? BuildingDefs.BurnedShop : counter);
            if (t <= 0)
            {
                p.Wall(Back(p), BuildingDefs.BurntShelf);
                if (p.Rng.NextDouble() < 0.6) p.Wall(Back(p), BuildingDefs.BurntShelf);
                for (int i = 0; i < 2 + p.Rng.Next(2); i++) p.Floor(BuildingDefs.Charred, spaced: false);
                return at;
            }
            int back = Take(trade.Back.Length, t, 0.5f, 0.75f);
            for (int i = 0; i < back; i++) p.Wall(Back(p), trade.Back[i]);
            int run = t >= 2 ? trade.Run.Length : Mathf.Min(1, trade.Run.Length);
            for (int i = 0; i < run; i++) p.CounterRun(trade.Run[i]);
            if (trade.Stool != null) for (int i = 0; i < t + 1; i++) p.Stool(trade.Stool);
            int side = Take(trade.Side.Length, t, 0.5f, 1f);
            for (int i = 0; i < side; i++) p.Wall(i % 2 == 0 ? SideOf(p) : OtherSide(p), trade.Side[i]);
            int floor = Take(trade.Floor.Length, t);
            for (int i = 0; i < floor; i++) p.Floor(trade.Floor[i], spaced: true);
            // Then fill the room to its fortune: stock along the walls, more out on the floor.
            p.WallFill(trade.Fill, t == 1 ? 0.3f : t == 2 ? 0.5f : 0.65f);
            if (trade.FloorRepeats) p.FloorFill(trade.Floor, t == 1 ? 16 : t == 2 ? 10 : 8);
            // A shop that's doing well looks after itself: plants by the door, a palm.
            if (t >= 2) p.Corner(BuildingDefs.Plant);
            if (t >= 3) { p.Corner(BuildingDefs.PottedPalm); p.Corner(BuildingDefs.Plant); }
            return at;
        }

        static void Storeroom(Planner p, BuildingDef counter, int t)
        {
            var trade = TradeFor(counter);
            if (t <= 0) { p.Floor(BuildingDefs.Charred, spaced: false); return; }
            int n = Take(trade.Store.Length, t, 0.5f, 1f);
            for (int i = 0; i < n; i++) p.Wall(i == 0 ? Back(p) : SideOf(p), trade.Store[i]);
            if (t >= 2) p.Wall(OtherSide(p), BuildingDefs.ShopShelf);
            var pool = new List<BuildingDef>(trade.Store) { BuildingDefs.ShopShelf, BuildingDefs.Crates };
            p.WallFill(pool.ToArray(), t == 1 ? 0.3f : t == 2 ? 0.45f : 0.55f);
        }

        // ---------- Reading the building ----------

        sealed class Room
        {
            public RectInt R;
            public readonly HashSet<Vector2Int> Cells = new HashSet<Vector2Int>();
            public readonly List<Vector2Int> Doors = new List<Vector2Int>();
            public Vector2Int Door;
            public int DoorSide;
            public bool Front;      // the room the street door opens into
        }

        static bool Structural(BuildingDef b) => b == BuildingDefs.Wall || b == BuildingDefs.Door;

        /// <summary>Takes out everything but walls and doors (and whatever is in <paramref name="keep"/>).</summary>
        static void Clear(Map map, Property prop, HashSet<Vector2Int> keep)
        {
            foreach (var c in GridUtil.Cells(prop.Interior, false))
            {
                if (!map.InBounds(c) || keep != null && keep.Contains(c)) continue;
                var b = map.GetBuilding(c);
                if (b != null && !Structural(b)) Footprint.Remove(map, c, out _, out _);
            }
        }

        static List<Room> Rooms(Map map, Property prop)
        {
            var rooms = new List<Room>();
            var seen = new HashSet<Vector2Int>();
            var inner = prop.Interior;
            foreach (var start in GridUtil.Cells(inner, false))
            {
                if (seen.Contains(start) || !Inside(map, inner, start)) continue;
                var room = new Room();
                var stack = new Stack<Vector2Int>();
                stack.Push(start);
                seen.Add(start);
                int x0 = start.x, y0 = start.y, x1 = start.x, y1 = start.y;
                while (stack.Count > 0)
                {
                    var c = stack.Pop();
                    room.Cells.Add(c);
                    x0 = Mathf.Min(x0, c.x); y0 = Mathf.Min(y0, c.y); x1 = Mathf.Max(x1, c.x); y1 = Mathf.Max(y1, c.y);
                    foreach (var d in Dirs)
                    {
                        var n = c + d;
                        if (map.InBounds(n) && map.GetBuilding(n) == BuildingDefs.Door && !room.Doors.Contains(n)) room.Doors.Add(n);
                        if (seen.Contains(n) || !Inside(map, inner, n)) continue;
                        seen.Add(n);
                        stack.Push(n);
                    }
                }
                if (room.Doors.Count == 0) continue;
                room.R = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
                room.Front = room.Doors.Contains(prop.Door);
                room.Door = room.Front ? prop.Door : room.Doors[0];
                var dr = room.Door;
                room.DoorSide = dr.x < room.R.xMin ? W : dr.x >= room.R.xMax ? E : dr.y < room.R.yMin ? S : N;
                rooms.Add(room);
            }
            return rooms;
        }

        static bool Inside(Map map, RectInt inner, Vector2Int c)
        {
            if (!inner.Contains(c) || !map.InBounds(c)) return false;
            var t = map.GetTerrain(c.x, c.y);
            if (t != TerrainType.Floor && t != TerrainType.Marble) return false;
            return !Structural(map.GetBuilding(c));
        }

        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        /// <summary>The turn that makes a piece against this wall face into the room (the art faces down).</summary>
        static int RotFor(int side) => side switch { N => 0, E => 3, S => 2, _ => 1 };

        // ---------- Placing ----------

        sealed class Planner
        {
            public readonly Map Map;
            public readonly Room Room;
            public readonly System.Random Rng;
            public bool AddOnly;
            public int SideA;
            readonly HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();
            readonly List<(Vector2Int anchor, BuildingDef def, bool use)> placed = new List<(Vector2Int, BuildingDef, bool)>();
            (Vector2Int anchor, BuildingDef def, int side, int rot) last;
            readonly List<Vector2Int> counterCells = new List<Vector2Int>();
            int counterDepth = 1;

            public Planner(Map map, Room room, System.Random rng, ICollection<Vector2Int> occupied)
            {
                Map = map;
                Room = room;
                Rng = rng;
                // Keep clear: round every door, and the first steps in from it.
                foreach (var d in room.Doors)
                    foreach (var c in room.Cells)
                        if (Mathf.Abs(c.x - d.x) <= 1 && Mathf.Abs(c.y - d.y) <= 1) blocked.Add(c);
                var inward = -Dir(room.DoorSide);
                for (int k = 1; k <= 2; k++) blocked.Add(room.Door + inward * k);
                if (occupied != null) foreach (var c in occupied) if (room.Cells.Contains(c)) blocked.Add(c);
                // The longer side wall first: more room for a run.
                int back = (room.DoorSide + 2) % 4;
                int a = (back + 1) % 4, b = (back + 3) % 4;
                SideA = a == N || b == N ? N : WallLength(a) >= WallLength(b) ? a : b;
                if (rng.NextDouble() < 0.15) SideA = SideA == a ? b : a;
            }

            static Vector2Int Dir(int side) => side switch { N => Vector2Int.up, E => Vector2Int.right, S => Vector2Int.down, _ => Vector2Int.left };

            int WallLength(int side) => side == N || side == S ? Room.R.width : Room.R.height;

            /// <summary>Runs <paramref name="add"/> unless the room already has a <paramref name="def"/>.</summary>
            public int Once(BuildingDef def, System.Func<bool> add)
            {
                foreach (var c in Room.Cells) if (Map.GetBuilding(c) == def) return 0;
                return add() ? 1 : 0;
            }

            // --- Zones ---

            /// <summary>Against a wall, facing in: tries the spots from the middle of the wall outwards.</summary>
            public bool Wall(int side, BuildingDef def, bool use = false)
            {
                foreach (var (anchor, rot) in WallSpots(side, def, last.side == side && last.def != null ? last.anchor : (Vector2Int?)null, false))
                    if (TryPlace(def, anchor, rot, use)) { last = (anchor, def, side, rot); return true; }
                return false;
            }

            /// <summary>Next to the last piece, along the same wall: a kitchen run, a row of shelves.</summary>
            public bool Run(BuildingDef def, bool use = false)
            {
                if (last.def == null) return Wall(SideA, def, use);
                foreach (var (anchor, rot) in WallSpots(last.side, def, last.anchor, true))
                    if (TryPlace(def, anchor, rot, use)) { last = (anchor, def, last.side, rot); return true; }
                return Wall(last.side, def, use);
            }

            IEnumerable<(Vector2Int, int)> WallSpots(int side, BuildingDef def, Vector2Int? near, bool nearFirst)
            {
                int rot = RotFor(side);
                var t = Footprint.SizeOf(def, rot);
                var r = Room.R;
                var spots = new List<(Vector2Int, int)>();
                if (side == N || side == S)
                {
                    int y = side == N ? r.yMax - t.y : r.yMin;
                    for (int x = r.xMin; x <= r.xMax - t.x; x++) spots.Add((new Vector2Int(x, y), rot));
                }
                else
                {
                    int x = side == E ? r.xMax - t.x : r.xMin;
                    for (int y = r.yMin; y <= r.yMax - t.y; y++) spots.Add((new Vector2Int(x, y), rot));
                }
                var mid = r.center;
                if (near.HasValue && nearFirst) spots.Sort((p, q) => (p.Item1 - near.Value).sqrMagnitude.CompareTo((q.Item1 - near.Value).sqrMagnitude));
                else spots.Sort((p, q) => ((Vector2)p.Item1 - mid).sqrMagnitude.CompareTo(((Vector2)q.Item1 - mid).sqrMagnitude));
                return spots;
            }

            /// <summary>In the middle of the room, with space all round.</summary>
            public bool Centre(BuildingDef def, bool use = false)
            {
                int rot = 0;   // tables and rugs stay square to the viewer
                var cells = new List<Vector2Int>(Room.Cells);
                var mid = Room.R.center - (Vector2)Footprint.SizeOf(def, rot) * 0.5f;
                cells.Sort((p, q) => ((Vector2)p - mid).sqrMagnitude.CompareTo(((Vector2)q - mid).sqrMagnitude));
                foreach (var c in cells)
                {
                    if (!Clearance(def, c, rot, 1)) continue;
                    if (TryPlace(def, c, rot, use)) { last = (c, def, -1, rot); return true; }
                }
                return false;
            }

            /// <summary>Chairs drawn up to the last table, facing it.</summary>
            public void Chairs(int count)
            {
                if (last.def == null) return;
                var t = Footprint.SizeOf(last.def, last.rot);
                var a = last.anchor;
                var around = new List<(Vector2Int, int)>();
                for (int x = 0; x < t.x; x++)
                {
                    around.Add((new Vector2Int(a.x + x, a.y - 1), 2));        // south of it, facing north
                    around.Add((new Vector2Int(a.x + x, a.y + t.y), 0));      // north of it, facing south
                }
                for (int y = 0; y < t.y; y++)
                {
                    around.Add((new Vector2Int(a.x - 1, a.y + y), 1));
                    around.Add((new Vector2Int(a.x + t.x, a.y + y), 3));
                }
                // The painted tables have chairs on their north edge already: start from the south.
                int n = 0;
                foreach (var (c, rot) in around)
                {
                    if (n >= count) break;
                    if (rot == 0 && (last.def == BuildingDefs.Table || last.def == BuildingDefs.CheckedTable)) continue;
                    if (TryPlace(BuildingDefs.DiningChair, c, rot, false)) n++;
                }
            }

            /// <summary>Beside the last piece along its wall: a bedside table by the bed.</summary>
            public bool Beside(BuildingDef def)
            {
                if (last.def == null || last.side < 0) return false;
                var t = Footprint.SizeOf(last.def, last.rot);
                var a = last.anchor;
                var options = last.side == N || last.side == S
                    ? new[] { new Vector2Int(a.x - 1, last.side == N ? a.y + t.y - 1 : a.y), new Vector2Int(a.x + t.x, last.side == N ? a.y + t.y - 1 : a.y) }
                    : new[] { new Vector2Int(last.side == E ? a.x + t.x - 1 : a.x, a.y - 1), new Vector2Int(last.side == E ? a.x + t.x - 1 : a.x, a.y + t.y) };
                foreach (var c in options)
                    if (TryPlace(def, c, last.rot, false)) return true;
                return false;
            }

            /// <summary>A corner, the ones by the door first (that's where the plant goes).</summary>
            public bool Corner(BuildingDef def)
            {
                var r = Room.R;
                var corners = new List<Vector2Int>
                {
                    new Vector2Int(r.xMin, r.yMin), new Vector2Int(r.xMax - 1, r.yMin),
                    new Vector2Int(r.xMin, r.yMax - 1), new Vector2Int(r.xMax - 1, r.yMax - 1),
                };
                corners.Sort((p, q) => (p - Room.Door).sqrMagnitude.CompareTo((q - Room.Door).sqrMagnitude));
                foreach (var c in corners)
                    if (TryPlace(def, c, RotFor((Room.DoorSide + 2) % 4), false)) return true;
                return false;
            }

            // --- Filling a big room ---

            /// <summary>
            /// Lines the walls with pieces from <paramref name="pool"/> (in turn) until about
            /// <paramref name="coverage"/> of the wall is used. Not the door's wall.
            /// </summary>
            public void WallFill(BuildingDef[] pool, float coverage)
            {
                if (pool == null || pool.Length == 0) return;
                int back = (Room.DoorSide + 2) % 4;
                var sides = new[] { back, SideA, (SideA + 2) % 4 };
                int perimeter = 0, used = 0;
                foreach (var c in Room.Cells)
                {
                    bool edge = c.x == Room.R.xMin || c.x == Room.R.xMax - 1 || c.y == Room.R.yMin || c.y == Room.R.yMax - 1;
                    if (!edge || blocked.Contains(c)) continue;
                    perimeter++;
                    if (Map.GetBuilding(c) != null) used++;
                }
                int fails = 0;
                for (int i = 0; used < perimeter * coverage && fails < pool.Length * sides.Length; i++)
                {
                    var def = pool[i % pool.Length];
                    if (Wall(sides[i % sides.Length], def)) { used += Footprint.SizeOf(def, 0).x; fails = 0; }
                    else fails++;
                }
            }

            /// <summary>Out on the floor, pieces from <paramref name="pool"/> in turn, about one per <paramref name="cellsEach"/> cells.</summary>
            public void FloorFill(BuildingDef[] pool, int cellsEach)
            {
                if (pool == null || pool.Length == 0) return;
                int target = Mathf.Max(1, Room.Cells.Count / cellsEach), placedHere = 0, fails = 0;
                for (int i = 0; placedHere < target && fails < pool.Length; i++)
                {
                    if (Floor(pool[i % pool.Length], spaced: true)) { placedHere++; fails = 0; }
                    else fails++;
                }
            }

            // --- Shops ---

            /// <summary>
            /// The counter, across the shop in front of the back wall (with an aisle behind it if the
            /// shop is deep enough), facing the door. Returns its cell.
            /// </summary>
            public Vector2Int? Counter(BuildingDef def)
            {
                int back = (Room.DoorSide + 2) % 4;
                int depth = back == N || back == S ? Room.R.height : Room.R.width;
                counterDepth = depth >= 5 ? 2 : 1;
                int rot = RotFor(back);
                foreach (var c in LineCells(back, counterDepth))
                {
                    if (!TryPlace(def, c, rot, true)) continue;
                    counterCells.Add(c);
                    last = (c, def, back, rot);
                    return c;
                }
                // A cramped shop: anywhere it'll stand.
                foreach (var c in Room.Cells)
                    if (TryPlace(def, c, rot, true)) { counterCells.Add(c); last = (c, def, back, rot); return c; }
                return null;
            }

            /// <summary>Cells of the line <paramref name="depth"/> in from the back wall, middle first.</summary>
            List<Vector2Int> LineCells(int back, int depth)
            {
                var r = Room.R;
                var list = new List<Vector2Int>();
                if (back == N || back == S)
                {
                    int y = back == N ? r.yMax - 1 - depth : r.yMin + depth;
                    for (int x = r.xMin + 1; x < r.xMax - 1; x++) list.Add(new Vector2Int(x, y));
                }
                else
                {
                    int x = back == E ? r.xMax - 1 - depth : r.xMin + depth;
                    for (int y = r.yMin + 1; y < r.yMax - 1; y++) list.Add(new Vector2Int(x, y));
                }
                var mid = r.center;
                list.Sort((p, q) => ((Vector2)p - mid).sqrMagnitude.CompareTo(((Vector2)q - mid).sqrMagnitude));
                return list;
            }

            /// <summary>Beside the counter, on its line: the till, the glass deli case. Leaves the ends open.</summary>
            public bool CounterRun(BuildingDef def)
            {
                if (counterCells.Count == 0) return false;
                int back = (Room.DoorSide + 2) % 4;
                int rot = RotFor(back);
                var t = Footprint.SizeOf(def, rot);
                bool alongX = back == N || back == S;
                int lo = int.MaxValue, hi = int.MinValue;
                foreach (var c in counterCells) { int v = alongX ? c.x : c.y; lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v); }
                var line = counterCells[0];
                int len = alongX ? t.x : t.y;
                var tries = Rng.NextDouble() < 0.5 ? new[] { hi + 1, lo - len } : new[] { lo - len, hi + 1 };
                foreach (int v in tries)
                {
                    Vector2Int anchor;
                    if (alongX) anchor = new Vector2Int(v, back == N ? line.y - (t.y - 1) : line.y);
                    else anchor = new Vector2Int(back == E ? line.x - (t.x - 1) : line.x, v);
                    // Leave the ends of the line open, to get behind the counter.
                    int lowEnd = alongX ? Room.R.xMin : Room.R.yMin, highEnd = alongX ? Room.R.xMax - 1 : Room.R.yMax - 1;
                    if (v <= lowEnd || v + len - 1 >= highEnd) continue;
                    if (!TryPlace(def, anchor, rot, false)) continue;
                    foreach (var c in Footprint.Cells(def, anchor, rot)) counterCells.Add(c);
                    return true;
                }
                return false;
            }

            /// <summary>A bar stool on the customers' side of the counter.</summary>
            public bool Stool(BuildingDef def)
            {
                int back = (Room.DoorSide + 2) % 4;
                var toward = -Dir(back);
                foreach (var c in counterCells)
                    if (TryPlace(def, c + toward, RotFor(back), false)) return true;
                return false;
            }

            /// <summary>Out on the shop floor, between the counter and the door: tables in rows, stands.</summary>
            public bool Floor(BuildingDef def, bool spaced) => FloorAt(def, spaced, 2) || FloorAt(def, spaced, 1) || spaced && FloorAt(def, false, 1);

            bool FloorAt(BuildingDef def, bool spaced, int aisle)
            {
                int back = (Room.DoorSide + 2) % 4;
                int rot = RotFor(back);
                var cells = new List<Vector2Int>();
                foreach (var c in Room.Cells)
                {
                    int fromBack = back == N ? Room.R.yMax - 1 - c.y : back == S ? c.y - Room.R.yMin : back == E ? Room.R.xMax - 1 - c.x : c.x - Room.R.xMin;
                    if (fromBack < counterDepth + aisle) continue;
                    // Tables in rows: every other cell, so there's a way between them.
                    if (spaced && ((c.x - Room.R.xMin) % 2 != 0 || (c.y - Room.R.yMin) % 2 != 0)) continue;
                    cells.Add(c);
                }
                // Fill from the counter end towards the door, so the door end stays open.
                cells.Sort((p, q) => -((p - Room.Door).sqrMagnitude.CompareTo((q - Room.Door).sqrMagnitude)));
                foreach (var c in cells)
                {
                    if (spaced && !Clearance(def, c, rot, 1)) continue;
                    if (TryPlace(def, c, rot, false)) return true;
                }
                return false;
            }

            // --- The rules every placement keeps ---

            /// <summary>No other furniture within <paramref name="ring"/> cells round it (walls are fine).</summary>
            bool Clearance(BuildingDef def, Vector2Int anchor, int rot, int ring)
            {
                foreach (var c in Footprint.Cells(def, anchor, rot))
                    for (int dy = -ring; dy <= ring; dy++)
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        var n = c + new Vector2Int(dx, dy);
                        if (!Room.Cells.Contains(n)) continue;
                        var b = Map.GetBuilding(n);
                        if (b != null && !Structural(b)) return false;
                    }
                return true;
            }

            bool TryPlace(BuildingDef def, Vector2Int anchor, int rot, bool use)
            {
                foreach (var c in Footprint.Cells(def, anchor, rot))
                    if (!Room.Cells.Contains(c) || blocked.Contains(c) || Map.GetBuilding(c) != null) return false;
                Footprint.Place(Map, def, anchor, rot);
                placed.Add((anchor, def, use));
                if (Walkable()) return true;
                Footprint.Remove(Map, anchor, out _, out _);
                placed.RemoveAt(placed.Count - 1);
                return false;
            }

            /// <summary>Every free cell can be reached from a door, and everything used can be reached.</summary>
            bool Walkable()
            {
                var reached = new HashSet<Vector2Int>();
                var queue = new Queue<Vector2Int>();
                foreach (var d in Room.Doors)
                    foreach (var dir in Dirs)
                    {
                        var c = d + dir;
                        if (Room.Cells.Contains(c) && Passable(c) && reached.Add(c)) queue.Enqueue(c);
                    }
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    foreach (var dir in Dirs)
                    {
                        var n = c + dir;
                        if (Room.Cells.Contains(n) && Passable(n) && reached.Add(n)) queue.Enqueue(n);
                    }
                }
                foreach (var c in Room.Cells)
                    if (Map.GetBuilding(c) == null && !reached.Contains(c)) return false;
                foreach (var (anchor, def, use) in placed)
                {
                    if (!use) continue;
                    bool ok = false;
                    foreach (var c in Footprint.Cells(def, anchor, Map.GetRotation(anchor.x, anchor.y)))
                    {
                        if (reached.Contains(c)) { ok = true; break; }
                        foreach (var dir in Dirs) if (reached.Contains(c + dir)) { ok = true; break; }
                        if (ok) break;
                    }
                    if (!ok) return false;
                }
                return true;
            }

            bool Passable(Vector2Int c)
            {
                var b = Map.GetBuilding(c);
                return b == null || b.Passable;
            }
        }
    }
}
