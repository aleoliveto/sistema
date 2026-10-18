using System.Collections.Generic;
using OSistema.AI;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Story;
using OSistema.World;
using UnityEngine;

namespace OSistema.Pawns
{
    public enum SleepState { Awake, InBed, OnFloor }

    /// <summary>The crew member's job in the clan. Hierarchy comes later; for now it's what they do all day.</summary>
    public enum CrewRole { General, Pusher, Lookout, Collector, Manager, Bagger, Roller, Bodyguard }

    public enum Skill { Construction, Cooking, Medicine, Intimidation, Awareness, Shooting }

    /// <summary>What a rival clan member on the map is here to do.</summary>
    public enum RivalMission { Pusher, Stesa, Hit, Leave, Lookout, Patrol }

    /// <summary>A crew member. Pure simulation data; PawnView draws it.</summary>
    public sealed class Pawn
    {
        /// <summary>Cells per tick on cost-1 terrain (~4.5 cells/s at 1x).</summary>
        public const float BaseSpeed = 0.075f;
        const float BarkSeconds = 3.5f;
        const int CheckInterval = 250;
        const int MentalBreakTicks = 15000; // ~6 in-game hours

        public readonly int Id;
        public string FirstName;
        public string LastName;
        public string Nickname;
        public readonly Color32 ShirtColor;

        public int ConstructionSkill;
        public int CookingSkill;
        public int MedicineSkill;
        public int IntimidationSkill;
        public int AwarenessSkill;
        public int ShootingSkill;

        public Faction Faction;          // set before adding to the PawnManager
        public bool Armed;               // carries a pistol
        public bool OnScooter;           // riding one of the family's scooters for a long trip
        public bool IsBoss;
        public long AggressiveUntil;     // willing to start shooting until this tick
        public long NextAttackTick;
        public bool Departed;            // rival who left the map
        public int MealsDelivered;       // food brought to a boss in hiding

        // Rival clan members only
        public RivalMission Mission;
        public DrugSquare MissionSquare;
        public Pawn MissionTarget;

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
        public long HospitalUntil;       // 0 when not in hospital
        public long LyingLowUntil;       // stays home after a close call with the police

        Pawn aimTarget;
        float aimUntil;

        /// <summary>Focus-fire order from the player (crew) or the mission (rivals).</summary>
        public Pawn ForcedTarget;

        /// <summary>Real time of the last wound, for the hit flash.</summary>
        public float LastHitTime = -10f;
        public Vector2 LastHitDir;          // which way the last bullet or punch was going: the body jerks that way
        public long StaggerUntil;           // just hit: slowed, and can't fire straight away

        // The pistol (player): rounds in the magazine, reloading, and how far recoil has thrown the aim.
        public const int MagazineSize = 8;
        public int Ammo = MagazineSize;
        public float ReloadUntil = -1f;     // real seconds (Time.unscaledTime)
        public float Bloom;                 // extra spread in degrees, from firing fast; settles back down
        public int RideModel = 1, RideColour = -1;   // which scooter they ride (the boss: the one bought at the dealer)

        /// <summary>Who this pawn is shooting at right now (for facing and effects).</summary>
        public Pawn AimTarget => aimTarget != null && Time.unscaledTime < aimUntil ? aimTarget : null;

        public void AimAt(Pawn target)
        {
            aimTarget = target;
            aimUntil = Time.unscaledTime + 1.2f;
        }

        public const int MaxSkill = 15;

        /// <summary>Experience towards the next level of each skill (indexed by Skill).</summary>
        public readonly float[] SkillXp = new float[6];

        public int GetSkill(Skill s) => s switch
        {
            Skill.Construction => ConstructionSkill,
            Skill.Cooking => CookingSkill,
            Skill.Medicine => MedicineSkill,
            Skill.Intimidation => IntimidationSkill,
            Skill.Awareness => AwarenessSkill,
            _ => ShootingSkill,
        };

        void SetSkill(Skill s, int v)
        {
            switch (s)
            {
                case Skill.Construction: ConstructionSkill = v; break;
                case Skill.Cooking: CookingSkill = v; break;
                case Skill.Medicine: MedicineSkill = v; break;
                case Skill.Intimidation: IntimidationSkill = v; break;
                case Skill.Awareness: AwarenessSkill = v; break;
                default: ShootingSkill = v; break;
            }
        }

        public static float XpForNextLevel(int level) => 40f + level * 20f;

        public static string SkillVerb(Skill s) => s switch
        {
            Skill.Construction => "building",
            Skill.Cooking => "cooking",
            Skill.Medicine => "patching people up",
            Skill.Intimidation => "putting the fear into people",
            Skill.Awareness => "keeping their eyes open",
            _ => "shooting",
        };

