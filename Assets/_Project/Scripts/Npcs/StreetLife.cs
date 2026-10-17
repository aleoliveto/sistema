using System.Collections.Generic;
using OSistema.AI;
using OSistema.Core;
using OSistema.World;
using UnityEngine;

namespace OSistema.Npcs
{
    /// <summary>
    /// The rione going about its day: people running errands and stopping to chat, kids, old women,
    /// scooters cutting through, police walking a beat where heat is high. They all scatter at gunfire.
    /// Transient: never saved.
    /// </summary>
    public sealed class StreetLife
    {
        const int PanicRadius = 18;
        const int PanicTicks = 120;

        readonly List<Vector2Int> doors = new List<Vector2Int>();
        readonly List<Vector2Int> walkable = new List<Vector2Int>(); // sidewalks, piazze, alleys
        long lastPatrolTick = -100000;

        public StreetLife(Game game)
        {
            var map = game.Map;
            var home = game.PlayerBase;
            foreach (var d in map.PositionsOf(BuildingDefs.Door))
                if (!home.Contains(d) && FreeNeighbour(map, d).HasValue) doors.Add(d);
            for (int y = 0; y < map.Height; y += 2)
            for (int x = 0; x < map.Width; x += 2)
            {
                var t = map.GetTerrain(x, y);
                if ((t == TerrainType.Sidewalk || t == TerrainType.Paving || t == TerrainType.Ground || t == TerrainType.Grass) && map.IsPassable(x, y))
                    walkable.Add(new Vector2Int(x, y));
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
            if (game.Ticks.TicksGame % 30 != 0) return;
            float day = game.Clock.Daylight;
            int hour = game.Clock.Hour;
            bool lateNight = hour >= 1 && hour < 6;

            int people = 0, scooters = 0;
            foreach (var npc in game.Npcs.All)
            {
                if (npc.Kind == NpcKind.Civilian || npc.Kind == NpcKind.Child || npc.Kind == NpcKind.Elder) people++;
                else if (npc.Kind == NpcKind.Scooter) scooters++;
            }

            // Fewer people out where there's been shooting recently.
            bool recentGunfire = game.Ticks.TicksGame - game.Combat.LastShotTick < GameClock.TicksPerHour * 2;
            int peopleTarget = lateNight ? 6 : Mathf.RoundToInt(Mathf.Lerp(14f, 55f, day) * (recentGunfire ? 0.4f : 1f));
            int scooterTarget = lateNight ? 1 : day > 0.3f ? 6 : 3;

            if (people < peopleTarget) SpawnPerson(game);
            if (scooters < scooterTarget && game.Rng.NextDouble() < 0.3) SpawnScooter(game);
            MaybeSendPatrol(game);
        }

        void SpawnPerson(Game game)
        {
            var rng = game.Rng;
            Vector2Int start;
            if (doors.Count > 0 && rng.NextDouble() < 0.7)
            {
                var door = doors[rng.Next(doors.Count)];
                start = FreeNeighbour(game.Map, door) ?? door;
            }
            else if (game.Territory.Entrances.Count > 0) start = game.Territory.RandomEntrance(rng);
            else return;

            double roll = rng.NextDouble();
            NpcKind kind = roll < 0.15 ? NpcKind.Child : roll < 0.32 ? NpcKind.Elder : NpcKind.Civilian;
            float speed = kind == NpcKind.Elder ? 0.042f : kind == NpcKind.Child ? 0.075f : 0.055f + (float)rng.NextDouble() * 0.015f;
            Color32 color = kind == NpcKind.Elder && rng.NextDouble() < 0.6
                ? new Color32(30, 28, 30, 255) // nonna in black
                : (Color32)Color.HSVToRGB((float)rng.NextDouble(), 0.25f + (float)rng.NextDouble() * 0.45f, 0.45f + (float)rng.NextDouble() * 0.5f);

            var npc = game.Npcs.Spawn(kind, start, speed, color);
            npc.TripsLeft = rng.Next(1, 4);
            npc.Target = RandomDestination(game);
            npc.Exit = doors.Count > 0 ? doors[rng.Next(doors.Count)] : start;
        }

        void SpawnScooter(Game game)
        {
            var entrances = game.Territory.Entrances;
            if (entrances.Count < 2) return;
            var rng = game.Rng;
            var from = entrances[rng.Next(entrances.Count)];
            Vector2Int to = from;
            for (int i = 0; i < 10 && (to - from).sqrMagnitude < 2500; i++) to = entrances[rng.Next(entrances.Count)];
            var npc = game.Npcs.Spawn(NpcKind.Scooter, from, 0.2f + (float)rng.NextDouble() * 0.06f,
                (Color32)Color.HSVToRGB((float)rng.NextDouble(), 0.3f, 0.5f + (float)rng.NextDouble() * 0.4f));
            npc.Exit = to;
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

        public Vector2Int RandomDestination(Game game)
        {
            var rng = game.Rng;
            if (doors.Count > 0 && rng.NextDouble() < 0.3)
            {
                var door = doors[rng.Next(doors.Count)];
                return FreeNeighbour(game.Map, door) ?? door;
            }
            return walkable.Count > 0 ? walkable[rng.Next(walkable.Count)] : Vector2Int.zero;
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

        /// <summary>Errands: walk somewhere, linger, repeat a few times, go home through a door.</summary>
        public static void TickCivilian(Npc npc, Game game)
        {
            if (!npc.Panicking && GunfireNear(npc, game))
            {
                // Run away from the shots, towards the nearest edge of the map, and don't come back today.
                npc.Panicking = true;
                npc.State = 2;
                var away = npc.Mover.Position + (npc.Mover.Position - game.Combat.LastShotPosition).normalized * 30f;
                var exit = game.Territory.Entrances.Count > 0 ? game.Territory.Entrances[0] : npc.Exit;
                foreach (var e in game.Territory.Entrances)
                    if ((e - away).sqrMagnitude < (exit - away).sqrMagnitude) exit = e;
                npc.Walker = new Walker(exit, adjacent: false, speedMultiplier: 1.9f);
            }

            switch (npc.State)
            {
                case 0: // walking to a destination
                {
                    npc.Walker ??= new Walker(npc.Target, adjacent: false);
                    var status = npc.Walker.Tick(npc.Mover, 1f, game);
                    if (status == WalkStatus.Walking) return;
                    npc.State = 1;
                    npc.Timer = game.Rng.Next(150, 900);
                    return;
                }
                case 1: // lingering: chatting, looking at shop windows
                {
                    if (--npc.Timer > 0) return;
                    npc.Walker = null;
                    if (--npc.TripsLeft > 0)
                    {
                        npc.Target = game.StreetLife.RandomDestination(game);
                        npc.State = 0;
                    }
                    else
                    {
                        npc.State = 2;
                        npc.Walker = new Walker(npc.Exit, adjacent: true);
                    }
                    return;
                }
                default: // going home
                {
                    if (npc.Walker == null) npc.Walker = new Walker(npc.Exit, adjacent: true);
                    if (npc.Walker.Tick(npc.Mover, 1f, game) != WalkStatus.Walking) npc.Despawned = true;
                    return;
                }
            }
        }

        public static void TickScooter(Npc npc, Game game)
        {
            npc.Walker ??= new Walker(npc.Exit, adjacent: false, roadOnly: true); // scooters stay on the asphalt
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
