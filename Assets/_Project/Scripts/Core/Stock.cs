using OSistema.Pawns;

namespace OSistema.Core
{
    /// <summary>
    /// The crew's shared supplies. Kept as simple counters (no item hauling) on purpose:
    /// the game is about people and pressure, not logistics.
    /// </summary>
    public sealed class Stock
    {
        /// <summary>Dirty money ('a bacinella): what crime pays. Fine for drugs, guns, wages, small things.</summary>
        public int Cash = 400;

        /// <summary>Clean money: legal income and laundered cash. Needed for anything big.</summary>
        public int Clean;
        public int Groceries = 6;
        public int Meals = 4;
        public int Medicine = 1;
        public int Product;   // 'a roba, stored at the basso
        public int Pistols;   // unassigned guns in the stash
        public int Scooters;  // the crew's own scooters
        public int Cigarettes; // cases of contraband cigarettes at the deposito

        // Only one pawn cooks and one shops at a time, so they don't all leave together.
        public Pawn Cook;
        public Pawn Shopper;
    }
}
