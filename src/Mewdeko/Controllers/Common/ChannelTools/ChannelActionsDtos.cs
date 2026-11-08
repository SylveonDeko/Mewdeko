using System.Text.Json.Serialization;
using DataModel;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Server_Management;

namespace Mewdeko.Controllers.Common.ChannelTools;

/// <summary>
///     Whether a server is locked down and how.
/// </summary>
public class LockdownStatusDto
{
    /// <summary>
    ///     None, Joins, Readonly or Full.
    /// </summary>
    public string Type { get; set; } = "None";

    /// <summary>
    ///     Kick or Ban for new members during a join lockdown, or null.
    /// </summary>
    public string? Action { get; set; }
}

/// <summary>
///     Request to lock a server down.
/// </summary>
public class LockdownRequest
{
    /// <summary>
    ///     Joins, Readonly or Full.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ServerManagement.LockdownType Type { get; set; } = ServerManagement.LockdownType.Readonly;

    /// <summary>
    ///     Kick or Ban for new members during a join lockdown.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PunishmentAction Action { get; set; } = PunishmentAction.Ban;

    /// <summary>
    ///     Whether to go ahead when the bot lacks some permissions.
    /// </summary>
    public bool Override { get; set; }
}

/// <summary>
///     Request to change the slowmode of channels.
/// </summary>
public class BulkSlowmodeRequest
{
    /// <summary>
    ///     The channels.
    /// </summary>
    public List<ulong> ChannelIds { get; set; } = [];

    /// <summary>
    ///     The slowmode in seconds, from 0 to 21600.
    /// </summary>
    public int Seconds { get; set; }
}

/// <summary>
///     Request to lock or hide channels.
/// </summary>
public class BulkLockRequest
{
    /// <summary>
    ///     The user doing it.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The channels.
    /// </summary>
    public List<ulong> ChannelIds { get; set; } = [];

    /// <summary>
    ///     Lock or Hide.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ChannelTimedActionKind Kind { get; set; }

    /// <summary>
    ///     How long it lasts in minutes, or null to last until undone.
    /// </summary>
    public int? Minutes { get; set; }
}

/// <summary>
///     Request to undo locks or hides.
/// </summary>
public class BulkUnlockRequest
{
    /// <summary>
    ///     The channels.
    /// </summary>
    public List<ulong> ChannelIds { get; set; } = [];

    /// <summary>
    ///     Lock or Hide.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ChannelTimedActionKind Kind { get; set; }
}

/// <summary>
///     A list of channels to act on.
/// </summary>
public class ChannelIdsRequest
{
    /// <summary>
    ///     The channels.
    /// </summary>
    public List<ulong> ChannelIds { get; set; } = [];
}

/// <summary>
///     Request to copy a channel.
/// </summary>
public class CloneChannelRequest
{
    /// <summary>
    ///     The channel to copy.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The name of the copy, or null to keep the name.
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
///     Request to delete recent messages from a channel.
/// </summary>
public class PurgeChannelRequest
{
    /// <summary>
    ///     The channel.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     How many messages to delete, from 1 to 1000.
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    ///     All, Humans, Bots, Links or Attachments.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AutoPurgeFilter Filter { get; set; }

    /// <summary>
    ///     Whether pinned messages are kept.
    /// </summary>
    public bool KeepPinned { get; set; } = true;
}

/// <summary>
///     How many channels an action worked on.
/// </summary>
public class BulkChannelResultDto
{
    /// <summary>
    ///     How many channels it worked on.
    /// </summary>
    public int Succeeded { get; set; }

    /// <summary>
    ///     How many channels it failed on.
    /// </summary>
    public int Failed { get; set; }
}
