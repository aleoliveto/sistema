using System.Linq;
using OSistema.AI;
using OSistema.Core;
using OSistema.Crime;
using OSistema.Pawns;
using UnityEngine;

namespace OSistema.Story
{
    /// <summary>Orders against a clan: a stesa over one of their piazze, a hit on one of their men, information.</summary>
    public static class OperationCards
    {
        static System.Collections.Generic.IEnumerable<Pawn> Gunmen(Game game) =>
            game.Pawns.Crew.Where(c => c.CanAct && !c.Controlled && c.Armed && !(c.CurrentJob is CrewStesaJob) && !(c.CurrentJob is CrewHitJob));

        /// <summary>Which of their piazze.</summary>
        public static GameEvent Stesa(RivalClan clan, Game game)
        {
            var e = new GameEvent
            {
                Id = "order_stesa",
                Title = $"A stesa on the {clan.Name}",
                English = "Shots over their piazza: a warning everyone in the rione will hear.",
                Body = "Hostility +15, respect +3. Their pusher runs, their customers scatter. The police will come to the noise.",
            };
            foreach (var sq in clan.Squares)
            {
                var square = sq;
                var zone = game.Territory.Zones[sq.ZoneId];
                e.Option($"Their piazza in {zone.Name}", "", g => g.Events.Raise(Riders(clan, square, null, g)));
            }
            return e.Option("Not now", "", g => { });
        }

        /// <summary>Who rides: one gunman, and a second if you want.</summary>
        static GameEvent Riders(RivalClan clan, DrugSquare square, Pawn first, Game game)
        {
            var e = new GameEvent
            {
                Id = "stesa_riders",
                Title = first == null ? "Who rides?" : $"With {first.Nickname}?",
                English = first == null ? "An armed man on a scooter. Two is better." : "A second rider, or just him.",
                Body = Gunmen(game).Any() ? "" : "Nobody armed and free. Give someone a pistol first (Famiglia, Crew).",
            };
            foreach (var c in Gunmen(game))
            {
                if (c == first) continue;
                var gunman = c;
                e.Option($"{gunman.DisplayName} · shooting {gunman.ShootingSkill}", "", g =>
                {
                    // The second rider sits behind the first.
                    gunman.ForceJob(new CrewStesaJob(gunman, clan, square, first), g);
                    if (first == null) g.Events.Raise(Riders(clan, square, gunman, g));
                    else g.Log.Add($"{first.Nickname} and {gunman.Nickname} ride out to the {clan.Name}'s piazza.", MessageKind.Info, square.Cell);
                });
            }
            if (first != null) e.Option("Just him", "", g => g.Log.Add($"{first.Nickname} rides out alone to the {clan.Name}'s piazza.", MessageKind.Info, square.Cell));
            else e.Option("Not now", "", g => { });
            return e;
        }

        /// <summary>Who to kill. What you know decides who you can find.</summary>
        public static GameEvent Hit(RivalClan clan, Game game)
        {
            var e = new GameEvent
            {
                Id = "order_hit",
                Title = $"A hit on the {clan.Name}",
                English = "You need to know where a man will be before you can send someone for him.",
                Body = $"What the family knows about them: {clan.Intel:0}/100. A lieutenant needs {RivalClan.IntelForLieutenant:0}, the boss {RivalClan.IntelForBoss:0}. Lookouts near their turf, Nanninella and their dead all tell you more.",
            };
            e.Option("One of their lieutenants", "Soldiers −1, hostility +30, respect +5. Evidence +8.", g => g.Events.Raise(Hitman(clan, false, g)), g => clan.Intel >= RivalClan.IntelForLieutenant);
            e.Option($"The boss, {(clan.Intel >= RivalClan.IntelForBoss ? clan.BossName : "whoever he is")}", "Soldiers −3; they may fall apart. Respect +12. Evidence +8, and a war.", g => g.Events.Raise(Hitman(clan, true, g)), g => clan.Intel >= RivalClan.IntelForBoss);
            return e.Option("Not now", "", g => { });
        }

        /// <summary>
        /// The hit squad, picked one by one: he'll have bodyguards (two, for a boss), so send at least
        /// as many. Riding two to a scooter.
        /// </summary>
        static GameEvent Hitman(RivalClan clan, bool boss, Game game, System.Collections.Generic.List<Pawn> squad = null)
        {
            squad ??= new System.Collections.Generic.List<Pawn>();
            int guards = boss ? 2 : 1;
            var e = new GameEvent
            {
                Id = "hitman",
                Title = squad.Count == 0 ? "Who goes?" : $"The squad: {string.Join(", ", squad.Select(s => s.Nickname))}",
                English = squad.Count == 0 ? $"He'll have {guards} bodyguard{(guards > 1 ? "s" : "")}. Send at least as many guns." : "Anyone else?",
                Body = Gunmen(game).Any(c => !squad.Contains(c)) ? "" : "Nobody else armed and free.",
            };
            foreach (var c in Gunmen(game))
            {
                if (squad.Contains(c)) continue;
                var hitman = c;
                e.Option($"{hitman.DisplayName} · shooting {hitman.ShootingSkill} · loyalty {hitman.Loyalty:0}", "", g =>
                {
                    var next = new System.Collections.Generic.List<Pawn>(squad) { hitman };
                    if (next.Count >= 4) Launch(clan, boss, next, g);
                    else g.Events.Raise(Hitman(clan, boss, g, next));
                });
            }
            if (squad.Count > 0) e.Option($"Go: {squad.Count} of them", squad.Count <= guards ? "Even odds at best." : "", g => Launch(clan, boss, squad, g));
            return e.Option("Call it off", "", g => { });
        }

        static void Launch(RivalClan clan, bool boss, System.Collections.Generic.List<Pawn> squad, Game g)
        {
            var target = clan.ExposeTarget(boss, g);
            if (target == null) { g.Log.Add("Nobody knows where to find him today.", MessageKind.Bad); return; }
            // Two to a scooter: every second man rides behind the one before him.
            for (int i = 0; i < squad.Count; i++)
                squad[i].ForceJob(new CrewHitJob(squad[i], target, i % 2 == 1 ? squad[i - 1] : null), g);
            g.Log.Add($"Word is {(boss ? clan.BossName : "one of the " + clan.Name + "'s lieutenants")} is in {g.Territory.ZoneAt(target.Cell)?.Name ?? "their rione"}. {string.Join(", ", squad.Select(s => s.Nickname))} go for him.", MessageKind.Info, target.Cell);
        }

        public const int InfoPrice = 250;

        /// <summary>Nanninella hears everything: who sleeps where, who drinks at which bar.</summary>
        public static GameEvent Information(Game game)
        {
            var e = new GameEvent
            {
                Id = "buy_info",
                Title = "What do you want to know?",
                English = "Names, faces, who drinks at which bar.",
                Body = $"€{InfoPrice} buys a good piece of what she knows about one clan (information +20).",
            };
            foreach (var c in game.Clans)
            {
                if (c.Destroyed || c.Intel >= 100f) continue;
                var clan = c;
                e.Option($"About the {clan.Name} · now {clan.Intel:0}/100", "", g =>
                {
                    if (!g.Org.Pay(InfoPrice, g, out var err)) { g.Log.Add(err, MessageKind.Bad); return; }
                    clan.Intel = Mathf.Min(100f, clan.Intel + 20f);
                    g.Log.Add($"Nanninella talks for a while about the {clan.Name}. You write it all down.", MessageKind.Good);
                });
            }
            return e.Option("Nothing", "", g => { });
        }
    }
}
