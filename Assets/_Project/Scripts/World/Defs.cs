using UnityEngine;

namespace OSistema.World
{
    public enum TerrainType : byte { Ground, Road, Sidewalk, Floor }

    public enum PlaceMode { Single, Outline, Fill }

    public enum ShopKind { None, Pizzeria, Salumeria, Farmacia, Bar, Macelleria, Tabaccheria }

    public static class TerrainDefs
    {
        public static float MoveCost(TerrainType t) => t switch
        {
            TerrainType.Ground => 1.25f, // rubble, dirt, courtyards
            _ => 1f,
        };

        public static Color32 BaseColor(TerrainType t) => t switch
        {
            TerrainType.Road => new Color32(52, 52, 58, 255),      // asphalt
            TerrainType.Sidewalk => new Color32(84, 84, 92, 255),  // basolato (volcanic stone)
            TerrainType.Floor => new Color32(170, 150, 122, 255),  // ceramic tiles
            _ => new Color32(112, 98, 76, 255),                    // dirt
        };

        public static string Label(TerrainType t) => t switch
        {
            TerrainType.Road => "Road",
            TerrainType.Sidewalk => "Sidewalk",
            TerrainType.Floor => "Floor",
            _ => "Ground",
        };
    }

    /// <summary>
    /// Static description of a building type. Defined in code for now; the plan is to load
    /// these from JSON so the game can be modded.
    /// </summary>
    public sealed class BuildingDef
    {
        public string Id;
        public string Label;
        public bool Passable;
        public float PathCost = 1f;
        public int WorkToBuild;
        public Color32 Color;
        public PlaceMode PlaceMode;
        public ShopKind Shop;
    }

    public static class BuildingDefs
    {
        public static readonly BuildingDef Wall = new BuildingDef
        {
            Id = "wall", Label = "Wall", Passable = false, WorkToBuild = 300,
            Color = new Color32(198, 170, 112, 255), // yellow tuff plaster
            PlaceMode = PlaceMode.Outline,
        };

        public static readonly BuildingDef Door = new BuildingDef
        {
            Id = "door", Label = "Door", Passable = true, PathCost = 1.6f, WorkToBuild = 400,
            Color = new Color32(104, 66, 38, 255),
            PlaceMode = PlaceMode.Single,
        };

        public static readonly BuildingDef Bed = new BuildingDef
        {
            Id = "bed", Label = "Bed", Passable = true, PathCost = 1.8f, WorkToBuild = 250,
            Color = new Color32(150, 44, 44, 255),
            PlaceMode = PlaceMode.Fill,
        };

        public static readonly BuildingDef Table = new BuildingDef
        {
            Id = "table", Label = "Table", Passable = false, WorkToBuild = 200,
            Color = new Color32(128, 92, 58, 255),
            PlaceMode = PlaceMode.Fill,
        };

        public static readonly BuildingDef Stove = new BuildingDef
        {
            Id = "stove", Label = "Stove", Passable = false, WorkToBuild = 500,
            Color = new Color32(60, 60, 64, 255),
            PlaceMode = PlaceMode.Single,
        };

        public static readonly BuildingDef Pantry = new BuildingDef
        {
            Id = "pantry", Label = "Pantry", Passable = false, WorkToBuild = 300,
            Color = new Color32(92, 120, 70, 255),
            PlaceMode = PlaceMode.Single,
        };

        // Shop counters: generated with the map, not buildable.
        public static readonly BuildingDef PizzeriaCounter = new BuildingDef
        {
            Id = "pizzeria_counter", Label = "Pizzeria", Passable = false, WorkToBuild = 600,
            Color = new Color32(222, 92, 40, 255), Shop = ShopKind.Pizzeria,
        };

        public static readonly BuildingDef SalumeriaCounter = new BuildingDef
        {
            Id = "salumeria_counter", Label = "Salumeria", Passable = false, WorkToBuild = 600,
            Color = new Color32(232, 196, 92, 255), Shop = ShopKind.Salumeria,
        };

        public static readonly BuildingDef FarmaciaCounter = new BuildingDef
        {
            Id = "farmacia_counter", Label = "Farmacia", Passable = false, WorkToBuild = 600,
            Color = new Color32(60, 190, 120, 255), Shop = ShopKind.Farmacia,
        };

        // Businesses that only matter for pizzo (for now).
        public static readonly BuildingDef BarCounter = new BuildingDef
        {
            Id = "bar_counter", Label = "Bar", Passable = false, WorkToBuild = 600,
            Color = new Color32(170, 120, 200, 255), Shop = ShopKind.Bar,
        };

        public static readonly BuildingDef MacelleriaCounter = new BuildingDef
        {
            Id = "macelleria_counter", Label = "Macelleria", Passable = false, WorkToBuild = 600,
            Color = new Color32(190, 60, 80, 255), Shop = ShopKind.Macelleria,
        };

        public static readonly BuildingDef TabaccheriaCounter = new BuildingDef
        {
            Id = "tabaccheria_counter", Label = "Tabaccheria", Passable = false, WorkToBuild = 600,
            Color = new Color32(80, 140, 200, 255), Shop = ShopKind.Tabaccheria,
        };

        // What's left of a shop after arson. Reverts to the counter when the shop reopens.
        public static readonly BuildingDef BurnedShop = new BuildingDef
        {
            Id = "burned_shop", Label = "Burned shop", Passable = false, WorkToBuild = 600,
            Color = new Color32(38, 30, 26, 255),
        };

        /// <summary>A hidden room behind a wall: where the boss hides during the latitanza.</summary>
        public static readonly BuildingDef Bunker = new BuildingDef
        {
            Id = "bunker", Label = "Bunker", Passable = true, WorkToBuild = 2500,
            Color = new Color32(70, 60, 50, 255),
            PlaceMode = PlaceMode.Single,
        };

        public static readonly BuildingDef[] Buildable = { Wall, Door, Bed, Table, Stove, Pantry, Bunker };

        /// <summary>Every def, in a fixed order used by save files. Only ever append to this list.</summary>
        public static readonly BuildingDef[] All =
        {
            Wall, Door, Bed, Table, Stove, Pantry, PizzeriaCounter, SalumeriaCounter, FarmaciaCounter,
            BarCounter, MacelleriaCounter, TabaccheriaCounter, BurnedShop, Bunker,
        };

        public static BuildingDef ById(string id)
        {
            foreach (var d in All)
                if (d.Id == id) return d;
            return null;
        }
    }

    /// <summary>Prices in euros.</summary>
    public static class Prices
    {
        public const int Pizza = 6;
        public const int Groceries = 5;  // one unit cooks two meals
        public const int Medicine = 15;
        public const int ProductWholesale = 10; // per unit, from the supplier
        public const int ProductStreet = 25;    // per unit, at the piazza
        public const int RecruitBonus = 100;
        public const int JailedFamilySupport = 100; // weekly, per crew member inside
        public const int Pistol = 400;
    }
}
