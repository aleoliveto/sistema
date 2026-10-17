using OSistema.Core;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.AI
{
    /// <summary>A unit of work a pawn performs over many ticks.</summary>
    public abstract class Job
    {
        protected readonly Pawn Pawn;

        protected Job(Pawn pawn) => Pawn = pawn;

        public abstract string Label { get; }

        /// <summary>Idle jobs are dropped as soon as anything useful comes up.</summary>
        public virtual bool IsIdle => false;

        /// <summary>Work jobs are dropped when a need becomes urgent.</summary>
        public virtual bool IsWork => false;

        /// <summary>Set when the job ended without doing its thing (unreachable, out of stock…).</summary>
        public bool Failed { get; protected set; }

        /// <summary>Advance one tick. Returns true when the job is over (done or failed).</summary>
        public abstract bool Tick(Game game);

        public virtual void OnEnd(Game game) { }

        protected bool Fail()
        {
            Failed = true;
            return true;
        }
    }

    public enum WalkStatus { Walking, Arrived, Failed }

    /// <summary>Path-following helper shared by jobs and NPCs: walks onto, or next to, a target cell, re-pathing when blocked.</summary>
    public sealed class Walker
    {
        Vector2Int target;
        readonly bool adjacent;
        readonly float speedMultiplier;
        bool started;
        int repaths;

        public Walker(Vector2Int target, bool adjacent, float speedMultiplier = 1f)
        {
            this.target = target;
            this.adjacent = adjacent;
            this.speedMultiplier = speedMultiplier;
        }

        public Vector2Int Target => target;

        public void Retarget(Vector2Int newTarget)
        {
            target = newTarget;
            started = false;
        }

        public WalkStatus Tick(Pawn pawn, Game game) => Tick(pawn.Mover, pawn.SpeedFactor, game);

        public WalkStatus Tick(Mover mover, float speedFactor, Game game)
        {
            if (!started)
            {
                var path = game.Pathfinder.FindPath(mover.Cell, target, adjacent);
                if (path == null) return WalkStatus.Failed;
                mover.SetPath(path);
                started = true;
            }

            mover.SpeedFactor = speedFactor * speedMultiplier;
            var result = mover.Tick(game.Map);
            if (result == MoveResult.Blocked)
            {
                if (++repaths > 3) return WalkStatus.Failed;
                started = false;
                return WalkStatus.Walking;
            }
            return result == MoveResult.Arrived ? WalkStatus.Arrived : WalkStatus.Walking;
        }

        public static bool IsNextTo(Vector2Int a, Vector2Int b) => GridUtil.Manhattan(a, b) == 1;
    }

    public sealed class GotoJob : Job
    {
        readonly Walker walker;
        readonly string label;

        public GotoJob(Pawn pawn, Vector2Int target, string label = "Moving") : base(pawn)
        {
            walker = new Walker(target, adjacent: false);
            this.label = label;
        }

        public override string Label => label;

        public override bool Tick(Game game)
        {
            var status = walker.Tick(Pawn, game);
            if (status == WalkStatus.Failed) return Fail();
            return status == WalkStatus.Arrived;
        }
    }

    public sealed class WaitJob : Job
    {
        readonly string label;
        readonly bool idle;
        int ticksLeft;

        public WaitJob(Pawn pawn, int ticks, string label, bool idle = true) : base(pawn)
        {
            ticksLeft = ticks;
            this.label = label;
            this.idle = idle;
        }

        public override string Label => label;
        public override bool IsIdle => idle;

        public override bool Tick(Game game) => --ticksLeft <= 0;
    }

    /// <summary>Stroll to a spot at home and hang around.</summary>
    public sealed class WanderJob : Job
    {
        readonly Walker walker;
        int waitTicks;
        bool arrived;

        public WanderJob(Pawn pawn, Vector2Int target, int waitTicks) : base(pawn)
        {
            walker = new Walker(target, adjacent: false);
            this.waitTicks = waitTicks;
        }

        public override string Label => arrived ? "Hanging around" : "Wandering";
        public override bool IsIdle => true;

        public override bool Tick(Game game)
        {
            if (!arrived)
            {
                var status = walker.Tick(Pawn, game);
                if (status == WalkStatus.Failed) return Fail();
                arrived = status == WalkStatus.Arrived;
                return false;
            }
            return --waitTicks <= 0;
        }
    }

    public sealed class BuildJob : Job
    {
        const int RetryDelayTicks = 600;

        readonly Blueprint blueprint;
        readonly Walker walker;
        bool working;

        public BuildJob(Pawn pawn, Blueprint blueprint) : base(pawn)
        {
            this.blueprint = blueprint;
            walker = new Walker(blueprint.Cell, adjacent: true);
            blueprint.ClaimedBy = pawn;
            if (Random.value < 0.15f) pawn.Say("work_start");
        }

        public override string Label => blueprint.Deconstruct ? $"Demolishing {blueprint.Def.Label}" : $"Building {blueprint.Def.Label}";
        public override bool IsWork => true;

        public override bool Tick(Game game)
        {
            if (!game.Construction.IsValid(blueprint)) return true; // cancelled

            if (!working)
            {
                var status = walker.Tick(Pawn, game);
                if (status == WalkStatus.Failed)
                {
                    blueprint.RetryAfterTick = game.Ticks.TicksGame + RetryDelayTicks;
                    return Fail();
                }
                working = status == WalkStatus.Arrived;
                return false;
            }

            if (!Walker.IsNextTo(Pawn.Cell, blueprint.Cell))
            {
                working = false; // got pushed away
                walker.Retarget(blueprint.Cell);
                return false;
            }

            blueprint.WorkLeft -= 1f + 0.15f * Pawn.ConstructionSkill;
            if (blueprint.WorkLeft > 0f) return false;

            game.Construction.Complete(blueprint, game);
            if (game.Rng.NextDouble() < 0.15) Pawn.Say("work_done");
            MaybeAccident(game);
            return true;
        }

        void MaybeAccident(Game game)
        {
            float chance = 0.02f * (1f - Pawn.ConstructionSkill / 12f);
            if (game.Rng.NextDouble() >= chance) return;
            string part = game.Rng.NextDouble() < 0.5 ? "Left hand" : "Right hand";
            Pawn.Health.AddInjury("Cut", part, 0.12f, 0.2f);
            Pawn.Mood.AddMemory("hurt", "Got hurt", -4f, game.Ticks.TicksGame, GameClock.TicksPerDay);
            Pawn.Say("hurt");
            game.Log.Add($"{Pawn.Nickname} cut their {part.ToLower()} while building.", MessageKind.Bad);
        }

        public override void OnEnd(Game game)
        {
            if (blueprint.ClaimedBy == Pawn) blueprint.ClaimedBy = null;
        }
    }

    /// <summary>Close in on a focus-fire target until there's a clean shot, then hold and let the guns talk.</summary>
    public sealed class EngageJob : Job
    {
        readonly Pawn target;
        Walker walker;
        int retarget;

        public EngageJob(Pawn pawn, Pawn target) : base(pawn) => this.target = target;

        public override string Label => $"Engaging {target.Nickname}";

        public override bool Tick(Game game)
        {
            if (!target.IsPresent || target.Health.Downed || Pawn.ForcedTarget != target)
            {
                if (Pawn.ForcedTarget == target) Pawn.ForcedTarget = null;
                return true;
            }

            float range = Pawn.Armed ? Combat.CombatSystem.PistolRange - 1f : Combat.CombatSystem.MeleeRange - 0.3f;
            float dist = (target.Position - Pawn.Position).magnitude;
            bool clear = !Pawn.Armed || Combat.CombatSystem.HasLineOfSight(game.Map, Pawn.Cell, target.Cell);
            if (dist <= range && clear)
            {
                Pawn.StopMoving();
                walker = null;
                return false;
            }

            if (walker == null || --retarget <= 0)
            {
                walker = new Walker(target.Cell, adjacent: true);
                retarget = 45;
            }
            if (walker.Tick(Pawn, game) == WalkStatus.Failed) walker = null;
            return false;
        }
    }
}
