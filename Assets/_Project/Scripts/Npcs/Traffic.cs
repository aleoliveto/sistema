using System.Collections.Generic;
using OSistema.Core;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Npcs
{
    /// <summary>
    /// Cars on the asphalt roads (never in the vicoli): they keep to the right, stop and hoot for anyone in the way,
    /// turn at the crossings, and leave by the edge of the map. Some are the Polizia, the Carabinieri or the
    /// Finanza: a patrol car that sees a gun out or a deal going on pulls over and two officers get out.
    /// </summary>
    public static class Traffic
    {
        const float Cruise = 0.11f;            // cells per tick
        const float PoliceCruise = 0.09f;      // a patrol car takes its time, looking about
        const int MaxLifeTicks = GameClock.TicksPerHour * 3;

        static readonly List<(Vector2Int cell, Vector2Int dir)> gates = new List<(Vector2Int, Vector2Int)>();
        static Map gatesFor;

        public static bool IsCar(NpcKind k) => k == NpcKind.Car || k == NpcKind.PoliceCar;

        static bool Drivable(Map map, Vector2Int c) =>
            map.InBounds(c) && map.GetTerrain(c.x, c.y) == TerrainType.Road && map.GetBuilding(c.x, c.y) == null;

        // Where the roads leave the map: cars come in there.
        static void FindGates(Map map)
        {
            gates.Clear();
            gatesFor = map;
            for (int x = 0; x < map.Width; x++)
            {
                Add(map, new Vector2Int(x, 0), Vector2Int.up);
                Add(map, new Vector2Int(x, map.Height - 1), Vector2Int.down);
            }
            for (int y = 0; y < map.Height; y++)
            {
                Add(map, new Vector2Int(0, y), Vector2Int.right);
                Add(map, new Vector2Int(map.Width - 1, y), Vector2Int.left);
            }
        }

        static void Add(Map map, Vector2Int c, Vector2Int dir)
        {
            if (Drivable(map, c) && Drivable(map, c + dir) && Drivable(map, c + dir * 2)) gates.Add((c, dir));
        }

        /// <summary>Every couple of seconds: enough cars for the hour, a patrol car or two when the rioni are hot.</summary>
        public static void Populate(Game game)
        {
            var map = game.Map;
            if (gatesFor != map) FindGates(map);
            if (gates.Count == 0) return;
            int cars = 0, police = 0;
            foreach (var n in game.Npcs.All)
            {
                if (n.Kind == NpcKind.Car) cars++;
                else if (n.Kind == NpcKind.PoliceCar) police++;
            }
            int hour = game.Clock.Hour;
            bool night = hour >= 1 && hour < 6;
            int want = Mathf.RoundToInt((night ? 2 : hour >= 7 && hour < 20 ? 12 : 6) * (1f - game.Weather.Rain * 0.3f));
            float hottest = 0f;
            foreach (var z in game.Territory.Zones) hottest = Mathf.Max(hottest, z.Heat);
            int wantPolice = hottest > 60f ? 3 : hottest > 30f ? 2 : 1;
            var rng = game.Rng;
            if (cars < want && rng.NextDouble() < 0.3) Spawn(game, police: false);
            else if (police < wantPolice && rng.NextDouble() < 0.04) Spawn(game, police: true);
        }

        static void Spawn(Game game, bool police)
        {
            var rng = game.Rng;
            var (cell, dir) = gates[rng.Next(gates.Count)];
            var npc = game.Npcs.Spawn(police ? NpcKind.PoliceCar : NpcKind.Car, cell, 0f, new Color32(255, 255, 255, 255));
            npc.CarDir = dir;
            npc.CarSpeed = police ? PoliceCruise : Cruise * (0.85f + (float)rng.NextDouble() * 0.3f);
            // Polizia (two liveries and the van), Carabinieri, Finanza; ordinary cars of every kind.
            npc.CarLook = police ? new[] { 0, 1, 1, 2, 4, 7 }[rng.Next(6)] : rng.Next(8);
            npc.CarPos = Mover.CellCenter(cell);
            Relane(npc, game.Map);
            npc.Mover.Place(npc.CarPos);
            npc.Timer = 0;
        }

        // Keep to the right: the outermost lane of the road on the right-hand side of the way it's going.
        static void Relane(Npc npc, Map map)
        {
            var dir = npc.CarDir;
            var right = new Vector2Int(dir.y, -dir.x);
            var c = Vector2Int.FloorToInt(npc.CarPos);
            var lane = c;
            for (int k = 1; k <= 2 && Drivable(map, c + right * k); k++) lane = c + right * k;
            var center = Mover.CellCenter(lane);
            if (dir.x != 0) npc.CarPos.y = center.y; else npc.CarPos.x = center.x;
        }

        public static void Tick(Npc npc, Game game)
        {
            var map = game.Map;
            if (++npc.Timer > MaxLifeTicks) { npc.Despawned = true; return; }
            if (npc.Kind == NpcKind.PoliceCar && PoliceWatch(npc, game)) return;
            if (npc.State == 1)   // pulled over: waiting for its officers
            {
                if (--npc.TripsLeft <= 0) npc.State = 0;
                return;
            }

            var pos = npc.CarPos;
            var fwd = (Vector2)npc.CarDir;
            float cruise = npc.Kind == NpcKind.PoliceCar ? PoliceCruise : Cruise;
            // Someone in the road ahead: brake, and hoot if they stay there.
            bool blocked = Blocked(npc, game, pos, fwd);
            npc.CarSpeed = Mathf.MoveTowards(npc.CarSpeed, blocked ? 0f : cruise, blocked ? 0.02f : 0.003f);
            if (blocked && npc.CarSpeed < 0.01f && game.Rng.NextDouble() < 0.01)
                OSistema.Views.SoundManager.PlayAt(OSistema.Views.AudioSynth.Horn, pos, 0.3f);

            var cell = Vector2Int.FloorToInt(pos);
            var aheadCell = Vector2Int.FloorToInt(pos + fwd * 0.75f);
            var left = new Vector2Int(-npc.CarDir.y, npc.CarDir.x);
            if (aheadCell != cell && !Drivable(map, aheadCell))
            {
                if (!map.InBounds(aheadCell)) { npc.Despawned = true; return; }
                // A parked car in the lane: pull out round it.
                if (map.GetTerrain(aheadCell.x, aheadCell.y) == TerrainType.Road && Drivable(map, cell + left) && Drivable(map, aheadCell + left))
                {
                    npc.CarPos += (Vector2)left;
                    npc.Mover.Place(npc.CarPos);
                    return;
                }
                Turn(npc, game, cell, mustTurn: true);
                return;
            }
            // Back in to the right once the way is clear there.
            if (npc.Timer % 20 == 0 && Drivable(map, cell - left) && Drivable(map, aheadCell - left) && Drivable(map, aheadCell - left + npc.CarDir))
                npc.CarPos -= (Vector2)left;
            // Now and then, at a crossing, turn off.
            var center = Mover.CellCenter(cell);
            if (npc.LastCell != cell)
            {
                npc.LastCell = cell;
                if ((pos - center).sqrMagnitude < 0.3f && game.Rng.NextDouble() < 0.06) Turn(npc, game, cell, mustTurn: false);
            }
            npc.CarPos = pos + fwd * npc.CarSpeed;
            npc.Mover.Place(npc.CarPos);
        }

        static void Turn(Npc npc, Game game, Vector2Int cell, bool mustTurn)
        {
            var map = game.Map;
            var dir = npc.CarDir;
            var left = new Vector2Int(-dir.y, dir.x);
            var right = -left;
            // A turn needs a real road that way, not just the next lane across.
            bool Road(Vector2Int d) => Drivable(map, cell + d) && Drivable(map, cell + d * 2) && Drivable(map, cell + d * 3) && Drivable(map, cell + d * 4);
            var options = new List<Vector2Int>();
            if (Road(left)) options.Add(left);
            if (Road(right)) options.Add(right);
            if (options.Count == 0)
            {
                if (!mustTurn) return;
                // A dead end: turn round, or give up and vanish if even that's blocked.
                if (Drivable(map, cell - dir)) options.Add(-dir);
                else { npc.Despawned = true; return; }
            }
            npc.CarDir = options[game.Rng.Next(options.Count)];
            npc.CarPos = Mover.CellCenter(cell);
            Relane(npc, map);
            npc.Mover.Place(npc.CarPos);
        }

        static bool Blocked(Npc car, Game game, Vector2 pos, Vector2 fwd)
        {
            bool InPath(Vector2 at)
            {
                var to = at - pos;
                float along = Vector2.Dot(to, fwd);
                if (along < 0.6f || along > 2.6f) return false;
                return Mathf.Abs(to.x * fwd.y - to.y * fwd.x) < 0.85f;
            }
            foreach (var p in game.Pawns.All)
                if (p.IsPresent && !p.Dead && InPath(p.Position)) return true;
            foreach (var n in game.Npcs.All)
            {
                if (n == car || n.Despawned) continue;
                // Oncoming cars squeeze past; only the one in front holds you up.
                if (IsCar(n.Kind) && Vector2.Dot(n.CarDir, car.CarDir) <= 0) continue;
                var at = IsCar(n.Kind) ? n.CarPos : n.Mover.Position;
                if (InPath(at)) return true;
            }
            return false;
        }

        /// <summary>
        /// A patrol car sees a gun in someone's hand or a deal on a corner near the road: it pulls over and two
        /// officers get out. True when it stopped this tick.
        /// </summary>
        static bool PoliceWatch(Npc car, Game game)
        {
            if (car.State == 1 || game.Ticks.TicksGame % 15 != 0) return false;
            var me = game.Player;
            if (me == null || !me.IsPresent || !me.CanAct || game.Pursuit.Active) return false;
            if ((me.Position - car.CarPos).sqrMagnitude > 9f * 9f) return false;
            if (!OSistema.Combat.CombatSystem.HasLineOfSight(game.Map, Vector2Int.FloorToInt(car.CarPos), me.Cell)) return false;
            string why = me.Armed && me.AimPoint.HasValue ? "A patrol car saw you with a gun in your hand."
                       : game.Org.DealingCorner != null || game.Org.WaitingCustomer(game) != null ? "A patrol car pulled up while you were dealing."
                       : null;
            if (why == null) return false;
            // Pull over, lights on; two officers out after you.
            car.State = 1;
            car.TripsLeft = GameClock.TicksPerHour;
            car.CarSpeed = 0f;
            var cell = Vector2Int.FloorToInt(car.CarPos);
            Npc first = null;
            for (int i = 0; i < 2; i++)
            {
                var cop = game.Npcs.Spawn(NpcKind.Police, cell, 0.05f, new Color32(30, 50, 130, 255));
                cop.IsPatrol = true;
                cop.TripsLeft = 2;
                cop.Target = me.Cell;
                cop.Exit = cell;
                first ??= cop;
            }
            game.Pursuit.Start(first, why, game);
            return true;
        }
    }
}
