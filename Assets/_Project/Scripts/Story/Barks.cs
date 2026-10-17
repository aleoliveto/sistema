using System.Collections.Generic;

namespace OSistema.Story
{
    /// <summary>
    /// Short spoken lines in Neapolitan with English subtitles.
    /// Draft text: to be reviewed by a native speaker, then moved into localization tables.
    /// </summary>
    public static class Barks
    {
        public readonly struct Line
        {
            public readonly string Neapolitan;
            public readonly string English;

            public Line(string neapolitan, string english)
            {
                Neapolitan = neapolitan;
                English = english;
            }
        }

        static readonly Dictionary<string, Line[]> Lines = new Dictionary<string, Line[]>
        {
            ["order_move"] = new[]
            {
                new Line("Vac', Don.", "On my way, Don."),
                new Line("Immediatamente.", "Right away."),
                new Line("Comme vulite vuje.", "As you wish."),
                new Line("Aggio capit, aggio capit.", "Got it, got it."),
            },
            ["drafted"] = new[]
            {
                new Line("Stammo pronte.", "We're ready."),
                new Line("Dicite vuje.", "Your call."),
            },
            ["undrafted"] = new[]
            {
                new Line("Allora me ne vaco.", "Then I'm off."),
                new Line("Finalmente.", "Finally."),
            },
            ["work_start"] = new[]
            {
                new Line("Mo' 'o faccio io.", "I'll do it now."),
                new Line("Jamme, faticammo.", "Come on, let's work."),
            },
            // New in Milestone 1. Drafts, please review.
            ["eat_pizza"] = new[]
            {
                new Line("Chesta è 'na pizza!", "Now that's a pizza!"),
                new Line("Accussì se campa.", "This is how you live."),
            },
            ["hurt"] = new[]
            {
                new Line("Mannaggia!", "Damn it!"),
                new Line("Aggio fatto 'nu guaio...", "I've made a mess of it..."),
            },
            ["sulk"] = new[]
            {
                new Line("Nun me scassate 'o cazz'.", "Leave me the hell alone."),
                new Line("Me ne vaco. Nun ce 'a faccio cchiù.", "I'm leaving. I can't take it anymore."),
            },
            ["work_done"] = new[]
            {
                new Line("Ecco fatto.", "There, done."),
                new Line("Manco 'o Re tene 'na casa accussì.", "Not even the King has a house like this."),
            },
        };

        static readonly System.Random Rng = new System.Random();

        public static Line Pick(string key)
        {
            if (!Lines.TryGetValue(key, out var options) || options.Length == 0) return new Line("...", "...");
            return options[Rng.Next(options.Length)];
        }
    }
}
