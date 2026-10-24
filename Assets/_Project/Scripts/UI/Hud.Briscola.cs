using System.Collections.Generic;
using System.Linq;
using OSistema.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace OSistema.UI
{
    /// <summary>
    /// Briscola at the bar, against one of the old men, for money. Forty Neapolitan cards in four suits; the last card
    /// of the deck names the trump (la briscola). Follow-suit isn't required: the higher card of the suit led takes
    /// the trick, unless a trump is played. Aces 11, threes 10, kings 4, horses 3, knaves 2: over 60 of 120 wins.
    /// </summary>
    public sealed partial class Hud
    {
        struct Card
        {
            public int Suit;   // 0 denari, 1 coppe, 2 spade, 3 bastoni
            public int Rank;   // 1..10: 8 fante, 9 cavallo, 10 re
            public int Points => Rank == 1 ? 11 : Rank == 3 ? 10 : Rank == 10 ? 4 : Rank == 9 ? 3 : Rank == 8 ? 2 : 0;
            public int Strength => Rank == 1 ? 10 : Rank == 3 ? 9 : Rank == 10 ? 8 : Rank == 9 ? 7 : Rank == 8 ? 6 : Rank - 2;   // 7 → 5 … 2 → 0
        }

        static readonly string[] SuitNames = { "Denari", "Coppe", "Spade", "Bastoni" };
        static readonly string[] SuitColours = { "#E8C26A", "#E07060", "#8FB0E0", "#8FD18A" };

        static string CardName(Card c)
        {
            string r = c.Rank switch { 1 => "Asso", 8 => "Fante", 9 => "Cavallo", 10 => "Re", _ => c.Rank.ToString() };
            return $"<color={SuitColours[c.Suit]}>{r} di {SuitNames[c.Suit]}</color>";
        }

        // The game in progress.
        List<Card> bDeck, bMine, bTheirs;
        Card bTrumpCard;
        Card? bMyPlay, bTheirPlay;
        bool bMyLead;
        int bMyPoints, bTheirPoints, bStake;
        string bOpponent, bLast;
        VisualElement bBody;

        public void OpenBriscola()
        {
            bOpponent = new[] { "Zì Ciccio", "Don Antonio", "'O Professore", "Zì Carmela" }[Random.Range(0, 4)];
            var panel = OpenMinigame("BRISCOLA AL BAR", $"{bOpponent} shuffles the deck. \"Quanto jucammo?\" How much are we playing for?");
            bBody = Ui.Box("col").Into(panel);
            bDeck = null;
            DrawBriscola();
        }

        void StartBriscola(int stake)
        {
            if (game.Stock.Cash < stake) { game.Log.Add("Not enough cash on you for that stake.", MessageKind.Bad); return; }
            bStake = stake;
            bDeck = new List<Card>();
            for (int s = 0; s < 4; s++) for (int r = 1; r <= 10; r++) bDeck.Add(new Card { Suit = s, Rank = r });
            bDeck = bDeck.OrderBy(_ => game.Rng.Next()).ToList();
            bTrumpCard = bDeck[0];   // turned face up under the deck: drawn last
            bMine = new List<Card>();
            bTheirs = new List<Card>();
            for (int i = 0; i < 3; i++) { bMine.Add(Draw()); bTheirs.Add(Draw()); }
            bMyPoints = bTheirPoints = 0;
            bMyPlay = bTheirPlay = null;
            bMyLead = true;
            bLast = "You lead.";
            DrawBriscola();
        }

        Card Draw()
        {
            var c = bDeck[bDeck.Count - 1];
            bDeck.RemoveAt(bDeck.Count - 1);
            return c;
        }

        /// <summary>Does b beat a, when a was led?</summary>
        bool Beats(Card a, Card b)
        {
            int trump = bTrumpCard.Suit;
            if (b.Suit == a.Suit) return b.Strength > a.Strength;
            return b.Suit == trump;
        }

        Card TheirChoice()
        {
            int trump = bTrumpCard.Suit;
            if (bMyPlay == null)
            {
                // Leading: the cheapest card, keeping trumps back.
                return bTheirs.OrderBy(c => c.Suit == trump ? 1 : 0).ThenBy(c => c.Points).ThenBy(c => c.Strength).First();
            }
            var led = bMyPlay.Value;
            var winners = bTheirs.Where(c => Beats(led, c)).ToList();
            // Worth taking: take it as cheaply as possible. Not worth it: throw away the least.
            if (winners.Count > 0 && (led.Points >= 2 || winners.Any(c => c.Suit != trump)))
                return winners.OrderBy(c => c.Suit == trump ? 1 : 0).ThenBy(c => c.Strength).First();
            return bTheirs.OrderBy(c => c.Points).ThenBy(c => c.Suit == trump ? 1 : 0).ThenBy(c => c.Strength).First();
        }

        void PlayMine(int i)
        {
            if (bMyPlay != null || i >= bMine.Count) return;
            bMyPlay = bMine[i];
            bMine.RemoveAt(i);
            if (bTheirPlay == null)
            {
                var c = TheirChoice();
                bTheirs.Remove(c);
                bTheirPlay = c;
            }
            ResolveTrick();
        }

        void ResolveTrick()
        {
            var mine = bMyPlay.Value;
            var theirs = bTheirPlay.Value;
            bool iWin = bMyLead ? !Beats(mine, theirs) : Beats(theirs, mine);
            int pts = mine.Points + theirs.Points;
            if (iWin) bMyPoints += pts; else bTheirPoints += pts;
            bLast = $"You: {CardName(mine)} · {bOpponent}: {CardName(theirs)} → {(iWin ? "<b>yours</b>" : $"<b>{bOpponent}'s</b>")}{(pts > 0 ? $" (+{pts})" : "")}";
            bMyLead = iWin;
            bMyPlay = bTheirPlay = null;
            // The winner draws first.
            if (bDeck.Count > 0)
            {
                if (iWin) { bMine.Add(Draw()); bTheirs.Add(Draw()); }
                else { bTheirs.Add(Draw()); bMine.Add(Draw()); }
            }
            if (bMine.Count == 0 && bTheirs.Count == 0) { EndBriscola(); return; }
            // They lead the next one: their card is on the table waiting for yours.
            if (!bMyLead)
            {
                var c = TheirChoice();
                bTheirs.Remove(c);
                bTheirPlay = c;
            }
            DrawBriscola();
        }

        void EndBriscola()
        {
            string result;
            if (bMyPoints > 60)
            {
                game.Stock.Cash += bStake;
                game.Org.Respect = Mathf.Min(100f, game.Org.Respect + 0.3f);
                result = $"<color=#8FD18A>You win, {bMyPoints} to {bTheirPoints}.</color> {bOpponent} pays €{bStake} and calls for another coffee.";
            }
            else if (bMyPoints < 60)
            {
                game.Stock.Cash -= bStake;
                result = $"<color=#F07A6E>{bOpponent} wins, {bTheirPoints} to {bMyPoints}.</color> €{bStake} on the table.";
            }
            else result = "Sixty each: a draw. Nobody pays.";
            game.Log.Add(System.Text.RegularExpressions.Regex.Replace(result, "<.*?>", ""), bMyPoints > 60 ? MessageKind.Good : MessageKind.Info);
            bDeck = null;
            bLast = result;
            DrawBriscola();
        }

        void DrawBriscola()
        {
            if (bBody == null) return;
            bBody.Clear();
            if (bDeck == null)
            {
                if (!string.IsNullOrEmpty(bLast)) Ui.Text(bLast, bBody, "wrap");
                var stakes = Ui.Row(bBody);
                foreach (int s in new[] { 10, 50, 200 })
                {
                    int stake = s;
                    var b = Ui.Btn($"Play for €{stake}", () => StartBriscola(stake), stakes, "btn--small");
                    b.SetEnabled(game.Stock.Cash >= stake);
                }
                Ui.Text("Aces 11, threes 10, kings 4, horses 3, knaves 2. Over 60 of the 120 points wins. The card under the deck is the briscola: any trump beats any other suit.", bBody, "small", "muted", "wrap");
                return;
            }
            Ui.Text($"Briscola: {CardName(bTrumpCard)} · deck {bDeck.Count} · you {bMyPoints}, {bOpponent} {bTheirPoints} · stake €{bStake}", bBody, "small");
            Ui.Text(bLast ?? "", bBody, "small", "wrap");
            Ui.Text(bTheirPlay != null ? $"{bOpponent} plays {CardName(bTheirPlay.Value)}. Your answer:" : "Your lead:", bBody, "section");
            var hand = Ui.Row(bBody);
            for (int i = 0; i < bMine.Count; i++)
            {
                int idx = i;
                var c = bMine[i];
                Ui.Btn($"{CardName(c)}{(c.Points > 0 ? $"  ({c.Points})" : "")}", () => PlayMine(idx), hand, "btn--small");
            }
        }
    }
}
