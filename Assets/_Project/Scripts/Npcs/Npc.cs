using System;
using System.Collections.Generic;
using OSistema.AI;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using UnityEngine;

namespace OSistema.Npcs
{
    public enum NpcKind { Customer, Police, Civilian, Child, Elder, Scooter, Ambulance, Paramedic }

    /// <summary>
    /// A lightweight non-crew character driven by a simple state machine. NPCs aren't saved:
    /// customers and patrols are transient.
    /// </summary>
    public sealed class Npc
    {
        public readonly int Id;
        public readonly NpcKind Kind;
        public readonly Mover Mover;
        public readonly Color32 Color;
        public bool Despawned;

        // Behaviour state
        public int State;
        public int Timer;
        public Walker Walker;
        public DrugSquare Square;
        public Raid Raid;
        public Vector2Int Exit;
        public bool HasArrested;
        public Pawn Chasing;
        public bool IsSearch;        // police searching the basso (perquisizione)
        public bool IsPatrol;        // police walking a beat through a hot zone
        public Vector2Int Target;
        public int TripsLeft;        // civilians: errands before heading home
        public bool Panicking;
        public Pawn Patient;         // ambulances and paramedics
        public Npc Vehicle;          // paramedics: their ambulance
        public bool Carrying;        // paramedics: carrying the patient
        public bool IsFixture;       // shopkeepers and vendors: stand all day, not counted as passers-by
        public bool Sitting;         // heading for, or sitting on, a chair
        public bool Playing;         // kids running around
        public bool InShop;          // on an errand in a shop
        public bool Responding;
        public Npc ChatWith;         // two neighbours stopped in the street for a word      // police sent to a call (a break-in, a shooting)
        public string ContactName;   // someone who hands out work (see ContactBook)

        public Npc(int id, NpcKind kind, Vector2Int cell, float speed, Color32 color)
        {
            Id = id;
            Kind = kind;
            Color = color;
            Mover = new Mover(cell, speed);
        }
    }

    public sealed class NpcManager
    {
        const float CustomerSpeed = 0.06f;
        const float PoliceSpeed = 0.08f;

        readonly List<Npc> npcs = new List<Npc>();
        int nextId = 1;

        public IReadOnlyList<Npc> All => npcs;

        public event Action<Npc> Added;

        /// <summary>Everyone in the street goes: a new district is loading.</summary>
        public void Clear() => npcs.Clear();

        public int Count(NpcKind kind)
        {
            int n = 0;
            foreach (var npc in npcs) if (npc.Kind == kind) n++;
            return n;
        }

        public Npc SpawnCustomer(DrugSquare square, Game game)
        {
            var rng = game.Rng;
            var entrance = game.Territory.RandomEntrance(rng);
            byte grey = (byte)rng.Next(110, 200);
            var npc = Spawn(NpcKind.Customer, entrance, CustomerSpeed * (0.8f + (float)rng.NextDouble() * 0.4f),
                new Color32(grey, (byte)(grey - 10), (byte)(grey - 25), 255));
            npc.Square = square;
            npc.Exit = game.Territory.RandomEntrance(rng);
            return npc;
        }

        public Npc SpawnPolice(Raid raid, Vector2Int entrance, Game game)
        {
            var npc = Spawn(NpcKind.Police, entrance, PoliceSpeed, new Color32(30, 50, 130, 255));
            npc.Raid = raid;
            npc.Square = raid.Square;
            npc.Exit = entrance;
            return npc;
        }

        public Npc SpawnSearch(Vector2Int entrance, Vector2Int target, Game game)
        {
            var npc = Spawn(NpcKind.Police, entrance, PoliceSpeed, new Color32(30, 50, 130, 255));
            npc.IsSearch = true;
            npc.Target = target;
            npc.Exit = entrance;
            return npc;
        }

        public Npc SpawnAmbulance(Vector2Int from, Vector2Int scene, Pawn patient, Game game)
        {
            var npc = Spawn(NpcKind.Ambulance, from, 0.16f, new Color32(240, 240, 236, 255));
            npc.Target = scene;
            npc.Exit = from;
            npc.Patient = patient;
            return npc;
        }

        public Npc Spawn(NpcKind kind, Vector2Int cell, float speed, Color32 color)
        {
            var npc = new Npc(nextId++, kind, cell, speed, color);
            npcs.Add(npc);
            Added?.Invoke(npc);
            return npc;
        }

