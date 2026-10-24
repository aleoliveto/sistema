using System.Collections.Generic;
using OSistema.AI;
using OSistema.Core;
using OSistema.Pawns;
using OSistema.Social;
using OSistema.World;
using UnityEngine;

namespace OSistema.Npcs
{
    /// <summary>One of the named neighbours (see <see cref="Favours"/>), with a front door and a day.</summary>
    public sealed class Resident
    {
        public Neighbour Who;
        public Vector2Int Home;         // outside their front door
        public Vector2Int Work;         // the shop, stall or bottega where they spend the day (the bar, for a pensioner)
        public Vector2Int? Bar;         // the morning coffee
        public bool Pensioner;
        public float Offset;            // 0..1: nobody keeps quite the same hours as the next person
        public Look Dress;
        public int Seed;
        public Color32 Color;

        public Npc Npc;
        public Walker Walk;             // where they're walking right now (null: standing)
        public string Going = "home";   // home, coffee, work, stroll, crowd
        public string Doing = "at home";
        public int Linger;              // ticks left before a stroller moves on, or a watcher leaves the crowd
        public long ShakenUntil;        // ran home from gunfire: stays in for a while
        public int Crowd;               // the gathering they last stood in

        public bool Present => Npc != null && !Npc.Despawned;
        public Vector2 Position => Present ? Npc.Mover.Position : Mover.CellCenter(Home);
    }

    /// <summary>
    /// The neighbours live in the rione: out of the same front door every morning, a coffee at the bar,
    /// the morning at their shop or stall (or at the bar with the other old men), home for lunch and the
    /// riposo, back out in the late afternoon, then the evening passeggiata round the piazze before bed.
    /// Sundays are for strolling; when Napoli play they're indoors at the television. They run home from
    /// gunfire and stay in for a while, and stop to look when something happens. Built from the saved
    /// neighbours and the map, so they need no saving of their own. Only in the family's own district.
    /// </summary>
    public sealed class Residents
    {
        public readonly List<Resident> All = new List<Resident>();
        FavourState builtFor;
        int builtCount = -1;

        public Resident Of(Neighbour n) => All.Find(r => r.Who == n);

        // ---------- Setup ----------

        void Build(Game game)
        {
            foreach (var r in All) if (r.Present) r.Npc.Despawned = true;
            All.Clear();
            builtFor = game.Favours.S;
            builtCount = builtFor.Neighbours.Count;
            var map = game.Map;
            var used = new HashSet<Vector2Int>();
            foreach (var n in builtFor.Neighbours)
            {
                var rect = n.Zone >= 0 && n.Zone < game.Territory.Zones.Count ? game.Territory.Zones[n.Zone].Rect : new RectInt(0, 0, map.Width, map.Height);
                var home = HomeIn(game, rect, n.Id, used);
                if (!home.HasValue) continue;
                used.Add(home.Value);
                int seed = n.Name.GetHashCode() ^ (game.Seed * 31);
                var rng = new System.Random(seed);
                var r = new Resident
                {
                    Who = n, Home = home.Value, Seed = seed,
                    Pensioner = n.Trade.Contains("pensioner"),
                    Offset = (float)rng.NextDouble(),
                    Color = (Color32)Color.HSVToRGB((float)rng.NextDouble(), 0.25f + (float)rng.NextDouble() * 0.4f, 0.45f + (float)rng.NextDouble() * 0.45f),
                };
                bool woman = Favours.IsWoman(n.Name);
                bool old = r.Pensioner || n.Name.StartsWith("Zì") || n.Name.StartsWith("Don ") || n.Name.StartsWith("Mast'");
                r.Dress = woman ? Look.Woman(seed, old) : old ? Look.Old(seed) : Look.Young(seed);
                r.Bar = ShopSpot(game, ShopKind.Bar, home.Value, inside: false);
                r.Work = WorkFor(game, r) ?? r.Bar ?? home.Value;
                All.Add(r);
            }
        }

