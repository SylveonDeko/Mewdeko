namespace Mewdeko.Database.Models;

/// <summary>
/// Represents an automatically executed command.
/// </summary>
public class AutoCommand : DbEntity
{
    /// <summary>
    /// Gets or sets the text of the command to be executed.
    /// </summary>
    public string CommandText { get; set; }

    /// <summary>
    /// Gets or sets the ID of the channel where the command should be executed.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the name of the channel where the command should be executed.
    /// </summary>
    public string ChannelName { get; set; }

    /// <summary>
    /// Gets or sets the ID of the guild where this auto-command is configured.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets or sets the name of the guild where this auto-command is configured.
    /// </summary>
    public string GuildName { get; set; }

    /// <summary>
    /// Gets or sets the ID of the voice channel associated with this auto-command.
    /// </summary>
    public ulong? VoiceChannelId { get; set; }

    /// <summary>
    /// Gets or sets the name of the voice channel associated with this auto-command.
    /// </summary>
    public string VoiceChannelName { get; set; }

    /// <summary>
    /// Gets or sets the interval (in seconds) at which the command should be executed.
    /// </summary>
    public int Interval { get; set; }
}