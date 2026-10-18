using System.Collections.Generic;
using OSistema.AI;
using OSistema.Combat;
using OSistema.Crime;
using OSistema.Npcs;
using OSistema.Pawns;
using OSistema.Social;
using OSistema.Story;
using OSistema.World;
using UnityEngine;

namespace OSistema.Core
{
    /// <summary>
    /// Root of the simulation state. Plain C#: views read from it, it never reads from views.
    /// Create with <see cref="NewGame"/> or <see cref="SaveSystem.Load"/>.
    /// </summary>
    public sealed class Game
    {
        public const int MapWidth = 200;
        public const int MapHeight = 150;

        public readonly int Seed;
        public readonly System.Random Rng;
        public readonly Map Map;
        public readonly TickManager Ticks;
        public readonly GameClock Clock;
        public readonly Pathfinder Pathfinder;
        public readonly ConstructionManager Construction;
        public readonly PawnManager Pawns;
        public readonly NpcManager Npcs = new NpcManager();
        public readonly Stock Stock = new Stock();
        public readonly MessageLog Log;
        public readonly Territory Territory;
        public readonly Organization Org = new Organization();
        public readonly Police Police = new Police();
        public readonly RivalClan Rival = new RivalClan { Id = 0, Colour = ClanColours[0] };

        /// <summary>
        /// The clans that split the city between them. The first is the one next door, at odds with you
        /// from the start (Rival); the others hold their own rioni and fight each other as much as you.
        /// </summary>
        public readonly List<RivalClan> Clans = new List<RivalClan>();

        static readonly Color32[] ClanColours =
        {
            new Color32(200, 60, 60, 255), new Color32(70, 120, 210, 255), new Color32(160, 90, 200, 255), new Color32(60, 170, 140, 255),
        };

        public RivalClan AddClan()
        {
            var c = new RivalClan { Id = Clans.Count, Colour = ClanColours[Clans.Count % ClanColours.Length] };
            Clans.Add(c);
            return c;
        }

        /// <summary>The clan with a piazza in this rione, if any.</summary>
        public RivalClan ClanOwning(int zoneId)
        {
            foreach (var c in Clans)
                if (!c.Destroyed && c.OwnsZone(zoneId)) return c;
            return null;
        }

        public RivalClan ClanOf(Pawn p) => p != null && p.Clan >= 0 && p.Clan < Clans.Count ? Clans[p.Clan] : Rival;

        /// <summary>Every rival piazza in the city, whoever runs it.</summary>
        public IEnumerable<DrugSquare> RivalSquares
        {
            get
            {
                foreach (var c in Clans)
                    if (!c.Destroyed)
                        foreach (var s in c.Squares) yield return s;
            }
        }
        public readonly CombatSystem Combat = new CombatSystem();
        public readonly EventSystem Events = new EventSystem();
        public readonly ContractBoard Contracts = new ContractBoard();
        public readonly Phone Phone = new Phone();
        public readonly Favours Favours = new Favours();
        public readonly Informants Informants = new Informants();
        public readonly Newspaper Newspaper = new Newspaper();
        public readonly ContactBook Contacts = new ContactBook();
        public readonly Pursuit Pursuit = new Pursuit();

        /// <summary>Setting: when the player leaves him alone, may Gennaro eat and sleep by himself? Off by default.</summary>
        public bool PlayerSelfCare;

        /// <summary>Who's working at each table: one person per workstation.</summary>
        public readonly Dictionary<Vector2Int, Pawn> StationClaims = new Dictionary<Vector2Int, Pawn>();

        public Pawn WorkerAt(Vector2Int station) =>
            StationClaims.TryGetValue(station, out var p) && p != null && p.IsPresent && !p.Dead && p.CurrentJob is AI.StationJob ? p : null;

