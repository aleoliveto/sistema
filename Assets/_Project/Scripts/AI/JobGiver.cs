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
            // "Follow me": they drop what they're doing and come along, keeping their job for when you say stop.
            if (pawn.Following && !pawn.IsBoss)
            {
                var boss = game.Player;
                if (boss != null && boss.IsPresent && !boss.InJail && pawn.Needs.Food > 0.08f && pawn.Needs.Rest > 0.05f) return new BodyguardJob(pawn, follow: true);
                pawn.Following = false;
            }
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
                if (!r.CanAct || !r.IsAggressive(now) || !Combat.CombatSystem.AfterFamily(r)) continue;
                float d = (r.Position - pawn.Position).magnitude;
                if (d < bestDist && Combat.CombatSystem.HasLineOfSight(game.Map, pawn.Cell, r.Cell))
                {
                    bestDist = d;
                    best = r;
                }
            }
            return best;
        }

        /// <summary>Only what the body needs: wounds, food, sleep. For the player's character left alone.</summary>
        public static Job ForNeeds(Pawn pawn, Game game) => TryTend(pawn, game) ?? TryEat(pawn, game) ?? TrySleep(pawn, game);

        /// <summary>Anything better than standing around, or null. Also used to interrupt idle pawns.</summary>
        public static Job TryNonIdle(Pawn pawn, Game game)
        {
            return TryTend(pawn, game)
                ?? TryCourier(pawn, game)
                ?? TryEat(pawn, game)
                ?? TrySleep(pawn, game)
                ?? TryContract(pawn, game)
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
            var stock = game.Stock;
            // On shift, out at their post: a pizza round the corner, not the walk back across the rione.
            if (!pawn.Controlled && pawn.Role != CrewRole.General && OnShift(pawn, game) &&
                !Housing.InHouse(pawn.Cell, Housing.HomeOf(pawn, game), game) && stock.Cash >= Prices.Pizza &&
                TryFindPizzeria(game, pawn.Cell, out var nearPost) && (nearPost - pawn.Cell).sqrMagnitude <= PizzeriaNearPost * PizzeriaNearPost)
                return new EatJob(pawn, nearPost, atShop: true);
            // Home food comes from the pantry of the house they live in (any of the family's, if theirs has none),
            // never a shop's or a neighbour's.
            if (stock.Meals > 0 && FindHome(pawn, game, BuildingDefs.Pantry, out var pantry))
                return new EatJob(pawn, pantry, atShop: false);
            // Nothing cooked but groceries in: whoever is hungry puts the pot on.
            if (stock.Meals <= 0 && stock.Groceries > 0 && stock.Cook == null &&
                FindHome(pawn, game, BuildingDefs.Pantry, out _) &&
                FindHome(pawn, game, BuildingDefs.Stove, out var stove))
                return new CookJob(pawn, stove, game);
            if (stock.Cash >= Prices.Pizza && TryFindPizzeria(game, pawn.Cell, out var pizzeria))
                return new EatJob(pawn, pizzeria, atShop: true);
            return null;
        }

        /// <summary>How far from their post the crew will walk for a pizza on shift, in cells.</summary>
        const int PizzeriaNearPost = 30;

        /// <summary>This furniture in the house the pawn lives in, or failing that the nearest in any of the family's homes.</summary>
        public static bool FindHome(Pawn pawn, Game game, BuildingDef def, out Vector2Int found) =>
            Housing.TryFindInHouse(pawn, def, game, out found) || TryFindAtHome(game, def, pawn.Cell, out found);

        /// <summary>The nearest piece of this furniture in the family's own homes: not a shop's, not a neighbour's.</summary>
        public static bool TryFindAtHome(Game game, BuildingDef def, Vector2Int from, out Vector2Int found)
        {
            found = default;
            int best = int.MaxValue;
            foreach (var c in game.Map.PositionsOf(def))
            {
                if (!game.PlayerBase.Contains(c) && !game.Properties.IsOwnedHome(c)) continue;
                int d = (c - from).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    found = c;
                }
            }
            return best != int.MaxValue;
        }

        /// <summary>The nearest pizzeria counter whose shop is open (not burned, not shut for the night).</summary>
        public static bool TryFindPizzeria(Game game, Vector2Int from, out Vector2Int found)
        {
            found = default;
            int hour = game.Clock.Hour;
            if (hour < 8 || hour >= 23) return false;
            int best = int.MaxValue;
            foreach (var c in game.Map.PositionsOf(BuildingDefs.PizzeriaCounter))
            {
                var shop = game.Org.BusinessAt(c);
                if (shop != null && shop.Status == BusinessStatus.Closed) continue;
                int d = (c - from).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    found = c;
                }
            }
            return best != int.MaxValue;
        }

        static Job TrySleep(Pawn pawn, Game game)
        {
            var needs = pawn.Needs;
            int hour = game.Clock.Hour;
            bool night = hour >= 22 || hour < 6;
            // Crew sleep when their schedule says so (or when they're dead on their feet).
            bool scheduled = !pawn.Controlled && pawn.Schedule[hour] == Pawn.Shift.Sleep && needs.Rest < 0.97f;
            bool bedtime = pawn.Controlled && night && needs.Rest < 0.85f && !HasDutyNow(pawn, game);
            if (!needs.Tired && !bedtime && !scheduled) return null;

            if (needs.Rest < 0.03f) return new SleepJob(pawn, null, null); // collapses where they stand

            // Any bed of their house nobody is in: the day team sleeps in the night team's beds, and back.
            var bed = Housing.FreeBed(pawn, game);
            if (bed.HasValue) return new SleepJob(pawn, bed, null);

            // Every bed taken (or none at all): first they sulk about it, then they lie down on the floor at home.
            long now = game.Ticks.TicksGame;
            if (!pawn.Controlled && !pawn.Mood.Memories.Exists(m => m.Id == "no_bed"))
            {
                var house = Housing.HomeOf(pawn, game);
                bool none = Housing.BedsIn(house, game).Count == 0;
                pawn.Mood.AddMemory("no_bed", none ? $"No bed at {Housing.Name(house)}" : $"Every bed at {Housing.Name(house)} taken", -6f, now, GameClock.TicksPerDay / 2);
                pawn.Say("sulk");
                game.Log.Add(none
                    ? $"{pawn.Nickname} came home to {Housing.Name(house)} to sleep and there isn't a bed. They're sulking."
                    : $"{pawn.Nickname} came home to {Housing.Name(house)} to sleep and every bed is taken. They're sulking.", MessageKind.Bad);
                return new SulkJob(pawn, now + GameClock.TicksPerHour, none ? "no bed at home" : "no bed free");
            }
            return new SleepJob(pawn, null, Housing.RandomCellAtHome(pawn, game));
        }

        /// <summary>True while the pawn's role keeps them up: an open piazza, or a night job for the collector.</summary>
        public static bool HasDutyNow(Pawn pawn, Game game)
        {
            int hour = game.Clock.Hour;
            if (!OnShift(pawn, game)) return false;
            var org = game.Org;
            switch (pawn.Role)
            {
                case CrewRole.Pusher:
                case CrewRole.Lookout:
                case CrewRole.Capo:
                {
                    var own = org.SquareOf(pawn, game);
                    return own != null ? own.IsOpenAt(hour) : org.Squares.Exists(s => s.IsOpenAt(hour));
                }
                case CrewRole.Collector:
                    return (hour >= 22 || hour < Organization.ArsonEndHour) &&
                           org.Businesses.Exists(b => b.PendingAction == Retaliation.Burn);
                default:
                    return false;
            }
        }

        static Job TryContract(Pawn pawn, Game game)
        {
            var c = game.Contracts.ActiveFor(pawn);
            if (c == null) return null;
            if (c.AtNight && c.Stage <= 1)
            {
                int h = game.Clock.Hour;
                if (!(h >= 21 || h < 4)) return null; // leave at nine for the ten o'clock boat
            }
            return new ContractJob(pawn, c);
        }

        /// <summary>What the pawn's clan role asks of them right now.</summary>
        public static bool OnShift(Pawn pawn, Game game) => pawn.Controlled || pawn.Schedule[game.Clock.Hour] == Pawn.Shift.Work;

        static Job TryDuty(Pawn pawn, Game game)
        {
            if (pawn.Role == CrewRole.General || pawn.IsLyingLow(game.Ticks.TicksGame)) return null;
            if (!OnShift(pawn, game)) return null;   // off the clock
            var org = game.Org;

            if (pawn.Role == CrewRole.Collector) return CollectorDuty(pawn, game);
            if (pawn.Role == CrewRole.Parking || pawn.Role == CrewRole.Vendor)
            {
                var kind = pawn.Role == CrewRole.Parking ? StreetWork.Parking : StreetWork.Stall;
                if (kind == StreetWork.Stall && game.Stock.Cigarettes <= 0 && org.H.fakes <= 0) return null;   // nothing to sell
                var spot = org.StreetSpot(kind, pawn, game);
                return spot.HasValue ? new StreetWorkJob(pawn, kind, spot.Value) : null;
            }
            if (pawn.Role == CrewRole.Bodyguard)
                return pawn.GuardPost.HasValue || (game.Player != null && game.Player.IsPresent && !game.Player.InJail) ? new BodyguardJob(pawn) : null;
            if (pawn.Role == CrewRole.Bagger)
                return game.Org.CanMakeDose(game) && StationJob.TryFind(pawn, BuildingDefs.BaggingTable, game, out var table) ? new BagJob(pawn, table) : null;
            if (pawn.Role == CrewRole.Roller)
                return game.Org.CanMakePack(game) && StationJob.TryFind(pawn, BuildingDefs.RollingTable, game, out var roller) ? new RollJob(pawn, roller) : null;
            if (pawn.Role == CrewRole.Manager)
            {
                var place = org.ManagedBy(pawn, game);
                int h = game.Clock.Hour;
                if (place == null || (h >= 2 && h < 10)) return null; // opens at ten, closes at two
                var r = place.Interior;
                var spot = new Vector2Int(game.Rng.Next(r.xMin, r.xMax), game.Rng.Next(r.yMin, r.yMax));
                if (!game.Map.IsPassable(spot)) return null;
                return new GotoJob(pawn, spot, $"Running the {Organization.Name(place.Operation).ToLower()}");
            }

            int hour = game.Clock.Hour;

            // Your own piazza, if you have one; otherwise where you're needed most.
            var mine = org.SquareOf(pawn, game);
            if (mine != null)
            {
                if (!mine.IsOpenAt(hour) || game.Police.IsRaiding(mine)) return null;
                var own = FreePost(mine, pawn, game);
                return own.HasValue ? new PostJob(pawn, mine, pawn.Role, own.Value) : null;
            }
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

            if (pawn.Role == CrewRole.Capo)
            {
                // The capo keeps an eye on the spot from a step or two away.
                foreach (var c in GridUtil.CellsAround(square.Cell, 2))
                    if (!taken.Contains(c) && GridUtil.Manhattan(c, square.Cell) >= 2 && game.Map.IsPassable(c) && game.Map.GetBuilding(c) == null) return c;
                return null;
            }
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
            if (!TryFindAtHome(game, BuildingDefs.Pantry, pawn.Cell, out _)) return null;
            if (!TryFindAtHome(game, BuildingDefs.Stove, pawn.Cell, out var stove)) return null;
            return new CookJob(pawn, stove, game);
        }

        static Job TryShop(Pawn pawn, Game game)
        {
            var stock = game.Stock;
            var map = game.Map;
            if (stock.Shopper != null) return null;
            if (!TryFindAtHome(game, BuildingDefs.Pantry, pawn.Cell, out var pantry)) return null;

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
            // Afternoons and evenings: the TV is on, if the family has one.
            int hour = game.Clock.Hour;
            if (hour >= 13 && game.Rng.NextDouble() < 0.3)
                if (Housing.TryFindInHouse(pawn, BuildingDefs.Television, game, out var tv)) return new WatchTvJob(pawn, tv);

            // Hang around the house they live in.
            var spot = Housing.RandomCellAtHome(pawn, game);
            if (spot.HasValue && spot.Value != pawn.Cell) return new WanderJob(pawn, spot.Value, game.Rng.Next(240, 900));
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
