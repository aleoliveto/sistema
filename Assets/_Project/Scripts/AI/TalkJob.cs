using System.Linq;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.Story;
using OSistema.World;
using UnityEngine;

namespace OSistema.AI
{
    /// <summary>Walk up to a contact and hear what they have. Work is offered face to face.</summary>
    public sealed class TalkJob : Job
    {
        readonly Contact contact;
        Walker walker;
        Vector2Int lastSpot;

        public TalkJob(Pawn pawn, Contact contact) : base(pawn) => this.contact = contact;

        public override string Label => $"Going to see {contact.Name}";

        public override bool Tick(Game game)
        {
            if (!contact.Present)
            {
                game.Log.Add($"{contact.Name} isn't around. Usually there {contact.Hours}.", MessageKind.Info, contact.Spot);
                return Fail();
            }
            var spot = contact.Npc.Mover.Cell;
            if (walker == null || spot != lastSpot)
            {
                walker = new Walker(spot, adjacent: true);
                lastSpot = spot;
            }
            var status = walker.Tick(Pawn, game);
            if (status == WalkStatus.Walking) return false;
            if (status == WalkStatus.Failed) return Fail();
            game.Events.Raise(Card(contact, Pawn, game));
            return true;
        }

        /// <summary>Who to send on a job: the free crew, with what they're good at.</summary>
        static GameEvent SendPicker(Contract job, Pawn boss, Game game)
        {
            var e = new GameEvent
            {
                Id = "send_picker",
                Title = $"Who goes? · {job.Title}",
                English = "Pick one of the boys. He goes and does it on his own.",
                Body = $"€{job.Reward} · respect +{job.RespectReward:0.#} · risk: {job.Risk}",
            };
            foreach (var c in game.Pawns.Crew)
            {
                if (c == boss || c.Controlled || !c.CanAct || game.Contracts.ActiveFor(c) != null) continue;
                var crew = c;
                e.Option($"{crew.DisplayName} · {crew.CurrentJob?.Label ?? "free"}", $"Intimidation {crew.IntimidationSkill} · awareness {crew.AwarenessSkill} · shooting {crew.ShootingSkill} · loyalty {crew.Loyalty:0}", g =>
                {
                    if (!g.Contracts.Assign(job, crew, g, out var error)) g.Log.Add(error, MessageKind.Bad);
                });
            }
            return e.Option("Nobody", "", g => { });
        }

