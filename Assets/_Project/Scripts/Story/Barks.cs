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
            // New in Milestone 2. Drafts, please review.
            ["police"] = new[]
            {
                new Line("Arrivano 'e guardie! Fuitevénne!", "Cops are coming! Run!"),
                new Line("Madonna, 'a pula! Jamme, jamme!", "The cops! Go, go!"),
            },
            ["sale"] = new[]
            {
                new Line("Tiè, e vattenne.", "Here, now get lost."),
                new Line("Venticinque. Nun t'aggio visto.", "Twenty-five. I never saw you."),
            },
            ["unpaid"] = new[]
            {
                new Line("Cca nun se magna cchiù, Don.", "We're not eating anymore here, Don."),
                new Line("E 'a mesata? Tengo 'na famiglia.", "And my wages? I've got a family."),
            },
            ["pizzo_ok"] = new[]
            {
                new Line("Bravo. Accussì nun succede niente.", "Good man. That way nothing happens."),
                new Line("'A sicurezza costa, signò.", "Safety costs money, sir."),
            },
            ["pizzo_refused"] = new[]
            {
                new Line("Va buono. Ce vedimmo.", "Fine. We'll be seeing you."),
                new Line("Te ne pentirai.", "You'll regret it."),
            },
            ["freed"] = new[]
            {
                new Line("Finalmente fore.", "Finally out."),
                new Line("Nun aggio parlato. Nun aggio ditto niente.", "I didn't talk. I didn't say a thing."),
            },
            ["recruited"] = new[]
            {
                new Line("Nun ve pentite, Don.", "You won't regret it, Don."),
                new Line("Pronto pe' qualsiasi cosa.", "Ready for anything."),
            },
            ["scare"] = new[]
            {
                new Line("'A prossima vota nun è 'a vetrina.", "Next time it won't be the window."),
                new Line("Mo' capisce comme funziona.", "Now you understand how it works."),
            },
            ["burn"] = new[]
            {
                new Line("Chi nun paga, s'abbrucia.", "Whoever doesn't pay, burns."),
                new Line("Zitto. Jamme, primma ca vene quaccheduno.", "Quiet. Let's go before someone comes."),
            },
            ["fight"] = new[]
            {
                new Line("Spara! Spara!", "Shoot! Shoot!"),
                new Line("Stanno lloco! Abbasciate 'a capa!", "They're over there! Heads down!"),
            },
            ["new_boss"] = new[]
            {
                new Line("Mo' se fa comme dico io.", "Now we do things my way."),
                new Line("'A famiglia nun more.", "The family doesn't die."),
            },
            ["job_done"] = new[]
            {
                new Line("Fatto. Tutto a posto.", "Done. All sorted."),
                new Line("Facile facile.", "Easy as anything."),
            },
            ["debt_paid"] = new[]
            {
                new Line("Vedi ca 'e sorde 'e tenive?", "See? You had the money after all."),
                new Line("'A prossima vota paghe 'a primma vota.", "Next time pay the first time."),
            },
            ["tv"] = new[]
            {
                new Line("Forza Napoli!", "Come on Napoli!"),
                new Line("Ma che ffa 'st'arbitro?!", "What is this referee doing?!"),
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
