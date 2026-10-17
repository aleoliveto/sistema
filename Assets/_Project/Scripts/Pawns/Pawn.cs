using System.Collections.Generic;
using OSistema.AI;
using OSistema.Core;
using OSistema.Story;
using OSistema.World;
using UnityEngine;

namespace OSistema.Pawns
{
    public enum SleepState { Awake, InBed, OnFloor }

    /// <summary>The crew member's job in the clan. Hierarchy comes later; for now it's what they do all day.</summary>
    public enum CrewRole { General, Pusher, Lookout, Collector }

    /// <summary>A crew member. Pure simulation data; PawnView draws it.</summary>
    public sealed class Pawn
    {
        /// <summary>Cells per tick on cost-1 terrain (~4.5 cells/s at 1x).</summary>
        public const float BaseSpeed = 0.075f;
        const float BarkSeconds = 3.5f;
        const int CheckInterval = 250;
        const int MentalBreakTicks = 15000; // ~6 in-game hours

        public readonly int Id;
        public readonly string FirstName;
        public readonly string LastName;
        public readonly string Nickname;
        public readonly Color32 ShirtColor;

        public int ConstructionSkill;
        public int CookingSkill;
        public int MedicineSkill;
        public int IntimidationSkill;
        public int AwarenessSkill;

        public CrewRole Role;
        public int Wage = 150;          // weekly mesata, paid on Sunday
        public float Loyalty = 60f;     // 0..100

        public readonly Needs Needs = new Needs();
        public readonly Health Health = new Health();
        public readonly Mood Mood = new Mood();
        public readonly Mover Mover;

        public bool Drafted { get; private set; }
        public bool Dead { get; private set; }
        public string DeathCause { get; private set; }
        public Job CurrentJob { get; private set; }

        public Vector2Int? Bed;
        public SleepState Sleep;
        public string Carrying;          // shown in the HUD while shopping
        public Pawn TendedBy;            // reserved by a doctor
        public long BreakUntilTick { get; private set; }
        public long JailedUntil;         // 0 when free
        public long LyingLowUntil;       // stays home after a close call with the police

        public Barks.Line Bark { get; private set; }
        float barkUntil = -1f;

        public Pawn(int id, string firstName, string lastName, string nickname, Color32 shirt, Vector2Int cell)
        {
            Id = id;
            FirstName = firstName;
            LastName = lastName;
            Nickname = nickname;
            ShirtColor = shirt;
            Mover = new Mover(cell, BaseSpeed);
        }

        public Vector2Int Cell => Mover.Cell;
        public Vector2 Position => Mover.Position;
        public string FullName => $"{FirstName} {LastName}";
        public bool HasBark => Time.unscaledTime < barkUntil;
        public bool HasBed => Bed.HasValue;
        public bool InJail => JailedUntil > 0;
        public bool InMentalBreak(long now) => BreakUntilTick > now;
        public bool IsLyingLow(long now) => LyingLowUntil > now;

        /// <summary>On the map, alive and able to stand.</summary>
        public bool CanAct => !Dead && !InJail && !Health.Downed;

        /// <summary>Alive and on the map (possibly downed).</summary>
        public bool IsPresent => !Dead && !InJail;

        /// <summary>Injuries slow you down.</summary>
        public float SpeedFactor => 1f - Health.Pain * 0.5f;

        /// <summary>A need bad enough to drop work for.</summary>
        public bool HasUrgentNeed =>
            Needs.Food < Needs.RavenousBelow ||
            Needs.Rest < Needs.ExhaustedBelow ||
            (Health.NeedsTending && Health.BleedRatePerDay > 0.3f);

        public static Vector2 CellCenter(Vector2Int c) => Mover.CellCenter(c);

