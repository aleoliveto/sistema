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

        public static GameEvent Shop(Business shop, Game game)
        {
            var s = game.Stock;
            if (shop.Kind == ShopKind.Tabaccheria)
            {
                return new GameEvent
                {
                    Id = "shop_tabacchi",
                    Title = shop.Name,
                    Neapolitan = "Tabacco sfuso? Pe' te, guagliò, ce sta sempe.",
                    English = "Loose tobacco? For you, kid, there's always some.",
                    Body = $"In stock at the basso: {s.TobaccoG} g of tobacco, {s.Papers} papers with filters. A pack of 20 takes {Prices.TobaccoPerPack} g.",
                }
                .Option($"Tobacco, 100 g · €{Prices.TobaccoPer100g}", "Enough for seven packs.", g => Buy(g, Prices.TobaccoPer100g, "100 g of tobacco", () => g.Stock.TobaccoG += 100))
                .Option($"Tobacco, 500 g · €{Prices.TobaccoPer100g * 5 - 4}", "Enough for 35 packs.", g => Buy(g, Prices.TobaccoPer100g * 5 - 4, "500 g of tobacco", () => g.Stock.TobaccoG += 500))
                .Option($"Papers and filters ×200 · €{Prices.Papers200}", "Enough for ten packs.", g => Buy(g, Prices.Papers200, "200 papers and filters", () => g.Stock.Papers += 200))
                .Option("Leave", "", g => { });
            }
            return new GameEvent
            {
                Id = "shop_salumeria",
                Title = shop.Name,
                Neapolitan = "Che ve servo? 'O prosciutto è frisco 'e stammatina.",
                English = "What can I get you? The ham came in this morning.",
                Body = $"At the basso: {s.Baggies} small bags, {s.Groceries} groceries.",
            }
            .Option($"Small food bags ×100 · €{Prices.Baggies100}", "Nobody asks what a seventeen-year-old needs a hundred little bags for.", g => Buy(g, Prices.Baggies100, "100 small bags", () => g.Stock.Baggies += 100))
            .Option($"Groceries ×5 · €{Prices.Groceries * 5}", "Each cooks two meals.", g => Buy(g, Prices.Groceries * 5, "groceries", () => g.Stock.Groceries += 5))
            .Option("Leave", "", g => { });
        }
    }
}
