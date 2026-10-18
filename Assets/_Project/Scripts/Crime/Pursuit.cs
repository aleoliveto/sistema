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
        const float CatchDistance = 1.5f;     // close enough to grab you
        bool stopping;                         // the stop-and-search card is up
        long shovedUntil;                      // you shoved past: a moment before they can grab you again
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

            if (stopping) return;   // everyone stands still while it's settled
            long now = game.Ticks.TicksGame;
            bool seen = false;
            bool fresh = ++repath % 30 == 0;
            foreach (var cop in cops)
            {
                float d = (cop.Mover.Position - p.Position).magnitude;
                if (d < CatchDistance && !p.Riding && now >= shovedUntil)
                {
                    Stop(cop, game);
                    return;
                }
                if (now < shovedUntil) continue;   // still picking himself up
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

        /// <summary>A hand on your shoulder: "Documents." Let them search you, try a bribe, or shove and run.</summary>
        void Stop(Npc cop, Game game)
        {
            stopping = true;
            var p = game.Player;
            var org = game.Org;
            bool dirty = org.CarriedDg > 0 || org.DealingCorner != null || p.Armed || p.Carrying != null || p.StolenScooter;
            int bribe = 100 + Mathf.RoundToInt(org.Evidence * 4f);
            float bribeChance = Mathf.Clamp(0.3f + org.Respect / 200f - org.Evidence / 250f, 0.1f, 0.75f);
            var e = new Story.GameEvent
            {
                Id = "police_stop",
                Title = "\"Documents.\"",
                English = "The officer has you by the arm. His partner is already reaching for your pockets.",
                Body = $"{reason}" + (dirty ? " What you're carrying won't survive a search." : " You're clean, as far as they'll find."),
            };
            e.Option("Let them search you", dirty ? "They'll find it." : "Nothing on you: they'll let you go.", g =>
            {
                stopping = false;
                if (dirty) Arrest(cop, g);
                else
                {
                    g.Log.Add("They pat you down, find nothing, and tell you to get lost.", MessageKind.Info, p.Cell);
                    g.Org.AddEvidence(0.5f, g);
                    Release(g);
                }
            });
            e.Option($"Slip them €{bribe}", $"About {bribeChance * 100f:0}%: respect helps, a fat file doesn't. If it fails, that's another charge.", g =>
            {
                stopping = false;
                if (g.Stock.Cash < bribe) { g.Log.Add("You don't have that much on you.", MessageKind.Bad); Arrest(cop, g); return; }
                g.Stock.Cash -= bribe;
                if (g.Rng.NextDouble() < bribeChance)
                {
                    g.Log.Add("The money disappears into a pocket. \"We never saw you.\"", MessageKind.Good, p.Cell);
                    Release(g);
                }
                else
                {
                    g.Org.AddEvidence(4f, g);
                    g.Log.Add("Wrong cop. Now it's attempted bribery too.", MessageKind.Bad, p.Cell);
                    Arrest(cop, g);
                }
            });
            e.Option("Shove him and run", "A head start of a few seconds. Heat, and they won't be gentle next time.", g =>
            {
                stopping = false;
                shovedUntil = g.Ticks.TicksGame + 150;
                g.Territory.AddHeat(p.Cell, 6f);
                g.Org.AddEvidence(1.5f, g);
                foreach (var c in cops) c.Walker = null;
                g.Log.Add("You shove the officer into his partner and run.", MessageKind.Bad, p.Cell);
            });
            game.Events.Raise(e);
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
            var job = game.Contracts.ActiveFor(p);
            if (job != null && p.Carrying != null) game.Contracts.Finish(job, false, $"the police took {p.Carrying}.", game);
            p.Carrying = null;
            p.StolenScooter = false;
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
            stopping = false;
        }
    }
}
