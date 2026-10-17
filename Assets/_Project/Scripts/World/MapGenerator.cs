using System.Collections.Generic;
using UnityEngine;

namespace OSistema.World
{
    /// <summary>
    /// Generates a fictional quarter of Naples. An irregular street grid splits the map into blocks,
    /// and each block gets a character: dense vicoli, ordinary palazzi, a Vele-style housing block,
    /// a church with its piazza, a scrap of park, a street market. Then the street furniture.
    /// </summary>
    public sealed class MapGenerator
    {
        enum BlockKind { Palazzi, Vicoli, Vele, Church, Park, Market }

        const int MinLot = 5;

        // Facade palettes (index = style byte): tuff yellow, terracotta, pink, ochre, grey, pale blue.
        public const int StyleCount = 6;

        readonly System.Random rng;
        readonly List<RectInt> shopLots = new List<RectInt>();
        readonly List<RectInt> homeLots = new List<RectInt>();

        public MapGenerator(int seed) => rng = new System.Random(seed);

        public Map Generate(int width, int height, out RectInt playerBase, out List<RectInt> blocks)
        {
            var map = new Map(width, height);
            blocks = new List<RectInt>();

            var xs = RoadPositions(width, out var xWidths);
            var ys = RoadPositions(height, out var yWidths);
            for (int i = 0; i < xs.Count; i++) Fill(map, new RectInt(xs[i], 0, xWidths[i], height), TerrainType.Road);
            for (int i = 0; i < ys.Count; i++) Fill(map, new RectInt(0, ys[i], width, yWidths[i]), TerrainType.Road);

            for (int i = 0; i < xs.Count; i++)
            for (int j = 0; j < ys.Count; j++)
            {
                int bx = xs[i] + xWidths[i];
                int by = ys[j] + yWidths[j];
                int bw = (i + 1 < xs.Count ? xs[i + 1] : width) - bx;
                int bh = (j + 1 < ys.Count ? ys[j + 1] : height) - by;
                if (bw < 10 || bh < 10) continue;
                var block = new RectInt(bx, by, bw, bh);
                blocks.Add(block);
                foreach (var c in GridUtil.Cells(block, outlineOnly: true)) map.SetTerrain(c.x, c.y, TerrainType.Sidewalk);
            }

            var kinds = AssignKinds(blocks, new Vector2(width / 2f, height / 2f));
            for (int i = 0; i < blocks.Count; i++)
            {
                var inner = new RectInt(blocks[i].x + 1, blocks[i].y + 1, blocks[i].width - 2, blocks[i].height - 2);
                switch (kinds[i])
                {
                    case BlockKind.Vicoli: BuildLots(map, inner, maxDepth: 4, minLot: 4, builtChance: 0.88, alleyDecor: true); break;
                    case BlockKind.Vele: BuildVele(map, inner); break;
                    case BlockKind.Church: BuildChurch(map, inner); break;
                    case BlockKind.Park: BuildPark(map, inner); break;
                    case BlockKind.Market: BuildMarket(map, inner); break;
                    default: BuildLots(map, inner, maxDepth: 3, minLot: MinLot, builtChance: 0.82, alleyDecor: false); break;
                }
            }

            if (homeLots.Count == 0) homeLots.AddRange(shopLots); // fallback: any decent building
            var baseLot = PickLot(homeLots, new Vector2(width / 2f, height / 2f), minArea: 48);
            homeLots.Remove(baseLot);
            shopLots.Remove(baseLot);
            BuildBase(map, baseLot);

            PlaceShops(map);
            DecorateStreets(map, blocks);

            playerBase = new RectInt(baseLot.x + 1, baseLot.y + 1, baseLot.width - 2, baseLot.height - 2);
            return map;
        }

        // ---------- Streets ----------

        // Irregular spacing: every third road is a wide avenue, blocks are 16-30 cells.
        List<int> RoadPositions(int length, out List<int> widths)
        {
            var positions = new List<int>();
            widths = new List<int>();
            int pos = 0, index = 0;
            while (pos < length - 8)
            {
                int w = index % 3 == 0 ? 4 : 3;
                positions.Add(pos);
                widths.Add(Mathf.Min(w, length - pos));
                pos += w + rng.Next(16, 31);
                index++;
            }
            return positions;
        }

