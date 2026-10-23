using OSistema.Core;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.AI
{
    /// <summary>Eat a home meal from the pantry, or pay for a pizza at a pizzeria.</summary>
    public sealed class EatJob : Job
    {
        const int EatTicks = 450;

        readonly Walker walker;
        bool atShop;
        int eatLeft = EatTicks;
        bool eating;

        public EatJob(Pawn pawn, Vector2Int source, bool atShop) : base(pawn)
        {
            walker = new Walker(source, adjacent: true);
            this.atShop = atShop;
        }

        public override string Label => eating
            ? (atShop ? "Eating a pizza" : "Eating")
            : (atShop ? "Going for a pizza" : "Getting food");

        public override float Progress => eating ? 1f - eatLeft / (float)EatTicks : -1f;

        public override bool Tick(Game game)
        {
            if (!eating)
            {
                var status = walker.Tick(Pawn, game);
                var stock = game.Stock;
                // Can't reach the pantry (or it ran out on the way): go for a pizza instead of trying again and again.
                if (!atShop && (status == WalkStatus.Failed || (status == WalkStatus.Arrived && stock.Meals <= 0)) &&
                    stock.Cash >= Prices.Pizza && JobGiver.TryFindPizzeria(game, Pawn.Cell, out var pizzeria))
                {
                    atShop = true;
                    walker.Retarget(pizzeria);
                    return false;
                }
                if (status == WalkStatus.Failed) return Fail();
                if (status == WalkStatus.Walking) return false;

                if (atShop)
                {
                    if (stock.Cash < Prices.Pizza) return Fail();
                    stock.Cash -= Prices.Pizza;
                }
                else
                {
                    if (stock.Meals <= 0) return Fail();
                    stock.Meals--;
                }
                eating = true;
                return false;
            }

            if (--eatLeft > 0) return false;

            long now = game.Ticks.TicksGame;
            if (atShop)
            {
                Pawn.Needs.Eat(0.9f);
                Pawn.Mood.AddMemory("pizza", "Had a pizza", 6f, now, GameClock.TicksPerDay / 2);
                Pawn.Say("eat_pizza");
            }
            else
            {
                Pawn.Needs.Eat(0.8f);
                Pawn.Mood.AddMemory("home_meal", "Ate a home-cooked meal", 3f, now, GameClock.TicksPerDay / 2);
            }
            return true;
        }
    }

    /// <summary>
    /// Sleep in a bed of the pawn's house, or on the floor if there's none. The bed is theirs only while
    /// they lie in it: once they get up, whoever's shift is over can have it.
    /// </summary>
    public sealed class SleepJob : Job
    {
        readonly Vector2Int? bed;
        readonly Walker walker;
        bool asleep, claimed;

        /// <param name="bed">A free bed in their house, or null to sleep on the floor.</param>
        /// <param name="floorSpot">Where to lie down without a bed; null sleeps on the spot.</param>
        public SleepJob(Pawn pawn, Vector2Int? bed, Vector2Int? floorSpot) : base(pawn)
        {
            this.bed = bed;
            var target = bed ?? floorSpot;
            if (target.HasValue) walker = new Walker(target.Value, adjacent: false);
        }

        public override string Label => !asleep ? "Going to bed" : Pawn.Sleep == SleepState.InBed ? "Sleeping" : "Sleeping on the floor";

        public override float Progress => asleep ? Pawn.Needs.Rest : -1f;

        public override bool Tick(Game game)
        {
            if (!claimed && bed.HasValue)
            {
                // Someone from the other shift got there first.
                if (Housing.BedTaken(bed.Value, Pawn, game)) return Fail();
                Pawn.Bed = bed;
                claimed = true;
            }
            if (!asleep)
            {
                if (walker != null)
                {
                    var status = walker.Tick(Pawn, game);
                    if (status == WalkStatus.Failed) return Fail();
                    if (status == WalkStatus.Walking) return false;
                }
                asleep = true;
                Pawn.Sleep = bed.HasValue ? SleepState.InBed : SleepState.OnFloor;
                // Lie down in the middle of the bed, not at its foot.
                if (bed.HasValue && game.Map.GetBuilding(bed.Value) == World.BuildingDefs.Bed)
                {
                    int rot = game.Map.GetRotation(bed.Value.x, bed.Value.y);
                    // Toward the pillow end: up the bed, as it was turned.
                    var up = rot == 0 ? Vector2.up : rot == 1 ? Vector2.left : rot == 2 ? Vector2.down : Vector2.right;
                    Pawn.Mover.Place(World.Footprint.Center(World.BuildingDefs.Bed, bed.Value, rot) + up * 0.2f);
                }
                return false;
            }

            bool rested = Pawn.Needs.Rest >= 0.99f;
            bool starving = Pawn.Needs.Food < Needs.RavenousBelow;
            if (!rested && !starving) return false;

            if (Pawn.Sleep == SleepState.OnFloor)
                Pawn.Mood.AddMemory("slept_floor", "Slept on the floor", -5f, game.Ticks.TicksGame, GameClock.TicksPerDay);
            return true;
        }

        public override void OnEnd(Game game)
        {
            Pawn.Sleep = SleepState.Awake;
            // Up and out: the bed is free for the next shift.
            if (claimed && Pawn.Bed == bed) Pawn.Bed = null;
        }
    }

    /// <summary>Patch up a crewmate (or yourself). Medicine from the farmacia makes it work much better.</summary>
    public sealed class TendJob : Job
    {
        const string WaitLabel = "Waiting for treatment";

        readonly Pawn patient;
        readonly Walker walker;
        readonly bool self;
        float workLeft = 360f;
        bool working;

        public TendJob(Pawn doctor, Pawn patient, Game game) : base(doctor)
        {
            this.patient = patient;
            self = doctor == patient;
            walker = new Walker(patient.Cell, adjacent: true);
            patient.TendedBy = doctor;
            if (!self && patient.CanAct && !patient.Drafted)
                patient.ForceJob(new WaitJob(patient, 900, WaitLabel, idle: false), game);
        }

        public override string Label => self ? "Tending own wounds" : $"Tending {patient.Nickname}";

        public override bool Tick(Game game)
        {
            if (patient.Dead || !patient.Health.NeedsTending) return true;

            if (self) working = true;

            if (!working)
            {
                if (walker.Target != patient.Cell) walker.Retarget(patient.Cell);
                var status = walker.Tick(Pawn, game);
                if (status == WalkStatus.Failed) return Fail();
                working = status == WalkStatus.Arrived;
                return false;
            }

            if (!self && !Walker.IsNextTo(Pawn.Cell, patient.Cell))
            {
                working = false;
                return false;
            }

            workLeft -= 1f + 0.12f * Pawn.MedicineSkill;
            if (workLeft > 0f) return false;

            var stock = game.Stock;
            bool medicine = stock.Medicine > 0;
            if (medicine) stock.Medicine--;
            float quality = 0.3f + 0.06f * Pawn.MedicineSkill + (medicine ? 0.3f : 0f) + ((float)game.Rng.NextDouble() - 0.5f) * 0.2f;
            if (self) quality *= 0.7f;
            quality = Mathf.Clamp01(quality);

            foreach (var injury in patient.Health.Injuries)
            {
                if (injury.Tended || injury.IsMalnutrition) continue;
                injury.Tended = true;
                injury.TendQuality = quality;
            }

            Pawn.Learn(Skill.Medicine, 15f, game);
            string who = self ? "their own" : $"{patient.Nickname}'s";
            string how = medicine ? "" : " without medicine";
            game.Log.Add($"{Pawn.Nickname} patched up {who} wounds{how} (quality {quality:P0}).", quality > 0.5f ? MessageKind.Good : MessageKind.Info);
            return true;
        }

        public override void OnEnd(Game game)
        {
            if (patient.TendedBy == Pawn) patient.TendedBy = null;
            if (!self && patient.CurrentJob is WaitJob wait && wait.Label == WaitLabel) patient.EndCurrentJob(game);
        }
    }

    /// <summary>Napoli is playing. Everyone in the basso watches.</summary>
    public sealed class WatchTvJob : Job
    {
        readonly Walker walker;
        int ticks = 1200;
        bool watching;

        public WatchTvJob(Pawn pawn, Vector2Int tv) : base(pawn) => walker = new Walker(tv, adjacent: true);

        public override string Label => watching ? "Watching Napoli on TV" : "Going to watch TV";
        public override bool IsIdle => true;

        public override bool Tick(Game game)
        {
            if (!watching)
            {
                var status = walker.Tick(Pawn, game);
                if (status == WalkStatus.Failed) return Fail();
                watching = status == WalkStatus.Arrived;
                return false;
            }
            if (--ticks > 0) return false;
            Pawn.Mood.AddMemory("tv", "Watched Napoli on the TV", 3f, game.Ticks.TicksGame, GameClock.TicksPerDay / 2);
            if (game.Rng.NextDouble() < 0.3) Pawn.Say("tv");
            return true;
        }
    }

    /// <summary>Too hurt to stand. Lies where they fell until the wounds heal enough.</summary>
    public sealed class DownedJob : Job
    {
        public DownedJob(Pawn pawn) : base(pawn) { }

        public override string Label => "Downed";

        public override bool Tick(Game game) => !Pawn.Health.Downed;
    }

    /// <summary>
    /// Mental break: wanders the rione ignoring orders and work until it passes. Also a shorter sulk with
    /// a reason, such as coming home tired to find every bed taken.
    /// </summary>
    public sealed class SulkJob : Job
    {
        readonly long until;
        readonly string why;
        Walker walker;
        int pause;

        public SulkJob(Pawn pawn) : base(pawn) { }

        /// <summary>A sulk of its own, for this long, whatever their mood.</summary>
        public SulkJob(Pawn pawn, long until, string why) : base(pawn)
        {
            this.until = until;
            this.why = why;
        }

        public override string Label => why != null ? $"Sulking: {why}" : "Sulking (mental break)";

        public override bool Tick(Game game)
        {
            long now = game.Ticks.TicksGame;
            if (!Pawn.InMentalBreak(now) && now >= until) return true;

            if (pause > 0)
            {
                pause--;
                return false;
            }

            if (walker == null)
            {
                var c = Pawn.Cell + new Vector2Int(game.Rng.Next(-12, 13), game.Rng.Next(-12, 13));
                if (!game.Map.IsPassable(c)) return false;
                walker = new Walker(c, adjacent: false);
            }

            if (walker.Tick(Pawn, game) != WalkStatus.Walking)
            {
                walker = null;
                pause = game.Rng.Next(300, 1200);
            }
            return false;
        }
    }
}
