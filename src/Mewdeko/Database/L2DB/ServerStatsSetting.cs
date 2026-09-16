using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Per guild configuration for activity stats tracking.
/// </summary>
[Table("ServerStatsSettings")]
public class ServerStatsSetting
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Whether voice time is recorded.
    /// </summary>
    [Column("TrackVoice")]
    public bool TrackVoice { get; set; } = true;

    /// <summary>
    ///     Whether hourly member and status snapshots are recorded.
    /// </summary>
    [Column("TrackSnapshots")]
    public bool TrackSnapshots { get; set; } = true;

    /// <summary>
    ///     Seconds that must pass between two counted messages from the same member. Zero counts every message.
    /// </summary>
    [Column("MessageCooldownSeconds")]
    public int MessageCooldownSeconds { get; set; }

    /// <summary>
    ///     The window stats commands use when none is given.
    /// </summary>
    [Column("DefaultLookbackDays")]
    public int DefaultLookbackDays { get; set; } = 14;

    /// <summary>
    ///     Whether bot accounts are included in stats.
    /// </summary>
    [Column("CountBots")]
    public bool CountBots { get; set; }

    /// <summary>
    ///     A <see cref="Mewdeko.Modules.ServerStats.Common.VoiceStateFlags" /> mask of states that do not count
    ///     toward voice time. Zero counts every state.
    /// </summary>
    [Column("VoiceStates")]
    public int VoiceStates { get; set; }

    /// <summary>
    ///     Whether presence activities (games, streams, Spotify) are recorded. Off by default because it is the
    ///     heaviest tracker.
    /// </summary>
    [Column("TrackActivities")]
    public bool TrackActivities { get; set; }

    /// <summary>
    ///     Whether only activities backed by a Discord application (or Spotify and streams) count, which filters out
    ///     spoofed presences from custom clients.
    /// </summary>
    [Column("VerifyActivities")]
    public bool VerifyActivities { get; set; } = true;

    /// <summary>
    ///     The <see cref="Mewdeko.Modules.ServerStats.Common.ActivityFilterMode" /> the activity filter list uses.
    /// </summary>
    [Column("ActivityFilterMode")]
    public int ActivityFilterMode { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }
}