        List<BlockKind> AssignKinds(List<RectInt> blocks, Vector2 center)
        {
            var kinds = new List<BlockKind>();
            foreach (var _ in blocks) kinds.Add(rng.NextDouble() < 0.5 ? BlockKind.Vicoli : BlockKind.Palazzi);

            // Special blocks, never the most central one (that's where the crew lives).
            var order = new List<int>();
            for (int i = 0; i < blocks.Count; i++) order.Add(i);
            order.Sort((a, b) => (blocks[a].center - center).sqrMagnitude.CompareTo((blocks[b].center - center).sqrMagnitude));
            kinds[order[0]] = BlockKind.Palazzi;

            void Assign(BlockKind kind, int count, int minW, int minH)
            {
                var candidates = new List<int>();
                for (int i = 1; i < order.Count; i++)
                {
                    int b = order[i];
                    if (kinds[b] != BlockKind.Vicoli && kinds[b] != BlockKind.Palazzi) continue;
                    if (blocks[b].width >= minW && blocks[b].height >= minH) candidates.Add(b);
                }
                for (int k = 0; k < count && candidates.Count > 0; k++)
                {
                    int pick = candidates[rng.Next(candidates.Count)];
                    candidates.Remove(pick);
                    kinds[pick] = kind;
                }
            }

            Assign(BlockKind.Church, 1, 16, 14);
            Assign(BlockKind.Vele, 2, 22, 18);
            Assign(BlockKind.Park, 2, 12, 12);
            Assign(BlockKind.Market, 1, 16, 12);
            return kinds;
        }

        // ---------- Ordinary blocks ----------

        void BuildLots(Map map, RectInt inner, int maxDepth, int minLot, double builtChance, bool alleyDecor)
        {
            // Alleys between lots are bare ground.
            foreach (var c in GridUtil.Cells(inner, false)) map.SetTerrain(c.x, c.y, TerrainType.Ground);

            var lots = new List<RectInt>();
            Split(inner, 0, maxDepth, minLot, lots);
            foreach (var lot in lots)
            {
                if (rng.NextDouble() > builtChance)
                {
                    BuildCourtyard(map, lot);
                    continue;
                }
                BuildBuilding(map, lot, partitions: true);
                if (lot.width >= 6 && lot.height >= 6) shopLots.Add(lot);
                // A home is a proper room, not a corridor.
                bool homeShaped = lot.width >= 8 && lot.height >= 7 && lot.width <= 13 && lot.height <= 11;
                if (homeShaped) homeLots.Add(lot);
            }

            if (!alleyDecor) return;
            // Edicole votive on alley walls and bins in the vicoli.
            foreach (var c in GridUtil.Cells(inner, false))
            {
                if (map.GetBuilding(c) != null || map.GetTerrain(c.x, c.y) != TerrainType.Ground) continue;
                if (rng.NextDouble() < 0.012) map.SetBuilding(c.x, c.y, BuildingDefs.Bin);
            }
        }

        void Split(RectInt r, int depth, int maxDepth, int minLot, List<RectInt> lots)
        {
            bool canSplitX = r.width >= minLot * 2 + 1;
            bool canSplitY = r.height >= minLot * 2 + 1;
            bool stop = depth >= maxDepth || (!canSplitX && !canSplitY) || (depth >= 1 && rng.NextDouble() < 0.2);
            if (stop)
            {
                lots.Add(r);
                return;
            }

            bool splitX = canSplitX && (!canSplitY || r.width >= r.height);
            if (splitX)
            {
                int cut = rng.Next(minLot, r.width - minLot);
                Split(new RectInt(r.x, r.y, cut, r.height), depth + 1, maxDepth, minLot, lots);
                Split(new RectInt(r.x + cut + 1, r.y, r.width - cut - 1, r.height), depth + 1, maxDepth, minLot, lots);
            }
            else
            {
                int cut = rng.Next(minLot, r.height - minLot);
                Split(new RectInt(r.x, r.y, r.width, cut), depth + 1, maxDepth, minLot, lots);
                Split(new RectInt(r.x, r.y + cut + 1, r.width, r.height - cut - 1), depth + 1, maxDepth, minLot, lots);
            }
        }

