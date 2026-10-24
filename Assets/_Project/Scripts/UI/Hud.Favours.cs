using System.Linq;
using OSistema.Core;
using OSistema.Pawns;
using OSistema.Social;
using OSistema.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>Business window tab: the neighbours' favours, trust in the crew, and wiretaps.</summary>
    public sealed partial class Hud
    {
        int confirmDisappear;   // pawn id waiting for a second click

        void FavoursTab()
        {
            var fav = game.Favours;
            long now = game.Ticks.TicksGame;

            Ui.Text($"The rione owes the family <b>{fav.TotalOwed}</b> favour(s). Neighbours ask by phone; every yes is a debt you can call in, every no is remembered." +
                    (fav.StashHidden(game) ? " <color=#8FD18A>The stash is hidden in a neighbour's cellar.</color>" : ""), bizBody, "wrap");

            Ui.Text("THE NEIGHBOURS", bizBody, "section");
            foreach (var n in fav.S.Neighbours.OrderByDescending(n => n.Witness).ThenByDescending(n => n.Owes))
            {
                var row = Ui.Box("table-row").Into(bizBody);
                var col = Ui.Box("col").Into(row);
                col.style.width = 210;
                Ui.Text($"<b>{n.Name}</b>", col);
                string zone = n.Zone >= 0 && n.Zone < game.Territory.Zones.Count ? game.Territory.Zones[n.Zone].Name : "";
                Ui.Text($"{n.Trade} · {zone}", col, "small", "muted");
                var res = game.StreetLife?.Residents.Of(n);
                if (res != null) Ui.Text($"now: {res.Doing}", col, "small", "muted");

                var st = Ui.Box("col").Into(row);
                st.style.width = 150;
                Ui.Text(n.Owes > 0 ? $"<color=#E8C26A>owes {n.Owes}</color>" : "owes nothing", st, "small");
                string grudge = n.Grudge > 0 ? $"<color=#F07A6E>{new string('●', Mathf.Min(5, n.Grudge))}</color> grudge" : "";
                string loan = n.Loan > 0 ? $" · €{n.Loan} due day {n.LoanDueDay}" : "";
                Ui.Text(grudge + loan, st, "small", "muted");

                var nb = n;
                if (n.Witness)
                {
                    Ui.Text("<color=#F07A6E>Talking to the police</color>", row, "small").style.width = 150;
                    Ui.Btn("Buy silence (€500)", Do(() =>
                    {
                        if (!fav.BuySilence(nb, game, out var error) && error != null) game.Log.Add(error, MessageKind.Bad);
                    }), row, "btn--small", "btn--danger");
                    continue;
                }
                foreach (CallIn c in System.Enum.GetValues(typeof(CallIn)))
                {
                    var call = c;
                    var b = Ui.Btn($"{Favours.Label(c)} ({Favours.Cost(c)})", Do(() =>
                    {
                        if (!fav.Call(nb, call, game, out var error)) game.Log.Add(error, MessageKind.Bad);
                    }), row, "btn--small");
                    b.SetEnabled(n.Owes >= Favours.Cost(c));
                    Tip(b, () => $"{Favours.Describe(call)}\nCosts {Favours.Cost(call)} of what {nb.Name} owes.");
                }
            }

            TrustSection(now);
            WiretapSection(now);
        }

        void TrustSection(long now)
        {
            var inf = game.Informants;
            Ui.Text("THE CREW · WHOM DO YOU TRUST?", bizBody, "section");
            Ui.Text("Gossip arrives by phone. Some of it is true. A false shipment told to one man only is the surest test.", bizBody, "small", "muted", "wrap");

            foreach (var p in game.Pawns.Crew.Where(p => !p.Dead && !p.Departed && !p.IsBoss))
            {
                var d = inf.Of(p);
                var row = Ui.Box("table-row").Into(bizBody);
                Avatar(p, row, big: false);
                var col = Ui.Box("col").Into(row);
                col.style.width = 150;
                Ui.Text($"<b>{p.DisplayName}</b>", col);
                string status = d.Confirmed && d.Feeding ? "<color=#F2B866>A rat, fed lies</color>"
                    : d.Confirmed ? "<color=#F07A6E>A rat</color>"
                    : inf.S.TestPawn == p.Id ? "Being tested…"
                    : d.Cleared ? "<color=#8FD18A>Passed the test</color>"
                    : p.InJail ? "In Poggioreale" : $"Loyalty {p.Loyalty:0}";
                Ui.Text(status, col, "small", "muted");

                var sus = Ui.Box("col").Into(row);
                sus.style.width = 110;
                Ui.Text("SUSPICION", sus, "tiny");
                var (_, fill) = Ui.Bar("", new Color(0.95f, 0.55f, 0.25f), sus, thin: true);
                Ui.SetFill(fill, d.Suspicion / 100f);

                var pawn = p;
                var test = Ui.Btn("Test", Do(() =>
                {
                    if (!inf.StartTest(pawn, game, out var error)) game.Log.Add(error, MessageKind.Bad);
                }), row, "btn--small");
                test.SetEnabled(inf.S.TestPawn == 0 && !d.Confirmed && p.IsPresent);
                Tip(test, () => $"Tell only {pawn.Nickname} about a load that doesn't exist. In two days you'll know.");

                var confront = Ui.Btn("Confront", Do(() => inf.Confront(pawn, game)), row, "btn--small");
                confront.SetEnabled(!d.Confirmed && p.IsPresent);
                Tip(confront, () => "Take him to the back room and ask. A rat may confess. An innocent man won't forget it.");

                var expel = Ui.Btn("Throw out", Do(() => inf.Expel(pawn, game)), row, "btn--small");
                Tip(expel, () => "He leaves the family. If he was talking, he'll talk a lot more now.");

                bool confirming = confirmDisappear == p.Id;
                var gone = Ui.Btn(confirming ? "Sure?" : "Disappear", Do(() =>
                {
                    if (confirmDisappear != pawn.Id)
                    {
                        confirmDisappear = pawn.Id;
                        return;
                    }
                    confirmDisappear = 0;
                    inf.Disappear(pawn, game);
                }), row, "btn--small", "btn--danger");
                gone.SetEnabled(p.IsPresent);
                Tip(gone, () => "Lupara bianca. Respect +5, heat, and fear in the crew. If he wasn't a rat, they'll never forgive you.");
            }
        }

        void WiretapSection(long now)
        {
            var phone = game.Phone;
            Ui.Text("MICROPHONES", bizBody, "section");
            Ui.Text("The more evidence piles up, the likelier the investigators bug the phone or a property. You won't be told. Watch for signs.", bizBody, "small", "muted", "wrap");
            var row = Ui.Row(bizBody);
            Ui.Btn($"Call 'o Tecnico for a sweep (€{Prices.Bonifica})", Do(() =>
            {
                if (!game.Informants.Bonifica(game, out var error)) game.Log.Add(error, MessageKind.Bad);
            }), row, "btn--small");
            var burner = Ui.Btn(phone.HasBurner(now) ? $"Burner: {(phone.S.BurnerUntil - now) / GameClock.TicksPerDay + 1}d left · add a week (€{Prices.Burner})" : $"Buy a burner SIM (€{Prices.Burner})", Do(() =>
            {
                phone.BuyBurner(game, out var error);
                if (error != null) game.Log.Add(error, MessageKind.Bad);
            }), row, "btn--small");
            Tip(burner, () => "A week of a clean phone line: nothing said on the phone can be used against the family.");
        }
    }
}
