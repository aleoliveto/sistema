using OSistema.Core;
using OSistema.Crime;
using OSistema.World;

namespace OSistema.Story
{
    /// <summary>Buying supplies over the counter, in person: tobacco and papers, bags, groceries.</summary>
    public static class ShopCards
    {
        static void Buy(Game g, int price, string what, System.Action give)
        {
            if (!g.Org.Pay(price, g, out var error))
            {
                g.Log.Add(error, MessageKind.Bad);
                return;
            }
            give();
            g.Log.Add($"Bought {what} for €{price}.");
        }

        public sealed class Item
        {
            public string Name, Note;
            public int Price;
            public System.Action<Game> Give;
            public BuildingDef Def;   // furniture: shows how many are in storage
        }

        /// <summary>What's on the shelves, for the checkout.</summary>
        public static System.Collections.Generic.List<Item> Items(Business shop)
        {
            var list = new System.Collections.Generic.List<Item>();
            if (shop.Kind == ShopKind.Tabaccheria)
            {
                list.Add(new Item { Name = "Loose tobacco, 100 g", Note = "seven packs' worth", Price = Prices.TobaccoPer100g, Give = g => g.Stock.TobaccoG += 100 });
                list.Add(new Item { Name = "Papers and filters ×200", Note = "ten packs' worth", Price = Prices.Papers200, Give = g => g.Stock.Papers += 200 });
            }
            else if (shop.Kind == ShopKind.Mobili)
            {
                foreach (var (def, note) in Furniture)
                    list.Add(new Item { Name = def.Label, Note = note, Price = BuildingDef.CostOf(def), Def = def, Give = g => g.Stock.Store(def) });
            }
            else if (shop.Kind == ShopKind.Lusso)
            {
                list.Add(new Item { Name = "Gold chain", Note = "worn on the chest · +3 respect", Price = 450, Give = g => { if (g.Player != null) g.Player.Look.Neck = 0; g.Org.Respect = UnityEngine.Mathf.Min(100f, g.Org.Respect + 3f); } });
                list.Add(new Item { Name = "Gold watch", Note = "people notice the wrist · +6 respect", Price = 1400, Give = g => g.Org.Respect = UnityEngine.Mathf.Min(100f, g.Org.Respect + 6f) });
                list.Add(new Item { Name = "Earrings for Mammà", Note = "the crew sees you look after family · +5 loyalty", Price = 300, Give = g => { foreach (var c in g.Pawns.Crew) if (!c.Dead) c.Loyalty = UnityEngine.Mathf.Min(100f, c.Loyalty + 5f); } });
                list.Add(new Item { Name = "A gift for the parish", Note = "a silver candlestick · +2 respect", Price = 250, Give = g => g.Org.Respect = UnityEngine.Mathf.Min(100f, g.Org.Respect + 2f) });
            }
            else if (shop.Kind == ShopKind.Farmacia)
            {
                list.Add(new Item { Name = "Medicine", Note = "patches up a wound", Price = Prices.Medicine, Give = g => g.Stock.Medicine += 1 });
                list.Add(new Item { Name = "Small food bags ×100", Note = "the chemist sells them too", Price = Prices.Baggies100 + 2, Give = g => g.Stock.Baggies += 100 });
            }
            else if (shop.Kind == ShopKind.Macelleria)
            {
                list.Add(new Item { Name = "Meat and sausages", Note = "cooks three meals", Price = Prices.Groceries * 2, Give = g => g.Stock.Groceries += 3 });
            }
            else
            {
                list.Add(new Item { Name = "Small food bags ×100", Note = "for the doses", Price = Prices.Baggies100, Give = g => g.Stock.Baggies += 100 });
                list.Add(new Item { Name = "Groceries", Note = "each cooks two meals", Price = Prices.Groceries, Give = g => g.Stock.Groceries += 1 });
            }
            return list;
        }

        /// <summary>What the Mobilificio has on the floor.</summary>
        static readonly (BuildingDef, string)[] Furniture =
        {
            (BuildingDefs.Bed, "a bed of one's own"),
            (BuildingDefs.Table, "cover in a firefight, too"),
            (BuildingDefs.PlasticChair, "for the vicolo"),
            (BuildingDefs.Stove, "cook groceries into meals"),
            (BuildingDefs.Pantry, "meals and supplies"),
            (BuildingDefs.Fridge, "cold beer"),
            (BuildingDefs.KitchenCounter, "a proper kitchen"),
            (BuildingDefs.Sofa, "to collapse on"),
            (BuildingDefs.Armchair, "for the boss"),
            (BuildingDefs.Television, "Napoli on Sunday: morale"),
            (BuildingDefs.Wardrobe, "for the good suit"),
            (BuildingDefs.Rug, "makes a home nicer"),
            (BuildingDefs.Plant, "a bit of green"),
            (BuildingDefs.Safe, "where the money sleeps"),
            (BuildingDefs.HomeShrine, "the Madonna watches over"),
        };

