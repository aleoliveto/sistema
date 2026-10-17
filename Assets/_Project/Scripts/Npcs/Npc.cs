using System;
using System.Collections.Generic;
using OSistema.AI;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using UnityEngine;

namespace OSistema.Npcs
{
    public enum NpcKind { Customer, Police, Civilian, Child, Elder, Scooter }

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
                        if (npc.IsPatrol) StreetLife.TickPatrol(npc, game);
                        else PoliceAI.Tick(npc, game);
                        break;
                    case NpcKind.Scooter: StreetLife.TickScooter(npc, game); break;
                    default: StreetLife.TickCivilian(npc, game); break;
                }
            }
            npcs.RemoveAll(n => n.Despawned);
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
                    npc.Walker ??= new Walker(npc.Square.Cell, adjacent: true);
                    var status = npc.Walker.Tick(npc.Mover, 1f, game);
                    if (status == WalkStatus.Failed) Leave(npc);
                    else if (status == WalkStatus.Arrived)
                    {
                        npc.State = 1;
                        npc.Timer = Patience;
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
                    bool rival = npc.Square.Rival;
                    var pusher = rival ? game.Rival.PusherAt(npc.Square, game) : game.Org.PusherAt(npc.Square, game);
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
