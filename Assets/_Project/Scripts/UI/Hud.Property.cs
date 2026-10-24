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
            else if (p.OwnedByPlayer && p.Rent > 0)
            {
                var prop = p;
                Ui.Text($"<color=#E8C26A>Rented</color>: €{p.Rent} a week, paid on Sundays{(p.RentLate ? " <color=#F07A6E>· a week behind: pay this Sunday or lose it</color>" : "")}. " +
                        "The landlord notices comings and goings: an operation here makes him nosy, and he may raise the rent, throw you out or call the police.", propertyPanel, "small", "wrap");
                var row = Ui.Row(propertyPanel);
                var buyOut = Ui.Btn($"Buy it for €{p.Price}", () =>
                {
                    if (!game.Org.BuyProperty(prop, game, out var error)) game.Log.Add(error, MessageKind.Bad);
                    shownProperty = null;
                    RefreshAll();
                }, row, "btn--small");
                buyOut.SetEnabled(CanAfford(p.Price));
                Ui.Btn("Give back the keys", () => { game.Org.GiveUpRental(prop, game, null); shownProperty = null; RefreshAll(); }, row, "btn--small", "btn--danger");
            }
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
                buy.SetEnabled(CanAfford(p.Price) && zone != null && game.Org.CanExpandInto(zone, game));
                if (p.Kind == PropertyKind.Home)
                {
                    int rent = Organization.RentFor(p);
                    var rentBtn = Ui.Btn($"Rent for €{rent}/week (2 weeks up front)", () =>
                    {
                        if (!game.Org.RentProperty(prop, game, out var error)) game.Log.Add(error, MessageKind.Bad);
                        shownProperty = null;
                        RefreshAll();
                    }, propertyPanel);
                    rentBtn.SetEnabled(CanAfford(rent * 2) && game.Org.CanRent(p, game, out _));
                    rentBtn.style.alignSelf = Align.FlexStart;
                    Tip(rentBtn, () => "Cheap, and yours to live in or use as a nascondiglio. But the landlord watches: an operation here can cost you the flat, or bring the police.");
                }
                if (zone != null && !game.Org.CanExpandInto(zone, game))
                    Ui.Text("Too far from your turf. Expand into neighbouring rioni first.", propertyPanel, "small", "bad");
                buy.style.alignSelf = Align.FlexStart;
            }

            if (p.OwnedByPlayer && p.Kind == PropertyKind.Home && !p.Rect.Overlaps(game.PlayerBase))
                OperationSection(p);

            if (business != null && business.Status != BusinessStatus.Owned)
            {
                Ui.Text("PIZZO", propertyPanel, "section");
                Ui.Text(business.Status == BusinessStatus.Closed ? "Burned out." : business.StatusLabel, propertyPanel, "small");
                var row = Ui.Row(propertyPanel);
                var b = business;
                string blocker = b.Status == BusinessStatus.Free ? game.Org.PizzoBlocker(b, game) : null;
                if (blocker != null) Ui.Text(blocker + ": you can't squeeze this shop.", propertyPanel, "small", "muted");
                else if (b.Status == BusinessStatus.Free)
                    Ui.Btn($"Impose €{b.WeeklyPizzo}/week", () => { game.Org.Target(b, game); shownProperty = null; RefreshAll(); }, row, "btn--small");
                if (b.Status == BusinessStatus.Refusing)
                {
                    Ui.Btn("Ask again", () => { game.Org.Target(b, game); shownProperty = null; RefreshAll(); }, row, "btn--small");
                    Ui.Btn("Scare", () => { game.Org.OrderRetaliation(b, Retaliation.Scare, game); shownProperty = null; RefreshAll(); }, row, "btn--small", "btn--danger");
                    Ui.Btn("Burn", () => { game.Org.OrderRetaliation(b, Retaliation.Burn, game); shownProperty = null; RefreshAll(); }, row, "btn--small", "btn--danger");
                }
            }
        }

        bool CanAfford(int amount) => amount >= Prices.CleanMoneyThreshold
            ? game.Stock.Clean >= amount
            : game.Stock.Cash + game.Stock.Clean >= amount;

        void OperationSection(Property p)
        {
            Ui.Text("OPERATION", propertyPanel, "section");
            if (p.Operation == Operation.None)
            {
                Ui.Text("Put this place to work. Each operation needs a manager from the crew.", propertyPanel, "small", "muted", "wrap");
                foreach (var op in new[] { Operation.Nascondiglio, Operation.Bisca, Operation.Laboratorio, Operation.Deposito })
                {
                    var card = Ui.Box("op-card").Into(propertyPanel);
                    var head = Ui.Row(card);
                    Ui.Text($"<b>{Organization.Name(op)}</b>", head);
                    Ui.Box("spacer").Into(head);
                    var o = op;
                    var btn = Ui.Btn($"Set up €{Organization.SetupCost(op)}", () =>
                    {
                        if (!game.Org.SetUpOperation(p, o, game, out var error)) game.Log.Add(error, MessageKind.Bad);
                        shownProperty = null;
                        RefreshAll();
                    }, head, "btn--small");
                    btn.SetEnabled(game.Org.Rank >= Organization.RankFor(op) && CanAfford(Organization.SetupCost(op)));
                    Ui.Text(Organization.Describe(op) + (Organization.RankFor(op) > game.Org.Rank ? $" <color=#F07A6E>Needs rank {Organization.RankFor(op)}.</color>" : ""), card, "small", "muted", "wrap");
                }
                return;
            }

            Ui.Text($"<b>{Organization.Name(p.Operation)}</b> · {Organization.Describe(p.Operation)}", propertyPanel, "small", "wrap");
            if (p.Operation == Operation.Nascondiglio)
            {
                var served = game.Org.Squares.Where(sq => game.Org.StashFor(sq, game) == p).Select(sq => game.Territory.Zones[sq.ZoneId].Name).ToList();
                Ui.Text($"<b>{p.StashDoses}</b> doses hidden here. " + (served.Count > 0
                    ? $"Serves the piazza in {string.Join(", ", served)}. A runner on that piazza keeps it stocked from the basso; you can also bring doses yourself (E inside)."
                    : $"<color=#F07A6E>No piazza within {Organization.StashReach} steps: nothing is served from here.</color>"), propertyPanel, "small", "wrap");
                return;
            }
            var m = p.Manager;
            Ui.Text(m != null && m.IsPresent && m.Role == CrewRole.Manager
                ? $"Run by <color=#E8C26A>{m.DisplayName}</color>. Takings come in every night, in cash."
                : "<color=#F07A6E>No manager: it stays shut.</color>", propertyPanel, "small", "wrap");
            Ui.Text("Manager:", propertyPanel, "tiny");
            var row = Ui.Row(propertyPanel);
            row.style.flexWrap = Wrap.Wrap;
            foreach (var c in game.Pawns.Crew.Where(c => c.IsPresent && !c.IsBoss))
            {
                var pawn = c;
                var b = Ui.Btn(c.DisplayName, () =>
                {
                    game.Org.AssignManager(p, pawn, game);
                    shownProperty = null;
                    RefreshAll();
                }, row, "btn--small", c == m ? "btn--on" : "x");
                Tip(b, () => p.Operation switch
                {
                    Operation.Bisca => $"Intimidation {pawn.IntimidationSkill}: more takings at the tables.",
                    Operation.Laboratorio => $"Construction {pawn.ConstructionSkill}: more fakes off the bench.",
                    _ => $"Awareness {pawn.AwarenessSkill}: more cases sold a night.",
                });
            }
        }

        void PropertyTab()
        {
            var owned = game.Properties.Owned.ToList();
            Ui.Text($"The family has {owned.Count} building(s), {owned.Count(o => o.Rent > 0)} of them rented. Click any building on the map to see it. Homes house the crew and can be a nascondiglio; shops become fronts. Renting is cheap, but the landlord watches.", bizBody, "wrap");

            Ui.Text("YOURS", bizBody, "section");
            foreach (var p in owned) PropertyRow(p, buy: false);

            Ui.Text("FOR SALE OR TO LET NEARBY", bizBody, "section");
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
                b.SetEnabled(CanAfford(p.Price));
                if (p.Kind == PropertyKind.Home)
                {
                    int rent = Organization.RentFor(p);
                    var r = Ui.Btn($"Rent €{rent}/wk", Do(() =>
                    {
                        if (!game.Org.RentProperty(prop, game, out var error)) game.Log.Add(error, MessageKind.Bad);
                    }), row, "btn--small");
                    r.SetEnabled(CanAfford(rent * 2) && game.Org.CanRent(p, game, out _));
                }
            }
            else if (p.Rent > 0) Ui.Text($"rented €{p.Rent}/wk", row, "small", "muted");
            Ui.Box("spacer").Into(row);
            Ui.Btn("Show", () => { FocusCamera(prop.Rect.center); controller.SelectProperty(prop); }, row, "btn--small");
        }
    }
}
