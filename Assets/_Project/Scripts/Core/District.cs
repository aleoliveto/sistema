using System.Collections.Generic;
using OSistema.AI;
using OSistema.Crime;
using OSistema.Npcs;
using OSistema.World;
using UnityEngine;

namespace OSistema.Core
{
    /// <summary>
    /// One district of the city: its own map and everything tied to the ground: the rioni, the
    /// buildings and who owns them, the shops, the family's piazze there, what's being built. The
    /// family itself (crew, money, rank, phone, clans' names) belongs to the Game and travels.
    /// Only one district is live at a time; see DISTRICTS_PLAN.md for the others.
    /// </summary>
    public sealed class District
    {
        /// <summary>Stable key, used for its save file (district_{Id}.json).</summary>
        public readonly string Id;
        public string Name;
        public readonly int Seed;

        public readonly Map Map;
        public readonly Territory Territory;
        public readonly PropertyManager Properties;
        public readonly Pathfinder Pathfinder;
        public readonly ConstructionManager Construction;

        /// <summary>Interior of the family's home here; empty in a district where the family has none.</summary>
        public readonly RectInt PlayerBase;

        /// <summary>The family's piazze in this district.</summary>
        public readonly List<DrugSquare> Squares = new List<DrugSquare>();
        /// <summary>Every shop and bar in this district, with its pizzo.</summary>
        public readonly List<Business> Businesses = new List<Business>();

        // Built once the Game exists (they look at the whole game state).
        public StreetLife StreetLife;
        public Hospital Hospital;

        public District(string id, string name, int seed, Map map, RectInt playerBase, IEnumerable<Zone> zones, List<Property> properties)
        {
            Id = id;
            Name = name;
            Seed = seed;
            Map = map;
            PlayerBase = playerBase;
            Territory = new Territory(map, zones);
            Properties = new PropertyManager(map, properties);
            Pathfinder = new Pathfinder(map);
            Construction = new ConstructionManager(map);
        }

        /// <summary>The district the game starts in: the vicoli around mamma's basso.</summary>
        public const string HomeId = "quartieri";
        public const string PortId = "porto";

        public static string NameOf(string id) => id == PortId ? "Il Porto" : "Quartieri";

        // ---------- Ways out ----------

        /// <summary>A road off the edge of the map that leads to another district.</summary>
        public sealed class Gate
        {
            public Vector2Int Cell;
            public string To;
        }

        public readonly List<Gate> Gates = new List<Gate>();

        /// <summary>Where the roads out are: the coast road runs from the Quartieri (east end) to the port (west end).</summary>
        public void FindGates()
        {
            Gates.Clear();
            if (Id == HomeId) AddGate(Map.Width - 1, 14, PortId);
            else if (Id == PortId) AddGate(0, MapGenerator.PortRoadY + 1, HomeId);
        }

        void AddGate(int x, int nearY, string to)
        {
            for (int d = 0; d < 12; d++)
                foreach (int y in new[] { nearY + d, nearY - d })
                    if (Map.InBounds(x, y) && Map.GetTerrain(x, y) == TerrainType.Road && Map.IsPassable(x, y))
                    {
                        Gates.Add(new Gate { Cell = new Vector2Int(x, y), To = to });
                        return;
                    }
        }

        /// <summary>The way into this district from another: where you arrive.</summary>
        public Vector2Int ArrivalFrom(string fromId)
        {
            foreach (var g in Gates)
                if (g.To == fromId)
                {
                    // A few steps in from the edge.
                    var inward = g.Cell.x == 0 ? Vector2Int.right : g.Cell.x == Map.Width - 1 ? Vector2Int.left : Vector2Int.zero;
                    var c = g.Cell + inward * 4;
                    return Map.InBounds(c) && Map.IsPassable(c) ? c : g.Cell;
                }
            return new Vector2Int(Map.Width / 2, Map.Height / 2);
        }

        // ---------- While the family is elsewhere ----------

        /// <summary>What's been explored here (the fog of war), kept between visits.</summary>
        public byte[] Explored;

        /// <summary>People who stay here while the family is away: crew left on their posts, and the clans' men.</summary>
        public readonly List<OSistema.Pawns.Pawn> Dormant = new List<OSistema.Pawns.Pawn>();

        /// <summary>The clans' piazze here while the family is away (in play, they live in each clan's list).</summary>
        public readonly List<DrugSquare> DormantClanSquares = new List<DrugSquare>();

        /// <summary>
        /// One game hour in a district nobody is watching: piazze with a pusher still sell, heat
        /// cools, and an undefended piazza may be taken by the clan that wants it most. Cheap: numbers,
        /// not people.
        /// </summary>
        public void LedgerHour(Game game)
        {
            Territory.HourlyDecay();
            var rng = game.Rng;
            for (int i = Squares.Count - 1; i >= 0; i--)
            {
                var sq = Squares[i];
                var zone = Territory.Zones[sq.ZoneId];
                bool pusher = false, guarded = false;
                foreach (var p in Dormant)
                {
                    if (p.Faction != OSistema.Pawns.Faction.Crew || !p.IsPresent) continue;
                    if (p.Role == OSistema.Pawns.CrewRole.Pusher) pusher = true;
                    if (p.Role == OSistema.Pawns.CrewRole.Lookout || p.Armed) guarded = true;
                }
                if (pusher && sq.IsOpenAt(game.Clock.Hour) && game.Stock.Product > 0)
                {
                    int sales = Mathf.Min(game.Stock.Product, Mathf.RoundToInt(6f * zone.Demand * (1f - zone.Heat / 150f) * (float)rng.NextDouble() * 2f));
                    game.Stock.Product -= sales;
                    game.Stock.Cash += sales * Prices.ProductStreet;
                    sq.SalesToday += sales;
                    zone.Heat = Mathf.Min(100f, zone.Heat + sales * 0.4f);
                }
                // Somebody angry, with men to spare, and nobody there to stop them.
                RivalClan taker = null;
                foreach (var c in game.Clans)
                    if (!c.Destroyed && c.Soldiers >= 4 && c.Hostility >= 55f && (taker == null || c.Hostility > taker.Hostility)) taker = c;
                if (taker != null && !guarded && rng.NextDouble() < 0.01)
                {
                    Squares.RemoveAt(i);
                    game.Org.SquaresVersion++;   // the map redraws without it
                    DormantClanSquares.Add(new DrugSquare { Cell = sq.Cell, ZoneId = sq.ZoneId, Rival = true, OpenHour = 0, CloseHour = 0, Clan = taker.Id });
                    game.Phone.Send(game, OSistema.Social.MsgKind.Info, "A friend in " + Name,
                        null, $"The {taker.Name} took your piazza in {zone.Name}. Nobody was there to stop them.", null);
                    game.Log.Add($"{Name}: the {taker.Name} took your piazza in {zone.Name}.", MessageKind.Bad);
                }
            }
        }
    }
}
