using System.Collections.Generic;
using OSistema.Core;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Npcs
{
    /// <summary>
    /// Cars on the asphalt roads (never in the vicoli): they keep to the right, pull round someone on foot when the other side is clear (else stop and hoot),
    /// turn at the crossings, and leave by the edge of the map. Some are the Polizia, the Carabinieri or the
    /// Finanza: a patrol car that sees a gun out or a deal going on pulls over and two officers get out.
    /// </summary>
    public static class Traffic
    {
        const float Cruise = 0.11f;            // cells per tick
        const float PoliceCruise = 0.09f;      // a patrol car takes its time, looking about
        const int MaxLifeTicks = GameClock.TicksPerHour * 3;

        const float HalfLength = 1.7f;         // half a car, in cells (PropArt.CarLength)
        const float LaneFromKerb = 2f;         // a lane's middle is two cells in from the kerb: room for the parked cars

        static readonly List<(Vector2Int cell, Vector2Int dir)> gates = new List<(Vector2Int, Vector2Int)>();
        static Map gatesFor;

        public static bool IsCar(NpcKind k) => k == NpcKind.Car || k == NpcKind.PoliceCar;

        static bool RoadCell(Map map, Vector2Int c) => map.InBounds(c) && map.GetTerrain(c.x, c.y) == TerrainType.Road;

        /// <summary>
        /// The road across, at a point, for something going along <paramref name="dir"/>: its first and last cells
        /// on the cross axis. A car road is at least five wide; the 2-cell lanes in the vicoli are for scooters.
        /// </summary>
        static bool Across(Map map, Vector2 at, Vector2Int dir, out int lo, out int hi)
        {
            var c = Vector2Int.FloorToInt(at);
            lo = hi = 0;
            if (!RoadCell(map, c)) return false;
            var axis = dir.x != 0 ? Vector2Int.up : Vector2Int.right;
            int k0 = dir.x != 0 ? c.y : c.x;
            int l = 0, h = 0;
            while (l < 10 && RoadCell(map, c - axis * (l + 1))) l++;
            while (h < 10 && RoadCell(map, c + axis * (h + 1))) h++;
            lo = k0 - l;
            hi = k0 + h;
            return hi - lo + 1 >= 5;
        }

        /// <summary>Where on the cross axis a car going <paramref name="dir"/> drives: keep right, clear of the parked cars.</summary>
        static float LaneOf(Vector2Int dir, int lo, int hi)
        {
            var right = new Vector2Int(dir.y, -dir.x);
            int s = right.x + right.y;   // +1 if right points up the cross axis
            return s > 0 ? hi + 1 - LaneFromKerb : lo + LaneFromKerb;
        }

        static void SetLane(Npc npc, float lane)
        {
            if (npc.CarDir.x != 0) npc.CarPos.y = lane; else npc.CarPos.x = lane;
        }

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
            // One gate per road per edge: its first cell across.
            if (!Across(map, Mover.CellCenter(c), dir, out int lo, out _)) return;
            if ((dir.x != 0 ? c.y : c.x) != lo) return;
            gates.Add((c, dir));
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
            int want = Mathf.RoundToInt((night ? 3 : hour >= 7 && hour < 20 ? 16 : 8) * (1f - game.Weather.Rain * 0.3f));
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
            if (!Across(game.Map, Mover.CellCenter(cell), dir, out int lo, out int hi)) return;
            var npc = game.Npcs.Spawn(police ? NpcKind.PoliceCar : NpcKind.Car, cell, 0f, new Color32(255, 255, 255, 255));
            npc.CarDir = dir;
            npc.CarSpeed = police ? PoliceCruise : Cruise * (0.85f + (float)rng.NextDouble() * 0.3f);
            // Polizia (two liveries and the van), Carabinieri, Finanza; ordinary cars of every kind.
            npc.CarLook = police ? new[] { 0, 1, 1, 2, 4, 7 }[rng.Next(6)] : rng.Next(8);
            npc.CarPos = Mover.CellCenter(cell);
            SetLane(npc, LaneOf(dir, lo, hi));
            npc.Mover.Place(npc.CarPos);
            npc.Timer = 0;
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
            var nose = pos + fwd * (HalfLength + 0.4f);
            var noseCell = Vector2Int.FloorToInt(nose);
            if (!map.InBounds(noseCell)) { npc.Despawned = true; return; }
            // The end of the road ahead (a T-junction): turn, or turn round.
            if (!RoadCell(map, noseCell))
            {
                if (!TryTurn(npc, game, anywhere: true)) UTurn(npc, game);
                return;
            }
            // Someone on foot in the lane: pull round them if the way past is clear, then back in.
            Steer(npc, game, ref pos, fwd);
            nose = pos + fwd * (HalfLength + 0.4f);
            noseCell = Vector2Int.FloorToInt(nose);
            if (!map.InBounds(noseCell)) { npc.Despawned = true; return; }
            // Someone or something in the road ahead: brake, and hoot if they stay there.
            bool blocked = map.GetBuilding(noseCell.x, noseCell.y) != null || Blocked(npc, game, pos, fwd);
            npc.CarSpeed = Mathf.MoveTowards(npc.CarSpeed, blocked ? 0f : cruise, blocked ? 0.02f : 0.003f);
            if (blocked && npc.CarSpeed < 0.01f && game.Rng.NextDouble() < 0.01)
                OSistema.Views.SoundManager.PlayAt(OSistema.Views.AudioSynth.Horn, pos, 0.3f);

            // Now and then, at a crossing, turn off.
            var cell = Vector2Int.FloorToInt(pos);
            if (npc.LastCell != cell)
            {
                npc.LastCell = cell;
                if (game.Rng.NextDouble() < 0.07 && TryTurn(npc, game, anywhere: false)) return;
            }
            npc.CarPos = pos + fwd * npc.CarSpeed;
            npc.Mover.Place(npc.CarPos);
        }

        /// <summary>
        /// Turn into a road to the left or right, if there is one here. <paramref name="anywhere"/>: at a T-junction,
        /// take whichever there is; otherwise only when the car is level with the side road.
        /// </summary>
        static bool TryTurn(Npc npc, Game game, bool anywhere)
        {
            var map = game.Map;
            var dir = npc.CarDir;
            if (!Across(map, npc.CarPos, dir, out int lo, out int hi)) return false;
            float mid = (lo + hi + 1) * 0.5f;
            float half = (hi - lo + 1) * 0.5f;
            float along = dir.x != 0 ? npc.CarPos.x : npc.CarPos.y;
            var options = new List<(Vector2Int d, float lane)>();
            foreach (var d in new[] { new Vector2Int(-dir.y, dir.x), new Vector2Int(dir.y, -dir.x) })
            {
                // Just past this road's edge, in the side road: is it a car road?
                var probe = dir.x != 0 ? new Vector2(along, mid) : new Vector2(mid, along);
                probe += (Vector2)d * (half + 2.5f);
                if (!Across(map, probe, d, out int lo2, out int hi2)) continue;
                // The side road spans [lo2, hi2] along our way: only turn while we're level with it.
                if (!anywhere && (along < lo2 + 0.5f || along > hi2 + 0.5f)) continue;
                options.Add((d, LaneOf(d, lo2, hi2)));
            }
            if (options.Count == 0) return false;
            var pick = options[game.Rng.Next(options.Count)];
            // Into the side road's lane: the old "along" becomes the new "across", and vice versa.
            npc.CarDir = pick.d;
            npc.CarPos = pick.d.x != 0 ? new Vector2(npc.CarPos.x, pick.lane) : new Vector2(pick.lane, npc.CarPos.y);
            npc.CarSpeed *= 0.5f;   // slow round the corner
            npc.Mover.Place(npc.CarPos);
            return true;
        }

        static void UTurn(Npc npc, Game game)
        {
            npc.CarDir = -npc.CarDir;
            if (!Across(game.Map, npc.CarPos, npc.CarDir, out int lo, out int hi)) { npc.Despawned = true; return; }
            SetLane(npc, LaneOf(npc.CarDir, lo, hi));
            npc.CarSpeed = 0f;
            npc.Mover.Place(npc.CarPos);
        }

        const float SwerveBy = 1.9f, SwerveRate = 0.035f;   // how far out to pull round someone, and how fast

        /// <summary>
        /// Keeps the car in its lane, or pulls it out by a car's width (towards the middle of the road) while
        /// someone on foot is in the lane ahead and the other side is free: no oncoming car, no one in the way.
        /// </summary>
        static void Steer(Npc car, Game game, ref Vector2 pos, Vector2 fwd)
        {
            var map = game.Map;
            if (!Across(map, pos, car.CarDir, out int lo, out int hi)) return;
            float lane = LaneOf(car.CarDir, lo, hi);
            var left = new Vector2(-fwd.y, fwd.x);
            // Which way along the cross axis is "out" (towards the middle of the road).
            float outSign = (left.x + left.y) > 0 ? 1f : -1f;
            float across = car.CarDir.x != 0 ? pos.y : pos.x;
            var inLane = car.CarDir.x != 0 ? new Vector2(pos.x, lane) : new Vector2(lane, pos.y);
            var outLane = inLane + left * SwerveBy;
            bool walkerAhead = WalkerInPath(game, inLane, fwd, HalfLength + 3.2f);
            bool wasOut = Mathf.Abs(across - lane) > 0.3f;
            float target = lane;
            if (walkerAhead && RoadCell(map, Vector2Int.FloorToInt(outLane + fwd * (HalfLength + 1f)))
                && ClearToPass(car, game, outLane, fwd))
                target = lane + outSign * SwerveBy;
            // Already out and still passing someone: stay out until they're behind.
            else if (wasOut && (walkerAhead || WalkerInPath(game, inLane, fwd, HalfLength + 0.5f, behindToo: true)) && ClearToPass(car, game, outLane, fwd))
                target = lane + outSign * SwerveBy;
            float next = Mathf.MoveTowards(across, target, SwerveRate);
            if (Mathf.Approximately(next, across)) return;
            if (car.CarDir.x != 0) pos.y = next; else pos.x = next;
            car.CarPos = pos;
        }

        static bool WalkerInPath(Game game, Vector2 from, Vector2 fwd, float reach, bool behindToo = false)
        {
            bool In(Vector2 at)
            {
                var to = at - from;
                float along = Vector2.Dot(to, fwd);
                if (along > reach || along < (behindToo ? -HalfLength : HalfLength * 0.6f)) return false;
                return Mathf.Abs(to.x * fwd.y - to.y * fwd.x) < 1.1f;
            }
            foreach (var p in game.Pawns.All) if (p.IsPresent && !p.Dead && !p.Riding && In(p.Position)) return true;
            foreach (var n in game.Npcs.All) if (!n.Despawned && !IsCar(n.Kind) && In(n.Mover.Position)) return true;
            return false;
        }

        /// <summary>The other side of the road is free for a pass: no car either way, nobody standing there.</summary>
        static bool ClearToPass(Npc car, Game game, Vector2 at, Vector2 fwd)
        {
            foreach (var n in game.Npcs.All)
            {
                if (n == car || n.Despawned) continue;
                var q = IsCar(n.Kind) ? n.CarPos : n.Mover.Position;
                var to = q - at;
                float along = Vector2.Dot(to, fwd);
                float side = Mathf.Abs(to.x * fwd.y - to.y * fwd.x);
                // An oncoming car is seen from further off: it closes twice as fast.
                float reach = IsCar(n.Kind) ? (Vector2.Dot(n.CarDir, car.CarDir) < 0 ? 16f : HalfLength * 2f + 2f) : HalfLength + 2.5f;
                if (side < (IsCar(n.Kind) ? 1.8f : 1.1f) && along > -HalfLength * 2f && along < reach) return false;
            }
            foreach (var p in game.Pawns.All)
            {
                if (!p.IsPresent || p.Dead) continue;
                var to = p.Position - at;
                float along = Vector2.Dot(to, fwd);
                if (Mathf.Abs(to.x * fwd.y - to.y * fwd.x) < 1.1f && along > -HalfLength && along < HalfLength + 2.5f) return false;
            }
            return true;
        }

        static bool Blocked(Npc car, Game game, Vector2 pos, Vector2 fwd)
        {
            bool InPath(Vector2 at, float reach)
            {
                var to = at - pos;
                float along = Vector2.Dot(to, fwd);
                if (along < HalfLength * 0.6f || along > reach) return false;
                return Mathf.Abs(to.x * fwd.y - to.y * fwd.x) < 1.1f;
            }
            foreach (var p in game.Pawns.All)
                if (p.IsPresent && !p.Dead && InPath(p.Position, HalfLength + 2.2f)) return true;
            foreach (var n in game.Npcs.All)
            {
                if (n == car || n.Despawned) continue;
                if (IsCar(n.Kind))
                {
                    // Oncoming cars are in the other lane; only the one in front holds you up.
                    if (Vector2.Dot(n.CarDir, car.CarDir) <= 0) continue;
                    if (InPath(n.CarPos, HalfLength * 2f + 1.2f)) return true;
                }
                else if (InPath(n.Mover.Position, HalfLength + 2.2f)) return true;
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
