using Mewdeko.Modules.Xp.Models;

namespace Mewdeko.Controllers.Common.XpEvents;

/// <summary>
///     A request to set a channel or role XP multiplier. A multiplier of 1 removes it.
/// </summary>
public class XpMultiplierRequest
{
    /// <summary>
    ///     The channel or role ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     The multiplier.
    /// </summary>
    public double Multiplier { get; set; } = 1;
}

/// <summary>
///     A request to start a temporary XP boost.
/// </summary>
public class XpBoostRequest
{
    /// <summary>
    ///     The boost name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The multiplier, above 1.
    /// </summary>
    public double Multiplier { get; set; }

    /// <summary>
    ///     How long the boost lasts, at least 5 minutes.
    /// </summary>
    public int DurationMinutes { get; set; }

    /// <summary>
    ///     Channels the boost is limited to, empty for every channel.
    /// </summary>
    public List<ulong> ChannelIds { get; set; } = [];

    /// <summary>
    ///     Roles the boost is limited to, empty for every role.
    /// </summary>
    public List<ulong> RoleIds { get; set; } = [];
}

/// <summary>
///     A request to start an XP competition.
/// </summary>
public class XpCompetitionRequest
{
    /// <summary>
    ///     The competition name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The competition type.
    /// </summary>
    public XpCompetitionType Type { get; set; }

    /// <summary>
    ///     How long it runs, at least 60 minutes.
    /// </summary>
    public int DurationMinutes { get; set; }

    /// <summary>
    ///     The level to reach, for <see cref="XpCompetitionType.ReachLevel" />.
    /// </summary>
    public int TargetLevel { get; set; }

    /// <summary>
    ///     The channel the start and results are announced in, 0 for none.
    /// </summary>
    public ulong AnnouncementChannelId { get; set; }

    /// <summary>
    ///     The placement rewards.
    /// </summary>
    public List<XpCompetitionRewardRequest> Rewards { get; set; } = [];
}

/// <summary>
///     A placement reward for a competition.
/// </summary>
public class XpCompetitionRewardRequest
{
    /// <summary>
    ///     The placement, 1 for first.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    ///     The role granted, 0 for none.
    /// </summary>
    public ulong RoleId { get; set; }

    /// <summary>
    ///     The XP granted.
    /// </summary>
    public int XpAmount { get; set; }

    /// <summary>
    ///     The currency granted.
    /// </summary>
    public long CurrencyAmount { get; set; }
}

/// <summary>
///     A request to add a custom level-up message.
/// </summary>
public class XpLevelUpMessageRequest
{
    /// <summary>
    ///     The message template.
    /// </summary>
    public string Message { get; set; } = "";
}

/// <summary>
///     A request to reset every member's XP.
/// </summary>
public class XpResetRequest
{
    /// <summary>
    ///     Whether bonus XP is reset too.
    /// </summary>
    public bool ResetBonusXp { get; set; }
}
