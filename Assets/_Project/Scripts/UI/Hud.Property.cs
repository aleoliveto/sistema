using System.Linq;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>Inspector for a clicked building, and the Property tab of the business window.</summary>
    public sealed partial class Hud
    {
        Property shownProperty;
        float propertyRefresh;

        void RefreshPropertyPanel(Property p)
        {
            // Rebuild when a different building is clicked, or once a second for live numbers.
            propertyRefresh += RefreshSeconds;
            if (p == shownProperty && propertyRefresh < 1f) return;
            shownProperty = p;
            propertyRefresh = 0f;
            propertyPanel.Clear();

            var zone = game.Territory.ZoneAt(Vector2Int.RoundToInt(p.Rect.center));
            Ui.Text(p.Name, propertyPanel, "title");
            string kind = p.Kind == PropertyKind.Shop ? "Shop" : p.Kind == PropertyKind.Civic ? "Public building" : "Home";
            Ui.Text($"{kind} · {zone?.Name} · {p.Rect.width}×{p.Rect.height}", propertyPanel, "subtitle");

            var business = game.Org.Businesses.Find(b => p.Rect.Contains(b.Cell));
            if (p.Name == "Ospedale")
                Ui.Text("Ambulances bring the wounded here. The doctors heal them, and report gunshot wounds to the police.", propertyPanel, "small", "wrap");
            else if (p.Kind == PropertyKind.Civic)
                Ui.Text("Not for sale.", propertyPanel, "small", "muted");
            else if (p.OwnedByPlayer)
            {
                Ui.Text(business != null
                    ? $"<color=#8FD18A>Yours.</color> A front: €{Organization.FrontIncome(business)} a week, and it launders money, slowing the investigation."
                    : "<color=#8FD18A>Yours.</color> The crew can sleep, hide and lie low here. Build beds and a bunker inside.", propertyPanel, "small", "wrap");
            }
            else
            {
                Ui.Text(business != null
                    ? $"For sale: <b>€{p.Price}</b>. As a front it would pay about €{Organization.FrontIncome(business)} a week."
                    : $"For sale: <b>€{p.Price}</b>. Owning it extends your base.", propertyPanel, "small", "wrap");
                var prop = p;
                var buy = Ui.Btn($"Buy for €{p.Price}", () =>
                {
                    if (!game.Org.BuyProperty(prop, game, out var error)) game.Log.Add(error, MessageKind.Bad);
                    shownProperty = null;
                    RefreshAll();
                }, propertyPanel);
                buy.SetEnabled(game.Stock.Cash >= p.Price);
                buy.style.alignSelf = Align.FlexStart;
            }

            if (business != null && business.Status != BusinessStatus.Owned)
            {
                Ui.Text("PIZZO", propertyPanel, "section");
                Ui.Text(business.Status == BusinessStatus.Closed ? "Burned out." : business.StatusLabel, propertyPanel, "small");
                var row = Ui.Row(propertyPanel);
                var b = business;
                if (b.Status == BusinessStatus.Free)
                    Ui.Btn($"Impose €{b.WeeklyPizzo}/week", () => { game.Org.Target(b); shownProperty = null; RefreshAll(); }, row, "btn--small");
                if (b.Status == BusinessStatus.Refusing)
                {
                    Ui.Btn("Ask again", () => { game.Org.Target(b); shownProperty = null; RefreshAll(); }, row, "btn--small");
                    Ui.Btn("Scare", () => { game.Org.OrderRetaliation(b, Retaliation.Scare, game); shownProperty = null; RefreshAll(); }, row, "btn--small", "btn--danger");
                    Ui.Btn("Burn", () => { game.Org.OrderRetaliation(b, Retaliation.Burn, game); shownProperty = null; RefreshAll(); }, row, "btn--small", "btn--danger");
                }
            }
        }

        void PropertyTab()
        {
            var owned = game.Properties.Owned.ToList();
            Ui.Text($"The family owns {owned.Count} building(s). Click any building on the map to see it; homes extend your base, shops become fronts.", bizBody, "wrap");

            Ui.Text("YOURS", bizBody, "section");
            foreach (var p in owned) PropertyRow(p, buy: false);

            Ui.Text("FOR SALE NEARBY", bizBody, "section");
            var home = game.PlayerBase.center;
            foreach (var p in game.Properties.All
                         .Where(p => !p.OwnedByPlayer && p.Kind != PropertyKind.Civic && FogOfWarExplored(p))
                         .OrderBy(p => (p.Rect.center - home).sqrMagnitude).Take(25))
                PropertyRow(p, buy: true);
        }

        static bool FogOfWarExplored(Property p) => OSistema.Views.FogOfWar.HasExplored(p.Door);

        void PropertyRow(Property p, bool buy)
        {
            var row = Ui.Box("table-row").Into(bizBody);
            var col = Ui.Box("col").Into(row);
            col.style.width = 330;
            Ui.Text($"<b>{p.Name}</b>", col);
            var zone = game.Territory.ZoneAt(Vector2Int.RoundToInt(p.Rect.center));
            Ui.Text($"{(p.Kind == PropertyKind.Shop ? "Shop" : "Home")} · {zone?.Name} · {p.Rect.width}×{p.Rect.height}", col, "small", "muted");
            var prop = p;
            if (buy)
            {
                var b = Ui.Btn($"Buy €{p.Price}", Do(() =>
                {
                    if (!game.Org.BuyProperty(prop, game, out var error)) game.Log.Add(error, MessageKind.Bad);
                }), row, "btn--small");
                b.SetEnabled(game.Stock.Cash >= p.Price);
            }
            Ui.Box("spacer").Into(row);
            Ui.Btn("Show", () => { FocusCamera(prop.Rect.center); controller.SelectProperty(prop); }, row, "btn--small");
        }
    }
}
