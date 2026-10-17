using System;
using System.Collections.Generic;
using OSistema.Core;
using UnityEngine;

namespace OSistema.Social
{
    [Serializable]
    public sealed class NewsItem
    {
        public int Day;
        public int Weight;
        public string Headline;
        public string Text;
    }

    [Serializable]
    public sealed class NewsState
    {
        public List<NewsItem> Items = new List<NewsItem>();
        public int LastEditionDay;
        public int Edition;
    }

    public sealed class Edition
    {
        public int Number, Day;
        public NewsItem Lead;
        public readonly List<NewsItem> Columns = new List<NewsItem>();
        public string Sidebar;     // what the prosecutors are saying
        public string Brief;       // a bit of city life
    }

    /// <summary>
    /// "La Sirena", the city paper. Every Sunday morning it prints what the family did that week,
    /// as the city saw it. Systems report events here with a weight; the heaviest makes the front page.
    /// </summary>
    public sealed class Newspaper
    {
        public const string Name = "LA SIRENA";

        public NewsState S = new NewsState();
        public Edition Latest { get; private set; }

        /// <summary>Raised when an edition comes out (presentation opens the paper).</summary>
        public event Action<Edition> Published;

        static readonly (string headline, string text)[] Fillers =
        {
            ("Napoli win at home, the city celebrates until dawn", "Horns, flags and fireworks from the Vomero to the port. Three people treated for minor burns."),
            ("Potholes: residents plant a tree in one", "In protest at a hole that has been there since spring, a family planted a lemon tree in it. The council says it will act."),
            ("Coffee climbs to €1.30", "Bar owners apologise; customers complain, then order another."),
            ("Strike on the trains, again", "Commuters stranded for the third time this month. A union leader promises \"it is for everyone's good\"."),
            ("Tourists queue three hours for a pizza", "A Japanese couple say it was worth it. The pizzaiolo says it always is."),
            ("Rain floods the underpass", "A scooter rider was seen crossing with water to his knees, still on his phone."),
        };

        public void Report(Game game, int weight, string headline, string text)
        {
            S.Items.Add(new NewsItem { Day = game.Clock.Day, Weight = weight, Headline = headline, Text = text });
            if (S.Items.Count > 40) S.Items.RemoveAt(0);
        }

        public void Hourly(Game game)
        {
            var clock = game.Clock;
            if (!clock.IsPayday || clock.Hour != 9 || S.LastEditionDay == clock.Day) return;
            S.LastEditionDay = clock.Day;
            Publish(game);
        }

        void Publish(Game game)
        {
            var e = new Edition { Number = ++S.Edition, Day = game.Clock.Day };
            var week = S.Items.FindAll(i => game.Clock.Day - i.Day < 7);
            week.Sort((a, b) => b.Weight.CompareTo(a.Weight));

            if (week.Count > 0)
            {
                e.Lead = week[0];
                for (int i = 1; i < week.Count && e.Columns.Count < 3; i++) e.Columns.Add(week[i]);
            }
            var rng = game.Rng;
            var fillers = new List<(string, string)>(Fillers);
            if (e.Lead == null)
            {
                var (h, t) = fillers[rng.Next(fillers.Count)];
                fillers.Remove((h, t));
                e.Lead = new NewsItem { Day = e.Day, Headline = h, Text = t };
            }
            while (e.Columns.Count < 2 && fillers.Count > 0)
            {
                int k = rng.Next(fillers.Count);
                e.Columns.Add(new NewsItem { Day = e.Day, Headline = fillers[k].Item1, Text = fillers[k].Item2 });
                fillers.RemoveAt(k);
            }

            var org = game.Org;
            e.Sidebar = org.Evidence > 70f ? "Prosecutor's office: \"We are very close to the head of this family.\""
                : org.Evidence > 40f ? "Investigators are said to be building a case against a family in the old town."
                : org.Respect > 50f ? "\"Who really runs these streets?\" asks a parish priest in his Sunday homily."
                : "Police report a quiet week in the historic centre.";
            e.Brief = $"Weather: {(rng.NextDouble() < 0.7 ? "sun over the gulf" : "scirocco and grey skies")}. Vesuvius: quiet.";

            S.Items.RemoveAll(i => game.Clock.Day - i.Day >= 7);
            Latest = e;
            Published?.Invoke(e);
        }
    }
}
