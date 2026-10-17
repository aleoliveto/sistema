using System;
using System.Collections.Generic;
using OSistema.Core;
using OSistema.World;
using UnityEngine;

namespace OSistema.Social
{
    /// <summary>Who a message is for: decides which system handles the answer.</summary>
    public enum MsgKind
    {
        Info,
        Favour,          // a neighbour asks for something
        LoanDue,         // a debtor who can't pay
        Dispute,         // two neighbours want the boss to judge
        RatClue,         // gossip that points at someone in the crew
        TestResult,      // the outcome of a false-shipment test
        BugClue,         // a hint that someone is listening
    }

    [Serializable]
    public sealed class PhoneMessage
    {
        public int Id;
        public int Kind;
        public int Day, Hour, Minute;
        public string From;
        public string Neapolitan;     // the line in dialect (draft, for native review)…
        public string English;        // …and its subtitle
        public string Body;
        public List<string> Choices = new List<string>();
        public List<string> Hints = new List<string>();
        public bool Criminal;         // answering "yes" on a tapped line is evidence
        public int Answer = -1;
        public bool Read;
        public long Expires;          // 0 = never
        // Context for the handler
        public int Neighbour, Neighbour2, Pawn, Amount, Request;

        public MsgKind KindOf => (MsgKind)Kind;
        public bool Open(long now) => Answer < 0 && Choices.Count > 0 && (Expires == 0 || now < Expires);
    }

    [Serializable]
    public sealed class PhoneState
    {
        public List<PhoneMessage> Messages = new List<PhoneMessage>();
        public int NextId = 1;
        public long BurnerUntil;
        public bool Bugged;
    }

    /// <summary>
    /// The family's phone: neighbours ask favours, contacts pass on gossip, and the line may be
    /// tapped. Most of the game's small stories arrive here instead of on pop-up cards.
    /// </summary>
    public sealed class Phone
    {
        const int MaxMessages = 60;

        public PhoneState S = new PhoneState();

        /// <summary>Raised when a message arrives (presentation plays the ring).</summary>
        public event Action<PhoneMessage> Ring;

        public IReadOnlyList<PhoneMessage> Messages => S.Messages;

        public int Unread
        {
            get
            {
                int n = 0;
                foreach (var m in S.Messages) if (!m.Read) n++;
                return n;
            }
        }

        public int Waiting(long now)
        {
            int n = 0;
            foreach (var m in S.Messages) if (m.Open(now)) n++;
            return n;
        }

        public bool HasBurner(long now) => now < S.BurnerUntil;
        public bool Tapped(long now) => S.Bugged && !HasBurner(now);

        public PhoneMessage Send(Game game, MsgKind kind, string from, string neapolitan, string english, string body, float hoursToAnswer = 0f)
        {
            var m = new PhoneMessage
            {
                Id = S.NextId++,
                Kind = (int)kind,
                Day = game.Clock.Day,
                Hour = game.Clock.Hour,
                Minute = game.Clock.Minute,
                From = from,
                Neapolitan = neapolitan,
                English = english,
                Body = body,
                Expires = hoursToAnswer > 0f ? game.Ticks.TicksGame + (long)(hoursToAnswer * GameClock.TicksPerHour) : 0,
            };
            S.Messages.Insert(0, m);
            if (S.Messages.Count > MaxMessages) S.Messages.RemoveAt(S.Messages.Count - 1);
            Ring?.Invoke(m);
            return m;
        }

        public void MarkAllRead()
        {
            foreach (var m in S.Messages) m.Read = true;
        }

        public void Answer(PhoneMessage m, int choice, Game game)
        {
            long now = game.Ticks.TicksGame;
            if (m == null || !m.Open(now) || choice < 0 || choice >= m.Choices.Count) return;
            m.Answer = choice;
            m.Read = true;

            // Anything said on a tapped line ends up in a transcript on a magistrate's desk.
            if (m.Criminal && choice == 0 && Tapped(now))
                game.Org.AddEvidence(2f, game);

            switch (m.KindOf)
            {
                case MsgKind.Favour:
                case MsgKind.LoanDue:
                case MsgKind.Dispute:
                    game.Favours.OnAnswer(m, choice, game);
                    break;
                case MsgKind.RatClue:
                case MsgKind.TestResult:
                case MsgKind.BugClue:
                    game.Informants.OnAnswer(m, choice, game);
                    break;
            }
        }

        /// <summary>Hourly: questions nobody answered in time count as a "no".</summary>
        public void Hourly(Game game)
        {
            long now = game.Ticks.TicksGame;
            foreach (var m in S.Messages)
            {
                if (m.Answer >= 0 || m.Expires == 0 || now < m.Expires || m.Choices.Count == 0) continue;
                m.Answer = m.Choices.Count - 1; // the last choice is always the "no" or "ignore"
                if (m.KindOf == MsgKind.Favour || m.KindOf == MsgKind.LoanDue || m.KindOf == MsgKind.Dispute)
                    game.Favours.OnAnswer(m, m.Answer, game, ignored: true);
            }
        }

        public void BuyBurner(Game game, out string error)
        {
            if (!game.Org.Pay(Prices.Burner, game, out error)) return;
            long now = game.Ticks.TicksGame;
            S.BurnerUntil = Math.Max(S.BurnerUntil, now) + GameClock.TicksPerDay * 7;
            game.Log.Add("A new SIM in someone else's name: the family phone is clean for a week.", MessageKind.Good);
        }

        public static string TimeOf(PhoneMessage m) => $"{m.Hour:00}:{m.Minute:00}";
    }
}
