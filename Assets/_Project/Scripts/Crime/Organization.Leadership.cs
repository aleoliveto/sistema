using System;
using System.Collections.Generic;
using System.Linq;
using OSistema.AI;
using OSistema.Core;
using OSistema.Pawns;
using OSistema.Story;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// The boss: evidence piling up against him, the arrest warrant, the latitanza in the bunker,
    /// orders sent out by pizzino, and who takes over when he's gone.
    /// </summary>
    public sealed partial class Organization
    {
        const long LifeSentenceTicks = GameClock.TicksPerDay * 36500L;

        public float Evidence;           // 0..100: the investigators' case against the boss
        public int Generation = 1;
        public bool WarrantActive;
        public bool Hiding;
        public int HidingSinceDay;
        public float LeakRisk;           // every courier visit is a chance someone talks
        public bool SuccessionPending;
        public bool GameOver;
        public string GameOverReason;
        public Pawn Courier;

        bool searchInProgress;
        bool noLeaderWarned;
        readonly List<(string description, Action<Game> action)> pizzini = new List<(string, Action<Game>)>();

        public int PendingPizzini => pizzini.Count;

        public Pawn Boss(Game game)
        {
            foreach (var p in game.Pawns.Crew)
                if (p.IsBoss && !p.Dead && !p.Departed) return p;
            return null;
        }

        static bool IsGone(Pawn boss, long now) =>
            boss == null || boss.Dead || boss.Departed || (boss.InJail && boss.JailedUntil - now > GameClock.TicksPerDay * 365);

        public void AddEvidence(float amount, Game game)
        {
            Evidence = Mathf.Clamp(Evidence + amount, 0f, 100f);
            if (Evidence < 100f || WarrantActive) return;
            var boss = Boss(game);
            if (boss == null || !boss.IsPresent) return;
            WarrantActive = true;
            game.Newspaper.Report(game, 7, $"Arrest warrant for {boss.FirstName} {boss.LastName}",
                "The prosecutors say they have enough to put the head of the family away. His lawyers say he is a businessman.");
            game.Events.Raise(EventCards.Warrant(game));
        }

        /// <summary>
        /// Runs an order now, or, while the boss is in hiding, sends it out by pizzino
        /// to be carried out the next morning.
        /// </summary>
        public void Issue(string description, Action<Game> action, Game game)
        {
            if (!Hiding)
            {
                action(game);
                return;
            }
            pizzini.Add((description, action));
            game.Log.Add($"Pizzino sent from the hideout: {description}. It reaches the crew tomorrow at 08:00.");
        }

        void LeadershipHourly(Game game)
        {
            long now = game.Ticks.TicksGame;
            if (game.Clock.Hour == 8 && pizzini.Count > 0)
            {
                var batch = new List<(string, Action<Game>)>(pizzini);
                pizzini.Clear();
                foreach (var (_, action) in batch) action(game);
                game.Log.Note($"The crew received {batch.Count} pizzini from the boss.", MessageKind.Good);
            }

            CheckSuccession(game, now);

            if (Hiding)
            {
                var boss = Boss(game);
                // A boss who cracks in the bunker goes for a walk. Someone always sees.
                if (boss != null && boss.InMentalBreak(now) && !game.PlayerBase.Contains(boss.Cell) && game.Rng.NextDouble() < 0.15)
                    CaughtInHiding(boss, "was recognised walking in the street", game);
            }
        }

        void LeadershipDaily(Game game)
        {
            Evidence = Mathf.Max(0f, Evidence - (Hiding ? 2f : 1f) - FrontsLaundering());
            if (!Hiding) return;

            var boss = Boss(game);
            if (boss == null) return;
            long now = game.Ticks.TicksGame;
            int days = game.Clock.Day - HidingSinceDay;

            boss.Mood.Memories.RemoveAll(m => m.Id == "bunker");
            boss.Mood.AddMemory("bunker", $"Buried alive in hiding ({days} days)", -Mathf.Min(20f, 2f * days), now, GameClock.TicksPerDay * 2);

            if (game.Rng.NextDouble() < 0.15 + Evidence / 400f + LeakRisk / 200f) StartSearch(game);
            LeakRisk *= 0.8f;

            if (days >= 10 && Evidence < 70f)
            {
                EndHiding($"The investigation has stalled and the warrant has lapsed. {boss.DisplayName} comes back into the light.", MessageKind.Good, game);
                Evidence = 40f;
            }
        }

        // ---------- Warrant choices ----------

        public void GoIntoHiding(Game game)
        {
            var boss = Boss(game);
            if (boss == null) return;
            boss.SetDrafted(false, game);
            Hiding = true;
            HidingSinceDay = game.Clock.Day;
            LeakRisk = 0f;
            boss.ForceJob(new HideJob(boss, game), game);
            bool bunker = game.Map.PositionsOf(BuildingDefs.Bunker).Count > 0;
            game.Log.Add(bunker
                ? $"{boss.DisplayName} disappears into the bunker. The latitanza begins."
                : $"{boss.DisplayName} hides at the basso. Without a bunker, a search will find him.", bunker ? MessageKind.Info : MessageKind.Bad);
        }

        public void TurnSelfIn(Game game)
        {
            var boss = Boss(game);
            if (boss == null) return;
            boss.SendToJail(game.Ticks.TicksGame + GameClock.TicksPerDay * 30, game);
            WarrantActive = false;
            Evidence = 30f;
            game.Log.Add($"{boss.DisplayName} walks into the Questura with his lawyer. 30 days in Poggioreale; the family waits.");
        }

        public void FleeAbroad(Game game)
        {
            var boss = Boss(game);
            if (boss == null) return;
            boss.EndCurrentJob(game);
            boss.Departed = true;
            WarrantActive = false;
            Hiding = false;
            game.Log.Add($"{boss.DisplayName} leaves on a fishing boat at night. He will never see Naples again.", MessageKind.Bad);
        }

        void EndHiding(string message, MessageKind kind, Game game)
        {
            Hiding = false;
            WarrantActive = false;
            var boss = Boss(game);
            if (boss != null)
            {
                boss.Mood.Memories.RemoveAll(m => m.Id == "bunker");
                if (boss.CurrentJob is HideJob) boss.EndCurrentJob(game);
            }
            if (message != null) game.Log.Add(message, kind);
        }

        // ---------- Searches (perquisizioni) ----------

        void StartSearch(Game game)
        {
            if (searchInProgress || game.Territory.Entrances.Count == 0) return;
            searchInProgress = true;
            var home = Vector2Int.RoundToInt(game.PlayerBase.center);
            var entrance = game.Territory.Entrances[0];
            foreach (var e in game.Territory.Entrances)
                if ((e - home).sqrMagnitude < (entrance - home).sqrMagnitude) entrance = e;
            for (int i = 0; i < 3; i++) game.Npcs.SpawnSearch(entrance, home, game);
            game.Log.Add("Police cars are surrounding the basso. A perquisizione!", MessageKind.Bad);
        }

        /// <summary>Called by the first officer to reach the basso.</summary>
        public void OnSearchArrived(Game game)
        {
            if (!searchInProgress) return;
            searchInProgress = false;

            var boss = Boss(game);
            if (!Hiding || boss == null || !boss.IsPresent)
            {
                game.Log.Add("The police turned the basso upside down and left with nothing.");
                return;
            }

            bool inBunker = game.Map.GetBuilding(boss.Cell) == BuildingDefs.Bunker;
            float chance = inBunker ? 0.12f + LeakRisk / 100f : 0.65f;
            if (game.Rng.NextDouble() < chance)
            {
                CaughtInHiding(boss, inBunker ? "was found in the bunker behind the wall" : "was found hiding at the basso", game);
                return;
            }
            Evidence = Mathf.Max(0f, Evidence - 10f);
            game.Log.Add(inBunker
                ? "They tore the basso apart and tapped every wall. They found nothing."
                : "Somehow they missed him. Next time he won't be so lucky.", MessageKind.Good);
        }

        void CaughtInHiding(Pawn boss, string how, Game game)
        {
            Hiding = false;
            WarrantActive = false;
            boss.SendToJail(game.Ticks.TicksGame + LifeSentenceTicks, game);
            game.Log.Add($"{boss.DisplayName} {how}. Ergastolo, under the 41-bis regime. He will die inside.", MessageKind.Bad);
        }

        // ---------- Succession ----------

        void CheckSuccession(Game game, long now)
        {
            if (GameOver) return;
            var boss = game.Pawns.Crew.FirstOrDefault(p => p.IsBoss);
            if (!IsGone(boss, now)) return;
            if (boss != null) boss.IsBoss = false;
            if (Hiding) EndHiding(null, MessageKind.Info, game);

            if (!game.Pawns.Crew.Any(p => !p.Dead && !p.Departed))
            {
                GameOver = true;
                GameOverReason = "There is no one left to carry the name. The System has swallowed the family whole.";
                game.Ticks.SetSpeed(0);
                game.Log.Add(GameOverReason, MessageKind.Bad);
                return;
            }

            var candidates = game.Pawns.Crew.Where(p => p.IsPresent)
                .OrderByDescending(p => p.Loyalty + 3f * p.IntimidationSkill).Take(3).ToList();
            if (candidates.Count == 0)
            {
                if (!noLeaderWarned) game.Log.Add("The family has no one free to lead it. Everyone is dead or inside.", MessageKind.Bad);
                noLeaderWarned = true;
                return;
            }

            if (SuccessionPending) return;
            SuccessionPending = true;
            game.Events.Raise(EventCards.Succession(game, candidates));
        }

        public void AppointBoss(Pawn heir, Game game)
        {
            foreach (var p in game.Pawns.Crew) p.IsBoss = false;
            heir.IsBoss = true;
            heir.Role = CrewRole.General;
            Generation++;
            SuccessionPending = false;
            noLeaderWarned = false;
            Respect *= 0.75f;
            Evidence = 20f;
            WarrantActive = false;
            game.Rival.Hostility = Mathf.Min(100f, game.Rival.Hostility + 15f); // they'll test the new boss

            foreach (var p in game.Pawns.Crew)
            {
                if (!p.IsPresent) continue;
                p.Loyalty = Mathf.Clamp(p.Loyalty + (p == heir ? 15f : -5f), 0f, 100f);
            }
            heir.Say("new_boss");
            game.Log.Add($"{heir.FullName} is the new boss of the family: {heir.DisplayName}. Generation {Generation}.", MessageKind.Good);
        }
    }
}
