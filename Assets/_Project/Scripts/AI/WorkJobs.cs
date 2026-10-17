using OSistema.Core;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.AI
{
    /// <summary>Turn one unit of groceries into two meals at the stove.</summary>
    public sealed class CookJob : Job
    {
        const float Work = 500f;

        readonly Walker walker;
        float workLeft = Work;
        bool cooking;

        public CookJob(Pawn pawn, Vector2Int stove, Game game) : base(pawn)
        {
            walker = new Walker(stove, adjacent: true);
            game.Stock.Cook = pawn;
        }

        public override string Label => cooking ? "Cooking" : "Going to the stove";
        public override bool IsWork => true;

        public override bool Tick(Game game)
        {
            var stock = game.Stock;
            if (!cooking)
            {
                var status = walker.Tick(Pawn, game);
                if (status == WalkStatus.Failed) return Fail();
                if (status == WalkStatus.Walking) return false;
                if (stock.Groceries <= 0) return Fail();
                stock.Groceries--;
                cooking = true;
                return false;
            }

            workLeft -= 1f + 0.12f * Pawn.CookingSkill;
            if (workLeft > 0f) return false;

            stock.Meals += 2;
            cooking = false;
            Pawn.Learn(Skill.Cooking, 12f, game);
            return true;
        }

        public override void OnEnd(Game game)
        {
            if (cooking) game.Stock.Groceries++; // interrupted: ingredients go back in the pantry
            if (game.Stock.Cook == Pawn) game.Stock.Cook = null;
        }
    }

    /// <summary>Walk to a shop, pay, and bring the goods back to the pantry.</summary>
    public sealed class ShopJob : Job
    {
        readonly Walker walker;
        readonly ShopKind shop;
        readonly Vector2Int pantry;
        int wanted;
        int carried;
        bool bought;

        public ShopJob(Pawn pawn, Vector2Int counter, ShopKind shop, int units, Vector2Int pantry, Game game) : base(pawn)
        {
            walker = new Walker(counter, adjacent: true);
            this.shop = shop;
            this.pantry = pantry;
            wanted = units;
            game.Stock.Shopper = pawn;
        }

        int Price => shop == ShopKind.Farmacia ? Prices.Medicine : Prices.Groceries;
        string ItemName => shop == ShopKind.Farmacia ? "medicine" : "groceries";

        public override string Label => bought ? $"Bringing {ItemName} home" : $"Shopping at the {shop.ToString().ToLower()}";
        public override bool IsWork => true;

        public override bool Tick(Game game)
        {
            var status = walker.Tick(Pawn, game);
            if (status == WalkStatus.Failed) return Fail();
            if (status == WalkStatus.Walking) return false;

            if (!bought)
            {
                int units = Mathf.Min(wanted, game.Stock.Cash / Price);
                if (units <= 0) return Fail();
                game.Stock.Cash -= units * Price;
                carried = units;
                Pawn.Carrying = $"{units} {ItemName}";
                bought = true;
                walker.Retarget(pantry);
                return false;
            }

            Deposit(game);
            return true;
        }

        void Deposit(Game game)
        {
            if (carried <= 0) return;
            if (shop == ShopKind.Farmacia) game.Stock.Medicine += carried;
            else game.Stock.Groceries += carried;
            carried = 0;
        }

        public override void OnEnd(Game game)
        {
            Deposit(game); // never lose what was paid for, even if interrupted
            Pawn.Carrying = null;
            if (game.Stock.Shopper == Pawn) game.Stock.Shopper = null;
        }
    }
}
