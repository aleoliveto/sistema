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
            foreach (var p in game.Pawns.Rivals)
                if (p.IsPresent && (p.Mission == RivalMission.Stesa || p.Mission == RivalMission.Hit)) return true;
            return false;
        }

        // ---------- Setup ----------

        public void Setup(Game game, System.Random rng)
        {
            string surname = Names.Pick(Names.RivalSurnames, rng);
            Name = $"Clan {surname}";
            BossName = $"{Names.Pick(Names.FirstNames, rng)} {surname}";

            // Their territory: the three blocks farthest from the player's basso.
            var home = game.PlayerBase.center;
            var zones = new List<Zone>(game.Territory.Zones);
            zones.Sort((a, b) => (b.Rect.center - home).sqrMagnitude.CompareTo((a.Rect.center - home).sqrMagnitude));
            foreach (var zone in zones)
            {
                if (Squares.Count >= 3) break;
                var cell = OutdoorCellNear(Vector2Int.RoundToInt(zone.Rect.center), game.Map, zone, game.Territory);
                if (cell == null) continue;
                var square = new DrugSquare { Cell = cell.Value, ZoneId = zone.Id, Rival = true, OpenHour = 0, CloseHour = 0 };
                Squares.Add(square);
                SpawnPusher(square, game);
            }
        }

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
                Armed = armed,
                ShootingSkill = rng.Next(3, 9),
                IntimidationSkill = rng.Next(3, 9),
                Mission = mission,
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

        public Pawn PusherAt(DrugSquare square, Game game)
        {
            foreach (var p in game.Pawns.Rivals)
                if (p.CanAct && p.Mission == RivalMission.Pusher && p.MissionSquare == square && p.CurrentJob is RivalPostJob post && post.AtPost)
                    return p;
            return null;
        }

        public void OnSoldierDied(Pawn pawn, Game game)
        {
            Soldiers = Mathf.Max(0, Soldiers - 1);
            game.Territory.AddHeat(pawn.Cell, 15f);
            game.Org.AddEvidence(6f, game);
            game.Log.Add($"A soldier of the {Name} lies dead in the street. {Soldiers} of theirs left.", MessageKind.Info);
            if (Soldiers == 0) Collapse(game);
        }

        void Collapse(Game game)
        {
            if (Destroyed) return;
            Destroyed = true;
            Squares.Clear();
            Hostility = 0f;
            game.Org.Respect = Mathf.Min(100f, game.Org.Respect + 20f);
            foreach (var p in game.Pawns.Rivals)
                if (p.IsPresent) p.Mission = RivalMission.Leave;
            game.Log.Add($"The {Name} is finished. Their piazze are yours for the taking.", MessageKind.Good);
        }

        public void OnCrewAttacked(Game game)
        {
            if (Destroyed) return;
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

            if (!Warned && Hostility >= 30f)
            {
                Warned = true;
                game.Events.Raise(EventCards.RivalWarning(game));
            }

            if (InTruce(game) || OperationActive(game) || game.Org.Squares.Count == 0) return;
            double roll = game.Rng.NextDouble();
            if (Hostility >= HitHostility && roll < 0.05) StartHit(game);
            else if (Hostility >= StesaHostility && roll < 0.05) StartStesa(game);
        }

        void OnNewDay(Game game)
        {
            atWar = Hostility >= HitHostility;

            // Your piazze near their territory are an insult that grows every day.
            float drift = -3f;
            foreach (var sq in game.Org.Squares)
            {
                if (OwnsZone(sq.ZoneId)) drift += 12f;
                else if (Squares.Exists(r => (r.Cell - sq.Cell).magnitude < NearDistance)) drift += 4f;
            }
            Hostility = Mathf.Clamp(Hostility + drift, 0f, 100f);
            if (InTruce(game)) Hostility = Mathf.Min(Hostility, 20f);

            // Replace pushers who were killed or ran off, while they still have men.
            foreach (var sq in Squares)
            {
                bool staffed = game.Pawns.Rivals.Any(p => p.IsPresent && p.Mission == RivalMission.Pusher && p.MissionSquare == sq);
                if (!staffed && Soldiers > 1) SpawnPusher(sq, game);
            }

            Cash += 300;
        }

        public bool NearRivalTerritory(DrugSquare square) =>
            Squares.Exists(r => (r.Cell - square.Cell).magnitude < NearDistance);

        // ---------- Operations ----------

        void StartStesa(Game game)
        {
            var target = PickTargetSquare(game);
            if (target == null) return;
            var entrance = NearestEntrance(target.Cell, game);
            StesaInProgress = false;
            for (int i = 0; i < 3; i++)
            {
                var p = SpawnSoldier(entrance, RivalMission.Stesa, true, game);
                p.MissionSquare = target;
            }
        }

        void StartHit(Game game)
        {
            // Someone exposed: on a post, collecting, or anywhere outside the basso.
            Pawn target = null;
            foreach (var p in game.Pawns.Crew)
            {
                if (!p.CanAct || game.PlayerBase.Contains(p.Cell)) continue;
                if (target == null || p.CurrentJob is PostJob || p.IsBoss) target = p;
            }
            if (target == null) return;

            var entrance = NearestEntrance(target.Cell, game);
            int count = Hostility > 85f ? 4 : 3;
            for (int i = 0; i < count; i++)
            {
                var p = SpawnSoldier(entrance, RivalMission.Hit, true, game);
                p.MissionTarget = target;
            }
            game.Log.Add($"Killers of the {Name} have been seen entering the rione. They're after {target.DisplayName}.", MessageKind.Bad);
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
            game.Log.Add($"Stesa! The {Name} fired shots over the piazza in {zone.Name}. A warning.", MessageKind.Bad);

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