        /// <summary>A house in their rione, outside its door: the same one for the same neighbour.</summary>
        static Vector2Int? HomeIn(Game game, RectInt rect, int id, HashSet<Vector2Int> used)
        {
            Vector2Int? best = null;
            float bestScore = float.MaxValue;
            var centre = rect.center;
            foreach (var pr in game.Properties.All)
            {
                if (pr.Kind != PropertyKind.Home || pr.OwnedByPlayer) continue;
                var outside = Outside(game.Map, pr.Door);
                if (!outside.HasValue || used.Contains(outside.Value)) continue;
                float score = (pr.Door - centre).magnitude * 0.3f + (rect.Contains(pr.Door) ? 0f : 200f) + (pr.Id * 7919 + id * 104729) % 37;
                if (score < bestScore) { bestScore = score; best = outside; }
            }
            return best;
        }

        /// <summary>Where their trade keeps them: their own kind of shop, a market stall, a bottega near home.</summary>
        static Vector2Int? WorkFor(Game game, Resident r)
        {
            string t = r.Who.Trade;
            if (t.Contains("tobacconist")) return ShopSpot(game, ShopKind.Tabaccheria, r.Home, inside: true);
            if (t.Contains("bar owner")) return ShopSpot(game, ShopKind.Bar, r.Home, inside: true);
            if (t.Contains("trattoria")) return ShopSpot(game, ShopKind.Pizzeria, r.Home, inside: true);
            if (t.Contains("baker")) return ShopSpot(game, ShopKind.Salumeria, r.Home, inside: true);
            if (t.Contains("pensioner")) return r.Bar;
            if (t.Contains("porter")) return r.Home;   // the palazzo door is theirs to watch
            if (t.Contains("fish") || t.Contains("fruit") || t.Contains("florist")) return StallNear(game, r.Home) ?? BottegaNear(game, r);
            return BottegaNear(game, r);   // the barber, the seamstress, the mechanic: a ground-floor room a few doors down
        }

        static Vector2Int? ShopSpot(Game game, ShopKind kind, Vector2Int near, bool inside)
        {
            var map = game.Map;
            Crime.Business best = null;
            float bestD = 70f * 70f;
            foreach (var b in game.Org.Businesses)
            {
                if (b.Kind != kind) continue;
                float d = (b.Cell - near).sqrMagnitude;
                if (d < bestD) { bestD = d; best = b; }
            }
            if (best == null) return null;
            if (inside)
                foreach (var c in GridUtil.CellsAround(best.Cell, 3))
                    if (map.InBounds(c) && map.IsPassable(c) && map.GetBuilding(c.x, c.y) == null &&
                        map.GetTerrain(c.x, c.y) == TerrainType.Floor && (c - best.Cell).sqrMagnitude >= 2) return c;
            var prop = game.Properties.At(best.Cell);
            return prop != null ? Outside(map, prop.Door) : null;
        }

        static Vector2Int? StallNear(Game game, Vector2Int near)
        {
            var map = game.Map;
            Vector2Int? best = null;
            float bestD = 60f * 60f;
            foreach (var s in map.PositionsOf(BuildingDefs.MarketStall))
                foreach (var n in new[] { s + Vector2Int.up, s + Vector2Int.down })
                {
                    if (!map.InBounds(n) || !map.IsPassable(n) || map.GetBuilding(n.x, n.y) != null) continue;
                    float d = (n - near).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = n; }
                }
            return best;
        }

        static Vector2Int? BottegaNear(Game game, Resident r)
        {
            Vector2Int? best = null;
            float bestScore = float.MaxValue;
            foreach (var pr in game.Properties.All)
            {
                if (pr.OwnedByPlayer) continue;
                float d = (pr.Door - r.Home).magnitude;
                if (d < 4f || d > 25f) continue;
                var outside = Outside(game.Map, pr.Door);
                float score = d + (pr.Id * 31 + r.Who.Id * 7) % 11;
                if (outside.HasValue && score < bestScore) { bestScore = score; best = outside; }
            }
            return best;
        }

        static Vector2Int? Outside(Map map, Vector2Int door)
        {
            foreach (var n in new[] { door + Vector2Int.down, door + Vector2Int.up, door + Vector2Int.left, door + Vector2Int.right })
                if (map.InBounds(n) && map.IsPassable(n) && map.GetTerrain(n.x, n.y) != TerrainType.Floor && map.GetBuilding(n.x, n.y) == null) return n;
            return null;
        }

