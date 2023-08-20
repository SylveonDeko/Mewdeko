using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

public class AntiRaidSetting : DbEntity
{
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    public GuildConfig GuildConfig { get; set; }

    public int UserThreshold { get; set; }
    public int Seconds { get; set; }
    public PunishmentAction Action { get; set; }

    /// <summary>
    ///     Duration of the punishment, in minutes. This works only for supported Actions, like:
    ///     Mute, Chatmute, Voicemute, etc...
    /// </summary>
    public int PunishDuration { get; set; }
}

public class AntiSpamSetting : DbEntity
{
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    public GuildConfig GuildConfig { get; set; }

    public PunishmentAction Action { get; set; }
    public int MessageThreshold { get; set; } = 0;
    public int MuteTime { get; set; } = 0;
    public ulong? RoleId { get; set; }
    public HashSet<AntiSpamIgnore> IgnoredChannels { get; set; } = new();
}

public class AntiMassMentionSetting : DbEntity
{
    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    public GuildConfig GuildConfig { get; set; }

    public PunishmentAction Action { get; set; }
    public int MentionThreshold { get; set; } = 3;
    public int MuteTime { get; set; } = 0;
    public ulong? RoleId { get; set; }
    public HashSet<AntiSpamIgnore> IgnoredChannels { get; set; } = new();
}

public class AntiAltSetting
{
    public int Id { get; set; }

    [ForeignKey("GuildConfigId")]
    public int GuildConfigId { get; set; }

    public string MinAge { get; set; }
    public PunishmentAction Action { get; set; }
    public int ActionDurationMinutes { get; set; }
    public ulong? RoleId { get; set; }
}

public enum PunishmentAction
{
    Mute,
    Kick,
    Ban,
    Softban,
    RemoveRoles,
    ChatMute,
    VoiceMute,
    AddRole,
    Delete,
    Warn,
    Timeout,
    None
}

public class AntiSpamIgnore : DbEntity
{
    public ulong ChannelId { get; set; }

    [ForeignKey("AntiSpamSettingId")]
    public int AntiSpamSettingId { get; set; }

    public override int GetHashCode() => ChannelId.GetHashCode();

    public override bool Equals(object obj) =>
        obj is AntiSpamIgnore inst && inst.ChannelId == ChannelId;
}