using System.Text.Json.Serialization;
using DataModel;
using Mewdeko.Common.Cards;

namespace Mewdeko.Controllers.Common.WelcomeCards;

/// <summary>
///     A guild's welcome card for one message, with what the designer needs.
/// </summary>
public class WelcomeCardResponse
{
    /// <summary>
    ///     Where the card appears: None, EmbedImage or Attached.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CardPlacement Placement { get; set; }

    /// <summary>
    ///     The card's design.
    /// </summary>
    public required CardTemplate Template { get; set; }

    /// <summary>
    ///     The classic layout, which resets and new elements start from.
    /// </summary>
    public required CardTemplate BuiltIn { get; set; }

    /// <summary>
    ///     The server's stored images, oldest first.
    /// </summary>
    public List<WelcomeCardImageResponse> Images { get; set; } = [];

    /// <summary>
    ///     The placeholders text can use.
    /// </summary>
    public List<string> Placeholders { get; set; } = [];

    /// <summary>
    ///     The server's palette: primary, secondary, accent, text, muted and background as hex.
    /// </summary>
    public Dictionary<string, string> Palette { get; set; } = [];

    /// <summary>
    ///     The limits a design is held to.
    /// </summary>
    public WelcomeCardLimits Limits { get; set; } = new();
}

/// <summary>
///     The limits a welcome card design is held to.
/// </summary>
public class WelcomeCardLimits
{
    /// <summary>
    ///     Smallest card width.
    /// </summary>
    public int MinWidth { get; set; } = CardTemplate.MinWidth;

    /// <summary>
    ///     Largest card width.
    /// </summary>
    public int MaxWidth { get; set; } = CardTemplate.MaxWidth;

    /// <summary>
    ///     Smallest card height.
    /// </summary>
    public int MinHeight { get; set; } = CardTemplate.MinHeight;

    /// <summary>
    ///     Largest card height.
    /// </summary>
    public int MaxHeight { get; set; } = CardTemplate.MaxHeight;

    /// <summary>
    ///     Most elements a card holds.
    /// </summary>
    public int MaxElements { get; set; } = CardTemplate.MaxElements;

    /// <summary>
    ///     Longest text an element holds.
    /// </summary>
    public int MaxText { get; set; } = CardTemplate.MaxText;

    /// <summary>
    ///     Most images a server keeps.
    /// </summary>
    public int MaxImages { get; set; } = Modules.MultiGreets.Services.WelcomeCardService.MaxImages;
}

/// <summary>
///     A stored image a design can use.
/// </summary>
public class WelcomeCardImageResponse
{
    /// <summary>
    ///     The image ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The value designs link it with: <c>upload:id</c>.
    /// </summary>
    public string Icon { get; set; } = "";

    /// <summary>
    ///     A small picture of it as a data URI.
    /// </summary>
    public string Url { get; set; } = "";
}

/// <summary>
///     A starter layout.
/// </summary>
public class WelcomeCardStarterResponse
{
    /// <summary>
    ///     Its short name.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     What it is called.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The layout.
    /// </summary>
    public required CardTemplate Template { get; set; }
}

/// <summary>
///     A request to save a card's state, its design, or both.
/// </summary>
public class WelcomeCardSaveRequest
{
    /// <summary>
    ///     Where the card appears: None, EmbedImage or Attached, or null to keep it.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CardPlacement? Placement { get; set; }

    /// <summary>
    ///     The design, or null to keep it.
    /// </summary>
    public CardTemplate? Template { get; set; }
}

/// <summary>
///     A request to draw a design before it is saved.
/// </summary>
public class WelcomeCardPreviewRequest
{
    /// <summary>
    ///     The design as edited, or null to draw the saved one.
    /// </summary>
    public CardTemplate? Template { get; set; }

    /// <summary>
    ///     An element to draw without its text, for typing over it.
    /// </summary>
    public string? EditingId { get; set; }

    /// <summary>
    ///     The member to draw the card for. The dashboard user, then the bot, are used when omitted or not found.
    /// </summary>
    public string? UserId { get; set; }
}

/// <summary>
///     A drawn preview with where each element landed.
/// </summary>
public class WelcomeCardPreviewResponse
{
    /// <summary>
    ///     The card as a data URI.
    /// </summary>
    public string Image { get; set; } = "";

    /// <summary>
    ///     The card's width without the margin.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    ///     The card's height without the margin.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    ///     Transparent space around the card on every side.
    /// </summary>
    public int Margin { get; set; }

    /// <summary>
    ///     Where each element landed.
    /// </summary>
    public List<WelcomeCardBoxResponse> Layout { get; set; } = [];

    /// <summary>
    ///     The design as drawn, after being brought within limits.
    /// </summary>
    public required CardTemplate Template { get; set; }
}

/// <summary>
///     Where one element landed on a drawn card, in card pixels.
/// </summary>
public class WelcomeCardBoxResponse
{
    /// <summary>
    ///     The element ID.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     Left edge.
    /// </summary>
    public float X { get; set; }

    /// <summary>
    ///     Top edge.
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    ///     Width.
    /// </summary>
    public float W { get; set; }

    /// <summary>
    ///     Height.
    /// </summary>
    public float H { get; set; }

    /// <summary>
    ///     Whether it was drawn on this card.
    /// </summary>
    public bool Drawn { get; set; }
}

/// <summary>
///     A request to store an image sent as base64 or a data URI.
/// </summary>
public class WelcomeCardImageRequest
{
    /// <summary>
    ///     The image as base64 or a data URI.
    /// </summary>
    public string Data { get; set; } = "";
}
