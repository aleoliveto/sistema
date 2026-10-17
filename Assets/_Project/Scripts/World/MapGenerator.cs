using System.Collections.Generic;
using UnityEngine;

namespace OSistema.World
{
    /// <summary>
    /// Generates a fictional rione: a street grid, sidewalks, and blocks split into
    /// buildings separated by narrow vicoli (alleys).
    /// </summary>
    public sealed class MapGenerator
    {
        const int RoadWidth = 3;
        const int BlockWidth = 26;
        const int BlockHeight = 20;
        const int MinLot = 5;

        readonly System.Random rng;

        public MapGenerator(int seed) => rng = new System.Random(seed);

        public Map Generate(int width, int height, out RectInt playerBase)
        {
            var map = new Map(width, height);
            var lots = new List<RectInt>();

            for (int rx = 0; rx < width; rx += BlockWidth)
                Fill(map, new RectInt(rx, 0, RoadWidth, height), TerrainType.Road);
            for (int ry = 0; ry < height; ry += BlockHeight)
                Fill(map, new RectInt(0, ry, width, RoadWidth), TerrainType.Road);

            for (int bx = 0; bx < width; bx += BlockWidth)
            for (int by = 0; by < height; by += BlockHeight)
            {
                var block = new RectInt(bx + RoadWidth, by + RoadWidth, BlockWidth - RoadWidth, BlockHeight - RoadWidth);
                block.width = Mathf.Min(block.width, width - block.x);
                block.height = Mathf.Min(block.height, height - block.y);
                if (block.width < 8 || block.height < 8) continue;

                foreach (var c in GridUtil.Cells(block, outlineOnly: true))
                    map.SetTerrain(c.x, c.y, TerrainType.Sidewalk);

                var inner = new RectInt(block.x + 1, block.y + 1, block.width - 2, block.height - 2);
                Split(inner, 0, lots);
            }

            var baseLot = PickBaseLot(lots, new Vector2(width / 2f, height / 2f));
            lots.Remove(baseLot);
            BuildBase(map, baseLot);

            // A few shops scattered around the district; the rest are ordinary buildings.
            var shops = new[]
            {
                BuildingDefs.PizzeriaCounter, BuildingDefs.PizzeriaCounter,
                BuildingDefs.SalumeriaCounter, BuildingDefs.FarmaciaCounter,
            };
            Shuffle(lots);
            int shopIndex = 0;
            foreach (var lot in lots)
            {
                if (shopIndex < shops.Length && lot.width >= 6 && lot.height >= 6) BuildShop(map, lot, shops[shopIndex++]);
                else BuildLot(map, lot);
            }

            playerBase = new RectInt(baseLot.x + 1, baseLot.y + 1, baseLot.width - 2, baseLot.height - 2);
            return map;
        }

        // Binary split leaving a 1-cell vicolo between the halves, so every lot borders walkable ground.
        void Split(RectInt r, int depth, List<RectInt> lots)
        {
            bool canSplitX = r.width >= MinLot * 2 + 1;
            bool canSplitY = r.height >= MinLot * 2 + 1;
            bool stop = depth >= 3 || (!canSplitX && !canSplitY) || (depth >= 1 && rng.NextDouble() < 0.25);
            if (stop)
            {
                lots.Add(r);
                return;
            }

            bool splitX = canSplitX && (!canSplitY || r.width >= r.height);
            if (splitX)
            {
                int cut = rng.Next(MinLot, r.width - MinLot);
                Split(new RectInt(r.x, r.y, cut, r.height), depth + 1, lots);
                Split(new RectInt(r.x + cut + 1, r.y, r.width - cut - 1, r.height), depth + 1, lots);
            }
            else
            {
                int cut = rng.Next(MinLot, r.height - MinLot);
                Split(new RectInt(r.x, r.y, r.width, cut), depth + 1, lots);
                Split(new RectInt(r.x, r.y + cut + 1, r.width, r.height - cut - 1), depth + 1, lots);
            }
        }

