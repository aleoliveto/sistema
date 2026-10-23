using OSistema.AI;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.UI
{
    /// <summary>What to do next, and where: a suggestion worked out from the state of the game.</summary>
    public struct Goal
    {
        public string Text;
        public Vector2? Target;
    }

    public static class Objectives
    {
        public static Goal Current(Game game, Vector2? waypoint)
        {
            var p = game.Player;
            if (p == null) return new Goal { Text = "" };
            if (p.InJail) return new Goal { Text = "In Poggioreale. Wait it out." };
            var org = game.Org;
            var s = game.Stock;
            int hour = game.Clock.Hour;

            if (org.CarriedDg > 0) return Go("Get the product home without meeting the police", Home(game));

            // Bleeding comes before anything: home with medicine, the pharmacist for a cut, the hospital for a bullet.
            if (p.Health.BleedRatePerDay > 0.15f && Healing.NeedsCare(p))
            {
                if (s.Medicine > 0) return Go("You're bleeding: get home and treat it (E, medicine)", Home(game));
                if (!Healing.HasGunshot(p))
                {
                    var ph = Shop(game, p.Cell, ShopKind.Farmacia, "You're bleeding: the pharmacist can stitch you up");
                    if (ph.Target.HasValue) return ph;
                }
                if (game.Map.TryFindNearest(BuildingDefs.HospitalDesk, p.Cell, out var desk))
                    return Go("You're bleeding: pronto soccorso at the Ospedale (they report bullets)", Mover.CellCenter(desk));
            }

            // A job in hand comes first, unless the body really can't wait.
            var job = game.Contracts.ActiveFor(p);
            bool critical = p.Needs.Food < 0.08f || p.Needs.Rest < 0.06f;
            if (job != null && !critical) return Go(Contracts.StepGoal(job), Mover.CellCenter(Contracts.StepTarget(job)));

            // The body: nobody looks after Gennaro but you.
            if (p.Needs.Food < 0.22f)
            {
                if (s.Meals > 0) return Station(game, p.Cell, BuildingDefs.Pantry, "You're starving: eat something at home (E at the pantry)", "You're starving: buy a pizza");
                if (s.Groceries > 0 && s.Cook == null && OSistema.AI.JobGiver.TryFindAtHome(game, BuildingDefs.Stove, p.Cell, out _))
                    return Station(game, p.Cell, BuildingDefs.Stove, "You're starving: cook something at home (E at the stove)", "You're starving: buy a pizza");
                return Shop(game, p.Cell, ShopKind.Pizzeria, $"You're starving: get a pizza (€{Prices.Pizza})");
            }
            if (p.Needs.Rest < 0.15f) return Station(game, p.Cell, BuildingDefs.Bed, "You're dead on your feet: sleep in your bed (E)", "You're exhausted: find somewhere to sleep");

            var cavallo = org.CavalloDropAt(game);
            if (cavallo.HasValue) return Go("Take the scooter to Nanninella", cavallo.Value);

            var visitor = game.Events.Visitor;
            if (visitor != null) return Go($"{game.Events.VisitorTitle}: someone wants a word with you", visitor.Mover.Position);

            if (job != null) return Go(Contracts.StepGoal(job), Mover.CellCenter(Contracts.StepTarget(job)));

            // The supplier: a serious run, always marked.
            if (org.SupplierIntroduced && s.BulkDg < 2 && s.Product < 10 && org.CarriedDg == 0)
            {
                var tore = game.Contacts.ByName(ContactBook.Supplier);
                if (tore != null && tore.Present && s.Cash + s.Clean >= Prices.Etto) return Go("Buy product from Zì Tore", tore.Position);
            }

            // First steps, as a tutorial: each hint shows until you've done it once, then never again.
            var learn = Tutorial(game, p, org, s);
            if (learn.HasValue) return learn.Value;

            // Nothing serious going on: no marker. The Taccuino (J) says who has work.
            if (hour >= 23 || hour < 5) return new Goal { Text = "Late. Home and bed, when you're done." };
            return new Goal { Text = "" };
        }

        // Hints shown at the moment, so that when one is done it's remembered and dropped.
        static readonly System.Collections.Generic.HashSet<string> shown = new System.Collections.Generic.HashSet<string>();

        static Goal? Tutorial(Game game, Pawn p, Organization org, Stock s)
        {
            Goal? Step(string key, bool needed, System.Func<Goal> goal)
            {
                if (org.Learned.Contains(key)) return null;
                if (needed)
                {
                    shown.Add(key);
                    var g = goal();
                    return g.Target.HasValue || !string.IsNullOrEmpty(g.Text) ? g : (Goal?)null;
                }
                // It was being asked for and now it isn't: done once, learned.
                if (shown.Remove(key)) org.Learned.Add(key);
                return null;
            }

            bool roller = false, bagger = false;
            foreach (var c in game.Pawns.Crew)
            {
                if (!c.IsPresent || c.Controlled) continue;
                roller |= c.Role == CrewRole.Roller;
                bagger |= c.Role == CrewRole.Bagger;
            }
            var totonno = game.Contacts.ByName(ContactBook.Totonno);
            var nanni = game.Contacts.ByName(ContactBook.Nanninella);
            return Step("tobacco", s.TobaccoG < Prices.TobaccoPerPack || s.Papers < 20, () => Shop(game, p.Cell, ShopKind.Tabaccheria, "Buy tobacco and papers at the tabaccaio"))
                ?? Step("packs", s.EmptyPacks <= 0 && nanni != null && nanni.Present, () => Go("Buy empty ROSSE packs from Nanninella", nanni.Position))
                ?? Step("roll", s.Cigarettes == 0 && org.CanMakePack(game) && s.TobaccoG >= Prices.TobaccoPerPack && !roller, () => StationOrBuy(game, p.Cell, BuildingDefs.RollingTable, "Roll a pack of ROSSE at the rolling table"))
                ?? Step("sell", s.Cigarettes > 0 && totonno != null && totonno.Present && org.TotonnoWants(game) > 0, () => Go($"Sell your packs of ROSSE to Zio Totonno", totonno.Position))
                ?? Step("bags", s.BulkDg >= 2 && s.Baggies <= 0, () => Shop(game, p.Cell, ShopKind.Salumeria, "Buy small bags at the salumeria"))
                ?? Step("bag", s.BulkDg >= 2 && s.Baggies > 0 && !bagger, () => StationOrBuy(game, p.Cell, BuildingDefs.BaggingTable, "Cut and bag the product at the bagging table"));
        }

        static Goal Go(string text, Vector2 at) => new Goal { Text = text, Target = at };

        static Vector2 Home(Game game)
        {
            var home = game.Properties.All.Find(pr => pr.OwnedByPlayer && pr.Rect.Overlaps(game.PlayerBase));
            return home != null ? Mover.CellCenter(home.Door) : game.PlayerBase.center;
        }

        static Goal Station(Game game, Vector2Int from, BuildingDef def, string text, string missing)
        {
            Vector2Int? best = null;
            int bestD = int.MaxValue;
            foreach (var c in game.Map.PositionsOf(def))
            {
                if (!game.PlayerBase.Contains(c) && !game.Properties.IsOwnedHome(c)) continue;
                int d = GridUtil.Manhattan(from, c);
                if (d < bestD) { bestD = d; best = c; }
            }
            return best.HasValue ? Go(text, Mover.CellCenter(best.Value)) : new Goal { Text = missing };
        }

        /// <summary>Go to the table; or put down the one in storage; or buy one from Nanninella.</summary>
        static Goal StationOrBuy(Game game, Vector2Int from, BuildingDef def, string text)
        {
            var g = Station(game, from, def, text, null);
            if (g.Target.HasValue) return g;
            if (game.Stock.StoredOf(def) > 0) return new Goal { Text = $"Put down your {def.Label.ToLower()} at home (B, Work)" };
            var nanni = game.Contacts.ByName(ContactBook.Nanninella);
            if (nanni != null && nanni.Present) return Go($"Buy a {def.Label.ToLower()} from Nanninella", nanni.Position);
            return new Goal { Text = $"Buy a {def.Label.ToLower()} from Nanninella" };
        }

        static Goal Shop(Game game, Vector2Int from, ShopKind kind, string text)
        {
            Business best = null;
            int bestD = int.MaxValue;
            foreach (var b in game.Org.Businesses)
            {
                if (b.Kind != kind || b.Status == BusinessStatus.Closed) continue;
                int d = GridUtil.Manhattan(from, b.Cell);
                if (d < bestD) { bestD = d; best = b; }
            }
            int hour = game.Clock.Hour;
            if (best == null) return new Goal { Text = text };
            if (hour < 8 || hour >= 21) return new Goal { Text = text + " (opens at 08:00)" };
            return Go(text, Mover.CellCenter(best.Cell));
        }
    }
}
