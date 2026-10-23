using System.Collections.Generic;
using System.Linq;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using OSistema.World;
using UnityEngine;

namespace OSistema.Story
{
    /// <summary>
    /// The deck. Each card returns null when it doesn't fit the current situation.
    /// Neapolitan lines are drafts for native review.
    /// </summary>
    public static class EventCards
    {
        public static IEnumerable<GameEvent> RandomDeck(Game game)
        {
            yield return MotherPlea(game);
            yield return PriestFesta(game);
            yield return Journalist(game);
            yield return Undercover(game);
            yield return CrewWantsOut(game);
            yield return Wedding(game);
            yield return SupplierDeal(game);
            yield return RivalTruce(game);
        }

        static IEnumerable<Pawn> PresentCrew(Game game) => game.Pawns.Crew.Where(p => p.IsPresent);

        static bool Cash(Game game, int amount) => game.Stock.Cash >= amount;

        static void Pay(Game game, int amount) => game.Stock.Cash -= amount;

        static void AdjustRespect(Game game, float delta) =>
            game.Org.Respect = Mathf.Clamp(game.Org.Respect + delta, 0f, 100f);

        // ---------- Random cards ----------

        static GameEvent MotherPlea(Game game)
        {
            var pusher = PresentCrew(game).FirstOrDefault(p => p.Role == CrewRole.Pusher && !p.IsBoss);
            if (pusher == null) return null;
            return new GameEvent
            {
                Id = "mother_plea",
                Title = "A mother at the door",
                Neapolitan = "Don, è 'nu guaglione. Lassatelo sta'.",
                English = "Don, he's just a boy. Leave him be.",
                Body = $"{pusher.FirstName}'s mother is at the door of the basso, rosary in hand. She begs you to take her son off the piazza before he ends up dead or inside.",
            }
            .Option("Take him off the piazza", "He goes back to general work. Respect −2, his loyalty +10.", g =>
            {
                pusher.Role = CrewRole.General;
                pusher.Loyalty = Mathf.Min(100f, pusher.Loyalty + 10f);
                AdjustRespect(g, -2f);
            })
            .Option("Tell her he's a man now", "Respect +2. He saw his mother cry: mood −5.", g =>
            {
                AdjustRespect(g, 2f);
                pusher.Mood.AddMemory("mother_cried", "My mother cried for me", -5f, g.Ticks.TicksGame, GameClock.TicksPerDay * 2);
            });
        }

        static GameEvent PriestFesta(Game game)
        {
            return new GameEvent
            {
                Id = "priest_festa",
                Title = "The parish priest",
                Neapolitan = "'A festa d''a Madonna è pe' tutte quante.",
                English = "The Madonna's feast is for everyone.",
                Body = "Don Peppino from the parish asks for a contribution to the procession. The whole rione will see whose name is on the banner.",
            }
            .Option("Donate €300", "Respect +5. Heat −3 in every zone.", g =>
            {
                Pay(g, 300);
                AdjustRespect(g, 5f);
                foreach (var z in g.Territory.Zones) z.Heat = Mathf.Max(0f, z.Heat - 3f);
            }, g => Cash(g, 300))
            .Option("Pay for the fireworks too (€700)", "Respect +12. The whole rione cheers your name.", g =>
            {
                Pay(g, 700);
                AdjustRespect(g, 12f);
            }, g => Cash(g, 700))
            .Option("Send him away", "Respect −3. People talk.", g => AdjustRespect(g, -3f));
        }

        static GameEvent Journalist(Game game)
        {
            if (game.Org.Squares.Count == 0) return null;
            return new GameEvent
            {
                Id = "journalist",
                Title = "A journalist in the rione",
                Neapolitan = "Chillo fa troppe dumande.",
                English = "That one asks too many questions.",
                Body = "A reporter from a national paper has been asking the shopkeepers about your piazza and taking photos from a car.",
            }
            .Option("Scare him off", "Respect +2. Heat +15 around your piazze, evidence +5.", g =>
            {
                AdjustRespect(g, 2f);
                foreach (var sq in g.Org.Squares) g.Territory.AddHeat(sq.Cell, 15f);
                g.Org.AddEvidence(5f, g);
            })
            .Option("Feed him a story about the rivals", "Evidence +2. The rivals won't like the headlines: hostility +10.", g =>
            {
                g.Org.AddEvidence(2f, g);
                g.Rival.Hostility = Mathf.Min(100f, g.Rival.Hostility + 10f);
            }, g => !g.Rival.Destroyed)
            .Option("Ignore him", "Evidence +8. His article will be read by the prosecutors.", g => g.Org.AddEvidence(8f, g));
        }