        static RectInt PickBaseLot(List<RectInt> lots, Vector2 center)
        {
            RectInt best = lots[0];
            float bestDist = float.MaxValue;
            foreach (var lot in lots)
            {
                if (lot.width < 6 || lot.height < 6) continue;
                float d = (lot.center - center).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = lot;
                }
            }
            return best;
        }

        void BuildLot(Map map, RectInt lot)
        {
            if (rng.NextDouble() < 0.15) return; // courtyard or empty lot

            var frontDoor = BuildShell(map, lot);

            // Large buildings get an interior partition with a doorway.
            if (lot.width >= 9 && lot.height >= 6 && rng.NextDouble() < 0.7)
            {
                int wx = lot.x + rng.Next(3, lot.width - 3);
                if (wx == frontDoor.x) wx = wx + 1 < lot.xMax - 2 ? wx + 1 : wx - 1; // don't wall off the entrance
                int doorY = lot.y + rng.Next(1, lot.height - 1);
                for (int y = lot.y + 1; y < lot.yMax - 1; y++)
                    map.SetBuilding(wx, y, y == doorY ? BuildingDefs.Door : BuildingDefs.Wall);
            }
        }

        void BuildBase(Map map, RectInt lot)
        {
            // Front door fixed at the bottom centre so the furniture never blocks it.
            BuildShell(map, lot, new Vector2Int(lot.x + lot.width / 2, lot.yMin));

            // Three beds along the top wall, stove and pantry in the bottom-left corner, a table in the middle.
            for (int i = 0; i < 3 && lot.x + 1 + i * 2 < lot.xMax - 1; i++)
                map.SetBuilding(lot.x + 1 + i * 2, lot.yMax - 2, BuildingDefs.Bed);
            map.SetBuilding(lot.x + 1, lot.y + 1, BuildingDefs.Stove);
            map.SetBuilding(lot.x + 1, lot.y + 2, BuildingDefs.Pantry);
            var mid = Vector2Int.RoundToInt(lot.center);
            map.SetBuilding(mid.x, mid.y - 1, BuildingDefs.Table);
        }

        void BuildShop(Map map, RectInt lot, BuildingDef counter)
        {
            BuildShell(map, lot);
            var mid = Vector2Int.RoundToInt(lot.center);
            map.SetBuilding(mid.x, mid.y, counter);
        }

        void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        Vector2Int BuildShell(Map map, RectInt lot, Vector2Int? fixedDoor = null)
        {
            foreach (var c in GridUtil.Cells(lot, outlineOnly: false))
            {
                bool edge = c.x == lot.xMin || c.x == lot.xMax - 1 || c.y == lot.yMin || c.y == lot.yMax - 1;
                map.SetTerrain(c.x, c.y, TerrainType.Floor);
                if (edge) map.SetBuilding(c.x, c.y, BuildingDefs.Wall);
            }

            // One front door on a random side, never on a corner.
            int side = rng.Next(4);
            Vector2Int door = fixedDoor ?? side switch
            {
                0 => new Vector2Int(lot.x + rng.Next(1, lot.width - 1), lot.yMin),
                1 => new Vector2Int(lot.x + rng.Next(1, lot.width - 1), lot.yMax - 1),
                2 => new Vector2Int(lot.xMin, lot.y + rng.Next(1, lot.height - 1)),
                _ => new Vector2Int(lot.xMax - 1, lot.y + rng.Next(1, lot.height - 1)),
            };
            map.SetBuilding(door.x, door.y, BuildingDefs.Door);
            return door;
        }

        static void Fill(Map map, RectInt r, TerrainType t)
        {
            foreach (var c in GridUtil.Cells(r, outlineOnly: false))
                if (map.InBounds(c)) map.SetTerrain(c.x, c.y, t);
        }
    }
}
