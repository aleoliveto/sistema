using System.Collections.Generic;
using OSistema.AI;
using OSistema.Core;
using OSistema.World;
using UnityEngine;

namespace OSistema.Npcs
{
    /// <summary>
    /// The rione going about its day. People run errands and stop to chat, old women sit outside their
    /// bassi, kids play in the courtyards, shopkeepers stand in their doorways and vendors at their
    /// stalls, scooters weave through the vicoli, police walk a beat where heat is high. Most of the
    /// life is concentrated around the crew, where the player is looking. Everyone scatters at gunfire.
    /// Transient: never saved.
    /// </summary>
    public sealed class StreetLife
    {
        const int PanicRadius = 18;
        const int PanicTicks = 120;
        const int NearCrew = 32;

        // Civilian sub-behaviours stored in Npc.State.
        const int Walking = 0, Lingering = 1, GoingHome = 2, Sitting = 3, Playing = 4, Standing = 5;

        readonly List<Vector2Int> doors = new List<Vector2Int>();
        readonly List<Vector2Int> walkable = new List<Vector2Int>();   // sidewalks, piazze, alleys
        readonly List<Vector2Int> streets = new List<Vector2Int>();    // where scooters can start
        readonly List<Vector2Int> chairs = new List<Vector2Int>();
        readonly List<Vector2Int> playgrounds = new List<Vector2Int>(); // courtyards, piazze, parks
        readonly List<(Vector2Int spot, bool vendor)> fixtures = new List<(Vector2Int, bool)>();
        readonly HashSet<Vector2Int> occupiedChairs = new HashSet<Vector2Int>();
        long lastPatrolTick = -100000;
        int fixturesDay = -1;

        public StreetLife(Game game)
        {
            var map = game.Map;
            var home = game.PlayerBase;
            foreach (var d in map.PositionsOf(BuildingDefs.Door))
                if (!home.Contains(d) && FreeNeighbour(map, d).HasValue) doors.Add(d);
            chairs.AddRange(map.PositionsOf(BuildingDefs.PlasticChair));

            for (int y = 0; y < map.Height; y += 2)
            for (int x = 0; x < map.Width; x += 2)
            {
                if (!map.IsPassable(x, y) || map.GetBuilding(x, y) != null) continue;
                var t = map.GetTerrain(x, y);
                var c = new Vector2Int(x, y);
                if (t == TerrainType.Sidewalk || t == TerrainType.Paving || t == TerrainType.Ground || t == TerrainType.Grass) walkable.Add(c);
                if (t == TerrainType.Sidewalk || t == TerrainType.Paving || t == TerrainType.Road) streets.Add(c);
                if (t == TerrainType.Ground || t == TerrainType.Grass || t == TerrainType.Paving) playgrounds.Add(c);
            }

            // Shopkeepers stand outside their doors; vendors behind every third market stall.
            foreach (var def in BuildingDefs.All)
            {
                if (def.Shop == ShopKind.None) continue;
                foreach (var counter in map.PositionsOf(def))
                {
                    var prop = game.Properties.At(counter);
                    if (prop == null) continue;
                    var outside = FreeNeighbour(map, prop.Door);
                    if (outside.HasValue) fixtures.Add((outside.Value, false));
                }
            }
            int stall = 0;
            foreach (var s in map.PositionsOf(BuildingDefs.MarketStall))
            {
                if (stall++ % 3 != 0) continue;
                foreach (var n in new[] { s + Vector2Int.up, s + Vector2Int.down })
                    if (map.InBounds(n) && map.IsPassable(n) && map.GetBuilding(n) == null)
                    {
                        fixtures.Add((n, true));
                        break;
                    }
            }
        }

        static Vector2Int? FreeNeighbour(Map map, Vector2Int c)
        {
            foreach (var n in new[] { c + Vector2Int.down, c + Vector2Int.up, c + Vector2Int.left, c + Vector2Int.right })
                if (map.InBounds(n) && map.IsPassable(n) && map.GetTerrain(n.x, n.y) != TerrainType.Floor) return n;
            return null;
        }

        // ---------- Population ----------

