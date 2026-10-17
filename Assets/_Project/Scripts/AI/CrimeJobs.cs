using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using UnityEngine;

namespace OSistema.AI
{
    /// <summary>Stand at the piazza as a pusher or a lookout (vedetta) during opening hours.</summary>
    public sealed class PostJob : Job
    {
        const int RethinkTicks = 2500; // step away now and then so needs get a look in

        public readonly DrugSquare Square;
        public readonly CrewRole Role;
        public readonly Vector2Int Post;
        readonly Walker walker;
        int ticksAtPost;

        public bool AtPost { get; private set; }

        public PostJob(Pawn pawn, DrugSquare square, CrewRole role, Vector2Int post) : base(pawn)
        {
            Square = square;
            Role = role;
            Post = post;
            walker = new Walker(post, adjacent: false);
        }

        public override string Label => Role == CrewRole.Pusher
            ? (AtPost ? "Pushing at the piazza" : "Going to the piazza")
            : (AtPost ? "Lookout (vedetta) at the piazza" : "Going to the lookout post");

        public override bool IsWork => true;

        public override bool Tick(Game game)
        {
            if (!game.Org.Squares.Contains(Square) || !Square.IsOpenAt(game.Clock.Hour)) return true;

            if (!AtPost)
            {
                var status = walker.Tick(Pawn, game);
                if (status == WalkStatus.Failed) return Fail();
                AtPost = status == WalkStatus.Arrived;
                return false;
            }
            if (++ticksAtPost % 600 == 0) Pawn.Learn(Skill.Awareness, Role == CrewRole.Lookout ? 3f : 1f, game);
            return ticksAtPost >= RethinkTicks;
        }
    }

    /// <summary>Run home when the police show up, then lie low for a few hours.</summary>
    public sealed class FleeJob : Job
    {
        const int LieLowTicks = 4000;

        public readonly DrugSquare Square;
        readonly Walker walker;

        public FleeJob(Pawn pawn, DrugSquare square, Game game) : base(pawn)
        {
            Square = square;
            var home = game.PlayerBase;
            var target = Vector2Int.RoundToInt(home.center);
            // Run to the nearest place the family owns.
            var safe = game.Properties.RandomHomeCell(pawn.Cell, game.Rng);
            if (safe.HasValue) home = new RectInt(safe.Value, Vector2Int.one);
            for (int i = 0; i < 10; i++)
            {
                var c = new Vector2Int(game.Rng.Next(home.xMin, home.xMax), game.Rng.Next(home.yMin, home.yMax));
                if (game.Map.IsPassable(c))
                {
                    target = c;
                    break;
                }
            }
            walker = new Walker(target, adjacent: false, speedMultiplier: 1.35f);
        }

        public override string Label => "Running from the police!";

        public override bool Tick(Game game)
        {
            var status = walker.Tick(Pawn, game);
            if (status == WalkStatus.Walking) return false;
            Pawn.LyingLowUntil = game.Ticks.TicksGame + LieLowTicks;
            return true;
        }
    }

    /// <summary>Visit a business to impose or collect pizzo.</summary>
    public sealed class CollectPizzoJob : Job
    {
        const float Work = 300f;

        readonly Business business;
        readonly Walker walker;
        readonly bool imposing;
        float workLeft = Work;
        bool talking;

        public CollectPizzoJob(Pawn pawn, Business business) : base(pawn)
        {
            this.business = business;
            imposing = business.Status == BusinessStatus.Targeted;
            walker = new Walker(business.Cell, adjacent: true);
            business.ClaimedBy = pawn;
        }

        public override string Label => imposing ? $"Imposing pizzo on {business.Name}" : $"Collecting pizzo at {business.Name}";
        public override bool IsWork => true;

        public override bool Tick(Game game)
        {
            if (!talking)
            {
                var status = walker.Tick(Pawn, game);
                if (status == WalkStatus.Failed) return Fail();
                talking = status == WalkStatus.Arrived;
                return false;
            }
            if (--workLeft > 0f) return false;
            game.Org.ResolvePizzoVisit(business, Pawn, game);
            Pawn.Learn(Skill.Intimidation, imposing ? 14f : 6f, game);
            return true;
        }

        public override void OnEnd(Game game)
        {
            if (business.ClaimedBy == Pawn) business.ClaimedBy = null;
        }
    }

    /// <summary>Punish a business that refused pizzo: smash the window, or burn the shop at night.</summary>
    public sealed class RetaliateJob : Job
    {
        readonly Business business;
        readonly Retaliation action;
        readonly Walker walker;
        float workLeft;
        bool working;

        public RetaliateJob(Pawn pawn, Business business, Retaliation action) : base(pawn)
        {
            this.business = business;
            this.action = action;
            walker = new Walker(business.Cell, adjacent: true);
            workLeft = action == Retaliation.Burn ? 400f : 200f;
            business.ClaimedBy = pawn;
        }

        public override string Label => action == Retaliation.Burn
            ? $"Setting fire to {business.Name}"
            : $"Paying {business.Name} a visit";

        public override bool IsWork => true;

        public override bool Tick(Game game)
        {
            if (business.PendingAction != action || business.Status != BusinessStatus.Refusing) return true; // order cancelled
            if (!working)
            {
                var status = walker.Tick(Pawn, game);
                if (status == WalkStatus.Failed) return Fail();
                working = status == WalkStatus.Arrived;
                return false;
            }
            if (--workLeft > 0f) return false;
            game.Org.ResolveRetaliation(business, Pawn, action, game);
            Pawn.Learn(Skill.Intimidation, 15f, game);
            return true;
        }

        public override void OnEnd(Game game)
        {
            if (business.ClaimedBy == Pawn) business.ClaimedBy = null;
        }
    }
}
