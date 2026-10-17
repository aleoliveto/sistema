using OSistema.Pawns;

namespace OSistema.Core
{
    public enum Background { MammasBoy, KidFromTheVele, OutOfJuvie }

    /// <summary>Who the player chose to be at the start: name, looks, skills and where he comes from.</summary>
    public sealed class CharacterSetup
    {
        public const int SkillPoints = 12;
        public const int MaxSkill = 8;

        public string FirstName = "Gennaro", LastName = "Iaccarino", Nickname = "'o Russo";
        public Look Look = new Look { Body = 0, Head = 2, Hair = 1, Face = 0 };
        public Background Background = Background.MammasBoy;

        // construction (handiwork), cooking, medicine, intimidation, awareness, shooting
        public readonly int[] Skills = { 2, 2, 1, 3, 3, 1 };

        public static readonly string[] SkillNames = { "Handiwork", "Cooking", "Medicine", "Intimidation", "Awareness", "Shooting" };
        public static readonly string[] SkillHints =
        {
            "Building, fixing, rolling cigarettes.",
            "Meals for the crew; a steady hand at the bagging table.",
            "Patching up wounds.",
            "Debts, pizzo, and fights that end before they start.",
            "Spotting the police, the boat, a lie.",
            "Hitting what you aim at.",
        };

        public int PointsUsed
        {
            get
            {
                int n = 0;
                foreach (var s in Skills) n += s;
                return n;
            }
        }

        public static string BackgroundName(Background b) => b switch
        {
            Background.KidFromTheVele => "Kid from the Vele",
            Background.OutOfJuvie => "Out of juvenile prison",
            _ => "Mamma's boy",
        };

        public static string BackgroundText(Background b) => b switch
        {
            Background.KidFromTheVele => "Grew up on the walkways of the Vele. Knows every escape route: +2 awareness, a little respect already.",
            Background.OutOfJuvie => "Two years in Nisida. People step aside: +2 intimidation, +1 shooting, respect, but the police know his face.",
            _ => "Mamma still irons his shirts. A little money put aside and nobody looking at him yet: +€80, no heat.",
        };
    }
}
