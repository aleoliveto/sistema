using System.Linq;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.Story;
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

        public static GameEvent Card(Contact contact, Pawn pawn, Game game)
        {
            var offers = game.Contracts.Offers.Where(o => o.Giver == contact.Name).ToList();
            bool busy = game.Contracts.ActiveFor(pawn) != null;
            var e = new GameEvent
            {
                Id = "contact_" + contact.Name,
                Title = contact.Name,
                Neapolitan = offers.Count > 0 ? (offers[0].Neapolitan ?? contact.Greeting) : contact.NoWork,
                English = offers.Count > 0 ? (offers[0].English ?? contact.GreetingEn) : contact.NoWorkEn,
                Body = offers.Count > 0 ? $"{contact.Who}\n\n{offers[0].Text}" : contact.Who,
            };
            foreach (var o in offers)
            {
                var job = o;
                long hours = (o.ExpiresTick - game.Ticks.TicksGame) / GameClock.TicksPerHour;
                e.Option($"{o.Title} · €{o.Reward}", $"Respect +{o.RespectReward:0.#}. Risk: {o.Risk} Offer stands {hours}h.", g =>
                {
                    if (!g.Contracts.Assign(job, pawn, g, out var error)) g.Log.Add(error, MessageKind.Bad);
                }, g => !busy && pawn.CanAct);
            }
            e.Option(offers.Count > 0 ? "Not now" : "Leave", busy ? $"{pawn.Nickname} is already on a job." : "Walk away.", g => { });
            return e;
        }
    }
}
