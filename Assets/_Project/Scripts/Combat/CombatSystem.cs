using System;
using OSistema.AI;
using OSistema.Core;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Combat
{
    public readonly struct ShotEvent
    {
        public readonly Vector2 From;
        public readonly Vector2 To;
        public readonly bool Hit;
        public readonly bool Melee;
        public readonly bool InCover;      // the bullet was stopped by cover

        public ShotEvent(Vector2 from, Vector2 to, bool hit, bool melee, bool inCover = false)
        {
            From = from;
            To = to;
            Hit = hit;
            Melee = melee;
            InCover = inCover;
        }
    }

    /// <summary>
    /// Real-time, short and lethal. Armed pawns shoot hostiles in range with line of sight;
    /// unarmed ones can only throw punches. One bullet can end a key character, which is the point:
    /// violence is a last resort.
    /// </summary>
    public sealed class CombatSystem
    {
        public const float PistolRange = 9f;
        public const float MeleeRange = 1.6f;
        const int ThinkEvery = 6;
        const int AggroTicks = 3000;

        static readonly (string part, float weight)[] BodyParts =
        {
            ("Torso", 0.35f), ("Left arm", 0.15f), ("Right arm", 0.15f), ("Left leg", 0.15f), ("Right leg", 0.15f), ("Head", 0.05f),
        };

        public event Action<ShotEvent> Shot;

        /// <summary>Raised when shooting starts after a quiet spell: the HUD sounds the alarm.</summary>
        public event Action<Vector2> EngagementStarted;

        const int QuietTicks = 900;
        const float CoverFactor = 0.45f;

        /// <summary>Where and when the last shot rang out: civilians run from it.</summary>
        public Vector2 LastShotPosition { get; private set; }
        public long LastShotTick { get; private set; } = -100000;

        void Fired(Vector2 at, Game game)
        {
            if (game.Ticks.TicksGame - LastShotTick > QuietTicks) EngagementStarted?.Invoke(at);
            LastShotPosition = at;
            LastShotTick = game.Ticks.TicksGame;
        }

        public void Tick(Game game)
        {
            long now = game.Ticks.TicksGame;
            if (now % ThinkEvery != 0) return;

            var all = game.Pawns.All;
            for (int i = 0; i < all.Count; i++)
            {
                var attacker = all[i];
                if (!attacker.CanAct || now < attacker.NextAttackTick) continue;
                var target = PickTarget(attacker, game, now);
                if (target != null) Attack(attacker, target, game, now);
            }
        }

        /// <summary>Who starts shooting at whom.</summary>
        static bool WantsToFight(Pawn a, Pawn b, long now)
        {
            if (a.Faction == b.Faction || !b.IsPresent || b.Health.Downed) return false;
            // Crew only open fire on your orders, or to defend themselves from rivals who are already shooting.
            if (a.Faction == Faction.Crew) return a.Drafted || (a.Armed && b.IsAggressive(now));
            return a.IsAggressive(now);
        }

        Pawn PickTarget(Pawn attacker, Game game, long now)
        {
            float range = attacker.Armed ? PistolRange : MeleeRange;

            // A focus-fire order wins whenever the target is in range and in sight.
            var forced = attacker.ForcedTarget;
            if (forced != null && WantsToFight(attacker, forced, now))
            {
                float d = (forced.Position - attacker.Position).magnitude;
                if (d <= range && (!attacker.Armed || HasLineOfSight(game.Map, attacker.Cell, forced.Cell))) return forced;
            }

            Pawn best = null;
            float bestDist = range;
            foreach (var other in game.Pawns.All)
            {
                if (!WantsToFight(attacker, other, now)) continue;
                float d = (other.Position - attacker.Position).magnitude;
                if (d > bestDist) continue;
                if (attacker.Armed && !HasLineOfSight(game.Map, attacker.Cell, other.Cell)) continue;
                best = other;
                bestDist = d;
            }
            return best;
        }

        void Attack(Pawn attacker, Pawn target, Game game, long now)
        {
            bool melee = !attacker.Armed;
            float dist = (target.Position - attacker.Position).magnitude;
            float chance = melee
                ? 0.55f + 0.03f * attacker.IntimidationSkill
                : 0.35f + 0.05f * attacker.ShootingSkill - 0.025f * dist;
            chance *= 1f - attacker.Health.Pain * 0.5f;
            bool covered = !melee && IsInCover(game.Map, target.Cell, attacker.Position);
            if (covered) chance *= CoverFactor;
            chance = Mathf.Clamp(chance, 0.05f, 0.9f);

            var rng = game.Rng;
            attacker.AimAt(target);
            attacker.NextAttackTick = now + (melee ? 60 : 75 + rng.Next(0, 30));
            bool hit = rng.NextDouble() < chance;

            var to = target.Position;
            if (!hit) to += new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 2.5f;
            Shot?.Invoke(new ShotEvent(attacker.Position, to, hit, melee, covered && !hit));

            if (!melee)
            {
                game.Territory.AddHeat(attacker.Cell, 0.8f); // gunfire brings attention
                Fired(attacker.Position, game);
            }
            if (attacker.Faction == Faction.Crew && rng.NextDouble() < 0.08) attacker.Say("fight");

            OnAttacked(target, attacker, game, now);
            if (hit) Wound(target, melee, game);
        }

        static void OnAttacked(Pawn target, Pawn attacker, Game game, long now)
        {
            target.AggressiveUntil = Math.Max(target.AggressiveUntil, now + AggroTicks);
            if (attacker.Faction == Faction.Crew && target.Faction == Faction.Rival) game.Rival.OnCrewAttacked(game);

            if (target.Faction == Faction.Rival && !target.Armed && target.CanAct && target.Mission == RivalMission.Pusher)
            {
                // An unarmed rival pusher runs for it.
                target.Mission = RivalMission.Leave;
                target.EndCurrentJob(game);
            }
            else if (target.Faction == Faction.Crew && !target.Drafted && !target.Armed && target.CanAct && !(target.CurrentJob is FleeJob))
            {
                target.ForceJob(new FleeJob(target, null, game), game);
            }
        }

        static void Wound(Pawn target, bool melee, Game game)
        {
            target.LastHitTime = Time.unscaledTime;
            var rng = game.Rng;
            string part = PickPart(rng);
            long now = game.Ticks.TicksGame;

            if (melee)
            {
                target.Health.AddInjury("Bruise", part, 0.05f + (float)rng.NextDouble() * 0.05f, 0f);
                return;
            }

            bool head = part == "Head";
            float severity = head ? 0.7f + (float)rng.NextDouble() * 0.25f : 0.2f + (float)rng.NextDouble() * 0.2f;
            float bleed = head ? 2f : 0.5f + (float)rng.NextDouble() * 0.9f;
            target.Health.AddInjury("Gunshot wound", part, severity, bleed);

            if (target.Faction == Faction.Crew)
            {
                target.Mood.AddMemory("shot", "Got shot", -10f, now, GameClock.TicksPerDay * 2);
                target.Say("hurt");
                game.Log.Add($"{target.DisplayName} was shot in the {part.ToLower()}!", MessageKind.Bad);
            }
        }

        static string PickPart(System.Random rng)
        {
            double roll = rng.NextDouble();
            foreach (var (part, weight) in BodyParts)
            {
                if (roll < weight) return part;
                roll -= weight;
            }
            return "Torso";
        }

        /// <summary>
        /// Is something solid between the target and the shooter, right next to the target?
        /// Walls, cars, trees, benches, stalls… Shooting from point blank ignores cover.
        /// </summary>
        public static bool IsInCover(Map map, Vector2Int target, Vector2 shooter)
        {
            var toShooter = shooter - Mover.CellCenter(target);
            if (toShooter.sqrMagnitude < 2.5f) return false;
            toShooter.Normalize();
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int x = target.x + dx, y = target.y + dy;
                if (!map.InBounds(x, y)) continue;
                var b = map.GetBuilding(x, y);
                if (b == null || !b.Cover) continue;
                if (Vector2.Dot(new Vector2(dx, dy).normalized, toShooter) > 0.55f) return true;
            }
            return false;
        }

        /// <summary>The nearest free cell within reach that has cover against a threat and still a line of fire.</summary>
        public static Vector2Int? FindCover(Map map, Vector2Int from, Vector2 threat, int radius)
        {
            Vector2Int? best = null;
            float bestScore = float.MaxValue;
            var threatCell = new Vector2Int(Mathf.FloorToInt(threat.x), Mathf.FloorToInt(threat.y));
            foreach (var c in GridUtil.CellsAround(from, radius))
            {
                if (!map.InBounds(c) || !map.IsPassable(c)) continue;
                if (!IsInCover(map, c, threat)) continue;
                if (!HasLineOfSight(map, c, threatCell)) continue; // cover you can't shoot from is just hiding
                float score = (c - from).sqrMagnitude;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }
            return best;
        }

        /// <summary>Bresenham line between cells; walls (impassable cells) block the shot.</summary>
        public static bool HasLineOfSight(Map map, Vector2Int a, Vector2Int b)
        {
            int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                if ((x0 != a.x || y0 != a.y) && (x0 != b.x || y0 != b.y) && !map.IsPassable(x0, y0)) return false;
                if (x0 == x1 && y0 == y1) return true;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>Shots fired into the air during a stesa: noise and fear, no target.</summary>
        public void FireIntoAir(Pawn shooter, Game game)
        {
            var rng = game.Rng;
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            var to = shooter.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (3f + (float)rng.NextDouble() * 3f);
            Shot?.Invoke(new ShotEvent(shooter.Position, to, false, false));
            game.Territory.AddHeat(shooter.Cell, 0.8f);
            Fired(shooter.Position, game);
        }
    }
}
