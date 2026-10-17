using OSistema.Core;
using OSistema.Story;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// The family's standing. You start as a few guagliuni doing lavoretti for others; respect and
    /// jobs done earn the right to run your own piazza, then a clan, then a place in the System.
    /// </summary>
    public sealed partial class Organization
    {
        public static readonly string[] RankNames = { "Guagliune", "Paranza", "Clan", "'O Sistema" };
        static readonly float[] RankRespect = { 0f, 15f, 40f, 70f };
        static readonly int[] RankJobs = { 0, 3, 6, 10 };
        static readonly int[] RankMaxCrew = { 4, 6, 9, 12 };
        static readonly int[] RankMaxSquares = { 0, 1, 2, 3 };

        public int Rank { get; private set; }

        public int CompletedJobs(Game game) => game.Contracts.Completed;

        public string RankName => RankNames[Rank];
        public int MaxCrewForRank => RankMaxCrew[Rank];
        public int MaxSquaresForRank => RankMaxSquares[Rank];

        /// <summary>What it takes to reach the next rank, for the HUD. Null at the top.</summary>
        public string NextRankGoal(Game game)
        {
            if (Rank >= RankNames.Length - 1) return null;
            int next = Rank + 1;
            return $"Next: {RankNames[next]} · respect {Respect:0}/{RankRespect[next]:0} · lavoretti {CompletedJobs(game)}/{RankJobs[next]}";
        }

        public static string UnlocksAt(int rank) => rank switch
        {
            1 => "your own piazza, pistols, up to 6 crew",
            2 => "a second piazza, arson, buying shops as fronts, up to 9 crew",
            3 => "a third piazza, up to 12 crew",
            _ => "",
        };

        public void CheckRank(Game game)
        {
            int rank = 0;
            for (int r = 1; r < RankNames.Length; r++)
                if (Respect >= RankRespect[r] && CompletedJobs(game) >= RankJobs[r]) rank = r;
            if (rank <= Rank) return;
            Rank = rank;
            game.Log.Add($"The family is now a {RankName}. Unlocked: {UnlocksAt(rank)}.", MessageKind.Good);
            game.Events.Raise(EventCards.RankUp(game, rank));
            game.Newspaper.Report(game, 3, "A new power in the old town?", $"Shopkeepers speak, off the record, of a family growing in influence. They call them a {RankName.ToLower()}.");
        }

        /// <summary>Restores the rank from a save without fanfare.</summary>
        public void RestoreRank(int rank) => Rank = Mathf.Clamp(rank, 0, RankNames.Length - 1);

        public bool RequireRank(int rank, string what, Game game, out string error)
        {
            error = Rank >= rank ? null : $"{what} needs the family to be a {RankNames[rank]}. {NextRankGoal(game)}";
            return error == null;
        }
    }
}
