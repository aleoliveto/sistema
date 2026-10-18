using System.Collections.Generic;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Story;
using OSistema.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>The counter: pick quantities, see the total, pay once.</summary>
    public sealed partial class Hud
    {
        VisualElement shopDim;

        public bool ShopOpen => shopDim != null && shopDim.style.display.value == DisplayStyle.Flex;

        public void OpenShop(Business shop)
        {
            if (shopDim == null) shopDim = Ui.Box("dim").Into(modalLayer);
            shopDim.Clear();
            shopDim.Show(true);
            var items = ShopCards.Items(shop);
            var qty = new int[items.Count];

            var card = Ui.Box("panel", "card", "shop").Into(shopDim);
            var band = Ui.Box("card__band").Into(card);
            band.style.backgroundColor = new Color(0.14f, 0.12f, 0.09f);
            Ui.Text(shop.Name, band, "card__title");
            var body = Ui.Box("card__body").Into(card);
            var s = game.Stock;
            Ui.Text(shop.Kind switch
            {
                ShopKind.Tabaccheria => $"At home: {s.TobaccoG} g tobacco, {s.Papers} papers.",
                ShopKind.Mobili => "Delivered to your storage. Put it down from Build › Home, in any home you own.",
                ShopKind.Lusso => $"Respect {game.Org.Respect:0}. People judge a man by his wrist.",
                ShopKind.Farmacia => $"At home: {s.Medicine} medicine, {s.Baggies} bags.",
                _ => $"At home: {s.Baggies} bags, {s.Groceries} groceries, {s.Meals} meals.",
            }, body, "small", "muted", "wrap");

            var total = Ui.Text("", body, "bold");
            Button pay = null;
            void Update()
            {
                int sum = 0;
                for (int i = 0; i < items.Count; i++) sum += qty[i] * items[i].Price;
                total.text = $"Total: €{sum}   <color=#A89F8E>(you have €{s.Cash} cash, €{s.Clean} clean)</color>";
                pay?.SetEnabled(sum > 0 && s.Cash + s.Clean >= sum);
                if (pay != null) pay.text = sum > 0 ? $"Pay €{sum}" : "Pay";
            }

            for (int i = 0; i < items.Count; i++)
            {
                int k = i;
                var row = Ui.Box("table-row").Into(body);
                var col = Ui.Box("col").Into(row);
                col.style.width = 260;
                Ui.Text($"<b>{items[i].Name}</b>", col);
                Ui.Text($"€{items[i].Price} · {items[i].Note}", col, "small", "muted");
                if (items[i].Def != null)
                    Ui.Text($"{s.StoredOf(items[i].Def)} in storage", col, "tiny", "muted");
                Ui.Box("spacer").Into(row);
                var n = Ui.Text("0", null, "bold");
                Ui.Btn("−", () => { if (qty[k] > 0) qty[k]--; n.text = qty[k].ToString(); Update(); }, row, "btn--small");
                row.Add(n);
                n.style.width = 30;
                n.style.unityTextAlign = TextAnchor.MiddleCenter;
                Ui.Btn("+", () => { if (qty[k] < 50) qty[k]++; n.text = qty[k].ToString(); Update(); }, row, "btn--small");
            }
            body.Add(total);
            total.style.marginTop = 8;

            var buttons = Ui.Row(body);
            buttons.style.marginTop = 8;
            pay = Ui.Btn("Pay", () =>
            {
                int sum = 0;
                for (int i = 0; i < items.Count; i++) sum += qty[i] * items[i].Price;
                if (!game.Org.Pay(sum, game, out var error)) { game.Log.Add(error, MessageKind.Bad); return; }
                for (int i = 0; i < items.Count; i++)
                    for (int q = 0; q < qty[i]; q++) items[i].Give(game);
                game.Log.Add($"Paid €{sum} at {shop.Name}.", MessageKind.Good);
                CloseShop();
            }, buttons);
            pay.AddToClassList("btn--on");
            if (ShopCards.PizzoActionable(shop, game))
                Ui.Btn("Have a word with the owner", () => { CloseShop(); game.Events.Raise(ShopCards.Owner(shop, game)); }, buttons, "btn--small").style.marginLeft = 10;
            Ui.Box("spacer").Into(buttons);
            Ui.Btn("Leave  [Esc]", CloseShop, buttons);
            Update();
        }

        public void CloseShop()
        {
            if (shopDim != null) shopDim.Show(false);
        }
    }
}
