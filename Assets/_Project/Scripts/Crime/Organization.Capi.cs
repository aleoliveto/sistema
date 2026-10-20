using System.Collections.Generic;
using OSistema.Core;
using OSistema.Pawns;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// The organigram: boss → capi paranza → crews. Every pusher, lookout and capo belongs to a piazza.
    /// The boss runs the first piazza himself; from the second on, each wants a capo paranza. Wherever
    /// someone is in charge, the crew is split into a day team and a night team that between them cover
    /// the piazza's open hours; where nobody is, they all work the day shift and the nights go uncovered. The player sets piazza hours and
    /// appoints capi; the shifts follow.
    /// </summary>
    public sealed partial class Organization
    {
        public static bool PiazzaRole(CrewRole r) => r == CrewRole.Pusher || r == CrewRole.Lookout || r == CrewRole.Capo;

        public DrugSquare SquareAt(Vector2Int? cell)
        {
            if (!cell.HasValue) return null;
            foreach (var sq in Squares) if (sq.Cell == cell.Value) return sq;
            return null;
        }

        /// <summary>The piazza this person works for, or null (not a piazza job, or not placed yet).</summary>
        public DrugSquare SquareOf(Pawn p, Game game)
        {
            if (p == null || !PiazzaRole(p.Role) || Squares.Count == 0) return null;
            var sq = SquareAt(p.Piazza);
            if (sq != null) return sq;
            return Squares.Count == 1 ? Squares[0] : null;
        }

        /// <summary>The capo paranza running this piazza, or null.</summary>
        public Pawn CapoFor(DrugSquare sq, Game game)
        {
            if (sq == null) return null;
            foreach (var p in game.Pawns.Crew)
                if (!p.Dead && !p.Departed && p.Role == CrewRole.Capo && SquareOf(p, game) == sq) return p;
            return null;
        }

        /// <summary>Who this person answers to: their piazza's capo, or null for the boss.</summary>
        public Pawn CapoOf(Pawn p, Game game)
        {
            if (p == null || p.Role == CrewRole.Capo) return null;
            return CapoFor(SquareOf(p, game), game);
        }

        /// <summary>A piazza's pushers and lookouts (not its capo).</summary>
        public List<Pawn> CrewOf(DrugSquare sq, Game game)
        {
            var list = new List<Pawn>();
            foreach (var p in game.Pawns.Crew)
                if (!p.Dead && !p.Departed && (p.Role == CrewRole.Pusher || p.Role == CrewRole.Lookout) && SquareOf(p, game) == sq) list.Add(p);
            return list;
        }

        /// <summary>Someone is in charge: the boss runs the first piazza, a capo the others.</summary>
        public bool IsRun(DrugSquare sq, Game game) => sq != null && (Squares.Count > 0 && sq == Squares[0] || CapoFor(sq, game) != null);

        /// <summary>Makes this person capo of a piazza; the old capo goes back to selling there.</summary>
        public void AppointCapo(Pawn p, DrugSquare sq, Game game)
        {
            if (p == null || sq == null) return;
            var old = CapoFor(sq, game);
            if (old == p) return;
            if (old != null) old.Role = CrewRole.Pusher;
            p.Role = CrewRole.Capo;
            p.Piazza = sq.Cell;
            p.Mood.AddMemory("capo", "Made capo paranza", 8f, game.Ticks.TicksGame, GameClock.TicksPerDay * 4);
            game.Log.Add($"{p.DisplayName} is capo paranza at {game.Territory.Zones[sq.ZoneId].Name} now. The shifts are his to run.", MessageKind.Good);
            OrganiseShifts(game);
        }

        public void MoveToPiazza(Pawn p, DrugSquare sq, Game game)
        {
            if (p == null || sq == null) return;
            if (p.Role == CrewRole.Capo && CapoFor(sq, game) != null) p.Role = CrewRole.Pusher;
            p.Piazza = sq.Cell;
            OrganiseShifts(game);
        }

        /// <summary>(from, to) of a team's shift at a piazza. Short openings are one team; long ones split in two.</summary>
        public static (int from, int to) TeamHours(DrugSquare sq, int team)
        {
            int len = ((sq.CloseHour - sq.OpenHour) % 24 + 24) % 24;
            if (len == 0) len = 24;
            if (len <= 12) return (sq.OpenHour, sq.CloseHour);
            int half = (len + 1) / 2;
            int mid = (sq.OpenHour + half) % 24;
            return team == 0 ? (sq.OpenHour, mid) : (mid, sq.CloseHour);
        }

        public static bool SplitsShifts(DrugSquare sq)
        {
            int len = ((sq.CloseHour - sq.OpenHour) % 24 + 24) % 24;
            return len == 0 || len > 12;
        }

        /// <summary>Which team someone's in at their piazza (0 day, 1 night), or -1 if they cover every open hour.</summary>
        public int TeamOf(Pawn p, Game game)
        {
            var sq = SquareOf(p, game);
            if (sq == null || !IsRun(sq, game) || !SplitsShifts(sq)) return -1;
            if (p.Role == CrewRole.Capo) return 0;
            var same = new List<Pawn>();
            foreach (var c in CrewOf(sq, game)) if (c.Role == p.Role) same.Add(c);
            if (same.Count < 2) return -1;   // nobody to take turns with
            same.Sort((a, b) => a.Id.CompareTo(b.Id));
            return same.IndexOf(p) % 2;
        }

        /// <summary>
        /// Every hour: put unplaced pushers and lookouts where they're short, and set everyone's shift from
        /// their piazza's hours and team. Without anyone in charge there's no night team: the crew works days.
        /// </summary>
        public void OrganiseShifts(Game game)
        {
            if (Squares.Count == 0) return;
            foreach (var p in game.Pawns.Crew)
            {
                if (p.Dead || p.Departed || !PiazzaRole(p.Role)) continue;
                if (SquareAt(p.Piazza) == null)
                {
                    // Not placed (or their piazza closed): the one with fewest in this role.
                    DrugSquare best = null;
                    int fewest = int.MaxValue;
                    foreach (var sq in Squares)
                    {
                        if (p.Role == CrewRole.Capo && CapoFor(sq, game) != null) continue;
                        int n = 0;
                        foreach (var c in game.Pawns.Crew)
                            if (c != p && !c.Dead && c.Role == p.Role && SquareAt(c.Piazza) == sq) n++;
                        if (n < fewest) { fewest = n; best = sq; }
                    }
                    if (best == null) { p.Role = CrewRole.Pusher; best = Squares[0]; }
                    p.Piazza = best.Cell;
                }
            }
            foreach (var p in game.Pawns.Crew)
            {
                if (p.Dead || p.Departed || p.Controlled || !PiazzaRole(p.Role)) continue;
                var sq = SquareOf(p, game);
                if (sq == null) continue;
                int team = TeamOf(p, game);
                // Nobody to take turns with, or nobody in charge: the day shift only, and the nights go uncovered.
                var (from, to) = TeamHours(sq, team < 0 ? 0 : team);
                if (p.WorkHours != (from, to)) p.SetWorkHours(from, to);
            }
        }

        /// <summary>The piazze that are short of someone in charge, for the warnings.</summary>
        public int PiazzeWithoutCapo(Game game)
        {
            int n = 0;
            foreach (var sq in Squares) if (!IsRun(sq, game)) n++;
            return n;
        }
    }
}
