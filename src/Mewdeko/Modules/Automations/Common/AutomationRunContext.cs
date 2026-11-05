using DataModel;

namespace Mewdeko.Modules.Automations.Common;

/// <summary>
///     The Discord objects of one event and the state of one run.
/// </summary>
public sealed class AutomationRunContext
{
    /// <summary>
    ///     Gets or sets the automation being run.
    /// </summary>
    public required Automation Automation { get; init; }

    /// <summary>
    ///     Gets or sets what conditions saw about the event.
    /// </summary>
    public required AutomationEventData Data { get; init; }

    /// <summary>
    ///     Gets or sets the guild.
    /// </summary>
    public required SocketGuild Guild { get; init; }

    /// <summary>
    ///     Gets or sets the member or user who caused the event.
    /// </summary>
    public IUser? User { get; init; }

    /// <summary>
    ///     Gets or sets the channel of the event.
    /// </summary>
    public IMessageChannel? Channel { get; init; }

    /// <summary>
    ///     Gets or sets the message of the event.
    /// </summary>
    public IUserMessage? Message { get; init; }

    /// <summary>
    ///     Gets or sets the reaction emoji.
    /// </summary>
    public IEmote? Emote { get; init; }

    /// <summary>
    ///     Gets or sets the clicked button's interaction.
    /// </summary>
    public SocketMessageComponent? Component { get; init; }

    /// <summary>
    ///     Gets or sets the role that was gained or lost.
    /// </summary>
    public IRole? Role { get; init; }

    /// <summary>
    ///     Gets or sets the thread that fired the thread trigger.
    /// </summary>
    public IThreadChannel? TriggerThread { get; init; }

    /// <summary>
    ///     Gets or sets whether the run was started by hand.
    /// </summary>
    public bool IsTest { get; init; }

    /// <summary>
    ///     Gets or sets the thread an earlier action of this run created.
    /// </summary>
    public IThreadChannel? CreatedThread { get; set; }

    /// <summary>
    ///     Gets or sets how many seconds this run has spent waiting.
    /// </summary>
    public int WaitedSeconds { get; set; }

    /// <summary>
    ///     Gets a copy with the same event for another automation.
    /// </summary>
    /// <param name="automation">The automation to run.</param>
    public AutomationRunContext For(Automation automation) => new()
    {
        Automation = automation,
        Data = Data,
        Guild = Guild,
        User = User,
        Channel = Channel,
        Message = Message,
        Emote = Emote,
        Component = Component,
        Role = Role,
        TriggerThread = TriggerThread,
        IsTest = IsTest
    };
}

/// <summary>
///     How a run ended.
/// </summary>
/// <param name="Success">Whether every action finished.</param>
/// <param name="FailedIndex">The position of the action that failed.</param>
/// <param name="FailedType">The type of the action that failed.</param>
/// <param name="Reason">Why it failed.</param>
public sealed record AutomationRunResult(bool Success, int? FailedIndex = null, string? FailedType = null,
    string? Reason = null)
{
    /// <summary>
    ///     A run where every action finished.
    /// </summary>
    public static readonly AutomationRunResult Ok = new(true);
}

/// <summary>
///     The placeholders that automations add on top of the usual ones.
/// </summary>
public static class AutomationPlaceholders
{
    /// <summary>
    ///     The extra placeholders and what they stand for.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        ["%message.content%"] = "The text of the message.",
        ["%message.id%"] = "The ID of the message.",
        ["%message.link%"] = "A link to the message.",
        ["%message.author%"] = "A mention of the message author.",
        ["%reaction.emoji%"] = "The emoji of the reaction.",
        ["%reaction.count%"] = "How many members used the reaction.",
        ["%role.name%"] = "The name of the role that was gained or lost.",
        ["%role.mention%"] = "A mention of the role that was gained or lost.",
        ["%thread.name%"] = "The name of the thread.",
        ["%thread.mention%"] = "A mention of the thread.",
        ["%thread.id%"] = "The ID of the thread."
    };
}
