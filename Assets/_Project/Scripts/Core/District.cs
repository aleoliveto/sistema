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
    }
}
