namespace CounterTopRearrange
{
    /// <summary>
    /// Builds the vision question for a chosen spawnable object.
    /// Has no Unity dependencies so the contract stays testable in isolation.
    /// </summary>
    public static class SpawnablePrompt
    {
        public const string DefaultObjectName = "book";

        /// <summary>
        /// Returns the question sent to Muse for <paramref name="objectDisplayName"/>.
        /// Blank names fall back to <see cref="DefaultObjectName"/>.
        /// </summary>
        public static string BuildQuestion(string objectDisplayName)
        {
            string name = string.IsNullOrWhiteSpace(objectDisplayName)
                ? DefaultObjectName
                : objectDisplayName.Trim();
            return "You are placing a " + name + " on the countertop in this photo. "
                + "The photo may contain overlaid UI text; ignore it. "
                + "Reply with exactly one JSON object and nothing else - "
                + "{\"x\": <0-1>, \"y\": <0-1>, \"reason\": \"<short reason>\"}. "
                + "x runs left-to-right, y runs top-to-bottom, pointing at a clear flat spot "
                + "on the counter where a " + name + " fits.";
        }
    }
}