        public void Tick(Game game)
        {
            for (int i = 0; i < npcs.Count; i++)
            {
                var npc = npcs[i];
                switch (npc.Kind)
                {
                    case NpcKind.Customer: CustomerAI.Tick(npc, game); break;
                    case NpcKind.Police:
                        if (npc.State == 9) break;   // chasing the player: see Pursuit
                        if (npc.IsPatrol) StreetLife.TickPatrol(npc, game);
                        else PoliceAI.Tick(npc, game);
                        break;
                    case NpcKind.Scooter: StreetLife.TickScooter(npc, game); break;
                    case NpcKind.Ambulance: TickAmbulance(npc, game); break;
                    case NpcKind.Paramedic: TickParamedic(npc, game); break;
                    default: StreetLife.TickCivilian(npc, game); break;
                }
            }
            npcs.RemoveAll(n => n.Despawned);
        }

        /// <summary>
        /// Siren on, through the streets to the scene; the paramedics go on foot to the patient and
        /// carry them back; then back to the Ospedale.
        /// </summary>
        void TickAmbulance(Npc npc, Game game)
        {
            switch (npc.State)
            {
                case 0: // driving to the scene
                {
                    npc.Walker ??= new Walker(npc.Target, adjacent: false, roadOnly: true);
                    var status = npc.Walker.Tick(npc.Mover, 1f, game);
                    if (status == WalkStatus.Walking) return;
                    npc.State = 1;
                    for (int i = 0; i < 2; i++)
                    {
                        var medic = Spawn(NpcKind.Paramedic, npc.Mover.Cell, 0.07f, new Color32(240, 120, 30, 255));
                        medic.Patient = npc.Patient;
                        medic.Vehicle = npc;
                        medic.Timer = i * 40; // they don't move in lockstep
                    }
                    return;
                }
                case 1: // waiting while the paramedics work; they set State = 2 when done
                    return;
                default: // back to the Ospedale
                    npc.Walker ??= new Walker(npc.Exit, adjacent: false, roadOnly: true);
                    if (npc.Walker.Tick(npc.Mover, 1f, game) != WalkStatus.Walking) npc.Despawned = true;
                    return;
            }
        }

        static void TickParamedic(Npc npc, Game game)
        {
            var patient = npc.Patient;
            var van = npc.Vehicle;
            bool lost = patient == null || patient.Dead || !patient.IsPresent || van == null || van.Despawned;

            switch (npc.State)
            {
                case 0: // walking to the patient
                {
                    if (lost) { ReturnToVan(npc); return; }
                    if (npc.Timer > 0) { npc.Timer--; return; }
                    npc.Walker ??= new Walker(patient.Cell, adjacent: true);
                    var status = npc.Walker.Tick(npc.Mover, 1f, game);
                    if (status == WalkStatus.Walking) return;
                    npc.State = 1;
                    npc.Timer = Hospital.Loading;
                    return;
                }
                case 1: // stabilising the patient on the spot
                {
                    if (lost) { ReturnToVan(npc); return; }
                    if (--npc.Timer > 0) return;
                    // The first paramedic to finish carries the stretcher; the other walks alongside.
                    bool someoneCarrying = false;
                    foreach (var other in game.Npcs.All)
                        if (other != npc && other.Kind == NpcKind.Paramedic && other.Vehicle == van && other.Carrying) someoneCarrying = true;
                    npc.Carrying = !someoneCarrying;
                    ReturnToVan(npc);
                    return;
                }
                default: // back to the van, maybe carrying the patient
                {
                    if (npc.Carrying && !lost) patient.Teleport(npc.Mover.Cell);
                    var status = npc.Walker.Tick(npc.Mover, 1f, game);
                    if (status == WalkStatus.Walking) return;
                    if (npc.Carrying && !lost) game.Hospital.Admit(patient, game);
                    if (npc.Carrying || lost)
                        if (van != null) van.State = 2;
                    npc.Despawned = true;
                    return;
                }
            }
        }

        static void ReturnToVan(Npc npc)
        {
            npc.State = 2;
            var target = npc.Vehicle != null ? npc.Vehicle.Mover.Cell : npc.Mover.Cell;
            npc.Walker = new Walker(target, adjacent: false, speedMultiplier: npc.Carrying ? 0.8f : 1f);
        }
    }

    /// <summary>Walk to the piazza, buy if a pusher is there, leave.</summary>
    static class CustomerAI
    {
        const int Patience = 600;

