using OSistema.Core;
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

        public override bool IsWork => true;
        protected abstract BuildingDef Station { get; }
        protected abstract bool HasInputs(Game game);
        protected abstract int TicksPerItem(Game game);
        protected abstract void Produce(Game game);

        public override bool Tick(Game game)
        {
            if (game.Map.GetBuilding(station) != Station || !HasInputs(game)) return true;
            if (!arrived)
            {
                var status = walker.Tick(Pawn, game);
                if (status == WalkStatus.Failed) return Fail();
                if (status == WalkStatus.Walking) return false;
                arrived = true;
                Pawn.AimPoint = Mover.CellCenter(station);
            }
            if (++work < TicksPerItem(game)) return false;
            work = 0;
            Produce(game);
            return false;
        }

        public override void OnEnd(Game game) => Pawn.AimPoint = null;

        public static bool TryFind(Pawn pawn, BuildingDef def, Game game, out Vector2Int station)
        {
            station = default;
            int best = int.MaxValue;
            bool found = false;
            foreach (var c in game.Map.PositionsOf(def))
            {
                if (!game.PlayerBase.Contains(c) && !game.Properties.IsOwnedHome(c)) continue;
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

    /// <summary>Stays close to the boss. The combat system makes him shoot when there's trouble.</summary>
    public sealed class BodyguardJob : Job
    {
        Walker walker;
        Vector2Int lastTarget;
        int wait;

        public BodyguardJob(Pawn pawn) : base(pawn) { }
        public override string Label => "Watching the boss's back";
        public override bool IsWork => true;

        public override bool Tick(Game game)
        {
            var boss = game.Player;
            if (boss == null || !boss.IsPresent || boss.InJail) return true;
            float d = (boss.Position - Pawn.Position).magnitude;
            if (d < 2.2f && walker == null)
            {
                if (++wait > 120) return true;   // re-think now and then (food, sleep)
                return false;
            }
            wait = 0;
            if (walker == null || (boss.Cell - lastTarget).sqrMagnitude > 4)
            {
                lastTarget = boss.Cell;
                walker = new Walker(boss.Cell, adjacent: true, speedMultiplier: 1.2f) { Flexible = true };
            }
            var status = walker.Tick(Pawn, game);
            if (status != WalkStatus.Walking) walker = null;
            return status == WalkStatus.Failed;
        }
    }
}
