using System.Collections.Generic;
using UnityEngine;

namespace OSistema.World
{
    public enum PropertyKind { Home, Shop, Civic }

    /// <summary>A building on the map that can be owned: a basso, a flat, a shop.</summary>
    public sealed class Property
    {
        public int Id;
        public string Name;
        public RectInt Rect;      // including its walls
        public Vector2Int Door;
        public PropertyKind Kind;
        public bool OwnedByPlayer;
        public int Price;

        public RectInt Interior => new RectInt(Rect.x + 1, Rect.y + 1, Rect.width - 2, Rect.height - 2);
    }

    /// <summary>
    /// Generates a fictional corner of Naples by the sea. A few asphalt roads split it into quarters;
    /// most quarters are a grid of narrow basolato vicoli lined with buildings wall to wall, bassi
    /// opening onto the alley, laundry strung overhead. One quarter has the church piazza and the
    /// market, one is a quarter of brutalist housing blocks on wasteland. Along the bottom: the
    /// lungomare and the sea.
    /// </summary>
    public sealed class MapGenerator
    {
        enum QuarterKind { Quartieri, Borgo, Vele }

        public const int StyleCount = 6;
        const int SeaRows = 9;
        const int PromenadeRows = 4;
        const int RoadWidth = 3;
        const int VicoloWidth = 2;

        readonly System.Random rng;
        readonly List<RectInt> shopLots = new List<RectInt>();
        readonly List<RectInt> homeLots = new List<RectInt>();
        readonly List<Property> properties = new List<Property>();

        public MapGenerator(int seed) => rng = new System.Random(seed);

        public Map Generate(int width, int height, out RectInt playerBase, out List<RectInt> zones, out List<Property> props)
        {
            var map = new Map(width, height);
            zones = new List<RectInt>();

            // ---- The sea, the lungomare, the coast road ----
            int coastRoadY = SeaRows + PromenadeRows;
            Fill(map, new RectInt(0, 0, width, SeaRows), TerrainType.Water);
            Fill(map, new RectInt(0, SeaRows, width, PromenadeRows), TerrainType.Paving);
            for (int x = 0; x < width; x++)
            {
                if (x % 14 != 7) map.SetBuilding(x, SeaRows, BuildingDefs.Railing);
                if (x % 10 == 3) map.SetBuilding(x, SeaRows + PromenadeRows - 1, BuildingDefs.StreetLamp);
                else if (x % 10 == 6) map.SetBuilding(x, SeaRows + 2, BuildingDefs.Bench);
                else if (x % 20 == 0) map.SetBuilding(x, SeaRows + PromenadeRows - 1, BuildingDefs.Tree);
            }
            Fill(map, new RectInt(0, coastRoadY, width, RoadWidth), TerrainType.Road);

            // ---- Main roads: two north-south, one east-west ----
            int top = coastRoadY + RoadWidth;
            int x1 = (int)(width * 0.32f), x2 = (int)(width * 0.66f);
            int yMid = top + (height - top) / 2 - 1;
            Fill(map, new RectInt(x1, top, RoadWidth, height - top), TerrainType.Road);
            Fill(map, new RectInt(x2, top, RoadWidth, height - top), TerrainType.Road);
            Fill(map, new RectInt(0, yMid, width, RoadWidth), TerrainType.Road);

            var quarters = new List<RectInt>();
            int[] xs = { 0, x1 + RoadWidth, x2 + RoadWidth };
            int[] xe = { x1, x2, width };
            int[] ys = { top, yMid + RoadWidth };
            int[] ye = { yMid, height };
            for (int j = 0; j < 2; j++)
            for (int i = 0; i < 3; i++)
                quarters.Add(new RectInt(xs[i], ys[j], xe[i] - xs[i], ye[j] - ys[j]));

            // The player lives in the bottom-middle quarter; the Borgo is beside it; the Vele far away.
            var kinds = new QuarterKind[quarters.Count];
            for (int i = 0; i < kinds.Length; i++) kinds[i] = QuarterKind.Quartieri;
            kinds[rng.Next(2) == 0 ? 0 : 2] = QuarterKind.Borgo;
            kinds[3 + (rng.Next(2) == 0 ? 0 : 2)] = QuarterKind.Vele;

            for (int i = 0; i < quarters.Count; i++)
            {
                var q = quarters[i];
                // Pavements along the roads.
                foreach (var c in GridUtil.Cells(q, outlineOnly: true)) map.SetTerrain(c.x, c.y, TerrainType.Sidewalk);
                var inner = new RectInt(q.x + 1, q.y + 1, q.width - 2, q.height - 2);
                switch (kinds[i])
                {
                    case QuarterKind.Vele: BuildVeleQuarter(map, inner); break;
                    case QuarterKind.Borgo: BuildBorgo(map, inner); break;
                    default: BuildQuartieri(map, inner); break;
                }
                // Each quarter is four rioni for the territory game.
                int hw = q.width / 2, hh = q.height / 2;
                zones.Add(new RectInt(q.x, q.y, hw, hh));
                zones.Add(new RectInt(q.x + hw, q.y, q.width - hw, hh));
                zones.Add(new RectInt(q.x, q.y + hh, hw, q.height - hh));
                zones.Add(new RectInt(q.x + hw, q.y + hh, q.width - hw, q.height - hh));
            }
            // The lungomare is a rione of its own.
            zones.Add(new RectInt(0, SeaRows, width, PromenadeRows + RoadWidth));

            BuildHospital(map, quarters[kinds[2] == QuarterKind.Quartieri ? 2 : 0]);

            var baseLot = PickLot(homeLots, quarters[1].center);
            homeLots.Remove(baseLot);
            shopLots.Remove(baseLot);
            properties.RemoveAll(p => p.Rect.Equals(baseLot));
            BuildBase(map, baseLot);

            PlaceShops(map);
            DecorateStreets(map, quarters);
            HangLaundry(map);

            for (int i = 0; i < properties.Count; i++) properties[i].Id = i;
            props = properties;
            playerBase = new RectInt(baseLot.x + 1, baseLot.y + 1, baseLot.width - 2, baseLot.height - 2);
            return map;
        }