        /// <summary>
        /// Street furniture (lamps, bins, benches, parked cars and scooters, chairs) must never choke a
        /// vicolo: anything that leaves less than two clear cells beside it, across the lane, goes.
        /// </summary>
        /// <summary>
        /// Furniture belongs indoors, in a building that's still standing. The map generator cuts squares
        /// and streets through rows of houses after furnishing them; whatever was left in the open goes.
        /// Runs on loads too, so older saves are tidied.
        /// </summary>
        void RemoveStrayFurniture()
        {
            var indoor = new System.Collections.Generic.HashSet<BuildingDef>
            {
                BuildingDefs.Bed, BuildingDefs.Table, BuildingDefs.Wardrobe, BuildingDefs.Sofa, BuildingDefs.Television,
                BuildingDefs.KitchenCounter, BuildingDefs.Fridge, BuildingDefs.Plant, BuildingDefs.HomeShrine, BuildingDefs.Armchair,
                BuildingDefs.Rug, BuildingDefs.Stove, BuildingDefs.Pantry, BuildingDefs.Safe, BuildingDefs.PizzaOven, BuildingDefs.CafeTable,
                BuildingDefs.ShopShelf, BuildingDefs.ClothesRack, BuildingDefs.IceCream, BuildingDefs.BaggingTable, BuildingDefs.RollingTable,
                BuildingDefs.PartSolid, BuildingDefs.PartSoft,
            };
            int removed = 0;
            for (int y = 0; y < Map.Height; y++)
            for (int x = 0; x < Map.Width; x++)
            {
                var b = Map.GetBuilding(x, y);
                if (b == null || !indoor.Contains(b)) continue;
                var t = Map.GetTerrain(x, y);
                var c = new Vector2Int(x, y);
                if ((t == TerrainType.Floor || t == TerrainType.Marble) && (PlayerBase.Contains(c) || Properties.At(c) != null)) continue;
                Map.SetBuilding(x, y, null);
                removed++;
            }
            if (removed > 0) Debug.Log($"[O Sistema] Removed {removed} pieces of furniture left in the open.");
        }

        void ClearNarrowLanes()
        {
            var clutter = new[] { BuildingDefs.StreetLamp, BuildingDefs.Bin, BuildingDefs.Bench, BuildingDefs.ParkedCar,
                BuildingDefs.ParkedScooter, BuildingDefs.PlasticChair, BuildingDefs.MarketStall,
                BuildingDefs.BinBags, BuildingDefs.Bicycle, BuildingDefs.Crates, BuildingDefs.PottedPlants, BuildingDefs.FruitStall };
            int Span(int x, int y, int dx, int dy)
            {
                int n = 0;
                for (int k = 1; k <= 3; k++)
                {
                    int cx = x + dx * k, cy = y + dy * k;
                    if (!Map.InBounds(cx, cy) || !Map.IsPassable(new Vector2Int(cx, cy))) break;
                    var t = Map.GetTerrain(cx, cy);
                    if (t == TerrainType.Floor || t == TerrainType.Marble) break;
                    n++;
                }
                return n;
            }
            // Something solid beside it: a wall, a building, the water's edge.
            bool AgainstEdge(Vector2Int c)
            {
                foreach (var n in new[] { c + Vector2Int.left, c + Vector2Int.right, c + Vector2Int.up, c + Vector2Int.down })
                {
                    if (!Map.InBounds(n.x, n.y)) return true;
                    var b = Map.GetBuilding(n.x, n.y);
                    var t = Map.GetTerrain(n.x, n.y);
                    if (b == BuildingDefs.Wall || b == BuildingDefs.Door || t == TerrainType.Floor || t == TerrainType.Marble || t == TerrainType.Water) return true;
                }
                return false;
            }
            int removed = 0;
            // Lamps belong on walls: move each one onto the nearest wall as a bracket lamp.
            foreach (var c in new List<Vector2Int>(Map.PositionsOf(BuildingDefs.StreetLamp)))
            {
                Map.SetBuilding(c.x, c.y, null);
                removed++;
                for (int r = 1; r <= 2; r++)
                foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    var w = c + d * r;
                    if (!Map.InBounds(w.x, w.y) || Map.GetBuilding(w.x, w.y) != BuildingDefs.Wall || Map.GetDecor(w.x, w.y) != MapDecor.None) continue;
                    Map.SetDecor(w.x, w.y, MapDecor.WallLamp);
                    goto placed;
                }
                placed:;
            }
            // Never two lamps side by side: thin them out to one every few cells along a wall.
            for (int y = 0; y < Map.Height; y++)
            for (int x = 0; x < Map.Width; x++)
            {
                if (Map.GetDecor(x, y) != MapDecor.WallLamp) continue;
                for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                {
                    if ((dx == 0 && dy == 0) || !Map.InBounds(x + dx, y + dy)) continue;
                    if ((dy > 0 || (dy == 0 && dx > 0)) && Map.GetDecor(x + dx, y + dy) == MapDecor.WallLamp) Map.SetDecor(x + dx, y + dy, MapDecor.None);
                }
            }
            foreach (var def in clutter)
            foreach (var c in new List<Vector2Int>(Map.PositionsOf(def)))
            {
                int across = Mathf.Min(Span(c.x, c.y, 1, 0) + Span(c.x, c.y, -1, 0), Span(c.x, c.y, 0, 1) + Span(c.x, c.y, 0, -1));
                // Keep it only against the edge of the street, with room to pass; never in the middle of a lane.
                bool narrow = across < 3;
                bool loose = !AgainstEdge(c) && across < 6 && def != BuildingDefs.MarketStall;
                if (!narrow && !loose) continue;
                Map.SetBuilding(c.x, c.y, null);
                removed++;
            }
            if (removed > 0) Debug.Log($"[O Sistema] Cleared {removed} props out of narrow lanes.");
        }

