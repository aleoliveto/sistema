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
    /// A rival clan holding part of the district. It runs its own piazze, resents yours when they
    /// get close, and answers with a stesa (shots in the air to frighten) or an agguato (an ambush to kill).
    /// </summary>
    public sealed class RivalClan
    {
        public const float StesaHostility = 40f;
        public const float HitHostility = 70f;
        const float NearDistance = 25f;
        const int StartingSoldiers = 8;

        public int Id;               // index in Game.Clans
        public Color32 Colour;       // on the territory map
        public float Intel;          // 0..100: how much the family knows about them (names, habits, the boss)
        public bool Allied;          // a pact with the family: they leave you alone and go after your enemies

        public const int AlliancePrice = 2000;

        /// <summary>A pact: money on the table, respect behind it. They have to not hate you already.</summary>
        public float AllianceChance(Game game) => Mathf.Clamp01(0.2f + game.Org.Respect / 120f - Hostility / 60f);

        public bool ProposeAlliance(Game game)
        {
            if (Allied || Destroyed) return false;
            if (!game.Org.Pay(AlliancePrice, game, out var err)) { game.Log.Add(err, MessageKind.Bad); return false; }
            if (game.Rng.NextDouble() < AllianceChance(game))
            {
                Allied = true;
                Hostility = Mathf.Min(Hostility, 5f);
                game.Log.Add($"{BossName} shakes your hand. The {Name} are with the family now: stay off their turf and they'll go after your enemies.", MessageKind.Good);
                game.Newspaper.Report(game, 3, "New understandings in the old town", "Investigators note an unusual calm between two groups that were at each other's throats a month ago.");
            }
            else
            {
                Hostility = Mathf.Min(100f, Hostility + 10f);
                game.Log.Add($"{BossName} takes the money and laughs. No deal.", MessageKind.Bad);
            }
            return Allied;
        }

        public void BreakAlliance(Game game, string why)
        {
            if (!Allied) return;
            Allied = false;
            Hostility = Mathf.Min(100f, Hostility + 50f);
            game.Log.Add($"The pact with the {Name} is broken: {why}. They won't forget it.", MessageKind.Bad);
        }
        public string Name;
        public string BossName;
        public float Hostility = 10f;
        public int Soldiers = StartingSoldiers;
        public int Cash = 2000;
        public int TruceUntilDay;
        public bool Warned;
        public bool Destroyed;
        public bool StesaInProgress;    // set by the first shooter to arrive, so the effects apply once
        public readonly List<DrugSquare> Squares = new List<DrugSquare>();

        int lastAbsoluteHour = -1;
        bool atWar;

        public bool InTruce(Game game) => game.Clock.Day < TruceUntilDay;

        public bool OperationActive(Game game)
        {
            foreach (var p in Members(game))
                if (p.IsPresent && (p.Mission == RivalMission.Stesa || p.Mission == RivalMission.Hit || p.Mission == RivalMission.Assault)) return true;
            return false;
        }

        // ---------- Setup ----------

        /// <summary>Takes a name nobody else in the city has, and claims the given rioni with a piazza in each.</summary>
        public void Setup(Game game, System.Random rng, IEnumerable<Zone> claim)
        {
            string surname;
            do surname = Names.Pick(Names.RivalSurnames, rng);
            while (game.Clans.Exists(c => c != this && c.Name == $"Clan {surname}"));
            Name = $"Clan {surname}";
            BossName = $"{Names.Pick(Names.FirstNames, rng)} {surname}";
            foreach (var zone in claim) ClaimZone(zone, game);
        }

        /// <summary>Opens a piazza in a rione, with a pusher and a lookout on it.</summary>
        public bool ClaimZone(Zone zone, Game game)
        {
            var cell = OutdoorCellNear(Vector2Int.RoundToInt(zone.Rect.center), game.Map, zone, game.Territory);
            if (cell == null) return false;
            var square = new DrugSquare { Cell = cell.Value, ZoneId = zone.Id, Rival = true, OpenHour = 0, CloseHour = 0, Clan = Id };
            Squares.Add(square);
            SpawnPusher(square, game);
            SpawnLookout(square, game);
            return true;
        }

        /// <summary>This clan's people on the map.</summary>
        public IEnumerable<Pawn> Members(Game game) => game.Pawns.Rivals.Where(p => p.Clan == Id);

        static Vector2Int? OutdoorCellNear(Vector2Int center, Map map, Zone zone, Territory territory)
        {
            foreach (var c in GridUtil.CellsAround(center, 12))
            {
                if (!map.InBounds(c) || territory.ZoneAt(c) != zone) continue;
                if (map.GetBuilding(c) != null || map.GetTerrain(c.x, c.y) == TerrainType.Floor) continue;
                return c;
            }
            return null;
        }

        public DrugSquare SquareInZone(int zoneId) => Squares.Find(s => s.ZoneId == zoneId);

        public bool OwnsZone(int zoneId) => Squares.Exists(s => s.ZoneId == zoneId);

        // ---------- Soldiers ----------

        Pawn SpawnSoldier(Vector2Int at, RivalMission mission, bool armed, Game game)
        {
            var rng = game.Rng;
            byte g = (byte)rng.Next(70, 110);
            var pawn = new Pawn(game.Pawns.NextId(), Names.Pick(Names.FirstNames, rng), Name.Replace("Clan ", ""),
                Names.Pick(Names.Nicknames, rng), new Color32((byte)(g - 30), g, (byte)(g - 40), 255), at)
            {
                Faction = Faction.Rival,
                Clan = Id,
                Armed = armed,
                ShootingSkill = rng.Next(3, 9),
                IntimidationSkill = rng.Next(3, 9),
                Mission = mission,
                // Gunmen sent to do a job wear a vest; corner boys a plate at most.
                Armor = !armed ? 0f : mission == RivalMission.Hit || mission == RivalMission.Assault || mission == RivalMission.Stesa ? 100f : 50f,
                AggressiveUntil = mission == RivalMission.Stesa || mission == RivalMission.Hit ? long.MaxValue : 0,
            };
            game.Pawns.Add(pawn);
            return pawn;
        }

        void SpawnPusher(DrugSquare square, Game game)
        {
            var pawn = SpawnSoldier(square.Cell, RivalMission.Pusher, game.Rng.NextDouble() < 0.5, game);
            pawn.MissionSquare = square;
        }

        void SpawnLookout(DrugSquare square, Game game)
        {
            var pawn = SpawnSoldier(LookoutPostFor(square, game), RivalMission.Lookout, false, game);
            pawn.MissionSquare = square;
        }

        /// <summary>A spot up the street from a rival piazza where their vedetta stands.</summary>
        public Vector2Int LookoutPostFor(DrugSquare square, Game game)
        {
            var map = game.Map;
            int index = Squares.IndexOf(square);
            var dir = new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down }[Mathf.Abs(index) % 4];
            foreach (var c in GridUtil.CellsAround(square.Cell + dir * 6, 3))
                if (map.InBounds(c) && map.IsPassable(c) && map.GetBuilding(c) == null && map.GetTerrain(c.x, c.y) != TerrainType.Floor)
                    return c;
            return square.Cell;
        }

        void SpawnPatrol(Game game)
        {
            if (Squares.Count == 0) return;
            var start = Squares[game.Rng.Next(Squares.Count)].Cell;
            for (int i = 0; i < 2; i++) SpawnSoldier(start, RivalMission.Patrol, true, game);
        }

        public Pawn PusherAt(DrugSquare square, Game game)
        {
            foreach (var p in Members(game))
                if (p.CanAct && p.Mission == RivalMission.Pusher && p.MissionSquare == square && p.CurrentJob is RivalPostJob post && post.AtPost)
                    return p;
            return null;
        }

        public void OnSoldierDied(Pawn pawn, Game game)
        {
            Intel = Mathf.Min(100f, Intel + 3f);   // a body talks: phones, papers, who came to the funeral
            if (pawn == MarkedTarget)
            {
                OnTargetKilled(pawn, game);
                MarkedTarget = null;
            }
            Soldiers = Mathf.Max(0, Soldiers - 1);
            game.Territory.AddHeat(pawn.Cell, 15f);
            game.Org.AddEvidence(6f, game);
            game.Log.Note($"A soldier of the {Name} lies dead in the street. {Soldiers} of theirs left.", MessageKind.Info);
            if (Soldiers == 0) Collapse(game);
        }

        public void Collapse(Game game)
        {
            if (Destroyed) return;
            Destroyed = true;
            Squares.Clear();
            Hostility = 0f;
            game.Org.Respect = Mathf.Min(100f, game.Org.Respect + 20f);
            foreach (var p in Members(game))
                if (p.IsPresent) p.Mission = RivalMission.Leave;
            game.Log.Add($"The {Name} is finished. Their rioni are open for whoever takes them.", MessageKind.Good);
            game.Newspaper.Report(game, 6, $"The end of the {Name}", "Investigators speak of a clan wiped out by its rivals. Nobody in the rione will say a word.");
        }

        // ---------- Under fire ----------

        long lastReinforcementTick = -100000;
        readonly Dictionary<DrugSquare, int> emptyFor = new Dictionary<DrugSquare, int>();
        public const int HoldToTake = 1800;

        /// <summary>The rival piazza the family is holding right now, and how far along (0..1); null if none.</summary>
        public DrugSquare Holding(out float progress)
        {
            progress = 0f;
            foreach (var kv in emptyFor)
                if (Squares.Contains(kv.Key)) { progress = Mathf.Clamp01(kv.Value / (float)HoldToTake); return kv.Key; }
            return null;
        }

        /// <summary>
        /// One of theirs was attacked. The armed ones take cover and shoot back at whoever did it,
        /// the unarmed run, everyone nearby joins in, and the clan sends shooters on scooters.
        /// </summary>
        public void OnRivalAttacked(Pawn victim, Pawn attacker, Game game)
        {
            if (Destroyed || attacker == null) return;
            long now = game.Ticks.TicksGame;
            foreach (var p in Members(game))
            {
                if (!p.CanAct || (p.Position - victim.Position).sqrMagnitude > 12f * 12f) continue;
                if (p.Mission == RivalMission.Hit || p.Mission == RivalMission.Stesa || p.Mission == RivalMission.Leave) continue;
                if (p.Armed)
                {
                    p.Mission = RivalMission.Hit;
                    p.MissionTarget = attacker;
                    p.AggressiveUntil = long.MaxValue;
                    p.ForceJob(new HitJob(p), game);
                    if (game.Rng.NextDouble() < 0.5) p.Say("fight");
                }
                else
                {
                    p.Mission = RivalMission.Leave;
                    p.ForceJob(new LeaveMapJob(p, game), game);
                }
            }

            // Reinforcements: at most once every couple of hours, while they have men to send.
            if (now - lastReinforcementTick > GameClock.TicksPerHour * 2 && Soldiers >= 3)
            {
                lastReinforcementTick = now;
                var entrance = NearestEntrance(victim.Cell, game);
                int count = Soldiers >= 6 ? 3 : 2;
                for (int i = 0; i < count; i++)
                {
                    var s = SpawnSoldier(entrance, RivalMission.Hit, true, game);
                    s.MissionTarget = attacker;
                }
                game.Log.Add($"Scooters are coming fast from the {Name}'s side. They're sending people.", MessageKind.Bad, entrance);
            }
        }

        /// <summary>
        /// A rival piazza with nobody of theirs left on it, and your people standing on it, becomes yours
        /// after a minute or two. Killing everyone isn't enough: you have to hold the corner.
        /// </summary>
        void CheckCaptures(Game game)
        {
            for (int i = Squares.Count - 1; i >= 0; i--)
            {
                var sq = Squares[i];
                int defenders = 0, ours = 0;
                foreach (var p in Members(game))
                    if (p.CanAct && p.Mission != RivalMission.Leave && (p.Position - Mover.CellCenter(sq.Cell)).sqrMagnitude < 9f * 9f) defenders++;
                foreach (var p in game.Pawns.Crew)
                    if (p.CanAct && (p.Position - Mover.CellCenter(sq.Cell)).sqrMagnitude < 5f * 5f) ours++;
                if (defenders > 0 || ours == 0)
                {
                    emptyFor.Remove(sq);
                    continue;
                }
                emptyFor.TryGetValue(sq, out int held);
                held += 120;
                emptyFor[sq] = held;
                if (held == 120)
                    game.Log.Add("Nobody of theirs is left on the corner. Hold it.", MessageKind.Info, sq.Cell);
                if (held < HoldToTake) continue;

                emptyFor.Remove(sq);
                Squares.RemoveAt(i);
                Hostility = Mathf.Min(100f, Hostility + 20f);
                game.Org.Respect = Mathf.Min(100f, game.Org.Respect + 8f);
                var zone = game.Territory.ZoneAt(sq.Cell);
                if (game.Org.TakeSquare(sq, game))
                    game.Log.Add($"The piazza in {zone?.Name ?? "the rione"} is yours now. Put a pusher and a lookout on it.", MessageKind.Good, sq.Cell);
                else
                    game.Log.Add($"You chased the {Name} out of {zone?.Name ?? "the rione"}, but you can't run another piazza yet: the corner stays empty.", MessageKind.Info, sq.Cell);
                game.Newspaper.Report(game, 4, $"Shooting and a change of hands in {zone?.Name ?? "the old town"}",
                    "Residents speak of gunfire in the afternoon. By evening, different young men were standing on the corner.");
                if (Squares.Count == 0 && Soldiers <= 2) Collapse(game);
            }
        }

        public void OnCrewAttacked(Game game)
        {
            if (Destroyed) return;
            BreakAlliance(game, "your people opened fire on theirs");
            Hostility = Mathf.Min(100f, Hostility + 6f);
            TruceUntilDay = 0;
            if (!atWar)
            {
                atWar = true;
                game.Log.Add($"Your crew opened fire on the {Name}. This means war.", MessageKind.Bad);
            }
        }

        // ---------- Time ----------

        public void Tick(Game game)
        {
            if (Destroyed) return;
            if (game.Ticks.TicksGame % 120 == 0)
            {
                CheckCaptures(game);
                CheckAssaults(game);
            }
            int absoluteHour = game.Clock.Day * 24 + game.Clock.Hour;
            if (lastAbsoluteHour < 0) lastAbsoluteHour = absoluteHour;
            else if (absoluteHour != lastAbsoluteHour)
            {
                lastAbsoluteHour = absoluteHour;
                OnHour(game);
            }
        }

        void OnHour(Game game)
        {
            if (game.Clock.Hour == 0) OnNewDay(game);

            if (Id == 0 && !Warned && Hostility >= 30f)
            {
                Warned = true;
                game.Events.Raise(EventCards.RivalWarning(game));
            }

            bool patrolling = Members(game).Any(p => p.IsPresent && p.Mission == RivalMission.Patrol);
            if (!patrolling && Soldiers >= 3 && game.Rng.NextDouble() < 0.25) SpawnPatrol(game);

            // The other clans: a feud with whoever sits next door.
            if (!OperationActive(game) && Soldiers >= 4 && game.Rng.NextDouble() < 0.015)
            {
                var target = NeighbourSquare(game);
                if (target.square != null) StartAssault(target.square, game);
            }

            if (OperationActive(game) || !CanStrikeFamily(game)) return;

            // Your piazze: when they're angry enough, they come to take one.
            if (Hostility >= 55f && Soldiers >= 4 && game.Rng.NextDouble() < 0.04)
            {
                var target = PickTargetSquare(game);
                if (target != null)
                {
                    StartAssault(target, game);
                    StruckFamily(game);
                    return;
                }
            }

            double roll = game.Rng.NextDouble();
            if (Hostility >= HitHostility && roll < 0.05) { if (StartHit(game)) StruckFamily(game); }
            else if (Hostility >= StesaHostility && roll < 0.05) { if (StartStesa(game)) StruckFamily(game); }
        }

        // Blows against the family come one at a time across the city, with a breather in between,
        // and a new boss gets to bury the old one first. Four clans striking at will wiped a family in days.
        const long StruckCooldown = GameClock.TicksPerDay * 3 / 2;
        const long CityCooldown = GameClock.TicksPerDay / 2;
        const long SuccessionGrace = GameClock.TicksPerDay * 2;
        long nextFamilyStrikeTick;
        int generationSeen = -1;

        bool CanStrikeFamily(Game game)
        {
            if (Allied) return false;   // a pact holds
            if (InTruce(game) || game.Org.Squares.Count == 0) return false;
            long now = game.Ticks.TicksGame;
            if (generationSeen != game.Org.Generation)
            {
                if (generationSeen >= 0) nextFamilyStrikeTick = System.Math.Max(nextFamilyStrikeTick, now + SuccessionGrace);
                generationSeen = game.Org.Generation;
            }
            if (now < nextFamilyStrikeTick) return false;
            // Someone (any clan) is already out for them: let that play out first.
            foreach (var p in game.Pawns.Rivals)
                if (p.IsPresent && Combat.CombatSystem.AfterFamily(p)) return false;
            return true;
        }

        void StruckFamily(Game game)
        {
            long now = game.Ticks.TicksGame;
            foreach (var c in game.Clans)
                c.nextFamilyStrikeTick = System.Math.Max(c.nextFamilyStrikeTick, now + (c == this ? StruckCooldown : CityCooldown));
        }

        void OnNewDay(Game game)
        {
            atWar = Hostility >= HitHostility;
            if (Id == 0 && MessageLog.Trace != null)
            {
                MessageLog.Trace("[day] crew " + game.Pawns.Crew.Count(p => p.IsPresent) + " present / " + game.Pawns.Crew.Count(p => !p.Dead && !p.Departed) + " alive, piazze " + game.Org.Squares.Count + ", rivals on map " + game.Pawns.Rivals.Count(p => p.IsPresent)
                    + " | " + string.Join(" · ", game.Clans.Select(c => $"{c.Id}: host {c.Hostility:0} sold {c.Soldiers} sq {c.Squares.Count}{(c.Destroyed ? " DEAD" : "")}")));
            }

            // Your piazze near their territory are an insult that grows every day.
            float drift = -3f;
            foreach (var sq in game.Org.Squares)
            {
                if (OwnsZone(sq.ZoneId)) drift += 12f;
                else if (Squares.Exists(r => (r.Cell - sq.Cell).magnitude < NearDistance)) drift += 4f;
            }
            Hostility = Mathf.Clamp(Hostility + drift, 0f, 100f);
            if (Allied) Hostility = Mathf.Min(Hostility, 10f);
            if (InTruce(game)) Hostility = Mathf.Min(Hostility, 20f);

            // Replace pushers who were killed or ran off, while they still have men.
            foreach (var sq in Squares)
            {
                bool staffed = Members(game).Any(p => p.IsPresent && p.Mission == RivalMission.Pusher && p.MissionSquare == sq);
                if (!staffed && Soldiers > 1) SpawnPusher(sq, game);
                bool watched = Members(game).Any(p => p.IsPresent && p.Mission == RivalMission.Lookout && p.MissionSquare == sq);
                if (!watched && Soldiers > 1) SpawnLookout(sq, game);
            }

            GatherIntel(game);

            // Money comes in from their piazze; men are hired when there's money.
            Cash += 100 + 150 * Squares.Count;
            if (Cash >= 600 && Soldiers < 14 && game.Rng.NextDouble() < 0.5)
            {
                Cash -= 400;
                Soldiers++;
            }

            // A strong clan grows into an empty rione next to its own.
            if (Soldiers >= 6 && Squares.Count < 4 && game.Rng.NextDouble() < 0.3)
            {
                var zone = EmptyZoneNextDoor(game);
                if (zone != null && ClaimZone(zone, game))
                    game.Log.Note($"Word in the street: the {Name} have opened a piazza in {zone.Name}.", MessageKind.Info);
                    game.Newspaper.Report(game, 1, "New dealing spot, say residents", $"In {zone.Name}, neighbours complain of a new piazza di spaccio run, they say, by the {Name}.");
            }
        }

        // ---------- What the family does to them ----------

        /// <summary>The man the family is hunting: the boss or one of his lieutenants, out in the open for once.</summary>
        public Pawn MarkedTarget;
        public bool MarkedIsBoss;

        public const float IntelForLieutenant = 30f, IntelForBoss = 70f;

        /// <summary>
        /// With enough information, you know where to find him: at the bar near their piazza, with a
        /// bodyguard or two. He's put on the map, and the hitman goes after him.
        /// </summary>
        public Pawn ExposeTarget(bool boss, Game game)
        {
            if (Squares.Count == 0) return null;
            if (MarkedTarget != null && MarkedTarget.IsPresent) return MarkedTarget;
            var sq = Squares[game.Rng.Next(Squares.Count)];
            var spot = OutdoorCellNear(sq.Cell + new Vector2Int(game.Rng.Next(-5, 6), game.Rng.Next(-5, 6)), game.Map, game.Territory.Zones[sq.ZoneId], game.Territory) ?? sq.Cell;
            var target = SpawnSoldier(spot, RivalMission.Patrol, true, game);
            if (boss)
            {
                var parts = BossName.Split(' ');
                target.FirstName = parts[0];
                target.Nickname = "the boss";
            }
            target.ShootingSkill = Mathf.Max(target.ShootingSkill, boss ? 7 : 5);
            for (int i = 0; i < (boss ? 2 : 1); i++)
            {
                var guard = SpawnSoldier(spot, RivalMission.Guard, true, game);
                guard.MissionTarget = target;
            }
            MarkedTarget = target;
            MarkedIsBoss = boss;
            return target;
        }

        void OnTargetKilled(Pawn target, Game game)
        {
            var zone = game.Territory.ZoneAt(target.Cell);
            game.Org.AddEvidence(8f, game);
            if (MarkedIsBoss)
            {
                Soldiers = Mathf.Max(0, Soldiers - 3);
                Hostility = 100f;
                Intel = Mathf.Max(0f, Intel - 40f);   // a new boss, new habits
                game.Org.Respect = Mathf.Min(100f, game.Org.Respect + 12f);
                game.Log.Add($"{BossName} is dead. The {Name} are without a head.", MessageKind.Good, target.Cell);
                game.Newspaper.Report(game, 9, $"Clan boss shot dead in {zone?.Name ?? "the old town"}", $"{BossName}, the head of the {Name}, was killed in the street in broad daylight. Investigators fear a war.");
                if (Soldiers <= 3) Collapse(game);
                else
                {
                    BossName = $"{Story.Names.Pick(Story.Names.FirstNames, game.Rng)} {Name.Replace("Clan ", "")}";
                    game.Log.Add($"{BossName} takes over what's left of the {Name}. They swear revenge.", MessageKind.Bad);
                }
            }
            else
            {
                Soldiers = Mathf.Max(0, Soldiers - 1);
                Hostility = Mathf.Min(100f, Hostility + 30f);
                game.Org.Respect = Mathf.Min(100f, game.Org.Respect + 5f);
                game.Log.Add($"One of the {Name}'s lieutenants is dead.", MessageKind.Good, target.Cell);
                game.Newspaper.Report(game, 5, $"Man shot dead in {zone?.Name ?? "the old town"}", "Known to the police. The neighbours heard nothing.");
            }
        }

        /// <summary>Your stesa reached their piazza: their people scatter, customers run, word gets round.</summary>
        public void OnStesaAgainst(DrugSquare square, Game game)
        {
            Hostility = Mathf.Min(100f, Hostility + 15f);
            game.Org.Respect = Mathf.Min(100f, game.Org.Respect + 3f);
            game.Territory.AddHeat(square.Cell, 10f);
            var zone = game.Territory.Zones[square.ZoneId];
            game.Log.Add($"Stesa over the {Name}'s piazza in {zone.Name}. They'll have heard the message.", MessageKind.Good, square.Cell);
            foreach (var p in Members(game))
                if (p.CanAct && !p.Armed && p.MissionSquare == square) p.Mission = RivalMission.Leave;
            foreach (var npc in game.Npcs.All)
                if (npc.Kind == Npcs.NpcKind.Customer && npc.Square == square) npc.Despawned = true;
        }

        /// <summary>Each day: your lookouts near their turf pick up names, faces and habits.</summary>
        void GatherIntel(Game game)
        {
            foreach (var c in game.Pawns.Crew)
            {
                if (!c.IsPresent || c.Role != CrewRole.Lookout) continue;
                foreach (var sq in game.Org.Squares)
                    if (Squares.Exists(r => (r.Cell - sq.Cell).magnitude < 45f)) { Intel = Mathf.Min(100f, Intel + 1.5f + c.AwarenessSkill * 0.2f); break; }
            }
        }

        // ---------- Expansion and war ----------

        Zone EmptyZoneNextDoor(Game game)
        {
            foreach (var mine in Squares.ToList())
            {
                var home = game.Territory.Zones[mine.ZoneId];
                foreach (var z in game.Territory.Zones)
                {
                    if (z == home || game.ClanOwning(z.Id) != null || game.Org.ControlsZone(z, game)) continue;
                    var near = new RectInt(home.Rect.x - 4, home.Rect.y - 4, home.Rect.width + 8, home.Rect.height + 8);
                    if (near.Overlaps(z.Rect)) return z;
                }
            }
            return null;
        }

        /// <summary>The nearest piazza of another clan, close to one of ours.</summary>
        (DrugSquare square, RivalClan owner) NeighbourSquare(Game game)
        {
            DrugSquare best = null;
            RivalClan owner = null;
            float bestD = 45f;
            foreach (var other in game.Clans)
            {
                if (other == this || other.Destroyed) continue;
                // An ally fights the family's enemies, and leaves the family's other friends alone.
                if (Allied && (other.Allied || other.Hostility < 40f)) continue;
                foreach (var theirs in other.Squares)
                foreach (var mine in Squares)
                {
                    float d = (theirs.Cell - mine.Cell).magnitude;
                    if (d < bestD) { bestD = d; best = theirs; owner = other; }
                }
            }
            return (best, owner);
        }

        /// <summary>A squad sent to take a piazza: yours, or another clan's.</summary>
        void StartAssault(DrugSquare target, Game game)
        {
            var from = Squares.Count > 0 ? Squares.OrderBy(s => (s.Cell - target.Cell).sqrMagnitude).First().Cell : NearestEntrance(target.Cell, game);
            int count = Soldiers >= 8 ? 4 : 3;
            for (int i = 0; i < count; i++)
            {
                var p = SpawnSoldier(from, RivalMission.Assault, true, game);
                p.MissionSquare = target;
                p.AggressiveUntil = long.MaxValue;
            }
            var zone = game.Territory.Zones[target.ZoneId];
            bool ours = !target.Rival;
            if (!ours && game.Rng.NextDouble() < 0.5)
                game.Log.Note($"The {Name} are moving on the {game.Clans[target.Clan].Name}'s piazza in {zone.Name}.", MessageKind.Info, target.Cell);
        }

        // Assaults on someone else's corner: how long nobody has been there to stop them.
        readonly Dictionary<DrugSquare, int> attackHeld = new Dictionary<DrugSquare, int>();

        /// <summary>Their squad holds a piazza nobody defends for a minute or two: it changes hands.</summary>
        void CheckAssaults(Game game)
        {
            var targets = new List<DrugSquare>(game.Org.Squares);
            foreach (var other in game.Clans) if (other != this && !other.Destroyed) targets.AddRange(other.Squares);
            foreach (var sq in targets)
            {
                var at = Mover.CellCenter(sq.Cell);
                int attackers = Members(game).Count(p => p.CanAct && p.Mission == RivalMission.Assault && p.MissionSquare == sq && (p.Position - at).sqrMagnitude < 5f * 5f);
                if (attackers == 0) { attackHeld.Remove(sq); continue; }
                int defenders = sq.Rival
                    ? game.Clans[sq.Clan].Members(game).Count(p => p.CanAct && p.Mission != RivalMission.Leave && (p.Position - at).sqrMagnitude < 9f * 9f)
                    : game.Pawns.Crew.Count(p => p.CanAct && (p.Position - at).sqrMagnitude < 9f * 9f);
                if (defenders > 0) { attackHeld.Remove(sq); continue; }
                attackHeld.TryGetValue(sq, out int held);
                held += 120;
                attackHeld[sq] = held;
                if (held < 1800) continue;
                attackHeld.Remove(sq);

                var zone = game.Territory.Zones[sq.ZoneId];
                if (!sq.Rival)
                {
                    game.Org.CloseSquareAt(sq.Cell);
                    game.Org.Respect = Mathf.Max(0f, game.Org.Respect - 8f);
                    game.Log.Add($"The {Name} have taken your piazza in {zone.Name}. Nobody was there to stop them.", MessageKind.Bad, sq.Cell);
                    game.Newspaper.Report(game, 4, $"A drug market changes hands in {zone.Name}", "Residents say new faces are selling on the corner since yesterday.");
                }
                else
                {
                    var loser = game.Clans[sq.Clan];
                    loser.Squares.Remove(sq);
                    loser.Soldiers = Mathf.Max(0, loser.Soldiers - 1);
                    if (loser.Squares.Count == 0 && loser.Soldiers <= 3) loser.Collapse(game);
                    game.Log.Note($"The {Name} took the {loser.Name}'s piazza in {zone.Name}.", MessageKind.Info, sq.Cell);
                    game.Newspaper.Report(game, 2, "Turf war in the old town", $"Residents of {zone.Name} describe new faces on the corner: the {Name} have pushed out the {loser.Name}.");
                }
                var mine = new DrugSquare { Cell = sq.Cell, ZoneId = sq.ZoneId, Rival = true, OpenHour = 0, CloseHour = 0, Clan = Id };
                Squares.Add(mine);
                foreach (var p in Members(game))
                    if (p.Mission == RivalMission.Assault && p.MissionSquare == sq)
                    {
                        p.Mission = RivalMission.Leave;
                        p.AggressiveUntil = game.Ticks.TicksGame + 600;
                    }
                SpawnPusher(mine, game);
                SpawnLookout(mine, game);
            }
        }

        public bool NearRivalTerritory(DrugSquare square) =>
            Squares.Exists(r => (r.Cell - square.Cell).magnitude < NearDistance);

        // ---------- Operations ----------

        // Debug shortcuts for testing fights without waiting for hostility to build.
        public void DebugHit(Game game) => StartHit(game);
        public void DebugStesa(Game game) => StartStesa(game);

        bool StartStesa(Game game)
        {
            var target = PickTargetSquare(game);
            if (target == null) return false;
            var entrance = NearestEntrance(target.Cell, game);
            StesaInProgress = false;
            for (int i = 0; i < 3; i++)
            {
                var p = SpawnSoldier(entrance, RivalMission.Stesa, true, game);
                p.MissionSquare = target;
            }
            return true;
        }

        bool StartHit(Game game)
        {
            // Someone exposed: on a post, collecting, or anywhere outside the basso.
            Pawn target = null;
            foreach (var p in game.Pawns.Crew)
            {
                if (!p.CanAct || game.PlayerBase.Contains(p.Cell)) continue;
                if (target == null || p.CurrentJob is PostJob || p.IsBoss) target = p;
            }
            if (target == null) return false;

            var entrance = NearestEntrance(target.Cell, game);
            int count = Hostility > 85f ? 4 : 3;
            for (int i = 0; i < count; i++)
            {
                var p = SpawnSoldier(entrance, RivalMission.Hit, true, game);
                p.MissionTarget = target;
            }
            game.Log.Add($"Killers of the {Name} have been seen entering the rione. They're after {target.DisplayName}.", MessageKind.Bad, target.Cell);
            return true;
        }

        DrugSquare PickTargetSquare(Game game)
        {
            DrugSquare best = null;
            float bestDist = float.MaxValue;
            int hour = game.Clock.Hour;
            foreach (var sq in game.Org.Squares)
            {
                if (!sq.IsOpenAt(hour)) continue;
                foreach (var r in Squares)
                {
                    float d = (r.Cell - sq.Cell).magnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = sq;
                    }
                }
            }
            return best ?? (game.Org.Squares.Count > 0 ? game.Org.Squares[0] : null);
        }

        static Vector2Int NearestEntrance(Vector2Int to, Game game)
        {
            var best = game.Territory.Entrances[0];
            foreach (var e in game.Territory.Entrances)
                if ((e - to).sqrMagnitude < (best - to).sqrMagnitude) best = e;
            return best;
        }

        /// <summary>Called once when a stesa reaches its target: shots in the air, panic.</summary>
        public void OnStesa(DrugSquare square, Game game)
        {
            long now = game.Ticks.TicksGame;
            game.Org.Respect = Mathf.Max(0f, game.Org.Respect - 4f);
            game.Territory.AddHeat(square.Cell, 12f);
            var zone = game.Territory.Zones[square.ZoneId];
            game.Log.Add($"Stesa! The {Name} fired shots over the piazza in {zone.Name}. A warning.", MessageKind.Bad, square.Cell);

            foreach (var p in game.Pawns.Crew)
            {
                if (!p.CanAct) continue;
                p.Mood.AddMemory("stesa", "Shots fired over our piazza", -6f, now, GameClock.TicksPerDay * 2);
                if (!p.Drafted && !p.Armed && (p.Position - Mover.CellCenter(square.Cell)).magnitude < 14f)
                    p.ForceJob(new FleeJob(p, square, game), game);
            }
            foreach (var npc in game.Npcs.All)
                if (npc.Kind == Npcs.NpcKind.Customer) npc.Despawned = true; // everyone scatters
        }

        // ---------- Diplomacy ----------

        public bool SendGift(int amount, float hostilityDrop, Game game)
        {
            if (game.Stock.Cash < amount) return false;
            game.Stock.Cash -= amount;
            Hostility = Mathf.Max(0f, Hostility - hostilityDrop);
            game.Log.Add($"A gift of €{amount} was sent to {BossName}. Tempers cool.", MessageKind.Good);
            return true;
        }

        public void StartTruce(int days, Game game)
        {
            TruceUntilDay = game.Clock.Day + days;
            Hostility = Mathf.Min(Hostility, 10f);
            atWar = false;
        }
    }
}
