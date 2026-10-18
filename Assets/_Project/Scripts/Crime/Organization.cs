using System.Collections.Generic;
using OSistema.AI;
using OSistema.Core;
using OSistema.Npcs;
using OSistema.Pawns;
using OSistema.Story;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// The player's clan as a business: the treasury (the crew cash in Stock acts as 'a bacinella),
    /// wages, the supplier, drug squares, pizzo and respect.
    /// </summary>
    public sealed partial class Organization
    {
        public const int MaxSquares = 3;
        public const int MaxCrew = 12;
        public const int ArsonStartHour = 2;  // arson happens in the dead of night
        public const int ArsonEndHour = 5;
        const float RespectPerLostCustomer = 0.25f;
        const int LostWarningCooldown = GameClock.TicksPerHour * 3;
        const float HeatPerSale = 0.35f;
        const int MaxCustomers = 16; // shared by your piazze and the rivals'

        public float Respect = 10f;

        /// <summary>Early-game steps the player has done once: the hints for them stop.</summary>
        public readonly System.Collections.Generic.List<string> Learned = new System.Collections.Generic.List<string>();
        public readonly List<DrugSquare> Squares = new List<DrugSquare>();
        public readonly List<Business> Businesses = new List<Business>();

        public int PendingOrderUnits;
        public int PendingOrderDay;     // delivered at 08:00 on this day
        public int LastPaydayWeek = -1;

        /// <summary>Bumped when squares change so the map overlay redraws.</summary>
        public int SquaresVersion { get; private set; }

        int lastAbsoluteHour = -1;

        public static int WeekOf(GameClock clock) => (clock.Day - 1) / 7;

        // ---------- Time ----------

        public void Tick(Game game)
        {
            int absoluteHour = game.Clock.Day * 24 + game.Clock.Hour;
            if (lastAbsoluteHour < 0) lastAbsoluteHour = absoluteHour;
            else if (absoluteHour != lastAbsoluteHour)
            {
                lastAbsoluteHour = absoluteHour;
                OnHour(game);
            }
            SpawnCustomers(game);
            TickCarrying(game);
        }

        void OnHour(Game game)
        {
            var clock = game.Clock;
            game.Territory.HourlyDecay();
            if (clock.Hour == 0) OnNewDay(game);
            if (clock.Hour == 8 && PendingOrderUnits > 0 && clock.Day >= PendingOrderDay) DeliverOrder(game);
            if (clock.Hour == 12 && clock.IsPayday && LastPaydayWeek != WeekOf(clock)) Payday(game);
            // Mammà: whatever her son gets up to, nobody in her basso goes hungry.
            if (clock.Hour == 13 && Generation == 1 && game.Stock.Meals < 2)
            {
                game.Stock.Meals += 3;
                game.Log.Add("Mammà left a pot of pasta e patate on the stove. \"Mangia, ca si' sicco.\"", MessageKind.Good);
            }
            ReopenBurnedShops(game);
            game.Police.HourlyCheck(game);
            LeadershipHourly(game);
            CheckRank(game);
            UpdateHomeComfort(game);
            game.Hospital.Discharge(game);
        }

        void OnNewDay(Game game)
        {
            foreach (var sq in Squares)
            {
                sq.SalesToday = 0;
                sq.LostToday = 0;
                sq.RevenueToday = 0;
            }

            LeadershipDaily(game);
            DailyMoney(game);
            DailyOperations(game);

            // Loyalty drifts with mood: a miserable crew stops believing in you.
            foreach (var p in game.Pawns.Crew)
                if (p.IsPresent) p.Loyalty = Mathf.Clamp(p.Loyalty + (p.Mood.Value - 50f) / 25f, 0f, 100f);
        }

        // ---------- Supplier ----------

        public bool OrderProduct(int units, Game game, out string error)
        {
            error = null;
            int cost = units * Prices.ProductWholesale;
            if (!SupplierIntroduced) error = "Nobody sells product to a stranger. Someone has to vouch for you.";
            else if (game.Contracts.ShipmentPending(ContractKind.Shipment)) error = "A shipment is already on its way.";
            if (error != null) return false;
            if (!Pay(cost, game, out error)) return false;
            game.Contracts.AddShipment(game, ContractKind.Shipment, units);
            return true;
        }

        public bool OrderCigarettes(int cases, Game game, out string error)
        {
            error = null;
            if (game.Contracts.ShipmentPending(ContractKind.CigaretteRun)) error = "'O Marenaro already has a load for you tonight.";
            else if (!RequireRank(2, "Contraband cigarettes", game, out error)) return false;
            if (error != null) return false;
            if (!Pay(cases * Prices.CigaretteCase, game, out error)) return false;
            game.Contracts.AddShipment(game, ContractKind.CigaretteRun, cases);
            return true;
        }

        void DeliverOrder(Game game)
        {
            game.Stock.Product += PendingOrderUnits;
            game.Log.Add($"The supplier's courier dropped off {PendingOrderUnits} units at the basso.", MessageKind.Good);
            PendingOrderUnits = 0;
        }

        // ---------- Wages (the mesata) ----------

        public int WeeklyWageBill(Game game)
        {
            int total = 0;
            foreach (var p in game.Pawns.Crew)
            {
                if (p.Dead) continue;
                total += p.InJail ? Prices.JailedFamilySupport : p.Wage;
            }
            return total;
        }

        void Payday(Game game)
        {
            PayFronts(game);
            WeeklyMoney(game);
            LastPaydayWeek = WeekOf(game.Clock);
            long now = game.Ticks.TicksGame;
            var stock = game.Stock;
            int paidTotal = 0, unpaid = 0;

            foreach (var p in game.Pawns.Crew)
            {
                if (p.Dead) continue;

                if (p.InJail)
                {
                    // Supporting the families of men inside is what keeps them from talking.
                    if (stock.Cash >= Prices.JailedFamilySupport)
                    {
                        stock.Cash -= Prices.JailedFamilySupport;
                        paidTotal += Prices.JailedFamilySupport;
                        p.Loyalty = Mathf.Min(100f, p.Loyalty + 3f);
                    }
                    else
                    {
                        p.Loyalty = Mathf.Max(0f, p.Loyalty - 15f);
                        unpaid++;
                        game.Log.Add($"No money for {p.Nickname}'s family while they're inside. They won't forget.", MessageKind.Bad);
                    }
                    continue;
                }

                if (stock.Cash >= p.Wage)
                {
                    stock.Cash -= p.Wage;
                    paidTotal += p.Wage;
                    p.Loyalty = Mathf.Min(100f, p.Loyalty + 3f);
                    p.Mood.AddMemory("paid", "Got my mesata", 5f, now, GameClock.TicksPerDay * 3);
                }
                else
                {
                    unpaid++;
                    p.Loyalty = Mathf.Max(0f, p.Loyalty - 12f);
                    p.Mood.AddMemory("unpaid", "Didn't get my mesata", -15f, now, GameClock.TicksPerDay * 4);
                    p.Say("unpaid");
                }
            }

            if (unpaid == 0) game.Log.Add($"Payday: €{paidTotal} paid out. The crew is happy.", MessageKind.Good);
            else game.Log.Add($"Payday: €{paidTotal} paid, {unpaid} left without their money.", MessageKind.Bad);
        }

        // ---------- Recruiting ----------

        /// <summary>Takes on a new guaglione. Free when a neighbour sends their son: no signing bonus.</summary>
        public Pawn Recruit(Game game, out string error, bool free = false)
        {
            error = null;
            int crew = 0;
            foreach (var p in game.Pawns.Crew) if (!p.Dead) crew++;
            if (crew >= MaxCrewForRank) error = $"A {RankName} can't keep more than {MaxCrewForRank} people. Climb higher for a bigger crew.";
            else if (!free && game.Stock.Cash < Prices.RecruitBonus) error = $"Recruiting costs €{Prices.RecruitBonus}.";
            if (error != null) return null;

            var rng = game.Rng;
            Vector2Int? spawn = null;
            foreach (var c in GridUtil.CellsAround(Vector2Int.RoundToInt(game.PlayerBase.center), 8))
            {
                if (game.PlayerBase.Contains(c) && game.Map.IsPassable(c))
                {
                    spawn = c;
                    break;
                }
            }
            if (spawn == null)
            {
                error = "No room at the basso.";
                return null;
            }

            var pawn = new Pawn(game.Pawns.NextId(), Names.Pick(Names.FirstNames, rng), Names.Pick(Names.Surnames, rng),
                Names.Pick(Names.Nicknames, rng), RandomShirt(rng), spawn.Value)
            {
                ConstructionSkill = rng.Next(0, 7),
                CookingSkill = rng.Next(0, 7),
                MedicineSkill = rng.Next(0, 5),
                IntimidationSkill = rng.Next(1, 9),
                AwarenessSkill = rng.Next(1, 9),
                ShootingSkill = rng.Next(1, 9),
                Wage = 120,
                Loyalty = 45f + rng.Next(0, 20),
            };
            if (!free) game.Stock.Cash -= Prices.RecruitBonus; // a signing bonus, in cash
            game.Pawns.Add(pawn);
            pawn.Say("recruited");
            game.Log.Add($"{pawn.FullName} “{pawn.Nickname}” joins the crew.", MessageKind.Good);
            return pawn;
        }

        static Color32 RandomShirt(System.Random rng)
        {
            Color c = Color.HSVToRGB((float)rng.NextDouble(), 0.35f + (float)rng.NextDouble() * 0.4f, 0.45f + (float)rng.NextDouble() * 0.45f);
            return c;
        }

        // ---------- Drug squares ----------

        public DrugSquare SquareAt(Vector2Int cell) => Squares.Find(s => s.Cell == cell);

        public bool TryOpenSquare(Vector2Int cell, Game game, out string error)
        {
            if (!RequireRank(1, "Running your own piazza", game, out error)) return false;
            if (Squares.Count >= MaxSquaresForRank)
            {
                error = $"A {RankName} can run {MaxSquaresForRank} piazza(e). Climb higher for more.";
                return false;
            }
            if (game.Rival.Squares.Exists(s => (s.Cell - cell).sqrMagnitude < 64))
            {
                error = "That's the rivals' piazza. Take it by force first.";
                return false;
            }
            error = null;
            var map = game.Map;
            var zone = game.Territory.ZoneAt(cell);
            if (!map.InBounds(cell) || zone == null) error = "Pick a sidewalk or courtyard inside a block.";
            else if (map.GetBuilding(cell) != null || map.GetTerrain(cell.x, cell.y) == TerrainType.Floor) error = "A piazza has to be outdoors.";
            else if (Squares.Count >= MaxSquares) error = $"You can run at most {MaxSquares} piazze for now.";
            else if (Squares.Exists(s => (s.Cell - cell).sqrMagnitude < 64)) error = "Too close to another piazza.";
            if (error != null) return false;

            if (!CanExpandInto(zone, game))
            {
                error = $"{zone.Name} is too far from your turf. Expand rione by rione.";
                return false;
            }
            WarnIfRivalZone(zone, game);
            var square = new DrugSquare { Cell = cell, ZoneId = zone.Id };
            ComputeLookoutPosts(square, map);
            Squares.Add(square);
            SquaresVersion++;
            game.Log.Add($"Opened a piazza in {zone.Name}. Assign pushers and lookouts, and order product.");
            game.Newspaper.Report(game, 2, $"New drug market in {zone.Name}, residents say", "\"They sell on the corner in broad daylight,\" says a pensioner. \"The police drive past and look the other way.\"");
            return true;
        }

        /// <summary>A rival piazza taken by force: same corner, now ours. False if the rank can't run another.</summary>
        public bool TakeSquare(DrugSquare rival, Game game)
        {
            if (Squares.Count >= MaxSquaresForRank || Squares.Count >= MaxSquares) return false;
            var square = new DrugSquare { Cell = rival.Cell, ZoneId = rival.ZoneId };
            ComputeLookoutPosts(square, game.Map);
            Squares.Add(square);
            SquaresVersion++;
            return true;
        }

        public void RestoreSquare(DrugSquare square, Map map)
        {
            ComputeLookoutPosts(square, map);
            Squares.Add(square);
            SquaresVersion++;
        }

        public bool CloseSquareAt(Vector2Int cell)
        {
            int removed = Squares.RemoveAll(s => s.Cell == cell);
            if (removed > 0) SquaresVersion++;
            return removed > 0;
        }

        static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        // Vedette stand a few cells up each approach, where they can see the police coming.
        static void ComputeLookoutPosts(DrugSquare square, Map map)
        {
            square.LookoutPosts.Clear();
            foreach (var dir in Directions)
            {
                var target = square.Cell + dir * 7;
                foreach (var c in GridUtil.CellsAround(target, 3))
                {
                    if (!map.InBounds(c) || !map.IsPassable(c) || map.GetBuilding(c) != null) continue;
                    if (map.GetTerrain(c.x, c.y) == TerrainType.Floor) continue;
                    square.LookoutPosts.Add(c);
                    break;
                }
            }
        }

        /// <summary>The pusher currently standing on this square, or null.</summary>
        public Pawn PusherAt(DrugSquare square, Game game)
        {
            foreach (var p in game.Pawns.Crew)
                if (p.CanAct && p.CurrentJob is PostJob post && post.Square == square && post.Role == CrewRole.Pusher && post.AtPost)
                    return p;
            return null;
        }

        /// <summary>Whether a square (ours or the rivals') is still operating.</summary>
        public bool IsActiveSquare(DrugSquare square, Game game) =>
            square != null && (square == DealingCorner || Squares.Contains(square) || game.Rival.Squares.Contains(square));

        public void Sell(DrugSquare square, Pawn pusher, Game game)
        {
            if (square.Rival)
            {
                game.Rival.Cash += Prices.ProductStreet;
                return;
            }
            game.Stock.Product--;
            game.Stock.Cash += Prices.ProductStreet;
            CheckDoseComplaints(game);
            square.SalesToday++;
            square.RevenueToday += Prices.ProductStreet;
            game.Territory.AddHeat(square.Cell, HeatPerSale);
            game.PopupAt(pusher.Position, $"+€{Prices.ProductStreet}");
            if (game.Rng.NextDouble() < 0.1) pusher.Say("sale");
        }

        /// <summary>A customer walked away with nothing: word gets around that this piazza can't deliver.</summary>
        public void LoseCustomer(DrugSquare square, Game game)
        {
            square.LostToday++;
            Respect = Mathf.Max(0f, Respect - RespectPerLostCustomer);

            long now = game.Ticks.TicksGame;
            if (now - square.LastLostWarningTick < LostWarningCooldown) return;
            square.LastLostWarningTick = now;
            var zone = game.Territory.Zones[square.ZoneId];
            string why = game.Stock.Product <= 0 ? "there's no product" : "nobody is selling";
            game.Log.Add($"Customers are leaving the piazza in {zone.Name} empty-handed: {why}. Respect is dropping.", MessageKind.Bad);
        }

        void SpawnCustomers(Game game)
        {
            SpawnCornerCustomers(game);
            if (Squares.Count == 0 && game.Rival.Squares.Count == 0) return;
            if (game.Npcs.Count(NpcKind.Customer) >= MaxCustomers) return;
            if (game.Territory.Entrances.Count == 0) return;

            int hour = game.Clock.Hour;
            foreach (var sq in Squares)
            {
                if (!sq.IsOpenAt(hour)) continue;
                var zone = game.Territory.Zones[sq.ZoneId];
                float perHour = 2.4f * zone.Demand * (1f - zone.Heat / 150f) * (0.7f + Respect / 250f);
                if (game.Rival.NearRivalTerritory(sq)) perHour *= 0.7f; // customers have a choice
                if (game.Rng.NextDouble() < perHour / GameClock.TicksPerHour)
                    game.Npcs.SpawnCustomer(sq, game);
            }

            foreach (var sq in game.Rival.Squares)
            {
                var zone = game.Territory.Zones[sq.ZoneId];
                if (game.Rng.NextDouble() < 1.2f * zone.Demand / GameClock.TicksPerHour)
                    game.Npcs.SpawnCustomer(sq, game);
            }
        }

        // ---------- Pizzo ----------

        public void BuildBusinesses(Map map, System.Random rng)
        {
            foreach (var def in BuildingDefs.All)
            {
                if (def.Shop == ShopKind.None) continue;
                foreach (var cell in map.PositionsOf(def))
                {
                    Businesses.Add(new Business
                    {
                        Cell = cell,
                        Kind = def.Shop,
                        Name = BusinessName(def.Shop, rng),
                        WeeklyPizzo = Mathf.RoundToInt(BasePizzo(def.Shop) * 0.7f / 10f) * 10 + rng.Next(-2, 3) * 10,
                        Defiance = 0.1f + (float)rng.NextDouble() * 0.5f,
                        CounterDefId = def.Id,
                    });
                }
            }
        }

        static string BusinessName(ShopKind kind, System.Random rng) => kind switch
        {
            ShopKind.Pizzeria => $"Pizzeria da {Names.Pick(Names.PizzaioloNames, rng)}",
            ShopKind.Salumeria => $"Salumeria {Names.Pick(Names.Surnames, rng)}",
            ShopKind.Farmacia => $"Farmacia {Names.Pick(Names.Saints, rng)}",
            ShopKind.Bar => $"Bar {Names.Pick(Names.Bars, rng)}",
            ShopKind.Macelleria => $"Macelleria {Names.Pick(Names.Surnames, rng)}",
            ShopKind.Tabaccheria => $"Tabacchi {Names.Pick(Names.Surnames, rng)}",
            ShopKind.Mobili => $"Mobilificio {Names.Pick(Names.Surnames, rng)}",
            ShopKind.Lusso => $"Gioielleria {Names.Pick(Names.Surnames, rng)}",
            ShopKind.Moto => $"Moto {Names.Pick(Names.Surnames, rng)}",
            _ => "Shop",
        };

        static int BasePizzo(ShopKind kind) => kind switch
        {
            ShopKind.Farmacia => 250,
            ShopKind.Tabaccheria => 180,
            ShopKind.Pizzeria => 150,
            ShopKind.Macelleria => 130,
            ShopKind.Salumeria => 120,
            ShopKind.Lusso => 300,
            ShopKind.Mobili => 160,
            ShopKind.Moto => 220,
            _ => 100,
        };

        public Business BusinessAt(Vector2Int cell) => Businesses.Find(b => b.Cell == cell);

        /// <summary>Orders a collector to go and impose pizzo.</summary>
        public void Target(Business b) => Target(b, null);

        public void Target(Business b, Game game)
        {
            if (game != null)
            {
                var blocker = PizzoBlocker(b, game);
                if (blocker != null)
                {
                    game.Log.Add($"{b.Name}: {blocker.ToLower()}. You can only impose pizzo in your own rioni.", MessageKind.Bad);
                    return;
                }
            }
            if (b.Status == BusinessStatus.Free || b.Status == BusinessStatus.Refusing)
            {
                b.Status = BusinessStatus.Targeted;
                b.PendingAction = Retaliation.None;
            }
        }

        // ---------- Retaliation ----------

        /// <summary>Orders the collector to scare or burn a business that refused to pay.</summary>
        public void OrderRetaliation(Business b, Retaliation action, Game game)
        {
            if (b.Status != BusinessStatus.Refusing) return;
            if (action == Retaliation.Burn && !RequireRank(2, "Burning a shop", game, out var error))
            {
                game.Log.Add(error, MessageKind.Bad);
                return;
            }
            b.PendingAction = action;
            game.Log.Add(action == Retaliation.Burn
                ? $"Order given: {b.Name} burns tonight, between {ArsonStartHour:00}:00 and {ArsonEndHour:00}:00."
                : $"Order given: someone pays {b.Name} a visit.");
        }

        public void ResolveRetaliation(Business b, Pawn pawn, Retaliation action, Game game)
        {
            b.PendingAction = Retaliation.None;
            var zone = game.Territory.ZoneAt(b.Cell);
            if (action == Retaliation.Scare) Scare(b, pawn, zone, game);
            else if (action == Retaliation.Burn) Burn(b, pawn, zone, game);
        }

        void Scare(Business b, Pawn pawn, Zone zone, Game game)
        {
            b.Defiance = Mathf.Max(0.02f, b.Defiance - 0.25f - 0.02f * pawn.IntimidationSkill);
            b.Status = BusinessStatus.Targeted; // the collector goes back to ask again, nicely
            Respect = Mathf.Min(100f, Respect + 2f);
            game.Territory.AddHeat(b.Cell, 8f);
            AddEvidence(3f, game);
            pawn.Say("scare");
            game.Log.Add($"{pawn.Nickname} smashed the window of {b.Name}. The owner will think twice now.");
            game.Newspaper.Report(game, 2, $"Shop window smashed in {zone?.Name ?? "the old town"}", $"The owner of {b.Name} found glass all over the floor. \"Kids,\" he told reporters.");
            if (game.Rng.NextDouble() < b.Defiance * 0.5)
            {
                game.Territory.AddHeat(b.Cell, 10f);
                game.Log.Add($"The owner of {b.Name} filed a report anyway. Heat is rising in {zone?.Name}.", MessageKind.Bad);
            }
        }

        void Burn(Business b, Pawn pawn, Zone zone, Game game)
        {
            long now = game.Ticks.TicksGame;
            b.Status = BusinessStatus.Closed;
            b.ClosedUntil = now + GameClock.TicksPerDay * 7;
            game.Map.SetBuilding(b.Cell.x, b.Cell.y, BuildingDefs.BurnedShop);
            Respect = Mathf.Min(100f, Respect + 6f);
            game.Territory.AddHeat(b.Cell, 25f);
            AddEvidence(10f, game);
            pawn.Say("burn");
            game.Log.Add($"{b.Name} burned in the night. The owner's family watches from the street.", MessageKind.Bad);
            game.Newspaper.Report(game, 5, $"Fire in the night: {b.Name} destroyed",
                $"Firefighters found traces of petrol. Investigators suspect extortion. The owner says he saw nothing and has no enemies.");

            // Fear spreads through the zone: everyone saw what happens to those who don't pay.
            int caved = 0;
            foreach (var other in Businesses)
            {
                if (other == b || game.Territory.ZoneAt(other.Cell) != zone) continue;
                other.Defiance = Mathf.Max(0.02f, other.Defiance - 0.15f);
                if (other.Status == BusinessStatus.Refusing && game.Rng.NextDouble() < 0.5)
                {
                    other.Status = BusinessStatus.Targeted;
                    other.PendingAction = Retaliation.None;
                    caved++;
                }
            }
            if (zone != null)
                game.Log.Add(caved > 0
                    ? $"Every shop in {zone.Name} got the message. {caved} that refused before are ready to talk."
                    : $"Every shop in {zone.Name} got the message.");
        }

        void ReopenBurnedShops(Game game)
        {
            long now = game.Ticks.TicksGame;
            foreach (var b in Businesses)
            {
                if (b.Status != BusinessStatus.Closed || now < b.ClosedUntil) continue;
                var def = BuildingDefs.ById(b.CounterDefId);
                if (def != null) game.Map.SetBuilding(b.Cell.x, b.Cell.y, def);
                b.Status = BusinessStatus.Free;
                b.Defiance = 0.05f;
                game.Log.Add($"{b.Name} has reopened. The owner keeps their eyes down.");
            }
        }

        public bool IsDue(Business b, int week) =>
            b.ClaimedBy == null &&
            (b.Status == BusinessStatus.Targeted || (b.Status == BusinessStatus.Paying && b.LastCollectedWeek < week));

        public void ResolvePizzoVisit(Business b, Pawn collector, Game game)
        {
            int week = WeekOf(game.Clock);
            double roll = game.Rng.NextDouble();

            if (b.Status == BusinessStatus.Targeted)
            {
                float chance = Mathf.Clamp(0.35f + 0.06f * collector.IntimidationSkill + 0.004f * Respect - 0.6f * b.Defiance, 0.05f, 0.95f);
                if (roll < chance)
                {
                    b.Status = BusinessStatus.Paying;
                    Respect = Mathf.Min(100f, Respect + 2f);
                    AddEvidence(1f, game);
                    collector.Say("pizzo_ok");
                    game.Log.Add($"{b.Name} agreed to pay €{b.WeeklyPizzo} a week.", MessageKind.Good);
                    Collect(b, week, game);
                }
                else
                {
                    b.Status = BusinessStatus.Refusing;
                    Respect = Mathf.Max(0f, Respect - 1f);
                    collector.Say("pizzo_refused");
                    game.Log.Add($"The owner of {b.Name} refused to pay.", MessageKind.Bad);
                    if (game.Rng.NextDouble() < 0.4 + b.Defiance * 0.5)
                    {
                        game.Territory.AddHeat(b.Cell, 15f);
                        AddEvidence(4f, game);
                        game.Log.Add($"The owner of {b.Name} went to the Carabinieri. Heat is rising in {game.Territory.ZoneAt(b.Cell)?.Name}.", MessageKind.Bad);
                    }
                }
                return;
            }

            if (b.Status == BusinessStatus.Paying)
            {
                if (roll < b.Defiance * 0.08)
                {
                    b.Status = BusinessStatus.Refusing;
                    game.Log.Add($"{b.Name} has stopped paying.", MessageKind.Bad);
                    return;
                }
                Collect(b, week, game);
            }
        }

        void Collect(Business b, int week, Game game)
        {
            game.Stock.Cash += b.WeeklyPizzo;
            game.PopupAt(Pawns.Mover.CellCenter(b.Cell), $"+€{b.WeeklyPizzo}");
            b.LastCollectedWeek = week;
            game.Log.Add($"Collected €{b.WeeklyPizzo} pizzo from {b.Name}.", MessageKind.Good);
        }
    }
}
