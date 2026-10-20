using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.AI
{
    /// <summary>A crew member at a table, turning inputs into goods while the boss does other things.</summary>
    public abstract class StationJob : Job
    {
        readonly Vector2Int station;
        readonly Walker walker;
        bool arrived;
        int work;

        protected StationJob(Pawn pawn, Vector2Int station) : base(pawn)
        {
            this.station = station;
            walker = new Walker(station, adjacent: true);
        }

        bool claimed;

        public override bool IsWork => true;
        public override float Progress => arrived ? work / (float)Mathf.Max(1, lastTicksPerItem) : -1f;
        int lastTicksPerItem = 1;
        protected abstract BuildingDef Station { get; }
        protected abstract bool HasInputs(Game game);
        protected abstract int TicksPerItem(Game game);
        protected abstract void Produce(Game game);

        public override bool Tick(Game game)
        {
            if (game.Map.GetBuilding(station) != Station || !HasInputs(game)) return true;
            if (!claimed)
            {
                var other = game.WorkerAt(station);
                if (other != null && other != Pawn) return true;
                game.StationClaims[station] = Pawn;
                claimed = true;
            }
            if (!arrived)
            {
                var status = walker.Tick(Pawn, game);
                if (status == WalkStatus.Failed) return Fail();
                if (status == WalkStatus.Walking) return false;
                arrived = true;
                Pawn.AimPoint = Mover.CellCenter(station);
            }
            lastTicksPerItem = TicksPerItem(game);
            if (++work < lastTicksPerItem) return false;
            work = 0;
            Produce(game);
            return false;
        }

        public override void OnEnd(Game game)
        {
            Pawn.AimPoint = null;
            if (claimed && game.StationClaims.TryGetValue(station, out var p) && p == Pawn) game.StationClaims.Remove(station);
        }

        public static bool TryFind(Pawn pawn, BuildingDef def, Game game, out Vector2Int station)
        {
            station = default;
            int best = int.MaxValue;
            bool found = false;
            foreach (var c in game.Map.PositionsOf(def))
            {
                if (!game.PlayerBase.Contains(c) && !game.Properties.IsOwnedHome(c)) continue;
                var busy = game.WorkerAt(c);
                if (busy != null && busy != pawn) continue;   // one person per table
                int d = GridUtil.Manhattan(pawn.Cell, c);
                if (d < best)
                {
                    best = d;
                    station = c;
                    found = true;
                }
            }
            return found;
        }
    }

    public sealed class BagJob : StationJob
    {
        public BagJob(Pawn pawn, Vector2Int station) : base(pawn, station) { }
        public override string Label => "Cutting and bagging doses";
        protected override BuildingDef Station => BuildingDefs.BaggingTable;
        protected override bool HasInputs(Game game) => game.Org.CanMakeDose(game);

        protected override int TicksPerItem(Game game)
        {
            var s = game.Stock;
            float speed = (1f + 0.08f * Pawn.CookingSkill) * (s.DigitalScale ? 1.4f : 1f) * (s.HeatSealer ? 1.3f : 1f);
            return Mathf.RoundToInt(300f / speed);
        }

        protected override void Produce(Game game)
        {
            // A careful hand weighs close to half a gram; the digital scale helps everyone.
            float spread = Mathf.Max(0.02f, 0.14f - 0.008f * Pawn.CookingSkill - (game.Stock.DigitalScale ? 0.05f : 0f));
            float grams = Prices.DoseDg / 10f + ((float)game.Rng.NextDouble() * 2f - 1f) * spread;
            game.Org.MakeDose(grams, game);
            Pawn.Learn(Skill.Cooking, 1.5f, game);
        }
    }

    public sealed class RollJob : StationJob
    {
        public RollJob(Pawn pawn, Vector2Int station) : base(pawn, station) { }
        public override string Label => "Rolling ROSSE";
        protected override BuildingDef Station => BuildingDefs.RollingTable;
        protected override bool HasInputs(Game game) => game.Org.CanMakePack(game) && game.Stock.TobaccoG >= Prices.TobaccoPerPack;

        protected override int TicksPerItem(Game game)
        {
            float speed = (1f + 0.07f * Pawn.ConstructionSkill) * (game.Stock.RollingMachine ? 1.6f : 1f);
            return Mathf.RoundToInt(1400f / speed);
        }

        protected override void Produce(Game game)
        {
            float q = 0.5f + 0.03f * Pawn.ConstructionSkill + (game.Stock.RollingMachine ? 0.15f : 0f) + ((float)game.Rng.NextDouble() - 0.5f) * 0.2f;
            game.Org.MakePack(Prices.TobaccoPerPack, q, game);
            Pawn.Learn(Skill.Construction, 2f, game);
        }
    }

    /// <summary>
    /// Stays close to the boss, in the formation slot the player gave them (close, behind, at his left or
    /// right, ahead, or wide on the approaches), turned the way the boss is going. When the boss rides,
    /// one bodyguard hops on the back; others take the family's other scooters and follow. Or, given a
    /// post (the basso's door, a piazza), they stand there instead. The combat system makes them shoot
    /// when there's trouble.
    /// </summary>
    public sealed class BodyguardJob : Job
    {
        public const int SlotClose = 0, SlotBehind = 1, SlotLeft = 2, SlotRight = 3, SlotAhead = 4, SlotWide = 5;
        public static readonly string[] SlotNames = { "Close", "Behind", "Left", "Right", "Ahead", "Wide" };

        Walker walker;
        Vector2Int lastTarget;
        Vector2 lastBossPos, facing = Vector2.up;
        int wait;

        public BodyguardJob(Pawn pawn) : base(pawn) { }
        public override string Label => Pawn.GuardPost.HasValue ? "Standing guard" : Pawn.Pillion ? "On the back of the boss's scooter" : Pawn.OnScooter ? "Riding behind the boss" : "Watching the boss's back";
        public override bool IsWork => true;

        /// <summary>Where in the formation this bodyguard stands, relative to the boss: (right, forward) in cells.</summary>
        static Vector2 SlotOffset(int slot, int id) => slot switch
        {
            SlotBehind => new Vector2(0f, -1.6f),
            SlotLeft => new Vector2(-1.6f, -0.3f),
            SlotRight => new Vector2(1.6f, -0.3f),
            SlotAhead => new Vector2(0f, 1.9f),
            SlotWide => new Vector2(id % 2 == 0 ? -3f : 3f, -1.8f),
            _ => Vector2.zero,
        };

        public override bool Tick(Game game)
        {
            if (Pawn.GuardPost.HasValue) return TickPost(game);
            var boss = game.Player;
            if (boss == null || !boss.IsPresent || boss.InJail) return true;
            int slot = Pawn.GuardSlot;
            bool closeSlot = slot == SlotClose || slot == SlotBehind;

            if (boss.Riding && !boss.StolenScooter)
            {
                if (Pawn.Pillion || (closeSlot && FreePillionSeat(game)))
                {
                    // On the back: wherever the boss goes, a hand on his shoulder.
                    Pawn.Pillion = true;
                    Pawn.OnScooter = Pawn.Riding = false;
                    Pawn.Heading = boss.Heading;
                    Pawn.Mover.Place(boss.Position - boss.Forward * 0.38f);
                    walker = null;
                    return false;
                }
                // A scooter of his own, if the family has one spare.
                if (!Pawn.OnScooter && SpareScooter(game)) Pawn.OnScooter = true;
            }
            else
            {
                if (Pawn.Pillion)
                {
                    // Hop off beside him.
                    Pawn.Pillion = false;
                    var beside = boss.Position + new Vector2(0.6f, -0.3f);
                    Pawn.Mover.Place(game.Map.IsPassable(Vector2Int.FloorToInt(beside)) ? beside : boss.Position);
                }
                if (Pawn.OnScooter && !boss.Riding) Pawn.OnScooter = Pawn.Riding = false;
            }

            // Which way the boss is heading: the way he last moved (or his scooter's nose).
            var moved = boss.Position - lastBossPos;
            if (boss.Riding) facing = boss.Forward;
            else if (moved.sqrMagnitude > 0.0025f && moved.sqrMagnitude < 4f) facing = moved.normalized;
            lastBossPos = boss.Position;

            Vector2 spot = boss.Position;
            if (slot != SlotClose && !boss.Riding)
            {
                var o = SlotOffset(slot, Pawn.Id);
                var right = new Vector2(facing.y, -facing.x);
                var want = boss.Position + right * o.x + facing * o.y;
                if (game.Map.IsPassable(Vector2Int.FloorToInt(want))) spot = want;
            }
            float d = (spot - Pawn.Position).magnitude;
            float near = slot == SlotClose || boss.Riding ? 2.2f : 0.9f;
            if (d < near && walker == null)
            {
                if (++wait > 120) return true;   // re-think now and then (food, sleep)
                return false;
            }
            wait = 0;
            var cell = Vector2Int.FloorToInt(spot);
            bool adjacent = slot == SlotClose || boss.Riding || cell == boss.Cell;
            if (walker == null || (cell - lastTarget).sqrMagnitude > (adjacent ? 4 : 1))
            {
                lastTarget = cell;
                walker = new Walker(adjacent ? boss.Cell : cell, adjacent: adjacent, speedMultiplier: Pawn.OnScooter ? 1.25f : 1.2f) { Flexible = true };
            }
            bool keepScooter = Pawn.OnScooter;
            var status = walker.Tick(Pawn, game);
            if (keepScooter && boss.Riding) Pawn.OnScooter = true;   // the walker parks it on arrival; we're still following
            if (status != WalkStatus.Walking) walker = null;
            return status == WalkStatus.Failed;
        }

        /// <summary>A fixed post: walk there and stand, looking out.</summary>
        bool TickPost(Game game)
        {
            if (Pawn.Pillion) { Pawn.Pillion = false; Pawn.Mover.Place(Mover.CellCenter(Pawn.Cell)); }
            var post = Pawn.GuardPost.Value;
            if (Pawn.Cell == post && walker == null)
            {
                if (++wait > 240) return true;   // re-think now and then (food, sleep)
                return false;
            }
            wait = 0;
            if (walker == null) walker = new Walker(post, adjacent: false) { Flexible = true };
            var status = walker.Tick(Pawn, game);
            if (status != WalkStatus.Walking) walker = null;
            return status == WalkStatus.Failed;
        }

        /// <summary>A free cell to stand guard at the basso's door (the n-th guard gets the n-th spot).</summary>
        public static Vector2Int? HomePost(Game game, Pawn guard)
        {
            var home = game.PlayerBase;
            var spots = new System.Collections.Generic.List<Vector2Int>();
            for (int x = home.xMin - 1; x <= home.xMax; x++)
            for (int y = home.yMin - 1; y <= home.yMax; y++)
            {
                var c = new Vector2Int(x, y);
                if (game.Map.GetBuilding(c) != BuildingDefs.Door) continue;
                foreach (var n in GridUtil.CellsAround(c, 2))
                    if (!home.Contains(n) && game.Map.IsPassable(n) && game.Map.GetBuilding(n) == null && !spots.Contains(n)) spots.Add(n);
            }
            return FreeSpot(game, guard, spots);
        }

        /// <summary>A free cell two steps off a piazza, to stand guard over it.</summary>
        public static Vector2Int? PiazzaPost(Game game, Pawn guard, DrugSquare sq)
        {
            var spots = new System.Collections.Generic.List<Vector2Int>();
            foreach (var c in GridUtil.CellsAround(sq.Cell, 3))
                if (GridUtil.Manhattan(c, sq.Cell) >= 2 && game.Map.IsPassable(c) && game.Map.GetBuilding(c) == null) spots.Add(c);
            return FreeSpot(game, guard, spots);
        }

        static Vector2Int? FreeSpot(Game game, Pawn guard, System.Collections.Generic.List<Vector2Int> spots)
        {
            foreach (var c in spots)
            {
                bool taken = false;
                foreach (var p in game.Pawns.Crew) if (p != guard && p.GuardPost == c) taken = true;
                if (!taken) return c;
            }
            return spots.Count > 0 ? spots[0] : (Vector2Int?)null;
        }

        public override void OnEnd(Game game)
        {
            if (Pawn.Pillion)
            {
                Pawn.Pillion = false;
                Pawn.Mover.Place(Mover.CellCenter(Pawn.Cell));
            }
        }

        bool FreePillionSeat(Game game)
        {
            foreach (var p in game.Pawns.Crew)
                if (p != Pawn && p.Pillion && p.CurrentJob is BodyguardJob) return false;   // the boss's seat is taken
            return true;
        }

        bool SpareScooter(Game game)
        {
            int inUse = 0;
            foreach (var p in game.Pawns.Crew) if (p.OnScooter || (p.Controlled && p.Riding)) inUse++;
            return game.Stock.Scooters - inUse > 0;
        }
    }
}
