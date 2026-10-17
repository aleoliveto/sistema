namespace OSistema.Story
{
    /// <summary>
    /// Name pools for generated characters, places and businesses. Everything is fictional:
    /// surnames of real clans are deliberately left out.
    /// </summary>
    public static class Names
    {
        public static readonly string[] FirstNames =
        {
            "Gennaro", "Ciro", "Salvatore", "Vincenzo", "Antonio", "Pasquale", "Raffaele", "Carmine",
            "Luigi", "Giuseppe", "Domenico", "Francesco", "Mario", "Enzo", "Tonino", "Peppe",
            "Lello", "Gaetano", "Alfonso", "Biagio", "Rosario", "Nunzio", "Ernesto", "Aniello",
        };

        public static readonly string[] Surnames =
        {
            "Esposito", "Romano", "De Rosa", "Ferrara", "Capasso", "Scognamiglio", "Sorrentino", "Cozzolino",
            "Aprea", "Ruggiero", "Imparato", "Langella", "Vitiello", "Ascione", "Iaccarino", "Carotenuto",
            "Di Maio", "Esposito", "Formisano", "Liguori", "Mennella", "Palumbo", "Somma", "Visone",
        };

        public static readonly string[] Nicknames =
        {
            "'o Chiatto", "'o Biondo", "'o Nano", "'o Lupo", "'o Professore", "Pallino", "'o Scugnizzo",
            "'o Mellone", "Tarzan", "'o Cinese", "'o Muto", "'o Ricciulillo", "'o Pazzariello", "Capa 'e Fierro",
            "'o Sorcio", "'o Malamente", "Bambulella", "'o Turco", "'o Zingaro", "Sciarpetta",
        };

        public static readonly string[] RivalSurnames = { "Capuozzo", "Tufano", "Pagliuca", "Cennamo", "Starace", "Merolla" };

        public static readonly string[] Zones =
        {
            "Rione Corallo", "Case Gialle", "Vico Lanterna", "Rione Sette Palazzi", "Borgo 'e Ciucci",
            "Rione Cupa Nera", "Vico Tre Re", "Rione Bellavista", "Parco Aurora", "Rione 'e Pescatori",
            "Case Rosse", "Vico Sant'Aniello", "Rione Fosso", "Lotto Zero", "Rione Pietrabianca",
        };

        public static readonly string[] Bars = { "Vesuvio", "Sport", "Centrale", "Sole", "Aurora", "Stella", "Gambrinus Piccolo" };
        public static readonly string[] Saints = { "Santa Lucia", "San Gennaro", "Sant'Anna", "San Ciro", "Santa Rita" };
        public static readonly string[] PizzaioloNames = { "Gino", "Totò", "Ciccio", "Nannina", "Michele", "Concetta", "Tonino" };

        public static string Pick(string[] pool, System.Random rng) => pool[rng.Next(pool.Length)];
    }
}
