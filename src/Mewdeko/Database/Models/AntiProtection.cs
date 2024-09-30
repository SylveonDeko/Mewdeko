using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

/// <summary>
/// Represents anti-raid settings for a guild.
/// </summary>
[Table("AntiRaidSetting")]
public class AntiRaidSetting : DbEntity
{
    /// <summary>
    /// Gets or sets the ID of the associated guild configuration.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    /// <summary>
    /// Gets or sets the user threshold for triggering anti-raid measures.
    /// </summary>
    public int UserThreshold { get; set; }

    /// <summary>
    /// Gets or sets the time window in seconds for the anti-raid measures.
    /// </summary>
    public int Seconds { get; set; }

    /// <summary>
    /// Gets or sets the action to be taken when anti-raid measures are triggered.
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    /// Gets or sets the duration of the punishment in minutes.
    /// This works only for supported Actions, like Mute, Chatmute, Voicemute, etc.
    /// </summary>
    public int PunishDuration { get; set; }
}

/// <summary>
/// Represents anti-spam settings for a guild.
/// </summary>
[Table("AntiSpamSetting")]
public class AntiSpamSetting : DbEntity
{
    /// <summary>
    /// Gets or sets the ID of the associated guild configuration.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    /// <summary>
    /// Gets or sets the action to be taken when anti-spam measures are triggered.
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    /// Gets or sets the message threshold for triggering anti-spam measures.
    /// </summary>
    public int MessageThreshold { get; set; } = 0;

    /// <summary>
    /// Gets or sets the mute duration in minutes.
    /// </summary>
    public int MuteTime { get; set; } = 0;

    /// <summary>
    /// Gets or sets the ID of the role to be assigned as punishment.
    /// </summary>
    public ulong? RoleId { get; set; }

    /// <summary>
    /// Gets or sets the collection of ignored channels for anti-spam measures.
    /// </summary>
    public HashSet<AntiSpamIgnore> IgnoredChannels { get; set; } = [];
}

/// <summary>
/// Represents settings for anti-mass mention protection in a guild.
/// </summary>
[Table("AntiMassMentionSetting")]
public class AntiMassMentionSetting : DbEntity
{
    /// <summary>
    /// Gets or sets the ID of the associated guild configuration.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    /// <summary>
    /// Gets or sets the action to be taken when anti-mass mention measures are triggered.
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of mentions allowed in a single message before triggering anti-mass mention measures.
    /// </summary>
    public int MentionThreshold { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of mentions allowed within a specified time window.
    /// </summary>
    public int MaxMentionsInTimeWindow { get; set; } = 5;

    /// <summary>
    /// Gets or sets the time window in seconds in which mentions are tracked.
    /// If the number of mentions exceeds MaxMentionsInTimeWindow within this time, anti-mass mention actions are triggered.
    /// </summary>
    public int TimeWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the mute duration in minutes.
    /// This is applied when a mute action is chosen as the punishment.
    /// </summary>
    public int MuteTime { get; set; } = 0;

    /// <summary>
    /// Gets or sets the ID of the role to be assigned as punishment.
    /// </summary>
    public ulong? RoleId { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating whether bots should be ignored by anti-mass mention measures.
    /// </summary>
    public bool IgnoreBots { get; set; } = true;

    /// <summary>
    /// Gets or sets the collection of ignored channels for anti-mass mention measures.
    /// </summary>
    public HashSet<AntiSpamIgnore> IgnoredChannels { get; set; } = new();
}



/// <summary>
/// Represents settings for anti-alt measures in a guild.
/// </summary>
[Table("AntiAltSetting")]
public class AntiAltSetting
{
    /// <summary>
    /// Gets or sets the unique identifier for this anti-alt setting.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the associated guild configuration.
    /// </summary>
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    /// <summary>
    /// Gets or sets the minimum age requirement for accounts.
    /// </summary>
    public string? MinAge { get; set; }

    /// <summary>
    /// Gets or sets the action to be taken when anti-alt measures are triggered.
    /// </summary>
    public PunishmentAction Action { get; set; }

    /// <summary>
    /// Gets or sets the duration of the action in minutes.
    /// </summary>
    public int ActionDurationMinutes { get; set; }

    /// <summary>
    /// Gets or sets the ID of the role to be assigned as part of the anti-alt measure.
    /// </summary>
    public ulong? RoleId { get; set; }
}

/// <summary>
/// Represents the types of punishment actions that can be taken.
/// </summary>
public enum PunishmentAction
{
    /// <summary>
    /// Mute the user, preventing them from sending messages.
    /// </summary>
    Mute,

    /// <summary>
    /// Kick the user from the server.
    /// </summary>
    Kick,

    /// <summary>
    /// Ban the user from the server.
    /// </summary>
    Ban,

    /// <summary>
    /// Softban the user (ban and immediately unban to clear recent messages).
    /// </summary>
    Softban,

    /// <summary>
    /// Remove all roles from the user.
    /// </summary>
    RemoveRoles,

    /// <summary>
    /// Mute the user in text channels only.
    /// </summary>
    ChatMute,

    /// <summary>
    /// Mute the user in voice channels only.
    /// </summary>
    VoiceMute,

    /// <summary>
    /// Add a specific role to the user.
    /// </summary>
    AddRole,

    /// <summary>
    /// Delete the user's message that triggered the action.
    /// </summary>
    Delete,

    /// <summary>
    /// Issue a warning to the user.
    /// </summary>
    Warn,

    /// <summary>
    /// Temporarily restrict the user's access to the server.
    /// </summary>
    Timeout,

    /// <summary>
    /// Take no action.
    /// </summary>
    None
}

/// <summary>
/// Represents an ignored channel for anti-spam measures.
/// </summary>
public class AntiSpamIgnore : DbEntity
{
    /// <summary>
    /// Gets or sets the ID of the ignored channel.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the associated anti-spam setting.
    /// </summary>
    [ForeignKey("AntiSpamSettingId")]
    public int? AntiSpamSettingId { get; set; }

    /// <summary>
    /// Gets the hash code for this instance.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => ChannelId.GetHashCode();

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object obj) =>
        obj is AntiSpamIgnore inst && inst.ChannelId == ChannelId;
}