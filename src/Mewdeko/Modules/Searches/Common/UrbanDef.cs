namespace Mewdeko.Modules.Searches.Common
{
    /// <summary>
    /// Represents the response from the Urban Dictionary API, including a list of definitions.
    /// </summary>
    public class UrbanResponse
    {
        /// <summary>
        /// Gets or sets the list of definitions.
        /// </summary>
        public UrbanDef[] List { get; set; }
    }

    /// <summary>
    /// Represents a definition from the Urban Dictionary.
    /// </summary>
    public class UrbanDef
    {
        /// <summary>
        /// Gets or sets the word being defined.
        /// </summary>
        public string Word { get; set; }

        /// <summary>
        /// Gets or sets the definition of the word.
        /// </summary>
        public string Definition { get; set; }

        /// <summary>
        /// Gets or sets the permalink to the definition on the Urban Dictionary website.
        /// </summary>
        public string Permalink { get; set; }
    }
}