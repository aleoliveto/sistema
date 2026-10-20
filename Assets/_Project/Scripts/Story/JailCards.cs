using System.Linq;
using OSistema.AI;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Story
{
    /// <summary>
    /// Running the family from Poggioreale: a pizzino a day, smuggled out through a guard who owes
    /// someone, and on Saturdays the colloquio, when one of the boys sits across the glass.
    /// </summary>
    public static class JailCards
    {
        public const int LawyerFee = 1500;

        static System.Collections.Generic.IEnumerable<Pawn> Free(Game g) =>
            g.Pawns.Crew.Where(c => c.CanAct && !c.Controlled && !(c.CurrentJob is SupplyRunJob));

        /// <summary>The orders you can give from inside. On a visit there's no risk; a pizzino might be read.</summary>
        public static GameEvent Orders(Game game, bool visit)
        {
            var org = game.Org;
            var e = new GameEvent
            {
                Id = visit ? "jail_visit" : "jail_pizzino",
                Title = visit ? "Colloquio" : "A pizzino",
                English = visit ? "Saturday. One of the boys sits across the glass, and listens." : "A note folded small, out through a guard who owes someone a favour.",
                Body = $"Outside: €{game.Stock.Cash} dirty, €{game.Stock.Clean} clean · {game.Stock.Product} doses, {game.Stock.BulkDg / 10} g to cut · {org.Squares.Count} piazze.",
            };
            var runner = Free(game).OrderByDescending(c => c.AwarenessSkill).FirstOrDefault();
            if (org.SupplierIntroduced)
                foreach (var (grams, price, label) in new[] { (100, Prices.Etto, "an etto"), (500, Prices.HalfKilo, "half a kilo") })
                {
                    int gr = grams, pr = price;
                    e.Option($"Send {runner?.Nickname ?? "someone"} to Zì Tore for {label} · €{pr}", "Otherwise the piazze run dry while you're inside.", g =>
                    {
                        var r = Free(g).OrderByDescending(c => c.AwarenessSkill).FirstOrDefault();
                        if (r == null) return;
                        r.ForceJob(new SupplyRunJob(r, gr, pr), g);
                        g.Log.Add($"{r.Nickname} got the message: Zì Tore, {gr} g.", MessageKind.Info);
                    }, g => runner != null && g.Stock.Cash + g.Stock.Clean >= pr);
                }
            var bagger = Free(game).FirstOrDefault(c => c.Role == CrewRole.General);
            if (game.Stock.BulkDg >= 20)
                e.Option($"Tell {bagger?.Nickname ?? "someone"} to cut and bag what's in the stash", "He takes the Bagger job.", g =>
                {
                    if (bagger != null) bagger.Role = CrewRole.Bagger;
                }, g => bagger != null);
            e.Option($"Pay the lawyer · €{LawyerFee}", "Two days off the sentence, give or take.", g =>
            {
                if (!g.Org.Pay(LawyerFee, g, out var err)) { g.Log.Add(err, MessageKind.Bad); return; }
                var me = g.Player;
                if (me != null && me.InJail) me.JailedUntil = System.Math.Max(g.Ticks.TicksGame + GameClock.TicksPerHour, me.JailedUntil - GameClock.TicksPerDay * 2);
                g.Log.Add("The avvocato takes the envelope and says he'll see what he can do.", MessageKind.Good);
            }, g => g.Stock.Cash + g.Stock.Clean >= LawyerFee);
            if (visit)
                e.Option("Tell them to keep their mouths shut", "Pressure on anyone thinking of talking eases. Loyalty +5.", g =>
                {
                    foreach (var c in g.Pawns.Crew)
                    {
                        if (!c.IsPresent || c.Controlled) continue;
                        c.Loyalty = Mathf.Min(100f, c.Loyalty + 5f);
                        g.Informants.Of(c).Pressure = Mathf.Max(0f, g.Informants.Of(c).Pressure - 10f);
                    }
                });
            else if (game.Phone.Tapped(game.Ticks.TicksGame))
                e.Body += "\n<color=#F2B866>Some pizzini get read before they get out.</color>";
            return e.Option(visit ? "That's all" : "Nothing today", "", g => { });
        }
    }
}
