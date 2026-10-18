using System.Collections.Generic;
using OSistema.Combat;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.AI
{
    /// <summary>Jobs for rival clan members on the map.</summary>
    public static class RivalAI
    {
        public static Job Next(Pawn pawn, Game game)
        {
            if (pawn.Health.Downed) return new DownedJob(pawn);
            switch (pawn.Mission)
            {
                case RivalMission.Pusher:
                    if (pawn.MissionSquare != null && game.ClanOf(pawn).Squares.Contains(pawn.MissionSquare))
                        return new RivalPostJob(pawn, pawn.MissionSquare);
                    pawn.Mission = RivalMission.Leave;
                    return new LeaveMapJob(pawn, game);
                case RivalMission.Lookout:
                    if (pawn.MissionSquare != null && game.ClanOf(pawn).Squares.Contains(pawn.MissionSquare))
                        return new RivalPostJob(pawn, pawn.MissionSquare, game.ClanOf(pawn).LookoutPostFor(pawn.MissionSquare, game), 1);
                    pawn.Mission = RivalMission.Leave;
                    return new LeaveMapJob(pawn, game);
                case RivalMission.Patrol:
                    return new RivalPatrolJob(pawn, game);
                case RivalMission.Stesa:
                    return new StesaJob(pawn);
                case RivalMission.Hit:
                    return new HitJob(pawn);
                case RivalMission.Assault:
                    if (pawn.MissionSquare != null) return new AssaultJob(pawn);
                    pawn.Mission = RivalMission.Leave;
                    return new LeaveMapJob(pawn, game);
                case RivalMission.Guard:
                    if (pawn.MissionTarget != null && pawn.MissionTarget.IsPresent) return new GuardJob(pawn);
                    pawn.Mission = RivalMission.Leave;
                    return new LeaveMapJob(pawn, game);
                default:
                    return new LeaveMapJob(pawn, game);
            }
        }
    }

    /// <summary>A rival pusher (or lookout) on their clan's piazza, pacing and fidgeting around the spot.</summary>
    public sealed class RivalPostJob : Job
    {
        public readonly DrugSquare Square;
        readonly Vector2Int post;
        readonly int paceRadius;
        readonly Walker walker;
        Walker pacing;
        int pause;
        int ticksAtPost;

        public bool AtPost { get; private set; }

        public RivalPostJob(Pawn pawn, DrugSquare square) : this(pawn, square, square.Cell, 2) { }

        public RivalPostJob(Pawn pawn, DrugSquare square, Vector2Int post, int paceRadius) : base(pawn)
        {
            Square = square;
            this.post = post;
            this.paceRadius = paceRadius;
            walker = new Walker(post, adjacent: false);
        }

        public override string Label => Pawn.Mission == RivalMission.Lookout ? "Watching for the rivals" : "Selling for the rivals";

        public override bool Tick(Game game)
        {
            if (!game.ClanOf(Pawn).Squares.Contains(Square)) return true;
            if (!AtPost)
            {
                var status = walker.Tick(Pawn, game);
                if (status == WalkStatus.Failed) return Fail();
                AtPost = status == WalkStatus.Arrived;
                pause = game.Rng.Next(100, 400);
                return false;
            }
            if (++ticksAtPost > 6000) return true;

            if (pacing != null)
            {
                if (pacing.Tick(Pawn, game) != WalkStatus.Walking)
                {
                    pacing = null;
                    pause = game.Rng.Next(200, 700);
                }
                return false;
            }
            if (--pause > 0) return false;
            var c = post + new Vector2Int(game.Rng.Next(-paceRadius, paceRadius + 1), game.Rng.Next(-paceRadius, paceRadius + 1));
            if (game.Map.IsPassable(c)) pacing = new Walker(c, adjacent: false, speedMultiplier: 0.7f);
            return false;
        }
    }

    /// <summary>An armed pair walking the clan's turf, and sometimes yours, so everyone sees them.</summary>
    public sealed class RivalPatrolJob : Job
    {
        readonly List<Vector2Int> route = new List<Vector2Int>();
        Walker walker;
        int index;
        int pause;

        public RivalPatrolJob(Pawn pawn, Game game) : base(pawn)
        {
            var rival = game.ClanOf(pawn);
            var rng = game.Rng;
            foreach (var sq in rival.Squares)
            {
                var zone = game.Territory.Zones[sq.ZoneId];
                route.Add(RandomOutdoorCell(zone.Rect, game));
            }
            // Once relations sour, the patrol swings past your nearest piazza to make a point.
            if (rival.Hostility >= 25f && game.Org.Squares.Count > 0)
                route.Insert(rng.Next(route.Count + 1), game.Org.Squares[rng.Next(game.Org.Squares.Count)].Cell);
        }

        static Vector2Int RandomOutdoorCell(RectInt rect, Game game)
        {
            for (int i = 0; i < 20; i++)
            {
                var c = new Vector2Int(game.Rng.Next(rect.xMin, rect.xMax), game.Rng.Next(rect.yMin, rect.yMax));
                if (game.Map.IsPassable(c) && game.Map.GetTerrain(c.x, c.y) != TerrainType.Floor) return c;
            }
            return Vector2Int.RoundToInt(rect.center);
        }

        public override string Label => "Patrolling";

        public override bool Tick(Game game)
        {
            if (index >= route.Count)
            {
                Pawn.Mission = RivalMission.Leave;
                return true;
            }
            if (pause > 0)
            {
                pause--;
                return false;
            }
            walker ??= new Walker(route[index], adjacent: true);
            if (walker.Tick(Pawn, game) == WalkStatus.Walking) return false;
            walker = null;
            index++;
            pause = game.Rng.Next(200, 600);
            return false;
        }
    }

    /// <summary>Ride up to the player's piazza, empty a magazine into the air, leave.</summary>
    public sealed class StesaJob : Job
    {
        const float FiringDistance = 7f;
        const int ShotInterval = 25;

        Walker walker;
        int shotsLeft = 6;
        int cooldown;
        bool firing;

        public StesaJob(Pawn pawn) : base(pawn)
        {
            walker = new Walker(pawn.MissionSquare.Cell, adjacent: true, speedMultiplier: 1.1f);
            pawn.OnScooter = true;   // a drive-by: they come on scooters
        }

        public override string Label => "Stesa";

        public override bool Tick(Game game)
        {
            var square = Pawn.MissionSquare;
            Pawn.OnScooter = true;
            if (!firing && (Pawn.Position - Mover.CellCenter(square.Cell)).magnitude <= FiringDistance)
            {
                // Through the piazza without stopping, emptying the magazine into the air.
                firing = true;
                if (!game.ClanOf(Pawn).StesaInProgress)
                {
                    game.ClanOf(Pawn).StesaInProgress = true;
                    game.ClanOf(Pawn).OnStesa(square, game);
                }
                var exit = game.Territory.Entrances[0];
                foreach (var e in game.Territory.Entrances)
                    if ((e - Pawn.Cell).sqrMagnitude > (exit - Pawn.Cell).sqrMagnitude && (e - Pawn.Cell).sqrMagnitude < 90 * 90) exit = e;
                walker = new Walker(exit, adjacent: false, speedMultiplier: 1.1f);
            }
            var status = walker.Tick(Pawn, game);
            Pawn.OnScooter = true;
            if (!firing)
            {
                if (status != WalkStatus.Walking) firing = true;
                return false;
            }

            if (--cooldown <= 0 && shotsLeft > 0)
            {
                cooldown = ShotInterval + game.Rng.Next(0, 15);
                game.Combat.FireIntoAir(Pawn, game);
                shotsLeft--;
            }
            if (shotsLeft > 0 && status == WalkStatus.Walking) return false;

            Pawn.Mission = RivalMission.Leave;
            Pawn.AggressiveUntil = game.Ticks.TicksGame + 600;
            game.ClanOf(Pawn).StesaInProgress = false;
            return true;
        }
    }

    /// <summary>Hunt down one crew member. The combat system does the shooting once in range.</summary>
    public sealed class HitJob : Job
    {
        const int GiveUpTicks = 9000;
        const float StandOffDistance = 6f;

        Walker walker;
        int ticks;
        bool soughtCover;
        bool movingToCover;

        public HitJob(Pawn pawn) : base(pawn) { }

        public override string Label => "Agguato";

        public override bool Tick(Game game)
        {
            var target = Pawn.MissionTarget;
            bool done = target == null || !target.CanAct;
            if (done || ++ticks > GiveUpTicks || Pawn.Health.Pain > 0.4f)
            {
                Pawn.Mission = RivalMission.Leave;
                Pawn.AggressiveUntil = game.Ticks.TicksGame + 1200; // still dangerous on the way out
                return true;
            }

            if (movingToCover)
            {
                if (walker != null && walker.Tick(Pawn, game) == WalkStatus.Walking) return false;
                movingToCover = false;
                walker = null;
            }

            float dist = (target.Position - Pawn.Position).magnitude;
            if (dist <= StandOffDistance && CombatSystem.HasLineOfSight(game.Map, Pawn.Cell, target.Cell))
            {
                // Professionals: duck behind a car or a wall before trading shots.
                if (!soughtCover && !CombatSystem.IsInCover(game.Map, Pawn.Cell, target.Position))
                {
                    soughtCover = true;
                    var cover = CombatSystem.FindCover(game.Map, Pawn.Cell, target.Position, 3);
                    if (cover.HasValue && cover.Value != Pawn.Cell)
                    {
                        walker = new Walker(cover.Value, adjacent: false, speedMultiplier: 1.2f);
                        movingToCover = true;
                        return false;
                    }
                }
                Pawn.StopMoving();
                walker = null;
                return false; // hold and shoot
            }

            // They come on a scooter and get off to do it.
            Pawn.OnScooter = dist > 12f;
            if (!Pawn.OnScooter) Pawn.Riding = false;
            if (walker == null || (walker.Target - target.Cell).sqrMagnitude > 4)
                walker = new Walker(target.Cell, adjacent: true, speedMultiplier: 1.15f);
            if (walker.Tick(Pawn, game) == WalkStatus.Failed) walker = null;
            return false;
        }
    }

    /// <summary>
    /// A squad sent to take a piazza: ride over, spread out round the corner, shoot whoever's there,
    /// and hold it. Whoever owned it has a minute or two to come and fight back.
    /// </summary>
    public sealed class AssaultJob : Job
    {
        const int HoldTicks = GameClock.TicksPerHour * 2;
        Walker walker;
        int held;
        Vector2Int spot;

        public AssaultJob(Pawn pawn) : base(pawn) { }

        public override string Label => "Taking a piazza";

        public override bool Tick(Game game)
        {
            var sq = Pawn.MissionSquare;
            bool gone = sq == null || (!sq.Rival && !game.Org.Squares.Contains(sq)) || (sq.Rival && !game.Clans[sq.Clan].Squares.Contains(sq));
            if (gone || held > HoldTicks || Pawn.Health.Pain > 0.45f)
            {
                Pawn.Mission = RivalMission.Leave;
                Pawn.AggressiveUntil = game.Ticks.TicksGame + 600;   // still edgy on the way out, not forever
                return true;
            }
            if (walker == null)
            {
                // Each takes a spot around the corner.
                spot = sq.Cell + new Vector2Int(game.Rng.Next(-3, 4), game.Rng.Next(-3, 4));
                if (!game.Map.InBounds(spot) || !game.Map.IsPassable(spot)) spot = sq.Cell;
                Pawn.OnScooter = (Pawn.Position - Mover.CellCenter(spot)).magnitude > 14f;
                walker = new Walker(spot, adjacent: true, speedMultiplier: 1.1f);
            }
            var status = walker.Tick(Pawn, game);
            if (status == WalkStatus.Walking) return false;
            if (status == WalkStatus.Failed) walker = null;
            Pawn.OnScooter = false;
            Pawn.Riding = false;
            held++;
            return false;
        }
    }

    /// <summary>A bodyguard sticks to the man he protects.</summary>
    public sealed class GuardJob : Job
    {
        Walker walker;

        public GuardJob(Pawn pawn) : base(pawn) { }

        public override string Label => "Guarding";

        public override bool Tick(Game game)
        {
            var boss = Pawn.MissionTarget;
            if (boss == null || !boss.IsPresent) { Pawn.Mission = RivalMission.Leave; return true; }
            if ((boss.Position - Pawn.Position).sqrMagnitude < 2.5f * 2.5f) { Pawn.StopMoving(); return false; }
            if (walker == null || (walker.Target - boss.Cell).sqrMagnitude > 4) walker = new Walker(boss.Cell, adjacent: true);
            if (walker.Tick(Pawn, game) == WalkStatus.Failed) walker = null;
            return false;
        }
    }

    /// <summary>Walk off the map at the nearest edge and disappear.</summary>
    public sealed class LeaveMapJob : Job
    {
        readonly Walker walker;

        public LeaveMapJob(Pawn pawn, Game game) : base(pawn)
        {
            var best = game.Territory.Entrances[0];
            foreach (var e in game.Territory.Entrances)
                if ((e - pawn.Cell).sqrMagnitude < (best - pawn.Cell).sqrMagnitude) best = e;
            walker = new Walker(best, adjacent: false, speedMultiplier: 1.2f);
            pawn.OnScooter = true;
        }

        public override string Label => "Leaving";

        public override bool Tick(Game game)
        {
            Pawn.OnScooter = true;   // back on the scooter and away
            if (walker.Tick(Pawn, game) == WalkStatus.Walking) return false;
            Pawn.Departed = true;
            return true;
        }
    }
}
