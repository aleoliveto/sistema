using OSistema.Core;
using OSistema.Crime;
using OSistema.Npcs;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.AI
{
    /// <summary>
    /// Carrying out a lavoretto, step by step. Progress is stored on the contract, so if the crew member
    /// stops to eat or sleep they pick up where they left off.
    /// </summary>
    public sealed class ContractJob : Job
    {
        // Contract.Stage values
        const int GoingToPickup = 0, AtPickup = 1, GoingToDrop = 2, AtDrop = 3;

        readonly Contract contract;
        Walker walker;

        public ContractJob(Pawn pawn, Contract contract) : base(pawn) => this.contract = contract;

        public override bool IsWork => true;

        public override string Label => contract.Kind switch
        {
            ContractKind.Delivery => contract.Stage < GoingToDrop ? "Picking up a parcel" : "Delivering a parcel",
            ContractKind.Debt => "Collecting a debt",
            ContractKind.ScooterTheft => contract.Stage < GoingToDrop ? "Stealing a scooter" : "Riding a stolen scooter",
            _ => contract.Stage == AtPickup ? "Waiting for the boat" : contract.Stage < AtPickup ? "Going down to the sea" : "Carrying contraband",
        };

        public override bool Tick(Game game)
        {
            if (contract.State != ContractState.Active) return true;
            var c = contract;

            // The smugglers' boat only comes at night.
            if (c.Kind == ContractKind.Smuggling && c.Stage <= AtPickup)
            {
                int h = game.Clock.Hour;
                bool boatHours = h >= 22 || h < 4;
                if (c.Stage == AtPickup)
                {
                    if (h >= 4 && h < 12)
                    {
                        game.Contracts.Finish(c, false, "the boat never came. Dawn broke on an empty sea.", game);
                        return true;
                    }
                    if (!boatHours) return false; // waiting at the landing, looking out to sea
                }
                else if (!(h >= 21 || h < 4)) return true; // too early to head down
            }

            switch (c.Stage)
            {
                case GoingToPickup:
                    if (!c.From.HasValue)
                    {
                        c.Stage = GoingToDrop;
                        return false;
                    }
                    if (!Walk(c.From.Value, c.Kind != ContractKind.Smuggling, game, 1f)) return false;
                    c.Stage = AtPickup;
                    c.Work = c.Kind == ContractKind.ScooterTheft ? 240 : c.Kind == ContractKind.Smuggling ? 700 : 90;
                    return false;

                case AtPickup:
                    if (--c.Work > 0) return false;
                    return Pickup(game);

                case GoingToDrop:
                {
                    if (CarriesRisk() && game.Ticks.TicksGame % 60 == 0 && StoppedByPolice(game)) return true;
                    float speed = c.Kind == ContractKind.ScooterTheft ? 2.4f : 1f;
                    if (!Walk(c.To, true, game, speed)) return false;
                    c.Stage = AtDrop;
                    c.Work = c.Kind == ContractKind.Debt ? 300 : 60;
                    return false;
                }

                default:
                    if (--c.Work > 0) return false;
                    Deliver(game);
                    return true;
            }
        }

        bool CarriesRisk() => contract.Kind == ContractKind.Delivery || contract.Kind == ContractKind.Smuggling;

        /// <summary>Walk to a cell; true once there. A failed path abandons the job.</summary>
        bool Walk(Vector2Int target, bool adjacent, Game game, float speed)
        {
            walker ??= new Walker(target, adjacent, speed);
            var status = walker.Tick(Pawn, game);
            if (status == WalkStatus.Walking) return false;
            walker = null;
            if (status == WalkStatus.Failed)
            {
                game.Contracts.Finish(contract, false, $"{Pawn.DisplayName} couldn't find a way there.", game);
                return false;
            }
            return true;
        }

        bool Pickup(Game game)
        {
            var c = contract;
            switch (c.Kind)
            {
                case ContractKind.ScooterTheft:
                {
                    var at = c.From.Value;
                    if (game.Map.GetBuilding(at) != BuildingDefs.ParkedScooter)
                    {
                        game.Contracts.Finish(c, false, "someone rode off on the scooter first.", game);
                        return true;
                    }
                    game.Map.SetBuilding(at.x, at.y, null);
                    if (Witnessed(game, at))
                    {
                        game.Territory.AddHeat(at, 8f);
                        game.Log.Add("A passer-by saw the scooter being taken. Heat is rising.", MessageKind.Bad, at);
                    }
                    Pawn.Carrying = "a stolen scooter";
                    break;
                }
                case ContractKind.Smuggling:
                {
                    // The Guardia di Finanza sometimes watches the coast. Sharp eyes help.
                    float chance = Mathf.Max(0.04f, 0.16f - Pawn.AwarenessSkill * 0.012f);
                    if (game.Rng.NextDouble() < chance)
                    {
                        Arrest(game, "the Guardia di Finanza was waiting at the landing.");
                        return true;
                    }
                    Pawn.Carrying = "cases of cigarettes";
                    break;
                }
                default:
                    Pawn.Carrying = "a parcel";
                    break;
            }
            c.Stage = GoingToDrop;
            return false;
        }

        void Deliver(Game game)
        {
            var c = contract;
            Pawn.Carrying = null;
            if (c.Kind == ContractKind.Debt)
            {
                float chance = Mathf.Clamp(0.35f + 0.06f * Pawn.IntimidationSkill, 0.1f, 0.95f);
                if (game.Rng.NextDouble() < chance)
                {
                    Pawn.Learn(Skill.Intimidation, 14f, game);
                    Pawn.Say("debt_paid");
                    game.Contracts.Finish(c, true, "he found the money after all.", game);
                }
                else
                {
                    Pawn.Learn(Skill.Intimidation, 6f, game);
                    if (game.Rng.NextDouble() < 0.3)
                    {
                        game.Territory.AddHeat(c.To, 6f);
                        game.Log.Add("The neighbours heard the shouting and called the police.", MessageKind.Bad, c.To);
                    }
                    game.Contracts.Finish(c, false, "he swore he has nothing. Next time, more conviction.", game);
                }
                return;
            }
            Pawn.Learn(Skill.Awareness, c.Kind == ContractKind.Smuggling ? 14f : 8f, game);
            game.Contracts.Finish(c, true, c.Kind switch
            {
                ContractKind.ScooterTheft => "Nanninella is happy with the scooter.",
                ContractKind.Smuggling => "the cases are stacked in the basso, ready for the stalls.",
                _ => "the parcel arrived unopened.",
            }, game);
        }

        static bool Witnessed(Game game, Vector2Int at)
        {
            foreach (var npc in game.Npcs.All)
            {
                if (npc.Kind != NpcKind.Civilian && npc.Kind != NpcKind.Elder) continue;
                if ((npc.Mover.Position - Mover.CellCenter(at)).sqrMagnitude > 49f) continue;
                if (Combat.CombatSystem.HasLineOfSight(game.Map, npc.Mover.Cell, at)) return true;
            }
            return false;
        }

        /// <summary>A patrol nearby may stop and search someone carrying something.</summary>
        bool StoppedByPolice(Game game)
        {
            foreach (var npc in game.Npcs.All)
            {
                if (npc.Kind != NpcKind.Police) continue;
                if ((npc.Mover.Position - Pawn.Position).sqrMagnitude > 25f) continue;
                if (!Combat.CombatSystem.HasLineOfSight(game.Map, npc.Mover.Cell, Pawn.Cell)) continue;
                float chance = Mathf.Max(0.05f, 0.35f - Pawn.AwarenessSkill * 0.02f);
                if (game.Rng.NextDouble() >= chance) return false; // they didn't look twice
                Arrest(game, "the police stopped and searched them.");
                return true;
            }
            return false;
        }

        void Arrest(Game game, string why)
        {
            Pawn.Carrying = null;
            int days = game.Rng.Next(2, 5);
            game.Contracts.Finish(contract, false, why, game);
            Pawn.SendToJail(game.Ticks.TicksGame + days * GameClock.TicksPerDay, game);
            game.Org.AddEvidence(3f, game);
            game.Log.Add($"{Pawn.DisplayName} is in Poggioreale for {days} days.", MessageKind.Bad);
        }

        public override void OnEnd(Game game)
        {
            if (contract.State != ContractState.Active) Pawn.Carrying = null;
        }
    }
}