        // ---------- Quartieri: the vicoli grid ----------

        void BuildQuartieri(Map map, RectInt area)
        {
            // Long north-south vicoli every 8-12 cells, east-west crossings every 13-20.
            var cols = Cuts(area.x, area.xMax, 8, 12);
            var rows = Cuts(area.y, area.yMax, 13, 20);
            foreach (int x in cols) Fill(map, new RectInt(x, area.y, VicoloWidth, area.height), TerrainType.Sidewalk);
            foreach (int y in rows) Fill(map, new RectInt(area.x, y, area.width, VicoloWidth), TerrainType.Sidewalk);

            var xStarts = new List<int> { area.x };
            foreach (int x in cols) xStarts.Add(x + VicoloWidth);
            var xEnds = new List<int>(cols) { area.xMax };
            var yStarts = new List<int> { area.y };
            foreach (int y in rows) yStarts.Add(y + VicoloWidth);
            var yEnds = new List<int>(rows) { area.yMax };

            for (int i = 0; i < xStarts.Count; i++)
            for (int j = 0; j < yStarts.Count; j++)
            {
                var block = new RectInt(xStarts[i], yStarts[j], xEnds[i] - xStarts[i], yEnds[j] - yStarts[j]);
                if (block.width >= 4 && block.height >= 4) BuildTerrace(map, block);
            }
        }

        List<int> Cuts(int start, int end, int min, int max)
        {
            var cuts = new List<int>();
            int pos = start + rng.Next(min, max + 1);
            while (pos < end - min)
            {
                cuts.Add(pos);
                pos += VicoloWidth + rng.Next(min, max + 1);
            }
            return cuts;
        }