        /// <summary>The character the player moves directly: the head of the family.</summary>
        public Pawn Player
        {
            get
            {
                foreach (var p in Pawns.Crew) if (p.Controlled && !p.Dead && !p.Departed) return p;
                return null;
            }
        }

        void PlaceStarterTable()
        {
            var r = PlayerBase;
            for (int y = r.yMin; y < r.yMax; y++)
            for (int x = r.xMin; x < r.xMax; x++)
            {
                bool edge = x == r.xMin || x == r.xMax - 1 || y == r.yMax - 1;
                if (!edge || Map.GetBuilding(x, y) != null || !Map.IsPassable(new Vector2Int(x, y))) continue;
                bool nearDoor = false;
                foreach (var c in GridUtil.CellsAround(new Vector2Int(x, y), 1))
                    if (Map.InBounds(c) && Map.GetBuilding(c.x, c.y) == BuildingDefs.Door) nearDoor = true;
                if (nearDoor) continue;
                Map.SetBuilding(x, y, BuildingDefs.RollingTable);
                return;
            }
        }

        /// <summary>Hands control to the boss (at the start, after loading, after a succession).</summary>
        public void EnsurePlayer()
        {
            var boss = Org.Boss(this);
            foreach (var p in Pawns.Crew) p.Controlled = p == boss;
        }
        int socialHour = -1;

        /// <summary>Floating text over the world (money earned and the like). Presentation only.</summary>
        public event System.Action<Vector2, string> Popup;
        public void PopupAt(Vector2 at, string text) => Popup?.Invoke(at, text);
        public StreetLife StreetLife { get; private set; }
        public readonly PropertyManager Properties;
        public readonly Hospital Hospital;

        /// <summary>Interior of the crew's starting apartment.</summary>
        public readonly RectInt PlayerBase;

        public Game(int seed, Map map, RectInt playerBase, long ticks, IEnumerable<Zone> zones, List<Property> properties)
        {
            Seed = seed;
            // System.Random can't be saved, so a loaded game reseeds from seed + time.
            Rng = new System.Random(unchecked(seed + (int)ticks));
            Map = map;
            PlayerBase = playerBase;
            Ticks = new TickManager();
            Ticks.Restore(ticks);
            Clock = new GameClock(Ticks);
            Pathfinder = new Pathfinder(Map);
            Construction = new ConstructionManager(Map);
            Pawns = new PawnManager();
            Log = new MessageLog(Ticks);
            Territory = new Territory(Map, zones);
            Properties = new PropertyManager(Map, properties);
            StreetLife = new StreetLife(this);
            Hospital = new Hospital(this);
            Clans.Add(Rival);
            ClearNarrowLanes();
            RemoveStrayFurniture();
            Contacts.Setup(this);
            Ticks.OnTick += Tick;
        }

