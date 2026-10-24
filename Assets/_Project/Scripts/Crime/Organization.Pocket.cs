using OSistema.Core;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// The boss's pockets. What's for sale sits in the stash at the basso (Stock); to sell it by hand he has to
    /// fill his pockets there first, and what he carries is what the police find when they search him.
    /// </summary>
    public sealed partial class Organization
    {
        public const int PocketMaxDoses = 30, PocketMaxPacks = 40, PocketMaxFakes = 15;
        /// <summary>What a pusher takes from the stash for a run at the piazza; under this many left, he fetches more.</summary>
        public const int PusherLoad = 25, PusherLow = 3;

        /// <summary>Where the stash is: the safe in the basso if there is one, else the middle of the basso.</summary>
        public Vector2Int StashCell(Game game)
        {
            var b = game.PlayerBase;
            foreach (var c in game.Map.PositionsOf(BuildingDefs.Safe)) if (b.Contains(c)) return c;
            var mid = Vector2Int.RoundToInt(b.center);
            foreach (var c in GridUtil.CellsAround(mid, 4))
                if (b.Contains(c) && game.Map.IsPassable(c)) return c;
            return mid;
        }

        /// <summary>A pusher (or capo) at the stash: he takes a load for the piazza.</summary>
        public void LoadPusher(Pawn p, Game game)
        {
            int n = System.Math.Min(game.Stock.Product, PusherLoad - p.PocketDoses);
            if (n <= 0) return;
            game.Stock.Product -= n;
            p.PocketDoses += n;
        }

        /// <summary>Anything in the stash that would still fit in his pockets.</summary>
        public bool CanFillPockets(Pawn p, Game game) =>
            p != null && ((game.Stock.Product > 0 && p.PocketDoses < PocketMaxDoses) ||
                          (game.Stock.Cigarettes > 0 && p.PocketPacks < PocketMaxPacks) ||
                          (H.fakes > 0 && p.PocketFakes < PocketMaxFakes));

        /// <summary>Take what fits from the stash.</summary>
        public void FillPockets(Pawn p, Game game)
        {
            var s = game.Stock;
            int d = System.Math.Min(s.Product, PocketMaxDoses - p.PocketDoses);
            int k = System.Math.Min(s.Cigarettes, PocketMaxPacks - p.PocketPacks);
            int f = System.Math.Min(H.fakes, PocketMaxFakes - p.PocketFakes);
            if (d > 0) { s.Product -= d; p.PocketDoses += d; }
            if (k > 0) { s.Cigarettes -= k; p.PocketPacks += k; }
            if (f > 0) { H.fakes -= f; p.PocketFakes += f; }
            game.Log.Add($"You fill your pockets: {p.PocketText}. G (or E) sells from them at your corner, the stall or a piazza.", MessageKind.Info);
        }

        /// <summary>Everything back in the stash: nothing on him if he's stopped.</summary>
        public void EmptyPockets(Pawn p, Game game)
        {
            game.Stock.Product += p.PocketDoses;
            game.Stock.Cigarettes += p.PocketPacks;
            H.fakes += p.PocketFakes;
            p.PocketDoses = p.PocketPacks = p.PocketFakes = 0;
            game.Log.Add("You put everything back in the stash. Nothing on you if they stop you.", MessageKind.Info);
        }
    }
}
