namespace Mewdeko.Modules.Utility.Common;

/// <summary>
///     Stores information for a deleted message snipe, including the message, user, and channel details.
/// </summary>
public class SnipeStore
{
    /// <summary>
    ///     Gets or sets the ID of the guild where the message was sent.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the user who sent the message.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the channel where the message was sent.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     Gets or sets the content of the sniped message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    ///     Gets or sets the content of the reference message, if any.
    /// </summary>
    public string ReferenceMessage { get; set; }

    /// <summary>
    ///     Indicates whether the message was edited.
    /// </summary>
    public bool Edited { get; set; }

    /// <summary>
    ///     Gets or sets the date and time when the message was added to the snipe store.
    /// </summary>
    public DateTime DateAdded { get; set; }
}