        /// <summary>A block of buildings packed wall to wall, each opening onto a vicolo.</summary>
        void BuildTerrace(Map map, RectInt block)
        {
            int y = block.y;
            while (y < block.yMax - 1)
            {
                int h = Mathf.Min(rng.Next(5, 9), block.yMax - y);
                if (block.yMax - (y + h - 1) < 4) h = block.yMax - y; // don't leave a sliver
                var lot = new RectInt(block.x, y, block.width, h);
                if (lot.height >= 4)
                {
                    if (rng.NextDouble() < 0.07) BuildCourtyard(map, lot);
                    else
                    {
                        // Doors on the long sides face the vicoli: the bassi open straight onto the alley.
                        bool west = rng.NextDouble() < 0.5;
                        var door = new Vector2Int(west ? lot.xMin : lot.xMax - 1, lot.y + rng.Next(1, lot.height - 1));
                        BuildBuilding(map, lot, partitions: lot.width >= 8, door);
                        AddProperty(lot, door, PropertyKind.Home);
                        Furnish(map, lot);
                        if (lot.width >= 6 && lot.height >= 5) shopLots.Add(lot);
                        if (lot.width >= 8 && lot.height >= 7 && lot.width <= 13 && lot.height <= 11) homeLots.Add(lot);

                        // A plastic chair outside the basso, for whoever's watching the street.
                        var outside = door + (west ? Vector2Int.left : Vector2Int.right);
                        if (rng.NextDouble() < 0.35 && map.InBounds(outside) && map.GetBuilding(outside) == null)
                        {
                            var chair = outside + (rng.NextDouble() < 0.5 ? Vector2Int.up : Vector2Int.down);
                            if (map.InBounds(chair) && map.GetBuilding(chair) == null && map.GetTerrain(chair.x, chair.y) == TerrainType.Sidewalk)
                                map.SetBuilding(chair.x, chair.y, BuildingDefs.PlasticChair);
                        }
                    }
                }
                y += h - 1; // shared wall with the next building
            }
        }

        void BuildCourtyard(Map map, RectInt lot)
        {
            foreach (var c in GridUtil.Cells(lot, false))
            {
                map.SetBuilding(c.x, c.y, null);
                map.SetTerrain(c.x, c.y, rng.NextDouble() < 0.6 ? TerrainType.Ground : TerrainType.Grass);
                if (rng.NextDouble() < 0.04) map.SetBuilding(c.x, c.y, BuildingDefs.Rubble);
            }
            var mid = Vector2Int.RoundToInt(lot.center);
            if (rng.NextDouble() < 0.6) map.SetBuilding(mid.x, mid.y, BuildingDefs.Tree);
        }

        void BuildBuilding(Map map, RectInt lot, bool partitions, Vector2Int door)
        {
            byte style = (byte)rng.Next(StyleCount);
            foreach (var c in GridUtil.Cells(lot, outlineOnly: false))
            {
                bool edge = c.x == lot.xMin || c.x == lot.xMax - 1 || c.y == lot.yMin || c.y == lot.yMax - 1;
                map.SetTerrain(c.x, c.y, TerrainType.Floor);
                map.SetStyle(c.x, c.y, style);
                map.SetBuilding(c.x, c.y, edge ? BuildingDefs.Wall : null);
            }
            map.SetBuilding(door.x, door.y, BuildingDefs.Door);

            if (partitions && lot.width >= 8 && lot.height >= 5 && rng.NextDouble() < 0.7)
            {
                int wx = lot.x + rng.Next(3, lot.width - 3);
                int doorY = lot.y + rng.Next(1, lot.height - 1);
                for (int y = lot.y + 1; y < lot.yMax - 1; y++)
                    map.SetBuilding(wx, y, y == doorY ? BuildingDefs.Door : BuildingDefs.Wall);
            }
        }

