namespace Mewdeko.Modules.Searches.Common;

/// <summary>
///     Represents Magic: The Gathering card data.
/// </summary>
public class MtgData
{
    /// <summary>
    ///     Gets or sets the name of the Magic: The Gathering card.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     Gets or sets the description or text of the Magic: The Gathering card.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    ///     Gets or sets the URL of the image associated with the Magic: The Gathering card.
    /// </summary>
    public string ImageUrl { get; set; }

    /// <summary>
    ///     Gets or sets the URL of the store page for purchasing the Magic: The Gathering card.
    /// </summary>
    public string StoreUrl { get; set; }

    /// <summary>
    ///     Gets or sets the types or categories of the Magic: The Gathering card.
    /// </summary>
    public string Types { get; set; }

    /// <summary>
    ///     Gets or sets the mana cost of the Magic: The Gathering card.
    /// </summary>
    public string ManaCost { get; set; }
}

/// <summary>
///     Represents the response containing Magic: The Gathering card data.
/// </summary>
public class MtgResponse
{
    /// <summary>
    ///     Gets or sets the list of Magic: The Gathering cards.
    /// </summary>
    public List<Data> Cards { get; set; }

    /// <summary>
    ///     Represents the individual data for a Magic: The Gathering card.
    /// </summary>
    public class Data
    {
        /// <summary>
        ///     Gets or sets the name of the Magic: The Gathering card.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Gets or sets the mana cost of the Magic: The Gathering card.
        /// </summary>
        public string ManaCost { get; set; }

        /// <summary>
        ///     Gets or sets the text or description of the Magic: The Gathering card.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        ///     Gets or sets the types or categories of the Magic: The Gathering card.
        /// </summary>
        public List<string> Types { get; set; }

        /// <summary>
        ///     Gets or sets the URL of the image associated with the Magic: The Gathering card.
        /// </summary>
        public string ImageUrl { get; set; }
    }
}