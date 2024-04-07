namespace Mewdeko.Modules.Searches.Common
{
    /// <summary>
    /// Represents a model for Wikipedia API response.
    /// </summary>
    public class WikipediaApiModel
    {
        /// <summary>
        /// Gets or sets the query result from Wikipedia API.
        /// </summary>
        public WikipediaQuery Query { get; set; }

        /// <summary>
        /// Represents a Wikipedia query result containing pages.
        /// </summary>
        public class WikipediaQuery
        {
            /// <summary>
            /// Gets or sets an array of Wikipedia pages.
            /// </summary>
            public WikipediaPage[] Pages { get; set; }

            /// <summary>
            /// Represents a Wikipedia page.
            /// </summary>
            public class WikipediaPage
            {
                /// <summary>
                /// Gets or sets a value indicating whether the page is missing.
                /// </summary>
                public bool Missing { get; set; } = false;

                /// <summary>
                /// Gets or sets the full URL of the page.
                /// </summary>
                public string FullUrl { get; set; }
            }
        }
    }
}