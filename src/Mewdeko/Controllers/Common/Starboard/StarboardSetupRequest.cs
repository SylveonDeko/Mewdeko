namespace Mewdeko.Controllers.Common.Starboard;

/// <summary>
///     Request model for creating a starboard together with its channel
/// </summary>
public class StarboardSetupRequest
{
    /// <summary>
    ///     The name of the channel to create
    /// </summary>
    public string Name { get; set; } = "starboard";

    /// <summary>
    ///     The emote to use for this starboard
    /// </summary>
    public string Emote { get; set; } = "⭐";

    /// <summary>
    ///     The number of reactions required to post a message
    /// </summary>
    public int Threshold { get; set; } = 1;
}

/// <summary>
///     Request model for the minimum and maximum age a message can be starred at
/// </summary>
public class StarboardMessageAgeRequest
{
    /// <summary>
    ///     The minimum message age in minutes, 0 for none, or null to keep the current value
    /// </summary>
    public int? MinMinutes { get; set; }

    /// <summary>
    ///     The maximum message age in minutes, 0 for none, or null to keep the current value
    /// </summary>
    public int? MaxMinutes { get; set; }
}

/// <summary>
///     Request model for sending a preview of a starboard post
/// </summary>
public class StarboardDesignTestRequest
{
    /// <summary>
    ///     The channel to send the preview to and to find the message in
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The message to build the preview from, or null for the latest member message in the channel
    /// </summary>
    public ulong? MessageId { get; set; }
}
