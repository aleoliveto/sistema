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

        // When the recent shots were fired: shooting that goes on brings the police (see Response).
        readonly System.Collections.Generic.Queue<long> recentShots = new System.Collections.Generic.Queue<long>();

        public int ShotsSince(long tick)
        {
            while (recentShots.Count > 0 && recentShots.Peek() < tick) recentShots.Dequeue();
            return recentShots.Count;
        }

        void Fired(Vector2 at, Game game)
        {
            recentShots.Enqueue(game.Ticks.TicksGame);
            if (recentShots.Count > 64) recentShots.Dequeue();
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

        /// <summary>When the player last pulled the trigger: bodyguards join in.</summary>
        public long LastPlayerShotTick { get; private set; } = -100000;

        /// <summary>Who starts shooting at whom.</summary>
        bool WantsToFight(Pawn a, Pawn b, long now)
        {
            if (!b.IsPresent || b.Health.Downed) return false;
            // Two clans' soldiers: enemies whenever either of them has come looking for a fight.
            if (a.Faction == Faction.Rival && b.Faction == Faction.Rival)
                return a.Clan != b.Clan && (a.IsAggressive(now) || b.IsAggressive(now));
            if (a.Faction == b.Faction) return false;
            if (a.Controlled) return false;   // the player aims and fires himself
            if (a.Faction == Faction.Crew)
            {
                // Bodyguards fight for the boss: anyone shooting, or anyone the boss is shooting at.
                if (a.Role == CrewRole.Bodyguard && a.Armed) return b.IsAggressive(now) || now - LastPlayerShotTick < 900;
                return a.Drafted || (a.Armed && (b.IsAggressive(now) || (a.OnOperationUntil > now && b.Faction == Faction.Rival)));
            }
            return a.IsAggressive(now);
        }

        /// <summary>
        /// The player shoots (or swings, unarmed) towards a point. The bullet hits the first person
        /// along the line, if the shot is good; walls stop it. Shooting a passer-by or a policeman
        /// is the worst thing you can do.
        /// </summary>
        public const float PlayerPistolRange = 12f;

        /// <summary>
        /// How wide the player's shots scatter, in degrees either side of the aim: steady hands and
        /// skill keep it tight; walking, running, pain and firing fast open it up.
        /// </summary>
        public static float SpreadFor(Pawn p, bool moving, bool running)
        {
            float s = Mathf.Max(1.2f, 6.5f - 0.5f * p.ShootingSkill);
            if (moving) s += running ? 9f : 3.5f;
            if (p.Riding) s += 6f;
            s += p.Health.Pain * 10f + p.Bloom;
            return Mathf.Min(s, 25f);
        }

        /// <summary>The last shot of the player's that drew blood: the HUD flashes a hit marker.</summary>
        public event Action<Vector2, bool> PlayerHit;

        public void PlayerAttack(Pawn shooter, Vector2 point, Game game, float spread = 0f)
        {
            long now = game.Ticks.TicksGame;
            if (!shooter.CanAct || now < shooter.NextAttackTick) return;
            bool melee = !shooter.Armed;
            float range = melee ? MeleeRange : PlayerPistolRange;
            var from = shooter.Position;
            var dir = point - from;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();
            var rng = game.Rng;
            if (!melee && spread > 0f)
            {
                // Where the bullet actually goes: somewhere in the cone, more often near the middle.
                float off = ((float)rng.NextDouble() + (float)rng.NextDouble() - 1f) * spread * Mathf.Deg2Rad;
                dir = new Vector2(dir.x * Mathf.Cos(off) - dir.y * Mathf.Sin(off), dir.x * Mathf.Sin(off) + dir.y * Mathf.Cos(off));
            }
            shooter.NextAttackTick = now + (melee ? 35 : 11);   // a semi-automatic: as fast as you can pull the trigger

            // Walk the line: who's on it first, and where does a wall stop it?
            float wallAt = range;
            for (float t = 0.3f; t < range; t += 0.2f)
            {
                var c = Vector2Int.FloorToInt(from + dir * t);
                if (c != shooter.Cell && (!game.Map.InBounds(c) || !game.Map.IsPassable(c.x, c.y))) { wallAt = t; break; }
            }
            Pawn hitPawn = null;
            Npcs.Npc hitNpc = null;
            float best = wallAt;
            foreach (var p in game.Pawns.All)
            {
                if (p == shooter || !p.IsPresent || p.Dead) continue;
                if (Along(from, dir, p.Position, best, out float t)) { best = t; hitPawn = p; }
            }
            foreach (var n in game.Npcs.All)
            {
                if (n.Despawned || n.Kind == Npcs.NpcKind.Scooter || n.Kind == Npcs.NpcKind.Ambulance) continue;
                if (Along(from, dir, n.Mover.Position, best, out float t)) { best = t; hitNpc = n; hitPawn = null; }
            }

            var to = from + dir * best;
            bool hit;
            bool covered = hitPawn != null && !melee && IsInCover(game.Map, hitPawn.Cell, from);
            if (melee)
            {
                float chance = (0.6f + 0.03f * shooter.IntimidationSkill) * (1f - shooter.Health.Pain * 0.5f);
                hit = (hitPawn != null || hitNpc != null) && rng.NextDouble() < Mathf.Clamp(chance, 0.05f, 0.92f);
            }
            else
            {
                // The aim decides: whoever the bullet's line crosses is hit, unless cover takes it.
                hit = hitPawn != null || hitNpc != null;
                if (hit && covered && rng.NextDouble() < 0.55) hit = false;
            }
            if (!hit && (hitPawn != null || hitNpc != null)) to += new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 1.5f;

            Shot?.Invoke(new ShotEvent(from, to, hit, melee, covered && !hit));
            if (!melee)
            {
                shooter.Learn(Skill.Shooting, 3f, game);
                game.Territory.AddHeat(shooter.Cell, 1.2f);
                Fired(from, game);
                LastPlayerShotTick = now;
            }

            if (hitPawn != null)
            {
                OnAttacked(hitPawn, shooter, game, now);
                if (hit)
                {
                    Wound(hitPawn, melee, game, dir);
                    PlayerHit?.Invoke(hitPawn.Position, hitPawn.Health.Downed);
                }
                if (hit && hitPawn.Faction == Faction.Crew)
                {
                    hitPawn.Loyalty = Mathf.Max(0f, hitPawn.Loyalty - 25f);
                    game.Log.Add($"You hit {hitPawn.DisplayName}. He won't forget it.", MessageKind.Bad);
                }
            }
            else if (hitNpc != null && hit) ShotBystander(hitNpc, melee, game);
        }

        static bool Along(Vector2 from, Vector2 dir, Vector2 at, float maxT, out float t)
        {
            var rel = at - from;
            t = Vector2.Dot(rel, dir);
            if (t <= 0.2f || t >= maxT) return false;
            return (rel - dir * t).sqrMagnitude < 0.45f * 0.45f;
        }

        static void ShotBystander(Npcs.Npc npc, bool melee, Game game)
        {
            bool cop = npc.Kind == Npcs.NpcKind.Police;
            var zone = game.Territory.ZoneAt(npc.Mover.Cell);
            if (melee)
            {
                game.Org.AddEvidence(cop ? 10f : 2f, game);
                game.Territory.AddHeat(npc.Mover.Cell, cop ? 25f : 6f);
                game.Log.Add(cop ? "You punched a policeman. Every patrol in the city will know your face." : "You punched a passer-by. People saw.", MessageKind.Bad);
                return;
            }
            npc.Despawned = true;
            game.Org.AddEvidence(cop ? 40f : 20f, game);
            game.Territory.AddHeat(npc.Mover.Cell, cop ? 60f : 35f);
            game.Org.Respect = Mathf.Max(0f, game.Org.Respect - (cop ? 0f : 8f));
            game.Log.Add(cop ? "You shot a policeman. There is no going back from this." : "You shot an innocent passer-by. The rione turns its back on the family.", MessageKind.Bad, npc.Mover.Cell);
            game.Newspaper.Report(game, cop ? 9 : 7, cop ? $"Policeman shot in {zone?.Name ?? "the old town"}" : $"Passer-by shot dead in {zone?.Name ?? "the old town"}",
                cop ? "The interior minister is flying to Naples. Hundreds of officers are being sent to the city." : "An innocent caught in the crossfire. The cardinal will say mass for the victim on Sunday.");
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
            var all = game.Pawns.All;
            foreach (var other in all)
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
            if (!melee) attacker.Learn(Skill.Shooting, 3f, game);
            attacker.NextAttackTick = now + (melee ? 45 : 38 + rng.Next(0, 25));
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
            if (hit) Wound(target, melee, game, (target.Position - attacker.Position).normalized);
        }

        static void OnAttacked(Pawn target, Pawn attacker, Game game, long now)
        {
            target.AggressiveUntil = Math.Max(target.AggressiveUntil, now + AggroTicks);
            if (attacker.Faction == Faction.Crew && target.Faction == Faction.Rival)
            {
                var clan = game.ClanOf(target);
                clan.OnCrewAttacked(game);
                clan.OnRivalAttacked(target, attacker, game);
            }

            if (target.Faction == Faction.Rival && target.CanAct && (target.Health.Pain > 0.45f || target.Health.BloodLoss > 0.3f))
            {
                // Hurt badly enough: they've had enough, and limp away.
                target.Mission = RivalMission.Leave;
                target.EndCurrentJob(game);
            }
            else if (target.Faction == Faction.Crew && !target.Controlled && !target.Drafted && !target.Armed && target.CanAct && !(target.CurrentJob is FleeJob))
            {
                target.ForceJob(new FleeJob(target, null, game), game);
            }
        }

        static void Wound(Pawn target, bool melee, Game game, Vector2 dir)
        {
            target.LastHitTime = Time.unscaledTime;
            target.LastHitDir = dir;
            var rng = game.Rng;
            string part = PickPart(rng);
            long now = game.Ticks.TicksGame;
            // Hit: a stumble, a moment before they can shoot back, and a step back from the impact.
            target.StaggerUntil = now + (melee ? 25 : 45);
            target.NextAttackTick = System.Math.Max(target.NextAttackTick, now + (melee ? 20 : 40));
            if (!target.Controlled && !target.Riding) target.Mover.Slide(dir * (melee ? 0.25f : 0.35f), game.Map);
            if (target.Faction == Faction.Rival && target.CanAct && rng.NextDouble() < 0.6) target.Say("hurt");

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