        public static void Tick(Npc npc, Game game)
        {
            switch (npc.State)
            {
                case 0: // walking to the piazza
                {
                    if (!game.Org.IsActiveSquare(npc.Square, game))
                    {
                        Leave(npc);
                        return;
                    }
                    // At the player's corner they come to him, wherever he's standing now.
                    bool corner = npc.Square == game.Org.DealingCorner && game.Player != null;
                    var goal = corner ? game.Player.Cell : npc.Square.Cell;
                    if (npc.Walker == null || (corner && (npc.Walker.Target - goal).sqrMagnitude > 2)) npc.Walker = new Walker(goal, adjacent: true);
                    var status = npc.Walker.Tick(npc.Mover, 1f, game);
                    if (status == WalkStatus.Failed) Leave(npc);
                    else if (status == WalkStatus.Arrived)
                    {
                        npc.State = 1;
                        npc.Timer = corner ? Patience * 3 : Patience;
                    }
                    return;
                }
                case 1: // waiting for the pusher
                {
                    if (!game.Org.IsActiveSquare(npc.Square, game))
                    {
                        Leave(npc);
                        return;
                    }
                    if (npc.Square == game.Org.DealingCorner)
                    {
                        // At the player's corner: wait for him to hand it over (E). If he steps away, follow.
                        var me = game.Player;
                        if (me != null && (me.Position - npc.Mover.Position).sqrMagnitude > 2f * 2f)
                        {
                            npc.State = 0;
                            npc.Walker = null;
                            return;
                        }
                        if (--npc.Timer <= 0)
                        {
                            game.Org.LoseCustomer(npc.Square, game);
                            Leave(npc);
                        }
                        return;
                    }
                    bool rival = npc.Square.Rival;
                    var pusher = rival ? game.Clans[npc.Square.Clan].PusherAt(npc.Square, game) : game.Org.PusherAt(npc.Square, game);
                    if (pusher != null && (rival || game.Stock.Product > 0))
                    {
                        game.Org.Sell(npc.Square, pusher, game);
                        Leave(npc);
                    }
                    else if (--npc.Timer <= 0)
                    {
                        if (!rival) game.Org.LoseCustomer(npc.Square, game);
                        Leave(npc);
                    }
                    return;
                }
                default: // leaving the district
                {
                    if (npc.Walker.Tick(npc.Mover, 1f, game) != WalkStatus.Walking) npc.Despawned = true;
                    return;
                }
            }
        }

        static void Leave(Npc npc)
        {
            npc.State = 2;
            npc.Walker = new Walker(npc.Exit, adjacent: false);
        }
    }

    /// <summary>Patrol to the piazza, chase anyone exposed nearby, search the spot, leave.</summary>
    static class PoliceAI
    {
        const int SearchTicks = 900;
        const float ChaseRange = 8f;

        public static void Tick(Npc npc, Game game)
        {
            if (npc.State < 2 && game.Ticks.TicksGame % 60 == npc.Id % 60) LookForTarget(npc, game);

            switch (npc.State)
            {
                case 0: // heading to the piazza or the basso (or chasing)
                {
                    npc.Walker ??= new Walker(npc.IsSearch ? npc.Target : npc.Square.Cell, adjacent: !npc.IsSearch);
                    var status = npc.Walker.Tick(npc.Mover, 1f, game);
                    if (status == WalkStatus.Walking) return;
                    if (npc.Chasing != null)
                    {
                        npc.Chasing = null;
                        npc.Walker = new Walker(npc.Square.Cell, adjacent: true);
                        return;
                    }
                    if (npc.IsSearch) game.Org.OnSearchArrived(game); // even if blocked at the door
                    else if (status == WalkStatus.Failed)
                    {
                        Leave(npc);
                        return;
                    }
                    else game.Police.OnPatrolArrived(npc.Raid, game);
                    npc.State = 1;
                    npc.Timer = SearchTicks;
                    return;
                }
                case 1: // searching the spot
                {
                    if (npc.Chasing != null)
                    {
                        if (npc.Walker.Tick(npc.Mover, 1f, game) != WalkStatus.Walking) npc.Chasing = null;
                        return;
                    }
                    if (--npc.Timer <= 0) Leave(npc);
                    return;
                }
                default: // leaving
                {
                    if (npc.Walker.Tick(npc.Mover, 1f, game) != WalkStatus.Walking) npc.Despawned = true;
                    return;
                }
            }
        }

        static void LookForTarget(Npc npc, Game game)
        {
            if (npc.HasArrested || npc.Square == null) return;
            Pawn best = null;
            float bestDist = ChaseRange;
            foreach (var p in game.Pawns.Crew)
            {
                if (!Police.IsExposed(p, npc.Square)) continue;
                float d = (p.Position - npc.Mover.Position).magnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }
            if (best == null) return;
            npc.Chasing = best;
            npc.Walker = new Walker(best.Cell, adjacent: true, speedMultiplier: 1.1f);
        }

        static void Leave(Npc npc)
        {
            npc.State = 2;
            npc.Chasing = null;
            npc.Walker = new Walker(npc.Exit, adjacent: false);
        }
    }
}
