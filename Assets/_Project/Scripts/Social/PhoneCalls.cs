using System;
using System.Collections.Generic;
using OSistema.Core;
using OSistema.Crime;
using OSistema.World;

namespace OSistema.Social
{
    /// <summary>
    /// Calling someone from the rubrica. They answer in their hours, say their piece, and some take
    /// orders: Zio Totonno gets tobacco and papers wholesale, Nanninella a box of empty packs. Orders
    /// come to the basso a couple of hours later. On a tapped line, every order is noted down.
    /// The dialect lines are drafts, for review.
    /// </summary>
    public static class PhoneCalls
    {
        public sealed class Call
        {
            public string Neapolitan, English;
            public bool Answered;
            public readonly List<(string label, string note, Action<Game> act)> Options = new List<(string, string, Action<Game>)>();
        }

        public const int TobaccoKilo = 70, PapersThousand = 18, EmptyPacks200 = 55;

        public static Call Ring(Contact c, Game game)
        {
            var call = new Call();
            int hour = game.Clock.Hour;
            if (!c.OpenAt(hour))
            {
                call.English = "It rings and rings. Nobody picks up.";
                return call;
            }
            call.Answered = true;
            bool work = game.Contacts.HasWork(c, game);
            switch (c.Name)
            {
                case ContactBook.Supplier:
                    // draft dialect
                    call.Neapolitan = "Ma tu si' scemo? Pe' telefono? Vieneme a truvà.";
                    call.English = "Are you mad? On the phone? Come and see me.";
                    return call;
                case ContactBook.Totonno:
                    call.Neapolitan = work ? "Gennà, passa d''o bar, tengo 'na cosa pe' te." : "Dimme, guagliò. Che te serve?";
                    call.English = work ? "Gennà, drop by the bar, I've got something for you." : "Tell me, kid. What do you need?";
                    Order(call, game, $"1 kg of loose tobacco · €{TobaccoKilo}", "Seventy packs' worth. At the basso in about two hours.", TobaccoKilo, 2, "1 kg of tobacco", g => g.Stock.TobaccoG += 1000);
                    Order(call, game, $"Papers and filters ×1000 · €{PapersThousand}", "Fifty packs' worth.", PapersThousand, 2, "1000 papers and filters", g => g.Stock.Papers += 1000);
                    return call;
                case ContactBook.Nanninella:
                    call.Neapolitan = "Pronto, bello mio. Pacchetti? Ne tengo quante ne vuò.";
                    call.English = "Hello, handsome. Packs? I've got as many as you want.";
                    Order(call, game, $"200 empty ROSSE packs · €{EmptyPacks200}", "Straight from the printer. Here in about three hours.", EmptyPacks200, 3, "200 empty ROSSE packs", g => g.Stock.EmptyPacks += 200);
                    return call;
                default:
                    call.Neapolitan = work ? c.Greeting : c.NoWork;
                    call.English = work ? (c.GreetingEn ?? "Come and see me, I've got work.") : (c.NoWorkEn ?? "Nothing for you right now.");
                    return call;
            }
        }

        static void Order(Call call, Game game, string label, string note, int price, int hours, string what, Action<Game> give)
        {
            call.Options.Add((label, note, g =>
            {
                if (!g.Org.Pay(price, g, out var err)) { g.Log.Add(err, MessageKind.Bad); return; }
                long now = g.Ticks.TicksGame;
                g.Phone.Deliver(now + hours * GameClock.TicksPerHour, what, give);
                g.Log.Add($"Ordered {what}. It'll be at the basso in about {hours} hours.", MessageKind.Info);
                if (g.Phone.Tapped(now))
                {
                    g.Org.AddEvidence(1.5f, g);
                    g.Log.Add("The line clicked twice. Someone may be listening.", MessageKind.Bad);
                }
            }));
        }
    }
}
