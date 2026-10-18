namespace OSistema.Story
{
    /// <summary>
    /// The game speaks English. Neapolitan only shows as flavour: short, common sayings everyone
    /// would get ("Uè, guagliò!"), never whole speeches that crowd the screen.
    /// </summary>
    public static class Dialect
    {
        public const int MaxLength = 28;

        public static bool Show(string neapolitan) => !string.IsNullOrEmpty(neapolitan) && neapolitan.Length <= MaxLength;
    }
}
