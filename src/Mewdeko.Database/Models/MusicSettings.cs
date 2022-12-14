namespace Mewdeko.Database.Models;

public class MusicPlayerSettings
{
    /// <summary>
    ///     Auto generated Id
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Id of the guild
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Queue repeat type
    /// </summary>
    public PlayerRepeatType PlayerRepeat { get; set; } = PlayerRepeatType.Queue;

    /// <summary>
    ///     Channel id the bot will always try to send track related messages to
    /// </summary>
    public ulong? MusicChannelId { get; set; } = null;

    /// <summary>
    ///     Default volume player will be created with
    /// </summary>
    public int Volume { get; set; } = 100;

    /// <summary>
    ///     Whether the bot should auto disconnect from the voice channel once the queue is done
    ///     This only has effect if
    /// </summary>
    public AutoDisconnect AutoDisconnect { get; set; } = AutoDisconnect.Voice;

    public int AutoPlay { get; set; } = 0;
}

public enum AutoDisconnect
{
    None,
    Voice,
    Queue,
    Either
}

public enum PlayerRepeatType
{
    None,
    Track,
    Queue,
    Song = 1,
    All = 2,
    Off = 0
}