        public void Tick(Game game)
        {
            if (game.Ticks.TicksGame % 20 != 0) return;
            float day = game.Clock.Daylight;
            int hour = game.Clock.Hour;
            bool lateNight = hour >= 1 && hour < 6;

            int people = 0, scooters = 0;
            foreach (var npc in game.Npcs.All)
            {
                if (npc.IsFixture) continue;
                if (npc.Kind == NpcKind.Civilian || npc.Kind == NpcKind.Child || npc.Kind == NpcKind.Elder) people++;
                else if (npc.Kind == NpcKind.Scooter) scooters++;
            }

            // Naples is busy until late. Fewer people out where there's been shooting recently.
            bool recentGunfire = game.Ticks.TicksGame - game.Combat.LastShotTick < GameClock.TicksPerHour * 2;
            float evening = hour >= 18 && hour < 23 ? 1.15f : 1f; // the evening passeggiata
            int peopleTarget = lateNight ? 10 : Mathf.RoundToInt(Mathf.Lerp(35f, 110f, day) * evening * (recentGunfire ? 0.35f : 1f));
            int scooterTarget = lateNight ? 3 : day > 0.3f ? 16 : 9;

            if (people < peopleTarget) SpawnPerson(game);
            if (people < peopleTarget - 20) SpawnPerson(game); // catch up quickly at dawn
            if (scooters < scooterTarget && game.Rng.NextDouble() < 0.35) SpawnScooter(game);
            UpdateFixtures(game, hour);
            MaybeSendPatrol(game);
        }

        /// <summary>A point near a random crew member, or the basso.</summary>
        static Vector2Int? CrewFocus(Game game)
        {
            var crew = game.Pawns.Crew;
            if (crew.Count == 0) return null;
            for (int i = 0; i < 4; i++)
            {
                var p = crew[game.Rng.Next(crew.Count)];
                if (p.IsPresent) return p.Cell;
            }
            return Vector2Int.RoundToInt(game.PlayerBase.center);
        }

        static T Near<T>(List<T> pool, System.Func<T, Vector2Int> cell, Vector2Int? focus, int radius, System.Random rng)
        {
            if (focus.HasValue)
                for (int i = 0; i < 25; i++)
                {
                    var candidate = pool[rng.Next(pool.Count)];
                    if ((cell(candidate) - focus.Value).sqrMagnitude <= radius * radius) return candidate;
                }
            return pool[rng.Next(pool.Count)];
        }

        void SpawnPerson(Game game)
        {
            var rng = game.Rng;
            if (doors.Count == 0) return;
            var focus = rng.NextDouble() < 0.7 ? CrewFocus(game) : null;
            var door = Near(doors, d => d, focus, NearCrew, rng);
            var start = FreeNeighbour(game.Map, door) ?? door;

            double roll = rng.NextDouble();
            NpcKind kind = roll < 0.18 ? NpcKind.Child : roll < 0.36 ? NpcKind.Elder : NpcKind.Civilian;
            float speed = kind == NpcKind.Elder ? 0.04f : kind == NpcKind.Child ? 0.075f : 0.055f + (float)rng.NextDouble() * 0.015f;
            Color32 color = kind == NpcKind.Elder && rng.NextDouble() < 0.6
                ? new Color32(30, 28, 30, 255) // nonna in black
                : (Color32)Color.HSVToRGB((float)rng.NextDouble(), 0.25f + (float)rng.NextDouble() * 0.45f, 0.45f + (float)rng.NextDouble() * 0.5f);

            var npc = game.Npcs.Spawn(kind, start, speed, color);
            npc.TripsLeft = rng.Next(1, 4);
            npc.Exit = Near(doors, d => d, start, 25, rng);
            npc.Target = DestinationFor(npc, game);
        }

        /// <summary>Where this person goes next: a chair for the old, a courtyard for kids, an errand nearby for others.</summary>
        Vector2Int DestinationFor(Npc npc, Game game)
        {
            var rng = game.Rng;
            var here = npc.Mover.Cell;
            if (npc.Kind == NpcKind.Elder && chairs.Count > 0 && rng.NextDouble() < 0.7)
            {
                var chair = Near(chairs, c => c, here, 30, rng);
                if (!occupiedChairs.Contains(chair))
                {
                    occupiedChairs.Add(chair);
                    npc.TripsLeft = 1;
                    npc.Sitting = true;
                    return chair;
                }
            }
            if (npc.Kind == NpcKind.Child && playgrounds.Count > 0 && rng.NextDouble() < 0.75)
            {
                npc.Playing = true;
                return Near(playgrounds, c => c, here, 30, rng);
            }
            return RandomDestinationNear(here, game);
        }

