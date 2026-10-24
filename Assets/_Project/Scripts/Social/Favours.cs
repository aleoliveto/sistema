using System;
using System.Collections.Generic;
using OSistema.Core;
using OSistema.Crime;
using OSistema.World;
using UnityEngine;

namespace OSistema.Social
{
    [Serializable]
    public sealed class Neighbour
    {
        public int Id;
        public string Name;
        public string Trade;
        public int Zone;
        public int Owes;          // favours they owe the family
        public int Grudge;        // slights they remember
        public int Loan;          // money they owe, interest included
        public int LoanDueDay;
        public bool Witness;      // talking to the police
    }

    [Serializable]
    public sealed class FavourState
    {
        public List<Neighbour> Neighbours = new List<Neighbour>();
        public long StashHiddenUntil;
        public int RequestsDay, RequestsToday;
    }

    public enum Request { SonJob, Loan, Bully, Operation, Funeral }

    public enum CallIn { Alibi, Lookout, Stash, AskAround }

    /// <summary>
    /// The rione's economy of favours. Neighbours come to the boss for what the state doesn't give
    /// them: work, money, justice, a hospital bed. Every yes is a debt the family can call in later;
    /// every no is remembered, and a neighbour with enough grudges talks to the police.
    /// </summary>
    public sealed class Favours
    {
        public FavourState S = new FavourState();

        static readonly string[] Women = { "Donna Carmela", "Donna Assunta", "Zì Nunzia", "Donna Concetta", "Titina", "Donna Rosaria", "Immacolata", "Zì Filomena" };
        static readonly string[] Men = { "Zì Peppe", "Don Ciccio", "Tonino", "Mast'Aniello", "Don Raffaele", "Biagio", "Zì Carmine", "Nicola" };
        static readonly string[] Trades =
        {
            "the fruit seller", "the barber", "the tobacconist", "the cook at the trattoria", "the porter of the palazzo",
            "the fishmonger", "the seamstress", "the mechanic", "the pensioner on the first floor", "the baker",
            "the bar owner", "the florist",
        };
        static readonly string[] DisputeOver =
        {
            "a parking space in the vicolo", "a wall between two balconies", "their late mother's flat",
            "the pitch for a market stall", "a debt from a card game", "water leaking through a ceiling",
        };

        public Neighbour ById(int id) => S.Neighbours.Find(n => n.Id == id);

        public static bool IsWoman(string name) => Array.IndexOf(Women, name) >= 0;

        public int TotalOwed
        {
            get
            {
                int t = 0;
                foreach (var n in S.Neighbours) t += n.Owes;
                return t;
            }
        }

        public bool StashHidden(Game game) => game.Ticks.TicksGame < S.StashHiddenUntil;

        public void Setup(Game game, System.Random rng)
        {
            S.Neighbours.Clear();
            var zones = new List<int>();
            foreach (var z in game.Territory.Zones)
                if (game.Org.CanExpandInto(z, game)) zones.Add(z.Id);
            if (zones.Count == 0) zones.Add(0);

            var used = new HashSet<string>();
            var trades = new List<string>(Trades);
            for (int i = 0; i < 10; i++)
            {
                string name;
                do name = rng.NextDouble() < 0.55 ? Women[rng.Next(Women.Length)] : Men[rng.Next(Men.Length)];
                while (!used.Add(name) && used.Count < Women.Length + Men.Length);
                int t = rng.Next(trades.Count);
                S.Neighbours.Add(new Neighbour { Id = i + 1, Name = name, Trade = trades[t], Zone = zones[rng.Next(zones.Count)] });
                trades.RemoveAt(t);
                if (trades.Count == 0) trades.AddRange(Trades);
            }
        }

        string ZoneName(Neighbour n, Game game) =>
            n.Zone >= 0 && n.Zone < game.Territory.Zones.Count ? game.Territory.Zones[n.Zone].Name : "the rione";

        // ---------- Time ----------

