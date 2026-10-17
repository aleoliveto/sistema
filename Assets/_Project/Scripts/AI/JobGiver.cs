using System.Collections.Generic;
using OSistema.Core;
using OSistema.Crime;
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
            if (pawn.Drafted) return DraftedJob(pawn, game);
            if (pawn.InMentalBreak(game.Ticks.TicksGame)) return new SulkJob(pawn);
            if (pawn.IsBoss && game.Org.Hiding) return new HideJob(pawn, game);
            return TryNonIdle(pawn, game) ?? Wander(pawn, game);
        }

        /// <summary>Drafted crew follow focus-fire orders and get behind cover when someone is shooting at them.</summary>
        static Job DraftedJob(Pawn pawn, Game game)
        {
            var target = pawn.ForcedTarget;
            if (target != null && (!target.IsPresent || target.Health.Downed)) pawn.ForcedTarget = target = null;
            if (target != null) return new EngageJob(pawn, target);

            var threat = NearestThreat(pawn, game);
            if (threat != null && !Combat.CombatSystem.IsInCover(game.Map, pawn.Cell, threat.Position))
            {
                var cover = Combat.CombatSystem.FindCover(game.Map, pawn.Cell, threat.Position, 4);
                if (cover.HasValue && cover.Value != pawn.Cell) return new GotoJob(pawn, cover.Value, "Taking cover");
            }
            return new WaitJob(pawn, 60, threat != null ? "Holding position" : "Drafted, awaiting orders", idle: false);
        }

        static Pawn NearestThreat(Pawn pawn, Game game)
        {
            long now = game.Ticks.TicksGame;
            Pawn best = null;
            float bestDist = 14f;
            foreach (var r in game.Pawns.Rivals)
            {
                if (!r.CanAct || !r.IsAggressive(now)) continue;
                float d = (r.Position - pawn.Position).magnitude;
                if (d < bestDist && Combat.CombatSystem.HasLineOfSight(game.Map, pawn.Cell, r.Cell))
                {
                    bestDist = d;
                    best = r;
                }
            }
            return best;
        }

        /// <summary>Anything better than standing around, or null. Also used to interrupt idle pawns.</summary>
        public static Job TryNonIdle(Pawn pawn, Game game)
        {
            return TryTend(pawn, game)
                ?? TryCourier(pawn, game)
                ?? TryEat(pawn, game)
                ?? TrySleep(pawn, game)
                ?? TryDuty(pawn, game)
                ?? TryCook(pawn, game)
                ?? TryShop(pawn, game)
                ?? TryBuild(pawn, game);
        }

        static Job TryTend(Pawn pawn, Game game)
        {
            foreach (var patient in game.Pawns.Crew)
            {
                if (patient == pawn || !patient.IsPresent || patient.TendedBy != null || !patient.Health.NeedsTending) continue;
                return new TendJob(pawn, patient, game);
            }

            // Tend yourself if bleeding badly or nobody else can.
            if (pawn.Health.NeedsTending && pawn.TendedBy == null &&
                (pawn.Health.BleedRatePerDay > 0.3f || !AnyoneElseCanHelp(pawn, game)))
                return new TendJob(pawn, pawn, game);
            return null;
        }

        static Job TryCourier(Pawn pawn, Game game)
        {
            var org = game.Org;
            if (!org.Hiding || pawn.IsBoss || org.Courier != null) return null;
            var boss = org.Boss(game);
            if (boss == null || !boss.IsPresent) return null;
            if (boss.Needs.Food > 0.5f || boss.MealsDelivered > 0 || game.Stock.Meals <= 0) return null;
            return new CourierJob(pawn, boss, game);
        }

        static bool AnyoneElseCanHelp(Pawn pawn, Game game)
        {
            long now = game.Ticks.TicksGame;
            foreach (var other in game.Pawns.Crew)
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
            // Night-shift crew only go to bed when actually tired, not just because it's late.
            bool bedtime = night && needs.Rest < 0.85f && !HasDutyNow(pawn, game);
            if (!needs.Tired && !bedtime) return null;

            if (needs.Rest < 0.03f) return new SleepJob(pawn, null, null); // collapses where they stand

            var bed = EnsureBed(pawn, game);
            if (bed.HasValue) return new SleepJob(pawn, bed, null);
            // No bed: go home and sleep on the floor of the basso, not in the street.
            return new SleepJob(pawn, null, FloorSpotAtHome(pawn, game));
        }

        static Vector2Int? FloorSpotAtHome(Pawn pawn, Game game)
        {
            var home = game.PlayerBase;
            for (int i = 0; i < 20; i++)
            {
                var c = new Vector2Int(game.Rng.Next(home.xMin, home.xMax), game.Rng.Next(home.yMin, home.yMax));
                if (game.Map.IsPassable(c) && game.Map.GetBuilding(c) == null) return c;
            }
            return null;
        }

        /// <summary>True while the pawn's role keeps them up: an open piazza, or a night job for the collector.</summary>
        public static bool HasDutyNow(Pawn pawn, Game game)
        {
            int hour = game.Clock.Hour;
            var org = game.Org;
            switch (pawn.Role)
            {
                case CrewRole.Pusher:
                case CrewRole.Lookout:
                    return org.Squares.Exists(s => s.IsOpenAt(hour));
                case CrewRole.Collector:
                    return (hour >= 22 || hour < Organization.ArsonEndHour) &&
                           org.Businesses.Exists(b => b.PendingAction == Retaliation.Burn);
                default:
                    return false;
            }
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

        /// <summary>What the pawn's clan role asks of them right now.</summary>
        static Job TryDuty(Pawn pawn, Game game)
        {
            if (pawn.Role == CrewRole.General || pawn.IsLyingLow(game.Ticks.TicksGame)) return null;
            var org = game.Org;

            if (pawn.Role == CrewRole.Collector) return CollectorDuty(pawn, game);

            int hour = game.Clock.Hour;

            // Go where you're needed most: the open square with the fewest people in this role.
            DrugSquare square = null;
            int fewest = int.MaxValue;
            foreach (var sq in org.Squares)
            {
                if (!sq.IsOpenAt(hour) || game.Police.IsRaiding(sq)) continue;
                int n = CountPosted(sq, pawn.Role, pawn, game);
                if (n < fewest)
                {
                    fewest = n;
                    square = sq;
                }
            }
            if (square == null) return null;

            var post = FreePost(square, pawn, game);
            return post.HasValue ? new PostJob(pawn, square, pawn.Role, post.Value) : null;
        }

        static Job CollectorDuty(Pawn pawn, Game game)
        {
            var org = game.Org;
            int hour = game.Clock.Hour;
            int week = Organization.WeekOf(game.Clock);

            bool arsonHours = hour >= Organization.ArsonStartHour && hour < Organization.ArsonEndHour;
            bool scareHours = hour >= 9 && hour < 22;
            bool shopHours = hour >= 9 && hour < 20;

            Business best = null;
            Retaliation action = Retaliation.None;
            int bestDist = int.MaxValue;
            foreach (var b in org.Businesses)
            {
                if (b.ClaimedBy != null) continue;
                Retaliation candidate;
                if (b.PendingAction == Retaliation.Burn && arsonHours) candidate = Retaliation.Burn;
                else if (b.PendingAction == Retaliation.Scare && scareHours) candidate = Retaliation.Scare;
                else if (shopHours && b.PendingAction == Retaliation.None && org.IsDue(b, week)) candidate = Retaliation.None;
                else continue;

                int d = GridUtil.Manhattan(pawn.Cell, b.Cell);
                // Retaliation orders come before routine collection.
                if (candidate != Retaliation.None) d -= 10000;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = b;
                    action = candidate;
                }
            }
            if (best == null) return null;
            return action == Retaliation.None ? new CollectPizzoJob(pawn, best) : new RetaliateJob(pawn, best, action);
        }

        static int CountPosted(DrugSquare square, CrewRole role, Pawn except, Game game)
        {
            int n = 0;
            foreach (var p in game.Pawns.Crew)
                if (p != except && p.CurrentJob is PostJob post && post.Square == square && post.Role == role) n++;
            return n;
        }

        /// <summary>
        /// A post nobody else has taken. Pushers use the spot itself, then the cells around it;
        /// lookouts spread over the approaches.
        /// </summary>
        static Vector2Int? FreePost(DrugSquare square, Pawn pawn, Game game)
        {
            var taken = new HashSet<Vector2Int>();
            foreach (var p in game.Pawns.Crew)
                if (p != pawn && p.CurrentJob is PostJob post && post.Square == square) taken.Add(post.Post);

            if (pawn.Role == CrewRole.Pusher)
            {
                foreach (var c in GridUtil.CellsAround(square.Cell, 1))
                    if (!taken.Contains(c) && game.Map.IsPassable(c) && game.Map.GetBuilding(c) == null) return c;
                return null;
            }

            foreach (var c in square.LookoutPosts)
                if (!taken.Contains(c)) return c;
            // More lookouts than approaches: double up near a post, but never on the same cell.
            foreach (var basePost in square.LookoutPosts)
            foreach (var c in GridUtil.CellsAround(basePost, 2))
                if (!taken.Contains(c) && game.Map.IsPassable(c) && game.Map.GetBuilding(c) == null) return c;
            return null;
        }

        static Job TryCook(Pawn pawn, Game game)
        {
            var stock = game.Stock;
            if (stock.Cook != null || stock.Groceries <= 0) return null;
            if (stock.Meals >= 2 + 2 * game.Pawns.PresentCount) return null;
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
            if (foodInMeals < 3 * game.Pawns.PresentCount && stock.Cash >= Prices.Groceries &&
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
