using UnityEngine;

namespace OSistema.World
{
    public enum TerrainType : byte { Ground, Road, Sidewalk, Floor, Paving, Marble, Grass, Water }

    public enum PlaceMode { Single, Outline, Fill }

    public enum ShopKind { None, Pizzeria, Salumeria, Farmacia, Bar, Macelleria, Tabaccheria, Mobili, Lusso, Moto }

    public static class TerrainDefs
    {
        public static float MoveCost(TerrainType t) => t switch
        {
            TerrainType.Ground => 1.25f, // rubble, dirt, courtyards
            TerrainType.Grass => 1.15f,
            _ => 1f,
        };

        public static Color32 BaseColor(TerrainType t) => t switch
        {
            TerrainType.Road => new Color32(52, 52, 58, 255),      // asphalt
            TerrainType.Sidewalk => new Color32(84, 84, 92, 255),  // basolato (volcanic stone)
            TerrainType.Floor => new Color32(170, 150, 122, 255),  // ceramic tiles
            TerrainType.Paving => new Color32(150, 145, 135, 255), // stone slabs of a piazza
            TerrainType.Marble => new Color32(225, 220, 210, 255), // church floor
            TerrainType.Grass => new Color32(70, 110, 55, 255),
            TerrainType.Water => new Color32(40, 90, 130, 255),
            _ => new Color32(112, 98, 76, 255),                    // dirt
        };

        public static string Label(TerrainType t) => t switch
        {
            TerrainType.Road => "Road",
            TerrainType.Sidewalk => "Sidewalk",
            TerrainType.Floor => "Floor",
            TerrainType.Paving => "Paving",
            TerrainType.Marble => "Marble",
            TerrainType.Grass => "Grass",
            TerrainType.Water => "Sea",
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
        public bool Cover;        // stops bullets: crouch behind it in a firefight
        public bool LightSource;  // lit at night
        public bool Generated;    // street furniture and the like: placed by the map generator only
        public float Beauty;      // how much nicer it makes a home (crew mood)
        public int Cost;          // euros, paid when the blueprint is placed
        public Vector2Int Size = Vector2Int.one;   // cells it covers, unturned (a bed is 1x2)
        public bool Filler;       // marks the other cells of something bigger than one cell (see Footprint)

        static readonly System.Collections.Generic.Dictionary<string, int> Costs = new System.Collections.Generic.Dictionary<string, int>
        {
            ["wall"] = 15, ["door"] = 50, ["bed"] = 150, ["table"] = 100, ["stove"] = 300, ["pantry"] = 150, ["bunker"] = 2000,
            ["sofa"] = 300, ["armchair"] = 150, ["tv"] = 400, ["wardrobe"] = 200, ["kitchen_counter"] = 250, ["fridge"] = 350,
            ["plant"] = 40, ["rug"] = 120, ["safe"] = 600, ["home_shrine"] = 100, ["plastic_chair"] = 15,
            ["bagging_table"] = 60, ["rolling_table"] = 35,
        };

        /// <summary>Walls, doors and the bunker are building work, paid where they go. Everything else is bought in a shop and kept in storage.</summary>
        public static bool BuiltInPlace(BuildingDef d) => d != null && (d.Id == "wall" || d.Id == "door" || d.Id == "bunker");

        public static int CostOf(BuildingDef d) => d != null && Costs.TryGetValue(d.Id, out var c) ? c : 0;
    }

    public static class BuildingDefs
    {
        public static readonly BuildingDef Wall = new BuildingDef
        {
            Id = "wall", Label = "Wall", Passable = false, WorkToBuild = 300, Cover = true,
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
            Id = "bed", Label = "Bed", Passable = true, PathCost = 1.8f, WorkToBuild = 250, Beauty = 1f,
            Color = new Color32(150, 44, 44, 255),
            PlaceMode = PlaceMode.Single, Size = new Vector2Int(1, 2),
        };

