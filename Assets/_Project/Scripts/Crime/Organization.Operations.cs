using System.Collections.Generic;
using OSistema.Core;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// Businesses beyond the piazza, run out of homes the family owns, each needing a manager from the crew:
    /// a bisca (cards and illegal betting), a laboratorio (fake designer bags and football shirts),
    /// a deposito (contraband cigarettes, fed by night runs to the boats).
    /// </summary>
    public sealed partial class Organization
    {
        public static string Name(Operation op) => op switch
        {
            Operation.Bisca => "Bisca",
            Operation.Laboratorio => "Laboratorio falsi",
            Operation.Deposito => "Deposito sigarette",
            Operation.Nascondiglio => "Nascondiglio (stash flat)",
            _ => "None",
        };

        public static string Describe(Operation op) => op switch
        {
            Operation.Bisca => "Cards, dice and bets on the football. Nightly takings depend on how much the manager is feared.",
            Operation.Laboratorio => "Fake bags and Napoli shirts for the market stalls. A handy manager makes more.",
            Operation.Deposito => "Sells your ROSSE by the pack, all day, to the stalls and the bars. Keep it stocked from the rolling table.",
            Operation.Nascondiglio => "Where the doses sleep, near the piazza. Pushers fetch from here instead of the basso, and a runner keeps them supplied so they carry only a few. If the police find it, they find all of it. No manager needed.",
            _ => "",
        };

        public static int SetupCost(Operation op) => op switch
        {
            Operation.Bisca => 1500,
            Operation.Laboratorio => 2000,
            Operation.Nascondiglio => 300,
            _ => 2500,
        };

        public static int RankFor(Operation op) => op == Operation.Deposito ? 2 : op == Operation.Nascondiglio ? 0 : 1;

        public bool SetUpOperation(Property p, Operation op, Game game, out string error)
        {
            error = null;
            if (!p.OwnedByPlayer || p.Kind != PropertyKind.Home) error = "Operations run out of homes the family owns.";
            else if (p.Rect.Overlaps(game.PlayerBase)) error = "Not in the family basso: too close to home.";
            else if (p.Operation != Operation.None) error = $"There's already a {Name(p.Operation).ToLower()} here.";
            if (error != null) return false;
            if (!RequireRank(RankFor(op), Name(op), game, out error)) return false;
            if (!Pay(SetupCost(op), game, out error)) return false;

            p.Operation = op;
            Furnish(p, op, game);
            game.Log.Add(op == Operation.Nascondiglio
                ? $"{p.Name} is a nascondiglio now. Piazze within reach take their doses from here; a runner keeps it stocked from the basso."
                : $"A {Name(op).ToLower()} opens in {p.Name}. Choose a manager to run it.", MessageKind.Good, p.Door);
            return true;
        }

        // Fit the place out for the job.
        static void Furnish(Property p, Operation op, Game game)
        {
            var map = game.Map;
            var r = p.Interior;
            var items = op switch
            {
                Operation.Bisca => new[] { BuildingDefs.Table, BuildingDefs.Table, BuildingDefs.Armchair, BuildingDefs.Armchair, BuildingDefs.Television },
                Operation.Laboratorio => new[] { BuildingDefs.KitchenCounter, BuildingDefs.KitchenCounter, BuildingDefs.KitchenCounter, BuildingDefs.Wardrobe },
                Operation.Nascondiglio => new[] { BuildingDefs.Safe, BuildingDefs.Wardrobe },
                _ => new[] { BuildingDefs.Wardrobe, BuildingDefs.Wardrobe, BuildingDefs.Wardrobe, BuildingDefs.Safe },
            };
            int i = 0;
            for (int y = r.yMin; y < r.yMax && i < items.Length; y++)
            for (int x = r.xMin; x < r.xMax && i < items.Length; x++)
            {
                bool againstWall = x == r.xMin || x == r.xMax - 1 || y == r.yMin || y == r.yMax - 1;
                if (!againstWall || map.GetBuilding(x, y) != null) continue;
                bool nearDoor = false;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (map.InBounds(x + dx, y + dy) && map.GetBuilding(x + dx, y + dy) == BuildingDefs.Door) nearDoor = true;
                if (nearDoor) continue;
                map.SetBuilding(x, y, items[i++]);
            }
        }

        public void AssignManager(Property p, Pawn pawn, Game game)
        {
            foreach (var other in game.Properties.All)
                if (other.Manager == pawn) other.Manager = null;
            if (p.Manager != null && p.Manager.Role == CrewRole.Manager) p.Manager.Role = CrewRole.General;
            p.Manager = pawn;
            pawn.Role = CrewRole.Manager;
            game.Log.Add($"{pawn.DisplayName} now runs the {Name(p.Operation).ToLower()}.", MessageKind.Info, p.Door);
        }

        public Property ManagedBy(Pawn pawn, Game game) => game.Properties.All.Find(p => p.Manager == pawn && p.Operation != Operation.None);

        /// <summary>Each night the operations hand over their takings, in dirty cash.</summary>
        void DailyOperations(Game game)
        {
            int total = 0;
            var lines = new List<string>();
            foreach (var p in game.Properties.All)
            {
                if (p.Operation == Operation.None || p.Operation == Operation.Nascondiglio || !p.OwnedByPlayer) continue;
                var m = p.Manager;
                if (m == null || !m.IsPresent || m.Role != CrewRole.Manager)
                {
                    lines.Add($"the {Name(p.Operation).ToLower()} stayed shut (no manager)");
                    continue;
                }
                int income;
                float heat;
                switch (p.Operation)
                {
                    case Operation.Bisca:
                        income = 70 + 6 * m.IntimidationSkill;
                        heat = 0.6f;
                        m.Learn(Skill.Intimidation, 4f, game);
                        break;
                    case Operation.Laboratorio:
                        income = 60 + 7 * m.ConstructionSkill;
                        heat = 0.3f;
                        m.Learn(Skill.Construction, 4f, game);
                        break;
                    default:
                        int sold = Mathf.Min(game.Stock.Cigarettes, 15 + m.AwarenessSkill * 2);
                        game.Stock.Cigarettes -= sold;
                        income = sold * PackPrice(game);
                        heat = 0.4f;
                        m.Learn(Skill.Awareness, 3f, game);
                        if (sold == 0) lines.Add("the deposito had no ROSSE to sell");
                        break;
                }
                if (income <= 0) continue;
                total += income;
                game.Territory.AddHeat(p.Door, heat);
                AddEvidence(0.3f, game);
                game.PopupAt(Mover.CellCenter(p.Door), $"+€{income}");
            }
            if (total > 0)
            {
                game.Stock.Cash += total;
                game.Log.Add($"The operations brought in €{total} in cash last night.", MessageKind.Good);
            }
            foreach (var l in lines) game.Log.Add(char.ToUpper(l[0]) + l.Substring(1) + ".", MessageKind.Bad);
        }
    }
}