        void BuildCourtyard(Map map, RectInt lot)
        {
            foreach (var c in GridUtil.Cells(lot, false))
            {
                map.SetTerrain(c.x, c.y, rng.NextDouble() < 0.6 ? TerrainType.Ground : TerrainType.Grass);
                if (rng.NextDouble() < 0.04) map.SetBuilding(c.x, c.y, BuildingDefs.Rubble);
            }
            var mid = Vector2Int.RoundToInt(lot.center);
            if (rng.NextDouble() < 0.6) map.SetBuilding(mid.x, mid.y, BuildingDefs.Tree);
        }

        Vector2Int BuildBuilding(Map map, RectInt lot, bool partitions, Vector2Int? fixedDoor = null)
        {
            byte style = (byte)rng.Next(StyleCount);
            foreach (var c in GridUtil.Cells(lot, outlineOnly: false))
            {
                bool edge = c.x == lot.xMin || c.x == lot.xMax - 1 || c.y == lot.yMin || c.y == lot.yMax - 1;
                map.SetTerrain(c.x, c.y, TerrainType.Floor);
                map.SetStyle(c.x, c.y, style);
                if (edge) map.SetBuilding(c.x, c.y, BuildingDefs.Wall);
            }

            int side = rng.Next(4);
            Vector2Int door = fixedDoor ?? side switch
            {
                0 => new Vector2Int(lot.x + rng.Next(1, lot.width - 1), lot.yMin),
                1 => new Vector2Int(lot.x + rng.Next(1, lot.width - 1), lot.yMax - 1),
                2 => new Vector2Int(lot.xMin, lot.y + rng.Next(1, lot.height - 1)),
                _ => new Vector2Int(lot.xMax - 1, lot.y + rng.Next(1, lot.height - 1)),
            };
            map.SetBuilding(door.x, door.y, BuildingDefs.Door);

            if (partitions && lot.width >= 9 && lot.height >= 6 && rng.NextDouble() < 0.7)
            {
                int wx = lot.x + rng.Next(3, lot.width - 3);
                if (wx == door.x) wx = wx + 1 < lot.xMax - 2 ? wx + 1 : wx - 1; // don't wall off the entrance
                int doorY = lot.y + rng.Next(1, lot.height - 1);
                for (int y = lot.y + 1; y < lot.yMax - 1; y++)
                    map.SetBuilding(wx, y, y == doorY ? BuildingDefs.Door : BuildingDefs.Wall);
            }
            return door;
        }

        // ---------- Special blocks ----------

