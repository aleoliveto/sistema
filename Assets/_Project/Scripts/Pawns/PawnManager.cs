using System;
using System.Collections.Generic;
using OSistema.Core;
using OSistema.World;
using UnityEngine;

namespace OSistema.Pawns
{
    public enum Faction { Crew, Rival }

    public sealed class PawnManager
    {
        readonly List<Pawn> pawns = new List<Pawn>();
        readonly List<Pawn> crew = new List<Pawn>();
        readonly List<Pawn> rivals = new List<Pawn>();
        int nextId = 1;

        /// <summary>Everyone on the books, both factions, including the dead.</summary>
        public IReadOnlyList<Pawn> All => pawns;

        /// <summary>The player's people (including the dead and the jailed).</summary>
        public IReadOnlyList<Pawn> Crew => crew;

        /// <summary>Rival clan members currently on the map (including their dead).</summary>
        public IReadOnlyList<Pawn> Rivals => rivals;

        public event Action<Pawn> Added;

        public int NextId() => nextId++;

        public void Add(Pawn pawn)
        {
            pawns.Add(pawn);
            (pawn.Faction == Faction.Crew ? crew : rivals).Add(pawn);
            nextId = Mathf.Max(nextId, pawn.Id + 1);
            Added?.Invoke(pawn);
        }

        public int LivingCount
        {
            get
            {
                int n = 0;
                foreach (var p in crew) if (!p.Dead) n++;
                return n;
            }
        }

        /// <summary>Crew alive and not in prison: the mouths to feed at the basso.</summary>
        public int PresentCount
        {
            get
            {
                int n = 0;
                foreach (var p in crew) if (p.IsPresent) n++;
                return n;
            }
        }

        public Pawn ById(int id) => pawns.Find(p => p.Id == id);

        public void Tick(Game game)
        {
            for (int i = 0; i < pawns.Count; i++) pawns[i].Tick(game);

            // Rivals who left the district are gone for good.
            if (pawns.Exists(p => p.Departed))
            {
                pawns.RemoveAll(p => p.Departed);
                rivals.RemoveAll(p => p.Departed);
                crew.RemoveAll(p => p.Departed);
            }
        }

        public void Kill(Pawn pawn, string cause, Game game)
        {
            if (pawn.Dead) return;
            pawn.Die(cause, game);

            var zone = game.Territory.ZoneAt(pawn.Cell);
            game.Newspaper.Report(game, 4, $"Shot dead in {zone?.Name ?? "the rione"}",
                $"{pawn.FirstName} {pawn.LastName}, known as {pawn.Nickname}, died of {cause}. Investigators speak of a settling of scores.");
            if (pawn.Faction == Faction.Rival)
            {
                game.Rival.OnSoldierDied(pawn, game);
                return;
            }

            game.Log.Add($"{pawn.FullName} “{pawn.Nickname}” has died of {cause}.", MessageKind.Bad);
            long now = game.Ticks.TicksGame;
            foreach (var other in crew)
            {
                if (!other.IsPresent) continue;
                other.Mood.AddMemory($"death_{pawn.Id}", $"{pawn.Nickname} died", -15f, now, GameClock.TicksPerDay * 3);
                if (other.TendedBy == pawn) other.TendedBy = null;
            }
        }

        public bool IsBedOwned(Vector2Int bed, Pawn except)
        {
            foreach (var p in crew)
                if (p != except && !p.Dead && p.Bed == bed) return true;
            return false;
        }

        /// <summary>Pushes pawns out of a cell that just became solid (e.g. a finished wall).</summary>
        public void EvictFrom(Vector2Int cell, Map map)
        {
            foreach (var pawn in pawns)
            {
                if (pawn.Cell != cell || !pawn.IsPresent) continue;
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
