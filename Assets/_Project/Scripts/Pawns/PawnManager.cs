using System;
using System.Collections.Generic;
using OSistema.Core;
using OSistema.World;
using UnityEngine;

namespace OSistema.Pawns
{
    public sealed class PawnManager
    {
        readonly List<Pawn> pawns = new List<Pawn>();
        int nextId = 1;

        /// <summary>Every pawn, including the dead (they stay on the map until burial exists).</summary>
        public IReadOnlyList<Pawn> All => pawns;

        public event Action<Pawn> Added;

        public int NextId() => nextId++;

        public void Add(Pawn pawn)
        {
            pawns.Add(pawn);
            nextId = Mathf.Max(nextId, pawn.Id + 1);
            Added?.Invoke(pawn);
        }

        public int LivingCount
        {
            get
            {
                int n = 0;
                foreach (var p in pawns) if (!p.Dead) n++;
                return n;
            }
        }

        public void Tick(Game game)
        {
            for (int i = 0; i < pawns.Count; i++) pawns[i].Tick(game);
        }

        public void Kill(Pawn pawn, string cause, Game game)
        {
            if (pawn.Dead) return;
            pawn.Die(cause, game);
            game.Log.Add($"{pawn.FullName} “{pawn.Nickname}” has died of {cause}.", MessageKind.Bad);

            long now = game.Ticks.TicksGame;
            foreach (var other in pawns)
            {
                if (other.Dead) continue;
                other.Mood.AddMemory($"death_{pawn.Id}", $"{pawn.Nickname} died", -15f, now, GameClock.TicksPerDay * 3);
                if (other.TendedBy == pawn) other.TendedBy = null;
            }
        }

        public bool IsBedOwned(Vector2Int bed, Pawn except)
        {
            foreach (var p in pawns)
                if (p != except && !p.Dead && p.Bed == bed) return true;
            return false;
        }

        /// <summary>Pushes pawns out of a cell that just became solid (e.g. a finished wall).</summary>
        public void EvictFrom(Vector2Int cell, Map map)
        {
            foreach (var pawn in pawns)
            {
                if (pawn.Cell != cell) continue;
                foreach (var c in GridUtil.CellsAround(cell, 6))
                {
                    if (!map.IsPassable(c)) continue;
                    pawn.Teleport(c);
                    break;
                }
            }
        }
    }
}
