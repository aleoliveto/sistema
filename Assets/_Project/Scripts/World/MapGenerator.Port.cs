using System.Collections.Generic;
using UnityEngine;

namespace OSistema.World
{
    /// <summary>
    /// Il Porto: piers into the sea and a quay stacked with crates along the bottom, the coast road,
    /// a belt of big warehouses on wide service roads, and above them the dockworkers' houses with
    /// their own bars and tabacchi. Wider, emptier and more open than the Quartieri: good for
    /// scooters, bad for hiding.
    /// </summary>
    public sealed partial class MapGenerator
    {
        public const int PortWidth = 160, PortHeight = 120;
        public const int PortRoadY = 24;   // the coast road: the way in from the Quartieri, at the west edge

        public static readonly string[] PortZoneNames =
        {
            "Il Molo", "Magazzini Ovest", "Magazzini Est", "Vico dei Marinai", "Case del Carmine", "Rampe del Porto", "Calata Vecchia",
        };

        public Map GeneratePort(int width, int height, out List<RectInt> zones, out List<Property> props)
        {
            var map = new Map(width, height);
            zones = new List<RectInt>();
            const int water = 14, quayTop = 24, warehouseTop = 61, mainRoadY = 61;

            // ---- The sea and the piers ----
            Fill(map, new RectInt(0, 0, width, water), TerrainType.Water);
            for (int x = 8; x < width - 8; x += 30)
                Fill(map, new RectInt(x, 3, 5, water - 3), TerrainType.Paving);

            // ---- The quay: paving, crates, the odd parked car, lamps ----
            Fill(map, new RectInt(0, water, width, quayTop - water), TerrainType.Paving);
            for (int x = 0; x < width; x++)
            {
                bool pier = false;
                for (int px = 8; px < width - 8; px += 30) if (x >= px && x < px + 5) pier = true;
                if (!pier && x % 17 != 3) map.SetBuilding(x, water, BuildingDefs.Railing);   // gaps to tie up a boat
                if (x % 12 == 6) map.SetBuilding(x, quayTop - 1, BuildingDefs.StreetLamp);
            }
            // Stacks of crates in the yards, with lanes between them.
            for (int x = 4; x < width - 6; x += 9)
            for (int y = water + 3; y < quayTop - 3; y += 4)
            {
                if (rng.NextDouble() < 0.35) continue;
                for (int dx = 0; dx < 3; dx++)
                for (int dy = 0; dy < 2; dy++)
                    if (map.GetBuilding(x + dx, y + dy) == null) map.SetBuilding(x + dx, y + dy, BuildingDefs.Crates);
            }
            zones.Add(new RectInt(0, 0, width, quayTop));

            // ---- The coast road, and the sidewalk on its north side ----
            Fill(map, new RectInt(0, PortRoadY, width, RoadWidth), TerrainType.Road);
            Fill(map, new RectInt(0, PortRoadY + RoadWidth, width, 1), TerrainType.Sidewalk);
            for (int x = 0; x < width; x++)
                if (rng.NextDouble() < 0.06 && x > 4) map.SetBuilding(x, PortRoadY, BuildingDefs.ParkedCar);

            // ---- Warehouses: big sheds on wide service roads ----
            int beltY = PortRoadY + RoadWidth + 1;
            Fill(map, new RectInt(0, mainRoadY, width, RoadWidth), TerrainType.Road);
            int mid = width / 2 - 1;
            Fill(map, new RectInt(mid, PortRoadY, RoadWidth, height - PortRoadY), TerrainType.Road);
            for (int x = 2; x < width - 10; )
            {
                int w = rng.Next(14, 20);
                if (x < mid && x + w > mid - 1) { x = mid + RoadWidth + 1; continue; }
                if (x + w > width - 2) break;
                for (int y = beltY + 1; y < mainRoadY - 6; )
                {
                    int h = rng.Next(9, 13);
                    if (y + h > mainRoadY - 1) break;
                    var lot = new RectInt(x, y, w, h);
                    var door = new Vector2Int(x + w / 2, y);   // the loading door on the road side
                    BuildBuilding(map, lot, partitions: false, door);
                    AddProperty(lot, door, PropertyKind.Home);
                    // Inside: crates and pallets along the walls.
                    for (int k = 0; k < 6; k++)
                    {
                        var c = new Vector2Int(rng.Next(lot.x + 1, lot.xMax - 1), rng.Next(lot.y + 2, lot.yMax - 1));
                        if (map.GetBuilding(c) == null) map.SetBuilding(c.x, c.y, BuildingDefs.Crates);
                    }
                    y += h + 3;
                }
                x += w + 4;
            }
            // The roads between the sheds are concrete yards.
            for (int y = beltY; y < mainRoadY; y++)
            for (int x = 0; x < width; x++)
                if (map.GetTerrain(x, y) == TerrainType.Ground) map.SetTerrain(x, y, TerrainType.Paving);
            zones.Add(new RectInt(0, beltY, mid, mainRoadY - beltY));
            zones.Add(new RectInt(mid, beltY, width - mid, mainRoadY - beltY));

            // ---- Above the main road: the dockworkers' houses, with their shops ----
            int housesY = mainRoadY + RoadWidth;
            Fill(map, new RectInt(0, housesY, width, 1), TerrainType.Sidewalk);
            var west = new RectInt(1, housesY + 1, mid - 2, height - housesY - 2);
            var east = new RectInt(mid + RoadWidth + 1, housesY + 1, width - mid - RoadWidth - 2, height - housesY - 2);
            BuildQuartieri(map, west);
            BuildQuartieri(map, east);
            int hh = (height - housesY) / 2;
            zones.Add(new RectInt(0, housesY, mid, hh));
            zones.Add(new RectInt(0, housesY + hh, mid, height - housesY - hh));
            zones.Add(new RectInt(mid, housesY, width - mid, hh));
            zones.Add(new RectInt(mid, housesY + hh, width - mid, height - housesY - hh));

            PlaceShops(map);
            DecorateStreets(map, new List<RectInt> { west, east });
            HangLaundry(map);

            for (int i = 0; i < properties.Count; i++) properties[i].Id = i;
            props = properties;
            return map;
        }
    }
}