        /// <summary>Somebody lives here: beds, a table, a sofa and the TV, the kitchen, a plant, a rug.</summary>
        void Furnish(Map map, RectInt lot)
        {
            // Only along the inside of the outer walls, never next to a door, so rooms stay walkable.
            var spots = new List<Vector2Int>();
            for (int y = lot.y + 1; y < lot.yMax - 1; y++)
            for (int x = lot.x + 1; x < lot.xMax - 1; x++)
            {
                bool againstWall = x == lot.x + 1 || x == lot.xMax - 2 || y == lot.y + 1 || y == lot.yMax - 2;
                if (!againstWall || map.GetBuilding(x, y) != null) continue;
                bool nearDoor = false;
                for (int dy = -1; dy <= 1 && !nearDoor; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (map.GetBuilding(x + dx, y + dy) == BuildingDefs.Door) { nearDoor = true; break; }
                if (!nearDoor) spots.Add(new Vector2Int(x, y));
            }
            for (int i = spots.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (spots[i], spots[j]) = (spots[j], spots[i]);
            }

            var items = new List<BuildingDef> { BuildingDefs.Bed, BuildingDefs.Table, BuildingDefs.Wardrobe };
            if (rng.NextDouble() < 0.6) items.Add(BuildingDefs.Bed);
            if (rng.NextDouble() < 0.7) items.Add(BuildingDefs.Sofa);
            if (rng.NextDouble() < 0.6) items.Add(BuildingDefs.Television);
            if (rng.NextDouble() < 0.6) { items.Add(BuildingDefs.KitchenCounter); items.Add(BuildingDefs.Fridge); }
            if (rng.NextDouble() < 0.5) items.Add(BuildingDefs.Plant);
            if (rng.NextDouble() < 0.4) items.Add(BuildingDefs.HomeShrine);
            if (rng.NextDouble() < 0.4) items.Add(BuildingDefs.Armchair);
            int max = Mathf.Min(items.Count, spots.Count / 2); // leave some wall free
            for (int i = 0; i < max; i++) map.SetBuilding(spots[i].x, spots[i].y, items[i]);

            var mid = Vector2Int.RoundToInt(lot.center);
            if (rng.NextDouble() < 0.5 && map.GetBuilding(mid) == null && map.GetTerrain(mid.x, mid.y) == TerrainType.Floor)
                map.SetBuilding(mid.x, mid.y, BuildingDefs.Rug);
        }

        void AddProperty(RectInt lot, Vector2Int door, PropertyKind kind)
        {
            properties.Add(new Property { Rect = lot, Door = door, Kind = kind });
        }

        // ---------- Borgo: church piazza, market street, vicoli ----------

        void BuildBorgo(Map map, RectInt area)
        {
            int pw = Mathf.Min(26, area.width - 4), ph = Mathf.Min(22, area.height / 2);
            var piazza = new RectInt(area.x + (area.width - pw) / 2, area.yMax - ph, pw, ph);
            var rest = new RectInt(area.x, area.y, area.width, area.height - ph - 1);

            BuildQuartieri(map, rest);
            BuildChurch(map, piazza);

            // Market street: a wider paved street with stalls along both sides.
            int my = rest.y + rest.height / 2;
            Fill(map, new RectInt(rest.x, my, rest.width, 4), TerrainType.Paving);
            for (int x = rest.x; x < rest.xMax; x++)
            {
                for (int y = my; y < my + 4; y++) map.SetBuilding(x, y, null);
                if (x % 3 != 0) continue;
                map.SetBuilding(x, my, BuildingDefs.MarketStall);
                map.SetBuilding(x, my + 3, BuildingDefs.MarketStall);
            }
            // Buildings cut in half by the market street are no longer properties.
            var street = new RectInt(rest.x, my, rest.width, 4);
            properties.RemoveAll(p => p.Rect.Overlaps(street));
            shopLots.RemoveAll(l => l.Overlaps(street));
            homeLots.RemoveAll(l => l.Overlaps(street));
        }

        void BuildChurch(Map map, RectInt area)
        {
            foreach (var c in GridUtil.Cells(area, false))
            {
                map.SetTerrain(c.x, c.y, TerrainType.Paving);
                map.SetBuilding(c.x, c.y, null);
            }

            int churchH = Mathf.Max(9, area.height / 2 + 1);
            int churchW = Mathf.Min(area.width - 6, 14);
            var church = new RectInt(area.x + (area.width - churchW) / 2, area.yMax - churchH, churchW, churchH);
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
            properties.RemoveAll(p => p.Rect.Overlaps(area));
            shopLots.RemoveAll(l => l.Overlaps(area));
            homeLots.RemoveAll(l => l.Overlaps(area));
            properties.Add(new Property { Rect = church, Door = new Vector2Int(doorX, church.yMin), Kind = PropertyKind.Civic, Name = "The parish church" });

            var plaza = new RectInt(area.x, area.y, area.width, area.height - churchH);
            var mid = Vector2Int.RoundToInt(plaza.center);
            map.SetBuilding(mid.x, mid.y, BuildingDefs.Fountain);
            map.SetBuilding(plaza.xMin + 2, plaza.yMin + 1, BuildingDefs.Tree);
            map.SetBuilding(plaza.xMax - 3, plaza.yMin + 1, BuildingDefs.Tree);
            map.SetBuilding(mid.x - 3, mid.y, BuildingDefs.Bench);
            map.SetBuilding(mid.x + 3, mid.y, BuildingDefs.Bench);
            map.SetBuilding(plaza.xMin + 1, plaza.yMax - 1, BuildingDefs.StreetLamp);
            map.SetBuilding(plaza.xMax - 2, plaza.yMax - 1, BuildingDefs.StreetLamp);
        }

        // ---------- Vele: housing megablocks on wasteland ----------

        void BuildVeleQuarter(Map map, RectInt area)
        {
            foreach (var c in GridUtil.Cells(area, false))
            {
                map.SetTerrain(c.x, c.y, rng.NextDouble() < 0.75 ? TerrainType.Ground : TerrainType.Grass);
                if (rng.NextDouble() < 0.015) map.SetBuilding(c.x, c.y, BuildingDefs.Rubble);
            }
            int slabs = area.width > 60 ? 3 : 2;
            int slabW = (area.width - (slabs + 1) * 6) / slabs;
            for (int i = 0; i < slabs; i++)
            {
                var slab = new RectInt(area.x + 6 + i * (slabW + 6), area.y + 6, slabW, area.height - 12);
                if (slab.width >= 12 && slab.height >= 14) BuildVele(map, slab);
            }
            for (int i = 0; i < 6; i++)
            {
                var c = new Vector2Int(rng.Next(area.xMin, area.xMax), rng.Next(area.yMin, area.yMax));
                if (map.GetBuilding(c) == null && map.GetTerrain(c.x, c.y) != TerrainType.Floor)
                    map.SetBuilding(c.x, c.y, i % 2 == 0 ? BuildingDefs.ParkedCar : BuildingDefs.Tree);
            }
        }

        void BuildVele(Map map, RectInt shell)
        {
            const byte concrete = 4;
            foreach (var c in GridUtil.Cells(shell, false))
            {
                bool edge = c.x == shell.xMin || c.x == shell.xMax - 1 || c.y == shell.yMin || c.y == shell.yMax - 1;
                map.SetTerrain(c.x, c.y, TerrainType.Floor);
                map.SetStyle(c.x, c.y, concrete);
                map.SetBuilding(c.x, c.y, edge ? BuildingDefs.Wall : null);
            }

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
                    bool flatWall = row == 2 || row == 4 || (x - shell.xMin) % 5 == 0;
                    if (flatWall) map.SetBuilding(x, y, BuildingDefs.Wall);
                }
                if (!corridor) continue;
                for (int x = shell.xMin + 3; x < shell.xMax - 2; x += 5)
                {
                    if (map.GetBuilding(x, y + 1) == BuildingDefs.Wall) map.SetBuilding(x, y + 1, BuildingDefs.Door);
                    if (map.GetBuilding(x, y - 1) == BuildingDefs.Wall) map.SetBuilding(x, y - 1, BuildingDefs.Door);
                }
                map.SetBuilding(shell.xMin, y, BuildingDefs.Door);
                map.SetBuilding(shell.xMax - 1, y, BuildingDefs.Door);
            }
            map.SetBuilding(midX, shell.yMin, BuildingDefs.Door);
            map.SetBuilding(midX, shell.yMax - 1, BuildingDefs.Door);
            foreach (var c in GridUtil.Cells(shell, false))
                if (map.GetBuilding(c) == null && rng.NextDouble() < 0.02) map.SetBuilding(c.x, c.y, BuildingDefs.Rubble);
        }