        public static Game NewGame(int seed, CharacterSetup setup = null)
        {
            var generator = new MapGenerator(seed);
            var map = generator.Generate(MapWidth, MapHeight, out var playerBase, out var zoneRects, out var properties);
            var rng = new System.Random(seed);
            var game = new Game(seed, map, playerBase, 0, Territory.FromBlocks(zoneRects, rng), properties);
            game.Org.BuildBusinesses(map, rng);
            game.NameProperties();
            game.Stock.Cash = 40; // what a seventeen-year-old has in his pocket
            // Mamma's basso has an old rolling table and the end of a pouch: enough for a few packs.
            game.Stock.TobaccoG = 45;
            game.Stock.Papers = 60;
            game.Stock.EmptyPacks = 3;
            game.PlaceStarterTable();
            game.SpawnStartingCrew(setup);
            game.SetupClans(rng);
            game.Favours.Setup(game, rng);
            game.Log.Add($"{game.Player?.FirstName ?? "Gennaro"} is seventeen. He lives with his mother in a basso in the rione, and he wants more.");
            game.Log.Add("WASD to walk, E to talk or use things. Someone with work has a gold ! over their head.");
            game.Phone.Send(game, Social.MsgKind.Info, "Mammà",
                "Gennà, nun fa' tarde stasera. E statte accorto a chi frequiente.",
                "Gennà, don't be late tonight. And be careful who you spend time with.", null);
            game.Phone.Send(game, Social.MsgKind.Info, ContactBook.Totonno,
                "Passa d''o bar quanno puo'. Tengo 'na cosa pe' te.",
                "Drop by the bar when you can. I've got something for you.",
                "Zio Totonno stands outside his bar every day. Walk over and hear him out. He also buys cigarettes, no questions asked: there's an old rolling table in mamma's basso.");
            return game;
        }

        public readonly Weather Weather = new Weather();
        public readonly Crime.Response Response = new Crime.Response();

        void Tick()
        {
            if (Org.GameOver) return;
            Weather.Tick(this);
            Response.Tick(this);
            Pawns.Tick(this);
            StreetLife.Tick(this);
            Hospital.Tick(this);
            Contracts.Tick(this);
            Contacts.Tick(this);
            Pursuit.Tick(this);
            Npcs.Tick(this);
            Combat.Tick(this);
            Police.Tick(this);
            Org.Tick(this);
            foreach (var c in Clans) c.Tick(this);
            Events.Tick(this);
            SocialTick();
        }

        /// <summary>
        /// The city broken up: four clans, each holding a pair of rioni far from each other and from the
        /// family's basso. Everything else is up for grabs.
        /// </summary>
        void SetupClans(System.Random rng)
        {
            var home = PlayerBase.center;
            var zones = new List<Zone>(Territory.Zones);
            var homeZone = Territory.ZoneAt(Vector2Int.RoundToInt(home));
            zones.Remove(homeZone);
            // Seeds spread out: each next one as far as possible from home and from the others.
            var seeds = new List<Zone>();
            for (int k = 0; k < 4 && zones.Count > 0; k++)
            {
                Zone best = null;
                float bestScore = -1f;
                foreach (var z in zones)
                {
                    float d = (z.Rect.center - home).magnitude * (k == 0 ? 1f : 0.6f);
                    foreach (var s in seeds) d = Mathf.Min(d, (z.Rect.center - s.Rect.center).magnitude * 1.4f);
                    if (k == 0 && (z.Rect.center - home).magnitude > 70f) d *= 0.4f;   // the first clan is the one next door
                    if (d > bestScore) { bestScore = d; best = z; }
                }
                seeds.Add(best);
                zones.Remove(best);
            }
            for (int k = 0; k < seeds.Count; k++)
            {
                var clan = k == 0 ? Rival : AddClan();
                // Each clan holds its seed and the nearest free rione to it.
                Zone second = null;
                float sd = float.MaxValue;
                foreach (var z in zones)
                {
                    float d = (z.Rect.center - seeds[k].Rect.center).magnitude;
                    if (d < sd) { sd = d; second = z; }
                }
                if (second != null) zones.Remove(second);
                clan.Setup(this, rng, second != null ? new[] { seeds[k], second } : new[] { seeds[k] });
            }
        }

        // The phone, the neighbours, the rats and the paper all think once an hour.
        void SocialTick()
        {
            int hour = Clock.Day * 24 + Clock.Hour;
            if (hour == socialHour) return;
            bool first = socialHour < 0;
            socialHour = hour;
            if (first) return;
            Phone.Hourly(this);
            Favours.Hourly(this);
            Informants.Hourly(this);
            Newspaper.Hourly(this);
        }

        /// <summary>Names and asking prices for every building, from its rione and its shop.</summary>
        void NameProperties()
        {
            foreach (var p in Properties.All)
            {
                var zone = Territory.ZoneAt(Vector2Int.RoundToInt(p.Rect.center));
                string where = zone != null ? zone.Name : "the rione";
                var business = Org.Businesses.Find(b => p.Rect.Contains(b.Cell));
                if (p.Name == null)
                    p.Name = business != null ? business.Name
                        : p.Rect.width * p.Rect.height < 70 ? $"A basso in {where}" : $"A palazzo in {where}";
                p.Price = PropertyManager.PriceFor(p, zone != null ? zone.Demand : 1f);
            }
        }

