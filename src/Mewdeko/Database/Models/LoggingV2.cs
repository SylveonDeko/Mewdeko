namespace Mewdeko.Database.Models;

/// <summary>
///     Represents the logging settings for a guild.
/// </summary>
public class LoggingV2 : DbEntity
{
    /// <summary>
    ///     Gets or sets the ID these logging settings are for.
    /// </summary>
    public ulong GuildId { get; set; }
    /// <summary>
    ///     Gets or sets the ID for logging other events.
    /// </summary>
    public ulong? LogOtherId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging updated messages.
    /// </summary>
    public ulong? MessageUpdatedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging deleted messages.
    /// </summary>
    public ulong? MessageDeletedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging created threads.
    /// </summary>
    public ulong? ThreadCreatedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging deleted threads.
    /// </summary>
    public ulong? ThreadDeletedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging updated threads.
    /// </summary>
    public ulong? ThreadUpdatedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging updated usernames.
    /// </summary>
    public ulong? UsernameUpdatedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging updated nicknames.
    /// </summary>
    public ulong? NicknameUpdatedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging updated avatars.
    /// </summary>
    public ulong? AvatarUpdatedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging user leave events.
    /// </summary>
    public ulong? UserLeftId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging user ban events.
    /// </summary>
    public ulong? UserBannedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging user unban events.
    /// </summary>
    public ulong? UserUnbannedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging updated user information.
    /// </summary>
    public ulong? UserUpdatedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging user join events.
    /// </summary>
    public ulong? UserJoinedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging role additions to users.
    /// </summary>
    public ulong? UserRoleAddedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging role removals from users.
    /// </summary>
    public ulong? UserRoleRemovedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging user mute events.
    /// </summary>
    public ulong? UserMutedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging user presence.
    /// </summary>
    public ulong? LogUserPresenceId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging voice presence.
    /// </summary>
    public ulong? LogVoicePresenceId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging text-to-speech voice presence.
    /// </summary>
    public ulong? LogVoicePresenceTTSId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging server updates.
    /// </summary>
    public ulong? ServerUpdatedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging role updates.
    /// </summary>
    public ulong? RoleUpdatedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging role deletions.
    /// </summary>
    public ulong? RoleDeletedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging event creations.
    /// </summary>
    public ulong? EventCreatedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging role creations.
    /// </summary>
    public ulong? RoleCreatedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging channel creations.
    /// </summary>
    public ulong? ChannelCreatedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging channel destructions.
    /// </summary>
    public ulong? ChannelDestroyedId { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the ID for logging channel updates.
    /// </summary>
    public ulong? ChannelUpdatedId { get; set; } = 0;

}