        static GameEvent Undercover(Game game)
        {
            var square = game.Org.Squares.FirstOrDefault();
            if (square == null || game.Stock.Product <= 0) return null;
            var zone = game.Territory.Zones[square.ZoneId];
            return new GameEvent
            {
                Id = "undercover",
                Title = "A customer with questions",
                Neapolitan = "Chisto nun è d''o quartiere.",
                English = "This one's not from the neighbourhood.",
                Body = $"A man in clean shoes at the piazza in {zone.Name} wants to buy a lot, and keeps asking who's in charge.",
            }
            .Option("Chase him off", "Respect −1. If he was a cop, you'll never know.", g => AdjustRespect(g, -1f))
            .Option("Sell to him anyway", "Could be a big buyer. Could be the police.", g =>
            {
                if (g.Rng.NextDouble() < 0.5)
                {
                    g.Territory.AddHeat(square.Cell, 25f);
                    g.Org.AddEvidence(10f, g);
                    g.Log.Add("He was an undercover officer. Your piazza is on a file now.", MessageKind.Bad);
                }
                else
                {
                    int units = Mathf.Min(g.Stock.Product, 10);
                    g.Stock.Product -= units;
                    g.Stock.Cash += units * Prices.ProductStreet;
                    g.Log.Add($"He bought {units} units and paid cash. Just a buyer from out of town.", MessageKind.Good);
                }
            });
        }

        static GameEvent CrewWantsOut(Game game)
        {
            var pawn = PresentCrew(game).Where(p => !p.IsBoss && p.Loyalty < 35f).OrderBy(p => p.Loyalty).FirstOrDefault();
            if (pawn == null) return null;
            return new GameEvent
            {
                Id = "wants_out",
                Title = $"{pawn.Nickname} wants out",
                Neapolitan = "Don, tengo 'na famiglia. Me ne voglio jì.",
                English = "Don, I've got a family. I want out.",
                Body = $"{pawn.FullName} asks to leave the System. Nobody leaves clean, and everyone knows what he's seen.",
            }
            .Option("Let him go", pawn.Loyalty < 20f ? "He leaves. He knows a lot: evidence +15." : "He leaves the crew.", g =>
            {
                pawn.EndCurrentJob(g);
                pawn.Departed = true;
                if (pawn.Loyalty < 20f) g.Org.AddEvidence(15f, g);
                g.Log.Add($"{pawn.FullName} has left the crew.", MessageKind.Info);
            })
            .Option("Give him €200 to stay", "Loyalty +25.", g =>
            {
                Pay(g, 200);
                pawn.Loyalty = Mathf.Min(100f, pawn.Loyalty + 25f);
            }, g => Cash(g, 200))
            .Option("Remind him what happens to those who leave", "Loyalty +15, but he's terrified: mood −12. The others hear about it.", g =>
            {
                long now = g.Ticks.TicksGame;
                pawn.Loyalty = Mathf.Min(100f, pawn.Loyalty + 15f);
                pawn.Mood.AddMemory("threatened", "Threatened by the boss", -12f, now, GameClock.TicksPerDay * 3);
                foreach (var other in PresentCrew(g))
                    if (other != pawn) other.Mood.AddMemory("fear", "The boss threatened one of us", -3f, now, GameClock.TicksPerDay);
            });
        }

        static GameEvent Wedding(Game game)
        {
            var pawn = PresentCrew(game).Where(p => !p.IsBoss).OrderBy(_ => game.Rng.Next()).FirstOrDefault();
            if (pawn == null) return null;
            return new GameEvent
            {
                Id = "wedding",
                Title = "A wedding in the family",
                Neapolitan = "Ce stanno tutte quante. Pure vuje, Don.",
                English = "Everyone will be there. You too, Don.",
                Body = $"{pawn.Nickname}'s sister is getting married at the Santuario. The whole rione is watching to see how generous the family is.",
            }
            .Option("A generous envelope (€250)", "The whole crew's loyalty +5, Respect +3.", g =>
            {
                Pay(g, 250);
                foreach (var p in PresentCrew(g)) p.Loyalty = Mathf.Min(100f, p.Loyalty + 5f);
                AdjustRespect(g, 3f);
            }, g => Cash(g, 250))
            .Option("A modest envelope (€50)", "Nothing more, nothing less.", g => Pay(g, 50), g => Cash(g, 50))
            .Option("Don't go", $"{pawn.Nickname}'s loyalty −10.", g => pawn.Loyalty = Mathf.Max(0f, pawn.Loyalty - 10f));
        }

