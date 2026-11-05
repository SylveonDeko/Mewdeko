#nullable enable

using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a user's own rank card background.
/// </summary>
[Table("UserXpBackgrounds")]
public class UserXpBackground
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the user ID.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the image: an image URL, or a file name prefixed with local: for an upload.
    /// </summary>
    [Column("Image")]
    public string Image { get; set; } = "";

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}

/// <summary>
///     Represents a guild's suggestion vote colour setting.
/// </summary>
[Table("SuggestionVoteColors")]
public class SuggestionVoteColor
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets how many votes one side has to lead by to recolour the embed, or 0 for off.
    /// </summary>
    [Column("Threshold")]
    public int Threshold { get; set; }

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}

/// <summary>
///     Represents one member's daily dice roll.
/// </summary>
[Table("DailyDiceRolls")]
public class DailyDiceRoll
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the member's user ID.
    /// </summary>
    [Column("UserId")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the UTC day the roll counts for.
    /// </summary>
    [Column("RollDate", DataType = LinqToDB.DataType.Date)]
    public DateTime RollDate { get; set; }

    /// <summary>
    ///     Gets or sets the rolled number.
    /// </summary>
    [Column("Roll")]
    public int Roll { get; set; }

    /// <summary>
    ///     Gets or sets when the roll was made.
    /// </summary>
    [Column("RolledAt")]
    public DateTime RolledAt { get; set; }
}

/// <summary>
///     Represents a channel that gets free game alerts.
/// </summary>
[Table("FreeGameAlerts")]
public class FreeGameAlert
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the channel alerts are posted in.
    /// </summary>
    [Column("ChannelId")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the role pinged with each alert, or null for no ping.
    /// </summary>
    [Column("RoleId")]
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     Gets or sets whether Epic Games Store games are announced.
    /// </summary>
    [Column("Epic")]
    public bool Epic { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether Steam games are announced.
    /// </summary>
    [Column("Steam")]
    public bool Steam { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether GOG games are announced.
    /// </summary>
    [Column("Gog")]
    public bool Gog { get; set; } = true;

    /// <summary>
    ///     Gets or sets when the row was created.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}

/// <summary>
///     Represents a free game that has already been announced.
/// </summary>
[Table("FreeGameAnnounced")]
public class FreeGameAnnounced
{
    /// <summary>
    ///     Gets or sets the database row identifier.
    /// </summary>
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the store name.
    /// </summary>
    [Column("Store")]
    public string Store { get; set; } = "";

    /// <summary>
    ///     Gets or sets the store's own identifier for the game.
    /// </summary>
    [Column("GameKey")]
    public string GameKey { get; set; } = "";

    /// <summary>
    ///     Gets or sets when the game was recorded.
    /// </summary>
    [Column("DateAdded")]
    public DateTime DateAdded { get; set; }
}
