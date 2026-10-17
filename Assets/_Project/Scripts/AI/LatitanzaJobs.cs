using OSistema.Core;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.AI
{
    /// <summary>The boss in latitanza: holed up in the bunker, sleeping and eating what the courier brings.</summary>
    public sealed class HideJob : Job
    {
        readonly Walker walker;
        readonly Vector2Int spot;
        bool hidden;

        public HideJob(Pawn pawn, Game game) : base(pawn)
        {
            if (!game.Map.TryFindNearest(BuildingDefs.Bunker, pawn.Cell, out spot))
            {
                spot = Vector2Int.RoundToInt(game.PlayerBase.center);
                var home = game.PlayerBase;
                for (int i = 0; i < 20; i++)
                {
                    var c = new Vector2Int(game.Rng.Next(home.xMin, home.xMax), game.Rng.Next(home.yMin, home.yMax));
                    if (game.Map.IsPassable(c))
                    {
                        spot = c;
                        break;
                    }
                }
            }
            walker = new Walker(spot, adjacent: false);
        }

        public override string Label => !hidden ? "Going into hiding" : Pawn.Sleep != SleepState.Awake ? "In hiding, asleep" : "In hiding";

        public override bool Tick(Game game)
        {
            if (!game.Org.Hiding) return true;

            if (!hidden)
            {
                hidden = walker.Tick(Pawn, game) != WalkStatus.Walking;
                return false;
            }

            var needs = Pawn.Needs;
            if (Pawn.Sleep == SleepState.Awake && needs.Rest < 0.3f) Pawn.Sleep = SleepState.InBed;
            else if (Pawn.Sleep != SleepState.Awake && needs.Rest >= 0.97f) Pawn.Sleep = SleepState.Awake;

            if (needs.Food < 0.4f && Pawn.MealsDelivered > 0)
            {
                Pawn.MealsDelivered--;
                needs.Eat(0.8f);
            }
            return false;
        }

        public override void OnEnd(Game game) => Pawn.Sleep = SleepState.Awake;
    }

    /// <summary>Carry a meal to the boss's hideout. Every visit is a risk: people notice comings and goings.</summary>
    public sealed class CourierJob : Job
    {
        readonly Pawn boss;
        readonly Walker walker;

        public CourierJob(Pawn pawn, Pawn boss, Game game) : base(pawn)
        {
            this.boss = boss;
            walker = new Walker(boss.Cell, adjacent: true);
            game.Org.Courier = pawn;
        }

        public override string Label => "Bringing food to the hideout";

        public override bool Tick(Game game)
        {
            if (!game.Org.Hiding || !boss.IsPresent) return true;
            if ((walker.Target - boss.Cell).sqrMagnitude > 2) walker.Retarget(boss.Cell);

            var status = walker.Tick(Pawn, game);
            if (status == WalkStatus.Failed) return Fail();
            if (status == WalkStatus.Walking) return false;

            if (game.Stock.Meals <= 0) return Fail();
            game.Stock.Meals--;
            boss.MealsDelivered++;
            game.Org.LeakRisk += 3f;
            return true;
        }

        public override void OnEnd(Game game)
        {
            if (game.Org.Courier == Pawn) game.Org.Courier = null;
        }
    }
}