        static GameEvent SupplierDeal(Game game)
        {
            if (game.Org.Squares.Count == 0) return null;
            return new GameEvent
            {
                Id = "supplier_deal",
                Title = "A new supplier",
                Neapolitan = "Roba bona, prezzo buono. Fidate.",
                English = "Good stuff, good price. Trust me.",
                Body = "A man from outside the rione offers 60 units for €350, cash, tonight. Half the usual price. Too good?",
            }
            .Option("Take the deal (€350)", "Usually it's just a bargain. Sometimes it's a setup.", g =>
            {
                Pay(g, 350);
                if (g.Rng.NextDouble() < 0.7)
                {
                    g.Stock.Product += 60;
                    g.Log.Add("The deal went through: 60 units at the basso.", MessageKind.Good);
                }
                else
                {
                    var sq = g.Org.Squares[g.Rng.Next(g.Org.Squares.Count)];
                    g.Territory.AddHeat(sq.Cell, 20f);
                    g.Org.AddEvidence(10f, g);
                    g.Log.Add("It was a setup. The money is gone and the police were watching the handover.", MessageKind.Bad);
                }
            }, g => Cash(g, 350))
            .Option("Stick with the usual supplier", "Nothing happens.", _ => { });
        }

        static GameEvent RivalTruce(Game game)
        {
            var rival = game.Rival;
            if (rival.Destroyed || rival.Hostility < 55f || rival.InTruce(game)) return null;
            return new GameEvent
            {
                Id = "rival_truce",
                Title = $"The {rival.Name} wants to talk",
                Neapolitan = "'O boss vo' parlà. Nisciuno s'avessa fa' male.",
                English = "The boss wants to talk. Nobody needs to get hurt.",
                Body = $"A message from {rival.BossName}: a meeting at a restaurant on neutral ground, to put an end to this.",
            }
            .Option("Pay tribute (€1000)", "Hostility drops to 10. Two weeks of truce.", g =>
            {
                Pay(g, 1000);
                g.Rival.StartTruce(14, g);
                AdjustRespect(g, -5f);
                g.Log.Add($"Tribute paid to the {g.Rival.Name}. Two weeks of peace, at the price of some pride.");
            }, g => Cash(g, 1000))
            .Option("Divide the territory", "Close the piazza nearest to them. Hostility −40.", g =>
            {
                var nearest = g.Org.Squares.OrderBy(s => g.Rival.NearRivalTerritory(s) ? 0 : 1).FirstOrDefault();
                if (nearest != null) g.Org.CloseSquareAt(nearest.Cell);
                g.Rival.Hostility = Mathf.Max(0f, g.Rival.Hostility - 40f);
                g.Rival.StartTruce(7, g);
            }, g => g.Org.Squares.Count > 0)
            .Option("Refuse to meet", "Respect +3. Hostility +10.", g =>
            {
                AdjustRespect(g, 3f);
                g.Rival.Hostility = Mathf.Min(100f, g.Rival.Hostility + 10f);
            });
        }

        // ---------- System cards ----------

        public static GameEvent RivalWarning(Game game)
        {
            var rival = game.Rival;
            return new GameEvent
            {
                Id = "rival_warning",
                Title = $"A message from the {rival.Name}",
                Neapolitan = "Dice 'o boss ca chesta zona nun è 'a vosta.",
                English = "The boss says this area isn't yours.",
                Body = $"Two boys on a scooter stop at the basso. {rival.BossName} doesn't like your piazze so close to his.",
            }
            .Option("Close the piazza nearest to them", "Hostility −20.", g =>
            {
                var nearest = g.Org.Squares.FirstOrDefault(s => g.Rival.NearRivalTerritory(s)) ?? g.Org.Squares.FirstOrDefault();
                if (nearest != null) g.Org.CloseSquareAt(nearest.Cell);
                g.Rival.Hostility = Mathf.Max(0f, g.Rival.Hostility - 20f);
            }, g => g.Org.Squares.Count > 0)
            .Option("Send a gift (€300)", "Hostility −15.", g => g.Rival.SendGift(300, 15f, g), g => Cash(g, 300))
            .Option("Tell them to go to hell", "Respect +3. Hostility +15.", g =>
            {
                AdjustRespect(g, 3f);
                g.Rival.Hostility = Mathf.Min(100f, g.Rival.Hostility + 15f);
            });
        }