        public Vector2Int RandomDestinationNear(Vector2Int near, Game game)
        {
            var rng = game.Rng;
            if (rng.NextDouble() < 0.3)
            {
                var door = Near(doors, d => d, near, 25, rng);
                return FreeNeighbour(game.Map, door) ?? door;
            }
            return walkable.Count > 0 ? Near(walkable, c => c, near, 25, rng) : near;
        }

        void SpawnScooter(Game game)
        {
            var rng = game.Rng;
            var entrances = game.Territory.Entrances;
            if (entrances.Count < 2 || streets.Count == 0) return;

            // Half of them start somewhere near the crew, so you actually see and hear them.
            Vector2Int from;
            var focus = CrewFocus(game);
            if (rng.NextDouble() < 0.5 && focus.HasValue) from = Near(streets, c => c, focus, 25, rng);
            else from = entrances[rng.Next(entrances.Count)];

            var to = entrances[rng.Next(entrances.Count)];
            for (int i = 0; i < 10 && (to - from).sqrMagnitude < 1600; i++) to = entrances[rng.Next(entrances.Count)];
            var npc = game.Npcs.Spawn(NpcKind.Scooter, from, 0.1f + (float)rng.NextDouble() * 0.045f,
                (Color32)Color.HSVToRGB((float)rng.NextDouble(), 0.25f + (float)rng.NextDouble() * 0.5f, 0.35f + (float)rng.NextDouble() * 0.6f));
            npc.Exit = to;
        }

        // Shopkeepers and vendors come out in the morning and go in at night.
        void UpdateFixtures(Game game, int hour)
        {
            bool open = hour >= 8 && hour < 21;
            if (!open || fixturesDay == game.Clock.Day) return;
            fixturesDay = game.Clock.Day;
            var rng = game.Rng;
            foreach (var (spot, vendor) in fixtures)
            {
                var npc = game.Npcs.Spawn(NpcKind.Civilian, spot, 0.05f,
                    vendor ? new Color32(235, 235, 225, 255) : (Color32)Color.HSVToRGB((float)rng.NextDouble(), 0.3f, 0.6f));
                npc.IsFixture = true;
                npc.State = Standing;
                npc.Target = spot;
            }
        }

        // Police walk a beat through the hottest zone now and then.
        void MaybeSendPatrol(Game game)
        {
            long now = game.Ticks.TicksGame;
            if (now - lastPatrolTick < GameClock.TicksPerHour * 3) return;
            Crime.Zone hottest = null;
            foreach (var z in game.Territory.Zones)
                if (z.Heat > 30f && (hottest == null || z.Heat > hottest.Heat)) hottest = z;
            if (hottest == null || game.Territory.Entrances.Count == 0) return;

            lastPatrolTick = now;
            var rng = game.Rng;
            var entrance = game.Territory.RandomEntrance(rng);
            for (int i = 0; i < 2; i++)
            {
                var cop = game.Npcs.Spawn(NpcKind.Police, entrance, 0.05f, new Color32(30, 50, 130, 255));
                cop.IsPatrol = true;
                cop.TripsLeft = 4;
                cop.Target = RandomCellIn(hottest.Rect, game);
                cop.Exit = entrance;
            }
        }

        static Vector2Int RandomCellIn(RectInt rect, Game game)
        {
            for (int i = 0; i < 20; i++)
            {
                var c = new Vector2Int(game.Rng.Next(rect.xMin, rect.xMax), game.Rng.Next(rect.yMin, rect.yMax));
                if (game.Map.IsPassable(c) && game.Map.GetTerrain(c.x, c.y) != TerrainType.Floor) return c;
            }
            return Vector2Int.RoundToInt(rect.center);
        }

        // ---------- Behaviours ----------

        static bool GunfireNear(Npc npc, Game game)
        {
            var combat = game.Combat;
            return game.Ticks.TicksGame - combat.LastShotTick < PanicTicks &&
                   (combat.LastShotPosition - npc.Mover.Position).sqrMagnitude < PanicRadius * PanicRadius;
        }

