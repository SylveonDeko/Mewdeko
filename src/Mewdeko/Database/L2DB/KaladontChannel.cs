using LinqToDB.Mapping;

namespace DataModel;

/// <summary>
///     Represents a Kaladont game channel configuration.
/// </summary>
[Table("kaladont_channels")]
public class KaladontChannel
{
    /// <summary>
    ///     Gets or sets the unique identifier for the kaladont channel.
    /// </summary>
    [Column("id")]
    [PrimaryKey]
    [Identity]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord guild ID.
    /// </summary>
    [Column("guild_id")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the Discord channel ID.
    /// </summary>
    [Column("channel_id")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the language code for the dictionary.
    /// </summary>
    [Column("language")]
    public string Language { get; set; } = "en";

    /// <summary>
    ///     Gets or sets the game mode (0 = Normal, 1 = Endless).
    /// </summary>
    [Column("mode")]
    public int Mode { get; set; }

    /// <summary>
    ///     Gets or sets the turn time in seconds.
    /// </summary>
    [Column("turn_time")]
    public int TurnTime { get; set; } = 30;

    /// <summary>
    ///     Gets or sets the current word in play.
    /// </summary>
    [Column("current_word")]
    public string CurrentWord { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets whether the channel is active.
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Gets or sets when the channel was created.
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets the total words played.
    /// </summary>
    [Column("total_words")]
    public long TotalWords { get; set; }

    /// <summary>
    ///     Gets or sets the current players (JSON array of user IDs).
    /// </summary>
    [Column("current_players")]
    public string? CurrentPlayers { get; set; }
}

/// <summary>
///     Represents user statistics for Kaladont games.
/// </summary>
[Table("kaladont_stats")]
public class KaladontStats
{
    /// <summary>
    ///     Gets or sets the unique identifier.
    /// </summary>
    [Column("id")]
    [PrimaryKey]
    [Identity]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord channel ID.
    /// </summary>
    [Column("channel_id")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the Discord user ID.
    /// </summary>
    [Column("user_id")]
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the number of words played by this user.
    /// </summary>
    [Column("words_count")]
    public long WordsCount { get; set; }

    /// <summary>
    ///     Gets or sets the number of wins.
    /// </summary>
    [Column("wins")]
    public int Wins { get; set; }

    /// <summary>
    ///     Gets or sets the number of eliminations.
    /// </summary>
    [Column("eliminations")]
    public int Eliminations { get; set; }

    /// <summary>
    ///     Gets or sets when the user last played.
    /// </summary>
    [Column("last_played")]
    public DateTime LastPlayed { get; set; } = DateTime.UtcNow;
}

/// <summary>
///     Represents a completed Kaladont game.
/// </summary>
[Table("kaladont_games")]
public class KaladontGameRecord
{
    /// <summary>
    ///     Gets or sets the unique identifier.
    /// </summary>
    [Column("id")]
    [PrimaryKey]
    [Identity]
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the Discord channel ID.
    /// </summary>
    [Column("channel_id")]
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets when the game started.
    /// </summary>
    [Column("started_at")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets when the game ended.
    /// </summary>
    [Column("ended_at")]
    public DateTime? EndedAt { get; set; }

    /// <summary>
    ///     Gets or sets the winner's user ID.
    /// </summary>
    [Column("winner_id")]
    public ulong? WinnerId { get; set; }

    /// <summary>
    ///     Gets or sets the total words played in this game.
    /// </summary>
    [Column("total_words")]
    public int TotalWords { get; set; }

    /// <summary>
    ///     Gets or sets the total players who participated.
    /// </summary>
    [Column("total_players")]
    public int TotalPlayers { get; set; }

    /// <summary>
    ///     Gets or sets the game mode (0 = Normal, 1 = Endless).
    /// </summary>
    [Column("mode")]
    public int Mode { get; set; }

    /// <summary>
    ///     Gets or sets the language used.
    /// </summary>
    [Column("language")]
    public string Language { get; set; } = "en";
}