        public void Hourly(Game game)
        {
            var clock = game.Clock;
            if (S.RequestsDay != clock.Day)
            {
                S.RequestsDay = clock.Day;
                S.RequestsToday = 0;
                DailyGrudges(game);
            }
            if (clock.Hour == 10) CheckLoans(game);
            // Neighbours don't ask a nobody for anything: favours start once the family has a name.
            if (game.Org.Respect < 12f) return;
            int maxToday = game.Org.Respect < 25f ? 1 : 2;
            if (clock.Hour >= 9 && clock.Hour <= 20 && S.RequestsToday < maxToday && game.Rng.NextDouble() < 0.06)
            {
                S.RequestsToday++;
                if (game.Rng.NextDouble() < 0.18) NewDispute(game);
                else NewRequest(game);
            }
        }

        bool Busy(Neighbour n, Game game)
        {
            long now = game.Ticks.TicksGame;
            foreach (var m in game.Phone.Messages)
                if (m.Open(now) && (m.Neighbour == n.Id || m.Neighbour2 == n.Id)) return true;
            return false;
        }

        Neighbour PickFree(Game game, Neighbour except = null)
        {
            var list = S.Neighbours.FindAll(n => n != except && !n.Witness && !Busy(n, game));
            return list.Count == 0 ? null : list[game.Rng.Next(list.Count)];
        }

        void NewRequest(Game game)
        {
            var n = PickFree(game);
            if (n == null) return;
            var rng = game.Rng;
            // Money (loans, the clinic, a funeral) is asked of a Clan; a smaller name gets asked for help, not cash.
            var kind = game.Org.Rank >= 2 ? (Request)rng.Next(5) : rng.Next(2) == 0 ? Request.SonJob : Request.Bully;
            if (kind == Request.Loan && n.Loan > 0) kind = Request.Funeral;
            PhoneMessage m;
            switch (kind)
            {
                case Request.SonJob:
                    m = game.Phone.Send(game, MsgKind.Favour, n.Name,
                        "Don, 'o guaglione mio nun tene niente 'a fà. Pigliatevillo cu vuje.",
                        "Don, my boy has nothing to do. Take him with you.",
                        $"{n.Name}, {n.Trade}, has a seventeen-year-old who hangs around the vicolo all day. A job with the family would keep him off the street, in a manner of speaking.", 10f);
                    m.Choices.Add("Take him on");
                    m.Hints.Add($"A new guaglione joins, no signing bonus. {n.Name} owes you 2.");
                    m.Choices.Add("Not now");
                    m.Hints.Add($"{n.Name} is disappointed.");
                    break;
                case Request.Loan:
                    int amount = 200 + rng.Next(13) * 50;
                    m = game.Phone.Send(game, MsgKind.Favour, n.Name,
                        "Don, me servono quatte sorde pe' fernì 'o mese. V''e torno, ve giuro.",
                        "Don, I need a bit of money to get to the end of the month. I'll pay you back, I swear.",
                        $"{n.Name}, {n.Trade}, asks to borrow €{amount}. The banks said no. The family's rate is 20% a week.", 8f);
                    m.Amount = amount;
                    m.Criminal = true;
                    m.Choices.Add($"Lend €{amount}");
                    m.Hints.Add($"Back in 7 days with interest: €{Interest(amount)}. {n.Name} owes you 1.");
                    m.Choices.Add("Refuse");
                    m.Hints.Add($"{n.Name} holds a small grudge.");
                    break;
                case Request.Bully:
                    m = game.Phone.Send(game, MsgKind.Favour, n.Name,
                        "Ce sta uno 'e fore ca dà fastidio a figliema. Nisciuno fa niente.",
                        "There's an outsider bothering my daughter. Nobody does anything.",
                        $"{n.Name}, {n.Trade}, says a man from outside the rione follows the daughter home every evening. The police took a statement and did nothing.", 8f);
                    m.Criminal = true;
                    m.Choices.Add("Send someone to talk to him");
                    m.Hints.Add($"Respect +3, heat +6 in {ZoneName(n, game)}. {n.Name} owes you 2.");
                    m.Choices.Add("Not our business");
                    m.Hints.Add($"Respect −1. {n.Name} remembers.");
                    break;
                case Request.Operation:
                    m = game.Phone.Send(game, MsgKind.Favour, n.Name,
                        "Mammà s'ha da operà, e 'a lista d'attesa è 'e otto mise.",
                        "Mum needs an operation, and the waiting list is eight months.",
                        $"{n.Name}, {n.Trade}, can't afford the private clinic. €400 would get the old woman seen this week.", 12f);
                    m.Amount = 400;
                    m.Choices.Add("Pay the clinic (€400)");
                    m.Hints.Add($"{n.Name} owes you 3. Respect +1.");
                    m.Choices.Add("I can't help");
                    m.Hints.Add("They'll wait the eight months.");
                    break;
                default:
                    m = game.Phone.Send(game, MsgKind.Favour, n.Name,
                        "Nun tenimmo manco 'e sorde p' 'o funerale 'e papà.",
                        "We don't even have the money for dad's funeral.",
                        $"{n.Name}'s father died last night. A proper funeral, with the band and the flowers, costs €250.", 6f);
                    m.Amount = 250;
                    m.Choices.Add("Pay for the funeral (€250)");
                    m.Hints.Add($"Respect +2, {n.Name} owes you 2. Everyone will see the wreath with the family's name.");
                    m.Choices.Add("Send condolences");
                    m.Hints.Add("Nothing more. Some will notice.");
                    break;
            }
            m.Neighbour = n.Id;
            m.Request = (int)kind;
        }