        public static void TickCivilian(Npc npc, Game game)
        {
            var life = game.StreetLife;
            if (!npc.Panicking && GunfireNear(npc, game))
            {
                // Run from the shots, towards the nearest edge of the map, and don't come back today.
                npc.Panicking = true;
                life.ReleaseChair(npc);
                npc.State = GoingHome;
                var away = npc.Mover.Position + (npc.Mover.Position - game.Combat.LastShotPosition).normalized * 30f;
                var exit = game.Territory.Entrances.Count > 0 ? game.Territory.Entrances[0] : npc.Exit;
                foreach (var e in game.Territory.Entrances)
                    if ((e - away).sqrMagnitude < (exit - away).sqrMagnitude) exit = e;
                npc.Walker = new Walker(exit, adjacent: false, speedMultiplier: 1.9f);
            }

            switch (npc.State)
            {
                case Walking:
                {
                    npc.Walker ??= new Walker(npc.Target, adjacent: false);
                    var status = npc.Walker.Tick(npc.Mover, 1f, game);
                    if (status == WalkStatus.Walking) return;
                    npc.Walker = null;
                    if (npc.Sitting && status == WalkStatus.Arrived)
                    {
                        npc.State = Sitting;
                        npc.Timer = game.Rng.Next(3000, 9000); // a long afternoon watching the street
                    }
                    else if (npc.Playing)
                    {
                        npc.State = Playing;
                        npc.Timer = game.Rng.Next(1500, 4000);
                    }
                    else
                    {
                        npc.State = Lingering;
                        npc.Timer = game.Rng.Next(150, 900);
                    }
                    return;
                }
                case Sitting:
                case Lingering:
                {
                    if (--npc.Timer > 0) return;
                    life.ReleaseChair(npc);
                    npc.Sitting = false;
                    if (--npc.TripsLeft > 0)
                    {
                        npc.Target = life.DestinationFor(npc, game);
                        npc.State = Walking;
                    }
                    else npc.State = GoingHome;
                    return;
                }
                case Playing:
                {
                    // Running around after a ball: short dashes near where they are.
                    if (--npc.Timer <= 0)
                    {
                        npc.Playing = false;
                        npc.State = GoingHome;
                        npc.Walker = null;
                        return;
                    }
                    if (npc.Walker == null || npc.Walker.Tick(npc.Mover, 1.2f, game) != WalkStatus.Walking)
                    {
                        var c = npc.Mover.Cell + new Vector2Int(game.Rng.Next(-3, 4), game.Rng.Next(-3, 4));
                        npc.Walker = game.Map.IsPassable(c) && game.Map.GetTerrain(c.x, c.y) != TerrainType.Floor ? new Walker(c, adjacent: false) : null;
                    }
                    return;
                }
                case Standing:
                {
                    // Shopkeepers and vendors: stay put, go in at closing time.
                    if (game.Clock.Hour >= 21 || game.Clock.Hour < 8) npc.Despawned = true;
                    return;
                }
                default: // going home
                {
                    npc.Walker ??= new Walker(npc.Exit, adjacent: true);
                    if (npc.Walker.Tick(npc.Mover, 1f, game) != WalkStatus.Walking) npc.Despawned = true;
                    return;
                }
            }
        }

        void ReleaseChair(Npc npc)
        {
            if (npc.Sitting) occupiedChairs.Remove(npc.Target);
        }

        public static void TickScooter(Npc npc, Game game)
        {
            npc.Walker ??= new Walker(npc.Exit, adjacent: false, roadOnly: true); // streets and vicoli, never indoors
            if (npc.Walker.Tick(npc.Mover, 1f, game) != WalkStatus.Walking) npc.Despawned = true;
        }

        /// <summary>Two officers walking the beat: a few points in a hot zone, then back out.</summary>
        public static void TickPatrol(Npc npc, Game game)
        {
            npc.Walker ??= new Walker(npc.Target, adjacent: false);
            var status = npc.Walker.Tick(npc.Mover, 1f, game);
            if (status == WalkStatus.Walking) return;

            if (npc.State == 2)
            {
                npc.Despawned = true;
                return;
            }
            if (--npc.TripsLeft > 0)
            {
                var zone = game.Territory.ZoneAt(npc.Target);
                npc.Target = zone != null ? RandomCellIn(zone.Rect, game) : npc.Exit;
                npc.Walker = new Walker(npc.Target, adjacent: false);
            }
            else
            {
                npc.State = 2;
                npc.Walker = new Walker(npc.Exit, adjacent: false);
            }
        }
    }
}
