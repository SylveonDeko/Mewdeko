namespace Mewdeko.Database.Models;

public class LogSetting : DbEntity
{
    public HashSet<IgnoredLogChannel> IgnoredChannels { get; set; } = new();
    public HashSet<IgnoredVoicePresenceChannel> IgnoredVoicePresenceChannelIds { get; set; } = new();

    public ulong? LogOtherId { get; set; } = null;
    public ulong? MessageUpdatedId { get; set; } = null;

    public ulong? MessageDeletedId { get; set; } = null;
    // Threads
    public ulong? ThreadCreatedId { get; set; } = null;
    public ulong? ThreadDeletedId { get; set; } = null;
    public ulong? ThreadUpdatedId { get; set; } = null;
    // Users
    public ulong? UsernameUpdatedId { get; set; } = null;
    public ulong? NicknameUpdatedId { get; set; } = null;
    public ulong? AvatarUpdatedId { get; set; } = null;
    public ulong? UserLeftId { get; set; } = null;
    public ulong? UserBannedId { get; set; } = null;
    public ulong? UserUnbannedId { get; set; } = null;
    public ulong? UserUpdatedId { get; set; } = null;
    public ulong? UserJoinedId { get; set; } = null;
    public ulong? UserRoleAddedId { get; set; } = null;
    public ulong? UserRoleRemovedId { get; set; } = null;
    public ulong? UserMutedId { get; set; }
    public ulong? LogUserPresenceId { get; set; } = null;

    public ulong? LogVoicePresenceId { get; set; } = null;
    // ReSharper disable once InconsistentNaming
    public ulong? LogVoicePresenceTTSId { get; set; } = null;
    // Server
    public ulong? ServerUpdatedId { get; set; } = null;
    public ulong? RoleUpdatedId { get; set; } = null;
    public ulong? RoleDeletedId { get; set; } = null;
    public ulong? EventCreatedId { get; set; } = null;
    public ulong? RoleCreatedId { get; set; } = null;
    
    // Channels
    public ulong? ChannelCreatedId { get; set; } = null;
    public ulong? ChannelDestroyedId { get; set; } = null;
    public ulong? ChannelUpdatedId { get; set; } = null;
    
    
}