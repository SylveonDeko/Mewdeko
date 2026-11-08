namespace Mewdeko.Controllers.Common.Reputation;

/// <summary>
///     Request model for reputation decay settings.
/// </summary>
public class DecaySettingsRequest
{
    /// <summary>
    ///     Whether decay is on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The decay schedule: daily, weekly, monthly, fixed or percentage.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    ///     How much reputation is lost per decay, 1 to 100.
    /// </summary>
    public int? Amount { get; set; }

    /// <summary>
    ///     Days without activity before decay starts, 1 to 365.
    /// </summary>
    public int? InactiveDays { get; set; }
}

/// <summary>
///     Request model for taking or setting a member's reputation.
/// </summary>
public class MemberRepRequest
{
    /// <summary>
    ///     The amount to take, or the value to set.
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    ///     The member making the change.
    /// </summary>
    public ulong AdminId { get; set; }
}

/// <summary>
///     Request model for a reaction that gives reputation.
/// </summary>
public class ReactionRuleRequest
{
    /// <summary>
    ///     The emoji, as a unicode character or a custom emoji mention.
    /// </summary>
    public string Emoji { get; set; } = null!;

    /// <summary>
    ///     The reputation each reaction gives.
    /// </summary>
    public int Amount { get; set; } = 1;

    /// <summary>
    ///     The reputation type it counts towards.
    /// </summary>
    public string RepType { get; set; } = "standard";
}

/// <summary>
///     Request model for a custom reputation type.
/// </summary>
public class CustomTypeRequest
{
    /// <summary>
    ///     The short name used in commands.
    /// </summary>
    public string TypeName { get; set; } = null!;

    /// <summary>
    ///     The name shown to members.
    /// </summary>
    public string DisplayName { get; set; } = null!;
}
