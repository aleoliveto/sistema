using System.Collections.Generic;
using System.Linq;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using UnityEngine;

namespace OSistema.Story
{
    /// <summary>
    /// The ties that make a family in the rione: the boss as godfather at a baptism, a marriage that ends a feud,
    /// the kids of the vicoli who want in, the neomelodic singer who wants a patron.
    /// Neapolitan lines are drafts for native review.
    /// </summary>
    public static class LifeCards
    {
        public static IEnumerable<GameEvent> Deck(Game game)
        {
            yield return Comparaggio(game);
            yield return ClanWedding(game);
            yield return BabyParanza(game);
            yield return Neomelodico(game);
        }

        /// <summary>A shopkeeper who pays asks the boss to hold his child at the font: from then on, family.</summary>
        static GameEvent Comparaggio(Game game)
        {
            var org = game.Org;
            if (org.Respect < 15f) return null;
            var shop = org.Businesses.Where(b => (b.Status == BusinessStatus.Paying || b.Status == BusinessStatus.Refusing) && !org.H.godchildShops.Contains(org.Businesses.IndexOf(b)))
                                     .OrderBy(_ => game.Rng.Next()).FirstOrDefault();
            if (shop == null) return null;
            int idx = org.Businesses.IndexOf(shop);
            return new GameEvent
            {
                Id = "comparaggio",
                Title = "Il comparaggio",
                Neapolitan = "Don, ce facite l'onore? Vulimmo a vuje comme compare.",
                English = "Don, will you do us the honour? We want you as godfather.",
                Body = $"The owner of {shop.Name} is baptising his son on Sunday and asks you to be the compare. In the rione, that makes you family: you don't squeeze family, and family doesn't go to the police.",
            }
            .Option("Hold the child at the font (€300 gift)", "Respect +3. The shop will always pay and never talk.", g =>
            {
                g.Stock.Cash -= 300;
                g.Org.Respect = Mathf.Min(100f, g.Org.Respect + 3f);
                g.Org.H.godchildShops.Add(idx);
                shop.Defiance = 0f;
                if (shop.Status == BusinessStatus.Refusing) shop.Status = BusinessStatus.Paying;
                g.Log.Add($"You held the baby at the font. {shop.Name} is family now.", MessageKind.Good);
            }, g => g.Stock.Cash >= 300)
            .Option("Send your regrets", "Respect −1. He'll remember the slight.", g =>
            {
                g.Org.Respect = Mathf.Max(0f, g.Org.Respect - 1f);
                shop.Defiance = Mathf.Min(1f, shop.Defiance + 0.15f);
            });
        }

        /// <summary>A clan not at war offers a marriage: one of your crew and a boss's niece. Peace, at a price.</summary>
        static GameEvent ClanWedding(Game game)
        {
            var clan = game.Clans.Where(c => !c.Destroyed && !c.Allied && c.Hostility < 50f).OrderBy(c => c.Hostility).FirstOrDefault();
            if (clan == null || game.Org.Rank < 2) return null;
            var groom = game.Pawns.Crew.Where(p => p.IsPresent && !p.Controlled && !p.IsBoss && !p.Young).OrderByDescending(p => p.Loyalty).FirstOrDefault();
            if (groom == null) return null;
            const int cost = 1500;
            return new GameEvent
            {
                Id = "clan_wedding",
                Title = $"A wedding with the {clan.Name}",
                Neapolitan = "'Nu matrimonio aggiusta cchiù cose 'e cientu pistole.",
                English = "A wedding fixes more than a hundred pistols.",
                Body = $"{clan.BossName} sends word: his niece and your {groom.FullName}. A church, a reception for four hundred, and two families that won't shoot at each other.",
            }
            .Option($"Give your blessing (the reception: €{cost})", $"An alliance with the {clan.Name}. {groom.Nickname}: loyalty +15.", g =>
            {
                g.Stock.Cash -= cost;
                clan.Allied = true;
                clan.Hostility = 0f;
                groom.Loyalty = Mathf.Min(100f, groom.Loyalty + 15f);
                foreach (var p in g.Pawns.Crew) if (p.IsPresent) p.Mood.AddMemory("wedding", $"{groom.Nickname}'s wedding", 6f, g.Ticks.TicksGame, GameClock.TicksPerDay * 2);
                g.Log.Add($"{groom.FullName} married into the {clan.Name}. The two families are bound now.", MessageKind.Good);
                g.Newspaper.Report(g, 3, "A society wedding in the old town", "Four hundred guests, a neomelodic singer and a procession of cars. Investigators took note of who sat at which table.");
                g.Org.AddEvidence(2f, g);
            }, g => g.Stock.Cash >= cost)
            .Option("Our people marry whom they like", $"Respect +1. The {clan.Name}: hostility +10.", g =>
            {
                g.Org.Respect = Mathf.Min(100f, g.Org.Respect + 1f);
                clan.Hostility = Mathf.Min(100f, clan.Hostility + 10f);
            });
        }

