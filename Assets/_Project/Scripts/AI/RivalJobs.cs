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
                    if (pawn.MissionSquare != null && game.Rival.Squares.Contains(pawn.MissionSquare))
                        return new RivalPostJob(pawn, pawn.MissionSquare);
                    pawn.Mission = RivalMission.Leave;
                    return new LeaveMapJob(pawn, game);
                case RivalMission.Stesa:
                    return new StesaJob(pawn);
                case RivalMission.Hit:
                    return new HitJob(pawn);
                default:
                    return new LeaveMapJob(pawn, game);
            }
        }
    }

    /// <summary>A rival pusher standing on their clan's piazza.</summary>
    public sealed class RivalPostJob : Job
    {
        public readonly DrugSquare Square;
        readonly Walker walker;
        int ticksAtPost;

        public bool AtPost { get; private set; }

        public RivalPostJob(Pawn pawn, DrugSquare square) : base(pawn)
        {
            Square = square;
            walker = new Walker(square.Cell, adjacent: false);
        }

        public override string Label => "Selling for the rivals";

        public override bool Tick(Game game)
        {
            if (!game.Rival.Squares.Contains(Square)) return true;
            if (!AtPost)
            {
                var status = walker.Tick(Pawn, game);
                if (status == WalkStatus.Failed) return Fail();
                AtPost = status == WalkStatus.Arrived;
                return false;
            }
            return ++ticksAtPost > 6000;
        }
    }

    /// <summary>Ride up to the player's piazza, empty a magazine into the air, leave.</summary>
    public sealed class StesaJob : Job
    {
        const float FiringDistance = 7f;
        const int ShotInterval = 25;

        readonly Walker walker;
        int shotsLeft = 5;
        int cooldown;
        bool firing;

        public StesaJob(Pawn pawn) : base(pawn)
        {
            walker = new Walker(pawn.MissionSquare.Cell, adjacent: true, speedMultiplier: 1.3f); // on scooters
        }

        public override string Label => "Stesa";

        public override bool Tick(Game game)
        {
            var square = Pawn.MissionSquare;
            if (!firing)
            {
                if ((Pawn.Position - Mover.CellCenter(square.Cell)).magnitude <= FiringDistance)
                {
                    firing = true;
                    Pawn.StopMoving();
                    if (!game.Rival.StesaInProgress)
                    {
                        game.Rival.StesaInProgress = true;
                        game.Rival.OnStesa(square, game);
                    }
                    return false;
                }
                var status = walker.Tick(Pawn, game);
                if (status != WalkStatus.Walking) firing = true;
                return false;
            }

            if (--cooldown > 0) return false;
            cooldown = ShotInterval + game.Rng.Next(0, 15);
            game.Combat.FireIntoAir(Pawn, game);
            if (--shotsLeft > 0) return false;

            Pawn.Mission = RivalMission.Leave;
            Pawn.AggressiveUntil = game.Ticks.TicksGame + 600;
            game.Rival.StesaInProgress = false;
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

            float dist = (target.Position - Pawn.Position).magnitude;
            if (dist <= StandOffDistance && CombatSystem.HasLineOfSight(game.Map, Pawn.Cell, target.Cell))
            {
                Pawn.StopMoving();
                walker = null;
                return false; // hold and shoot
            }

            if (walker == null || (walker.Target - target.Cell).sqrMagnitude > 4)
                walker = new Walker(target.Cell, adjacent: true, speedMultiplier: 1.15f);
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
        }

        public override string Label => "Leaving";

        public override bool Tick(Game game)
        {
            if (walker.Tick(Pawn, game) == WalkStatus.Walking) return false;
            Pawn.Departed = true;
            return true;
        }
    }
}
