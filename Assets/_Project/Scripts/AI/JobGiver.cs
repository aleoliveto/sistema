using OSistema.Core;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.AI
{
    /// <summary>Decides what a pawn does next, in priority order.</summary>
    public static class JobGiver
    {
        public static Job Next(Pawn pawn, Game game)
        {
            if (pawn.Dead) return new WaitJob(pawn, 600, "Dead", idle: false);
            if (pawn.Health.Downed) return new DownedJob(pawn);
            if (pawn.Drafted) return new WaitJob(pawn, 60, "Drafted, awaiting orders", idle: false);
            if (pawn.InMentalBreak(game.Ticks.TicksGame)) return new SulkJob(pawn);
            return TryNonIdle(pawn, game) ?? Wander(pawn, game);
        }

        /// <summary>Anything better than standing around, or null. Also used to interrupt idle pawns.</summary>
        public static Job TryNonIdle(Pawn pawn, Game game)
        {
            return TryTend(pawn, game)
                ?? TryEat(pawn, game)
                ?? TrySleep(pawn, game)
                ?? TryCook(pawn, game)
                ?? TryShop(pawn, game)
                ?? TryBuild(pawn, game);
        }

        static Job TryTend(Pawn pawn, Game game)
        {
            foreach (var patient in game.Pawns.All)
            {
                if (patient == pawn || patient.Dead || patient.TendedBy != null || !patient.Health.NeedsTending) continue;
                return new TendJob(pawn, patient, game);
            }

            // Tend yourself if bleeding badly or nobody else can.
            if (pawn.Health.NeedsTending && pawn.TendedBy == null &&
                (pawn.Health.BleedRatePerDay > 0.3f || !AnyoneElseCanHelp(pawn, game)))
                return new TendJob(pawn, pawn, game);
            return null;
        }

        static bool AnyoneElseCanHelp(Pawn pawn, Game game)
        {
            long now = game.Ticks.TicksGame;
            foreach (var other in game.Pawns.All)
                if (other != pawn && other.CanAct && !other.Drafted && !other.InMentalBreak(now)) return true;
            return false;
        }

        static Job TryEat(Pawn pawn, Game game)
        {
            if (!pawn.Needs.Hungry) return null;
            var map = game.Map;
            if (game.Stock.Meals > 0 && map.TryFindNearest(BuildingDefs.Pantry, pawn.Cell, out var pantry))
                return new EatJob(pawn, pantry, atShop: false);
            if (game.Stock.Cash >= Prices.Pizza && map.TryFindNearest(BuildingDefs.PizzeriaCounter, pawn.Cell, out var pizzeria))
                return new EatJob(pawn, pizzeria, atShop: true);
            return null;
        }

        static Job TrySleep(Pawn pawn, Game game)
        {
            var needs = pawn.Needs;
            int hour = game.Clock.Hour;
            bool night = hour >= 22 || hour < 6;
            if (!needs.Tired && !(night && needs.Rest < 0.85f)) return null;
            return new SleepJob(pawn, EnsureBed(pawn, game));
        }

        static Vector2Int? EnsureBed(Pawn pawn, Game game)
        {
            if (pawn.Bed.HasValue) return pawn.Bed;
            Vector2Int? best = null;
            int bestDist = int.MaxValue;
            foreach (var bed in game.Map.PositionsOf(BuildingDefs.Bed))
            {
                if (game.Pawns.IsBedOwned(bed, pawn)) continue;
                int d = GridUtil.Manhattan(pawn.Cell, bed);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = bed;
                }
            }
            pawn.Bed = best;
            return best;
        }

        static Job TryCook(Pawn pawn, Game game)
        {
            var stock = game.Stock;
            if (stock.Cook != null || stock.Groceries <= 0) return null;
            if (stock.Meals >= 2 + 2 * game.Pawns.LivingCount) return null;
            var map = game.Map;
            if (map.PositionsOf(BuildingDefs.Pantry).Count == 0) return null;
            if (!map.TryFindNearest(BuildingDefs.Stove, pawn.Cell, out var stove)) return null;
            return new CookJob(pawn, stove, game);
        }

        static Job TryShop(Pawn pawn, Game game)
        {
            var stock = game.Stock;
            var map = game.Map;
            if (stock.Shopper != null) return null;
            if (!map.TryFindNearest(BuildingDefs.Pantry, pawn.Cell, out var pantry)) return null;

            if (stock.Medicine < 1 && stock.Cash >= Prices.Medicine * 2 + 20 &&
                map.TryFindNearest(BuildingDefs.FarmaciaCounter, pawn.Cell, out var farmacia))
                return new ShopJob(pawn, farmacia, ShopKind.Farmacia, 2, pantry, game);

            int foodInMeals = stock.Groceries * 2 + stock.Meals;
            if (foodInMeals < 3 * game.Pawns.LivingCount && stock.Cash >= Prices.Groceries &&
                map.TryFindNearest(BuildingDefs.SalumeriaCounter, pawn.Cell, out var salumeria))
                return new ShopJob(pawn, salumeria, ShopKind.Salumeria, 6, pantry, game);

            return null;
        }

        static Job TryBuild(Pawn pawn, Game game)
        {
            long now = game.Ticks.TicksGame;
            Blueprint best = null;
            int bestDist = int.MaxValue;
            foreach (var bp in game.Construction.All)
            {
                if (bp.ClaimedBy != null || bp.RetryAfterTick > now) continue;
                int d = GridUtil.Manhattan(pawn.Cell, bp.Cell);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = bp;
                }
            }
            return best != null ? new BuildJob(pawn, best) : null;
        }

        static Job Wander(Pawn pawn, Game game)
        {
            var home = game.PlayerBase;
            for (int i = 0; i < 10; i++)
            {
                var c = new Vector2Int(game.Rng.Next(home.xMin, home.xMax), game.Rng.Next(home.yMin, home.yMax));
                if (c != pawn.Cell && game.Map.IsPassable(c))
                    return new WanderJob(pawn, c, game.Rng.Next(240, 900));
            }
            return new WaitJob(pawn, 120, "Idle");
        }
    }
}
