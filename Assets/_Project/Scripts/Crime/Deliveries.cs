using System;
using System.Collections.Generic;
using OSistema.Core;
using OSistema.Npcs;
using OSistema.Pawns;
using OSistema.Social;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>Someone in the rione who texts the family phone for a delivery.</summary>
    [Serializable]
    public sealed class Customer
    {
        public int Id;
        public string Name;
        public int Neighbour = -1;      // one of the named neighbours (lives in a Resident's house), or -1: a friend of a friend
        public int HomeX, HomeY;        // outside their front door
        public int Likes;               // the Item they usually want
        public float Happy = 0.6f;      // 0..1: on time and honest bags keep them calling
        public int Orders, Late;
        public int NextHour;            // absolute game hour of the earliest next order
        public bool Gone;               // stopped ordering
        public Vector2Int Home => new Vector2Int(HomeX, HomeY);
    }

    [Serializable]
    public sealed class Order
    {
        public int Id, Customer = -1, Msg;
        public string Name, Place;
        public int Item, Amount, Pay;
        public int X, Y;
        public long From, Until;        // the time window, in ticks
        public int State;               // 0 offered, 1 accepted, 2 done, 3 failed
        public int Bait;                // 0 a real customer, 1 plainclothes police, 2 a clan setting you up
        public int Clan = -1;
        public Vector2Int Cell => new Vector2Int(X, Y);
    }

    [Serializable]
    public sealed class DeliveryState
    {
        public List<Customer> Customers = new List<Customer>();
        public List<Order> Orders = new List<Order>();
        public int NextId = 1;
    }

    /// <summary>
    /// Orders over the family phone: a neighbour texts for a few doses, ROSSE or fakes, at their door, a bar
    /// or a corner, in a time window. Accept, carry it there in the bag (or under the scooter seat) and hand
    /// it over with E. Better money than the corner; regulars who are served well call more and pass your
    /// number on, the ones kept waiting stop calling. Some numbers are bait.
    /// Neapolitan lines here are drafts for native review.
    /// </summary>
    public sealed class Deliveries
    {
        const float WindowHours = 1.5f, GraceHours = 0.5f, AnswerHours = 1f;
        const float HandReach = 1.9f;

        DeliveryState s = new DeliveryState();
        public DeliveryState S { get => s; set { s = value ?? new DeliveryState(); waiting.Clear(); } }

        // Who is standing at the meeting place right now (not saved: they come back out after a load).
        readonly Dictionary<int, Npc> waiting = new Dictionary<int, Npc>();

        /// <summary>Developer soak: accept every order and hand it over the moment the window opens.</summary>
        public bool DevAuto;

        static readonly string[] Friends =
        {
            "Gennaro 'o Biondo", "Lello", "Enzo 'o Meccanico", "Rituccia", "Pasqualino", "Salvatore 'o Lungo",
            "Cuncettina", "Peppe 'o Studente", "Totore", "Nennella", "Gigino", "Mimì 'a Parrucchiera",
        };

        public int Accepted
        {
            get { int n = 0; foreach (var o in s.Orders) if (o.State == 1) n++; return n; }
        }

        public Customer ById(int id) => s.Customers.Find(c => c.Id == id);

        /// <summary>A neighbour out to meet you: Residents keeps them indoors so there's only one of them.</summary>
        public bool Busy(int neighbourId)
        {
            foreach (var kv in waiting)
            {
                var o = s.Orders.Find(x => x.Id == kv.Key);
                var c = o != null ? ById(o.Customer) : null;
                if (c != null && c.Neighbour == neighbourId) return true;
            }
            return false;
        }

        /// <summary>Where the accepted orders are to be handed over, for the map.</summary>
        public IEnumerable<Vector2> Markers()
        {
            foreach (var o in s.Orders) if (o.State == 1) yield return Mover.CellCenter(o.Cell);
        }

        /// <summary>The people waiting for you, for their name tag in the street.</summary>
        public IEnumerable<(Vector2 at, string name)> Waiting()
        {
            foreach (var kv in waiting)
            {
                if (kv.Value.Despawned) continue;
                var o = s.Orders.Find(x => x.Id == kv.Key);
                if (o != null && o.State == 1) yield return (kv.Value.Mover.Position, o.Name);
            }
        }

        static long Now(Game g) => g.Ticks.TicksGame;
        static int HourAbs(Game g) => g.Clock.Day * 24 + g.Clock.Hour;
        static string Clock(Game g, long tick)
        {
            long dt = tick - Now(g);
            float h = g.Clock.HourF + dt / (float)GameClock.TicksPerHour;
            h = (h % 24f + 24f) % 24f;
            int hh = Mathf.FloorToInt(h), mm = Mathf.FloorToInt((h - hh) * 60f / 5f) * 5;
            return $"{hh:00}:{mm:00}";
        }

        public static string What(Order o) => Noun((Item)o.Item, o.Amount);

        static string Noun(Item i, int n) => i switch
        {
            Item.Doses => n == 1 ? "1 dose" : $"{n} doses",
            Item.Packs => n == 1 ? "1 pack of ROSSE" : $"{n} packs of ROSSE",
            _ => n == 1 ? "1 fake" : $"{n} fakes",
        };

        // ---------- Once an hour ----------

        public void Hourly(Game game)
        {
            if (game.District.Id != District.HomeId || game.Org.GameOver) return;
            Seed(game);
            long now = Now(game);
            // Orders nobody answered: dropped quietly, they'll ask someone else.
            foreach (var o in s.Orders)
            {
                if (o.State != 0) continue;
                var m = FindMsg(game, o.Msg);
                if (m != null && m.Open(now)) continue;
                o.State = 3;
                var c = ById(o.Customer);
                if (c != null) c.Happy = Mathf.Max(0f, c.Happy - 0.03f);
            }
            s.Orders.RemoveAll(o => o.State >= 2 && now - o.Until > GameClock.TicksPerDay);

            var boss = game.Player;
            int hour = game.Clock.Hour;
            if (boss == null || boss.InJail || hour < 9 || hour > 22) return;
            int open = 0;
            foreach (var o in s.Orders) if (o.State <= 1) open++;
            float respect = game.Org.Respect;
            int cap = Mathf.Min(3, 1 + Mathf.FloorToInt(respect / 30f));
            if (open >= cap) return;
            if (game.Rng.NextDouble() > Mathf.Min(0.7f, 0.3f + respect / 150f)) return;

            if (TryBait(game)) return;
            // Someone due to call, the happiest most likely.
            Customer pick = null;
            float best = 0f;
            int abs = HourAbs(game);
            foreach (var c in s.Customers)
            {
                if (c.Gone || c.NextHour > abs || s.Orders.Exists(o => o.Customer == c.Id && o.State <= 1)) continue;
                float w = (0.2f + c.Happy) * (float)game.Rng.NextDouble();
                if (w > best) { best = w; pick = c; }
            }
            if (pick != null) Offer(game, pick);
        }

        /// <summary>The first customers: a few of the neighbours, once they live somewhere.</summary>
        void Seed(Game game)
        {
            if (s.Customers.Count > 0) return;
            var res = game.StreetLife?.Residents;
            if (res == null || res.All.Count == 0) return;
            var list = new List<Resident>(res.All);
            int n = Mathf.Min(4, list.Count);
            for (int i = 0; i < n; i++)
            {
                int k = game.Rng.Next(list.Count);
                var r = list[k];
                list.RemoveAt(k);
                s.Customers.Add(new Customer
                {
                    Id = s.NextId++, Name = r.Who.Name, Neighbour = r.Who.Id, HomeX = r.Home.x, HomeY = r.Home.y,
                    Likes = (int)RandomLike(game), NextHour = HourAbs(game) + game.Rng.Next(1, 8),
                });
            }
        }

        static Item RandomLike(Game game)
        {
            double r = game.Rng.NextDouble();
            return r < 0.5 ? Item.Doses : r < 0.85 ? Item.Packs : Item.Fakes;
        }

        /// <summary>Does the family have any of this, in the stash or in pockets?</summary>
        static int Have(Game game, Item item)
        {
            var p = game.Player;
            return item switch
            {
                Item.Doses => game.Stock.Product + (p?.PocketDoses ?? 0),
                Item.Packs => game.Stock.Cigarettes + (p?.PocketPacks ?? 0),
                Item.Fakes => game.Org.H.fakes + (p?.PocketFakes ?? 0),
                _ => 0,
            };
        }

        public static int UnitPay(Game game, Item item) => item switch
        {
            Item.Doses => Prices.ProductStreet + 8,
            Item.Packs => game.Org.PackPrice(game) + 3,
            _ => Organization.FakePrice + 5,
        };

        static Item Available(Game game, Item liked)
        {
            if (Have(game, liked) > 0) return liked;
            foreach (var i in new[] { Item.Packs, Item.Doses, Item.Fakes }) if (Have(game, i) > 0) return i;
            // Nothing in the house: some still ask, and it's a reason to get some.
            return game.Rng.NextDouble() < 0.4 ? liked : Item.None;
        }

        static int AmountOf(Game game, Item item, float happy)
        {
            int lo = item == Item.Doses ? 2 : item == Item.Packs ? 3 : 1;
            int hi = item == Item.Doses ? 6 : item == Item.Packs ? 10 : 3;
            return Mathf.Clamp(lo + Mathf.RoundToInt((hi - lo) * (0.3f + happy * 0.7f) * (float)game.Rng.NextDouble() + 0.4f), lo, hi);
        }

        void Offer(Game game, Customer c)
        {
            var item = Available(game, (Item)c.Likes);
            if (item == Item.None) { c.NextHour = HourAbs(game) + 6; return; }
            int amount = AmountOf(game, item, c.Happy);
            var (cell, place) = PlaceFor(game, c.Home, game.Rng.Next(3));
            var o = NewOrder(game, c.Name, item, amount, cell, place);
            o.Customer = c.Id;
            c.NextHour = HourAbs(game) + Mathf.RoundToInt(Mathf.Lerp(30f, 6f, c.Happy));
            Text(game, o, "Me serve na cosa.", "I need something.");
        }

        Order NewOrder(Game game, string name, Item item, int amount, Vector2Int cell, string place)
        {
            long now = Now(game);
            // The window opens on the next half hour or so, one to three hours out.
            long from = now + (long)((1f + game.Rng.Next(0, 5) * 0.5f) * GameClock.TicksPerHour);
            var o = new Order
            {
                Id = s.NextId++, Name = name, Item = (int)item, Amount = amount, Pay = amount * UnitPay(game, item),
                X = cell.x, Y = cell.y, Place = place, From = from, Until = from + (long)(WindowHours * GameClock.TicksPerHour),
            };
            s.Orders.Add(o);
            return o;
        }

        void Text(Game game, Order o, string nap, string en)
        {
            var m = game.Phone.Send(game, MsgKind.Order, o.Name, nap, en,
                $"{What(o)} · €{o.Pay} · {o.Place} · {Clock(game, o.From)}–{Clock(game, o.Until)}", AnswerHours);
            m.Choices.Add("Accept");
            m.Choices.Add("Ignore");
            m.Criminal = true;
            m.Request = o.Id;
            o.Msg = m.Id;
            if (DevAuto) game.Phone.Answer(m, 0, game);
        }

        static PhoneMessage FindMsg(Game game, int id)
        {
            foreach (var m in game.Phone.Messages) if (m.Id == id) return m;
            return null;
        }

        /// <summary>Their door, the nearest bar, or a corner near home.</summary>
        static (Vector2Int, string) PlaceFor(Game game, Vector2Int home, int kind)
        {
            var map = game.Map;
            if (kind == 1)
            {
                Business bar = null;
                float bd = 45f * 45f;
                foreach (var b in game.Org.Businesses)
                {
                    if (b.Kind != ShopKind.Bar) continue;
                    float d = (b.Cell - home).sqrMagnitude;
                    if (d < bd) { bd = d; bar = b; }
                }
                var prop = bar != null ? game.Properties.At(bar.Cell) : null;
                var outside = prop != null ? Outside(map, prop.Door) : null;
                if (outside.HasValue) return (outside.Value, $"outside {bar.Name}");
            }
            if (kind == 2)
            {
                Vector2Int? best = null;
                float bd = float.MaxValue;
                for (int dx = -14; dx <= 14; dx++)
                    for (int dy = -14; dy <= 14; dy++)
                    {
                        var c = home + new Vector2Int(dx, dy);
                        if (!map.InBounds(c) || !map.IsPassable(c)) continue;
                        var t = map.GetTerrain(c.x, c.y);
                        if (t != TerrainType.Sidewalk && t != TerrainType.Paving) continue;
                        float d = Mathf.Abs(dx * dx + dy * dy - 64f);   // a short walk, not the doorstep
                        if (d < bd && Organization.IsCorner(map, c)) { bd = d; best = c; }
                    }
                if (best.HasValue) return (best.Value, $"the corner in {game.Territory.ZoneAt(best.Value)?.Name ?? "the rione"}");
            }
            return (home, "at their door");
        }

        static Vector2Int? Outside(Map map, Vector2Int door)
        {
            foreach (var n in new[] { door + Vector2Int.down, door + Vector2Int.up, door + Vector2Int.left, door + Vector2Int.right })
                if (map.InBounds(n) && map.IsPassable(n) && map.GetTerrain(n.x, n.y) != TerrainType.Floor && map.GetBuilding(n.x, n.y) == null) return n;
            return null;
        }

        // ---------- Bait ----------

        /// <summary>A new number, generous and in a hurry: a plainclothes officer, or a clan that wants you caught.</summary>
        bool TryBait(Game game)
        {
            var org = game.Org;
            var home = Vector2Int.RoundToInt(game.PlayerBase.center);
            float heat = game.Territory.ZoneAt(home)?.Heat ?? 0f;
            float cop = 0.04f + heat / 500f + org.Evidence / 400f;
            RivalClan clan = null;
            foreach (var c in game.Clans) if (!c.Destroyed && c.Hostility > 30f && (clan == null || c.Hostility > clan.Hostility)) clan = c;
            float rival = clan != null ? 0.03f + clan.Hostility / 1500f : 0f;
            double r = game.Rng.NextDouble();
            int bait = r < cop ? 1 : r < cop + rival ? 2 : 0;
            if (bait == 0 || s.Customers.Count == 0) return false;
            var item = Available(game, Item.Doses);
            if (item == Item.None) return false;
            var near = s.Customers[game.Rng.Next(s.Customers.Count)];
            var (cell, place) = PlaceFor(game, near.Home, 2);
            string name = $"A new number (says {ShortName(near.Name)} sent him)";
            var o = NewOrder(game, name, item, AmountOf(game, item, 1f) + 2, cell, place);
            o.Pay = Mathf.RoundToInt(o.Pay * 1.4f);
            o.Bait = bait;
            o.Clan = bait == 2 ? game.Clans.IndexOf(clan) : -1;
            Text(game, o, "M'hanno dato 'o nummero.", "Somebody gave me your number. Can you come now?");
            return true;
        }

        static string ShortName(string n) => n.Split(' ')[0] == "Donna" || n.Split(' ')[0] == "Don" || n.Split(' ')[0] == "Zì" ? n : n.Split(' ')[0];

        /// <summary>A lookout on his post, or a neighbour who owes you, may know that number.</summary>
        void MaybeWarn(Game game, Order o)
        {
            string from = null;
            float chance = 0.12f;
            foreach (var p in game.Pawns.Crew)
                if (p.IsPresent && p.Role == CrewRole.Lookout) { chance += 0.35f; from = p.DisplayName; break; }
            foreach (var n in game.Favours.S.Neighbours)
                if (n.Owes > 0) { chance += 0.25f; from ??= n.Name; break; }
            if (game.Rng.NextDouble() > chance) return;
            from ??= "A friend";
            if (o.Bait == 1) game.Phone.Send(game, MsgKind.Info, from, "Chillo è nu sbirro.", "That new number is a cop. Don't go.", $"About the order {o.Place}.");
            else game.Phone.Send(game, MsgKind.Info, from, "È na trappola.", "That new number is a trap: the clan's boys are watching.", $"About the order {o.Place}.");
        }

        // ---------- Answers ----------

        public void OnAnswer(PhoneMessage m, int choice, Game game)
        {
            var o = s.Orders.Find(x => x.Id == m.Request);
            if (o == null || o.State != 0) return;
            if (choice != 0) { o.State = 3; return; }
            o.State = 1;
            game.Log.Add($"Order accepted: {What(o)} for {o.Name}, {o.Place}, {Clock(game, o.From)}–{Clock(game, o.Until)}. It's marked on the map.", MessageKind.Info, o.Cell);
            if (o.Bait != 0) MaybeWarn(game, o);
        }

        // ---------- Every tick ----------

        public void Tick(Game game)
        {
            if (s.Orders.Count == 0) return;
            long now = Now(game);
            bool home = game.District.Id == District.HomeId;
            foreach (var o in s.Orders)
            {
                waiting.TryGetValue(o.Id, out var npc);
                if (npc != null && npc.Despawned) { waiting.Remove(o.Id); npc = null; }
                if (o.State != 1)
                {
                    if (npc != null && npc.Chasing == null && npc.State != 9) { npc.Despawned = true; waiting.Remove(o.Id); }
                    continue;
                }
                if (now < o.From) continue;
                if (now > o.Until + (long)(GraceHours * GameClock.TicksPerHour))
                {
                    Fail(game, o, "Aggio aspettato n'ora!", "I waited an hour. Forget it.");
                    continue;
                }
                if (npc == null && home)
                {
                    npc = SpawnAt(game, o);
                    waiting[o.Id] = npc;
                }
                if (DevAuto && npc != null) HandOver(game, o);
            }
        }

        Npc SpawnAt(Game game, Order o)
        {
            var c = ById(o.Customer);
            int seed = (o.Name ?? "").GetHashCode() ^ game.Seed;
            var npc = game.Npcs.Spawn(NpcKind.Civilian, o.Cell, 0.05f, new Color32(150, 140, 120, 255));
            npc.IsFixture = true;
            npc.ResidentName = c?.Name ?? o.Name;   // keeps StreetLife from walking them off
            bool woman = c != null && Favours.IsWoman(c.Name);
            npc.Dress = woman ? Look.Woman(seed) : Look.Young(seed);
            npc.DressSeed = seed;
            npc.State = StreetLife.StandingState;
            npc.Exit = o.Cell;
            return npc;
        }

        void Fail(Game game, Order o, string nap, string en)
        {
            o.State = 3;
            if (o.Bait != 0) return;   // they were never going to buy
            var c = ById(o.Customer);
            game.Org.Respect = Mathf.Max(0f, game.Org.Respect - 0.5f);
            game.Phone.Send(game, MsgKind.Info, o.Name, nap, en, $"The order {o.Place}.");
            if (c == null) return;
            c.Happy = Mathf.Max(0f, c.Happy - 0.25f);
            c.Late++;
            CheckGone(game, c);
        }

        void CheckGone(Game game, Customer c)
        {
            if (c.Happy >= 0.15f || c.Gone) return;
            c.Gone = true;
            game.Phone.Send(game, MsgKind.Info, c.Name, "Nun me chiammà cchiù.", "Don't call me any more. I'll find someone else.", null);
        }

        // ---------- The hand-over ----------

        /// <summary>The order whose customer is standing in front of the player, if any.</summary>
        public Order InReach(Game game)
        {
            var p = game.Player;
            if (p == null) return null;
            foreach (var o in s.Orders)
            {
                if (o.State != 1 || !waiting.TryGetValue(o.Id, out var npc) || npc.Despawned) continue;
                if ((npc.Mover.Position - p.Position).sqrMagnitude <= HandReach * HandReach) return o;
            }
            return null;
        }

        /// <summary>What the player can hand over: the bag, and the seat of the scooter he's on or next to.</summary>
        static Inventory SeatFor(Game game, Pawn p)
        {
            if (p.Riding && p.HasFamilyScooter) return p.Seat;
            return FamilyScooters.Nearest(game, p.Position, FamilyScooters.PlayerReach, out var at) ? game.Seats.At(game, at, false) : null;
        }

        public int CanGive(Game game, Order o)
        {
            var p = game.Player;
            if (p == null) return 0;
            var item = (Item)o.Item;
            return Bag.For(p).Count(item) + (SeatFor(game, p)?.Count(item) ?? 0);
        }

        public string Label(Game game, Order o)
        {
            int have = CanGive(game, o);
            return have >= o.Amount ? $"Hand over {What(o)} · €{o.Pay}" : have > 0 ? $"Hand over {have}/{o.Amount} (short)" : $"{o.Name} wants {What(o)}: you have none on you";
        }

        public void HandOver(Game game, Order o)
        {
            var p = game.Player;
            if (p == null || o.State != 1) return;
            var item = (Item)o.Item;
            waiting.TryGetValue(o.Id, out var npc);
            int have = CanGive(game, o);
            if (have <= 0 && !DevAuto) { game.Log.Add($"{o.Name} wants {What(o)}. You've none in the bag or under the seat.", MessageKind.Bad); return; }

            if (o.Bait == 1)
            {
                o.State = 3;
                game.Log.Add("He shows you a badge instead of the money. Plainclothes police.", MessageKind.Bad, p.Cell);
                game.Pursuit.RedHanded = true;
                game.Pursuit.Start(npc, "It was a set-up: a plainclothes officer.", game);
                JoinPatrol(game);
                return;
            }

            int give = Mathf.Min(have, o.Amount);
            int taken = Bag.Take(p, item, give);
            var seat = SeatFor(game, p);
            if (taken < give && seat != null) taken += seat.Remove(item, give - taken);
            if (DevAuto && taken == 0) taken = give = o.Amount;   // the soak doesn't carry stock around
            if (item == Item.Doses) game.Territory.AddHeat(o.Cell, Organization.HeatPerSale * taken);

            if (o.Bait == 2)
            {
                o.State = 3;
                var clan = o.Clan >= 0 && o.Clan < game.Clans.Count ? game.Clans[o.Clan] : game.Rival;
                clan.Hostility = Mathf.Min(100f, clan.Hostility + 4f);
                game.Log.Add($"He takes the goods and walks off without paying. The {clan.Name}'s boys are watching from a scooter: it was a set-up.", MessageKind.Bad, p.Cell);
                // They told the police where to be.
                if (!JoinPatrol(game)) game.Territory.AddHeat(o.Cell, 10f);
                return;
            }

            var c = ById(o.Customer);
            bool late = Now(game) > o.Until;
            float share = give / (float)o.Amount;
            int pay = Mathf.RoundToInt(o.Pay * share * (late ? 0.7f : 1f));
            game.Stock.Cash += pay;
            game.PopupAt(p.Position, $"+€{pay}");
            o.State = 2;
            game.Org.Respect = Mathf.Min(100f, game.Org.Respect + (late ? 0.1f : 0.3f));
            p.Learn(Skill.Awareness, 2f, game);
            string how = give < o.Amount ? $" Only {give} of {o.Amount}: they pay for what they got." : late ? " Late: they knock something off." : "";
            game.Log.Add($"Delivered {Noun(item, give)} to {o.Name}, {o.Place}: €{pay}.{how}", MessageKind.Good, p.Cell);
            if (c != null)
            {
                float quality = item == Item.Doses ? game.Stock.DoseQuality : item == Item.Packs ? game.Stock.PackQuality : 0.75f;
                float d = late ? -0.12f : 0.08f;
                d += (quality - 0.7f) * 0.4f;
                d -= (1f - share) * 0.3f;
                c.Happy = Mathf.Clamp01(c.Happy + d);
                c.Orders++;
                if (late) c.Late++;
                CheckGone(game, c);
                if (!c.Gone && c.Happy > 0.75f && game.Rng.NextDouble() < 0.25) Refer(game, c);
            }

            // Handing it over in front of a policeman.
            var cop = Pursuit.WitnessCop(game, 6f);
            if (cop != null && !game.Pursuit.Active && game.Rng.NextDouble() < 0.6)
            {
                game.Pursuit.RedHanded = true;
                game.Pursuit.Start(cop, "A policeman saw you hand something over.", game);
            }
        }

        /// <summary>A patrol close by comes running. False if none was near.</summary>
        static bool JoinPatrol(Game game)
        {
            var p = game.Player;
            foreach (var n in game.Npcs.All)
            {
                if (n.Kind != NpcKind.Police || n.Despawned || n.HasArrested || n.State == 9) continue;
                if ((n.Mover.Position - p.Position).sqrMagnitude > 16f * 16f) continue;
                game.Pursuit.RedHanded = true;
                game.Pursuit.Start(n, "A patrol was waiting round the corner.", game);
                return true;
            }
            return false;
        }

        /// <summary>A happy regular passes the number on.</summary>
        void Refer(Game game, Customer c)
        {
            if (s.Customers.Count >= 14) return;
            var used = new HashSet<string>();
            foreach (var x in s.Customers) used.Add(x.Name);
            foreach (var n in game.Favours.S.Neighbours) used.Add(n.Name);
            var free = new List<string>();
            foreach (var f in Friends) if (!used.Contains(f)) free.Add(f);
            if (free.Count == 0) return;
            var home = HomeNear(game, c.Home);
            if (!home.HasValue) return;
            var friend = new Customer
            {
                Id = s.NextId++, Name = free[game.Rng.Next(free.Count)], HomeX = home.Value.x, HomeY = home.Value.y,
                Likes = (int)RandomLike(game), Happy = 0.55f, NextHour = HourAbs(game) + game.Rng.Next(3, 12),
            };
            s.Customers.Add(friend);
            game.Phone.Send(game, MsgKind.Info, c.Name, "Nu cumpà mio te chiamma.", $"A friend of mine, {friend.Name}, will call you. I gave him the number.", "A new customer.");
        }

        static Vector2Int? HomeNear(Game game, Vector2Int near)
        {
            Vector2Int? best = null;
            float bd = float.MaxValue;
            foreach (var pr in game.Properties.All)
            {
                if (pr.Kind != PropertyKind.Home || pr.OwnedByPlayer) continue;
                float d = (pr.Door - near).magnitude;
                if (d < 6f || d > 40f) continue;
                d += game.Rng.Next(0, 15);
                var o = Outside(game.Map, pr.Door);
                if (o.HasValue && d < bd) { bd = d; best = o; }
            }
            return best;
        }

        // ---------- Runners (stub) ----------

        // A crew member could take accepted orders for the boss: a runner with no piazza of his own walks the goods
        // from the stash to the meeting place and hands them over (paid as above, lower risk for the boss, his own
        // risk of a search). Not wired yet: needs a delivery job in AI/JobGiver alongside the cavallino's.
        public Pawn RunnerFor(Game game) => null;
    }
}
