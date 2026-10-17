using OSistema.AI;
using OSistema.Core;
using OSistema.Npcs;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// The street, done by hand. You stand on a corner with doses in your pocket and sell them one by
    /// one as people come; you walk into a shop and ask for the pizzo yourself. Later, pushers and a
    /// collector do it for you.
    /// </summary>
    public sealed partial class Organization
    {
        public DrugSquare DealingCorner;

        public bool CanDealHere(Pawn p, Game game)
        {
            if (p == null || !p.CanAct || DealingCorner != null || game.Stock.Product <= 0) return false;
            var t = game.Map.GetTerrain(p.Cell.x, p.Cell.y);
            return (t == TerrainType.Sidewalk || t == TerrainType.Paving) && game.Territory.ZoneAt(p.Cell) != null;
        }

        public void StartDealing(Game game)
        {
            var p = game.Player;
            if (!CanDealHere(p, game)) return;
            var zone = game.Territory.ZoneAt(p.Cell);
            DealingCorner = new DrugSquare { Cell = p.Cell, ZoneId = zone.Id, OpenHour = 0, CloseHour = 0 };
            WarnIfRivalZone(zone, game);
            game.Log.Add($"You lean against the wall in {zone.Name} with {game.Stock.Product} doses in your pocket. Word gets round. Press E when someone comes.", MessageKind.Info, p.Cell);
        }

        public void StopDealing(Game game, bool quiet = false)
        {
            if (DealingCorner == null) return;
            if (!quiet) game.Log.Add("You leave the corner.");
            DealingCorner = null;
        }

        /// <summary>A customer standing in front of the player, waiting to buy.</summary>
        public Npc WaitingCustomer(Game game)
        {
            var p = game.Player;
            if (DealingCorner == null || p == null) return null;
            Npc best = null;
            float bestD = 2.4f * 2.4f;
            foreach (var n in game.Npcs.All)
            {
                if (n.Kind != NpcKind.Customer || n.Despawned || n.Square != DealingCorner || n.State != 1) continue;
                float d = (n.Mover.Position - p.Position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = n; }
            }
            return best;
        }

        public void SellToCustomer(Npc customer, Game game)
        {
            var p = game.Player;
            if (customer == null || p == null || game.Stock.Product <= 0) return;
            Sell(DealingCorner, p, game);
            p.Learn(Skill.Awareness, 2f, game);
            customer.State = 2;
            customer.Walker = new Walker(customer.Exit, adjacent: false);
            var zone = game.Territory.Zones[DealingCorner.ZoneId];
            if (!game.Rival.Destroyed && game.Rival.OwnsZone(zone.Id)) game.Rival.Hostility = Mathf.Min(100f, game.Rival.Hostility + 0.6f);
            // Selling in front of a policeman.
            var cop = Pursuit.WitnessCop(game, 7f);
            if (cop != null && !game.Pursuit.Active && game.Rng.NextDouble() < 0.7)
                game.Pursuit.Start(cop, "A policeman saw you hand something over.", game);
            if (game.Stock.Product <= 0)
            {
                game.Log.Add("Out of doses. The corner can wait.", MessageKind.Info);
                StopDealing(game, quiet: true);
            }
        }

        /// <summary>Customers find the player's corner: fewer than a real piazza, more with respect.</summary>
        void SpawnCornerCustomers(Game game)
        {
            var p = game.Player;
            if (DealingCorner == null) return;
            if (p == null || !p.CanAct || (p.Position - Mover.CellCenter(DealingCorner.Cell)).sqrMagnitude > 36f)
            {
                game.Log.Add("You left your corner.");
                StopDealing(game, quiet: true);
                return;
            }
            var zone = game.Territory.Zones[DealingCorner.ZoneId];
            float perHour = 5f * zone.Demand * (0.6f + Respect / 100f) * Mathf.Max(0.2f, 1f - zone.Heat / 150f);
            if (game.Rival.OwnsZone(zone.Id)) perHour *= 0.6f;
            if (game.Rng.NextDouble() >= perHour / GameClock.TicksPerHour) return;
            var npc = game.Npcs.SpawnCustomer(DealingCorner, game);
            npc.Mover.Teleport(game.StreetLife.RandomDestinationNear(DealingCorner.Cell, game));
        }

        // ---------- Pizzo, in person ----------

        public float PizzoChance(Business b, Pawn p) =>
            Mathf.Clamp(0.35f + 0.05f * p.IntimidationSkill + Respect / 150f - b.Defiance * 0.5f, 0.05f, 0.95f);

        /// <summary>The boss asks the owner himself. Yes: he pays every week. No: he's refusing.</summary>
        public void DemandPizzo(Business b, Game game)
        {
            var p = game.Player;
            if (p == null) return;
            if (game.Rng.NextDouble() < PizzoChance(b, p))
            {
                b.Status = BusinessStatus.Paying;
                b.LastCollectedWeek = WeekOf(game.Clock) - 1;   // first payment due now
                Respect = Mathf.Min(100f, Respect + 1f);
                p.Learn(Skill.Intimidation, 8f, game);
                game.Log.Add($"The owner of {b.Name} looked at the floor and said yes. €{b.WeeklyPizzo} a week.", MessageKind.Good, b.Cell);
            }
            else
            {
                b.Status = BusinessStatus.Refusing;
                b.Defiance = Mathf.Min(1f, b.Defiance + 0.05f);
                p.Learn(Skill.Intimidation, 4f, game);
                game.Log.Add($"The owner of {b.Name} told you to get out of his shop.", MessageKind.Bad, b.Cell);
                if (game.Rng.NextDouble() < b.Defiance * 0.4f)
                {
                    game.Territory.AddHeat(b.Cell, 8f);
                    game.Log.Add("He picked up the phone as you left. Heat is rising.", MessageKind.Bad);
                }
            }
        }

        public void CollectPizzoInPerson(Business b, Game game)
        {
            int week = WeekOf(game.Clock);
            if (b.Status != BusinessStatus.Paying || b.LastCollectedWeek >= week) return;
            b.LastCollectedWeek = week;
            game.Stock.Cash += b.WeeklyPizzo;
            game.PopupAt(Mover.CellCenter(b.Cell), $"+€{b.WeeklyPizzo}");
            game.Log.Add($"An envelope from {b.Name}: €{b.WeeklyPizzo}.", MessageKind.Good, b.Cell);
        }

        public void ScareInPerson(Business b, Game game)
        {
            var p = game.Player;
            if (p == null) return;
            Scare(b, p, game.Territory.ZoneAt(b.Cell), game);
            b.Status = BusinessStatus.Refusing;   // he asks again himself, not the collector
        }
    }
}
