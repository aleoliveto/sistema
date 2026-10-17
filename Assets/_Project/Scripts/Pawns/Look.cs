using System;

namespace OSistema.Pawns
{
    /// <summary>
    /// What someone looks like: which clothes, head, hair, hat and chain they wear (indexes into the
    /// character art in Resources/Characters), and their portrait. -1 means none.
    /// </summary>
    [Serializable]
    public sealed class Look
    {
        public int Body, Head, Hair;
        public int Hat = -1, Neck = -1, Face = -1;

        public Look Clone() => (Look)MemberwiseClone();

        // Groups of parts, by the order they were drawn on the sheets.
        static readonly int[] CasualBodies = { 0, 2, 4, 5, 6, 7, 8, 10, 12, 13 };
        static readonly int[] TracksuitBodies = { 1, 5, 11 };
        static readonly int[] OldBodies = { 8, 9, 14 };
        static readonly int[] YoungHair = { 0, 1, 2, 3, 4, 7, 8, 9, 10, 11, 15 };
        static readonly int[] OldHair = { 5, 6, 12, 13 };
        static readonly int[] ShortHair = { 0, 2, 8, 10, 14 };
        static readonly int[] YoungFaces = { 0, 4, 6, 9, 14 };
        static readonly int[] OldFaces = { 5, 11, 13, 15 };

        static int Pick(int[] from, Random rng) => from[rng.Next(from.Length)];

        /// <summary>A young man of the rione: casual clothes, any young hairstyle, sometimes a cap or a chain.</summary>
        public static Look Young(int seed)
        {
            var rng = new Random(seed * 7919 + 17);
            return new Look
            {
                Body = Pick(CasualBodies, rng),
                Head = rng.Next(16),
                Hair = Pick(YoungHair, rng),
                Hat = rng.NextDouble() < 0.25 ? rng.Next(0, 3) : -1,
                Neck = rng.NextDouble() < 0.3 ? rng.Next(2) : -1,
                Face = Pick(YoungFaces, rng),
            };
        }

        public static Look Old(int seed)
        {
            var rng = new Random(seed * 104729 + 3);
            return new Look { Body = Pick(OldBodies, rng), Head = rng.Next(16), Hair = Pick(OldHair, rng), Hat = rng.NextDouble() < 0.3 ? 3 : -1, Face = Pick(OldFaces, rng) };
        }

        public static Look Rival(int seed)
        {
            var rng = new Random(seed * 31337 + 5);
            return new Look { Body = Pick(TracksuitBodies, rng), Head = rng.Next(16), Hair = Pick(ShortHair, rng), Hat = rng.NextDouble() < 0.35 ? 0 : -1, Neck = rng.NextDouble() < 0.4 ? 0 : -1 };
        }

        public static Look Police(int seed)
        {
            var rng = new Random(seed * 271 + 9);
            return new Look { Body = 15, Head = rng.Next(16), Hair = Pick(ShortHair, rng) };
        }
    }
}