        void NewDispute(Game game)
        {
            var a = PickFree(game);
            var b = a != null ? PickFree(game, a) : null;
            if (a == null || b == null) return;
            string over = DisputeOver[game.Rng.Next(DisputeOver.Length)];
            var m = game.Phone.Send(game, MsgKind.Dispute, $"{a.Name} & {b.Name}",
                "Don, decidite vuje. Nuje ce fidammo sulo 'e vuje.",
                "Don, you decide. We only trust you.",
                $"{a.Name} ({a.Trade}) and {b.Name} ({b.Trade}) are fighting over {over}. They came to the boss, not the courts.", 10f);
            m.Neighbour = a.Id;
            m.Neighbour2 = b.Id;
            m.Choices.Add($"Side with {a.Name}");
            m.Hints.Add($"{a.Name} owes you 2, {b.Name} holds a grudge. Respect +2.");
            m.Choices.Add($"Side with {b.Name}");
            m.Hints.Add($"{b.Name} owes you 2, {a.Name} holds a grudge. Respect +2.");
            m.Choices.Add("Stay out of it");
            m.Hints.Add("Respect −2: a boss who doesn't judge isn't a boss.");
        }

        static int Interest(int amount) => Mathf.RoundToInt(amount * 1.2f / 10f) * 10;

        void CheckLoans(Game game)
        {
            foreach (var n in S.Neighbours)
            {
                if (n.Loan <= 0 || game.Clock.Day < n.LoanDueDay || Busy(n, game)) continue;
                double chance = 0.55 + 0.05 * n.Owes - 0.1 * n.Grudge;
                if (game.Rng.NextDouble() < chance)
                {
                    game.Stock.Cash += n.Loan;
                    game.Log.Add($"{n.Name} paid back the loan: €{n.Loan}, in an envelope.", MessageKind.Good);
                    n.Loan = 0;
                    continue;
                }
                var m = game.Phone.Send(game, MsgKind.LoanDue, n.Name,
                    "Don, perdonateme... nun ce 'a faccio. Dateme n'ata semmana.",
                    "Don, forgive me... I can't make it. Give me another week.",
                    $"{n.Name} ({n.Trade}) owes €{n.Loan} and can't pay.", 12f);
                m.Neighbour = n.Id;
                m.Criminal = true;
                m.Choices.Add("Another week, interest doubles");
                m.Hints.Add($"Due in 7 days: €{Interest(n.Loan)}.");
                m.Choices.Add("Send the collector");
                m.Hints.Add($"Take what they have (about half). Respect +2, grudge +2, heat in {ZoneName(n, game)}.");
                m.Choices.Add("Forgive the debt");
                m.Hints.Add($"Lose the money. {n.Name} owes you 3: a debt worth more than cash.");
            }
        }

