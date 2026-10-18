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
        public bool InCover;
        public bool Retreating;   // hurt: out of sight until the health comes back
        public int Strafe;        // ticks until the next sidestep when there's no cover
    }

    public static class Tactics
    {
        public const float EngageRange = 9f;

        /// <summary>
        /// One tick of fighting <paramref name="target"/>. The combat system does the shooting; this
        /// decides where to stand. Close in until you can see him; get behind something (up to six
        /// cells away) and hold there; with no cover, keep sidestepping so you're hard to hit; badly
        /// hurt, break the line of sight and wait for the health to come back, then push again.
        /// </summary>
        public static void Fight(Pawn pawn, Pawn target, FightState s, Game game)
        {
            var rng = game.Rng;
            float dist = (target.Position - pawn.Position).magnitude;
            bool sight = CombatSystem.HasLineOfSight(game.Map, pawn.Cell, target.Cell);
            pawn.AimAt(target);

            // Hurt: get out of his sight and let the health come back.
            if (!s.Retreating && pawn.Hp < 40f)
            {
                var hide = HidingSpot(game, pawn.Cell, target.Cell, 7);
                if (hide.HasValue)
                {
                    s.Retreating = true;
                    s.Walker = new Walker(hide.Value, adjacent: false, speedMultiplier: 1.35f);
                    s.Hold = 0;
                    return;
                }
            }
            if (s.Retreating)
            {
                if (s.Walker != null && s.Walker.Tick(pawn, game) == WalkStatus.Walking) return;
                s.Walker = null;
                pawn.StopMoving();
                if (pawn.Hp >= 90f) { s.Retreating = false; s.TriedCover = false; }
                return;
            }

            // On the way somewhere: finish getting there.
            if (s.Walker != null)
            {
                var status = s.Walker.Tick(pawn, game);
                if (status == WalkStatus.Walking && --s.Repath > 0) return;
                if (status == WalkStatus.Walking && !(sight && dist <= EngageRange)) { s.Repath = 50; return; }
                s.Walker = null;
                pawn.StopMoving();
                s.InCover = CombatSystem.IsInCover(game.Map, pawn.Cell, target.Position);
                s.Hold = 90 + rng.Next(90);
                return;
            }

            // Holding: stand and shoot. Out of sight for a second and a half: move.
            if (s.Hold > 0)
            {
                s.Hold--;
                s.Blind = sight ? 0 : s.Blind + 1;
                if (s.Blind > 90) s.Hold = 0;
                // In the open: a sidestep every second or so, never a sitting duck.
                if (!s.InCover && --s.Strafe <= 0)
                {
                    s.Strafe = 50 + rng.Next(40);
                    var away = (pawn.Position - target.Position).normalized;
                    var side = new Vector2(-away.y, away.x) * (rng.NextDouble() < 0.5 ? 1f : -1f);
                    var step = Vector2Int.RoundToInt(pawn.Position + side * 1.6f - new Vector2(0.5f, 0.5f));
                    if (game.Map.InBounds(step) && game.Map.IsPassable(step)) { s.Walker = new Walker(step, adjacent: false, speedMultiplier: 1.2f); s.Repath = 40; }
                }
                return;
            }

            if (sight && dist <= EngageRange)
            {
                // In range: get behind something, once; otherwise hold here (strafing).
                if (!s.TriedCover && !CombatSystem.IsInCover(game.Map, pawn.Cell, target.Position))
                {
                    s.TriedCover = true;
                    var cover = CombatSystem.FindCover(game.Map, pawn.Cell, target.Position, 6);
                    if (cover.HasValue && cover.Value != pawn.Cell)
                    {
                        s.Walker = new Walker(cover.Value, adjacent: false, speedMultiplier: 1.35f);
                        s.Repath = 120;
                        return;
                    }
                }
                s.InCover = CombatSystem.IsInCover(game.Map, pawn.Cell, target.Position);
                s.Hold = 120 + rng.Next(120);
                return;
            }

            // Too far, or no line: close in. Re-path about once a second, not every tick.
            s.TriedCover = false;
            s.Walker = new Walker(target.Cell, adjacent: true, speedMultiplier: 1.15f);
            s.Repath = 60;
        }

        /// <summary>A cell nearby that the enemy can't see: round a corner, behind a wall.</summary>
        static Vector2Int? HidingSpot(Game game, Vector2Int from, Vector2Int enemy, int radius)
        {
            Vector2Int? best = null;
            int bestD = int.MaxValue;
            foreach (var c in OSistema.World.GridUtil.CellsAround(from, radius))
            {
                if (!game.Map.InBounds(c) || !game.Map.IsPassable(c)) continue;
                if (CombatSystem.HasLineOfSight(game.Map, c, enemy)) continue;
                int d = (c - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
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
