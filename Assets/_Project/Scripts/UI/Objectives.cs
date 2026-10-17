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

            var visitor = game.Events.Visitor;
            if (visitor != null) return Go($"{game.Events.VisitorTitle}: someone wants a word with you", visitor.Mover.Position);

            var job = game.Contracts.ActiveFor(p);
            if (job != null) return Go(Contracts.StepGoal(job), Mover.CellCenter(Contracts.StepTarget(job)));

            var saverio = game.Contacts.ByName(ContactBook.Saverio);
            if (saverio != null && saverio.Present && game.Contacts.HasWork(saverio, game) && hour >= 16)
                return Go("Don Saverio needs a lookout tonight", saverio.Position);

            var totonno = game.Contacts.ByName(ContactBook.Totonno);
            if (s.Cigarettes > 0 && totonno != null && totonno.Present && org.TotonnoWants(game) > 0)
                return Go($"Sell your {s.Cigarettes} packs of ROSSE to Zio Totonno", totonno.Position);

            if (s.BulkDg >= 2 && s.Baggies > 0)
                return Station(game, p.Cell, BuildingDefs.BaggingTable, "Cut and bag the product at the bagging table", "Build a bagging table at home (B, Work & Crime)");
            if (s.BulkDg >= 2 && s.Baggies <= 0)
                return Shop(game, p.Cell, ShopKind.Salumeria, "Buy small bags at the salumeria");

            if (org.CanMakePack(game) && s.TobaccoG >= Prices.TobaccoPerPack)
                return Station(game, p.Cell, BuildingDefs.RollingTable, "Roll a pack of ROSSE at the rolling table", "Build a rolling table at home (B, Work & Crime)");
            if (s.TobaccoG < Prices.TobaccoPerPack || s.Papers < 20)
            {
                var g = Shop(game, p.Cell, ShopKind.Tabaccheria, "Buy tobacco and papers at the tabaccaio");
                if (g.Target.HasValue && game.Stock.Cash + game.Stock.Clean >= Prices.TobaccoPer100g) return g;
            }
            var nanni = game.Contacts.ByName(ContactBook.Nanninella);
            if (s.EmptyPacks <= 0 && nanni != null && nanni.Present && s.Cash >= Prices.EmptyPacks20)
                return Go("Buy empty ROSSE packs from Nanninella", nanni.Position);

            if (org.SupplierIntroduced && s.BulkDg < 2 && org.CarriedDg == 0)
            {
                var tore = game.Contacts.ByName(ContactBook.Supplier);
                if (tore != null && tore.Present && s.Cash + s.Clean >= Prices.Etto) return Go("Buy product from Zì Tore", tore.Position);
            }

            Contact best = null;
            float bestD = float.MaxValue;
            foreach (var c in game.Contacts.All)
            {
                if (!c.Present || !game.Contacts.HasWork(c, game)) continue;
                float d = (c.Position - p.Position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            if (best != null) return Go($"{best.Name} has work for you", best.Position);

            if (hour >= 22 || hour < 6) return Go("Go home and sleep", Home(game));
            return new Goal { Text = "Walk the rione. Someone always needs something." };
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
