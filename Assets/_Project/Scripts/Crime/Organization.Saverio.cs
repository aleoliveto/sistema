using System.Collections.Generic;
using OSistema.AI;
using OSistema.Core;
using OSistema.Npcs;
using OSistema.Pawns;
using OSistema.Story;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>Don Saverio's corner, saved with the family.</summary>
    [System.Serializable]
    public sealed class SaverioState
    {
        public bool taken;          // the corner is the family's
        public int how;             // 0 not yet, 1 by force, 2 bought out, 3 on a weekly cut
        public bool feud;           // the family started shooting: his men shoot back on sight
        public bool dead;           // shot dead; his men still hold the corner until they fall too
        public int hits;            // bullets he has taken
        public int guards = Organization.SaverioGuardCount;   // his armed men still standing
        public int weeksLeft;       // the cut: Sundays still to pay
        public int lastCutWeek = -1;
        public int missed;          // Sundays missed in a row
    }

    /// <summary>
    /// The first piazza belongs to someone. Don Saverio 'o Malommo runs the corner where the family
    /// starts out, with a few armed men around him. Until it's taken, nobody else opens a piazza in
    /// his rione. Two ways to take it: kill him and his men, or pay him to retire (all at once, or a
    /// cut every Sunday for a while). Then the corner and the rione are the family's.
    /// </summary>
    public sealed partial class Organization
    {
        /// <summary>The "clan" index his men carry: not one of Game.Clans.</summary>
        public const int SaverioClan = 99;
        public const int SaverioGuardCount = 3;
        const int SaverioHitsToKill = 2;
        public const int SaverioCutDown = 2000, SaverioCutWeekly = 900, SaverioCutWeeks = 10;
        public const float SaverioCutRespect = 25f;   // he only takes a promise from a name that's worth something

        public SaverioState Sav = new SaverioState();

        /// <summary>His men on the map now (not saved: they come back to the corner when he does).</summary>
        readonly List<Pawn> saverioMen = new List<Pawn>();

        /// <summary>He's out of the picture: dead, retired, or paid to stay away.</summary>
        public bool SaverioGone => Sav.taken || Sav.dead;

        /// <summary>The cut is still being paid.</summary>
        public bool SaverioCutRunning => Sav.taken && Sav.how == 3 && Sav.weeksLeft > 0;

        /// <summary>The price to retire: a lot of money, less for a family with a name.</summary>
        public int SaverioPrice => Mathf.RoundToInt(Mathf.Clamp(15000f - Respect * 100f, 9000f, 15000f) / 500f) * 500;

        public Contact SaverioContact(Game game) =>
            game.District.Id == Core.District.HomeId ? game.Contacts.ByName(ContactBook.Saverio) : null;

        /// <summary>The rione around his corner (the nearest one, if his spot is on a road).</summary>
        public Zone SaverioZone(Game game)
        {
            var c = SaverioContact(game);
            if (c == null) return null;
            var zone = game.Territory.ZoneAt(c.Spot);
            if (zone != null) return zone;
            foreach (var cell in GridUtil.CellsAround(c.Spot, 8))
            {
                zone = game.Map.InBounds(cell) ? game.Territory.ZoneAt(cell) : null;
                if (zone != null) return zone;
            }
            return null;
        }

        /// <summary>This rione is still Don Saverio's: no piazza of the family's opens there.</summary>
        public bool SaverioHolds(Zone zone, Game game) => !Sav.taken && zone != null && zone == SaverioZone(game);

        /// <summary>The corner taken, with the family's piazza on it: the rione is the family's.</summary>
        bool SaverioCornerHeld(Zone zone, Game game)
        {
            if (!Sav.taken || zone == null || zone != SaverioZone(game)) return false;
            foreach (var sq in Squares) if (sq.ZoneId == zone.Id) return true;
            return false;
        }

        /// <summary>One line on the state of his corner, for the Piazza screen.</summary>
        public string SaverioStatus(Game game)
        {
            var zone = SaverioZone(game);
            string where = zone?.Name ?? "the corner";
            if (Sav.taken)
            {
                if (Sav.how == 1) return $"Taken by force. {where} is the family's.";
                if (SaverioCutRunning) return $"Yours, on a cut: €{SaverioCutWeekly} to Don Saverio every Sunday, {Sav.weeksLeft} more Sunday{(Sav.weeksLeft == 1 ? "" : "s")}." + (Sav.missed > 0 ? " One payment missed: miss another and he takes it back." : "");
                return $"Bought. Don Saverio has retired to Ischia, and {where} is the family's.";
            }
            if (Sav.dead) return $"Don Saverio is dead. {Sav.guards} of his men still hold {where}: finish them and it's yours.";
            if (Sav.feud) return $"At war with Don Saverio: his {Sav.guards} men shoot on sight. Kill him and them, and {where} is yours.";
            return $"Don Saverio's corner, in {where}. He's there {SaverioContact(game)?.Hours ?? "afternoons and nights"} with {Sav.guards} armed men. No piazza of yours opens in {where} until you take it: kill him and his men, or pay him to retire.";
        }

        // ---------- On the map ----------

        /// <summary>Keeps his men on the corner while he's there, and hands it over when they've all fallen.</summary>
        void SaverioTick(Game game)
        {
            if (Sav.taken || game.Ticks.TicksGame % 30 != 0) return;
            var c = SaverioContact(game);
            if (c == null) return;

            // Who's out of the fight: dead, down, or running away bleeding.
            for (int i = saverioMen.Count - 1; i >= 0; i--)
            {
                var g = saverioMen[i];
                if (!g.Dead && !g.Departed && !g.Health.Downed && g.Mission != RivalMission.Leave) continue;
                saverioMen.RemoveAt(i);
                if (g.Dead || g.Health.Downed || g.Mission == RivalMission.Leave)
                {
                    Sav.guards = Mathf.Max(0, Sav.guards - 1);
                    g.Mission = RivalMission.Leave;   // if he gets up, he's had enough
                    if (Sav.guards > 0) game.Log.Note($"One of Don Saverio's men is down. {Sav.guards} left.", MessageKind.Info, g.Cell);
                }
            }

            if (Sav.dead && Sav.guards <= 0)
            {
                TakeSaverioCorner(1, game);
                return;
            }

            // His men stand by him while he's on the corner; after he's shot, they hold it on their own.
            bool needed = Sav.dead || c.Present;
            if (!needed)
            {
                foreach (var g in saverioMen)
                {
                    g.Mission = RivalMission.Leave;
                    g.EndCurrentJob(game);
                }
                saverioMen.Clear();
                return;
            }
            while (saverioMen.Count < Sav.guards) saverioMen.Add(SpawnSaverioMan(c, saverioMen.Count, game));
        }

        Pawn SpawnSaverioMan(Contact c, int index, Game game)
        {
            var rng = game.Rng;
            var post = SaverioPost(c, index, game);
            byte g = (byte)rng.Next(50, 80);
            var pawn = new Pawn(game.Pawns.NextId(), Names.Pick(Names.FirstNames, rng), "Malommo", Names.Pick(Names.Nicknames, rng),
                new Color32(g, g, (byte)(g + 6), 255), post)
            {
                Faction = Faction.Rival,
                Clan = SaverioClan,
                Armed = true,
                ShootingSkill = rng.Next(3, 7),
                IntimidationSkill = rng.Next(4, 9),
                Mission = RivalMission.Corner,
                Armor = 50f,
            };
            if (Sav.feud) Rile(pawn);
            game.Pawns.Add(pawn);
            return pawn;
        }

        /// <summary>Where one of his men stands: by the corner, a different spot for each.</summary>
        public Vector2Int SaverioPostFor(Pawn pawn, Game game)
        {
            var c = game.Contacts.ByName(ContactBook.Saverio);
            if (c == null) return pawn.Cell;
            return SaverioPost(c, Mathf.Max(0, saverioMen.IndexOf(pawn)), game);
        }

        /// <summary>A spot a couple of steps from him, a different one for each man.</summary>
        static Vector2Int SaverioPost(Contact c, int index, Game game)
        {
            var map = game.Map;
            int found = 0;
            foreach (var cell in GridUtil.CellsAround(c.Spot, 4))
            {
                if (cell == c.Spot || (cell - c.Spot).sqrMagnitude < 2) continue;
                if (!map.InBounds(cell) || !map.IsPassable(cell) || map.GetBuilding(cell) != null || map.GetTerrain(cell.x, cell.y) == TerrainType.Floor) continue;
                if (found++ == index * 3) return cell;
            }
            return c.Spot;
        }

        static void Rile(Pawn p)
        {
            p.Feuding = true;
            p.AggressiveUntil = long.MaxValue;
        }

        /// <summary>Someone of the family laid a hand on him or his men: from now on it's war.</summary>
        public void SaverioProvoked(Game game)
        {
            if (Sav.taken) return;
            foreach (var g in saverioMen) Rile(g);
            if (Sav.feud) return;
            Sav.feud = true;
            game.Contracts.Withdraw(ContactBook.Saverio);
            var c = SaverioContact(game);
            game.Log.Add("Don Saverio's men draw their guns. There's no talking now: it's him or you.", MessageKind.Bad, c?.Spot);
        }

        /// <summary>A bullet (or a fist) for Don Saverio himself. True if it was him.</summary>
        public bool OnSaverioHit(Npc npc, bool melee, Game game)
        {
            if (npc == null || npc.ContactName != ContactBook.Saverio || Sav.taken || Sav.dead) return false;
            SaverioProvoked(game);
            var at = npc.Mover.Cell;
            if (melee)
            {
                game.Territory.AddHeat(at, 4f);
                game.Log.Add("You hit Don Saverio. He spits blood and calls his men.", MessageKind.Bad, at);
                return true;
            }
            Sav.hits++;
            game.Territory.AddHeat(at, 10f);
            if (Sav.hits < SaverioHitsToKill)
            {
                game.Log.Add("Don Saverio is hit. He staggers back against the wall, shouting for his men.", MessageKind.Bad, at);
                return true;
            }
            npc.Despawned = true;
            var c = game.Contacts.ByName(ContactBook.Saverio);
            if (c != null) c.Npc = null;
            Sav.dead = true;
            game.Contracts.Withdraw(ContactBook.Saverio);
            AddEvidence(12f, game);
            game.Territory.AddHeat(at, 30f);
            Respect = Mathf.Min(100f, Respect + 6f);
            var zone = game.Territory.ZoneAt(at);
            game.Log.Add(Sav.guards > 0
                ? $"Don Saverio 'o Malommo is dead. {Sav.guards} of his men still hold the corner."
                : "Don Saverio 'o Malommo is dead.", MessageKind.Good, at);
            game.Newspaper.Report(game, 8, $"Small-time boss shot dead in {zone?.Name ?? "the old town"}",
                "Saverio M., known to the police as the man who ran the corner, was killed on his own pavement. Nobody saw anything.");
            return true;
        }

        public void OnSaverioManDied(Pawn pawn, Game game)
        {
            game.Territory.AddHeat(pawn.Cell, 12f);
            AddEvidence(4f, game);
            game.Log.Note("One of Don Saverio's men lies dead in the street.", MessageKind.Info, pawn.Cell);
        }

        // ---------- Taking it ----------

        /// <summary>The corner changes hands: a piazza of the family's where he used to stand.</summary>
        /// <summary>Developer panel: the corner is yours, as if he'd retired.</summary>
        public void DevTakeSaverio(Game game) => TakeSaverioCorner(2, game);

        void TakeSaverioCorner(int how, Game game)
        {
            Sav.taken = true;
            Sav.how = how;
            foreach (var g in saverioMen)
            {
                g.Mission = RivalMission.Leave;
                g.EndCurrentJob(game);
            }
            saverioMen.Clear();
            game.Contracts.Withdraw(ContactBook.Saverio);
            var c = SaverioContact(game);
            if (c != null && c.Present && how != 1)
            {
                c.Npc.Despawned = true;
                c.Npc = null;
            }
            var zone = SaverioZone(game);
            if (c != null && zone != null && !Squares.Exists(s => (s.Cell - c.Spot).sqrMagnitude < 64) && Squares.Count < MaxSquares)
            {
                var square = new DrugSquare { Cell = c.Spot, ZoneId = zone.Id };
                ComputeLookoutPosts(square, game.Map);
                Squares.Add(square);
                SquaresVersion++;
            }
            Respect = Mathf.Min(100f, Respect + (how == 1 ? 8f : 4f));
            string name = zone?.Name ?? "the rione";
            game.Log.Add($"Don Saverio's corner is the family's piazza now, and {name} is your turf. Put a pusher and a lookout on it (Famiglia › Organigram).",
                MessageKind.Good, c?.Spot);
            game.Newspaper.Report(game, 3, $"New faces on the corner in {name}",
                how == 1 ? "After the killing of the man who ran it, residents say younger men have taken his place." : "The man who ran the corner for years has not been seen. Neighbours say he's gone to live by the sea.");
            if (!SupplierIntroduced)
            {
                SupplierIntroduced = true;
                var tore = game.Contacts.ByName(ContactBook.Supplier);
                game.Log.Add($"Word reaches Zì Tore 'o Grossista: whoever holds Don Saverio's corner buys from him. He meets buyers at night ({tore?.Hours}).", MessageKind.Good, tore?.Spot);
            }
        }

        static bool PayAnyMoney(int amount, Game game)
        {
            var s = game.Stock;
            if (s.Cash + s.Clean < amount) return false;
            int cash = Mathf.Min(s.Cash, amount);
            s.Cash -= cash;
            s.Clean -= amount - cash;
            return true;
        }

        public bool CanBuyOutSaverio(Game game, out string why)
        {
            why = null;
            if (Sav.taken) why = "The corner is already yours.";
            else if (Sav.dead) why = "He's dead. Finish his men.";
            else if (Sav.feud) why = "After the shooting, he won't sell you anything.";
            else if (SaverioContact(game) == null) why = "He's back in the Quartieri.";
            else if (Rank < 1) why = "He doesn't sell to kids: become a Paranza first.";
            return why == null;
        }

        /// <summary>All at once: he takes the money and goes to live by the sea.</summary>
        public bool BuyOutSaverio(Game game)
        {
            if (!CanBuyOutSaverio(game, out var why)) { game.Log.Add(why, MessageKind.Bad); return false; }
            int price = SaverioPrice;
            if (!PayAnyMoney(price, game)) { game.Log.Add($"Don Saverio wants €{price}, in cash or clean. You don't have it.", MessageKind.Bad); return false; }
            game.Log.Add($"€{price} in a shoebox. Don Saverio counts it twice, shakes your hand and leaves for Ischia.", MessageKind.Good);
            TakeSaverioCorner(2, game);
            return true;
        }

        /// <summary>A little now and a cut every Sunday: the corner is yours as long as you keep paying.</summary>
        public bool CutDealWithSaverio(Game game)
        {
            if (!CanBuyOutSaverio(game, out var why)) { game.Log.Add(why, MessageKind.Bad); return false; }
            if (Respect < SaverioCutRespect) { game.Log.Add($"He doesn't take promises from a name like yours yet (respect {Respect:0}/{SaverioCutRespect:0}).", MessageKind.Bad); return false; }
            if (!PayAnyMoney(SaverioCutDown, game)) { game.Log.Add($"He wants €{SaverioCutDown} on the table first. You don't have it.", MessageKind.Bad); return false; }
            Sav.weeksLeft = SaverioCutWeeks;
            Sav.missed = 0;
            Sav.lastCutWeek = WeekOf(game.Clock);
            game.Log.Add($"A handshake: €{SaverioCutDown} now and €{SaverioCutWeekly} every Sunday for {SaverioCutWeeks} weeks. Miss two Sundays and he takes it back.", MessageKind.Good);
            TakeSaverioCorner(3, game);
            return true;
        }

        /// <summary>Sunday at noon: his cut.</summary>
        void SaverioHourly(Game game)
        {
            var clock = game.Clock;
            if (!SaverioCutRunning || clock.Hour != 12 || !clock.IsPayday || Sav.lastCutWeek == WeekOf(clock)) return;
            Sav.lastCutWeek = WeekOf(clock);
            if (PayAnyMoney(SaverioCutWeekly, game))
            {
                Sav.weeksLeft--;
                Sav.missed = 0;
                if (Sav.weeksLeft <= 0)
                {
                    Sav.how = 2;
                    game.Log.Add($"The last €{SaverioCutWeekly} for Don Saverio. He sends word: the corner is yours for good.", MessageKind.Good);
                }
                else game.Log.Note($"€{SaverioCutWeekly} to Don Saverio for the corner. {Sav.weeksLeft} more Sunday{(Sav.weeksLeft == 1 ? "" : "s")}.", MessageKind.Info);
                return;
            }
            Sav.missed++;
            Respect = Mathf.Max(0f, Respect - 4f);
            if (Sav.missed < 2)
            {
                game.Log.Add($"No €{SaverioCutWeekly} for Don Saverio this Sunday. He lets it go once. Not twice.", MessageKind.Bad);
                return;
            }
            // Twice: the deal's off and the corner goes back to him.
            var c = SaverioContact(game);
            if (c != null) CloseSquareAt(c.Spot);
            Sav.taken = false;
            Sav.how = 0;
            Sav.weeksLeft = 0;
            Sav.missed = 0;
            Sav.guards = SaverioGuardCount;
            game.Log.Add("Two Sundays without his cut: Don Saverio takes his corner back. His men are on it tonight.", MessageKind.Bad, c?.Spot);
        }

        /// <summary>What the objectives line says about him, if anything.</summary>
        public (string text, Vector2 at)? SaverioGoal(Game game)
        {
            if (Sav.taken) return null;
            var c = SaverioContact(game);
            if (c == null) return null;
            if (Sav.dead || Sav.feud)
            {
                string who = Sav.dead ? $"Finish Don Saverio's men ({Sav.guards} left)" : "Kill Don Saverio and his men";
                return (who, c.Present ? c.Position : Mover.CellCenter(c.Spot));
            }
            if (Rank < 1 || Squares.Count > 0) return null;
            return ("Take Don Saverio's corner: kill him and his men, or buy him out (Piazza › Piazze)", c.Present ? c.Position : Mover.CellCenter(c.Spot));
        }

        /// <summary>Old saves: a family that already runs a piazza in his rione took it before he existed.</summary>
        public void SaverioFromOldSave(Game game)
        {
            Sav = new SaverioState();
            var zone = SaverioZone(game);
            if (zone != null && Squares.Exists(s => s.ZoneId == zone.Id)) { Sav.taken = true; Sav.how = 2; }
        }
    }
}