        /// <summary>The baby paranza: kids from the vicoli on scooters who want guns and a name. Cheap, fearless, and a scandal when caught.</summary>
        static GameEvent BabyParanza(Game game)
        {
            var org = game.Org;
            if (org.Rank < 1 || game.Pawns.Crew.Count(p => !p.Dead && !p.Departed) >= org.MaxCrewForRank - 1) return null;
            if (game.Pawns.Crew.Count(p => p.Young && !p.Dead && !p.Departed) >= 3) return null;
            return new GameEvent
            {
                Id = "baby_paranza",
                Title = "The kids from the vicoli",
                Neapolitan = "Nuje nun tenimmo paura 'e niente, Don.",
                English = "We're not afraid of anything, Don.",
                Body = "Three boys, fifteen or sixteen, waiting by the basso on one scooter. They've watched the piazza for months. They want in, they want a gun, and they want everyone to know their names.",
            }
            .Option("Take them in", "Up to three new crew, cheap (€40 a week) and fearless, all hotheads. If they're caught, the papers go wild: evidence +6.", g =>
            {
                int taken = 0;
                for (int i = 0; i < 3; i++)
                {
                    var kid = g.Org.Recruit(g, out _, free: true);
                    if (kid == null) break;
                    kid.Young = true;
                    kid.Wage = 40;
                    kid.Loyalty = 70f;
                    kid.ShootingSkill = g.Rng.Next(3, 7);
                    kid.AwarenessSkill = g.Rng.Next(1, 4);
                    kid.Nickname = new[] { "'o Piccirillo", "'o Muschillo", "'o Nennillo", "'o Criaturo" }[g.Rng.Next(4)];
                    taken++;
                }
                g.Log.Add(taken > 0 ? $"{taken} boys from the vicoli join the family." : "No room for them in the family.", taken > 0 ? MessageKind.Good : MessageKind.Info);
            })
            .Option("Send them home to their mothers", "Respect +1 with the old people of the rione.", g =>
                g.Org.Respect = Mathf.Min(100f, g.Org.Respect + 1f));
        }

        /// <summary>A neomelodic singer who wants a patron, and offers a song in return.</summary>
        static GameEvent Neomelodico(Game game)
        {
            if (game.Org.Respect < 25f) return null;
            string singer = new[] { "Gigino 'a Voce", "Tony Melodia", "Ciro Passione" }[game.Clock.Day % 3];
            const int fee = 1200;
            return new GameEvent
            {
                Id = "neomelodico",
                Title = $"{singer} wants a patron",
                Neapolitan = "Pe' vuje scrivo 'na canzone ca 'a cantano tutte 'e vicoli.",
                English = "For you I'll write a song the whole of the vicoli will sing.",
                Body = $"{singer} plays weddings, baptisms and the festa. He wants you to pay for a concert in the piazza, and in return he'll dedicate a song: about a man of honour, with your name between the lines.",
            }
            .Option($"Pay for the concert (€{fee})", "Respect +6, the crew love it (mood +5). The video goes online: evidence +2.", g =>
            {
                g.Stock.Cash -= fee;
                g.Org.Respect = Mathf.Min(100f, g.Org.Respect + 6f);
                g.Org.AddEvidence(2f, g);
                foreach (var p in g.Pawns.Crew) if (p.IsPresent) p.Mood.AddMemory("concert", $"{singer} sang for the family", 5f, g.Ticks.TicksGame, GameClock.TicksPerDay * 2);
                g.Log.Add($"{singer} sang in the piazza until two. The whole rione heard your song.", MessageKind.Good);
            }, g => g.Stock.Cash >= fee)
            .Option("Not this year", "Nothing happens.", g => { });
        }
    }
}