        void DailyGrudges(Game game)
        {
            foreach (var n in S.Neighbours)
            {
                if (n.Witness || n.Grudge < 3 || game.Rng.NextDouble() > 0.12 * (n.Grudge - 2)) continue;
                n.Witness = true;
                game.Org.AddEvidence(4f, game);
                var m = game.Phone.Send(game, MsgKind.Info, "Zì Nunziatina",
                    $"Aggio visto a {n.Name} trasì 'int' 'a Questura. E nun era p' 'o passaporto.",
                    $"I saw {n.Name} go into the Questura. And it wasn't for a passport.",
                    $"{n.Name} ({n.Trade}) has had enough of the family and is talking to the police. Evidence is piling up. Buy their silence from the Favours tab, or deal with it.");
                m.Neighbour = n.Id;
            }
            foreach (var n in S.Neighbours)
                if (n.Witness) game.Org.AddEvidence(1f, game);
        }

        // ---------- Answers ----------

        public void OnAnswer(PhoneMessage m, int choice, Game game, bool ignored = false)
        {
            var n = ById(m.Neighbour);
            if (n == null) return;
            var org = game.Org;

            if (m.KindOf == MsgKind.Dispute)
            {
                var b = ById(m.Neighbour2);
                if (choice == 2 || b == null)
                {
                    Respect(game, -2f);
                    game.Log.Add($"The boss stayed out of {n.Name} and {b?.Name}'s quarrel. People talk.", MessageKind.Bad);
                    return;
                }
                var winner = choice == 0 ? n : b;
                var loser = choice == 0 ? b : n;
                winner.Owes += 2;
                loser.Grudge += 1;
                Respect(game, 2f);
                game.Log.Add($"The boss ruled for {winner.Name}. {loser.Name} accepted it, through gritted teeth.");
                return;
            }

            if (m.KindOf == MsgKind.LoanDue)
            {
                if (ignored) choice = 0;
                m.Answer = choice;
                switch (choice)
                {
                    case 0:
                        n.Loan = Interest(n.Loan);
                        n.LoanDueDay = game.Clock.Day + 7;
                        game.Log.Add($"{n.Name} has another week. The debt is now €{n.Loan}.");
                        break;
                    case 1:
                        int got = n.Loan / 2;
                        game.Stock.Cash += got;
                        n.Loan = 0;
                        n.Grudge += 2;
                        Respect(game, 2f);
                        game.Territory.AddHeat(ZoneCell(n, game), 6f);
                        game.Log.Add($"The collector took €{got} from {n.Name}: the till, the gold chain, the scooter keys.", MessageKind.Bad);
                        break;
                    default:
                        n.Loan = 0;
                        n.Owes += 3;
                        game.Log.Add($"The boss forgave {n.Name}'s debt. They won't forget it.", MessageKind.Good);
                        break;
                }
                return;
            }

            // Favour requests: choice 0 is yes, the last choice is no.
            bool yes = choice == 0 && !ignored;
            var kind = (Request)m.Request;
            if (!yes)
            {
                n.Grudge += kind == Request.Operation || kind == Request.Funeral ? 0 : 1;
                if (kind == Request.Bully) Respect(game, -1f);
                if (ignored) game.Log.Add($"Nobody answered {n.Name}. They took it as a no.");
                return;
            }

            switch (kind)
            {
                case Request.SonJob:
                    if (org.Recruit(game, out var error, free: true) == null)
                    {
                        game.Log.Add(error, MessageKind.Bad);
                        return;
                    }
                    n.Owes += 2;
                    break;
                case Request.Loan:
                    if (!org.Pay(m.Amount, game, out error))
                    {
                        game.Log.Add(error, MessageKind.Bad);
                        return;
                    }
                    n.Loan = Interest(m.Amount);
                    n.LoanDueDay = game.Clock.Day + 7;
                    n.Owes += 1;
                    game.Log.Add($"€{m.Amount} lent to {n.Name}. €{n.Loan} due on day {n.LoanDueDay}.");
                    break;
                case Request.Bully:
                    n.Owes += 2;
                    Respect(game, 3f);
                    game.Territory.AddHeat(ZoneCell(n, game), 6f);
                    game.Log.Add($"Two of the boys had a word with the man bothering {n.Name}'s daughter. He won't be back.");
                    game.Newspaper.Report(game, 1, $"Man beaten in {ZoneName(n, game)}", "Witnesses say they saw nothing. The victim says he fell.");
                    break;
                case Request.Operation:
                case Request.Funeral:
                    if (!org.Pay(m.Amount, game, out error))
                    {
                        game.Log.Add(error, MessageKind.Bad);
                        return;
                    }
                    n.Owes += kind == Request.Operation ? 3 : 2;
                    Respect(game, kind == Request.Operation ? 1f : 2f);
                    game.Log.Add(kind == Request.Operation
                        ? $"{n.Name}'s mother has a bed at the clinic. The family paid."
                        : $"A white wreath with the family's name led {n.Name}'s father to the cemetery.", MessageKind.Good);
                    break;
            }
        }