        // ---------- Hospital ----------

        void BuildHospital(Map map, RectInt quarter)
        {
            // In the corner of a quarter, its doors opening onto the pavement of the road below.
            var lot = new RectInt(quarter.x + 2, quarter.y + 2, Mathf.Min(16, quarter.width - 4), 10);
            var door = new Vector2Int(lot.x + lot.width / 2, lot.yMin);
            foreach (var c in GridUtil.Cells(new RectInt(lot.x - 1, quarter.y + 1, lot.width + 2, lot.yMax - quarter.y), false))
                if (map.InBounds(c))
                {
                    map.SetBuilding(c.x, c.y, null);
                    map.SetTerrain(c.x, c.y, TerrainType.Paving);
                }
            const byte white = 5;
            foreach (var c in GridUtil.Cells(lot, false))
            {
                bool edge = c.x == lot.xMin || c.x == lot.xMax - 1 || c.y == lot.yMin || c.y == lot.yMax - 1;
                map.SetTerrain(c.x, c.y, TerrainType.Marble);
                map.SetStyle(c.x, c.y, white);
                map.SetBuilding(c.x, c.y, edge ? BuildingDefs.Wall : null);
            }
            map.SetBuilding(door.x, door.y, BuildingDefs.Door);
            map.SetBuilding(door.x - 1, door.y, BuildingDefs.Door);
            map.SetBuilding(door.x, door.y + 3, BuildingDefs.HospitalDesk);
            for (int x = lot.xMin + 2; x < lot.xMax - 2; x += 3) map.SetBuilding(x, lot.yMax - 2, BuildingDefs.Bed);

            var footprint = new RectInt(lot.x - 1, quarter.y, lot.width + 2, lot.yMax - quarter.y + 1);
            properties.RemoveAll(p => p.Rect.Overlaps(footprint));
            shopLots.RemoveAll(l => l.Overlaps(footprint));
            homeLots.RemoveAll(l => l.Overlaps(footprint));
            properties.Add(new Property { Rect = lot, Door = door, Kind = PropertyKind.Civic, Name = "Ospedale" });
        }

