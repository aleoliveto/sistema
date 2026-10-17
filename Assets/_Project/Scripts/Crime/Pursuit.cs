using System.Collections.Generic;
using OSistema.AI;
using OSistema.Core;
using OSistema.Npcs;
using OSistema.Pawns;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// "Fermo! Polizia!" Police who catch the player at something chase him on foot. Get far enough
    /// away and out of sight for a few seconds and they lose you; get caught and you're inside.
    /// Running costs stamina; a scooter leaves them standing.
    /// </summary>
    public sealed class Pursuit
    {
        const float CatchDistance = 0.95f;
        const float LoseDistance = 11f;
        const int LoseTicks = 240;

        readonly List<Npc> cops = new List<Npc>();
        int unseenTicks;
        int repath;
        string reason;

        public bool Active => cops.Count > 0;
        public float EscapeProgress => Mathf.Clamp01(unseenTicks / (float)LoseTicks);
        public string Reason => reason;

        /// <summary>Starts (or joins) a chase after the player.</summary>
        public void Start(Npc cop, string why, Game game)
        {
            var p = game.Player;
            if (p == null || !p.CanAct || cop == null || cop.Despawned || cops.Contains(cop)) return;
            if (cops.Count == 0)
            {
                reason = why;
                unseenTicks = 0;
                game.Log.Add($"\"Fermo! Polizia!\" {why} Run, or get caught.", MessageKind.Bad, p.Cell);
                p.Say("police");
            }
            cops.Add(cop);
            cop.Chasing = p;
            cop.IsPatrol = false;
            cop.Square = null;
            cop.State = 9;   // handled here, not by the patrol or raid logic
        }

        /// <summary>The nearest cop who can see the player, if any.</summary>
        public static Npc WitnessCop(Game game, float range)
        {
            var p = game.Player;
            if (p == null) return null;
            Npc best = null;
            float bestD = range * range;
            foreach (var n in game.Npcs.All)
            {
                if (n.Kind != NpcKind.Police || n.Despawned || n.HasArrested) continue;
                float d = (n.Mover.Position - p.Position).sqrMagnitude;
                if (d > bestD || !Combat.CombatSystem.HasLineOfSight(game.Map, n.Mover.Cell, p.Cell)) continue;
                bestD = d;
                best = n;
            }
            return best;
        }

        public void Tick(Game game)
        {
            if (cops.Count == 0) return;
            var p = game.Player;
            cops.RemoveAll(c => c.Despawned);
            if (p == null || !p.IsPresent || p.InJail)
            {
                Release(game);
                return;
            }

            bool seen = false;
            bool fresh = ++repath % 30 == 0;
            foreach (var cop in cops)
            {
                float d = (cop.Mover.Position - p.Position).magnitude;
                if (d < CatchDistance && !p.Riding)
                {
                    Arrest(cop, game);
                    return;
                }
                if (d < LoseDistance && Combat.CombatSystem.HasLineOfSight(game.Map, cop.Mover.Cell, p.Cell)) seen = true;
                if (fresh || cop.Walker == null) cop.Walker = new Walker(p.Cell, adjacent: true, speedMultiplier: 1.3f);
                cop.Walker.Tick(cop.Mover, 1f, game);
            }
            unseenTicks = seen ? 0 : unseenTicks + 1;
            if (unseenTicks >= LoseTicks)
            {
                game.Log.Add("You lost them. Keep your head down for a while.", MessageKind.Good, p.Cell);
                game.Org.AddEvidence(1f, game);
                Release(game);
            }
        }

        void Arrest(Npc cop, Game game)
        {
            var p = game.Player;
            var org = game.Org;
            long now = game.Ticks.TicksGame;
            int days = 3 + game.Rng.Next(0, 4);
            string found = "";
            if (org.CarriedDg > 0)
            {
                days += 8 + org.CarriedDg / 1000;
                found = $" They found {org.CarriedDg / 10} g on him.";
                org.CarriedDg = 0;
            }
            if (org.DealingCorner != null)
            {
                int seized = Mathf.Min(game.Stock.Product, 10);
                game.Stock.Product -= seized;
                if (seized > 0) found += $" {seized} doses seized.";
                days += 2;
                org.StopDealing(game, quiet: true);
            }
            if (p.Armed)
            {
                days += 5;
                found += " And a pistol.";
            }
            p.Riding = p.OnScooter = false;
            p.SendToJail(now + days * GameClock.TicksPerDay, game);
            org.AddEvidence(8f, game);
            game.Log.Add($"Caught. {p.DisplayName} is taken to Poggioreale for {days} days.{found}", MessageKind.Bad, p.Cell);
            game.Newspaper.Report(game, 3, "Young man arrested after a chase", $"Officers caught him after a run through the vicoli.{found}");
            cop.HasArrested = true;
            Release(game);
        }

        void Release(Game game)
        {
            foreach (var cop in cops)
            {
                cop.Chasing = null;
                cop.State = 2;   // leaving
                cop.Walker = new Walker(cop.Exit, adjacent: false);
            }
            cops.Clear();
            unseenTicks = 0;
        }
    }
}
