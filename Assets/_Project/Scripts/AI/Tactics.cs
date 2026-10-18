using OSistema.Combat;
using OSistema.Core;
using OSistema.Pawns;
using UnityEngine;

namespace OSistema.AI
{
    /// <summary>
    /// How a gunman fights, crew or clan: close in until he can see the target, get behind something
    /// if there's cover nearby, then hold there and shoot for a while before thinking again. No
    /// re-pathing every tick, no dancing in the open.
    /// </summary>
    public sealed class FightState
    {
        public Walker Walker;
        public int Hold;          // ticks to stay put and shoot
        public int Blind;         // ticks without a line of sight while holding
        public int Repath;
        public bool TriedCover;
    }

    public static class Tactics
    {
        public const float EngageRange = 8.5f;

        /// <summary>One tick of fighting <paramref name="target"/>. The combat system does the shooting.</summary>
        public static void Fight(Pawn pawn, Pawn target, FightState s, Game game)
        {
            var rng = game.Rng;
            float dist = (target.Position - pawn.Position).magnitude;
            bool sight = CombatSystem.HasLineOfSight(game.Map, pawn.Cell, target.Cell);
            pawn.AimAt(target);

            // On the way somewhere: finish getting there.
            if (s.Walker != null)
            {
                var status = s.Walker.Tick(pawn, game);
                if (status == WalkStatus.Walking && !(sight && dist <= EngageRange && !s.TriedCover && --s.Repath <= 0)) return;
                s.Walker = null;
                pawn.StopMoving();
                s.Hold = 90 + rng.Next(90);
                return;
            }

            // Holding: stand and shoot. If the target's out of sight for a second and a half, move.
            if (s.Hold > 0)
            {
                s.Hold--;
                s.Blind = sight ? 0 : s.Blind + 1;
                if (s.Blind > 90) s.Hold = 0;
                return;
            }

            if (sight && dist <= EngageRange)
            {
                // In range: duck behind something close by, once; otherwise hold here.
                if (!s.TriedCover && !CombatSystem.IsInCover(game.Map, pawn.Cell, target.Position))
                {
                    s.TriedCover = true;
                    var cover = CombatSystem.FindCover(game.Map, pawn.Cell, target.Position, 3);
                    if (cover.HasValue && cover.Value != pawn.Cell)
                    {
                        s.Walker = new Walker(cover.Value, adjacent: false, speedMultiplier: 1.3f);
                        s.Repath = 40;
                        return;
                    }
                }
                s.Hold = 120 + rng.Next(120);
                return;
            }

            // Too far, or no line: close in. Re-path about once a second, not every tick.
            s.TriedCover = false;
            s.Walker = new Walker(target.Cell, adjacent: true, speedMultiplier: 1.15f);
            s.Repath = 60;
        }
    }

    /// <summary>
    /// A crew member with a gun, when the clan comes shooting on the family's turf: he fights back
    /// until they're down or gone, then goes back to what he was doing.
    /// </summary>
    public sealed class DefendJob : Job
    {
        Pawn target;
        readonly FightState state = new FightState();
        int quiet;

        public DefendJob(Pawn pawn, Pawn target) : base(pawn) => this.target = target;

        public override string Label => target != null ? $"Fighting {target.Nickname}" : "On guard";

        public override bool Tick(Game game)
        {
            if (Pawn.Health.Pain > 0.55f) return true;   // hurt: done fighting
            if (target == null || !target.IsPresent || target.Health.Downed || target.Dead)
            {
                target = CombatSystem.NearestHostile(Pawn, game, 16f);
                if (target == null) return ++quiet > 120;
                quiet = 0;
            }
            Tactics.Fight(Pawn, target, state, game);
            return false;
        }

        public override void OnEnd(Game game) => Pawn.StopMoving();
    }
}