        /// <summary>A brutalist housing megablock: corridors, flats either side, a ruined courtyard.</summary>
        void BuildVele(Map map, RectInt inner)
        {
            var shell = new RectInt(inner.x + 1, inner.y + 1, inner.width - 2, inner.height - 2);
            foreach (var c in GridUtil.Cells(inner, false)) map.SetTerrain(c.x, c.y, TerrainType.Ground);
            const byte concrete = 4;
            foreach (var c in GridUtil.Cells(shell, false))
            {
                bool edge = c.x == shell.xMin || c.x == shell.xMax - 1 || c.y == shell.yMin || c.y == shell.yMax - 1;
                map.SetTerrain(c.x, c.y, TerrainType.Floor);
                map.SetStyle(c.x, c.y, concrete);
                if (edge) map.SetBuilding(c.x, c.y, BuildingDefs.Wall);
            }

            // Corridors every 6 rows, flats between them with a door onto the corridor.
            int midX = shell.x + shell.width / 2;
            for (int y = shell.yMin + 1; y < shell.yMax - 1; y++)
            {
                int row = (y - shell.yMin) % 6;
                bool corridor = row == 3;
                for (int x = shell.xMin + 1; x < shell.xMax - 1; x++)
                {
                    if (corridor || x == midX || x == midX + 1)
                    {
                        map.SetTerrain(x, y, TerrainType.Paving);
                        continue;
                    }
                    // Walls line both sides of each corridor; flats are split every 5 columns.
                    bool flatWall = row == 2 || row == 4 || (x - shell.xMin) % 5 == 0;
                    if (flatWall) map.SetBuilding(x, y, BuildingDefs.Wall);
                }
                if (corridor)
                {
                    // Doors from the corridor into the flats above and below.
                    for (int x = shell.xMin + 3; x < shell.xMax - 2; x += 5)
                    {
                        if (y + 1 < shell.yMax - 1 && map.GetBuilding(x, y + 1) == BuildingDefs.Wall) map.SetBuilding(x, y + 1, BuildingDefs.Door);
                        if (y - 1 > shell.yMin && map.GetBuilding(x, y - 1) == BuildingDefs.Wall) map.SetBuilding(x, y - 1, BuildingDefs.Door);
                    }
                    // Corridor exits on both ends.
                    map.SetBuilding(shell.xMin, y, BuildingDefs.Door);
                    map.SetBuilding(shell.xMax - 1, y, BuildingDefs.Door);
                }
            }
            map.SetBuilding(midX, shell.yMin, BuildingDefs.Door);
            map.SetBuilding(midX, shell.yMax - 1, BuildingDefs.Door);

            // Rubble and abandoned flats: the place is falling apart.
            foreach (var c in GridUtil.Cells(shell, false))
                if (map.GetBuilding(c) == null && rng.NextDouble() < 0.02) map.SetBuilding(c.x, c.y, BuildingDefs.Rubble);
        }

        void BuildChurch(Map map, RectInt inner)
        {
            foreach (var c in GridUtil.Cells(inner, false)) map.SetTerrain(c.x, c.y, TerrainType.Paving);

            // The church takes the top half; the piazza in front of it the rest.
            int churchH = Mathf.Max(8, inner.height / 2);
            int churchW = Mathf.Min(inner.width - 4, 14);
            var church = new RectInt(inner.x + (inner.width - churchW) / 2, inner.yMax - churchH, churchW, churchH);
            const byte stone = 4;
            foreach (var c in GridUtil.Cells(church, false))
            {
                bool edge = c.x == church.xMin || c.x == church.xMax - 1 || c.y == church.yMin || c.y == church.yMax - 1;
                map.SetTerrain(c.x, c.y, TerrainType.Marble);
                map.SetStyle(c.x, c.y, stone);
                if (edge) map.SetBuilding(c.x, c.y, BuildingDefs.Wall);
            }
            int doorX = church.x + church.width / 2;
            map.SetBuilding(doorX, church.yMin, BuildingDefs.Door);
            map.SetBuilding(doorX - 1, church.yMin, BuildingDefs.Door);
            map.SetBuilding(doorX, church.yMax - 2, BuildingDefs.Altar);
            map.SetBuilding(doorX - 1, church.yMax - 2, BuildingDefs.Altar);
            for (int y = church.yMin + 2; y < church.yMax - 3; y += 2)
            for (int x = church.xMin + 2; x < church.xMax - 2; x++)
                if (x != doorX && x != doorX - 1) map.SetBuilding(x, y, BuildingDefs.Pew);

            // The piazza: a fountain, benches, a few trees.
            var plaza = new RectInt(inner.x, inner.y, inner.width, inner.height - churchH);
            var mid = Vector2Int.RoundToInt(plaza.center);
            map.SetBuilding(mid.x, mid.y, BuildingDefs.Fountain);
            map.SetBuilding(plaza.xMin + 2, plaza.yMin + 2, BuildingDefs.Tree);
            map.SetBuilding(plaza.xMax - 3, plaza.yMin + 2, BuildingDefs.Tree);
            map.SetBuilding(mid.x - 3, mid.y, BuildingDefs.Bench);
            map.SetBuilding(mid.x + 3, mid.y, BuildingDefs.Bench);
            map.SetBuilding(plaza.xMin + 1, plaza.yMax - 1, BuildingDefs.StreetLamp);
            map.SetBuilding(plaza.xMax - 2, plaza.yMax - 1, BuildingDefs.StreetLamp);
        }

