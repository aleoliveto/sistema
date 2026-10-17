using OSistema.Core;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>Buying houses and shops. Owned shops are fronts: legal income that also launders.</summary>
    public sealed partial class Organization
    {
        const float LaunderingPerFrontPerDay = 0.5f;

        public bool BuyProperty(Property p, Game game, out string error)
        {
            error = null;
            if (p.OwnedByPlayer) error = "Already yours.";
            else if (p.Kind == PropertyKind.Civic) error = "Not for sale.";
            else if (p.Kind == PropertyKind.Shop && Rank < 2) error = $"Buying a shop as a front needs the family to be a {RankNames[2]}.";
            else if (game.Stock.Cash < p.Price) error = $"You need €{p.Price}.";
            if (error != null) return false;

            game.Stock.Cash -= p.Price;
            p.OwnedByPlayer = true;

            var business = Businesses.Find(b => p.Rect.Contains(b.Cell));
            if (business != null)
            {
                business.Status = BusinessStatus.Owned;
                business.PendingAction = Retaliation.None;
                game.Log.Add($"{business.Name} is yours now: a clean front with clean money. Weekly income €{FrontIncome(business)}.", MessageKind.Good, business.Cell);
            }
            else game.Log.Add($"Bought {p.Name} for €{p.Price}. The crew can sleep, hide and lie low there.", MessageKind.Good, p.Door);

            // Owning bricks and mortar in the rione earns respect.
            Respect = Mathf.Min(100f, Respect + (p.Kind == PropertyKind.Shop ? 3f : 1f));
            return true;
        }

        public static int FrontIncome(Business b) => b.WeeklyPizzo * 2;

        /// <summary>How nice the family's homes are, per person: furniture, plants, a shrine, a TV.</summary>
        public float HomeComfort { get; private set; }

        void UpdateHomeComfort(Game game)
        {
            float beauty = 0f;
            var map = game.Map;
            void Count(RectInt r)
            {
                foreach (var c in GridUtil.Cells(r, false))
                {
                    var b = map.InBounds(c) ? map.GetBuilding(c) : null;
                    if (b != null) beauty += b.Beauty;
                }
            }
            Count(game.PlayerBase);
            foreach (var p in game.Properties.Owned)
                if (p.Kind == PropertyKind.Home && !p.Interior.Overlaps(game.PlayerBase)) Count(p.Interior);
            HomeComfort = beauty / Mathf.Max(1, game.Pawns.PresentCount);

            var (label, value) = HomeComfort < 1.5f ? ("A squalid basso", -4f)
                : HomeComfort < 3.5f ? (null, 0f)
                : HomeComfort < 6f ? ("A decent home", 2f)
                : ("A beautiful home", 5f);
            long now = game.Ticks.TicksGame;
            foreach (var pawn in game.Pawns.Crew)
            {
                if (!pawn.IsPresent) continue;
                pawn.Mood.Memories.RemoveAll(m => m.Id == "home");
                if (label != null) pawn.Mood.AddMemory("home", label, value, now, GameClock.TicksPerHour * 2);
            }
        }

        /// <summary>Sunday: owned shops pay out. Laundering also makes the investigators' job harder.</summary>
        void PayFronts(Game game)
        {
            int total = 0;
            foreach (var b in Businesses)
                if (b.Status == BusinessStatus.Owned) total += FrontIncome(b);
            if (total == 0) return;
            game.Stock.Cash += total;
            game.Log.Add($"Your shops made €{total} this week. On paper, all of it legal.", MessageKind.Good);
        }

        float FrontsLaundering()
        {
            int fronts = 0;
            foreach (var b in Businesses)
                if (b.Status == BusinessStatus.Owned) fronts++;
            return fronts * LaunderingPerFrontPerDay;
        }
    }
}
