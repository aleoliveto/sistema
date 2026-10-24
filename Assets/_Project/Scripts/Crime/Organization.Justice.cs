using System.Collections.Generic;
using System.Linq;
using OSistema.Core;
using OSistema.Pawns;
using OSistema.Story;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// When the investigators have enough (evidence 65+), they don't wait for a warrant for the boss: at five in the
    /// morning the blitz comes to the family's homes and takes whoever is asleep there, and what's in the stash.
    /// A week later, the trial. A good lawyer helps; a witness who talks hurts; a witness can be persuaded not to.
    /// </summary>
    public sealed partial class Organization
    {
        sealed class Trial
        {
            public int Day, WitnessDay;
            public readonly List<Pawn> Accused = new List<Pawn>();
            public bool Lawyer, HasWitness, WitnessSilenced, WitnessAsked;
        }

        readonly List<Trial> trials = new List<Trial>();
        int lastBlitzDay = -100;
        const int TrialAfterDays = 7;

        void JusticeHourly(Game game)
        {
            int day = game.Clock.Day, hour = game.Clock.Hour;
            if (hour == 5 && Evidence >= 65f && !Hiding && day - lastBlitzDay >= 10 &&
                game.Rng.NextDouble() < 0.12 + (Evidence - 65f) / 200f)
                Blitz(game);
            if (hour == 10)
            {
                foreach (var t in trials.ToList())
                {
                    if (t.HasWitness && !t.WitnessAsked && day >= t.WitnessDay)
                    {
                        t.WitnessAsked = true;
                        game.Events.Raise(WitnessCard(t, game));
                    }
                    if (day >= t.Day) Verdict(t, game);
                }
            }
        }

        public void DevBlitz(Game game) { lastBlitzDay = -100; Blitz(game); }

        static bool AtHome(Pawn p, Game game) => game.PlayerBase.Contains(p.Cell) || game.Properties.IsOwnedHome(p.Cell);

        void Blitz(Game game)
        {
            var rng = game.Rng;
            long now = game.Ticks.TicksGame;
            // Whoever is asleep at the family's homes; the boss, if he's there, is the one they want least today:
            // the warrant for him is a different file.
            var home = game.Pawns.Crew.Where(p => p.IsPresent && !p.Controlled && !p.IsBoss && AtHome(p, game)).OrderBy(_ => rng.Next()).ToList();
            if (home.Count == 0) return;
            lastBlitzDay = game.Clock.Day;
            int n = Mathf.Min(home.Count, Evidence >= 85f ? 3 : 2);
            var trial = new Trial { Day = game.Clock.Day + TrialAfterDays, HasWitness = rng.NextDouble() < 0.5 };
            trial.WitnessDay = game.Clock.Day + 2;
            for (int i = 0; i < n; i++)
            {
                var p = home[i];
                p.SendToJail(now + (TrialAfterDays + 1) * GameClock.TicksPerDay, game);
                trial.Accused.Add(p);
                float press = p.Character == Trait.Loyal ? 10f : p.Character == Trait.Chatty ? 32f : 20f;
                game.Informants.Of(p).Pressure += press;
            }
            trials.Add(trial);

            // The stash, unless it's well hidden.
            var s = game.Stock;
            int doses = game.Favours.StashHidden(game) ? 0 : s.Product / 2;
            int guns = s.Pistols + s.Berettas + s.Kalash;
            int cash = s.Cash * 15 / 100;
            s.Product -= doses;
            s.Pistols = s.Berettas = s.Kalash = 0;
            s.Cash -= cash;
            Evidence = Mathf.Max(0f, Evidence - 15f);
            game.Territory.AddHeat(Vector2Int.RoundToInt(game.PlayerBase.center), 10f);

            string names = string.Join(", ", trial.Accused.Select(p => p.DisplayName));
            game.Log.Add($"Blitz at dawn: the Carabinieri broke down the doors at five. {names} taken. " +
                         $"Seized: {doses} doses, {guns} guns, €{cash}. The trial is on {Calendar.DateString(trial.Day)}.", MessageKind.Bad);
            game.Newspaper.Report(game, 6, "Dawn raid on the family", $"{n} arrests in an operation by the Carabinieri. Drugs, weapons and cash were seized from homes in the old town.");
            foreach (var other in game.Pawns.Crew)
                if (other.IsPresent) other.Mood.AddMemory("blitz", "The blitz at dawn", -8f, now, GameClock.TicksPerDay * 2);
            game.Events.Raise(LawyerCard(trial, game));
        }

        GameEvent LawyerCard(Trial t, Game game)
        {
            int fee = 900 * t.Accused.Count;
            return new GameEvent
            {
                Id = "blitz_lawyer",
                Title = "Avvocato Ruocco",
                Neapolitan = "Cu 'e sorde justi, 'e carte se perdono.",
                English = "With the right money, papers get lost.",
                Body = $"{string.Join(", ", t.Accused.Select(p => p.FullName))} go before the judge on {Calendar.DateString(t.Day)}. Ruocco can take the case: €{fee}, and he wants it now.",
            }
            .Option($"Hire Ruocco (€{fee})", "Each of them is much likelier to walk out free.", g => { g.Stock.Cash -= fee; t.Lawyer = true; }, g => g.Stock.Cash >= fee)
            .Option("A court lawyer will do", "Free, and it shows.", g => { });
        }

        GameEvent WitnessCard(Trial t, Game game)
        {
            var zone = game.Territory.ZoneAt(Vector2Int.RoundToInt(game.PlayerBase.center));
            var heavy = game.Pawns.Crew.Where(p => p.IsPresent && !p.Controlled).OrderByDescending(p => p.IntimidationSkill).FirstOrDefault();
            return new GameEvent
            {
                Id = "blitz_witness",
                Title = "A witness",
                Neapolitan = "'O salumiere ha parlato cu 'e guardie.",
                English = "The salumiere has talked to the cops.",
                Body = $"A shopkeeper from {zone?.Name ?? "the rione"} has agreed to testify at the trial: he saw who came and went from the basso.",
            }
            .Option(heavy != null ? $"Send {heavy.Nickname} to talk to him" : "Send someone to talk to him",
                "Usually he forgets everything. Sometimes he goes straight to the Carabinieri: evidence +6.", g =>
            {
                if (g.Rng.NextDouble() < 0.7 + (heavy?.IntimidationSkill ?? 0) * 0.02) { t.WitnessSilenced = true; g.Log.Add("The witness has remembered he saw nothing.", MessageKind.Good); }
                else { g.Org.AddEvidence(6f, g); g.Log.Add("The witness went to the Carabinieri about the visit. Now he'll talk louder.", MessageKind.Bad); }
            })
            .Option("Pay him to forget (€600)", "Nine times in ten it works.", g =>
            {
                g.Stock.Cash -= 600;
                if (g.Rng.NextDouble() < 0.9) t.WitnessSilenced = true;
                else g.Log.Add("He took the money and testified anyway.", MessageKind.Bad);
            }, g => g.Stock.Cash >= 600)
            .Option("Let him talk", "The case gets stronger.", g => { });
        }

        void Verdict(Trial t, Game game)
        {
            trials.Remove(t);
            long now = game.Ticks.TicksGame;
            var free = new List<string>();
            var lines = new List<string>();
            foreach (var p in t.Accused)
            {
                if (p.Dead || p.Departed || !p.InJail) continue;
                float guilty = 0.55f + (t.HasWitness && !t.WitnessSilenced ? 0.25f : 0f) - (t.Lawyer ? 0.3f : 0f) - (p.Character == Trait.Careful ? 0.1f : 0f);
                if (game.Rng.NextDouble() < Mathf.Clamp(guilty, 0.1f, 0.9f))
                {
                    int days = game.Rng.Next(20, 61);
                    p.JailedUntil = now + days * GameClock.TicksPerDay;
                    lines.Add($"{p.DisplayName}: {days} days");
                    // A long stretch inside: the investigators lean harder.
                    if (p.Loyalty < 45f) game.Informants.Of(p).Pressure += 20f;
                }
                else
                {
                    p.JailedUntil = now;   // released at the next hour
                    free.Add(p.DisplayName);
                }
            }
            if (lines.Count == 0 && free.Count == 0) return;
            game.Log.Add("The verdict." + (lines.Count > 0 ? $" Convicted: {string.Join(", ", lines)}." : "") + (free.Count > 0 ? $" Acquitted: {string.Join(", ", free)}." : ""),
                lines.Count > free.Count ? MessageKind.Bad : MessageKind.Good);
            game.Newspaper.Report(game, 4, "Verdict in the dawn-raid trial", lines.Count > 0
                ? $"{lines.Count} convicted, {free.Count} acquitted. The defence speaks of a theorem without proof."
                : "All the accused acquitted. The prosecutors will appeal.");
        }
    }
}
