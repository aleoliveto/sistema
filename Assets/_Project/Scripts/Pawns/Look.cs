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
        // The men's hair sheet: 0 buzz, 1 crop, 2 slicked, 3 side parting, 4 curls, 5 fade, 6 receding, 7 horseshoe,
        // 8 shaved, 9 bun, 10 tied back, 11 spiky, 12 grey short, 13 grey comb-over, 14 mullet (not used), 15 mohawk.
        static readonly int[] YoungHair = { 0, 1, 2, 3, 4, 5, 9, 10, 11, 15 };
        static readonly int[] OldHair = { 6, 7, 8, 12, 13 };
        static readonly int[] ShortHair = { 0, 1, 2, 3, 5, 8 };
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

        // Indexes into the later sheets (see CharacterArt): women 1xx, police 2xx, street people 3xx.
        public const int Women = 100, Uniforms = 200, Street = 300;

        /// <summary>Which force: 0 police, 1 carabinieri, 2 finanza, 3 plain clothes, 4 riot.</summary>
        public static Look Police(int seed, int force = -1)
        {
            var rng = new Random(seed * 271 + 9);
            if (force < 0) force = rng.NextDouble() < 0.55 ? 0 : rng.NextDouble() < 0.6 ? 1 : 2;
            int body = force switch
            {
                3 => 12 + rng.Next(2),
                4 => 14 + rng.Next(2),
                _ => force * 4 + rng.Next(4),
            };
            int hat = force switch
            {
                3 => -1,
                4 => Uniforms + 12 + rng.Next(2),
                _ => Uniforms + force * 4 + rng.Next(4),
            };
            var faces = new[] { 4, 7, 9, 12, 13, 14, 15, 11 };
            return new Look { Body = Uniforms + body, Head = rng.Next(16), Hair = Pick(ShortHair, rng), Hat = hat, Face = Uniforms + faces[rng.Next(faces.Length)] };
        }

        static readonly int[] YoungWomenBodies = { 2, 3, 4, 5, 6, 7, 9, 10, 11, 12, 14, 15 };
        static readonly int[] OldWomenBodies = { 0, 1, 8, 13 };
        // The women's hair sheet: 0 long, 1 long curly, 2 ponytail, 3 low bun, 4 grey bun, 5 grey perm, 6 bob, 7 blonde,
        // 8 red curls, 9 braid, 10 wavy, 11 headscarf, 12 pixie, 13 half-up, 14 big curls and 15 slick ponytail (not used).
        static readonly int[] YoungWomenHair = { 0, 1, 2, 3, 6, 7, 8, 9, 10, 12, 13 };
        static readonly int[] OldWomenHair = { 4, 5, 11 };
        static readonly int[] YoungWomenFaces = { 0, 2, 6, 8, 10 };
        static readonly int[] OldWomenFaces = { 1, 3, 5, 11, 12, 13, 14 };

        public static Look Woman(int seed, bool old = false)
        {
            var rng = new Random(seed * 6151 + 29);
            return new Look
            {
                Body = Women + Pick(old ? OldWomenBodies : YoungWomenBodies, rng),
                Head = rng.Next(16),
                Hair = Women + Pick(old ? OldWomenHair : YoungWomenHair, rng),
                Face = Women + Pick(old ? OldWomenFaces : YoungWomenFaces, rng),
            };
        }

        /// <summary>Someone at work in the street: a rider, a fishmonger, a waiter, a priest...</summary>
        public static Look Worker(int seed)
        {
            var rng = new Random(seed * 919 + 41);
            int body = rng.Next(16);
            bool woman = body == 13;
            return new Look
            {
                Body = Street + body,
                Head = rng.Next(16),
                Hair = woman ? Women + Pick(OldWomenHair, rng) : body == 6 ? Pick(OldHair, rng) : Pick(YoungHair, rng),
                Face = woman ? Women + Pick(OldWomenFaces, rng) : Pick(YoungFaces, rng),
            };
        }

        /// <summary>A passer-by: men and women, young and old, some in work clothes.</summary>
        public static Look Passerby(int seed)
        {
            var rng = new Random(seed * 3571 + 11);
            double r = rng.NextDouble();
            if (r < 0.4) return Woman(seed, rng.NextDouble() < 0.3);
            if (r < 0.6) return Worker(seed);
            return Young(seed);
        }
    }
}
