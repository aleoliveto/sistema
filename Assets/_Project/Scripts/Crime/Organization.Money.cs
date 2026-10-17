using OSistema.Core;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// Dirty and clean money. Crime pays in cash nobody can explain; shops you own wash it a little at a
    /// time for a small cut, the commercialista washes it fast for a big one. Property and anything
    /// expensive has to be paid with clean money.
    /// </summary>
    public sealed partial class Organization
    {
        public const float FrontLaunderingFee = 0.10f;
        public const int FrontLaunderingCapacity = 1500;   // per shop, per week
        public const float CommercialistaFee = 0.30f;
        const int SuspiciousHoard = 20000;

        public int FrontCount
        {
            get
            {
                int n = 0;
                foreach (var b in Businesses) if (b.Status == BusinessStatus.Owned) n++;
                return n;
            }
        }

        public int WeeklyLaunderingCapacity => FrontCount * FrontLaunderingCapacity;

        /// <summary>Pay for something: big amounts must be clean, small ones are paid dirty first.</summary>
        public bool Pay(int amount, Game game, out string error)
        {
            error = null;
            var s = game.Stock;
            if (amount >= Prices.CleanMoneyThreshold)
            {
                if (s.Clean < amount)
                {
                    error = $"€{amount} has to be clean money: nobody pays that in cash without questions. Clean: €{s.Clean}. Launder dirty money through your shops or the commercialista.";
                    return false;
                }
                s.Clean -= amount;
                return true;
            }
            if (s.Cash >= amount) s.Cash -= amount;
            else if (s.Cash + s.Clean >= amount)
            {
                s.Clean -= amount - s.Cash;
                s.Cash = 0;
            }
            else
            {
                error = $"Not enough money (€{amount} needed).";
                return false;
            }
            return true;
        }

        public bool LaunderViaCommercialista(int amount, Game game, out string error)
        {
            error = null;
            if (game.Stock.Cash < amount)
            {
                error = $"You don't have €{amount} in dirty cash.";
                return false;
            }
            int clean = Mathf.RoundToInt(amount * (1f - CommercialistaFee));
            game.Stock.Cash -= amount;
            game.Stock.Clean += clean;
            AddEvidence(1f, game); // every middleman is a risk
            game.Log.Add($"The commercialista washed €{amount}: €{clean} came back clean.", MessageKind.Info);
            return true;
        }

        /// <summary>Sunday: shops wash dirty money, then the bills arrive.</summary>
        void WeeklyMoney(Game game)
        {
            var s = game.Stock;
            int capacity = WeeklyLaunderingCapacity;
            if (capacity > 0 && s.Cash > 0)
            {
                int amount = Mathf.Min(s.Cash, capacity);
                int clean = Mathf.RoundToInt(amount * (1f - FrontLaunderingFee));
                s.Cash -= amount;
                s.Clean += clean;
                game.Log.Add($"Your shops laundered €{amount} this week: €{clean} is now clean.", MessageKind.Good);
            }

            int homes = 0, operations = 0;
            foreach (var p in game.Properties.Owned)
            {
                if (p.Kind == PropertyKind.Home) homes++;
                if (p.Operation != Operation.None) operations++;
            }
            int bills = homes * Prices.BillsPerHome + operations * Prices.BillsPerOperation;
            if (bills > 0)
            {
                if (Pay(bills, game, out _)) game.Log.Add($"Bills for the week: €{bills} (light, gas, rent for the operations).");
                else
                {
                    Respect = Mathf.Max(0f, Respect - 2f);
                    game.Log.Add($"Couldn't pay €{bills} in bills. The lights went out at the basso, and people noticed.", MessageKind.Bad);
                }
            }
        }

        void DailyMoney(Game game)
        {
            // A mountain of cash under the bed is evidence in itself.
            if (game.Stock.Cash > SuspiciousHoard)
            {
                AddEvidence(1f, game);
                game.Log.Add($"€{game.Stock.Cash} in dirty cash is hard to hide. Launder it.", MessageKind.Bad);
            }
        }
    }
}
