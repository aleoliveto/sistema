using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.AI
{
    /// <summary>
    /// One of the boys goes to Zì Tore with the family's cash, buys, and brings it home under his
    /// jacket. The boss's orders from inside, or just the routine when he can't go himself.
    /// </summary>
    public sealed class SupplyRunJob : Job
    {
        readonly int grams, price;
        Walker walker;
        bool bought;
        int waited;

        public SupplyRunJob(Pawn pawn, int grams, int price) : base(pawn)
        {
            this.grams = grams;
            this.price = price;
        }

        public override string Label => bought ? $"Bringing {grams} g home" : "Going to see Zì Tore";

        public override bool Tick(Game game)
        {
            if (!bought)
            {
                var tore = game.Contacts.ByName(ContactBook.Supplier);
                if (tore == null) return Fail();
                if (!tore.Present)
                {
                    // He keeps his own hours: wait for him, up to a day.
                    if (++waited > GameClock.TicksPerDay) return Fail();
                    return false;
                }
                if (walker == null) walker = new Walker(Vector2Int.FloorToInt(tore.Position), adjacent: true) { Flexible = true };
                var status = walker.Tick(Pawn, game);
                if (status == WalkStatus.Walking) return false;
                if (status == WalkStatus.Failed) return Fail();
                if (!game.Org.Pay(price, game, out var error))
                {
                    game.Log.Add($"{Pawn.Nickname} couldn't pay Zì Tore: {error}", MessageKind.Bad);
                    return Fail();
                }
                bought = true;
                walker = new Walker(Vector2Int.RoundToInt(game.PlayerBase.center), adjacent: false) { Flexible = true };
                return false;
            }
            var home = walker.Tick(Pawn, game);
            if (home == WalkStatus.Walking) return false;
            game.Stock.BulkDg += grams * 10;
            game.Log.Add($"{Pawn.Nickname} is back from Zì Tore with {grams} g. It's in the stash, ready to cut.", MessageKind.Good);
            return true;
        }
    }
}