        /// <summary>Practice makes better. Crew members level up their skills by doing things.</summary>
        public void Learn(Skill s, float xp, Game game)
        {
            if (Faction != Faction.Crew) return;
            int level = GetSkill(s);
            if (level >= MaxSkill) return;
            SkillXp[(int)s] += xp;
            while (level < MaxSkill && SkillXp[(int)s] >= XpForNextLevel(level))
            {
                SkillXp[(int)s] -= XpForNextLevel(level);
                level++;
                SetSkill(s, level);
                Mood.AddMemory($"skill_{s}", "Getting good at this", 3f, game.Ticks.TicksGame, GameClock.TicksPerDay / 2);
                game.Log.Add($"{DisplayName} is getting better at {SkillVerb(s)}: {s} {level}.", MessageKind.Good);
            }
        }

        public Barks.Line Bark { get; private set; }
        float barkUntil = -1f;

        /// <summary>Clothes, head, hair, hat, chain and portrait (painted art).</summary>
        public Look Look;

        public Pawn(int id, string firstName, string lastName, string nickname, Color32 shirt, Vector2Int cell)
        {
            Id = id;
            Look = Look.Young(id);
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
        public bool InHospital => HospitalUntil > 0;
        public bool IsAggressive(long now) => AggressiveUntil > now;
        public bool Titled;

        /// <summary>The player's own character, moved with WASD. The AI only takes over when the player leaves him be.</summary>
        public bool Controlled;
        public long LastInputTick = -1000000;
        public Vector2? AimPoint;        // where the player is aiming with the mouse
        public float Stamina = 1f;       // the player's breath for running
        public bool Winded;              // ran out of breath: no running until it comes back
        public Vector2 Velocity;         // the player's scooter keeps its momentum
        public bool StolenScooter;       // riding someone else's, for Nanninella
        public float Heading;            // the player's scooter: degrees, 0 = north, positive = turning left
        public float ScooterSpeed;       // forward speed along the heading, cells per second
        public bool Pillion;             // riding on the back of the boss's scooter

        /// <summary>The working day, hour by hour: free time, work (their job), or sleep.</summary>
        public enum Shift : byte { Free, Work, Sleep }
        public readonly Shift[] Schedule = DefaultSchedule();

        public static Shift[] DefaultSchedule()
        {
            var s = new Shift[24];
            for (int h = 0; h < 24; h++) s[h] = h < 7 ? Shift.Sleep : h < 9 ? Shift.Free : h < 21 ? Shift.Work : Shift.Free;
            return s;
        }

        /// <summary>The working hours as one block: from (inclusive) to (exclusive), wrapping past midnight.</summary>
        public (int from, int to) WorkHours
        {
            get
            {
                for (int h = 0; h < 24; h++)
                    if (Schedule[h] == Shift.Work && Schedule[(h + 23) % 24] != Shift.Work)
                    {
                        int end = h;
                        while (Schedule[end % 24] == Shift.Work && end < h + 24) end++;
                        return (h, end % 24);
                    }
                return (9, 21);
            }
        }

        /// <summary>Works from one hour to another; sleeps eight hours starting two after work ends; free otherwise.</summary>
        public void SetWorkHours(int from, int to)
        {
            int len = ((to - from) % 24 + 24) % 24;
            if (len == 0) len = 24;
            for (int h = 0; h < 24; h++) Schedule[h] = Shift.Free;
            for (int i = 0; i < len; i++) Schedule[(from + i) % 24] = Shift.Work;
            for (int i = 0; i < 8; i++)
            {
                int h = (to + 2 + i) % 24;
                if (Schedule[h] != Shift.Work) Schedule[h] = Shift.Sleep;
            }
        }

        public string ScheduleString
        {
            get
            {
                var c = new char[24];
                for (int h = 0; h < 24; h++) c[h] = Schedule[h] == Shift.Work ? 'W' : Schedule[h] == Shift.Sleep ? 'S' : 'F';
                return new string(c);
            }
            set
            {
                if (string.IsNullOrEmpty(value) || value.Length != 24) return;
                for (int h = 0; h < 24; h++) Schedule[h] = value[h] == 'W' ? Shift.Work : value[h] == 'S' ? Shift.Sleep : Shift.Free;
            }
        }

        public Vector2 Forward => new Vector2(-Mathf.Sin(Heading * Mathf.Deg2Rad), Mathf.Cos(Heading * Mathf.Deg2Rad));
        public const int ManualHoldTicks = 1500;
        /// <summary>Off by default: Gennaro only moves when you move him. Z lets him get on with things alone.</summary>
        public bool Autopilot;
        public bool Manual(long now) => Controlled && (!Autopilot || now - LastInputTick < ManualHoldTicks);              // a boss is "Don" only once the family is somebody
        public string DisplayName => IsBoss ? (Titled ? $"Don {FirstName}" : FirstName) : Nickname;
        public bool InMentalBreak(long now) => BreakUntilTick > now;
        public bool IsLyingLow(long now) => LyingLowUntil > now;

        /// <summary>On the map, alive and able to stand.</summary>
        public bool CanAct => !Dead && !InJail && !InHospital && !Departed && !Health.Downed;

        /// <summary>Alive and on the map (possibly downed).</summary>
        public bool IsPresent => !Dead && !InJail && !InHospital && !Departed;

        /// <summary>Injuries slow you down; a scooter speeds you up.</summary>
        public bool Riding;              // on the scooter right now: only on streets, never indoors
        public float SpeedFactor => (1f - Health.Pain * 0.5f) * (Riding ? 2.3f : 1f) * (staggered ? 0.45f : 1f);
        bool staggered;
        public void UpdateStagger(long now) => staggered = now < StaggerUntil;

        /// <summary>A need bad enough to drop work for.</summary>
        public bool HasUrgentNeed =>
            Needs.Food < Needs.RavenousBelow ||
            Needs.Rest < Needs.ExhaustedBelow ||
            (Health.NeedsTending && Health.BleedRatePerDay > 0.3f);

        public static Vector2 CellCenter(Vector2Int c) => Mover.CellCenter(c);

        public void Tick(Game game)
        {
            if (Dead || InJail || Departed) return;
            long now = game.Ticks.TicksGame;
            UpdateStagger(now);

            if (InHospital)
            {
                // Fed and cared for: only the wounds tick, and they heal.
                Needs.Food = Mathf.Max(Needs.Food, 0.7f);
                Needs.Rest = Mathf.Min(1f, Needs.Rest + 1f / 20000f);
                Health.Tick(this);
                if (Health.IsDeadly) game.Pawns.Kill(this, "their wounds, in hospital", game);
                return;
            }

            if (Faction == Faction.Rival)
            {
                TickRival(game);
                return;
            }

            Needs.Tick(this);
            Health.Tick(this);
            if (Health.IsDeadly)
            {
                game.Pawns.Kill(this, Health.BloodLoss >= 1f ? "blood loss" : "starvation", game);
                return;
            }

            if ((now + Id * 37) % CheckInterval == 0) PeriodicCheck(game, now);

            if (Health.Downed && !(CurrentJob is DownedJob)) ForceJob(new DownedJob(this), game);
            // The player is steering: nothing happens unless he asked for it. But a body left alone
            // for a while still eats when it's starving and sleeps when it can't stand.
            if (CurrentJob == null && Manual(now))
            {
                if (!game.PlayerSelfCare || now - LastInputTick < ManualHoldTicks || !HasUrgentNeed) return;
                CurrentJob = JobGiver.ForNeeds(this, game);
                if (CurrentJob == null) return;
            }
            if (CurrentJob == null) CurrentJob = JobGiver.Next(this, game);
            if (CurrentJob.Tick(game))
            {
                bool failed = CurrentJob.Failed;
                EndCurrentJob(game);
                // Back off briefly after a failure so an unreachable target isn't re-pathed every tick.
                if (failed) CurrentJob = new WaitJob(this, 90, "Idle");
            }
        }

        // Rivals live off-map: no needs or mood, only wounds and their mission.
        void TickRival(Game game)
        {
            Health.Tick(this);
            if (Health.IsDeadly)
            {
                game.Pawns.Kill(this, "gunshot wounds", game);
                return;
            }
            if (Health.Downed && !(CurrentJob is DownedJob)) ForceJob(new DownedJob(this), game);
            if (CurrentJob == null) CurrentJob = RivalAI.Next(this, game);
            if (CurrentJob.Tick(game))
            {
                bool failed = CurrentJob.Failed;
                EndCurrentJob(game);
                if (failed) CurrentJob = new WaitJob(this, 90, "Idle");
            }
        }

        void PeriodicCheck(Game game, long now)
        {
            if (Bed.HasValue && (game.Map.GetBuilding(Bed.Value) != BuildingDefs.Bed ||
                                 (!game.PlayerBase.Contains(Bed.Value) && !game.Properties.IsOwnedHome(Bed.Value)))) Bed = null;

            Mood.Recalculate(this, now);

            if (BreakUntilTick > 0 && now >= BreakUntilTick)
            {
                BreakUntilTick = 0;
                Mood.AddMemory("vented", "Vented my frustration", 12f, now, GameClock.TicksPerDay);
                game.Log.Add($"{Nickname} has calmed down.", MessageKind.Good);
            }

            if (!Controlled && !InMentalBreak(now) && !Drafted && CanAct && Mood.Value < Mood.BreakBelow && game.Rng.NextDouble() < 0.012)
            {
                StartMentalBreak(game, now);
                return;
            }

            if (CurrentJob == null || Drafted || InMentalBreak(now) || !CanAct || Manual(now)) return;
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
            if (drafted && IsBoss && game.Org.Hiding) return; // stays in the bunker
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

        public void SendToHospital(long until, Game game)
        {
            EndCurrentJob(game);
            HospitalUntil = until;
            Drafted = false;
            ForcedTarget = null;
            Sleep = SleepState.InBed; // heals faster
            TendedBy = null;
        }

        public void LeaveHospital(Vector2Int at)
        {
            HospitalUntil = 0;
            Sleep = SleepState.Awake;
            Teleport(at);
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