        // ---------- The day ----------

        /// <summary>Where they mean to be at this hour.</summary>
        static string PlanFor(Resident r, Game game)
        {
            if (game.Ticks.TicksGame < r.ShakenUntil) return "home";
            var cal = game.Calendar;
            if (cal.MatchOn(game)) return "home";                              // in front of the television
            if (cal.Celebrating(game) || cal.Fireworks(game)) return "stroll"; // everyone's out
            float h = game.Clock.HourF, o = r.Offset;
            bool sunday = game.Clock.IsPayday;
            float wake = 7f + o * 1.5f, lunch = 13f + o * 0.5f, riposoEnd = 16.5f + o, evening = 19f + o * 0.5f, bed = 21.5f + o * 1.5f;
            if (h < wake || h >= bed) return "home";
            if (h < wake + 0.6f && r.Bar.HasValue && !sunday && !r.Pensioner) return "coffee";
            if (h < lunch) return sunday ? "stroll" : "work";   // Sunday morning: out in their good clothes
            if (h < riposoEnd) return "home";
            if (h < evening) return sunday ? "stroll" : "work";
            return game.Weather.Rain > 0.6f ? "home" : "stroll";
        }

        static string AtWork(Resident r)
        {
            string t = r.Who.Trade;
            if (t.Contains("fruit")) return "at the fruit stall";
            if (t.Contains("barber")) return "cutting hair";
            if (t.Contains("tobacconist")) return "behind the tobacco counter";
            if (t.Contains("trattoria")) return "in the trattoria's kitchen";
            if (t.Contains("porter")) return "at the palazzo door";
            if (t.Contains("fish")) return "at the fish stall";
            if (t.Contains("seamstress")) return "sewing in the bottega";
            if (t.Contains("mechanic")) return "under a scooter";
            if (t.Contains("pensioner")) return "at the bar with the old men";
            if (t.Contains("baker")) return "at the bread counter";
            if (t.Contains("bar owner")) return "behind the bar";
            if (t.Contains("florist")) return "at the flower stall";
            return "at work";
        }

        static string AtHome(Game game)
        {
            float h = game.Clock.HourF;
            if (game.Calendar.MatchOn(game)) return "at home, watching Napoli";
            if (h >= 13f && h < 17.5f) return "at home for lunch and the riposo";
            if (h >= 21f || h < 6f) return "at home for the night";
            return "at home";
        }

        public void Tick(Game game)
        {
            if (game.District.Id != District.HomeId) return;
            if (builtFor != game.Favours.S || builtCount != game.Favours.S.Neighbours.Count) Build(game);
            long now = game.Ticks.TicksGame;
            var life = game.StreetLife;

            // Walking and standing, every tick.
            foreach (var r in All)
            {
                if (!r.Present) { r.Npc = null; r.Walk = null; continue; }
                var npc = r.Npc;
                if (!npc.Panicking && StreetLife.GunfireNear(npc, game))
                {
                    // Home, fast, and they won't come out again for a few hours.
                    npc.Panicking = true;
                    npc.FaceTo = null;
                    StreetLife.EndChat(npc);
                    r.ShakenUntil = now + GameClock.TicksPerHour * 3;
                    r.Going = "home";
                    r.Doing = "running home from the shooting";
                    r.Walk = new Walker(r.Home, adjacent: false, speedMultiplier: 1.9f);
                }
                if (r.Walk == null)
                {
                    if (r.Linger > 0) r.Linger--;
                    continue;
                }
                if (r.Walk.Tick(npc.Mover, 1f, game) == WalkStatus.Walking) continue;
                r.Walk = null;
                Arrived(r, game);
            }

            // Something to look at nearby.
            if (now % 30 == 0)
                foreach (var r in All)
                {
                    if (!r.Present || r.Npc.Panicking || r.Going == "crowd" || now < r.ShakenUntil) continue;
                    var g = life.GatheringNear(r.Npc.Mover.Position, game, r.Crowd);
                    if (g == null) continue;
                    StreetLife.EndChat(r.Npc);
                    r.Crowd = g.Id;
                    r.Going = "crowd";
                    r.Linger = 0;
                    r.Doing = $"going to see {g.Why}";
                    r.Walk = new Walker(life.RingSpot(g, r.Npc.Mover.Position, game), adjacent: false);
                }

            // The day's plan, once a game minute or so.
            if (now % 60 != 0) return;
            foreach (var r in All)
            {
                // Gone out to meet the family for a delivery (see Deliveries): one of them in the street at a time.
                if (game.Deliveries.Busy(r.Who.Id))
                {
                    if (r.Present) { StreetLife.EndChat(r.Npc); r.Npc.Despawned = true; r.Npc = null; r.Walk = null; }
                    r.Going = ""; r.Doing = "waiting for a delivery";
                    continue;
                }
                string plan = PlanFor(r, game);
                if (r.Going == "crowd" && (r.Walk != null || r.Linger > 0) && life.GatheringById(r.Crowd) != null) continue;
                if (!r.Present)
                {
                    if (plan == "home") { r.Going = "home"; if (now >= r.ShakenUntil) r.Doing = AtHome(game); continue; }
                    // Out of the front door.
                    var npc = game.Npcs.Spawn(NpcKind.Civilian, r.Home, r.Pensioner ? 0.042f : 0.055f, r.Color);
                    npc.IsFixture = true;   // not one of the passers-by
                    npc.ResidentName = r.Who.Name;
                    npc.Dress = r.Dress;
                    npc.DressSeed = r.Seed;
                    npc.State = StreetLife.StandingState;
                    r.Npc = npc;
                    r.Going = "";
                }
                if (plan == r.Going && (r.Walk != null || r.Linger > 0 || plan != "stroll")) continue;
                Go(r, plan, game);
            }
        }

