using System;
using System.Collections.Generic;
using System.IO;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.Social;
using OSistema.World;
using UnityEngine;

namespace OSistema.Core
{
    /// <summary>
    /// Saves the whole simulation to JSON, in a folder: family.json for everything that travels with
    /// the family (crew, money, clans, phone, the clock) and district_{id}.json for each district's
    /// map and what stands on it. Jobs are not saved: pawns simply re-think after loading.
    /// A single save1.json from before districts still loads, as the home district.
    /// Bump <see cref="Version"/> whenever the format changes.
    /// </summary>
    public static class SaveSystem
    {
        public const int Version = 11;
        const int OldestReadable = 5; // the map generator changed too much to migrate older saves

        /// <summary>The save slot: a folder.</summary>
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "save1");

        /// <summary>Where a save from before districts lived: one file next to the folder.</summary>
        static string LegacyFile(string folder) => folder.TrimEnd('/', '\\') + ".json";
        static string FamilyFile(string folder) => Path.Combine(folder, "family.json");
        static string DistrictFile(string folder, string id) => Path.Combine(folder, $"district_{id}.json");

        public static bool HasSave(string path) => File.Exists(FamilyFile(path)) || File.Exists(LegacyFile(path));

        public static void Save(Game game, string folder)
        {
            var data = Collect(game);
            // The district's ground goes in its own file; the rest is the family's.
            var district = SplitDistrict(data, game.District);
            Directory.CreateDirectory(folder);
            File.WriteAllText(DistrictFile(folder, district.id), JsonUtility.ToJson(district));
            File.WriteAllText(FamilyFile(folder), JsonUtility.ToJson(data));
        }

