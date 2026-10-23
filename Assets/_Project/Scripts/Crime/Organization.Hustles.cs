using System;
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
    public enum StreetWork { Parking, Stall }

    /// <summary>A loan to a shopkeeper: interest every week, then the money back. Miss twice and the shop is yours.</summary>
    [Serializable]
    public sealed class ShopLoan
    {
        public int x, y, owed, weekly, weeksLeft, missed, startDay;
    }

    /// <summary>Everything the hustles and rackets remember, saved as one block.</summary>
    [Serializable]
    public sealed class HustleState
    {
        public int fakes;                 // fake bags and Napoli shirts, to sell at the stall
        public bool lotto;
        public bool betting;
        public int poker;                 // video poker machines in the bars
        public int roofFloors;            // illegal floors built on the family's roofs
        public int cavalli;               // stolen scooters hidden away, waiting for their owners to call
        public long cavalloAt;
        public int cavalloDay = -1;
        public bool cavalloRiding;        // the boss is on the way to Nanninella's with one
        public int lastLotto = -1, lastBet = -1, lastWeekly = -1, lastLoans = -1;
        public List<ShopLoan> loans = new List<ShopLoan>();
    }

    /// <summary>
    /// The rungs between rolling cigarettes and running piazze, and the rackets that come with a name.
    /// Early (anyone): minding cars as a parcheggiatore abusivo, a stall with ROSSE and fake goods, the cavallo di
    /// ritorno (steal a scooter, sell it back to its owner). With a Paranza: the lotto clandestino, a book on Napoli's
    /// matches, usura, illegal rooftop floors. With a Clan: video poker in the bars.
    /// </summary>
    public sealed partial class Organization
    {
        public HustleState H = new HustleState();

        /// <summary>Where the boss himself is minding cars or selling right now (not saved: you have to be there).</summary>
        public Vector2Int? PlayerParking, PlayerStall;
        int playerStreetTakings;
        int parkingToday, stallToday;   // the crew's street takings, for the evening's line

        public const int FakesBatch = 10, FakesBatchPrice = 60, FakePrice = 12;
        public const int LottoSetup = 300, BettingSetup = 200, PokerMachine = 400, RoofFloorCost = 1500, LoanAmount = 500;
        public const int RoofRent = 130, MaxRoofFloors = 2, MaxLoans = 3;

        public static int DayOfWeek(GameClock c) => (c.Day - 1) % 7;   // 0 Monday … 6 Sunday

        // ---------- Street work ----------

        /// <summary>A spot for street work near home: beside a parked car for parking, beside a market stall (or a bar) for selling.</summary>
        public Vector2Int? StreetSpot(StreetWork kind, Pawn who, Game game)
        {
            var map = game.Map;
            var home = Vector2Int.RoundToInt(game.PlayerBase.center);
            var taken = new HashSet<Vector2Int>();
            foreach (var p in game.Pawns.Crew)
                if (p != who && p.CurrentJob is StreetWorkJob sw) taken.Add(sw.Spot);
            var anchors = new List<Vector2Int>();
            if (kind == StreetWork.Parking)
            {
                anchors.AddRange(map.PositionsOf(BuildingDefs.ParkedCar));
                anchors.AddRange(map.PositionsOf(BuildingDefs.ParkedScooter));
            }
            else
            {
                anchors.AddRange(map.PositionsOf(BuildingDefs.MarketStall));
                foreach (var b in Businesses) if (b.Kind == ShopKind.Bar) anchors.Add(b.Cell);
            }
            anchors.Sort((a, b) => GridUtil.Manhattan(a, home).CompareTo(GridUtil.Manhattan(b, home)));
            foreach (var a in anchors)
            {
                if (GridUtil.Manhattan(a, home) > 70) break;
                foreach (var c in GridUtil.CellsAround(a, 1))
                {
                    if (taken.Contains(c) || !map.InBounds(c) || !map.IsPassable(c) || map.GetBuilding(c) != null) continue;
                    var t = map.GetTerrain(c.x, c.y);
                    if (t == TerrainType.Floor || t == TerrainType.Marble) continue;   // out on the street, not in a shop
                    return c;
                }
            }
            return null;
        }

        /// <summary>The boss starts minding cars, or selling, right here (E). Stay close and the takings come in by the hour.</summary>
        public void StartStreetWork(StreetWork kind, Vector2Int at, Game game)
        {
            playerStreetTakings = 0;
            if (kind == StreetWork.Parking)
            {
                PlayerParking = at;
                PlayerStall = null;
                game.Log.Add("You wave the cars into the spaces. A few euro an hour in tips while you stay here.", MessageKind.Info, at);
            }
            else
            {
                PlayerStall = at;
                PlayerParking = null;
                var zone = game.Territory.ZoneAt(at);
                StallCorner = new DrugSquare { Cell = at, ZoneId = zone != null ? zone.Id : 0, OpenHour = 0, CloseHour = 0 };
                game.Log.Add($"You set out your stock: {game.Stock.Cigarettes} packs of ROSSE, {H.fakes} fakes. People will stop by: G (or E) to sell. Watch for the Finanza.", MessageKind.Info, at);
            }
        }

        void EndPlayerStreetWork(Game game)
        {
            if (playerStreetTakings > 0)
                game.Log.Add($"You packed up. €{playerStreetTakings} made on the street.", MessageKind.Good);
            PlayerParking = PlayerStall = null;
            StallCorner = null;
            playerStreetTakings = 0;
        }

        // ---------- The boss at his stall: buyers come up one at a time, like at the corner ----------

        /// <summary>The player's own stall, as a selling spot customers walk to (not a piazza).</summary>
        public DrugSquare StallCorner;

        public bool IsPlayerCorner(DrugSquare sq) => sq != null && (sq == DealingCorner || sq == StallCorner);

        public bool HasStallStock(Game game) => game.Stock.Cigarettes > 0 || H.fakes > 0;

        /// <summary>What the next buyer wants: mostly a pack, sometimes a fake bag or shirt.</summary>
        public string StallOffer(Npc customer, Game game)
        {
            bool fake = H.fakes > 0 && (game.Stock.Cigarettes <= 0 || customer.Id % 3 == 0);
            return fake ? $"Sell a fake · €{FakePrice}" : $"Sell a pack · €{PackPrice(game) + 1}";
        }

        void SellFromStall(Npc customer, Game game)
        {
            var p = game.Player;
            var s = game.Stock;
            bool fake = H.fakes > 0 && (s.Cigarettes <= 0 || customer.Id % 3 == 0);
            int price;
            if (fake) { H.fakes--; price = FakePrice; }
            else if (s.Cigarettes > 0) { s.Cigarettes--; price = PackPrice(game) + 1; }
            else return;
            // A second pack now and then.
            if (!fake && s.Cigarettes > 0 && game.Rng.NextDouble() < 0.25) { s.Cigarettes--; price += PackPrice(game) + 1; }
            s.Cash += price;
            playerStreetTakings += price;
            game.PopupAt(p.Position, $"+€{price}");
            customer.State = 2;
            customer.Walker = new Walker(customer.Exit, adjacent: false);
            if (!HasStallStock(game))
            {
                game.Log.Add("Nothing left to sell.", MessageKind.Info);
                EndPlayerStreetWork(game);
            }
        }

        /// <summary>Passers-by stop at the boss's stall: busiest at lunch and in the evening.</summary>
        void SpawnStallCustomers(Game game)
        {
            if (StallCorner == null) return;
            int h = game.Clock.Hour;
            if (h < 9 || h >= 21) return;
            float perHour = (h >= 12 && h < 14) || h >= 18 ? 7f : 4.5f;
            var zone = game.Territory.Zones[StallCorner.ZoneId];
            perHour *= 0.7f + zone.Demand * 0.3f;
            if (game.Rng.NextDouble() >= perHour / GameClock.TicksPerHour) return;
            var npc = game.Npcs.SpawnCustomer(StallCorner, game);
            npc.Mover.Teleport(game.StreetLife.RandomDestinationNear(StallCorner.Cell, game));
        }

        /// <summary>One hour of minding cars: tips, more in a busier rione, and now and then a driver who won't pay.</summary>
        int ParkingHour(Vector2Int at, Game game)
        {
            int h = game.Clock.Hour;
            if (h < 8 || h >= 23) return 0;
            int tips = game.Rng.Next(1, 4) + (h >= 19 ? 1 : 0);    // a few coins a car; evenings, when people go out, pay best
            game.Territory.AddHeat(at, 0.15f);
            return tips;
        }

        /// <summary>The Finanza walks up: the stock on the stall is gone. True if they came.</summary>
        bool FinanzaCheck(Vector2Int at, Game game)
        {
            if (game.Rng.NextDouble() >= 0.025) return false;
            var s = game.Stock;
            int packs = Math.Min(s.Cigarettes, 8), fakes = Math.Min(H.fakes, 5);
            s.Cigarettes -= packs;
            H.fakes -= fakes;
            AddEvidence(1f, game);
            game.Territory.AddHeat(at, 4f);
            game.Log.Add($"The Guardia di Finanza walked up to the stall: {packs} packs and {fakes} fakes confiscated, and your name taken.", MessageKind.Bad, at);
            if (!HasStallStock(game)) EndPlayerStreetWork(game);
            return true;
        }

        /// <summary>One hour at the stall: a few packs and a few fakes go, at street prices. The Finanza now and then.</summary>
        int StallHour(Vector2Int at, Game game, bool boss)
        {
            int h = game.Clock.Hour;
            if (h < 9 || h >= 21) return 0;
            var s = game.Stock;
            if (game.Rng.NextDouble() < 0.025)
            {
                int packs = Math.Min(s.Cigarettes, 8), fakes = Math.Min(H.fakes, 5);
                s.Cigarettes -= packs;
                H.fakes -= fakes;
                AddEvidence(1f, game);
                game.Territory.AddHeat(at, 4f);
                game.Log.Add($"The Guardia di Finanza walked up to the stall: {packs} packs and {fakes} fakes confiscated{(boss ? ", and your name taken" : "")}.", MessageKind.Bad, at);
                return 0;
            }
            int earned = 0;
            int sellPacks = Math.Min(s.Cigarettes, game.Rng.Next(1, 4));
            s.Cigarettes -= sellPacks;
            earned += sellPacks * (PackPrice(game) + 1);
            int sellFakes = Math.Min(H.fakes, game.Rng.Next(0, 3));
            H.fakes -= sellFakes;
            earned += sellFakes * FakePrice;
            return earned;
        }

        // ---------- The hourly round ----------

        void HustlesHourly(Game game)
        {
            var s = game.Stock;
            long now = game.Ticks.TicksGame;
            int earned = 0;

            // The crew at work on the street.
            foreach (var p in game.Pawns.Crew)
            {
                if (!p.CanAct || !(p.CurrentJob is StreetWorkJob sw) || !sw.AtSpot) continue;
                if (sw.Kind == StreetWork.Parking) { int e = ParkingHour(sw.Spot, game); parkingToday += e; earned += e; }
                else { int e = StallHour(sw.Spot, game, false); stallToday += e; earned += e; }
            }

            // The boss himself, if he's stayed at his spot.
            var boss = game.Player;
            var spot = PlayerParking ?? PlayerStall;
            if (spot.HasValue)
            {
                bool there = boss != null && boss.CanAct && boss.Sleep == SleepState.Awake && !boss.Riding
                             && (boss.Position - Mover.CellCenter(spot.Value)).sqrMagnitude < 4f * 4f;
                if (!there) EndPlayerStreetWork(game);
                else
                {
                    // At the stall the boss sells by hand (SellFromStall); the hour only brings the Finanza's risk.
                    int e = 0;
                    if (PlayerParking.HasValue) e = ParkingHour(spot.Value, game);
                    else FinanzaCheck(spot.Value, game);
                    playerStreetTakings += e;
                    earned += e;
                }
            }
            s.Cash += earned;

            // A stolen scooter's owner rings, as they always do, to buy it back.
            if (H.cavalli > 0 && now >= H.cavalloAt && game.Events.Current == null)
            {
                H.cavalli--;
                H.cavalloAt = now + GameClock.TicksPerHour * game.Rng.Next(3, 8);
                game.Events.Raise(CavalloCall(game));
            }

            var c = game.Clock;
            int dow = DayOfWeek(c);
            // The evening's count from the street.
            if (c.Hour == 22 && parkingToday + stallToday > 0)
            {
                game.Log.Add($"The street today: €{parkingToday} from the cars, €{stallToday} from the stall.", MessageKind.Good);
                parkingToday = stallToday = 0;
            }
            // The lotto draw: Saturday evening.
            if (H.lotto && dow == 5 && c.Hour == 19 && H.lastLotto != c.Day) { H.lastLotto = c.Day; LottoDraw(game); }
            // Napoli play on Sunday afternoon: the book settles at five.
            if (H.betting && dow == 6 && c.Hour == 17 && H.lastBet != c.Day) { H.lastBet = c.Day; SettleBets(game); }
            // Sunday noon, with the wages: the machines and the rooftop rents.
            if (dow == 6 && c.Hour == 12 && H.lastWeekly != c.Day) { H.lastWeekly = c.Day; RacketsWeekly(game); }
            // Monday morning: the loans come due.
            if (dow == 0 && c.Hour == 10 && H.lastLoans != c.Day) { H.lastLoans = c.Day; LoansWeekly(game); }
        }

        // ---------- Cavallo di ritorno ----------

        public bool CanStealForRansom(Game game) => H.cavalloDay != game.Clock.Day;

        /// <summary>Take a parked scooter off the street and hide it; its owner will call to buy it back.</summary>
        public void StealForRansom(Vector2Int at, Game game)
        {
            var p = game.Player;
            if (p == null || p.Riding) return;
            // You ride off on that very scooter (same model, same colour) to Nanninella's, who hides it.
            p.StolenLook = OSistema.Views.PropArt.ParkedScooterLook(at.x, at.y);
            game.Map.SetBuilding(at.x, at.y, null);
            H.cavalloDay = game.Clock.Day;
            H.cavalloRiding = true;
            p.StolenScooter = true;
            p.Riding = p.OnScooter = true;
            p.Velocity = Vector2.zero;
            p.ScooterSpeed = 0f;
            p.Carrying = "a stolen scooter";
            p.EndCurrentJob(game);
            if (game.Rng.NextDouble() < 0.3)
            {
                game.Territory.AddHeat(at, 6f);
                game.Log.Add("Someone on a balcony saw you take it. Heat is rising.", MessageKind.Bad, at);
            }
            game.Log.Add("It started first kick. Ride it to Nanninella: she'll hide it until the owner comes asking.", MessageKind.Info, at);
        }

        /// <summary>Every tick while riding a cavallo: at Nanninella's, she takes it and the wait for the owner begins.</summary>
        public void CheckCavalloDelivery(Game game)
        {
            if (!H.cavalloRiding) return;
            var p = game.Player;
            if (p == null || !p.StolenScooter || p.Dead) { H.cavalloRiding = false; return; }
            var nanni = game.Contacts.ByName(ContactBook.Nanninella);
            if (nanni == null || (nanni.Position - p.Position).sqrMagnitude > 2.5f * 2.5f) return;
            H.cavalloRiding = false;
            p.StolenScooter = false;
            p.Riding = p.OnScooter = false;
            p.Velocity = Vector2.zero;
            p.ScooterSpeed = 0f;
            p.Carrying = null;
            p.Mover.Unstick(game.Map);
            H.cavalli++;
            if (H.cavalli == 1) H.cavalloAt = game.Ticks.TicksGame + GameClock.TicksPerHour * game.Rng.Next(4, 9);
            game.Log.Add("Nanninella rolls it into the back of her garage. \"Mo' aspettammo ca 'o padrone se fa vivo.\" Now you wait for the owner to ask around.", MessageKind.Good, p.Cell);
        }

        /// <summary>Where the cavallo goes, for the objective line.</summary>
        public Vector2? CavalloDropAt(Game game)
        {
            if (!H.cavalloRiding) return null;
            var nanni = game.Contacts.ByName(ContactBook.Nanninella);
            return nanni?.Position;
        }

        GameEvent CavalloCall(Game game)
        {
            int ask = game.Rng.Next(15, 31) * 10;
            return new GameEvent
            {
                Id = "cavallo",
                Title = "Cavallo di ritorno",
                Neapolitan = "Me dicono ca vuje sapite addò sta 'o motorino mio...",
                English = "They tell me you know where my scooter is...",
                Body = $"The owner of the scooter you took has asked around the rione and found his way to you. He'd rather pay than go to the police. €{ask} and he gets it back, no questions.",
            }
            .Option($"€{ask}, and he gets it back", "Cash in hand; the scooter turns up outside his door tonight.", g =>
            {
                g.Stock.Cash += ask;
                g.Org.Respect = Mathf.Min(100f, g.Org.Respect + 0.5f);
                g.Log.Add($"€{ask} for a scooter that was his to begin with. That's how it works here.", MessageKind.Good);
            })
            .Option($"Ask for €{ask * 2}", "He might pay. He might go to the Carabinieri instead.", g =>
            {
                if (g.Rng.NextDouble() < 0.5)
                {
                    g.Stock.Cash += ask * 2;
                    g.Log.Add($"He paid €{ask * 2}, through his teeth.", MessageKind.Good);
                }
                else
                {
                    g.Org.AddEvidence(3f, g);
                    g.Log.Add("He went to the Carabinieri instead. They've got a description of you.", MessageKind.Bad);
                }
            });
        }

        // ---------- Lotto clandestino ----------

        static readonly (int number, string dream)[] Smorfia =
        {
            (1, "'L'Italia'"), (17, "'a disgrazia"), (33, "ll'anne 'e Cristo"), (47, "'o muorto che parla"), (48, "'o muorto ca parla"),
            (71, "l'ommo 'e merda"), (77, "'e diavule"), (90, "'a paura"), (8, "'a Maronna"), (22, "'o pazzo"), (55, "'a musica"), (13, "Sant'Antonio"),
        };

        public bool StartLotto(Game game, out string error)
        {
            error = Rank < 1 ? "The lotto needs a Paranza behind it: people have to trust you'll pay." : null;
            if (error != null || H.lotto) return false;
            if (!Pay(LottoSetup, game, out error)) return false;
            H.lotto = true;
            game.Log.Add("The lotto clandestino is open: the neighbours bring you their dreams, you give them numbers. The draw is every Saturday at seven.", MessageKind.Good);
            return true;
        }

        void LottoDraw(Game game)
        {
            var rng = game.Rng;
            int takings = 150 + 60 * Turf(game).Count + rng.Next(0, 120);
            var (num, dream) = Smorfia[rng.Next(Smorfia.Length)];
            int payout = rng.NextDouble() < 0.15 ? rng.Next(5, 13) * 100 : 0;
            game.Stock.Cash += takings - payout;
            if (payout > 0)
                game.Log.Add($"Lotto: {num} came up, {dream}. Somebody dreamed it right: €{takings} taken, €{payout} paid out.", payout > takings ? MessageKind.Bad : MessageKind.Info);
            else
                game.Log.Add($"Lotto: {num} came up, {dream}. Nobody had it: the bank keeps €{takings}.", MessageKind.Good);
        }

        // ---------- The book on Napoli ----------

        public bool StartBetting(Game game, out string error)
        {
            error = Rank < 1 ? "Nobody bets with a guaglione: they need to know you'll pay out." : null;
            if (error != null || H.betting) return false;
            if (!Pay(BettingSetup, game, out error)) return false;
            H.betting = true;
            game.Log.Add("You're taking bets on Napoli. Every Sunday the book settles after the match.", MessageKind.Good);
            return true;
        }

        void SettleBets(Game game)
        {
            var rng = game.Rng;
            int takings = 200 + 50 * Turf(game).Count + rng.Next(0, 150);
            bool won = rng.NextDouble() < 0.45;
            int payout = won ? Mathf.RoundToInt(takings * (0.6f + (float)rng.NextDouble() * 0.8f)) : 0;
            game.Stock.Cash += takings - payout;
            game.Log.Add(won ? $"Napoli won! Half the rione had them to win: €{takings} in bets, €{payout} paid out."
                             : $"Napoli didn't win. The book keeps €{takings}.", won && payout > takings ? MessageKind.Bad : MessageKind.Good);
        }

        // ---------- Video poker, rooftop floors ----------

        public int MaxPokerMachines(Game game)
        {
            int bars = 0;
            var turf = new HashSet<Zone>(Turf(game));
            foreach (var b in Businesses)
                if (b.Kind == ShopKind.Bar && turf.Contains(game.Territory.ZoneAt(b.Cell))) bars++;
            return bars + 1;
        }

        public bool InstallPoker(Game game, out string error)
        {
            error = Rank < 2 ? "The bars only take a Clan's machines." : H.poker >= MaxPokerMachines(game) ? "Every bar on your turf has a machine already." : null;
            if (error != null) return false;
            if (!Pay(PokerMachine, game, out error)) return false;
            H.poker++;
            game.Log.Add("A video poker machine goes into a bar on your turf. The owner gets a cut, you get the rest, every Sunday.", MessageKind.Good);
            return true;
        }

        public bool OwnsHome(Game game)
        {
            foreach (var p in game.Properties.Owned) if (p.Kind == PropertyKind.Home) return true;
            return false;
        }

        public bool BuildRoofFloor(Game game, out string error)
        {
            error = Rank < 1 ? "A builder won't risk it for a nobody." : !OwnsHome(game) ? "You need a home of your own to build on." : H.roofFloors >= MaxRoofFloors ? "Two floors is all the roof will take." : null;
            if (error != null) return false;
            if (!Pay(RoofFloorCost, game, out error)) return false;
            H.roofFloors++;
            game.Log.Add($"A new floor on the roof, no permits, no architect. Rented out from Sunday: €{RoofRent} a week.", MessageKind.Good);
            return true;
        }

        void RacketsWeekly(Game game)
        {
            var rng = game.Rng;
            int poker = 0, seized = 0;
            for (int i = 0; i < H.poker; i++)
            {
                if (rng.NextDouble() < 0.06) { seized++; continue; }
                poker += rng.Next(80, 141);
            }
            H.poker -= seized;
            if (seized > 0)
            {
                AddEvidence(2f * seized, game);
                game.Log.Add($"The police took {seized} video poker machine{(seized == 1 ? "" : "s")} out of a bar. The owner kept his mouth shut, for now.", MessageKind.Bad);
            }
            if (poker > 0) { game.Stock.Cash += poker; game.Log.Add($"Video poker: €{poker} from the machines this week.", MessageKind.Good); }

            if (H.roofFloors > 0)
            {
                if (rng.NextDouble() < 0.1)
                {
                    const int fine = 400;
                    if (game.Stock.Cash >= fine)
                    {
                        game.Stock.Cash -= fine;
                        game.Log.Add($"A council inspector climbed up to your roof. €{fine} and he saw nothing.", MessageKind.Bad);
                    }
                    else
                    {
                        H.roofFloors--;
                        game.Log.Add("A council inspector found the illegal floor, and there was nothing to pay him with. It comes down.", MessageKind.Bad);
                    }
                }
                int rent = H.roofFloors * RoofRent;
                if (rent > 0) { game.Stock.Cash += rent; game.Log.Add($"Rent from the rooftop floors: €{rent}.", MessageKind.Good); }
            }
        }

        // ---------- Usura ----------

        public ShopLoan LoanTo(Business b) => H.loans.Find(l => l.x == b.Cell.x && l.y == b.Cell.y);

        public bool Lend(Business b, Game game, out string error)
        {
            error = Rank < 1 ? "Shopkeepers don't borrow from a guaglione." : LoanTo(b) != null ? "He already owes you." :
                    H.loans.Count >= MaxLoans ? $"You can only carry {MaxLoans} loans at once." : b.Status == BusinessStatus.Owned ? "It's yours already." : null;
            if (error != null) return false;
            if (!Pay(LoanAmount, game, out error)) return false;
            H.loans.Add(new ShopLoan { x = b.Cell.x, y = b.Cell.y, owed = LoanAmount, weekly = LoanAmount * 15 / 100, weeksLeft = 4, startDay = game.Clock.Day });
            game.Log.Add($"{b.Name} took €{LoanAmount} off you: €{LoanAmount * 15 / 100} a week for four weeks, then the €{LoanAmount} back. Miss twice and the shop is yours.", MessageKind.Info, b.Cell);
            return true;
        }

        void LoansWeekly(Game game)
        {
            for (int i = H.loans.Count - 1; i >= 0; i--)
            {
                var l = H.loans[i];
                var b = BusinessAt(new Vector2Int(l.x, l.y));
                if (b == null) { H.loans.RemoveAt(i); continue; }
                if (l.startDay == game.Clock.Day) continue;   // lent today: the first week starts now
                int due = l.weeksLeft > 0 ? l.weekly : l.owed;
                bool pays = game.Rng.NextDouble() < 0.82 - b.Defiance * 0.3;
                if (pays)
                {
                    game.Stock.Cash += due;
                    if (l.weeksLeft > 0) l.weeksLeft--;
                    else
                    {
                        H.loans.RemoveAt(i);
                        game.Log.Add($"{b.Name} paid back the €{l.owed}. Square.", MessageKind.Good, b.Cell);
                        continue;
                    }
                    game.Log.Add($"{b.Name} paid this week's €{due} on the loan.", MessageKind.Info, b.Cell);
                }
                else if (++l.missed >= 2)
                {
                    H.loans.RemoveAt(i);
                    b.Status = BusinessStatus.Owned;
                    b.PendingAction = Retaliation.None;
                    Respect = Mathf.Min(100f, Respect + 2f);
                    game.Log.Add($"{b.Name} couldn't pay, twice. The keys are yours now: a front with clean takings.", MessageKind.Good, b.Cell);
                }
                else game.Log.Add($"{b.Name} couldn't pay this week. Once more and the shop is yours.", MessageKind.Bad, b.Cell);
            }
        }
    }
}
