using System.Collections.Generic;
using OSistema.AI;
using OSistema.Core;
using OSistema.Npcs;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// The Ospedale and the 118. When someone lies wounded in the street, a passer-by may call an
    /// ambulance. Patients heal well in hospital, but doctors report gunshot wounds to the police.
    /// </summary>
    public sealed class Hospital
    {
        const int CheckInterval = 120;
        const float WitnessRange = 10f;
        const int LoadingTicks = 300;
        const int MinStayTicks = GameClock.TicksPerDay;

        readonly HashSet<Pawn> called = new HashSet<Pawn>();
        Vector2Int door;
        Vector2Int bay;          // the road cell where ambulances park
        bool exists;

        public Hospital(Game game)
        {
            var map = game.Map;
            if (!map.TryFindNearest(BuildingDefs.HospitalDesk, Vector2Int.zero, out var desk)) return;
            var prop = game.Properties.At(desk);
            door = prop != null ? prop.Door : desk;
            bay = NearestRoad(map, door, 30) ?? door;
            exists = true;
        }

        public bool Exists => exists;
        public bool IsCalled(Pawn p) => called.Contains(p);

        static Vector2Int? NearestRoad(Map map, Vector2Int from, int radius)
        {
            foreach (var c in GridUtil.CellsAround(from, radius))
                if (map.InBounds(c) && map.GetTerrain(c.x, c.y) == TerrainType.Road && map.IsPassable(c)) return c;
            return null;
        }

        public void Tick(Game game)
        {
            if (!exists || game.Ticks.TicksGame % CheckInterval != 0) return;

            foreach (var p in game.Pawns.All)
            {
                if (!p.IsPresent || !p.Health.Downed || p.Dead || called.Contains(p)) continue;
                if (game.PlayerBase.Contains(p.Cell) || game.Properties.IsOwnedHome(p.Cell)) continue; // indoors: nobody sees
                if (!Witnessed(p, game) || game.Rng.NextDouble() > 0.35) continue;
                Call(p, game, byPasserby: true);
            }
            called.RemoveWhere(p => p.Dead || p.InHospital || !p.Health.Downed);
        }

        static bool Witnessed(Pawn p, Game game)
        {
            foreach (var npc in game.Npcs.All)
            {
                if (npc.Kind != NpcKind.Civilian && npc.Kind != NpcKind.Elder && npc.Kind != NpcKind.Child) continue;
                if ((npc.Mover.Position - p.Position).sqrMagnitude > WitnessRange * WitnessRange) continue;
                if (OSistema.Combat.CombatSystem.HasLineOfSight(game.Map, npc.Mover.Cell, p.Cell)) return true;
            }
            return false;
        }

        /// <summary>Dispatch an ambulance to a downed person.</summary>
        public void Call(Pawn patient, Game game, bool byPasserby)
        {
            if (!exists || called.Contains(patient)) return;
            called.Add(patient);
            var scene = NearestRoad(game.Map, patient.Cell, 40) ?? patient.Cell;
            game.Npcs.SpawnAmbulance(bay, scene, patient, game);
            if (patient.Faction == Faction.Crew)
                game.Log.Add(byPasserby
                    ? $"A passer-by called 118 for {patient.DisplayName}. An ambulance is on its way."
                    : $"You called 118 for {patient.DisplayName}.", MessageKind.Info, patient.Cell);
        }

        /// <summary>The paramedics load the patient. Called by the ambulance on arrival.</summary>
        public void Admit(Pawn patient, Game game)
        {
            if (patient.Dead || !patient.IsPresent) return;
            long now = game.Ticks.TicksGame;

            bool gunshot = false;
            foreach (var injury in patient.Health.Injuries)
            {
                if (injury.IsMalnutrition) continue;
                injury.Tended = true;
                injury.TendQuality = Mathf.Max(injury.TendQuality, 0.9f);
                if (injury.Label == "Gunshot wound") gunshot = true;
            }

            if (patient.Faction == Faction.Rival)
            {
                // Out of the fight for good, as far as you're concerned.
                patient.EndCurrentJob(game);
                patient.Departed = true;
                return;
            }

            patient.SendToHospital(now + MinStayTicks, game);
            game.Log.Add($"{patient.DisplayName} was taken to the Ospedale.", MessageKind.Info);
            if (gunshot)
            {
                // Doctors are obliged to report gunshot wounds.
                game.Org.AddEvidence(5f, game);
                game.Log.Add($"The hospital reported {patient.DisplayName}'s gunshot wound to the police.", MessageKind.Bad);
            }
        }

        /// <summary>Discharge patients who are well enough. Called hourly.</summary>
        public void Discharge(Game game)
        {
            long now = game.Ticks.TicksGame;
            foreach (var p in game.Pawns.Crew)
            {
                if (!p.InHospital || p.Dead) continue;
                if (now < p.HospitalUntil || p.Health.Downed || p.Health.BloodLoss > 0.25f) continue;
                var outside = NearestFree(game.Map, door) ?? door;
                p.LeaveHospital(outside);
                p.Mood.AddMemory("hospital", "Patched up at the Ospedale", 4f, now, GameClock.TicksPerDay);
                game.Log.Add($"{p.DisplayName} has been discharged from the Ospedale.", MessageKind.Good, outside);
            }
        }

        static Vector2Int? NearestFree(Map map, Vector2Int from)
        {
            foreach (var c in GridUtil.CellsAround(from, 4))
                if (map.InBounds(c) && map.IsPassable(c) && map.GetTerrain(c.x, c.y) != TerrainType.Floor) return c;
            return null;
        }

        public Vector2Int Bay => bay;
        public static int Loading => LoadingTicks;
    }
}
