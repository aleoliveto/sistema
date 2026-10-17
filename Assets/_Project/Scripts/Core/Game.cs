using System.Collections.Generic;
using OSistema.AI;
using OSistema.Combat;
using OSistema.Crime;
using OSistema.Npcs;
using OSistema.Pawns;
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
        public readonly RivalClan Rival = new RivalClan();
        public readonly CombatSystem Combat = new CombatSystem();
        public readonly EventSystem Events = new EventSystem();
        public StreetLife StreetLife { get; private set; }

        /// <summary>Interior of the crew's starting apartment.</summary>
        public readonly RectInt PlayerBase;

        public Game(int seed, Map map, RectInt playerBase, long ticks, IEnumerable<Zone> zones)
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
            StreetLife = new StreetLife(this);
            Ticks.OnTick += Tick;
        }

        public static Game NewGame(int seed)
        {
            var generator = new MapGenerator(seed);
            var map = generator.Generate(MapWidth, MapHeight, out var playerBase, out var blocks);
            var rng = new System.Random(seed);
            var game = new Game(seed, map, playerBase, 0, Territory.FromBlocks(blocks, rng));
            game.Org.BuildBusinesses(map, rng);
            game.Stock.Cash = 800;
            game.Stock.Pistols = 1;
            game.SpawnStartingCrew();
            game.Rival.Setup(game, rng);
            game.Log.Add("The crew moves into a basso in the rione. Keep them fed, paid and out of prison.");
            game.Log.Add("Press B to open the business panel. Use the Piazza tool to open a drug square.");
            game.Log.Add($"The {game.Rival.Name} runs the far side of the district. Don't get too close.");
            return game;
        }

        void Tick()
        {
            if (Org.GameOver) return;
            Pawns.Tick(this);
            StreetLife.Tick(this);
            Npcs.Tick(this);
            Combat.Tick(this);
            Police.Tick(this);
            Org.Tick(this);
            Rival.Tick(this);
            Events.Tick(this);
        }

        void SpawnStartingCrew()
        {
            // (first, last, nickname, shirt, construction, cooking, medicine, intimidation, awareness, shooting, role)
            var crew = new[]
            {
                ("Gennaro", "Iaccarino", "'o Russo", new Color32(176, 58, 46, 255), 6, 2, 3, 7, 4, 6, CrewRole.Collector),
                ("Ciro", "Capasso", "Capatosta", new Color32(41, 98, 160, 255), 3, 7, 1, 4, 3, 4, CrewRole.General),
                ("Salvatore", "Scognamiglio", "'o Sicco", new Color32(214, 214, 205, 255), 8, 1, 6, 2, 7, 3, CrewRole.General),
            };

            int i = 0;
            foreach (var cell in GridUtil.CellsAround(Vector2Int.RoundToInt(PlayerBase.center), 6))
            {
                if (i >= crew.Length) break;
                if (!PlayerBase.Contains(cell) || !Map.IsPassable(cell)) continue;
                bool boss = i == 0; // Gennaro leads the family
                var (first, last, nick, shirt, construction, cooking, medicine, intimidation, awareness, shooting, role) = crew[i++];
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
                    Loyalty = boss ? 100f : 65f,
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
            }
        }
    }
}
