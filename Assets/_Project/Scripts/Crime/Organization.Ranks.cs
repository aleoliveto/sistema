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
        public static readonly string[] RankNames = { "Guaglione", "Paranza", "Clan", "'O Sistema" };
        public const float FriendsRespect = 6f;   // when the boys from the vicolo start wanting in
        static readonly float[] RankRespect = { 0f, 15f, 40f, 70f };
        static readonly int[] RankJobs = { 0, 4, 8, 12 };
        static readonly int[] RankMaxCrew = { 4, 6, 9, 12 };
        static readonly int[] RankMaxSquares = { 0, 1, 2, 3 };

        public int Rank { get; private set; }

        public int CompletedJobs(Game game) => game.Contracts.Completed;

        public string RankName => RankNames[Rank];
        public int MaxCrewForRank => Rank == 0 && Respect < FriendsRespect ? 1 : RankMaxCrew[Rank];
        public int FriendsJoined;
        /// <summary>The day the last friend called: they come one at a time, a few days apart, as the name grows.</summary>
        public int LastFriendCallDay = -10;
        const int DaysBetweenFriends = 3;
        const float RespectPerFriend = 6f;
        /// <summary>The rank the family basso was last done up for (Interiors).</summary>
        public int DressedRank;
        public int MaxSquaresForRank => RankMaxSquares[Rank];

        /// <summary>What it takes to reach the next rank, for the HUD. Null at the top.</summary>
        public string NextRankGoal(Game game)
        {
            if (Rank >= RankNames.Length - 1) return null;
            if (Rank == 0 && Respect < FriendsRespect)
                return $"Make a name for yourself: respect {Respect:0}/{FriendsRespect:0}, and the boys from the vicolo will want in.";
            int next = Rank + 1;
            return $"Next: {RankNames[next]} · respect {Respect:0}/{RankRespect[next]:0} · lavoretti {CompletedJobs(game)}/{RankJobs[next]}";
        }

        /// <summary>What the next rank asks, as a checklist: (line, done). Empty at the top.</summary>
        public System.Collections.Generic.List<(string text, bool done)> RankChecklist(Game game)
        {
            var list = new System.Collections.Generic.List<(string, bool)>();
            if (Rank >= RankNames.Length - 1) return list;
            int next = Rank + 1;
            list.Add(($"Respect {Mathf.Min(Respect, RankRespect[next]):0}/{RankRespect[next]:0}", Respect >= RankRespect[next]));
            list.Add(($"Lavoretti done {Mathf.Min(CompletedJobs(game), RankJobs[next])}/{RankJobs[next]}", CompletedJobs(game) >= RankJobs[next]));
            return list;
        }

        /// <summary>How far along the way to the next rank, 0..1 (the slower of respect and lavoretti). 1 at the top.</summary>
        public float RankProgress(Game game)
        {
            if (Rank >= RankNames.Length - 1) return 1f;
            int next = Rank + 1;
            float r = RankRespect[next] <= 0f ? 1f : Mathf.Clamp01(Respect / RankRespect[next]);
            float j = RankJobs[next] <= 0 ? 1f : Mathf.Clamp01(CompletedJobs(game) / (float)RankJobs[next]);
            return Mathf.Min(r, j);
        }

        public static string UnlocksAt(int rank) => rank switch
        {
            1 => "your own piazza, pistols, up to 6 crew",
            2 => "pizzo on the shops of your turf, a second piazza, arson, buying shops as fronts, up to 9 crew",
            3 => "a third piazza, up to 12 crew",
            _ => "",
        };

        /// <summary>Developer switches only: jump to a rank.</summary>
        public void DevSetRank(int rank) => Rank = Mathf.Clamp(rank, 0, RankNames.Length - 1);

        public void CheckRank(Game game)
        {
            CheckIntroduction(game);
            var head = Boss(game);
            if (head != null) head.Titled = Rank >= 1;
            if (head != null && !head.Controlled) game.EnsurePlayer();

            // Word gets round: the friends from the vicolo call.
            // One friend at a time, never on the first day, a few days apart, and each wants more of a name than the last.
            if (Respect >= FriendsRespect + FriendsJoined * RespectPerFriend && FriendsJoined < Game.FriendCount && !FriendCalling(game)
                && game.Clock.Day >= 2 && game.Clock.Day - LastFriendCallDay >= DaysBetweenFriends
                && game.Clock.Hour >= 9 && game.Clock.Hour <= 22 && game.Rng.NextDouble() < 0.3)
            {
                LastFriendCallDay = game.Clock.Day;
                var m = game.Phone.Send(game, Social.MsgKind.Friend, Game.FriendName(FriendsJoined),
                    FriendsJoined == 0 ? "Gennà, aggio sentuto ca mo' faje 'e cose serie. Pigliame cu te, fratè."
                                       : "Pure io, Gennà. Nun me lassà 'ncopp' 'o muretto a guardà.",
                    FriendsJoined == 0 ? "Gennà, I heard you're doing serious things now. Take me with you, brother."
                                       : "Me too, Gennà. Don't leave me on the wall watching.",
                    "A friend from the vicolo wants to work with you. He'll want a little money every Sunday.", 24f);
                m.Amount = FriendsJoined;
                m.Choices.Add("Bring him in");
                m.Hints.Add("He joins the crew. €60 a week.");
                m.Choices.Add("Not yet");
                m.Hints.Add("He'll ask again.");
            }

            int rank = 0;
            for (int r = 1; r < RankNames.Length; r++)
                if (Respect >= RankRespect[r] && CompletedJobs(game) >= RankJobs[r]) rank = r;
            if (rank <= Rank) return;
            Rank = rank;
            if (head != null) head.Titled = Rank >= 1;
            game.Log.Add($"The family is now a {RankName}. Unlocked: {UnlocksAt(rank)}.", MessageKind.Good);
            game.Events.Raise(EventCards.RankUp(game, rank));
            game.Newspaper.Report(game, 3, "A new power in the old town?", $"Shopkeepers speak, off the record, of a family growing in influence. They call them a {RankName.ToLower()}.");
        }

        bool FriendCalling(Game game)
        {
            foreach (var m in game.Phone.Messages)
                if (m.KindOf == Social.MsgKind.Friend && (m.Open(game.Ticks.TicksGame) || game.Ticks.TicksGame - m.Expires < GameClock.TicksPerDay)) return true;
            return false;
        }

        public void OnFriendAnswer(Social.PhoneMessage m, int choice, Game game)
        {
            if (choice != 0 || m.Amount != FriendsJoined) return;
            var p = game.AddFriend(FriendsJoined);
            if (p == null)
            {
                game.Log.Add("No room at the basso.", MessageKind.Bad);
                return;
            }
            FriendsJoined++;
            p.Say("recruited");
            game.Log.Add($"{p.FullName} “{p.Nickname}” is with you now. A crew of {game.Pawns.Crew.Count}.", MessageKind.Good);
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
