using System.Collections.Generic;
using System.Linq;
using OSistema.Core;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    public enum ContractKind { Delivery, Debt, ScooterTheft, Smuggling }
    public enum ContractState { Offered, Active, Done, Failed, Expired }

    /// <summary>
    /// A lavoretto: a small job for someone in the neighbourhood. This is how a paranza starts out,
    /// earning money and respect before it can run its own piazze.
    /// </summary>
    public sealed class Contract
    {
        public int Id;
        public ContractKind Kind;
        public string Giver;
        public string Title;
        public string Text;
        public string Neapolitan;
        public string English;
        public int Reward;
        public float RespectReward;
        public string Risk;
        public Vector2Int? From;   // where to pick something up (or what to take)
        public Vector2Int To;      // where to deliver, or whose door to knock on
        public long ExpiresTick;
        public ContractState State;
        public Pawn Assignee;
        public string Outcome;
        public int Stage;          // progress, kept here so the job survives breaks for food and sleep
        public float Work;
    }

    /// <summary>The jobs on offer from the neighbourhood's contacts, refreshed through the day.</summary>
    public sealed class ContractBoard
    {
        const int MaxOffers = 4;

        public readonly List<Contract> All = new List<Contract>();
        public int Completed;
        public int Version { get; private set; }

        int nextId = 1;
        int lastAbsoluteHour = -1;

        public IEnumerable<Contract> Offers
        {
            get { foreach (var c in All) if (c.State == ContractState.Offered) yield return c; }
        }

        public int OfferCount
        {
            get
            {
                int n = 0;
                foreach (var c in All) if (c.State == ContractState.Offered) n++;
                return n;
            }
        }

        public Contract ActiveFor(Pawn p) => All.Find(c => c.State == ContractState.Active && c.Assignee == p);

        public void Tick(Game game)
        {
            int absoluteHour = game.Clock.Day * 24 + game.Clock.Hour;
            if (absoluteHour == lastAbsoluteHour) return;
            bool first = lastAbsoluteHour < 0;
            lastAbsoluteHour = absoluteHour;

            long now = game.Ticks.TicksGame;
            foreach (var c in All.ToArray())
                if (c.State == ContractState.Active && (c.Assignee == null || !c.Assignee.IsPresent))
                    Finish(c, false, $"{c.Assignee?.DisplayName ?? "the crew member"} never made it.", game);
            foreach (var c in All)
                if (c.State == ContractState.Offered && now > c.ExpiresTick)
                {
                    c.State = ContractState.Expired;
                    Version++;
                }
            All.RemoveAll(c => (c.State == ContractState.Expired || c.State == ContractState.Done || c.State == ContractState.Failed) && now - c.ExpiresTick > GameClock.TicksPerDay * 2);

            int hour = game.Clock.Hour;
            // Work comes in the morning, after lunch and, for the smugglers, in the evening.
            if (first || hour == 9 || hour == 14 || hour == 19)
            {
                int added = 0;
                while (OfferCount < MaxOffers && added < 2)
                {
                    var c = Generate(game, hour);
                    if (c == null) break;
                    All.Add(c);
                    added++;
                }
                if (added > 0)
                {
                    Version++;
                    game.Log.Add(added == 1 ? $"{LastGiver} has a lavoretto for you. (Lavoretti button, bottom right)" : "New lavoretti on offer. (Lavoretti button, bottom right)", MessageKind.Good);
                }
            }
        }

        string LastGiver => All.Count > 0 ? All[All.Count - 1].Giver : "Someone";

        // ---------- Generating jobs ----------

        Contract Generate(Game game, int hour)
        {
            var rng = game.Rng;
            bool evening = hour >= 18;
            var kinds = new List<ContractKind> { ContractKind.Delivery, ContractKind.Debt, ContractKind.ScooterTheft };
            if (evening) { kinds.Add(ContractKind.Smuggling); kinds.Add(ContractKind.Smuggling); }
            var kind = kinds[rng.Next(kinds.Count)];
            var home = Vector2Int.RoundToInt(game.PlayerBase.center);
            long expires = game.Ticks.TicksGame + GameClock.TicksPerDay;

            switch (kind)
            {
                case ContractKind.Delivery:
                {
                    var from = DoorNear(game, home, 8, 30);
                    var to = DoorNear(game, home, 30, 70);
                    if (from == null || to == null) return null;
                    return Make(kind, "'O Zio Totonno", "Deliver a package",
                        $"Pick up a parcel in {ZoneName(game, from.Value)} and take it to a door in {ZoneName(game, to.Value)}. Don't open it. Don't get stopped by the police.",
                        "Nun 'o arapì. E nun te fa' fermà.", "Don't open it. And don't get stopped.",
                        rng.Next(12, 26) * 10, 1f, "Police patrols may stop and search the courier.", from, to.Value, expires);
                }
                case ContractKind.Debt:
                {
                    var to = DoorNear(game, home, 12, 60);
                    if (to == null) return null;
                    return Make(kind, "Don Peppe 'o Strozzino", "Collect a debt",
                        $"A shopkeeper in {ZoneName(game, to.Value)} owes Don Peppe money and stopped answering the door. Go and remind him. You keep a third.",
                        "Chi tene 'o debito, tene 'o padrone.", "He who has a debt has a master.",
                        rng.Next(15, 36) * 10, 1.5f, "He might refuse. Intimidation helps. Neighbours may call the police.", null, to.Value, expires);
                }
                case ContractKind.ScooterTheft:
                {
                    var scooter = FindNear(game, BuildingDefs.ParkedScooter, home, 10, 60);
                    var fence = DoorNear(game, home, 20, 70);
                    if (scooter == null || fence == null) return null;
                    return Make(kind, "Nanninella 'a Ricettatrice", "Steal a scooter",
                        $"There's a nice scooter parked in {ZoneName(game, scooter.Value)}. Take it to Nanninella's garage in {ZoneName(game, fence.Value)} before anyone notices.",
                        "Nu Liberty nuovo nuovo. Portamelo, e nun fa' rummore.", "A brand new Liberty. Bring it to me, quietly.",
                        rng.Next(20, 31) * 10, 1f, "Witnesses raise heat; a passer-by may call the police.", scooter, fence.Value, expires);
                }
                default:
                {
                    var landing = Landing(game);
                    var drop = DoorNear(game, home, 10, 60);
                    if (landing == null || drop == null) return null;
                    return Make(ContractKind.Smuggling, "'O Marenaro", "Cigarettes off the boat",
                        $"Tonight, between 22:00 and 04:00, a boat comes in to the lungomare. Wait for it at the gap in the railing, then carry the cases to a basso in {ZoneName(game, drop.Value)}.",
                        "Stanotte 'a varca arriva. Nun fa' tardi.", "Tonight the boat comes. Don't be late.",
                        rng.Next(35, 51) * 10, 2f, "The Guardia di Finanza sometimes watches the coast.", landing, drop.Value,
                        game.Ticks.TicksGame + GameClock.TicksPerDay / 2);
                }
            }
        }

        Contract Make(ContractKind kind, string giver, string title, string text, string nap, string en, int reward, float respect,
            string risk, Vector2Int? from, Vector2Int to, long expires) => new Contract
        {
            Id = nextId++, Kind = kind, Giver = giver, Title = title, Text = text, Neapolitan = nap, English = en,
            Reward = reward, RespectReward = respect, Risk = risk, From = from, To = to, ExpiresTick = expires,
        };

        static string ZoneName(Game game, Vector2Int c) => game.Territory.ZoneAt(c)?.Name ?? "the rione";

        static Vector2Int? DoorNear(Game game, Vector2Int around, int min, int max)
        {
            var doors = new List<Vector2Int>(game.Map.PositionsOf(BuildingDefs.Door));
            var rng = game.Rng;
            for (int i = 0; i < 60 && doors.Count > 0; i++)
            {
                var d = doors[rng.Next(doors.Count)];
                float dist = (d - around).magnitude;
                if (dist < min || dist > max || game.PlayerBase.Contains(d) || game.Properties.IsOwnedHome(d)) continue;
                // Must open onto the street.
                foreach (var n in new[] { d + Vector2Int.left, d + Vector2Int.right, d + Vector2Int.up, d + Vector2Int.down })
                    if (game.Map.InBounds(n) && game.Map.IsPassable(n) && game.Map.GetTerrain(n.x, n.y) != TerrainType.Floor) return d;
            }
            return null;
        }

        static Vector2Int? FindNear(Game game, BuildingDef def, Vector2Int around, int min, int max)
        {
            var list = new List<Vector2Int>(game.Map.PositionsOf(def));
            var rng = game.Rng;
            for (int i = 0; i < 60 && list.Count > 0; i++)
            {
                var c = list[rng.Next(list.Count)];
                float dist = (c - around).magnitude;
                if (dist >= min && dist <= max) return c;
            }
            return null;
        }

        // The gap in the railing on the lungomare where boats tie up.
        static Vector2Int? Landing(Game game)
        {
            var map = game.Map;
            var gaps = new List<Vector2Int>();
            for (int x = 1; x < map.Width - 1; x++)
            for (int y = 1; y < 20; y++)
                if (map.GetTerrain(x, y - 1) == TerrainType.Water && map.GetTerrain(x, y) == TerrainType.Paving && map.GetBuilding(x, y) == null)
                    gaps.Add(new Vector2Int(x, y));
            return gaps.Count > 0 ? gaps[game.Rng.Next(gaps.Count)] : (Vector2Int?)null;
        }

        // ---------- Taking and finishing jobs ----------

        public bool Assign(Contract c, Pawn p, Game game, out string error)
        {
            error = null;
            if (c.State != ContractState.Offered) error = "That job is gone.";
            else if (!p.CanAct || p.InJail) error = $"{p.DisplayName} can't go right now.";
            else if (ActiveFor(p) != null) error = $"{p.DisplayName} is already on a job.";
            if (error != null) return false;

            c.State = ContractState.Active;
            c.Assignee = p;
            p.SetDrafted(false, game);
            p.EndCurrentJob(game);
            Version++;
            game.Log.Add($"{p.DisplayName} takes the job for {c.Giver}: {c.Title.ToLower()}.", MessageKind.Info, c.From ?? c.To);
            return true;
        }

        public void Finish(Contract c, bool success, string outcome, Game game)
        {
            if (c.State != ContractState.Active) return;
            c.State = success ? ContractState.Done : ContractState.Failed;
            c.Outcome = outcome;
            c.ExpiresTick = game.Ticks.TicksGame;
            Version++;
            var p = c.Assignee;
            if (success)
            {
                Completed++;
                game.Stock.Cash += c.Reward;
                game.Org.Respect = Mathf.Min(100f, game.Org.Respect + c.RespectReward);
                game.PopupAt(p.Position, $"+€{c.Reward}");
                p.Mood.AddMemory("job_done", "Pulled off a job", 4f, game.Ticks.TicksGame, GameClock.TicksPerDay / 2);
                p.Say("job_done");
                game.Log.Add($"Job done for {c.Giver}: {outcome} +€{c.Reward}, respect +{c.RespectReward:0.#}.", MessageKind.Good, p.Cell);
            }
            else
            {
                game.Org.Respect = Mathf.Max(0f, game.Org.Respect - 0.5f);
                game.Log.Add($"The job for {c.Giver} went wrong: {outcome}", MessageKind.Bad, p.Cell);
            }
            game.Org.CheckRank(game);
        }
    }
}
