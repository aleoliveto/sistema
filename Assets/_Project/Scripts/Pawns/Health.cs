using System.Collections.Generic;
using UnityEngine;

namespace OSistema.Pawns
{
    public sealed class Injury
    {
        public string Label;      // "Gunshot wound", "Cut", "Malnutrition"
        public string Part;       // "Torso", "Left hand"…
        public float Severity;    // 0..1
        public float BleedPerDay; // blood lost per day while untended
        public bool Tended;
        public float TendQuality; // 0..1, reduces bleeding and speeds healing

        public bool IsMalnutrition => Label == Health.MalnutritionLabel;
    }

    /// <summary>
    /// Lightweight injury model. Hospitals are a risk for a camorrista (gunshot wounds get reported),
    /// so most care happens at home with whatever medicine the crew has.
    /// </summary>
    public sealed class Health
    {
        public const string MalnutritionLabel = "Malnutrition";
        const float PerDay = 1f / 60000f;

        public readonly List<Injury> Injuries = new List<Injury>();
        public float BloodLoss;

        public float Pain
        {
            get
            {
                float p = 0f;
                foreach (var i in Injuries) p += i.Severity * (i.IsMalnutrition ? 0.4f : 0.7f);
                return Mathf.Clamp01(p);
            }
        }

        public float Malnutrition
        {
            get
            {
                foreach (var i in Injuries) if (i.IsMalnutrition) return i.Severity;
                return 0f;
            }
        }

        public bool Downed => Pain >= 0.8f || BloodLoss >= 0.6f || Malnutrition >= 0.7f;
        public bool IsDeadly => BloodLoss >= 1f || Malnutrition >= 1f;
        public bool Healthy => Injuries.Count == 0 && BloodLoss <= 0.01f;

        public bool NeedsTending
        {
            get
            {
                foreach (var i in Injuries) if (!i.Tended && !i.IsMalnutrition) return true;
                return false;
            }
        }

        public float BleedRatePerDay
        {
            get
            {
                float b = 0f;
                foreach (var i in Injuries) b += i.BleedPerDay * (i.Tended ? 1f - i.TendQuality : 1f);
                return b;
            }
        }

        public void AddInjury(string label, string part, float severity, float bleedPerDay)
        {
            Injuries.Add(new Injury { Label = label, Part = part, Severity = severity, BleedPerDay = bleedPerDay });
        }

        public void Tick(Pawn pawn)
        {
            float bleed = BleedRatePerDay;
            if (bleed > 0.001f) BloodLoss = Mathf.Min(1f, BloodLoss + bleed * PerDay);
            else BloodLoss = Mathf.Max(0f, BloodLoss - 0.6f * PerDay);

            UpdateMalnutrition(pawn.Needs.Food <= 0f);

            float restBonus = pawn.Sleep == SleepState.InBed ? 1.5f : 1f;
            for (int i = Injuries.Count - 1; i >= 0; i--)
            {
                var inj = Injuries[i];
                if (inj.IsMalnutrition) continue;
                float heal = inj.Tended ? 0.35f + 0.4f * inj.TendQuality : 0.08f;
                inj.Severity -= heal * restBonus * PerDay;
                if (inj.Severity <= 0f) Injuries.RemoveAt(i);
            }
        }

        void UpdateMalnutrition(bool starving)
        {
            var mal = Injuries.Find(i => i.IsMalnutrition);
            if (starving)
            {
                if (mal == null) Injuries.Add(mal = new Injury { Label = MalnutritionLabel, Part = "Whole body", Tended = true });
                mal.Severity = Mathf.Min(1f, mal.Severity + 0.6f * PerDay);
            }
            else if (mal != null)
            {
                mal.Severity -= 0.8f * PerDay;
                if (mal.Severity <= 0f) Injuries.Remove(mal);
            }
        }
    }
}