        /// <summary>
        /// A word with Don Saverio about his corner: in person, or on the phone from the Piazza screen.
        /// He sells it once, to a Paranza with the money, or for a cut every Sunday to a name he trusts.
        /// </summary>
        public static GameEvent SaverioOffer(Game game)
        {
            var org = game.Org;
            var contact = game.Contacts.ByName(ContactBook.Saverio);
            bool can = org.CanBuyOutSaverio(game, out var why);
            int price = org.SaverioPrice;
            var zone = org.SaverioZone(game);
            var e = new GameEvent
            {
                Id = "saverio_offer",
                Title = "Don Saverio's corner",
                Portrait = contact?.Face ?? -1,
                Neapolitan = can ? "'O pizzo mio? Tutto se venne, guagliò. Basta ca 'o prezzo è giusto." : "Tu? Va' a jucà 'o pallone, va'.",
                English = can ? "My corner? Everything's for sale, kid. As long as the price is right." : "You? Go and play football.",
                Body = can
                    ? $"He's run the corner in {zone?.Name ?? "the rione"} for twenty years and his knees hurt. For €{price} he retires to Ischia and the corner is yours. " +
                      $"Or €{Organization.SaverioCutDown} now and €{Organization.SaverioCutWeekly} every Sunday for {Organization.SaverioCutWeeks} weeks: miss two Sundays and he takes it back."
                    : why,
            };
            if (can)
            {
                e.Option($"Pay him to retire · €{price}", "Cash or clean, he doesn't care. The corner and the rione are yours today.",
                    g => g.Org.BuyOutSaverio(g), g => g.Stock.Cash + g.Stock.Clean >= price);
                e.Option($"A cut · €{Organization.SaverioCutDown} now, €{Organization.SaverioCutWeekly} a Sunday ×{Organization.SaverioCutWeeks}",
                    org.Respect >= Organization.SaverioCutRespect
                        ? $"The corner is yours today. €{Organization.SaverioCutDown + Organization.SaverioCutWeekly * Organization.SaverioCutWeeks} in all."
                        : $"He only takes promises from a name: respect {org.Respect:0}/{Organization.SaverioCutRespect:0}.",
                    g => g.Org.CutDealWithSaverio(g),
                    g => g.Org.Respect >= Organization.SaverioCutRespect && g.Stock.Cash + g.Stock.Clean >= Organization.SaverioCutDown);
            }
            return e.Option("Not now", "The other way is a gun: him, and his men.", g => { });
        }

        public static GameEvent RankUp(Game game, int rank)
        {
            var boss = game.Org.Boss(game);
            string who = boss != null ? boss.DisplayName : "The family";
            var (title, nap, en, body) = rank switch
            {
                1 => ("The paranza grows", "Mo' nun simmo cchiù guagliune.", "We're not kids anymore.",
                    $"Word has gone round the vicoli: {who}'s boys get things done. Time to stop working for others and open a piazza of your own."),
                2 => ("A clan is born", "'O nomme nuosto se sape.", "People know our name now.",
                    $"Shopkeepers lower their eyes when {who} walks past. You can run more piazze, punish those who don't pay, and buy shops to launder the money."),
                _ => ("Inside the System", "Mo' simmo 'o Sistema.", "Now we are the System.",
                    $"{who} sits at the table with the men who decide things in this city. Everything is possible now, and everything can be lost."),
            };
            return new GameEvent
            {
                Id = $"rank_{rank}",
                Title = title,
                Neapolitan = nap,
                English = en,
                Body = body + $"\n\nUnlocked: {Organization.UnlocksAt(rank)}.",
            }
            .Option("Avanti", "", _ => { });
        }

        public static GameEvent Warrant(Game game)
        {
            var boss = game.Org.Boss(game);
            if (boss == null) return null;
            bool bunker = game.Map.PositionsOf(OSistema.World.BuildingDefs.Bunker).Count > 0;
            return new GameEvent
            {
                Id = "warrant",
                Title = "Mandato di cattura",
                Neapolitan = "Don, l'avvocato ha chiammato. Stanno venenno pe' vuje.",
                English = "Don, the lawyer called. They're coming for you.",
                Body = $"The prosecutors have enough on {boss.FullName}. An arrest warrant has been signed. Decide now.",
            }
            .Option("Go into latitanza", bunker
                ? "Hide in the bunker. Orders go out by pizzino, a day late. Searches may find you."
                : "No bunker: you'll hide at the basso, and a search will very likely find you.", g => g.Org.GoIntoHiding(g))
            .Option("Turn yourself in", "30 days in Poggioreale. The case loses steam (evidence 30).", g => g.Org.TurnSelfIn(g))
            .Option("Flee abroad", "You escape, but this boss's story ends. An heir takes over.", g => g.Org.FleeAbroad(g));
        }

        public static GameEvent Succession(Game game, List<Pawn> candidates)
        {
            var e = new GameEvent
            {
                Id = "succession",
                Title = "Who leads the family now?",
                Neapolitan = "'O re è muorto. Viva 'o re.",
                English = "The king is dead. Long live the king.",
                Body = "The boss is gone. The family needs a head before the rivals smell weakness. Choose the heir.",
            };
            foreach (var c in candidates)
            {
                var heir = c;
                e.Option($"{heir.FullName} “{heir.Nickname}”",
                    $"Loyalty {heir.Loyalty:0} · Intimidation {heir.IntimidationSkill} · Shooting {heir.ShootingSkill}",
                    g => g.Org.AppointBoss(heir, g), g => heir.IsPresent);
            }
            return e;
        }
    }
}
