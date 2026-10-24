using System.Collections.Generic;
using OSistema.Core;
using OSistema.Pawns;
using OSistema.World;

namespace OSistema.Crime
{
    /// <summary>
    /// Running the people without the paperwork: what the family is short of, a job for a newcomer, and
    /// one button that sorts everyone out (jobs, a capo where one's missing, beds). The player can still
    /// set anything by hand; this only fills the gaps.
    /// </summary>
    public sealed partial class Organization
    {
        /// <summary>One gap in the family: a job nobody is doing.</summary>
        public struct Need
        {
            public CrewRole Role;
            public DrugSquare Square;   // for piazza jobs
            public string Why;
        }

        int CountRole(CrewRole role, DrugSquare sq, Game game)
        {
            int n = 0;
            foreach (var c in game.Pawns.Crew)
                if (!c.Dead && !c.Departed && !c.Controlled && c.Role == role && (sq == null || SquareOf(c, game) == sq)) n++;
            return n;
        }

        /// <summary>What the family is short of, most important first.</summary>
        public List<Need> Needs(Game game)
        {
            var list = new List<Need>();
            string Name(DrugSquare sq) => game.Territory.Zones[sq.ZoneId].Name;
            // Every piazza: a pusher and a lookout for each team that works it.
            foreach (var sq in Squares)
            {
                int teams = SplitsShifts(sq) && IsRun(sq, game) ? 2 : 1;
                for (int k = CountRole(CrewRole.Pusher, sq, game); k < teams; k++)
                    list.Add(new Need { Role = CrewRole.Pusher, Square = sq, Why = $"a pusher at {Name(sq)}" });
                for (int k = CountRole(CrewRole.Lookout, sq, game); k < teams; k++)
                    list.Add(new Need { Role = CrewRole.Lookout, Square = sq, Why = $"a lookout at {Name(sq)}" });
            }
            // Product to bag, cigarettes to roll: someone at each table there is.
            if (game.Map.PositionsOf(BuildingDefs.BaggingTable).Count > 0 && Squares.Count > 0 && CountRole(CrewRole.Bagger, null, game) == 0)
                list.Add(new Need { Role = CrewRole.Bagger, Why = "someone at the bagging table" });
            if (game.Map.PositionsOf(BuildingDefs.RollingTable).Count > 0 && CountRole(CrewRole.Roller, null, game) == 0)
                list.Add(new Need { Role = CrewRole.Roller, Why = "someone at the rolling table" });
            // A man at the boss's side once there's a name to protect.
            if (Rank >= 1 && CountRole(CrewRole.Bodyguard, null, game) == 0)
                list.Add(new Need { Role = CrewRole.Bodyguard, Why = "a bodyguard for the boss" });
            return list;
        }

        /// <summary>The job for someone with nothing to do: the first gap, or the street when there's none.</summary>
        CrewRole PickJob(Pawn p, List<Need> needs, Game game, out DrugSquare square)
        {
            square = null;
            if (needs.Count > 0)
            {
                var n = needs[0];
                needs.RemoveAt(0);
                square = n.Square;
                return n.Role;
            }
            // Nothing missing: the street pays a little. Half mind cars, half work the stall.
            return CountRole(CrewRole.Parking, null, game) <= CountRole(CrewRole.Vendor, null, game) ? CrewRole.Parking : CrewRole.Vendor;
        }

        void GiveJob(Pawn p, CrewRole role, DrugSquare sq, Game game)
        {
            p.Role = role;
            if (sq != null) p.Piazza = sq.Cell;
        }

        /// <summary>A newcomer goes where the family is short, and sleeps where there's a bed.</summary>
        public void PlaceNewcomer(Pawn p, Game game)
        {
            var needs = Needs(game);
            var role = PickJob(p, needs, game, out var sq);
            GiveJob(p, role, sq, game);
            HouseSomewhere(p, game);
            OrganiseShifts(game);
        }

        /// <summary>Into the house with the most free beds (the basso if it's all the same).</summary>
        void HouseSomewhere(Pawn p, Game game)
        {
            Property best = null;
            int bestFree = int.MinValue;
            foreach (var h in Housing.Houses(game))
            {
                int free = Housing.BedsIn(h, game).Count - Housing.Residents(h, game).Count + (Housing.HomeOf(p, game) == h ? 1 : 0);
                if (free > bestFree) { bestFree = free; best = h; }
            }
            Housing.MoveIn(p, best);
        }

        /// <summary>
        /// One button: everyone without a job gets one where it's needed, piazze from the second on get a
        /// capo when they have the people, and anyone in a house short of beds moves where there's room.
        /// Returns a line saying what changed.
        /// </summary>
        public string SortOutCrew(Game game)
        {
            int jobs = 0, capi = 0, moved = 0;
            var needs = Needs(game);
            // People on the street while a piazza is short come off it first.
            var crew = new List<Pawn>();
            foreach (var c in game.Pawns.Crew) if (!c.Dead && !c.Departed && !c.Controlled && !c.IsBoss && !c.InJail) crew.Add(c);
            foreach (var p in crew)
            {
                bool idle = p.Role == CrewRole.General;
                bool street = p.Role == CrewRole.Parking || p.Role == CrewRole.Vendor;
                if (!idle && !(street && needs.Count > 0)) continue;
                var role = PickJob(p, needs, game, out var sq);
                if (role == p.Role && sq == null) continue;
                GiveJob(p, role, sq, game);
                jobs++;
            }
            // A capo for every piazza after the first that has enough people to split days and nights.
            for (int i = 1; i < Squares.Count; i++)
            {
                var sq = Squares[i];
                if (CapoFor(sq, game) != null) continue;
                Pawn best = null;
                foreach (var c in CrewOf(sq, game)) if (best == null || c.Loyalty > best.Loyalty) best = c;
                if (best != null && CrewOf(sq, game).Count >= 2) { AppointCapo(best, sq, game); capi++; }
            }
            // Beds: anyone in a house with more people than beds moves to one with room.
            foreach (var p in crew)
            {
                var home = Housing.HomeOf(p, game);
                if (Housing.BedsShort(home, game) <= 0) continue;
                var before = home;
                HouseSomewhere(p, game);
                if (Housing.HomeOf(p, game) != before) moved++;
            }
            OrganiseShifts(game);
            if (jobs + capi + moved == 0)
                return needs.Count == 0 ? "Everyone has a job and a bed. Nothing to change." : $"Still short of {needs[0].Why}: recruit someone.";
            var parts = new List<string>();
            if (jobs > 0) parts.Add($"{jobs} given a job");
            if (capi > 0) parts.Add($"{capi} made capo");
            if (moved > 0) parts.Add($"{moved} moved to a house with a free bed");
            return string.Join(", ", parts) + ".";
        }
    }
}
