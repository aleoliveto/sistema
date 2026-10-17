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
    public sealed class Organization
    {
        public const int MaxSquares = 3;
        public const int MaxCrew = 12;
        const float HeatPerSale = 0.35f;
        const int MaxCustomers = 10;

        public float Respect = 10f;
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
        }

        void OnHour(Game game)
        {
            var clock = game.Clock;
            game.Territory.HourlyDecay();
            if (clock.Hour == 0) OnNewDay(game);
            if (clock.Hour == 8 && PendingOrderUnits > 0 && clock.Day >= PendingOrderDay) DeliverOrder(game);
            if (clock.Hour == 12 && clock.IsPayday && LastPaydayWeek != WeekOf(clock)) Payday(game);
            game.Police.HourlyCheck(game);
        }

        void OnNewDay(Game game)
        {
            foreach (var sq in Squares)
            {
                sq.SalesToday = 0;
                sq.LostToday = 0;
                sq.RevenueToday = 0;
            }

            // Loyalty drifts with mood: a miserable crew stops believing in you.
            foreach (var p in game.Pawns.All)
                if (p.IsPresent) p.Loyalty = Mathf.Clamp(p.Loyalty + (p.Mood.Value - 50f) / 25f, 0f, 100f);
        }

        // ---------- Supplier ----------

        public bool OrderProduct(int units, Game game, out string error)
        {
            error = null;
            int cost = units * Prices.ProductWholesale;
            if (PendingOrderUnits > 0) error = "An order is already on its way.";
            else if (game.Stock.Cash < cost) error = $"Not enough cash (€{cost} needed).";
            if (error != null) return false;

            game.Stock.Cash -= cost;
            PendingOrderUnits = units;
            PendingOrderDay = game.Clock.Hour < 8 ? game.Clock.Day : game.Clock.Day + 1;
            game.Log.Add($"Ordered {units} units of product for €{cost}. The courier comes at 08:00.");
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
            foreach (var p in game.Pawns.All)
            {
                if (p.Dead) continue;
                total += p.InJail ? Prices.JailedFamilySupport : p.Wage;
            }
            return total;
        }

        void Payday(Game game)
        {
            LastPaydayWeek = WeekOf(game.Clock);
            long now = game.Ticks.TicksGame;
            var stock = game.Stock;
            int paidTotal = 0, unpaid = 0;

            foreach (var p in game.Pawns.All)
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

        public Pawn Recruit(Game game, out string error)
        {
            error = null;
            int crew = 0;
            foreach (var p in game.Pawns.All) if (!p.Dead) crew++;
            if (crew >= MaxCrew) error = "The crew is as big as it can get for now.";
            else if (game.Stock.Cash < Prices.RecruitBonus) error = $"Recruiting costs €{Prices.RecruitBonus}.";
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
                Wage = 120,
                Loyalty = 45f + rng.Next(0, 20),
            };
            game.Stock.Cash -= Prices.RecruitBonus;
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
            error = null;
            var map = game.Map;
            var zone = game.Territory.ZoneAt(cell);
            if (!map.InBounds(cell) || zone == null) error = "Pick a sidewalk or courtyard inside a block.";
            else if (map.GetBuilding(cell) != null || map.GetTerrain(cell.x, cell.y) == TerrainType.Floor) error = "A piazza has to be outdoors.";
            else if (Squares.Count >= MaxSquares) error = $"You can run at most {MaxSquares} piazze for now.";
            else if (Squares.Exists(s => (s.Cell - cell).sqrMagnitude < 64)) error = "Too close to another piazza.";
            if (error != null) return false;

            var square = new DrugSquare { Cell = cell, ZoneId = zone.Id };
            ComputeLookoutPosts(square, map);
            Squares.Add(square);
            SquaresVersion++;
            game.Log.Add($"Opened a piazza in {zone.Name}. Assign pushers and lookouts, and order product.");
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
            foreach (var p in game.Pawns.All)
                if (p.CanAct && p.CurrentJob is PostJob post && post.Square == square && post.Role == CrewRole.Pusher && post.AtPost)
                    return p;
            return null;
        }

        public void Sell(DrugSquare square, Pawn pusher, Game game)
        {
            game.Stock.Product--;
            game.Stock.Cash += Prices.ProductStreet;
            square.SalesToday++;
            square.RevenueToday += Prices.ProductStreet;
            game.Territory.AddHeat(square.Cell, HeatPerSale);
            if (game.Rng.NextDouble() < 0.1) pusher.Say("sale");
        }

        void SpawnCustomers(Game game)
        {
            if (Squares.Count == 0 || !DrugSquare.IsOpenAt(game.Clock.Hour)) return;
            if (game.Npcs.Count(NpcKind.Customer) >= MaxCustomers) return;
            if (game.Territory.Entrances.Count == 0) return;

            foreach (var sq in Squares)
            {
                var zone = game.Territory.Zones[sq.ZoneId];
                float perHour = 1.6f * zone.Demand * (1f - zone.Heat / 150f) * (0.7f + Respect / 250f);
                if (game.Rng.NextDouble() < perHour / GameClock.TicksPerHour)
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
                        WeeklyPizzo = BasePizzo(def.Shop) + rng.Next(-2, 3) * 10,
                        Defiance = 0.1f + (float)rng.NextDouble() * 0.5f,
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
            _ => "Shop",
        };

        static int BasePizzo(ShopKind kind) => kind switch
        {
            ShopKind.Farmacia => 250,
            ShopKind.Tabaccheria => 180,
            ShopKind.Pizzeria => 150,
            ShopKind.Macelleria => 130,
            ShopKind.Salumeria => 120,
            _ => 100,
        };

        public Business BusinessAt(Vector2Int cell) => Businesses.Find(b => b.Cell == cell);

        /// <summary>Orders a collector to go and impose pizzo.</summary>
        public void Target(Business b)
        {
            if (b.Status == BusinessStatus.Free || b.Status == BusinessStatus.Refusing) b.Status = BusinessStatus.Targeted;
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
            b.LastCollectedWeek = week;
            game.Log.Add($"Collected €{b.WeeklyPizzo} pizzo from {b.Name}.", MessageKind.Good);
        }
    }
}