        public static readonly BuildingDef Table = new BuildingDef
        {
            Id = "table", Label = "Table", Passable = false, WorkToBuild = 200, Cover = true, Beauty = 1f,
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

        // ---- The street: generated with the map ----
        public static readonly BuildingDef Tree = Street("tree", "Tree", new Color32(60, 105, 50, 255), cover: true);
        public static readonly BuildingDef ParkedCar = Street("parked_car", "Parked car", new Color32(140, 40, 40, 255), cover: true);
        public static readonly BuildingDef ParkedScooter = Street("parked_scooter", "Scooter", new Color32(200, 200, 205, 255), cover: false);
        public static readonly BuildingDef Bench = Street("bench", "Bench", new Color32(120, 90, 60, 255), cover: true);
        public static readonly BuildingDef Bin = Street("bin", "Bin", new Color32(60, 90, 70, 255), cover: true);
        public static readonly BuildingDef StreetLamp = Street("street_lamp", "Street lamp", new Color32(50, 50, 55, 255), cover: false, light: true);
        public static readonly BuildingDef MarketStall = Street("market_stall", "Market stall", new Color32(200, 60, 50, 255), cover: true);
        public static readonly BuildingDef Shrine = Street("shrine", "Edicola votiva", new Color32(70, 110, 190, 255), cover: true, light: true);
        public static readonly BuildingDef Altar = Street("altar", "Altar", new Color32(230, 220, 190, 255), cover: true);
        public static readonly BuildingDef Pew = Street("pew", "Pew", new Color32(110, 75, 45, 255), cover: true);
        public static readonly BuildingDef Fountain = Street("fountain", "Fountain", new Color32(150, 150, 145, 255), cover: true);
        public static readonly BuildingDef Railing = Street("railing", "Railing", new Color32(60, 60, 64, 255), cover: true);
        public static readonly BuildingDef HospitalDesk = Street("hospital_desk", "Pronto soccorso", new Color32(235, 235, 235, 255), cover: true, light: true);
        public static readonly BuildingDef PlasticChair = new BuildingDef
        {
            Id = "plastic_chair", Label = "Plastic chair", Passable = true, PathCost = 1.5f, WorkToBuild = 50,
            Color = new Color32(235, 235, 230, 255), Generated = true,
        };

        public static readonly BuildingDef Rubble = new BuildingDef
        {
            Id = "rubble", Label = "Rubble", Passable = true, PathCost = 2f, WorkToBuild = 100,
            Color = new Color32(120, 112, 100, 255), Generated = true,
        };

        static BuildingDef Street(string id, string label, Color32 color, bool cover, bool light = false) => new BuildingDef
        {
            Id = id, Label = label, Passable = false, WorkToBuild = 200, Color = color,
            Cover = cover, LightSource = light, Generated = true,
        };

        // ---- Furniture: makes a basso a home ----
        public static readonly BuildingDef Sofa = Sized(Furniture("sofa", "Sofa", new Color32(120, 40, 50, 255), 3f, passable: false, cover: true), 2, 1);
        public static readonly BuildingDef Armchair = Furniture("armchair", "Armchair", new Color32(150, 110, 70, 255), 1.5f, passable: false);
        public static readonly BuildingDef Television = Furniture("tv", "TV", new Color32(30, 30, 34, 255), 2f, passable: false);
        public static readonly BuildingDef Wardrobe = Sized(Furniture("wardrobe", "Wardrobe", new Color32(110, 76, 46, 255), 1f, passable: false, cover: true), 2, 1);
        public static readonly BuildingDef KitchenCounter = Sized(Furniture("kitchen_counter", "Kitchen counter", new Color32(200, 196, 186, 255), 1f, passable: false, cover: true), 2, 1);
        public static readonly BuildingDef Fridge = Furniture("fridge", "Fridge", new Color32(225, 228, 230, 255), 1f, passable: false, cover: true);
        public static readonly BuildingDef Plant = Furniture("plant", "Plant", new Color32(60, 120, 60, 255), 1.5f, passable: false);
        public static readonly BuildingDef Rug = Sized(Furniture("rug", "Rug", new Color32(150, 50, 40, 255), 1f, passable: true), 2, 2);

        static BuildingDef Sized(BuildingDef d, int w, int h)
        {
            d.Size = new Vector2Int(w, h);
            return d;
        }

        // The other cells of a big piece: solid, or walkable (a bed, a rug).
        public static readonly BuildingDef PartSolid = new BuildingDef { Id = "part_solid", Label = "", Passable = false, Cover = true, Filler = true, Color = new Color32(0, 0, 0, 0) };
        public static readonly BuildingDef PartSoft = new BuildingDef { Id = "part_soft", Label = "", Passable = true, PathCost = 1.6f, Filler = true, Color = new Color32(0, 0, 0, 0) };
        public static readonly BuildingDef Safe = Furniture("safe", "Safe", new Color32(70, 74, 80, 255), 0.5f, passable: false, cover: true);
        public static readonly BuildingDef HomeShrine = Furniture("home_shrine", "Home shrine", new Color32(70, 110, 190, 255), 2.5f, passable: false);

        // ---- More shops ----
        public static readonly BuildingDef MotoCounter = new BuildingDef
        {
            Id = "moto_counter", Label = "Concessionaria", Passable = false, WorkToBuild = 600,
            Color = new Color32(60, 60, 70, 255), Shop = ShopKind.Moto,
        };

        public static readonly BuildingDef MobiliCounter = new BuildingDef
        {
            Id = "mobili_counter", Label = "Mobilificio", Passable = false, WorkToBuild = 600,
            Color = new Color32(150, 104, 66, 255), Shop = ShopKind.Mobili,
        };

        public static readonly BuildingDef LussoCounter = new BuildingDef
        {
            Id = "lusso_counter", Label = "Gioielleria", Passable = false, WorkToBuild = 600,
            Color = new Color32(90, 190, 200, 255), Shop = ShopKind.Lusso,
        };

        // ---- Workstations: where the money is made by hand ----
        public static readonly BuildingDef BaggingTable = Furniture("bagging_table", "Bagging table", new Color32(176, 170, 160, 255), 0f, passable: false, cover: true);
        public static readonly BuildingDef RollingTable = Furniture("rolling_table", "Rolling table", new Color32(150, 110, 70, 255), 0f, passable: false, cover: true);

        // ---- Shop fittings and street clutter (painted art) ----
        public static readonly BuildingDef PizzaOven = Furniture("pizza_oven", "Wood-fired oven", new Color32(170, 80, 50, 255), 0f, passable: false, cover: true);
        public static readonly BuildingDef CafeTable = Furniture("cafe_table", "Café table", new Color32(200, 196, 186, 255), 0.3f, passable: false, cover: true);
        public static readonly BuildingDef ShopShelf = Furniture("shop_shelf", "Shelves", new Color32(150, 110, 70, 255), 0f, passable: false, cover: true);
        public static readonly BuildingDef ClothesRack = Furniture("clothes_rack", "Clothes rack", new Color32(90, 90, 120, 255), 0f, passable: false);
        public static readonly BuildingDef FruitStall = Street("fruit_stall", "Fruit and veg", new Color32(150, 120, 60, 255), cover: true);
        public static readonly BuildingDef IceCream = Furniture("ice_cream", "Ice-cream freezer", new Color32(200, 220, 230, 255), 0f, passable: false, cover: true);
        public static readonly BuildingDef BinBags = Street("bin_bags", "Bin bags", new Color32(30, 30, 32, 255), cover: false);
        public static readonly BuildingDef Bicycle = Street("bicycle", "Bicycle", new Color32(60, 80, 60, 255), cover: false);
        public static readonly BuildingDef Crates = Street("crates", "Crates", new Color32(160, 120, 70, 255), cover: true);
        public static readonly BuildingDef PottedPlants = Street("potted_plants", "Potted plants", new Color32(70, 120, 60, 255), cover: false);

        static BuildingDef Furniture(string id, string label, Color32 color, float beauty, bool passable, bool cover = false) => new BuildingDef
        {
            Id = id, Label = label, Passable = passable, PathCost = passable ? 1f : 1f, WorkToBuild = 250, Color = color,
            Beauty = beauty, Cover = cover, PlaceMode = PlaceMode.Single,
        };

        public static readonly BuildingDef[] Buildable =
        {
            Wall, Door, Bed, Table, Stove, Pantry, Bunker,
            Sofa, Armchair, Television, Wardrobe, KitchenCounter, Fridge, Plant, Rug, Safe, HomeShrine,
            BaggingTable, RollingTable,
        };

        /// <summary>Every def, in a fixed order used by save files. Only ever append to this list.</summary>
        public static readonly BuildingDef[] All =
        {
            Wall, Door, Bed, Table, Stove, Pantry, PizzeriaCounter, SalumeriaCounter, FarmaciaCounter,
            BarCounter, MacelleriaCounter, TabaccheriaCounter, BurnedShop, Bunker,
            Tree, ParkedCar, ParkedScooter, Bench, Bin, StreetLamp, MarketStall, Shrine, Altar, Pew, Fountain, Rubble,
            Railing, HospitalDesk, PlasticChair,
            Sofa, Armchair, Television, Wardrobe, KitchenCounter, Fridge, Plant, Rug, Safe, HomeShrine,
            BaggingTable, RollingTable, MobiliCounter, LussoCounter,
            PizzaOven, CafeTable, ShopShelf, ClothesRack, FruitStall, IceCream, BinBags, Bicycle, Crates, PottedPlants,
            MotoCounter, PartSolid, PartSoft,
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
        public const int Pizza = 8;
        public const int Groceries = 6;  // one unit cooks two meals
        public const int Medicine = 20;
        public const int ProductWholesale = 12; // per unit, from the supplier
        public const int ProductStreet = 20;    // per unit, at the piazza
        public const int RecruitBonus = 250;
        public const int JailedFamilySupport = 150; // weekly, per crew member inside
        public const int Pistol = 600;
        public const int Scooter = 900;
        /// <summary>The four scooters at the dealer, cheap to flash: step-through 50, commuter 125, GT 300, maxi 530.</summary>
        public static readonly int[] ScooterModels = { 700, 1700, 2800, 7800 };
        public static readonly string[] ScooterNames = { "Step-through 50", "Commuter 125", "GT 300", "Maxi 530" };
        public static readonly string[] ScooterNotes =
        {
            "Cheap, slow, everybody's first. Nobody looks twice.",
            "Big wheels and a top box: what every dad in the rione rides.",
            "Wide seat, windscreen, real speed.",
            "The boss's ride. Fastest thing in the vicoli; needs clean money.",
        };
        public const int CigaretteCase = 10;    // wholesale, from the smugglers
        public const int CigaretteStreet = 25;  // per case, sold from the deposito
        public const int BillsPerHome = 40;     // weekly: light, gas, water
        public const int BillsPerOperation = 80;
        public const int Burner = 150;          // a week of a clean SIM

        // Product, bought in bulk from Zi Tore and cut into doses of half a gram.
        public const int DoseDg = 5;            // a dose is 0.5 g (in decigrams)
        public const int Etto = 900;            // 100 g, for a beginner
        public const int HalfKilo = 4000;
        public const int Kilo = 7500;
        public const int Baggies100 = 6;

        // Cigarettes, rolled at home and sold by the pack.
        public const int TobaccoPer100g = 8;
        public const int Papers200 = 4;         // papers and filters for ten packs
        public const int EmptyPacks20 = 6;      // ROSSE packs from Nanninella's printer
        public const int TobaccoPerPack = 14;   // grams in a pack of 20
        public const int PackStreet = 4;
        public const int DigitalScale = 120;
        public const int HeatSealer = 90;
        public const int RollingMachine = 150;
        public const int Bonifica = 400;        // a technician sweeps for bugs

        /// <summary>Anything this expensive can't be paid in dirty cash without questions.</summary>
        public const int CleanMoneyThreshold = 3000;
    }
}