        void Go(Resident r, string plan, Game game)
        {
            var npc = r.Npc;
            StreetLife.EndChat(npc);
            npc.FaceTo = null;
            r.Going = plan;
            r.Linger = 0;
            Vector2Int to;
            switch (plan)
            {
                case "home":
                    to = r.Home;
                    float h = game.Clock.HourF;
                    r.Doing = h >= 12.5f && h < 17.5f ? "going home for lunch" : "going home";
                    break;
                case "coffee":
                    to = r.Bar ?? r.Work;
                    r.Doing = "off for a coffee";
                    break;
                case "work":
                    to = r.Work;
                    r.Doing = r.Pensioner ? "off to the bar" : "off to work";
                    break;
                default:
                    to = game.StreetLife.StrollSpot(r.Home, game);
                    r.Doing = "out for the passeggiata";
                    break;
            }
            r.Walk = new Walker(to, adjacent: false);
        }

        void Arrived(Resident r, Game game)
        {
            var npc = r.Npc;
            switch (r.Going)
            {
                case "home":
                    npc.Despawned = true;   // in at the front door
                    r.Npc = null;
                    if (game.Ticks.TicksGame >= r.ShakenUntil) r.Doing = AtHome(game);
                    else r.Doing = "at home, shaken: shooting in the street";
                    return;
                case "crowd":
                {
                    var g = game.StreetLife.GatheringById(r.Crowd);
                    npc.FaceTo = g?.At;
                    r.Linger = game.Rng.Next(600, 1500);
                    r.Doing = g != null ? $"watching {g.Why}, with the crowd" : "having a look";
                    return;
                }
                case "coffee":
                    r.Doing = "having a coffee at the bar";
                    return;
                case "work":
                    r.Doing = AtWork(r);
                    return;
                default:
                    r.Linger = game.Rng.Next(300, 900);
                    r.Doing = "on the passeggiata";
                    // Another neighbour out strolling: they stop for a word.
                    foreach (var o in All)
                    {
                        if (o == r || !o.Present || o.Going != "stroll" || o.Walk != null || o.Npc.ChatWith != null) continue;
                        if ((o.Npc.Mover.Position - npc.Mover.Position).sqrMagnitude > 3f * 3f) continue;
                        npc.ChatWith = o.Npc;
                        o.Npc.ChatWith = npc;
                        o.Linger = Mathf.Max(o.Linger, r.Linger);
                        r.Doing = $"chatting with {o.Who.Name}";
                        o.Doing = $"chatting with {r.Who.Name}";
                        break;
                    }
                    return;
            }
        }
    }
}