        // ---------- The crew's home and the shops ----------

        static RectInt PickLot(List<RectInt> lots, Vector2 near)
        {
            RectInt best = lots.Count > 0 ? lots[0] : new RectInt((int)near.x, (int)near.y, 9, 8);
            float bestDist = float.MaxValue;
            foreach (var lot in lots)
            {
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
            // One big room, its door onto the alley.
            var door = new Vector2Int(lot.xMin, lot.y + lot.height / 2);
            if (!map.InBounds(door.x - 1, door.y) || !map.IsPassable(door.x - 1, door.y) || map.GetTerrain(door.x - 1, door.y) == TerrainType.Floor)
                door = new Vector2Int(lot.xMax - 1, lot.y + lot.height / 2);
            BuildBuilding(map, lot, partitions: false, door);
            properties.Add(new Property { Rect = lot, Door = door, Kind = PropertyKind.Home, OwnedByPlayer = true, Name = "The family basso" });

            for (int i = 0; i < 3 && lot.x + 2 + i * 2 < lot.xMax - 1; i++)
                map.SetBuilding(lot.x + 2 + i * 2, lot.yMax - 2, BuildingDefs.Bed);
            int kitchenX = door.x == lot.xMin ? lot.xMax - 2 : lot.xMin + 1;
            map.SetBuilding(kitchenX, lot.y + 1, BuildingDefs.Stove);
            map.SetBuilding(kitchenX, lot.y + 2, BuildingDefs.Pantry);
            var mid = Vector2Int.RoundToInt(lot.center);
            map.SetBuilding(mid.x, mid.y - 1, BuildingDefs.Table);
            int shrineX = door.x == lot.xMin ? lot.xMin + 1 : lot.xMax - 2;
            map.SetBuilding(shrineX, lot.y + 1, BuildingDefs.Shrine);
        }

        void PlaceShops(Map map)
        {
            var shops = new[]
            {
                BuildingDefs.PizzeriaCounter, BuildingDefs.PizzeriaCounter, BuildingDefs.PizzeriaCounter, BuildingDefs.PizzeriaCounter,
                BuildingDefs.SalumeriaCounter, BuildingDefs.SalumeriaCounter, BuildingDefs.FarmaciaCounter,
                BuildingDefs.BarCounter, BuildingDefs.BarCounter, BuildingDefs.BarCounter, BuildingDefs.BarCounter,
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
                if (map.GetTerrain(mid.x, mid.y) != TerrainType.Floor || map.GetBuilding(mid.x, mid.y) != null) continue;
                bool clear = true;
                foreach (var c in GridUtil.CellsAround(mid, 1))
                    if (map.GetBuilding(c) == BuildingDefs.Wall) clear = false;
                if (!clear) continue;
                map.SetBuilding(mid.x, mid.y, shops[placed++]);
                var prop = properties.Find(p => p.Rect.Equals(lot));
                if (prop != null) prop.Kind = PropertyKind.Shop;
            }
        }

        // ---------- Street furniture ----------

        void DecorateStreets(Map map, List<RectInt> quarters)
        {
            // Lamps and parked scooters along the pavements of the main roads.
            foreach (var q in quarters)
            {
                int i = 0;
                foreach (var c in GridUtil.Cells(q, outlineOnly: true))
                {
                    i++;
                    if (map.GetBuilding(c) != null || map.GetTerrain(c.x, c.y) != TerrainType.Sidewalk) continue;
                    if (i % 9 == 0) map.SetBuilding(c.x, c.y, BuildingDefs.StreetLamp);
                    else if (rng.NextDouble() < 0.03) map.SetBuilding(c.x, c.y, BuildingDefs.ParkedScooter);
                    else if (rng.NextDouble() < 0.012) map.SetBuilding(c.x, c.y, BuildingDefs.Bin);
                }
            }

            // In the vicoli: shrines and lamps on the walls, bins and scooters against them.
            for (int y = 1; y < map.Height - 1; y++)
            for (int x = 1; x < map.Width - 1; x++)
            {
                var b = map.GetBuilding(x, y);
                if (b == BuildingDefs.Wall && FacesAlley(map, x, y))
                {
                    double r = rng.NextDouble();
                    if (r < 0.012) map.SetBuilding(x, y, BuildingDefs.Shrine);
                    else if (r < 0.06) map.SetDecor(x, y, MapDecor.WallLamp);
                }
                else if (b == null && map.GetTerrain(x, y) == TerrainType.Sidewalk && NextToWall(map, x, y) && !NextToDoor(map, x, y))
                {
                    double r = rng.NextDouble();
                    if (r < 0.012) map.SetBuilding(x, y, BuildingDefs.ParkedScooter);
                    else if (r < 0.02) map.SetBuilding(x, y, BuildingDefs.Bin);
                }
            }

            // Parked cars along the kerb of the asphalt roads, leaving the lanes free.
            for (int y = 1; y < map.Height - 1; y++)
            for (int x = 1; x < map.Width - 1; x++)
            {
                if (map.GetTerrain(x, y) != TerrainType.Road || map.GetBuilding(x, y) != null) continue;
                bool kerb = IsT(map, x + 1, y, TerrainType.Sidewalk) || IsT(map, x - 1, y, TerrainType.Sidewalk) ||
                            IsT(map, x, y + 1, TerrainType.Sidewalk) || IsT(map, x, y - 1, TerrainType.Sidewalk);
                if (kerb && !NearJunction(map, x, y) && rng.NextDouble() < 0.1) map.SetBuilding(x, y, BuildingDefs.ParkedCar);
            }
        }

        // Laundry strung across the vicoli between facing buildings.
        void HangLaundry(Map map)
        {
            for (int y = 1; y < map.Height - 1; y++)
            for (int x = 1; x < map.Width - 2; x++)
            {
                if (!IsT(map, x, y, TerrainType.Sidewalk) || !IsT(map, x + 1, y, TerrainType.Sidewalk)) continue;
                bool westWall = map.GetBuilding(x - 1, y) == BuildingDefs.Wall;
                bool eastWall = map.InBounds(x + 2, y) && map.GetBuilding(x + 2, y) == BuildingDefs.Wall;
                if (!westWall || !eastWall || rng.NextDouble() > 0.3) continue;
                map.SetDecor(x, y, MapDecor.Laundry);
                map.SetDecor(x + 1, y, MapDecor.Laundry);
            }
        }

        static bool FacesAlley(Map map, int x, int y) =>
            IsT(map, x - 1, y, TerrainType.Sidewalk) || IsT(map, x + 1, y, TerrainType.Sidewalk) ||
            IsT(map, x, y - 1, TerrainType.Sidewalk);

        static bool NextToWall(Map map, int x, int y) =>
            map.GetBuilding(x - 1, y) == BuildingDefs.Wall || map.GetBuilding(x + 1, y) == BuildingDefs.Wall;

        static bool NextToDoor(Map map, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (map.InBounds(x + dx, y + dy) && map.GetBuilding(x + dx, y + dy) == BuildingDefs.Door) return true;
            return false;
        }

        // Keep junctions clear of parked cars.
        static bool NearJunction(Map map, int x, int y)
        {
            for (int dy = -3; dy <= 3; dy++)
            for (int dx = -3; dx <= 3; dx++)
                if (CountRoad(map, x + dx, y + dy) >= 4) return true;
            return false;
        }

        static bool IsT(Map map, int x, int y, TerrainType t) => map.InBounds(x, y) && map.GetTerrain(x, y) == t;
        static bool IsRoad(Map map, int x, int y) => IsT(map, x, y, TerrainType.Road);

        static int CountRoad(Map map, int x, int y)
        {
            int n = 0;
            if (IsRoad(map, x + 1, y)) n++;
            if (IsRoad(map, x - 1, y)) n++;
            if (IsRoad(map, x, y + 1)) n++;
            if (IsRoad(map, x, y - 1)) n++;
            return n;
        }

        static void Fill(Map map, RectInt r, TerrainType t)
        {
            foreach (var c in GridUtil.Cells(r, outlineOnly: false))
                if (map.InBounds(c)) map.SetTerrain(c.x, c.y, t);
        }
    }
}