        void BuildPark(Map map, RectInt inner)
        {
            foreach (var c in GridUtil.Cells(inner, false))
            {
                map.SetTerrain(c.x, c.y, TerrainType.Grass);
                double r = rng.NextDouble();
                if (r < 0.07) map.SetBuilding(c.x, c.y, BuildingDefs.Tree);
                else if (r < 0.085) map.SetBuilding(c.x, c.y, BuildingDefs.Rubble);
            }
            // A dirt path across, with benches.
            int py = inner.y + inner.height / 2;
            for (int x = inner.xMin; x < inner.xMax; x++)
            {
                map.SetTerrain(x, py, TerrainType.Ground);
                if (map.GetBuilding(x, py) != null) map.SetBuilding(x, py, null);
                if (x % 6 == 0 && py + 1 < inner.yMax && map.GetBuilding(x, py + 1) == null) map.SetBuilding(x, py + 1, BuildingDefs.Bench);
            }
        }

        void BuildMarket(Map map, RectInt inner)
        {
            // Shops around the edge, stalls in rows down the middle.
            foreach (var c in GridUtil.Cells(inner, false)) map.SetTerrain(c.x, c.y, TerrainType.Paving);
            var top = new RectInt(inner.x, inner.yMax - 7, inner.width, 7);
            var lots = new List<RectInt>();
            Split(top, 0, 2, 6, lots);
            foreach (var lot in lots)
            {
                if (lot.height < 6) continue;
                BuildBuilding(map, lot, partitions: false, new Vector2Int(lot.x + lot.width / 2, lot.yMin));
                if (lot.width >= 6) shopLots.Add(lot);
            }
            for (int y = inner.yMin + 2; y < inner.yMax - 9; y += 3)
            for (int x = inner.xMin + 2; x < inner.xMax - 2; x++)
                if ((x - inner.xMin) % 6 != 0) map.SetBuilding(x, y, BuildingDefs.MarketStall);
        }

        // ---------- The crew's home and the shops ----------

        RectInt PickLot(List<RectInt> lots, Vector2 near, int minArea)
        {
            RectInt best = lots.Count > 0 ? lots[0] : new RectInt(near.x > 0 ? (int)near.x : 0, (int)near.y, 8, 8);
            float bestDist = float.MaxValue;
            foreach (var lot in lots)
            {
                if (lot.width * lot.height < minArea) continue;
                float d = (lot.center - near).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = lot;
                }
            }
            return best;
        }

        void BuildBase(Map map, RectInt lot)
        {
            // Clear whatever the block put there, then a simple basso: one big room.
            foreach (var c in GridUtil.Cells(lot, false)) map.SetBuilding(c.x, c.y, null);
            var door = new Vector2Int(lot.x + lot.width / 2, lot.yMin);
            BuildBuilding(map, lot, partitions: false, door);
            // Make sure the door opens onto walkable ground.
            if (map.InBounds(door.x, door.y - 1) && !map.IsPassable(door.x, door.y - 1)) map.SetBuilding(door.x, door.y - 1, null);

            for (int i = 0; i < 3 && lot.x + 1 + i * 2 < lot.xMax - 1; i++)
                map.SetBuilding(lot.x + 1 + i * 2, lot.yMax - 2, BuildingDefs.Bed);
            map.SetBuilding(lot.x + 1, lot.y + 1, BuildingDefs.Stove);
            map.SetBuilding(lot.x + 1, lot.y + 2, BuildingDefs.Pantry);
            var mid = Vector2Int.RoundToInt(lot.center);
            map.SetBuilding(mid.x, mid.y - 1, BuildingDefs.Table);
            map.SetBuilding(lot.xMax - 2, lot.y + 1, BuildingDefs.Shrine);
        }

