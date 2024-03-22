namespace Mewdeko.Common
{
    /// <summary>
    /// Represents a tone tag used for tagging messages or content with a specific tone or theme.
    /// </summary>
    public class ToneTag
    {
        /// <summary>
        /// Gets or sets the source of the tone tag, if available.
        /// </summary>
        public ToneTagSource? Source { get; set; }

        /// <summary>
        /// Gets or sets the default name of the tone tag.
        /// </summary>
        public string DefaultName { get; set; }

        /// <summary>
        /// Gets or sets the default short name of the tone tag.
        /// </summary>
        public string DefaultShortName { get; set; }

        /// <summary>
        /// Gets or sets the description of the tone tag.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the list of aliases for the tone tag.
        /// </summary>
        public List<string> Aliases { get; set; }

        /// <summary>
        /// Gets or sets the list of short aliases for the tone tag.
        /// </summary>
        public List<string> ShortAliases { get; set; }

        /// <summary>
        /// Gets all values associated with the tone tag, including aliases and default names.
        /// </summary>
        /// <returns>A list of all values associated with the tone tag.</returns>
        public List<string> GetAllValues()
            => Aliases.Concat(ShortAliases).Append(DefaultName).Append(DefaultShortName).ToList();
    }

    /// <summary>
    /// Represents the source of a tone tag, including the title and URL.
    /// </summary>
    public record ToneTagSource(string Title, string? Url);
}