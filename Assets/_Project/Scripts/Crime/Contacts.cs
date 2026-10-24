using System.Collections.Generic;
using OSistema.AI;
using OSistema.Core;
using OSistema.Npcs;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>Someone in the rione who hands out work, in person, from their usual spot.</summary>
    public sealed class Contact
    {
        public string Name;
        public string Who;              // a line about them, for the notebook
        public string Greeting, GreetingEn;
        public string NoWork, NoWorkEn;
        public Vector2Int Spot;
        public int OpenHour, CloseHour;
        public Color32 Color;
        public Npc Npc;
        public bool Secret;             // only shows up once someone has vouched for you
        public int Face = -1;           // portrait

        // A life: a home they walk from in the morning and back to at night, and a break in the middle of a long day.
        public Vector2Int? Home;
        public Vector2Int? BreakSpot;   // the bar or pizzeria where they eat; null: they go home
        public int BreakHour = -1;      // the hour of the break, or -1 for none
        public Walker Walk;             // where they're walking right now (null: standing)
        public string Doing = "at work";

        public bool OpenAt(int hour) => OpenHour < CloseHour ? hour >= OpenHour && hour < CloseHour : hour >= OpenHour || hour < CloseHour;
        public string Hours => $"{OpenHour:00}:00–{CloseHour:00}:00";
        public Vector2 Position => Npc != null && !Npc.Despawned ? Npc.Mover.Position : Mover.CellCenter(Spot);
        public bool Present => Npc != null && !Npc.Despawned;
    }

    /// <summary>
    /// The people who give a guaglione his first work. Their spots come from the map, so they don't need
    /// saving: the same city always has the same Zio Totonno outside the same bar.
    /// </summary>
    public sealed class ContactBook
    {
        public const string Totonno = "'O Zio Totonno";
        public const string Peppe = "Don Peppe 'o Strozzino";
        public const string Nanninella = "Nanninella 'a Ricettatrice";
        public const string Marenaro = "'O Marenaro";
        public const string Saverio = "Don Saverio 'o Malommo";
        public const string Supplier = "Zì Tore 'o Grossista";

        public readonly List<Contact> All = new List<Contact>();

        public Contact ByName(string name) => All.Find(c => c.Name == name);

        public bool Visible(Contact c, Game game) =>
            (!c.Secret || game.Org.SupplierIntroduced) && !(c.Name == Saverio && game.Org.SaverioGone);

        static readonly System.Collections.Generic.Dictionary<string, int> Faces = new System.Collections.Generic.Dictionary<string, int>
        {
            [Totonno] = 11, [Saverio] = 3, [Peppe] = 15, [Marenaro] = 12, [Supplier] = 2, [Nanninella] = Pawns.Look.Women + 4,
        };

        public void Setup(Game game)
        {
            All.Clear();
            var map = game.Map;
            var home = Vector2Int.RoundToInt(game.PlayerBase.center);
            var used = new HashSet<Vector2Int>();

            Vector2Int? ShopFront(ShopKind kind, float min, float max)
            {
                Vector2Int? best = null;
                float bestD = float.MaxValue;
                foreach (var def in BuildingDefs.All)
                {
                    if (def.Shop != kind) continue;
                    foreach (var counter in map.PositionsOf(def))
                    {
                        var prop = game.Properties.At(counter);
                        if (prop == null) continue;
                        var spot = Outside(map, prop.Door);
                        if (!spot.HasValue || used.Contains(spot.Value)) continue;
                        float d = (spot.Value - home).magnitude;
                        if (d < min || d > max || d >= bestD) continue;
                        bestD = d;
                        best = spot;
                    }
                }
                return best;
            }

            Vector2Int StreetSpot(float min, float max, int salt)
            {
                var rng = new System.Random(game.Seed + salt);
                for (int i = 0; i < 400; i++)
                {
                    var c = new Vector2Int(rng.Next(map.Width), rng.Next(map.Height));
                    float d = (c - home).magnitude;
                    if (d < min || d > max || used.Contains(c) || !map.IsPassable(c) || map.GetBuilding(c.x, c.y) != null) continue;
                    var t = map.GetTerrain(c.x, c.y);
                    if (t == TerrainType.Sidewalk || t == TerrainType.Paving) return c;
                }
                return home;
            }

            // Don Saverio holds the corner of the family's own rione: a crossing a short walk from the basso.
            Vector2Int? SaverioCorner()
            {
                var zone = game.Territory.ZoneAt(home);
                if (zone == null) return null;
                Vector2Int? best = null;
                float bestScore = float.MaxValue;
                var rng = new System.Random(game.Seed + 29);
                foreach (var c in GridUtil.Cells(zone.Rect, outlineOnly: false))
                {
                    if (used.Contains(c) || !map.IsPassable(c) || map.GetBuilding(c.x, c.y) != null) continue;
                    var t = map.GetTerrain(c.x, c.y);
                    if (t != TerrainType.Sidewalk && t != TerrainType.Paving) continue;
                    float d = (c - home).magnitude;
                    if (d < 6f || d > 22f) continue;
                    // A real crossing first; then the nearer, with a little chance in it.
                    float score = (Crime.Organization.IsCorner(map, c) ? 0f : 40f) + Mathf.Abs(d - 12f) + (float)rng.NextDouble() * 4f;
                    if (score < bestScore) { bestScore = score; best = c; }
                }
                return best;
            }

            // Their front door: a house a short walk from where they work.
            Vector2Int? HomeNear(Vector2Int spot)
            {
                Vector2Int? best = null;
                float bestD = float.MaxValue;
                foreach (var pr in game.Properties.All)
                {
                    if (pr.Kind != PropertyKind.Home || pr.OwnedByPlayer) continue;
                    float d = (pr.Door - spot).magnitude;
                    if (d < 8f || d > 40f) continue;
                    d += (float)((pr.Id * 7919) % 13);   // not always the very nearest
                    var outside = Outside(map, pr.Door);
                    if (outside.HasValue && d < bestD) { bestD = d; best = outside; }
                }
                return best;
            }

            // Where they eat on a break: the nearest bar or pizzeria front.
            Vector2Int? EatNear(Vector2Int spot)
            {
                Vector2Int? best = null;
                float bestD = 45f;
                foreach (var def in BuildingDefs.All)
                {
                    if (def.Shop != ShopKind.Bar && def.Shop != ShopKind.Pizzeria) continue;
                    foreach (var counter in map.PositionsOf(def))
                    {
                        var prop = game.Properties.At(counter);
                        var front = prop != null ? Outside(map, prop.Door) : null;
                        if (!front.HasValue) continue;
                        float d = (front.Value - spot).magnitude;
                        if (d > 4f && d < bestD) { bestD = d; best = front; }
                    }
                }
                return best;
            }

            Contact Add(string name, string who, Vector2Int spot, int open, int close, Color32 color,
                string greet, string greetEn, string none, string noneEn)
            {
                used.Add(spot);
                var c = new Contact
                {
                    Name = name, Who = who, Spot = spot, OpenHour = open, CloseHour = close, Color = color,
                    Greeting = greet, GreetingEn = greetEn, NoWork = none, NoWorkEn = noneEn,
                };
                c.Face = Faces.TryGetValue(name, out int f) ? f : -1;
                c.Home = HomeNear(spot);
                // A long day has a break: lunch at home (the riposo) for day people, dinner out for night people.
                int span = (close - open + 24) % 24;
                if (span >= 9)
                {
                    c.BreakHour = open < 12 ? 13 : 20;
                    c.BreakSpot = open < 12 ? null : EatNear(spot);
                }
                All.Add(c);
                return c;
            }

            // At the port, only the people who work the docks.
            if (game.District.Id == Core.District.PortId)
            {
                Add(Marenaro, "Has a boat, and friends on other boats. Works when the sea is dark.",
                    Landing(map) ?? StreetSpot(10, 120, 53), 18, 4, new Color32(60, 80, 100, 255),
                    "'O mare stanotte è calmo. Buono pe' fatica'.", "The sea's calm tonight. Good for work.",
                    "Stanotte nun esce nisciuna varca.", "No boats going out tonight.");
                return;
            }
            Add(Totonno, "Runs the bar on the corner. Knows everyone, owes nobody. Always has an errand.",
                ShopFront(ShopKind.Bar, 3, 60) ?? StreetSpot(6, 25, 11), 7, 22, new Color32(120, 90, 70, 255),
                "Guagliò, viene ccà. Tengo 'na cosa pe' te.", "Come here, kid. I've got something for you.",
                "Pe' mo' niente. Pigliate 'nu caffè e passa cchiù tarde.", "Nothing for now. Have a coffee and come back later.");
            Add(Saverio, "Runs the corner for the people above him. Pays lookouts by the night, and watches who's reliable.",
                SaverioCorner() ?? StreetSpot(8, 30, 23), 15, 2, new Color32(40, 40, 46, 255),
                "Tu si 'o figlio d''a signora Iaccarino? Sient'a me.", "You're Mrs Iaccarino's son? Listen to me.",
                "Mo' nun me serve nisciuno. Torna 'o pomeriggio, verso 'e tre.", "I don't need anyone right now. Come back in the afternoon, around three.");
            Add(Peppe, "Lends money at rates the banks would blush at. Needs young legs to collect.",
                ShopFront(ShopKind.Tabaccheria, 6, 70) ?? ShopFront(ShopKind.Salumeria, 6, 70) ?? StreetSpot(10, 40, 37), 9, 20, new Color32(90, 70, 110, 255),
                "'E sorde nun cadono d''o cielo, guagliò.", "Money doesn't fall from the sky, kid.",
                "Oggi pavano tutte quante. Miracolo.", "Everyone's paying today. A miracle.");
            Add(Nanninella, "Fences anything with wheels or a screen. Never asks where it came from.",
                StreetSpot(20, 55, 41), 10, 21, new Color32(150, 60, 70, 255),
                "Tiene 'e mmane leste? Allora parlammo.", "Quick hands? Then let's talk.",
                "Aggio chino 'o garage. Torna dimane.", "The garage is full. Come back tomorrow.");
            Add(Supplier, "Sells product by the kilo to people he's been told to trust. Meets at night, never twice in a row on the phone.",
                StreetSpot(35, 85, 67), 21, 3, new Color32(30, 30, 34, 255),
                "Tu si chillo ca m'ha mannato Saverio. Parlammo.", "You're the one Saverio sent me. Let's talk.",
                "Pe' mo' nun tengo niente. Torna 'n'ata sera.", "Nothing for now. Come back another night.").Secret = true;
            Add(Marenaro, "Has a boat, and friends on other boats. Works when the sea is dark.",
                Landing(map) ?? StreetSpot(30, 120, 53), 18, 4, new Color32(60, 80, 100, 255),
                "'O mare stanotte è calmo. Buono pe' fatica'.", "The sea's calm tonight. Good for work.",
                "Stanotte nun esce nisciuna varca.", "No boats going out tonight.");
        }

        static Vector2Int? Outside(Map map, Vector2Int door)
        {
            foreach (var n in new[] { door + Vector2Int.down, door + Vector2Int.up, door + Vector2Int.left, door + Vector2Int.right })
                if (map.InBounds(n) && map.IsPassable(n) && map.GetTerrain(n.x, n.y) != TerrainType.Floor && map.GetBuilding(n.x, n.y) == null) return n;
            return null;
        }

        static Vector2Int? Landing(Map map)
        {
            for (int x = map.Width / 2; x < map.Width - 1; x++)
            for (int y = 1; y < 20; y++)
                if (map.GetTerrain(x, y - 1) == TerrainType.Water && map.GetTerrain(x, y) == TerrainType.Paving && map.GetBuilding(x, y) == null)
                    return new Vector2Int(x, y + 1);
            return null;
        }

        /// <summary>
        /// Contacts live: they walk from home to their spot when they open, take a break in a long day (home for
        /// lunch, or out to eat), and walk home when they close. Talk to them wherever they are.
        /// </summary>
        public void Tick(Game game)
        {
            // Walking, every tick.
            foreach (var c in All)
            {
                if (!c.Present || c.Walk == null) continue;
                var status = c.Walk.Tick(c.Npc.Mover, 1f, game);
                if (status == WalkStatus.Walking) continue;
                c.Walk = null;
                if (c.Doing == "going home")
                {
                    c.Npc.Despawned = true;   // in at the front door
                    c.Npc = null;
                }
                else if (c.Doing == "going to work" || c.Doing == "back to work") c.Doing = "at work";
                else if (c.Doing == "going for a break") c.Doing = c.BreakSpot.HasValue ? "eating out" : "at home for lunch";
            }

            if (game.Ticks.TicksGame % 60 != 0) return;
            int hour = game.Clock.Hour;
            foreach (var c in All)
            {
                bool open = c.OpenAt(hour) && Visible(c, game);
                bool onBreak = open && hour == c.BreakHour;
                if (!open && !c.Present) c.Doing = "at home";
                if (open && !c.Present)
                {
                    // Out of the front door (or straight at the spot if they have none), and off to work.
                    var from = c.Home ?? c.Spot;
                    if (onBreak && !c.BreakSpot.HasValue) continue;   // still at home for lunch
                    c.Npc = game.Npcs.Spawn(NpcKind.Civilian, from, 0.05f, c.Color);
                    c.Npc.IsFixture = true;
                    c.Npc.State = StreetLife.StandingState;   // their own legs, not a passer-by's errands
                    c.Npc.Target = c.Spot;
                    c.Npc.ContactName = c.Name;
                    if (from != c.Spot) { c.Walk = new Walker(c.Spot, adjacent: false); c.Doing = c.Doing == "at home for lunch" ? "back to work" : "going to work"; }
                    else c.Doing = "at work";
                }
                else if (!open && c.Present && c.Doing != "going home")
                {
                    if (c.Home.HasValue && game.Map.InBounds(c.Home.Value)) { c.Walk = new Walker(c.Home.Value, adjacent: false); c.Doing = "going home"; }
                    else { c.Npc.Despawned = true; c.Npc = null; }
                }
                else if (c.Present && onBreak && c.Doing == "at work")
                {
                    var to = c.BreakSpot ?? c.Home;
                    if (!to.HasValue) continue;
                    c.Walk = new Walker(to.Value, adjacent: false);
                    c.Doing = "going for a break";
                    if (!c.BreakSpot.HasValue) c.Doing = "going home for lunch";
                }
                else if (c.Present && !onBreak && open && (c.Doing == "eating out" || c.Doing == "going for a break"))
                {
                    c.Walk = new Walker(c.Spot, adjacent: false);
                    c.Doing = "back to work";
                }
                if (c.Present && c.Doing == "going home for lunch" && c.Walk == null)
                {
                    c.Npc.Despawned = true;   // indoors for the riposo; back out at the end of the hour
                    c.Npc = null;
                    c.Doing = "at home for lunch";
                }
            }
        }

        public Contact At(Vector2 world)
        {
            Contact best = null;
            float bestD = 0.8f * 0.8f;
            foreach (var c in All)
            {
                if (!c.Present) continue;
                float d = (c.Position - world).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = c;
                }
            }
            return best;
        }

        public bool HasWork(Contact c, Game game)
        {
            foreach (var o in game.Contracts.Offers) if (o.Giver == c.Name) return true;
            return false;
        }
    }
}