        static void Respect(Game game, float delta) => game.Org.Respect = Mathf.Clamp(game.Org.Respect + delta, 0f, 100f);

        static Vector2Int ZoneCell(Neighbour n, Game game) =>
            n.Zone >= 0 && n.Zone < game.Territory.Zones.Count
                ? Vector2Int.RoundToInt(game.Territory.Zones[n.Zone].Rect.center)
                : Vector2Int.RoundToInt(game.PlayerBase.center);

        // ---------- Calling in ----------

        public static int Cost(CallIn c) => c switch
        {
            CallIn.Lookout => 1,
            _ => 2,
        };

        public static string Label(CallIn c) => c switch
        {
            CallIn.Alibi => "Alibi",
            CallIn.Lookout => "Watch the street",
            CallIn.Stash => "Hide the stash",
            _ => "Ask around",
        };

        public static string Describe(CallIn c) => c switch
        {
            CallIn.Alibi => "They swear the boss was at their table all evening. Evidence −8.",
            CallIn.Lookout => "They watch the street and call when a car without plates stops. Heat −12 in their rione.",
            CallIn.Stash => "Product hidden in their cellar: police raids seize nothing for 3 days.",
            _ => "They listen at the bar and the market: is anyone in the crew talking?",
        };

        public bool Call(Neighbour n, CallIn c, Game game, out string error)
        {
            error = null;
            if (n.Witness) error = $"{n.Name} is talking to the police. They won't do the family any favours.";
            else if (n.Owes < Cost(c)) error = $"{n.Name} doesn't owe the family enough for that.";
            if (error != null) return false;
            n.Owes -= Cost(c);
            switch (c)
            {
                case CallIn.Alibi:
                    game.Org.Evidence = Mathf.Max(0f, game.Org.Evidence - 8f);
                    game.Log.Add($"{n.Name} gave the boss an alibi. The investigators lose a thread.", MessageKind.Good);
                    break;
                case CallIn.Lookout:
                    if (n.Zone >= 0 && n.Zone < game.Territory.Zones.Count)
                    {
                        var z = game.Territory.Zones[n.Zone];
                        z.Heat = Mathf.Max(0f, z.Heat - 12f);
                    }
                    game.Log.Add($"{n.Name} keeps watch over {ZoneName(n, game)} from the balcony.", MessageKind.Good);
                    break;
                case CallIn.Stash:
                    S.StashHiddenUntil = game.Ticks.TicksGame + GameClock.TicksPerDay * 3;
                    game.Log.Add($"The product is in {n.Name}'s cellar, behind the tomato jars.", MessageKind.Good);
                    break;
                default:
                    game.Informants.AskAround(n, game);
                    break;
            }
            return true;
        }

        public bool BuySilence(Neighbour n, Game game, out string error)
        {
            error = null;
            if (!n.Witness) return false;
            if (!game.Org.Pay(500, game, out error)) return false;
            n.Witness = false;
            n.Grudge = 0;
            game.Org.Evidence = Mathf.Max(0f, game.Org.Evidence - 4f);
            game.Log.Add($"{n.Name} remembered nothing at the second interview. €500 helped.", MessageKind.Good);
            return true;
        }
    }
}
