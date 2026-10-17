using OSistema.Pawns;

namespace OSistema.Core
{
    /// <summary>
    /// The crew's shared supplies. Kept as simple counters (no item hauling) on purpose:
    /// the game is about people and pressure, not logistics.
    /// </summary>
    public sealed class Stock
    {
        public int Cash = 400;
        public int Groceries = 6;
        public int Meals = 4;
        public int Medicine = 1;

        // Only one pawn cooks and one shops at a time, so they don't all leave together.
        public Pawn Cook;
        public Pawn Shopper;
    }
}