        public static Game Load(string folder)
        {
            SaveData data;
            if (File.Exists(FamilyFile(folder)))
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FamilyFile(folder)));
                string id = string.IsNullOrEmpty(data?.current) ? District.HomeId : data.current;
                var district = JsonUtility.FromJson<DistrictData>(File.ReadAllText(DistrictFile(folder, id)));
                MergeDistrict(data, district);
            }
            else data = JsonUtility.FromJson<SaveData>(File.ReadAllText(LegacyFile(folder)));
            return Restore(data);
        }

        /// <summary>
        /// For testing (the dev switch): saves to a scratch folder, loads it back and compares the two
        /// games; then loads the old single-file save if there is one. Returns a report for the log.
        /// </summary>
        public static string DevRoundTrip(Game game)
        {
            var report = new System.Text.StringBuilder();
            string folder = Path.Combine(Application.persistentDataPath, "dev_roundtrip");
            Save(game, folder);
            var back = Load(folder);
            int diff = 0;
            for (int i = 0; i < game.Map.CellCount; i++)
                if (game.Map.GetTerrainAt(i) != back.Map.GetTerrainAt(i) || game.Map.GetBuildingAt(i) != back.Map.GetBuildingAt(i)) diff++;
            string Counts(Game g) =>
                $"district {g.District.Id} · cells {g.Map.CellCount} · pawns {g.Pawns.All.Count} · properties {g.Properties.All.Count} · " +
                $"businesses {g.Org.Businesses.Count} · piazze {g.Org.Squares.Count} · clans {g.Clans.Count} · clan piazze {System.Linq.Enumerable.Count(g.RivalSquares)} · cash {g.Stock.Cash} · day {g.Clock.Day} {g.Clock.Hour:00}h";
            report.AppendLine($"ROUNDTRIP saved : {Counts(game)}");
            report.AppendLine($"ROUNDTRIP loaded: {Counts(back)} · differing cells {diff}");
            report.AppendLine($"ROUNDTRIP files : {string.Join(", ", System.Linq.Enumerable.Select(Directory.GetFiles(folder), f => Path.GetFileName(f) + " " + new FileInfo(f).Length / 1024 + "KB"))}");
            string legacy = Path.Combine(Application.persistentDataPath, "save1");
            if (File.Exists(LegacyFile(legacy)) && !File.Exists(FamilyFile(legacy)))
            {
                var old = Load(legacy);
                report.AppendLine($"LEGACY loaded   : {Counts(old)}");
            }
            return report.ToString();
        }

        /// <summary>Moves the map and everything on it out of the save into a district record.</summary>
        static DistrictData SplitDistrict(SaveData data, District district)
        {
            var d = new DistrictData
            {
                version = data.version, id = district.Id, name = district.Name, seed = district.Seed,
                width = data.width, height = data.height,
                terrain = data.terrain, buildings = data.buildings, styles = data.styles, decor = data.decor, rotation = data.rotation,
                baseX = data.baseX, baseY = data.baseY, baseW = data.baseW, baseH = data.baseH,
                properties = data.properties, zones = data.zones, squares = data.squares, businesses = data.businesses, blueprints = data.blueprints,
            };
            // Every clan's piazze here, tagged with the clan.
            foreach (var sq in data.rivalSquares) { sq.clan = 0; d.clanSquares.Add(sq); }
            for (int i = 0; i < data.clans.Count; i++)
                foreach (var sq in data.clans[i].squares) { sq.clan = i + 1; d.clanSquares.Add(sq); }

            data.current = district.Id;
            data.districts.Clear();
            data.districts.Add(new DistrictEntry { id = district.Id, name = district.Name, seed = district.Seed });
            data.terrain = data.buildings = data.styles = data.decor = data.rotation = null;
            data.properties = new List<PropertyData>();
            data.zones = new List<ZoneData>();
            data.squares = new List<SquareData>();
            data.businesses = new List<BusinessData>();
            data.blueprints = new List<BlueprintData>();
            data.rivalSquares = new List<SquareData>();
            foreach (var c in data.clans) c.squares = new List<SquareData>();
            return d;
        }

        /// <summary>Puts a district record back into the save, so the rest of loading reads one shape.</summary>
        static void MergeDistrict(SaveData data, DistrictData d)
        {
            data.width = d.width; data.height = d.height;
            data.terrain = d.terrain; data.buildings = d.buildings; data.styles = d.styles; data.decor = d.decor; data.rotation = d.rotation;
            data.baseX = d.baseX; data.baseY = d.baseY; data.baseW = d.baseW; data.baseH = d.baseH;
            data.properties = d.properties; data.zones = d.zones; data.squares = d.squares; data.businesses = d.businesses; data.blueprints = d.blueprints;
            data.districtName = d.name;
            data.districtSeed = d.seed;
            data.rivalSquares = new List<SquareData>();
            foreach (var sq in d.clanSquares)
            {
                if (sq.clan == 0) data.rivalSquares.Add(sq);
                else if (sq.clan - 1 < data.clans.Count) data.clans[sq.clan - 1].squares.Add(sq);
            }
        }

        static SaveData Collect(Game game)
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
                styles = Convert.ToBase64String(map.RawStyles),
                decor = Convert.ToBase64String(map.RawDecor),
                rotation = Convert.ToBase64String(map.RawRotation),
                baseX = game.PlayerBase.x,
                baseY = game.PlayerBase.y,
                baseW = game.PlayerBase.width,
                baseH = game.PlayerBase.height,
                cash = game.Stock.Cash,
                groceries = game.Stock.Groceries,
                meals = game.Stock.Meals,
                medicine = game.Stock.Medicine,
                product = game.Stock.Product,
                respect = game.Org.Respect, learned = game.Org.Learned.ToArray(),
                pendingOrderUnits = game.Org.PendingOrderUnits,
                pendingOrderDay = game.Org.PendingOrderDay,
                lastPaydayWeek = game.Org.LastPaydayWeek,
                pistols = game.Stock.Pistols,
                clean = game.Stock.Clean,
                scooters = game.Stock.Scooters, scooterModel = game.Stock.ScooterModel, scooterColour = game.Stock.ScooterColour,
                cigarettes = game.Stock.Cigarettes,
                rank = game.Org.Rank,
                jobsDone = game.Contracts.Completed,
                evidence = game.Org.Evidence,
                generation = game.Org.Generation,
                warrant = game.Org.WarrantActive,
                hiding = game.Org.Hiding,
                hidingSinceDay = game.Org.HidingSinceDay,
                leakRisk = game.Org.LeakRisk,
                rivalName = game.Rival.Name,
                rivalBoss = game.Rival.BossName,
                rivalHostility = game.Rival.Hostility,
                rivalSoldiers = game.Rival.Soldiers,
                rivalCash = game.Rival.Cash,
                rivalTruceUntil = game.Rival.TruceUntilDay,
                rivalWarned = game.Rival.Warned,
                rivalDestroyed = game.Rival.Destroyed,
                phone = game.Phone.S,
                favours = game.Favours.S,
                informants = game.Informants.S,
                news = game.Newspaper.S,
                friendsJoined = game.Org.FriendsJoined,
                lookoutShifts = game.Org.LookoutShifts,
                supplierIntroduced = game.Org.SupplierIntroduced,
                bulkDg = game.Stock.BulkDg, carriedDg = game.Org.CarriedDg, baggies = game.Stock.Baggies,
                tobaccoG = game.Stock.TobaccoG, papers = game.Stock.Papers, emptyPacks = game.Stock.EmptyPacks,
                digitalScale = game.Stock.DigitalScale, heatSealer = game.Stock.HeatSealer, rollingMachine = game.Stock.RollingMachine, stored = game.Stock.Stored,
                doseQuality = game.Stock.DoseQuality, packQuality = game.Stock.PackQuality,
            };
            foreach (var sq in game.Rival.Squares)
                data.rivalSquares.Add(new SquareData { x = sq.Cell.x, y = sq.Cell.y, zone = sq.ZoneId });
            data.rivalIntel = game.Rival.Intel;
            // Every other clan in the city.
            for (int i = 1; i < game.Clans.Count; i++)
            {
                var c = game.Clans[i];
                var cd = new ClanData
                {
                    name = c.Name, boss = c.BossName, hostility = c.Hostility, soldiers = c.Soldiers, cash = c.Cash,
                    truce = c.TruceUntilDay, warned = c.Warned, destroyed = c.Destroyed, intel = c.Intel,
                };
                foreach (var sq in c.Squares) cd.squares.Add(new SquareData { x = sq.Cell.x, y = sq.Cell.y, zone = sq.ZoneId });
                data.clans.Add(cd);
            }

            foreach (var p in game.Properties.All)
                data.properties.Add(new PropertyData
                {
                    x = p.Rect.x, y = p.Rect.y, w = p.Rect.width, h = p.Rect.height, doorX = p.Door.x, doorY = p.Door.y,
                    kind = (int)p.Kind, name = p.Name, owned = p.OwnedByPlayer, price = p.Price,
                    operation = (int)p.Operation, manager = p.Manager?.Id ?? 0,
                });

            foreach (var z in game.Territory.Zones)
                data.zones.Add(new ZoneData { name = z.Name, x = z.Rect.x, y = z.Rect.y, w = z.Rect.width, h = z.Rect.height, heat = z.Heat, demand = z.Demand });
            foreach (var sq in game.Org.Squares)
                data.squares.Add(new SquareData { x = sq.Cell.x, y = sq.Cell.y, zone = sq.ZoneId, open = sq.OpenHour, close = sq.CloseHour });
            foreach (var b in game.Org.Businesses)
                data.businesses.Add(new BusinessData
                {
                    x = b.Cell.x, y = b.Cell.y, name = b.Name, kind = (int)b.Kind,
                    // Someone sent to impose pizzo is a standing order; keep it.
                    status = (int)b.Status, pizzo = b.WeeklyPizzo, defiance = b.Defiance, lastWeek = b.LastCollectedWeek,
                    action = (int)b.PendingAction, closedUntil = b.ClosedUntil, counterDef = b.CounterDefId,
                });

            foreach (var bp in game.Construction.All)
                data.blueprints.Add(new BlueprintData { x = bp.Cell.x, y = bp.Cell.y, def = bp.Def.Id, deconstruct = bp.Deconstruct, work = bp.WorkLeft });

            foreach (var p in game.Pawns.All)
            {
                var d = ToData(p);
                var clan = p.Clan >= 0 && p.Clan < game.Clans.Count ? game.Clans[p.Clan] : game.Rival;
                d.missionSquare = p.MissionSquare != null ? clan.Squares.IndexOf(p.MissionSquare) : -1;
                data.pawns.Add(d);
            }

            return data;
        }

        static Game Restore(SaveData data)
        {
            if (data == null || data.version < OldestReadable || data.version > Version)
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

            System.Array.Copy(Convert.FromBase64String(data.styles), map.RawStyles, map.CellCount);
            System.Array.Copy(Convert.FromBase64String(data.decor), map.RawDecor, map.CellCount);
            if (!string.IsNullOrEmpty(data.rotation)) System.Array.Copy(Convert.FromBase64String(data.rotation), map.RawRotation, map.CellCount);
            var properties = new List<Property>();
            foreach (var p in data.properties)
                properties.Add(new Property
                {
                    Rect = new RectInt(p.x, p.y, p.w, p.h), Door = new Vector2Int(p.doorX, p.doorY), Kind = (PropertyKind)p.kind,
                    Name = p.name, OwnedByPlayer = p.owned, Price = p.price, Operation = (Operation)p.operation,
                });

            var zones = new List<Zone>();
            foreach (var z in data.zones)
                zones.Add(new Zone { Name = z.name, Rect = new RectInt(z.x, z.y, z.w, z.h), Heat = z.heat, Demand = z.demand });

            var district = new District(string.IsNullOrEmpty(data.current) ? District.HomeId : data.current,
                string.IsNullOrEmpty(data.districtName) ? "Quartieri" : data.districtName,
                data.districtSeed != 0 ? data.districtSeed : data.seed,
                map, new RectInt(data.baseX, data.baseY, data.baseW, data.baseH), zones, properties);
            var game = new Game(data.seed, data.ticks, district);
            game.Stock.Cash = data.cash;
            game.Stock.Product = data.product;
            game.Org.Respect = data.respect;
            game.Org.Learned.Clear();
            if (data.learned != null) game.Org.Learned.AddRange(data.learned);
            game.Org.PendingOrderUnits = data.pendingOrderUnits;
            game.Org.PendingOrderDay = data.pendingOrderDay;
            game.Org.LastPaydayWeek = data.lastPaydayWeek;
            game.Stock.Pistols = data.pistols;
            game.Stock.Clean = data.clean;
            game.Stock.Scooters = data.scooters;
            game.Stock.ScooterModel = data.scooterModel;
            game.Stock.ScooterColour = data.scooterColour;
            game.Stock.Cigarettes = data.cigarettes;
            game.Org.RestoreRank(data.rank);
            game.Contracts.Completed = data.jobsDone;
            var org = game.Org;
            org.Evidence = data.evidence;
            org.Generation = data.generation;
            org.WarrantActive = data.warrant;
            org.Hiding = data.hiding;
            org.HidingSinceDay = data.hidingSinceDay;
            org.LeakRisk = data.leakRisk;
            var rival = game.Rival;
            rival.Name = data.rivalName;
            rival.BossName = data.rivalBoss;
            rival.Hostility = data.rivalHostility;
            rival.Soldiers = data.rivalSoldiers;
            rival.Cash = data.rivalCash;
            rival.TruceUntilDay = data.rivalTruceUntil;
            rival.Warned = data.rivalWarned;
            rival.Destroyed = data.rivalDestroyed;
            foreach (var sq in data.rivalSquares)
                rival.Squares.Add(new DrugSquare { Cell = new Vector2Int(sq.x, sq.y), ZoneId = sq.zone, Rival = true, OpenHour = 0, CloseHour = 0 });
            rival.Intel = data.rivalIntel;
            if (data.clans != null)
                foreach (var cd in data.clans)
                {
                    var c = game.AddClan();
                    c.Name = cd.name; c.BossName = cd.boss; c.Hostility = cd.hostility; c.Soldiers = cd.soldiers; c.Cash = cd.cash;
                    c.TruceUntilDay = cd.truce; c.Warned = cd.warned; c.Destroyed = cd.destroyed; c.Intel = cd.intel;
                    foreach (var sq in cd.squares)
                        c.Squares.Add(new DrugSquare { Cell = new Vector2Int(sq.x, sq.y), ZoneId = sq.zone, Rival = true, OpenHour = 0, CloseHour = 0, Clan = c.Id });
                }

            foreach (var sq in data.squares)
            {
                var square = new DrugSquare { Cell = new Vector2Int(sq.x, sq.y), ZoneId = sq.zone };
                if (data.version >= 3)
                {
                    square.OpenHour = sq.open;
                    square.CloseHour = sq.close;
                }
                game.Org.RestoreSquare(square, map);
            }
            foreach (var b in data.businesses)
                game.Org.Businesses.Add(new Business
                {
                    Cell = new Vector2Int(b.x, b.y), Name = b.name, Kind = (ShopKind)b.kind, Status = (BusinessStatus)b.status,
                    WeeklyPizzo = b.pizzo, Defiance = b.defiance, LastCollectedWeek = b.lastWeek,
                    PendingAction = (Retaliation)b.action, ClosedUntil = b.closedUntil,
                    // Version 2 saves didn't store the counter: derive it from the map.
                    CounterDefId = !string.IsNullOrEmpty(b.counterDef) ? b.counterDef : map.GetBuilding(b.x, b.y)?.Id,
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
            {
                var pawn = FromData(pd);
                var own = pawn.Clan >= 0 && pawn.Clan < game.Clans.Count ? game.Clans[pawn.Clan] : rival;
                if (pawn.Faction == Faction.Rival && pd.missionSquare >= 0 && pd.missionSquare < own.Squares.Count)
                    pawn.MissionSquare = own.Squares[pd.missionSquare];
                game.Pawns.Add(pawn);
            }
            // Operation managers and mission targets point at pawns: resolve them once everyone exists.
            for (int i = 0; i < data.properties.Count && i < game.Properties.All.Count; i++)
                if (data.properties[i].manager > 0) game.Properties.All[i].Manager = game.Pawns.ById(data.properties[i].manager);
            foreach (var pd in data.pawns)
            {
                if (pd.missionTarget <= 0) continue;
                var pawn = game.Pawns.ById(pd.id);
                if (pawn != null) pawn.MissionTarget = game.Pawns.ById(pd.missionTarget);
            }

            // Version 5 saves predate the phone and the neighbours: start them fresh.
            if (data.phone != null) game.Phone.S = data.phone;
            if (data.informants != null) game.Informants.S = data.informants;
            if (data.news != null) game.Newspaper.S = data.news;
            // Before version 7 the crew started with all three friends.
            game.Org.FriendsJoined = data.version >= 7 ? data.friendsJoined : Game.FriendCount;
            game.Org.LookoutShifts = data.lookoutShifts;
            var st = game.Stock;
            st.BulkDg = data.bulkDg; game.Org.CarriedDg = data.carriedDg; st.Baggies = data.baggies;
            st.TobaccoG = data.tobaccoG; st.Papers = data.papers; st.EmptyPacks = data.emptyPacks;
            st.DigitalScale = data.digitalScale; st.HeatSealer = data.heatSealer; st.RollingMachine = data.rollingMachine;
            if (data.stored != null)
            {
                st.Stored = new int[BuildingDefs.All.Length];
                System.Array.Copy(data.stored, st.Stored, System.Math.Min(data.stored.Length, st.Stored.Length));
            }
            if (data.version >= 9) { st.DoseQuality = data.doseQuality; st.PackQuality = data.packQuality; }
            // Families from before version 8 already had a supplier.
            game.Org.SupplierIntroduced = data.version < 8 || data.supplierIntroduced;
            if (data.favours != null && data.favours.Neighbours.Count > 0) game.Favours.S = data.favours;
            else game.Favours.Setup(game, game.Rng);

            game.EnsurePlayer();
            var loadedBoss = game.Org.Boss(game);
            if (loadedBoss != null) loadedBoss.Titled = game.Org.Rank >= 1;
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
                hospitalUntil = p.HospitalUntil,
                xp = (float[])p.SkillXp.Clone(),
                faction = (int)p.Faction,
                shooting = p.ShootingSkill,
                armed = p.Armed,
                isBoss = p.IsBoss,
                aggressiveUntil = p.AggressiveUntil,
                mealsDelivered = p.MealsDelivered,
                mission = (int)p.Mission, clan = p.Clan,
                missionTarget = p.MissionTarget?.Id ?? 0,
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
                schedule = p.ScheduleString,
                hasLook = p.Look != null,
                lookBody = p.Look?.Body ?? 0, lookHead = p.Look?.Head ?? 0, lookHair = p.Look?.Hair ?? 0,
                lookHat = p.Look?.Hat ?? -1, lookNeck = p.Look?.Neck ?? -1, lookFace = p.Look?.Face ?? -1,
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
                HospitalUntil = d.hospitalUntil,
                Faction = (Faction)d.faction,
                ShootingSkill = d.shooting,
                Armed = d.armed,
                IsBoss = d.isBoss,
                AggressiveUntil = d.aggressiveUntil,
                MealsDelivered = d.mealsDelivered,
                Mission = (RivalMission)d.mission, Clan = d.clan,
                Bed = d.hasBed ? new Vector2Int(d.bedX, d.bedY) : (Vector2Int?)null,
            };
            p.ScheduleString = d.schedule;
            if (d.hasLook) p.Look = new Look { Body = d.lookBody, Head = d.lookHead, Hair = d.lookHair, Hat = d.lookHat, Neck = d.lookNeck, Face = d.lookFace };
            p.RestoreState(d.drafted, d.dead, d.deathCause, d.breakUntil);
            if (d.xp != null) System.Array.Copy(d.xp, p.SkillXp, Mathf.Min(d.xp.Length, p.SkillXp.Length));
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
            public string terrain, buildings, styles, decor, rotation;
            public List<PropertyData> properties = new List<PropertyData>();
            public int baseX, baseY, baseW, baseH;
            public int cash, groceries, meals, medicine, product;
            public float respect;
            public string[] learned;
            public PhoneState phone;
            public FavourState favours;
            public InformantState informants;
            public NewsState news;
            public int friendsJoined;
            public int lookoutShifts;
            public bool supplierIntroduced;
            public int bulkDg, carriedDg, baggies, tobaccoG, papers, emptyPacks;
            public bool digitalScale, heatSealer, rollingMachine;
            public int[] stored;
            public float doseQuality = 0.75f, packQuality = 0.75f;
            public int pendingOrderUnits, pendingOrderDay, lastPaydayWeek;
            public List<ZoneData> zones = new List<ZoneData>();
            public List<SquareData> squares = new List<SquareData>();
            public List<BusinessData> businesses = new List<BusinessData>();
            public int pistols, generation, hidingSinceDay, rank, jobsDone, clean, scooters, cigarettes, scooterModel, scooterColour;
            public float evidence, leakRisk;
            public bool warrant, hiding;
            public string rivalName, rivalBoss;
            public float rivalHostility;
            public int rivalSoldiers, rivalCash, rivalTruceUntil;
            public bool rivalWarned, rivalDestroyed;
            public float rivalIntel;
            public List<ClanData> clans = new List<ClanData>();
            public List<SquareData> rivalSquares = new List<SquareData>();
            public List<BlueprintData> blueprints = new List<BlueprintData>();
            public List<PawnData> pawns = new List<PawnData>();
            // Districts (version 11): which one the family is in, and the ones it knows.
            public string current;
            public List<DistrictEntry> districts = new List<DistrictEntry>();
            [NonSerialized] public string districtName;
            [NonSerialized] public int districtSeed;
        }

        [Serializable]
        sealed class DistrictEntry
        {
            public string id, name;
            public int seed;
        }

        /// <summary>One district's ground: the map and what stands on it.</summary>
        [Serializable]
        sealed class DistrictData
        {
            public int version;
            public string id, name;
            public int seed;
            public int width, height;
            public string terrain, buildings, styles, decor, rotation;
            public int baseX, baseY, baseW, baseH;
            public List<PropertyData> properties = new List<PropertyData>();
            public List<ZoneData> zones = new List<ZoneData>();
            public List<SquareData> squares = new List<SquareData>();
            public List<BusinessData> businesses = new List<BusinessData>();
            public List<BlueprintData> blueprints = new List<BlueprintData>();
            public List<SquareData> clanSquares = new List<SquareData>();
        }

        [Serializable]
        sealed class PropertyData
        {
            public int x, y, w, h, doorX, doorY, kind, price, operation, manager;
            public string name;
            public bool owned;
        }

        [Serializable]
        sealed class ZoneData
        {
            public string name;
            public int x, y, w, h;
            public float heat, demand;
        }

        [Serializable]
        sealed class ClanData
        {
            public string name, boss;
            public float hostility, intel;
            public int soldiers, cash, truce;
            public bool warned, destroyed;
            public List<SquareData> squares = new List<SquareData>();
        }

        [Serializable]
        sealed class SquareData
        {
            public int x, y, zone, open, close, clan;
        }

        [Serializable]
        sealed class BusinessData
        {
            public int x, y, kind, status, pizzo, lastWeek, action;
            public long closedUntil;
            public string name, counterDef;
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
            public long jailedUntil, lyingLowUntil, hospitalUntil;
            public float[] xp;
            public int faction, shooting, mealsDelivered, mission, missionTarget, missionSquare = -1, clan;
            public bool armed, isBoss;
            public long aggressiveUntil;
            public int x, y;
            public bool drafted, dead;
            public string deathCause;
            public long breakUntil;
            public float food, rest, bloodLoss;
            public bool hasBed;
            public string schedule;
            public bool hasLook;
            public int lookBody, lookHead, lookHair, lookHat = -1, lookNeck = -1, lookFace = -1;
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
