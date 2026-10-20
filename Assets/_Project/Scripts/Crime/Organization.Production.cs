using OSistema.Core;
using OSistema.Npcs;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// Making the goods. Product comes from Zi Tore by the etto, half kilo or kilo and has to be carried
    /// home and cut into half-gram doses; cigarettes are rolled at home from tobacco bought at the
    /// tabaccaio and boxed in ROSSE packs. By hand at first; later, people on the tables do it for you.
    /// </summary>
    public sealed partial class Organization
    {
        public int CarriedDg;            // product on the boss, not yet home
        public int TotonnoPacksDay, TotonnoPacksToday;
        /// <summary>How many packs Totonno takes a day: enough that rolling pays the first week's bills.</summary>
        public const int TotonnoDailyPacks = 25;
        long lastCarryCheck;
        float lastQualityWarning = -999f;

        /// <summary>Drugs are paid in cash, however much: nobody launders money to buy cocaine.</summary>
        public bool PayCash(int amount, Game game, out string error)
        {
            error = null;
            var s = game.Stock;
            if (s.Cash + s.Clean < amount)
            {
                error = $"Not enough money (€{amount} needed).";
                return false;
            }
            int fromCash = Mathf.Min(s.Cash, amount);
            s.Cash -= fromCash;
            s.Clean -= amount - fromCash;
            return true;
        }

        public bool BuyBulk(int grams, int price, Game game, out string error)
        {
            error = null;
            if (!SupplierIntroduced) error = "Nobody sells product to a stranger. Someone has to vouch for you.";
            else if (CarriedDg > 0) error = "Get the last lot home first.";
            if (error != null) return false;
            if (!PayCash(price, game, out error)) return false;
            CarriedDg = grams * 10;
            game.Log.Add($"{grams} g wrapped in cling film, under your jacket. Get it home to the basso without meeting the police.", MessageKind.Info);
            return true;
        }

        /// <summary>Product on the boss's person: home safe, or stopped and searched on the way.</summary>
        void TickCarrying(Game game)
        {
            TickParcel(game);
            if (CarriedDg <= 0) return;
            long now = game.Ticks.TicksGame;
            if (now - lastCarryCheck < 60) return;
            lastCarryCheck = now;
            var p = game.Player;
            if (p == null || !p.IsPresent)
            {
                game.Log.Add("The product the boss was carrying is gone.", MessageKind.Bad);
                CarriedDg = 0;
                return;
            }
            if (game.PlayerBase.Contains(p.Cell) || game.Properties.IsOwnedHome(p.Cell))
            {
                game.Stock.BulkDg += CarriedDg;
                game.Log.Add($"{CarriedDg / 10} g are in the stash. Cut it into doses at the bagging table.", MessageKind.Good);
                CarriedDg = 0;
                return;
            }
            // A patrol that takes a good look at a nervous kid with a bulge in his jacket.
            if (game.Pursuit.Active) return;
            var cop = Pursuit.WitnessCop(game, 4f);
            if (cop == null) return;
            float chance = Mathf.Max(0.01f, 0.06f - p.AwarenessSkill * 0.004f) * (p.Riding ? 0.5f : 1f);
            if (game.Rng.NextDouble() < chance) game.Pursuit.Start(cop, "A patrol wants to search you, and you're carrying.", game);
        }

        // A parcel, a stolen scooter, cases of cigarettes: anything carried for a job can get you stopped.
        void TickParcel(Game game)
        {
            var p = game.Player;
            if (p == null || p.Carrying == null || game.Pursuit.Active || game.Ticks.TicksGame % 60 != 0) return;
            var cop = Pursuit.WitnessCop(game, 5f);
            if (cop == null) return;
            float chance = Mathf.Max(0.01f, 0.05f - p.AwarenessSkill * 0.003f);
            if (game.Rng.NextDouble() < chance) game.Pursuit.Start(cop, $"A patrol wants a look at {p.Carrying}.", game);
        }

        /// <summary>One bag, weighed by hand. Short bags cheat customers; heavy ones give product away.</summary>
        public void MakeDose(float grams, Game game)
        {
            var s = game.Stock;
            int dg = Mathf.Clamp(Mathf.RoundToInt(grams * 10f), 1, s.BulkDg);
            s.BulkDg -= dg;
            s.Baggies--;
            float err = (grams - Prices.DoseDg / 10f) / (Prices.DoseDg / 10f);
            float q = err < 0f ? 1f - Mathf.Clamp01(-err * 3f) : 1f - Mathf.Clamp01(err * 0.8f);
            // The stock's honesty is the average of the bags in it: each bag counts the same.
            s.DoseQuality = s.Product <= 0 ? q : (s.DoseQuality * s.Product + q) / (s.Product + 1);
            s.Product++;
        }

        public bool CanMakeDose(Game game) => game.Stock.BulkDg >= 2 && game.Stock.Baggies > 0;

        public bool CanMakePack(Game game)
        {
            var s = game.Stock;
            return s.TobaccoG >= 8 && s.Papers >= 20 && s.EmptyPacks > 0;
        }

        /// <summary>A pack of 20, rolled and boxed. Uses the tobacco actually put in the trough.</summary>
        public void MakePack(int tobaccoG, float quality, Game game)
        {
            var s = game.Stock;
            s.TobaccoG = Mathf.Max(0, s.TobaccoG - tobaccoG);
            s.Papers -= 20;
            s.EmptyPacks--;
            // The quality of the packs in stock is their average: a good pack never drags the rest down.
            float q = Mathf.Clamp01(quality);
            s.PackQuality = s.Cigarettes <= 0 ? q : (s.PackQuality * s.Cigarettes + q) / (s.Cigarettes + 1);
            s.Cigarettes++;
        }

        public int PackPrice(Game game) => Mathf.Max(1, Mathf.RoundToInt(Prices.PackStreet * (0.5f + 0.7f * game.Stock.PackQuality)));
        public int TotonnoPrice(Game game) => Mathf.Max(1, PackPrice(game) - 1);
        public int TotonnoWants(Game game)
        {
            if (TotonnoPacksDay != game.Clock.Day)
            {
                TotonnoPacksDay = game.Clock.Day;
                TotonnoPacksToday = 0;
            }
            return Mathf.Max(0, TotonnoDailyPacks - TotonnoPacksToday);
        }

        public void SellPacksToTotonno(Game game)
        {
            int n = Mathf.Min(game.Stock.Cigarettes, TotonnoWants(game));
            if (n <= 0) return;
            int price = TotonnoPrice(game);
            game.Stock.Cigarettes -= n;
            game.Stock.Cash += n * price;
            TotonnoPacksToday += n;
            game.Log.Add($"Zio Totonno took {n} packs of ROSSE for under the counter: €{n * price}.", MessageKind.Good);
        }

        /// <summary>Customers notice light bags.</summary>
        void CheckDoseComplaints(Game game)
        {
            float q = game.Stock.DoseQuality;
            if (q > 0.65f || game.Rng.NextDouble() > (0.65f - q) * 0.6f) return;
            Respect = Mathf.Max(0f, Respect - 0.15f);
            if (Time.unscaledTime - lastQualityWarning < 60f) return;
            lastQualityWarning = Time.unscaledTime;
            game.Log.Add("Customers say the bags are light. Weigh them properly.", MessageKind.Bad);
        }
    }
}
