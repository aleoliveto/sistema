using OSistema.Core;
using OSistema.Pawns;
using OSistema.Story;
using UnityEngine;

namespace OSistema.Crime
{
    /// <summary>
    /// Getting patched up. Three ways, each with its price:
    /// medicine from home (cheap, half a job, anyone can do it, a medic does it better);
    /// the pharmacist (cuts and bruises only: he won't touch a bullet);
    /// the Ospedale (does it properly, but a gunshot wound gets reported to the police).
    /// </summary>
    public static class Healing
    {
        public const int PharmacistFee = 30;
        public const int HospitalFee = 90;

        public static bool HasGunshot(Pawn p)
        {
            foreach (var i in p.Health.Injuries) if (i.Label == "Gunshot wound") return true;
            return false;
        }

        /// <summary>Wounds that still need a hand: untended, or tended badly.</summary>
        public static bool NeedsCare(Pawn p)
        {
            foreach (var i in p.Health.Injuries)
                if (!i.IsMalnutrition && (!i.Tended || i.TendQuality < 0.85f)) return true;
            return p.Health.BloodLoss > 0.3f;
        }

        /// <summary>A bullet deep in, or a lot of blood gone: beyond what a box of medicine at home can do.</summary>
        public static bool TooHurtForHome(Pawn p)
        {
            if (p.Health.BloodLoss >= 0.5f) return true;
            foreach (var i in p.Health.Injuries)
                if (i.Label == "Gunshot wound" && i.Severity >= 0.5f && (!i.Tended || i.TendQuality < 0.85f)) return true;
            return false;
        }

        /// <summary>A box of medicine from home. Quality depends on whoever does it: best medic present.</summary>
        public static bool WithMedicine(Game game, Pawn patient)
        {
            if (game.Stock.Medicine <= 0)
            {
                game.Log.Add("No medicine at home. The farmacia sells it.", MessageKind.Bad);
                return false;
            }
            if (!NeedsCare(patient)) return false;
            if (TooHurtForHome(patient))
            {
                game.Log.Add($"{patient.DisplayName}'s wounds are too bad to treat at home. Only the hospital can (118), and they report gunshots.", MessageKind.Bad, patient.Cell);
                return false;
            }
            int skill = patient.MedicineSkill;
            foreach (var c in game.Pawns.Crew)
                if (!c.Dead && c.IsPresent && c.CanAct && (c.Position - patient.Position).sqrMagnitude < 25f) skill = Mathf.Max(skill, c.MedicineSkill);
            float quality = Mathf.Clamp(0.55f + skill * 0.045f, 0.55f, 0.92f);
            game.Stock.Medicine--;
            foreach (var i in patient.Health.Injuries)
            {
                if (i.IsMalnutrition) continue;
                i.Tended = true;
                i.TendQuality = Mathf.Max(i.TendQuality, quality);
            }
            game.Log.Add($"{patient.DisplayName}'s wounds are cleaned and bandaged ({quality * 100f:0}%).", MessageKind.Good, patient.Cell);
            return true;
        }

        /// <summary>The pharmacist: bandages and stitches for small things. Not bullets.</summary>
        public static GameEvent Pharmacist(Game game, Pawn patient)
        {
            bool bullet = HasGunshot(patient);
            var e = new GameEvent
            {
                Id = "pharmacist",
                Title = "The pharmacist",
                Neapolitan = bullet ? "Chesta è 'na botta 'e pistola. Io nun aggio visto niente. Iate â l'ospedale." : "Assettateve, mo' ve sistemo io.",
                English = bullet ? "That's a bullet wound. I didn't see anything. Go to the hospital." : "Sit down, I'll sort you out.",
                Body = bullet ? "He won't touch it. The hospital can, but doctors must report gunshot wounds." : $"He cleans and stitches it in the back room. €{PharmacistFee}.",
            };
            if (!bullet && NeedsCare(patient))
                e.Option($"Let him patch you up · €{PharmacistFee}", "Well tended: heals quickly.", g =>
                {
                    if (!g.Org.Pay(PharmacistFee, g, out var err)) { g.Log.Add(err, MessageKind.Bad); return; }
                    foreach (var i in patient.Health.Injuries)
                    {
                        if (i.IsMalnutrition) continue;
                        i.Tended = true;
                        i.TendQuality = Mathf.Max(i.TendQuality, 0.85f);
                    }
                    g.Log.Add("Stitched and bandaged at the farmacia.", MessageKind.Good);
                });
            return e.Option("Leave", "", g => { });
        }

        /// <summary>Walking into the Ospedale yourself.</summary>
        public static GameEvent Hospital(Game game, Pawn patient)
        {
            bool bullet = HasGunshot(patient);
            var e = new GameEvent
            {
                Id = "hospital_desk",
                Title = "Pronto soccorso",
                Neapolitan = "Nomme, cognome, e che v'è succiesso?",
                English = "Name, surname, and what happened to you?",
                Body = bullet
                    ? "They'll fix it properly, and stop the bleeding for good. But a gunshot wound goes in a report to the police."
                    : "A doctor sees you, cleans everything up and sends you home.",
            };
            e.Option($"Get treated · €{HospitalFee}" + (bullet ? " (reported)" : ""), "Wounds tended properly, blood topped up.", g =>
            {
                if (!g.Org.Pay(HospitalFee, g, out var err)) { g.Log.Add(err, MessageKind.Bad); return; }
                foreach (var i in patient.Health.Injuries)
                {
                    if (i.IsMalnutrition) continue;
                    i.Tended = true;
                    i.TendQuality = Mathf.Max(i.TendQuality, 0.97f);
                    i.Severity *= 0.7f;
                }
                patient.Health.BloodLoss = Mathf.Max(0f, patient.Health.BloodLoss - 0.35f);
                if (bullet)
                {
                    g.Org.AddEvidence(5f, g);
                    g.Log.Add("The hospital reported your gunshot wound to the police.", MessageKind.Bad);
                }
                g.Log.Add("Patched up at the Ospedale.", MessageKind.Good);
            });
            if (bullet)
                e.Option("Say you fell off the scooter", "They'll still see it's a bullet. Rarely works. Intimidation helps.", g =>
                {
                    bool believed = g.Rng.NextDouble() < 0.15 + patient.IntimidationSkill * 0.04;
                    if (!g.Org.Pay(HospitalFee, g, out var err)) { g.Log.Add(err, MessageKind.Bad); return; }
                    foreach (var i in patient.Health.Injuries)
                    {
                        if (i.IsMalnutrition) continue;
                        i.Tended = true;
                        i.TendQuality = Mathf.Max(i.TendQuality, 0.97f);
                    }
                    patient.Health.BloodLoss = Mathf.Max(0f, patient.Health.BloodLoss - 0.35f);
                    if (believed) g.Log.Add("The doctor looks at you for a long moment, and writes 'road accident'.", MessageKind.Good);
                    else { g.Org.AddEvidence(7f, g); g.Log.Add("Nobody believes the scooter story. The report goes in, with a note about your attitude.", MessageKind.Bad); }
                });
            return e.Option("Leave", "", g => { });
        }
    }
}