        public static GameEvent Card(Contact contact, Pawn pawn, Game game)
        {
            if (contact.Name == ContactBook.Supplier) return SupplierCard(contact, game);
            var offers = game.Contracts.Offers.Where(o => o.Giver == contact.Name).ToList();
            bool busy = game.Contracts.ActiveFor(pawn) != null;
            var e = new GameEvent
            {
                Id = "contact_" + contact.Name,
                Title = contact.Name,
                Portrait = contact.Face,
                Neapolitan = offers.Count > 0 ? (offers[0].Neapolitan ?? contact.Greeting) : contact.NoWork,
                English = offers.Count > 0 ? (offers[0].English ?? contact.GreetingEn) : contact.NoWorkEn,
                Body = offers.Count > 0 ? $"{contact.Who}\n\n{offers[0].Text}" : contact.Who,
            };
            foreach (var o in offers)
            {
                var job = o;
                long hours = (o.ExpiresTick - game.Ticks.TicksGame) / GameClock.TicksPerHour;
                e.Option($"{o.Title} · €{o.Reward} · do it yourself", $"Respect +{o.RespectReward:0.#}. Risk: {o.Risk} Offer stands {hours}h.", g =>
                {
                    if (!g.Contracts.Assign(job, pawn, g, out var error)) g.Log.Add(error, MessageKind.Bad);
                }, g => !busy && pawn.CanAct);
                // Or hand it to one of your boys: one choice, then pick who.
                int free = game.Pawns.Crew.Count(c => c != pawn && !c.Controlled && c.CanAct && game.Contracts.ActiveFor(c) == null);
                if (free > 0)
                    e.Option($"    …or send someone ({free} free) ▸", "Pick who goes. Same pay and respect for the family; he learns from it.", g => g.Events.Raise(SendPicker(job, pawn, g)));
            }
            // Some contacts trade as well as giving work.
            if (contact.Name == ContactBook.Totonno && game.Stock.Cigarettes > 0 && game.Org.TotonnoWants(game) > 0)
            {
                int n = System.Math.Min(game.Stock.Cigarettes, game.Org.TotonnoWants(game));
                e.Option($"Sell him {n} packs of ROSSE · €{n * game.Org.TotonnoPrice(game)}", "He sells them under the counter. Up to 20 packs a day.", g => g.Org.SellPacksToTotonno(g));
            }
            if (contact.Name == ContactBook.Nanninella)
            {
                e.Option($"Empty ROSSE packs ×20 · €{Prices.EmptyPacks20}", "From a printer she knows. Red and white, nobody looks twice.", g =>
                {
                    if (g.Org.Pay(Prices.EmptyPacks20, g, out var err)) g.Stock.EmptyPacks += 20; else g.Log.Add(err, MessageKind.Bad);
                });
                e.Option($"Ask what she knows about the clans · €{OperationCards.InfoPrice}", "Information for a hit: names, faces, where the boss drinks.", g => g.Events.Raise(OperationCards.Information(g)));
                foreach (var table in new[] { BuildingDefs.RollingTable, BuildingDefs.BaggingTable })
                {
                    var t = table;
                    int price = BuildingDef.CostOf(t);
                    e.Option($"A {t.Label.ToLower()} · €{price}", $"Delivered to your storage ({game.Stock.StoredOf(t)} there now). Put it down from Build › Work.", g =>
                    {
                        if (g.Org.Pay(price, g, out var err)) { g.Stock.Store(t); g.Log.Add($"Nanninella's nephew brings a {t.Label.ToLower()} round. It's in storage.", MessageKind.Good); }
                        else g.Log.Add(err, MessageKind.Bad);
                    });
                }
                if (!game.Stock.DigitalScale)
                    e.Option($"A digital scale · €{Prices.DigitalScale}", "Weighs to a hundredth of a gram. Honest doses, faster baggers.", g =>
                    {
                        if (g.Org.Pay(Prices.DigitalScale, g, out var err)) g.Stock.DigitalScale = true; else g.Log.Add(err, MessageKind.Bad);
                    });
                if (!game.Stock.HeatSealer)
                    e.Option($"A heat sealer · €{Prices.HeatSealer}", "Seals a bag in a second. Baggers work faster.", g =>
                    {
                        if (g.Org.Pay(Prices.HeatSealer, g, out var err)) g.Stock.HeatSealer = true; else g.Log.Add(err, MessageKind.Bad);
                    });
                if (!game.Stock.RollingMachine)
                    e.Option($"A rolling machine · €{Prices.RollingMachine}", "Rolls twenty at a time. Better cigarettes, faster rollers.", g =>
                    {
                        if (g.Org.Pay(Prices.RollingMachine, g, out var err)) g.Stock.RollingMachine = true; else g.Log.Add(err, MessageKind.Bad);
                    });
            }
            e.Option(offers.Count > 0 ? "Not now" : "Leave", busy ? $"{pawn.Nickname} is already on a job." : "Walk away.", g => { });
            return e;
        }

        // Buying product: face to face, at night, cash up front.
        static GameEvent SupplierCard(Contact contact, Game game)
        {
            bool carrying = game.Org.CarriedDg > 0;
            var e = new GameEvent
            {
                Id = "contact_supplier",
                Title = contact.Name,
                Portrait = contact.Face,
                Neapolitan = carrying ? "Tiene ancora 'a roba 'ncuollo? Vattenne a casa, primma ca te fermano." : contact.Greeting,
                English = carrying ? "You've still got the stuff on you? Go home, before they stop you." : contact.GreetingEn,
                Body = carrying
                    ? "Get the last lot home first."
                    : $"{contact.Who}\n\nCash only. You carry it home yourself: if a patrol stops you on the way, it's gone and so are you. At home, cut it into half-gram doses at the bagging table.",
            };
            if (!carrying)
            {
                foreach (var (grams, price, label) in new[] { (100, Prices.Etto, "An etto, 100 g"), (500, Prices.HalfKilo, "Half a kilo"), (1000, Prices.Kilo, "A kilo") })
                {
                    int gr = grams, pr = price;
                    e.Option($"{label} · €{pr}", $"{gr * 10 / Prices.DoseDg} doses once bagged.", g =>
                    {
                        if (!g.Org.BuyBulk(gr, pr, g, out var error)) g.Log.Add(error, MessageKind.Bad);
                    }, g => g.Stock.Cash + g.Stock.Clean >= pr);
                }
            }
            e.Option("Leave", "Walk away.", g => { });
            return e;
        }
    }
}
