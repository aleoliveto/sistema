using OSistema.Core;
using OSistema.Crime;
using OSistema.Npcs;
using OSistema.Pawns;
using UnityEngine;

namespace OSistema.Story
{
    /// <summary>Scenes played out face to face in the street: the debtor at his door, and the like.</summary>
    public static class StreetScenes
    {
        /// <summary>You knock. He opens the door a crack, and comes out onto the step.</summary>
        public static GameEvent Knock(Contract job, Pawn p, Game game)
        {
            // Someone actually comes out.
            var door = job.To;
            Npc debtor = null;
            foreach (var n in new[] { door + Vector2Int.down, door + Vector2Int.up, door + Vector2Int.left, door + Vector2Int.right })
            {
                if (!game.Map.InBounds(n) || !game.Map.IsPassable(n)) continue;
                debtor = game.Npcs.Spawn(NpcKind.Civilian, n, 0.05f, new Color32(150, 140, 120, 255));
                debtor.Target = n;
                break;
            }

            int skill = p.IntimidationSkill;
            float ask = Mathf.Clamp(0.25f + 0.04f * skill + game.Org.Respect / 200f, 0.05f, 0.9f);
            float threat = Mathf.Clamp(0.5f + 0.05f * skill + game.Org.Respect / 200f, 0.1f, 0.95f);

            void Outcome(Game g, bool paid, string how, float heat)
            {
                if (heat > 0f) g.Territory.AddHeat(door, heat);
                p.Learn(Skill.Intimidation, paid ? 14f : 6f, g);
                g.Contracts.Finish(job, paid, how, g);
                if (heat >= 6f && g.Rng.NextDouble() < 0.35)
                {
                    g.Log.Add("A neighbour watched it all from the balcony and called the police.", MessageKind.Bad, door);
                    var cop = Pursuit.WitnessCop(g, 12f);
                    if (cop != null) g.Pursuit.Start(cop, "A neighbour called the police on you.", g);
                }
            }

            return new GameEvent
            {
                Id = "knock",
                Title = "The debtor's door",
                Neapolitan = "Chi è? ...Ah, si' tu. Nun tengo niente, 'o giuro 'ncopp' 'e figlie mie.",
                English = "Who is it? ...Oh, it's you. I've got nothing, I swear on my children.",
                Body = $"A man in a vest opens the door a crack, then steps out so the wife won't hear. He owes Don Peppe money. You keep a third of €{job.Reward * 3}.",
            }
            .Option("Ask him nicely", $"Chance about {ask * 100f:0}%. No heat.", g =>
            {
                bool ok = g.Rng.NextDouble() < ask;
                Outcome(g, ok, ok ? "he went back in and came out with an envelope." : "he shrugged and closed the door.", 0f);
            })
            .Option("Lean on him", $"Chance about {threat * 100f:0}%. A little heat.", g =>
            {
                bool ok = g.Rng.NextDouble() < threat;
                Outcome(g, ok, ok ? "he found the money after all, behind the Madonna on the shelf." : "he swore he has nothing. Next time, more conviction.", 3f);
            })
            .Option("Slap him against the wall", "He'll pay. The neighbours will talk. Heat, respect, and a little evidence.", g =>
            {
                g.Org.Respect = Mathf.Min(100f, g.Org.Respect + 1f);
                g.Org.AddEvidence(2f, g);
                Outcome(g, true, "he paid, with a split lip.", 8f);
            })
            .Option("Leave it for now", "Come back later. The job stays yours until it expires.", g => { });
        }
    }
}