        public void Tick(Game game)
        {
            if (Dead || InJail) return;
            long now = game.Ticks.TicksGame;

            Needs.Tick(this);
            Health.Tick(this);
            if (Health.IsDeadly)
            {
                game.Pawns.Kill(this, Health.BloodLoss >= 1f ? "blood loss" : "starvation", game);
                return;
            }

            if ((now + Id * 37) % CheckInterval == 0) PeriodicCheck(game, now);

            if (Health.Downed && !(CurrentJob is DownedJob)) ForceJob(new DownedJob(this), game);
            if (CurrentJob == null) CurrentJob = JobGiver.Next(this, game);
            if (CurrentJob.Tick(game))
            {
                bool failed = CurrentJob.Failed;
                EndCurrentJob(game);
                // Back off briefly after a failure so an unreachable target isn't re-pathed every tick.
                if (failed) CurrentJob = new WaitJob(this, 90, "Idle");
            }
        }

        void PeriodicCheck(Game game, long now)
        {
            if (Bed.HasValue && game.Map.GetBuilding(Bed.Value) != BuildingDefs.Bed) Bed = null;

            Mood.Recalculate(this, now);

            if (BreakUntilTick > 0 && now >= BreakUntilTick)
            {
                BreakUntilTick = 0;
                Mood.AddMemory("vented", "Vented my frustration", 12f, now, GameClock.TicksPerDay);
                game.Log.Add($"{Nickname} has calmed down.", MessageKind.Good);
            }

            if (!InMentalBreak(now) && !Drafted && CanAct && Mood.Value < Mood.BreakBelow && game.Rng.NextDouble() < 0.012)
            {
                StartMentalBreak(game, now);
                return;
            }

            if (CurrentJob == null || Drafted || InMentalBreak(now) || !CanAct) return;
            if (CurrentJob.IsIdle)
            {
                var job = JobGiver.TryNonIdle(this, game);
                if (job != null) ForceJob(job, game);
            }
            else if (CurrentJob.IsWork && HasUrgentNeed)
            {
                EndCurrentJob(game);
            }
        }

        void StartMentalBreak(Game game, long now)
        {
            BreakUntilTick = now + MentalBreakTicks;
            ForceJob(new SulkJob(this), game);
            Say("sulk");
            game.Log.Add($"{Nickname} can't take it anymore and is sulking around the rione.", MessageKind.Bad);
        }

        public void ForceJob(Job job, Game game)
        {
            EndCurrentJob(game);
            CurrentJob = job;
        }

        public void EndCurrentJob(Game game)
        {
            if (CurrentJob == null) return;
            var job = CurrentJob;
            CurrentJob = null;
            job.OnEnd(game);
            StopMoving();
        }

        public void SetDrafted(bool drafted, Game game)
        {
            if (Drafted == drafted || !CanAct) return;
            Drafted = drafted;
            EndCurrentJob(game);
            Say(drafted ? "drafted" : "undrafted");
        }

        public void Die(string cause, Game game)
        {
            EndCurrentJob(game);
            Dead = true;
            DeathCause = cause;
            Drafted = false;
            Bed = null;
            Sleep = SleepState.Awake;
        }

        public void SendToJail(long until, Game game)
        {
            EndCurrentJob(game);
            JailedUntil = until;
            Drafted = false;
            Sleep = SleepState.Awake;
            TendedBy = null;
        }

        public void ReleaseFromJail(Vector2Int at)
        {
            JailedUntil = 0;
            Teleport(at);
        }

        /// <summary>Restores state that normally changes through gameplay. Used by save/load only.</summary>
        public void RestoreState(bool drafted, bool dead, string deathCause, long breakUntil)
        {
            Drafted = drafted;
            Dead = dead;
            DeathCause = deathCause;
            BreakUntilTick = breakUntil;
        }

        public void Say(string key)
        {
            Bark = Barks.Pick(key);
            barkUntil = Time.unscaledTime + BarkSeconds;
        }

        public void SetPath(List<Vector2Int> newPath) => Mover.SetPath(newPath);
        public void StopMoving() => Mover.Stop();
        public void Teleport(Vector2Int cell) => Mover.Teleport(cell);
    }
}
