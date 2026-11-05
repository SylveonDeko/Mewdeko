using System.Text.Json.Serialization;
using DataModel;
using Mewdeko.Modules.Automations.Common;

namespace Mewdeko.Controllers.Common.Automations;

/// <summary>
///     An automation as it is read and as it is written.
/// </summary>
public class AutomationRequest
{
    /// <summary>
    ///     The name, 1 to 100 characters.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Whether the automation runs.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     The trigger type, such as MessageSent. The schema endpoint lists them.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AutomationTriggerType Trigger { get; set; }

    /// <summary>
    ///     The conditions. All of them have to pass. Each is an object with a type and that type's fields.
    /// </summary>
    public List<AutomationNode> Conditions { get; set; } = [];

    /// <summary>
    ///     The actions, run in order. Each is an object with a type and that type's fields.
    /// </summary>
    public List<AutomationNode> Actions { get; set; } = [];

    /// <summary>
    ///     Seconds before the same member or channel can run it again, or 0 for none.
    /// </summary>
    public int CooldownSeconds { get; set; }

    /// <summary>
    ///     User or Channel.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AutomationCooldownScope CooldownScope { get; set; }

    /// <summary>
    ///     Whether an action that fails on a rate limit or a temporary Discord error is tried again.
    /// </summary>
    public bool RetryOnFailure { get; set; }

    /// <summary>
    ///     Whether events caused by bots are ignored.
    /// </summary>
    public bool IgnoreBots { get; set; } = true;

    /// <summary>
    ///     Whether messages may mention everyone, here and roles.
    /// </summary>
    public bool AllowMassMentions { get; set; }

    /// <summary>
    ///     The ID of the dashboard user making the request. Kept as the creator on create and left alone on update.
    /// </summary>
    public ulong? UserId { get; set; }

    /// <summary>
    ///     Turns the request into settings the service understands.
    /// </summary>
    public AutomationInput ToInput() => new()
    {
        Name = Name ?? "",
        Enabled = Enabled,
        Trigger = Trigger,
        Conditions = Conditions ?? [],
        Actions = Actions ?? [],
        CooldownSeconds = CooldownSeconds,
        CooldownScope = CooldownScope,
        RetryOnFailure = RetryOnFailure,
        IgnoreBots = IgnoreBots,
        AllowMassMentions = AllowMassMentions
    };
}

/// <summary>
///     A stored automation.
/// </summary>
public class AutomationDto : AutomationRequest
{
    /// <summary>
    ///     The automation ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The ID of the member who created it.
    /// </summary>
    public ulong CreatorId { get; set; }

    /// <summary>
    ///     When it was created.
    /// </summary>
    public DateTime DateAdded { get; set; }

    /// <summary>
    ///     When it was last changed.
    /// </summary>
    public DateTime DateModified { get; set; }

    /// <summary>
    ///     Reads a stored automation.
    /// </summary>
    /// <param name="row">The stored automation.</param>
    public static AutomationDto From(Automation row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Enabled = row.Enabled,
        Trigger = row.Trigger,
        Conditions = AutomationNode.ParseList(row.Conditions),
        Actions = AutomationNode.ParseList(row.Actions),
        CooldownSeconds = row.CooldownSeconds,
        CooldownScope = row.CooldownScope,
        RetryOnFailure = row.RetryOnFailure,
        IgnoreBots = row.IgnoreBots,
        AllowMassMentions = row.AllowMassMentions,
        CreatorId = row.CreatorId,
        DateAdded = DateTime.SpecifyKind(row.DateAdded, DateTimeKind.Utc),
        DateModified = DateTime.SpecifyKind(row.DateModified, DateTimeKind.Utc)
    };
}

/// <summary>
///     Turns an automation on or off.
/// </summary>
public class AutomationEnabledRequest
{
    /// <summary>
    ///     Whether the automation should run.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>
///     One run in an automation's log.
/// </summary>
public class AutomationRunDto
{
    /// <summary>
    ///     The run ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The automation ID.
    /// </summary>
    public int AutomationId { get; set; }

    /// <summary>
    ///     Whether every action finished.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Whether the run was started by hand as a test.
    /// </summary>
    public bool IsTest { get; set; }

    /// <summary>
    ///     The position, from 0, of the action that failed.
    /// </summary>
    public int? FailedActionIndex { get; set; }

    /// <summary>
    ///     The type of the action that failed.
    /// </summary>
    public string? FailedActionType { get; set; }

    /// <summary>
    ///     Why the run failed.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    ///     The ID of the member who caused the run, or 0.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The ID of the channel the run happened in, or 0.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     How long the run took in milliseconds.
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    ///     When the run started.
    /// </summary>
    public DateTime RanAt { get; set; }

    /// <summary>
    ///     Reads a stored run.
    /// </summary>
    /// <param name="run">The stored run.</param>
    public static AutomationRunDto From(AutomationRun run) => new()
    {
        Id = run.Id,
        AutomationId = run.AutomationId,
        Success = run.Success,
        IsTest = run.IsTest,
        FailedActionIndex = run.FailedActionIndex,
        FailedActionType = run.FailedActionType,
        Reason = run.Reason,
        UserId = run.UserId,
        ChannelId = run.ChannelId,
        DurationMs = run.DurationMs,
        RanAt = DateTime.SpecifyKind(run.RanAt, DateTimeKind.Utc)
    };
}

/// <summary>
///     Who runs a test and where.
/// </summary>
public class AutomationTestRequest
{
    /// <summary>
    ///     The ID of the member the test acts as. They have to be in the server.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The channel the test happens in. Leave out to run without one.
    /// </summary>
    public ulong? ChannelId { get; set; }
}

/// <summary>
///     How a test run ended.
/// </summary>
public class AutomationTestResultDto
{
    /// <summary>
    ///     Whether every action finished.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     The position, from 0, of the action that failed.
    /// </summary>
    public int? FailedActionIndex { get; set; }

    /// <summary>
    ///     The type of the action that failed.
    /// </summary>
    public string? FailedActionType { get; set; }

    /// <summary>
    ///     Why the run failed.
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
///     What a template needs to be told.
/// </summary>
public class AutomationTemplateRequest
{
    /// <summary>
    ///     The ID of the dashboard user making the request, recorded as the creator.
    /// </summary>
    public ulong? CreatorId { get; set; }

    /// <summary>
    ///     The channel, when the template needs one.
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    ///     The role, when the template needs one.
    /// </summary>
    public ulong? RoleId { get; set; }

    /// <summary>
    ///     The member, when the template needs one.
    /// </summary>
    public ulong? UserId { get; set; }

    /// <summary>
    ///     Turns the request into template parameters.
    /// </summary>
    public Dictionary<string, ulong> ToValues()
    {
        var values = new Dictionary<string, ulong>();
        if (ChannelId is { } channel)
            values["channel"] = channel;
        if (RoleId is { } role)
            values["role"] = role;
        if (UserId is { } user)
            values["user"] = user;
        return values;
    }
}

/// <summary>
///     One problem found while checking an automation.
/// </summary>
public class AutomationErrorDto
{
    /// <summary>
    ///     Where the problem is, such as <c>actions[1].roleIds</c>.
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    ///     What is wrong.
    /// </summary>
    public string Message { get; set; } = "";
}

/// <summary>
///     What came of creating automations from a template.
/// </summary>
public class AutomationTemplateResultDto
{
    /// <summary>
    ///     The automations that were created.
    /// </summary>
    public List<AutomationDto> Created { get; set; } = [];

    /// <summary>
    ///     Why the first message could not be posted, when it could not.
    /// </summary>
    public string? SetupError { get; set; }
}
