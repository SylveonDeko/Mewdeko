/// <summary>
/// Represents data for a Hearthstone card.
/// </summary>
public class HearthstoneCardData
{
    /// <summary>
    /// Gets or sets the text describing the card's abilities.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the flavor text of the card, providing lore or humorous commentary.
    /// </summary>
    public string Flavor { get; set; }

    /// <summary>
    /// Indicates whether the card is collectible and can be found in Hearthstone card packs.
    /// </summary>
    public bool Collectible { get; set; }

    /// <summary>
    /// Gets or sets the URL to the image of the card.
    /// </summary>
    public string Img { get; set; }

    /// <summary>
    /// Gets or sets the URL to the golden version of the card's image.
    /// </summary>
    public string ImgGold { get; set; }

    /// <summary>
    /// Gets or sets the class that the card is associated with, if any.
    /// </summary>
    public string PlayerClass { get; set; }
}