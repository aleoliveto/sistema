using OSistema.Pawns;
using OSistema.World;

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
        public int ScooterModel;   // the boss's ride: 0 step-through .. 3 maxi (see Prices.ScooterModels)
        public int ScooterColour;
        public int Cigarettes; // packs of ROSSE, ready to sell

        // Production
        public int BulkDg;         // product not yet bagged, in decigrams
        public int Baggies;
        public int TobaccoG;
        public int Papers;         // papers with filters
        public int EmptyPacks;
        public bool DigitalScale, HeatSealer, RollingMachine;
        public float DoseQuality = 0.75f;   // how honest the doses are (0..1): short bags lose customers
        public float PackQuality = 0.75f;   // how good the cigarettes are: sets the price

        // Furniture bought and not yet put down, by BuildingDefs.All index.
        public int[] Stored = new int[BuildingDefs.All.Length];

        public int StoredOf(BuildingDef d)
        {
            int i = System.Array.IndexOf(BuildingDefs.All, d);
            return i >= 0 && i < Stored.Length ? Stored[i] : 0;
        }

        public void Store(BuildingDef d, int n = 1)
        {
            int i = System.Array.IndexOf(BuildingDefs.All, d);
            if (i < 0) return;
            if (Stored.Length < BuildingDefs.All.Length) System.Array.Resize(ref Stored, BuildingDefs.All.Length);
            Stored[i] = System.Math.Max(0, Stored[i] + n);
        }

        // Only one pawn cooks and one shops at a time, so they don't all leave together.
        public Pawn Cook;
        public Pawn Shopper;
    }
}