        // (first, last, nickname, shirt, construction, cooking, medicine, intimidation, awareness, shooting, role)
        static readonly (string, string, string, Color32, int, int, int, int, int, int, CrewRole)[] Friends =
        {
            ("Gennaro", "Iaccarino", "'o Russo", new Color32(176, 58, 46, 255), 3, 2, 1, 4, 4, 2, CrewRole.General),
            ("Ciro", "Capasso", "Capatosta", new Color32(41, 98, 160, 255), 3, 7, 1, 4, 3, 4, CrewRole.General),
            ("Salvatore", "Scognamiglio", "'o Sicco", new Color32(214, 214, 205, 255), 8, 1, 6, 2, 7, 3, CrewRole.General),
        };

        public static int FriendCount => Friends.Length - 1;
        public static string FriendName(int i) => $"{Friends[i + 1].Item1} “{Friends[i + 1].Item3}”";

        /// <summary>A childhood friend from the vicolo joins (i = 0 or 1).</summary>
        public Pawn AddFriend(int i) => SpawnCrew(Friends[i + 1], boss: false);

        // Gennaro starts alone. His friends come when he's somebody.
        void SpawnStartingCrew(CharacterSetup setup)
        {
            var me = SpawnCrew(Friends[0], boss: true);
            EnsurePlayer();
            if (me == null) return;
            Stock.Store(BuildingDefs.Bed);   // a folding bed for the first guaglione who joins
            me.Look = new Pawns.Look { Body = 0, Head = 2, Hair = 1, Face = 0 };
            if (setup == null) return;
            // The player's own character, as he made him.
            me.FirstName = setup.FirstName;
            me.LastName = setup.LastName;
            me.Nickname = setup.Nickname;
            me.Look = setup.Look.Clone();
            var sk = setup.Skills;
            me.ConstructionSkill = sk[0];
            me.CookingSkill = sk[1];
            me.MedicineSkill = sk[2];
            me.IntimidationSkill = sk[3];
            me.AwarenessSkill = sk[4];
            me.ShootingSkill = sk[5];
            switch (setup.Background)
            {
                case Background.KidFromTheVele:
                    me.AwarenessSkill += 2;
                    Org.Respect += 3f;
                    break;
                case Background.OutOfJuvie:
                    me.IntimidationSkill += 2;
                    me.ShootingSkill += 1;
                    Org.Respect += 5f;
                    Org.Evidence += 12f;
                    break;
                default:
                    Stock.Cash += 80;
                    break;
            }
        }

        Pawn SpawnCrew((string, string, string, Color32, int, int, int, int, int, int, CrewRole) f, bool boss)
        {
            foreach (var cell in GridUtil.CellsAround(Vector2Int.RoundToInt(PlayerBase.center), 6))
            {
                if (!PlayerBase.Contains(cell) || !Map.IsPassable(cell) || Pawns.SomeoneElseAt(cell, null)) continue;
                var (first, last, nick, shirt, construction, cooking, medicine, intimidation, awareness, shooting, role) = f;
                var pawn = new Pawn(Pawns.NextId(), first, last, nick, shirt, cell)
                {
                    ConstructionSkill = construction,
                    CookingSkill = cooking,
                    MedicineSkill = medicine,
                    IntimidationSkill = intimidation,
                    AwarenessSkill = awareness,
                    ShootingSkill = shooting,
                    Role = role,
                    IsBoss = boss,
                    Loyalty = boss ? 100f : 70f,
                    Wage = boss ? 0 : 60,
                };
                pawn.Needs.Food = 0.5f + (float)Rng.NextDouble() * 0.4f;
                pawn.Needs.Rest = 0.6f + (float)Rng.NextDouble() * 0.35f;
                // Each of the starting crew already has a bed at the basso.
                foreach (var bed in Map.PositionsOf(BuildingDefs.Bed))
                {
                    if (Pawns.IsBedOwned(bed, pawn)) continue;
                    pawn.Bed = bed;
                    break;
                }
                Pawns.Add(pawn);
                return pawn;
            }
            return null;
        }
    }
}