        void PlaceShops(Map map)
        {
            var shops = new[]
            {
                BuildingDefs.PizzeriaCounter, BuildingDefs.PizzeriaCounter, BuildingDefs.PizzeriaCounter,
                BuildingDefs.SalumeriaCounter, BuildingDefs.SalumeriaCounter, BuildingDefs.FarmaciaCounter,
                BuildingDefs.BarCounter, BuildingDefs.BarCounter, BuildingDefs.BarCounter,
                BuildingDefs.MacelleriaCounter, BuildingDefs.MacelleriaCounter,
                BuildingDefs.TabaccheriaCounter, BuildingDefs.TabaccheriaCounter,
            };
            for (int i = shopLots.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (shopLots[i], shopLots[j]) = (shopLots[j], shopLots[i]);
            }
            int placed = 0;
            foreach (var lot in shopLots)
            {
                if (placed >= shops.Length) break;
                var mid = Vector2Int.RoundToInt(lot.center);
                // The lot must still be a building with free floor in the middle.
                if (map.GetTerrain(mid.x, mid.y) != TerrainType.Floor || map.GetBuilding(mid.x, mid.y) != null) continue;
                bool clear = true;
                foreach (var c in GridUtil.CellsAround(mid, 1))
                    if (map.GetBuilding(c) == BuildingDefs.Wall) clear = false;
                if (!clear) continue;
                map.SetBuilding(mid.x, mid.y, shops[placed++]);
            }
        }

        // ---------- Street furniture ----------

        void DecorateStreets(Map map, List<RectInt> blocks)
        {
            foreach (var block in blocks)
            {
                int i = 0;
                foreach (var c in GridUtil.Cells(block, outlineOnly: true))
                {
                    i++;
                    if (map.GetBuilding(c) != null) continue;
                    if (i % 9 == 0) map.SetBuilding(c.x, c.y, BuildingDefs.StreetLamp);
                    else if (rng.NextDouble() < 0.025) map.SetBuilding(c.x, c.y, BuildingDefs.ParkedScooter);
                    else if (rng.NextDouble() < 0.012) map.SetBuilding(c.x, c.y, BuildingDefs.Bin);
                }
            }

            // Parked cars along the kerb of wide roads, leaving the lanes free.
            for (int y = 1; y < map.Height - 1; y++)
            for (int x = 1; x < map.Width - 1; x++)
            {
                if (map.GetTerrain(x, y) != TerrainType.Road || map.GetBuilding(x, y) != null) continue;
                bool kerb = IsSidewalk(map, x + 1, y) || IsSidewalk(map, x - 1, y) || IsSidewalk(map, x, y + 1) || IsSidewalk(map, x, y - 1);
                bool wide = RoadWidthAt(map, x, y) >= 4;
                bool corner = (IsRoad(map, x + 1, y) || IsRoad(map, x - 1, y)) && (IsRoad(map, x, y + 1) || IsRoad(map, x, y - 1)) && CountRoad(map, x, y) >= 3;
                if (kerb && wide && !corner && rng.NextDouble() < 0.12) map.SetBuilding(x, y, BuildingDefs.ParkedCar);
            }
        }

        static bool IsSidewalk(Map map, int x, int y) => map.InBounds(x, y) && map.GetTerrain(x, y) == TerrainType.Sidewalk;
        static bool IsRoad(Map map, int x, int y) => map.InBounds(x, y) && map.GetTerrain(x, y) == TerrainType.Road;

        static int CountRoad(Map map, int x, int y)
        {
            int n = 0;
            if (IsRoad(map, x + 1, y)) n++;
            if (IsRoad(map, x - 1, y)) n++;
            if (IsRoad(map, x, y + 1)) n++;
            if (IsRoad(map, x, y - 1)) n++;
            return n;
        }

        // Width of the road through this cell, measured across its narrower axis.
        static int RoadWidthAt(Map map, int x, int y)
        {
            int h = 1, v = 1;
            for (int d = 1; d < 6 && IsRoad(map, x + d, y); d++) h++;
            for (int d = 1; d < 6 && IsRoad(map, x - d, y); d++) h++;
            for (int d = 1; d < 6 && IsRoad(map, x, y + d); d++) v++;
            for (int d = 1; d < 6 && IsRoad(map, x, y - d); d++) v++;
            return Mathf.Min(h, v);
        }

        static void Fill(Map map, RectInt r, TerrainType t)
        {
            foreach (var c in GridUtil.Cells(r, outlineOnly: false))
                if (map.InBounds(c)) map.SetTerrain(c.x, c.y, t);
        }
    }
}
