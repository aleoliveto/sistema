using System.Linq;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.AI
{
    /// <summary>
    /// Two to a scooter: the driver rides, the passenger sits behind him and shoots at the other side
    /// as they go. When the driver stops (to fight, or because they're there) the passenger gets off.
    /// </summary>
    public static class Pillion
    {
        /// <summary>True while the passenger is riding behind the driver this tick (skip his own walking).</summary>
        public static bool Ride(Pawn passenger, Pawn driver, Game game)
        {
            bool riding = driver != null && driver.CanAct && driver.Riding && !driver.Pillion;
            if (riding)
            {
                passenger.Pillion = true;
                passenger.OnScooter = passenger.Riding = false;
                passenger.Heading = driver.Heading;
                passenger.Mover.Place(driver.Position - driver.Forward * 0.38f);
                // Shooting from the back of the scooter at any of theirs in range.
                passenger.OnOperationUntil = game.Ticks.TicksGame + 30;
                return true;
            }
            if (passenger.Pillion)
            {
                passenger.Pillion = false;
                passenger.Mover.Unstick(game.Map);
            }
            return false;
        }
    }

    /// <summary>
    /// A stesa of your own: ride to a rival piazza on the scooter, empty a few rounds over it (and at
    /// whoever of theirs is standing there), and get out before the sirens. A warning, not a war.
    /// </summary>
    public sealed class CrewStesaJob : Job
    {
        readonly RivalClan clan;
        readonly DrugSquare target;
        readonly Pawn driver;   // riding on the back of his scooter, or null to ride his own
        Walker walker;
        int shots, timer;
        bool arrived, done;

        public CrewStesaJob(Pawn pawn, RivalClan clan, DrugSquare target, Pawn driver = null) : base(pawn)
        {
            this.clan = clan;
            this.target = target;
            this.driver = driver;
        }

        public override string Label => arrived ? "Stesa!" : "Riding to the stesa";

        public override bool Tick(Game game)
        {
            long now = game.Ticks.TicksGame;
            if (done || Pawn.Health.Pain > 0.5f) return Home(game);
            if (!arrived)
            {
                if (driver != null && Pillion.Ride(Pawn, driver, game)) return false;
                if (driver != null && (Pawn.Position - Mover.CellCenter(target.Cell)).sqrMagnitude < 7f * 7f) { arrived = true; Pawn.OnOperationUntil = now + 600; return false; }
                Pawn.OnScooter = driver == null && game.Stock.Scooters > 0;
                walker ??= new Walker(target.Cell, adjacent: true, speedMultiplier: 1.15f) { Flexible = true };
                var status = walker.Tick(Pawn, game);
                if (status == WalkStatus.Failed) return Home(game);
                if ((Pawn.Position - Mover.CellCenter(target.Cell)).sqrMagnitude > 6f * 6f && status == WalkStatus.Walking) return false;
                arrived = true;
                Pawn.StopMoving();
                Pawn.OnOperationUntil = now + 600;
                clan.OnStesaAgainst(target, game);
            }
            // A shot every half second or so: into the air, or at one of theirs if he's standing there.
            if (++timer % 30 != 0) return false;
            var mark = clan.Members(game).Where(p => p.CanAct && (p.Position - Pawn.Position).sqrMagnitude < 8f * 8f).OrderBy(p => (p.Position - Pawn.Position).sqrMagnitude).FirstOrDefault();
            if (mark != null && Pawn.Armed) Pawn.ForcedTarget = mark;
            else game.Combat.FireIntoAir(Pawn, game);
            if (++shots >= 6) done = true;
            return false;
        }

        bool Home(Game game)
        {
            Pawn.ForcedTarget = null;
            Pawn.OnOperationUntil = 0;
            // Out of there, fast: the job after this one is the ride home.
            Pawn.OnScooter = game.Stock.Scooters > 0;
            Pawn.QueueJob(new GotoJob(Pawn, Vector2Int.RoundToInt(game.PlayerBase.center), "Getting away"));
            return true;
        }
    }

    /// <summary>
    /// A killing: find the man, get close, and shoot until he's down. Then away. If the hitman is hit
    /// badly, he runs.
    /// </summary>
    public sealed class CrewHitJob : Job
    {
        readonly Pawn target;
        readonly Pawn driver;
        Walker walker;
        int ticks;

        public CrewHitJob(Pawn pawn, Pawn target, Pawn driver = null) : base(pawn)
        {
            this.target = target;
            this.driver = driver;
        }

        public override string Label => $"Hunting {target.DisplayName}";

        public override bool Tick(Game game)
        {
            long now = game.Ticks.TicksGame;
            if (driver != null && Pillion.Ride(Pawn, driver, game)) return false;
            // Down but not dead: walk up and finish it. Nobody leaves a witness breathing.
            if (target != null && !target.Dead && target.Health.Downed && Pawn.Health.Pain <= 0.5f)
            {
                float dd = (target.Position - Pawn.Position).magnitude;
                if (dd < 1.8f)
                {
                    game.Combat.Execute(Pawn, target, game);
                    return false;
                }
                if (walker == null || (walker.Target - target.Cell).sqrMagnitude > 1) walker = new Walker(target.Cell, adjacent: true) { Flexible = true };
                if (walker.Tick(Pawn, game) == WalkStatus.Failed) walker = null;
                return false;
            }
            if (target == null || !target.IsPresent || target.Dead || Pawn.Health.Pain > 0.5f || ++ticks > GameClock.TicksPerHour * 4)
            {
                Pawn.ForcedTarget = null;
                Pawn.OnOperationUntil = 0;
                Pawn.OnScooter = game.Stock.Scooters > 0;
                Pawn.QueueJob(new GotoJob(Pawn, Vector2Int.RoundToInt(game.PlayerBase.center), "Getting away"));
                return true;
            }
            float d = (target.Position - Pawn.Position).magnitude;
            Pawn.OnScooter = driver == null && d > 14f && game.Stock.Scooters > 0;
            if (d < 7f && Combat.CombatSystem.HasLineOfSight(game.Map, Pawn.Cell, target.Cell))
            {
                Pawn.StopMoving();
                Pawn.Riding = false;
                Pawn.ForcedTarget = target;
                Pawn.OnOperationUntil = now + 300;
                return false;
            }
            if (walker == null || (walker.Target - target.Cell).sqrMagnitude > 4) walker = new Walker(target.Cell, adjacent: true, speedMultiplier: 1.1f) { Flexible = true };
            if (walker.Tick(Pawn, game) == WalkStatus.Failed) walker = null;
            return false;
        }
    }
}