        /// <summary>True when there's pizzo business to do with this shop in person.</summary>
        public static bool PizzoActionable(Business b, Game game)
        {
            if (b.Status == BusinessStatus.Owned || b.Status == BusinessStatus.Closed) return false;
            if (game.Org.PizzoBlocker(b, game) != null) return false;
            if (b.Status == BusinessStatus.Paying) return game.Org.IsDue(b, Organization.WeekOf(game.Clock));
            return true;
        }

        /// <summary>Adds the pizzo choices that fit this shop's situation to a card.</summary>
        public static GameEvent WithPizzo(GameEvent e, Business b, Game game)
        {
            var org = game.Org;
            var p = game.Player;
            if (p == null || b.Status == BusinessStatus.Owned || b.Status == BusinessStatus.Closed) return e;
            string blocker = org.PizzoBlocker(b, game);
            if (blocker != null) return e;
            int week = Organization.WeekOf(game.Clock);
            if (b.Status == BusinessStatus.Paying)
            {
                if (org.IsDue(b, week)) e.Option($"Collect this week's envelope · €{b.WeeklyPizzo}", "He's expecting you.", g => g.Org.CollectPizzoInPerson(b, g));
            }
            else if (b.Status == BusinessStatus.Refusing)
            {
                e.Option($"Ask him again · €{b.WeeklyPizzo} a week", $"Chance about {org.PizzoChance(b, p) * 100f:0}%.", g => g.Org.DemandPizzo(b, g));
                e.Option("Smash his window", "Fear works. Heat and evidence rise; he'll think again.", g => g.Org.ScareInPerson(b, g));
            }
            else
                e.Option($"Ask for €{b.WeeklyPizzo} a week, for protection", $"Chance about {org.PizzoChance(b, p) * 100f:0}%: intimidation and respect help.", g => g.Org.DemandPizzo(b, g));
            return e;
        }

        /// <summary>A shop with nothing to sell you: just the owner, and the pizzo.</summary>
        public static GameEvent Owner(Business b, Game game)
        {
            string status = b.Status switch
            {
                BusinessStatus.Paying => "He pays the family.",
                BusinessStatus.Refusing => "He refused to pay.",
                _ => "He doesn't pay anyone. Yet.",
            };
            var e = new GameEvent
            {
                Id = "owner",
                Title = b.Name,
                Neapolitan = b.Status == BusinessStatus.Paying ? "Ecco, Don. Comme sempe." : "Che vulite? Io sto faticanno.",
                English = b.Status == BusinessStatus.Paying ? "Here you are. As always." : "What do you want? I'm working.",
                Body = $"{status} {game.Org.PizzoBlocker(b, game) ?? ""}".Trim(),
            };
            WithPizzo(e, b, game);
            return e.Option("Leave", "", g => { });
        }

        public static GameEvent Shop(Business shop, Game game)
        {
            var s = game.Stock;
            if (shop.Kind == ShopKind.Tabaccheria)
            {
                return WithPizzo(new GameEvent
                {
                    Id = "shop_tabacchi",
                    Title = shop.Name,
                    Neapolitan = "Tabacco sfuso? Pe' te, guagliò, ce sta sempe.",
                    English = "Loose tobacco? For you, kid, there's always some.",
                    Body = $"In stock at the basso: {s.TobaccoG} g of tobacco, {s.Papers} papers with filters. A pack of 20 takes {Prices.TobaccoPerPack} g.",
                }
                .Option($"Tobacco, 100 g · €{Prices.TobaccoPer100g}", "Enough for seven packs.", g => Buy(g, Prices.TobaccoPer100g, "100 g of tobacco", () => g.Stock.TobaccoG += 100))
                .Option($"Tobacco, 500 g · €{Prices.TobaccoPer100g * 5 - 4}", "Enough for 35 packs.", g => Buy(g, Prices.TobaccoPer100g * 5 - 4, "500 g of tobacco", () => g.Stock.TobaccoG += 500))
                .Option($"Papers and filters ×200 · €{Prices.Papers200}", "Enough for ten packs.", g => Buy(g, Prices.Papers200, "200 papers and filters", () => g.Stock.Papers += 200)), shop, game)
                .Option("Leave", "", g => { });
            }
            return WithPizzo(new GameEvent
            {
                Id = "shop_salumeria",
                Title = shop.Name,
                Neapolitan = "Che ve servo? 'O prosciutto è frisco 'e stammatina.",
                English = "What can I get you? The ham came in this morning.",
                Body = $"At the basso: {s.Baggies} small bags, {s.Groceries} groceries.",
            }
            .Option($"Small food bags ×100 · €{Prices.Baggies100}", "Nobody asks what a seventeen-year-old needs a hundred little bags for.", g => Buy(g, Prices.Baggies100, "100 small bags", () => g.Stock.Baggies += 100))
            .Option($"Groceries ×5 · €{Prices.Groceries * 5}", "Each cooks two meals.", g => Buy(g, Prices.Groceries * 5, "groceries", () => g.Stock.Groceries += 5)), shop, game)
            .Option("Leave", "", g => { });
        }
    }
}
