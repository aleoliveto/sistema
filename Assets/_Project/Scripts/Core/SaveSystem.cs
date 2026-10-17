using System;
using System.Collections.Generic;
using System.IO;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Core
{
    /// <summary>
    /// Saves the whole simulation to JSON. Jobs are not saved: pawns simply re-think after loading.
    /// Bump <see cref="Version"/> whenever the format changes.
    /// </summary>
    public static class SaveSystem
    {
        public const int Version = 2;

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "save1.json");

        public static bool HasSave(string path) => File.Exists(path);

        public static void Save(Game game, string path)
        {
            var map = game.Map;
            var terrain = new byte[map.CellCount];
            var buildings = new byte[map.CellCount];
            for (int i = 0; i < map.CellCount; i++)
            {
                terrain[i] = (byte)map.GetTerrainAt(i);
                var b = map.GetBuildingAt(i);
                buildings[i] = b == null ? (byte)0 : (byte)(Array.IndexOf(BuildingDefs.All, b) + 1);
            }

            var data = new SaveData
            {
                version = Version,
                seed = game.Seed,
                ticks = game.Ticks.TicksGame,
                width = map.Width,
                height = map.Height,
                terrain = Convert.ToBase64String(terrain),
                buildings = Convert.ToBase64String(buildings),
                baseX = game.PlayerBase.x,
                baseY = game.PlayerBase.y,
                baseW = game.PlayerBase.width,
                baseH = game.PlayerBase.height,
                cash = game.Stock.Cash,
                groceries = game.Stock.Groceries,
                meals = game.Stock.Meals,
                medicine = game.Stock.Medicine,
                product = game.Stock.Product,
                respect = game.Org.Respect,
                pendingOrderUnits = game.Org.PendingOrderUnits,
                pendingOrderDay = game.Org.PendingOrderDay,
                lastPaydayWeek = game.Org.LastPaydayWeek,
            };

            foreach (var z in game.Territory.Zones)
                data.zones.Add(new ZoneData { name = z.Name, x = z.Rect.x, y = z.Rect.y, w = z.Rect.width, h = z.Rect.height, heat = z.Heat, demand = z.Demand });
            foreach (var sq in game.Org.Squares)
                data.squares.Add(new SquareData { x = sq.Cell.x, y = sq.Cell.y, zone = sq.ZoneId });
            foreach (var b in game.Org.Businesses)
                data.businesses.Add(new BusinessData
                {
                    x = b.Cell.x, y = b.Cell.y, name = b.Name, kind = (int)b.Kind,
                    // Someone sent to impose pizzo is a standing order; keep it.
                    status = (int)b.Status, pizzo = b.WeeklyPizzo, defiance = b.Defiance, lastWeek = b.LastCollectedWeek,
                });

            foreach (var bp in game.Construction.All)
                data.blueprints.Add(new BlueprintData { x = bp.Cell.x, y = bp.Cell.y, def = bp.Def.Id, deconstruct = bp.Deconstruct, work = bp.WorkLeft });

            foreach (var p in game.Pawns.All)
                data.pawns.Add(ToData(p));

            File.WriteAllText(path, JsonUtility.ToJson(data));
        }

        public static Game Load(string path)
        {
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (data == null || data.version != Version)
                throw new InvalidDataException($"Unsupported save version {data?.version}");

            var map = new Map(data.width, data.height);
            var terrain = Convert.FromBase64String(data.terrain);
            var buildings = Convert.FromBase64String(data.buildings);
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                int i = map.Index(x, y);
                map.SetTerrain(x, y, (TerrainType)terrain[i]);
                if (buildings[i] > 0) map.SetBuilding(x, y, BuildingDefs.All[buildings[i] - 1]);
            }

            var zones = new List<Zone>();
            foreach (var z in data.zones)
                zones.Add(new Zone { Name = z.name, Rect = new RectInt(z.x, z.y, z.w, z.h), Heat = z.heat, Demand = z.demand });

            var game = new Game(data.seed, map, new RectInt(data.baseX, data.baseY, data.baseW, data.baseH), data.ticks, zones);
            game.Stock.Cash = data.cash;
            game.Stock.Product = data.product;
            game.Org.Respect = data.respect;
            game.Org.PendingOrderUnits = data.pendingOrderUnits;
            game.Org.PendingOrderDay = data.pendingOrderDay;
            game.Org.LastPaydayWeek = data.lastPaydayWeek;
            foreach (var sq in data.squares)
                game.Org.RestoreSquare(new DrugSquare { Cell = new Vector2Int(sq.x, sq.y), ZoneId = sq.zone }, map);
            foreach (var b in data.businesses)
                game.Org.Businesses.Add(new Business
                {
                    Cell = new Vector2Int(b.x, b.y), Name = b.name, Kind = (ShopKind)b.kind, Status = (BusinessStatus)b.status,
                    WeeklyPizzo = b.pizzo, Defiance = b.defiance, LastCollectedWeek = b.lastWeek,
                });
            game.Stock.Groceries = data.groceries;
            game.Stock.Meals = data.meals;
            game.Stock.Medicine = data.medicine;

            foreach (var bp in data.blueprints)
            {
                var def = BuildingDefs.ById(bp.def);
                if (def != null) game.Construction.Restore(new Vector2Int(bp.x, bp.y), def, bp.deconstruct, bp.work);
            }

            foreach (var pd in data.pawns)
                game.Pawns.Add(FromData(pd));

            game.Log.Add("Game loaded.", MessageKind.Info);
            return game;
        }

        static PawnData ToData(Pawn p)
        {
            var d = new PawnData
            {
                id = p.Id,
                firstName = p.FirstName,
                lastName = p.LastName,
                nickname = p.Nickname,
                shirtR = p.ShirtColor.r,
                shirtG = p.ShirtColor.g,
                shirtB = p.ShirtColor.b,
                construction = p.ConstructionSkill,
                cooking = p.CookingSkill,
                medicine = p.MedicineSkill,
                intimidation = p.IntimidationSkill,
                awareness = p.AwarenessSkill,
                role = (int)p.Role,
                wage = p.Wage,
                loyalty = p.Loyalty,
                jailedUntil = p.JailedUntil,
                lyingLowUntil = p.LyingLowUntil,
                x = p.Cell.x,
                y = p.Cell.y,
                drafted = p.Drafted,
                dead = p.Dead,
                deathCause = p.DeathCause,
                breakUntil = p.BreakUntilTick,
                food = p.Needs.Food,
                rest = p.Needs.Rest,
                bloodLoss = p.Health.BloodLoss,
                hasBed = p.Bed.HasValue,
                bedX = p.Bed?.x ?? 0,
                bedY = p.Bed?.y ?? 0,
            };
            foreach (var i in p.Health.Injuries)
                d.injuries.Add(new InjuryData { label = i.Label, part = i.Part, severity = i.Severity, bleed = i.BleedPerDay, tended = i.Tended, quality = i.TendQuality });
            foreach (var t in p.Mood.Memories)
                d.memories.Add(new ThoughtData { id = t.Id, label = t.Label, value = t.Value, expires = t.ExpiresTick });
            return d;
        }

        static Pawn FromData(PawnData d)
        {
            var p = new Pawn(d.id, d.firstName, d.lastName, d.nickname, new Color32(d.shirtR, d.shirtG, d.shirtB, 255), new Vector2Int(d.x, d.y))
            {
                ConstructionSkill = d.construction,
                CookingSkill = d.cooking,
                MedicineSkill = d.medicine,
                IntimidationSkill = d.intimidation,
                AwarenessSkill = d.awareness,
                Role = (CrewRole)d.role,
                Wage = d.wage,
                Loyalty = d.loyalty,
                JailedUntil = d.jailedUntil,
                LyingLowUntil = d.lyingLowUntil,
                Bed = d.hasBed ? new Vector2Int(d.bedX, d.bedY) : (Vector2Int?)null,
            };
            p.RestoreState(d.drafted, d.dead, d.deathCause, d.breakUntil);
            p.Needs.Food = d.food;
            p.Needs.Rest = d.rest;
            p.Health.BloodLoss = d.bloodLoss;
            foreach (var i in d.injuries)
                p.Health.Injuries.Add(new Injury { Label = i.label, Part = i.part, Severity = i.severity, BleedPerDay = i.bleed, Tended = i.tended, TendQuality = i.quality });
            foreach (var t in d.memories)
                p.Mood.Memories.Add(new Thought { Id = t.id, Label = t.label, Value = t.value, ExpiresTick = t.expires });
            return p;
        }

        // ---- Serialized shapes (JsonUtility needs public fields) ----

        [Serializable]
        sealed class SaveData
        {
            public int version;
            public int seed;
            public long ticks;
            public int width, height;
            public string terrain, buildings;
            public int baseX, baseY, baseW, baseH;
            public int cash, groceries, meals, medicine, product;
            public float respect;
            public int pendingOrderUnits, pendingOrderDay, lastPaydayWeek;
            public List<ZoneData> zones = new List<ZoneData>();
            public List<SquareData> squares = new List<SquareData>();
            public List<BusinessData> businesses = new List<BusinessData>();
            public List<BlueprintData> blueprints = new List<BlueprintData>();
            public List<PawnData> pawns = new List<PawnData>();
        }

        [Serializable]
        sealed class ZoneData
        {
            public string name;
            public int x, y, w, h;
            public float heat, demand;
        }

        [Serializable]
        sealed class SquareData
        {
            public int x, y, zone;
        }

        [Serializable]
        sealed class BusinessData
        {
            public int x, y, kind, status, pizzo, lastWeek;
            public string name;
            public float defiance;
        }

        [Serializable]
        sealed class BlueprintData
        {
            public int x, y;
            public string def;
            public bool deconstruct;
            public float work;
        }

        [Serializable]
        sealed class PawnData
        {
            public int id;
            public string firstName, lastName, nickname;
            public byte shirtR, shirtG, shirtB;
            public int construction, cooking, medicine, intimidation, awareness;
            public int role, wage;
            public float loyalty;
            public long jailedUntil, lyingLowUntil;
            public int x, y;
            public bool drafted, dead;
            public string deathCause;
            public long breakUntil;
            public float food, rest, bloodLoss;
            public bool hasBed;
            public int bedX, bedY;
            public List<InjuryData> injuries = new List<InjuryData>();
            public List<ThoughtData> memories = new List<ThoughtData>();
        }

        [Serializable]
        sealed class InjuryData
        {
            public string label, part;
            public float severity, bleed, quality;
            public bool tended;
        }

        [Serializable]
        sealed class ThoughtData
        {
            public string id, label;
            public float value;
            public long expires;
        }
    }
}
