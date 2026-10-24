namespace OSistema.Combat
{
    public enum Gun { Pistol, Beretta, Kalash }

    /// <summary>
    /// What a gun does. The old pistol is what's around; a Beretta is better in every way; a Kalashnikov
    /// empties a street, costs a fortune, and everyone in the rione hears it.
    /// </summary>
    public readonly struct GunStats
    {
        public readonly string Name, Note;
        public readonly int Magazine, Price, Rank;
        public readonly float PlayerRange, AiRange, Damage, Spread;
        public readonly int Delay;     // ticks between shots
        public readonly bool Automatic;
        public readonly float LengthOnBody;

        public GunStats(string name, string note, int mag, int price, int rank, float playerRange, float aiRange, float damage, float spread, int delay, bool auto, float length)
        {
            Name = name; Note = note; Magazine = mag; Price = price; Rank = rank;
            PlayerRange = playerRange; AiRange = aiRange; Damage = damage; Spread = spread; Delay = delay; Automatic = auto; LengthOnBody = length;
        }
    }

    public static class Guns
    {
        static readonly GunStats[] all =
        {
            new GunStats("Pistol", "An old semi-automatic off the boats. 8 rounds.", 8, 600, 1, 12f, 9f, 1f, 0f, 11, false, 0.3f),
            new GunStats("Beretta 92", "Fifteen rounds, a longer reach, and it hits harder.", 15, 1800, 1, 13.5f, 10f, 1.15f, -0.6f, 9, false, 0.34f),
            new GunStats("Kalashnikov", "Thirty rounds on full automatic: hold the trigger. Kicks hard, and the whole rione hears it.", 30, 14000, 2, 16f, 13f, 1.35f, 1.4f, 6, true, 0.6f),
        };

        public static GunStats Of(Gun g) => all[(int)g < 0 || (int)g >= all.Length ? 0 : (int)g];
        public static readonly Gun[] All = { Gun.Pistol, Gun.Beretta, Gun.Kalash };
    }
}
