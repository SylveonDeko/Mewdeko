#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Which message a welcome card is attached to.
/// </summary>
public enum WelcomeCardTarget
{
    /// <summary>
    ///     The greet messages sent when a member joins.
    /// </summary>
    Greet = 0,

    /// <summary>
    ///     The bye message sent when a member leaves.
    /// </summary>
    Bye = 1,

    /// <summary>
    ///     The greet message sent in a direct message when a member joins.
    /// </summary>
    DmGreet = 2,

    /// <summary>
    ///     The message sent when a member boosts the guild.
    /// </summary>
    Boost = 3,

    /// <summary>
    ///     The messages sent when a member is given a role.
    /// </summary>
    RoleGreet = 4
}

/// <summary>
///     Where a message card appears.
/// </summary>
public enum CardPlacement
{
    /// <summary>
    ///     The message has no card.
    /// </summary>
    None = 0,

    /// <summary>
    ///     The card is the embed's image.
    /// </summary>
    EmbedImage = 1,

    /// <summary>
    ///     The card is sent as a file with the message.
    /// </summary>
    Attached = 2
}

/// <summary>
///     Represents the card of one member event message: a multi-greet, a role greet, or the bye, DM greet or boost
///     message.
/// </summary>
[Table("MessageCards")]
public class MessageCard
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID the card belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the kind of message the card is for.
    /// </summary>
    [Column("Target")]
    public WelcomeCardTarget Target { get; set; }

    /// <summary>
    ///     Gets or sets the multi-greet or role greet ID, or 0 for the bye, DM greet and boost messages.
    /// </summary>
    [Column("OwnerId")]
    public int OwnerId { get; set; }

    /// <summary>
    ///     Gets or sets where the card appears.
    /// </summary>
    [Column("Placement")]
    public CardPlacement Placement { get; set; }

    /// <summary>
    ///     Gets or sets the card design as card template JSON, or null for the classic layout.
    /// </summary>
    [Column("Template")]
    public string? Template { get; set; }

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}

/// <summary>
///     The shape the avatar is cut to on a welcome card.
/// </summary>
public enum WelcomeCardAvatarShape
{
    /// <summary>
    ///     A circle.
    /// </summary>
    Circle = 0,

    /// <summary>
    ///     A square with rounded corners.
    /// </summary>
    Rounded = 1,

    /// <summary>
    ///     A square.
    /// </summary>
    Square = 2
}

/// <summary>
///     Represents how a guild's welcome card looks for one kind of message.
/// </summary>
[Table("WelcomeCardSettings")]
public class WelcomeCardSetting
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID these settings belong to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets which message the card is attached to.
    /// </summary>
    [Column("Target")]
    public WelcomeCardTarget Target { get; set; }

    /// <summary>
    ///     Gets or sets whether the card is attached to messages.
    /// </summary>
    [Column("Enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    ///     Gets or sets the background colour as hex.
    /// </summary>
    [Column("BackgroundColor")]
    public string BackgroundColor { get; set; } = "#23272A";

    /// <summary>
    ///     Gets or sets the background image as PNG, or null for the plain colour.
    /// </summary>
    [Column("BackgroundData")]
    public byte[]? BackgroundData { get; set; }

    /// <summary>
    ///     Gets or sets how dark the overlay on the background is, from 0 to 100.
    /// </summary>
    [Column("OverlayOpacity")]
    public int OverlayOpacity { get; set; } = 40;

    /// <summary>
    ///     Gets or sets the title text, which can use placeholders.
    /// </summary>
    [Column("Title")]
    public string Title { get; set; } = "Welcome, %user.name%";

    /// <summary>
    ///     Gets or sets the subtitle text, which can use placeholders.
    /// </summary>
    [Column("Subtitle")]
    public string Subtitle { get; set; } = "You are member #%server.members% of %server.name%";

    /// <summary>
    ///     Gets or sets the font name.
    /// </summary>
    [Column("FontName")]
    public string FontName { get; set; } = "noto-bold";

    /// <summary>
    ///     Gets or sets the text colour as hex.
    /// </summary>
    [Column("TextColor")]
    public string TextColor { get; set; } = "#FFFFFF";

    /// <summary>
    ///     Gets or sets the shape the avatar is cut to.
    /// </summary>
    [Column("AvatarShape")]
    public WelcomeCardAvatarShape AvatarShape { get; set; }

    /// <summary>
    ///     Gets or sets the card design as card template JSON, or null for a card that still draws from the columns above.
    /// </summary>
    [Column("Template")]
    public string? Template { get; set; }

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}

/// <summary>
///     Represents an image a guild uploaded for its welcome card designs.
/// </summary>
[Table("WelcomeCardImages")]
public class WelcomeCardImage
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID the image belongs to.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the image as PNG.
    /// </summary>
    [Column("Data")]
    public byte[] Data { get; set; } = [];

    /// <summary>
    ///     Gets or sets a small PNG of the image for pickers.
    /// </summary>
    [Column("Thumbnail")]
    public byte[] Thumbnail { get; set; } = [];

    /// <summary>
    ///     Gets or sets when the image was uploaded.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
