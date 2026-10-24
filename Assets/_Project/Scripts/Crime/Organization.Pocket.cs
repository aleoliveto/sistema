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
        public void LoadPusher(Pawn p, DrugSquare sq, Game game)
        {
            int want = (RunnerFor(sq, game) != null ? PusherWithRunner : PusherLoad) - p.PocketDoses;
            if (want > 0) p.PocketDoses += TakeDoses(StashFor(sq, game), want, game);
        }

        /// <summary>Anything in the stash that would still fit in his pockets.</summary>
        public bool CanFillPockets(Pawn p, Game game) =>
            p != null && ((DosesIn(NascondiglioAt(p.Cell, game), game) > 0 && p.PocketDoses < PocketMaxDoses) ||
                          (NascondiglioAt(p.Cell, game) == null &&
                           ((game.Stock.Cigarettes > 0 && p.PocketPacks < PocketMaxPacks) || (H.fakes > 0 && p.PocketFakes < PocketMaxFakes))));

        /// <summary>Take what fits from the stash.</summary>
        public void FillPockets(Pawn p, Game game)
        {
            var s = game.Stock;
            var here = NascondiglioAt(p.Cell, game);
            // The boss carries what fits in his bag's slots; the crew, what fits in their pockets.
            bool boss = p == game.Player;
            int Fit(Item it, int max, int have) => boss ? System.Math.Min(max - have, Bag.Room(p, it)) : max - have;
            int d = TakeDoses(here, Fit(Item.Doses, PocketMaxDoses, p.PocketDoses), game);
            // Packs and fakes are kept at the basso; a nascondiglio only hides doses.
            int k = here != null ? 0 : System.Math.Min(s.Cigarettes, Fit(Item.Packs, PocketMaxPacks, p.PocketPacks));
            int f = here != null ? 0 : System.Math.Min(H.fakes, Fit(Item.Fakes, PocketMaxFakes, p.PocketFakes));
            if (boss && d + k + f <= 0 && Fit(Item.Doses, PocketMaxDoses, p.PocketDoses) <= 0 && Fit(Item.Packs, PocketMaxPacks, p.PocketPacks) <= 0) { game.Log.Add("Your bag is full (I to sort it).", MessageKind.Bad); return; }
            if (d > 0) p.PocketDoses += d;
            if (k > 0) { s.Cigarettes -= k; p.PocketPacks += k; }
            if (f > 0) { H.fakes -= f; p.PocketFakes += f; }
            if (boss) Bag.Sync(p, game);
            game.Log.Add($"You fill your pockets: {p.PocketText}. G (or E) sells from them at your corner, the stall or a piazza.", MessageKind.Info);
        }

        /// <summary>Everything back in the stash: nothing on him if he's stopped.</summary>
        public void EmptyPockets(Pawn p, Game game)
        {
            var here = NascondiglioAt(p.Cell, game);
            PutDoses(here, p.PocketDoses, game);
            p.PocketDoses = 0;
            if (here != null)
            {
                game.Log.Add($"You hide the doses in {here.Name}: {here.StashDoses} there now.", MessageKind.Info, here.Door);
                return;
            }
            game.Stock.Cigarettes += p.PocketPacks;
            H.fakes += p.PocketFakes;
            p.PocketPacks = p.PocketFakes = 0;
            game.Log.Add("You put everything back in the stash. Nothing on you if they stop you.", MessageKind.Info);
        }
    }
}
