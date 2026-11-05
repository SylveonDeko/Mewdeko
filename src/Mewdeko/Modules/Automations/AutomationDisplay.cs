using DataModel;
using Mewdeko.Modules.Automations.Common;
using Mewdeko.Modules.Automations.Services;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Automations;

/// <summary>
///     Builds the messages the automation commands reply with.
/// </summary>
public static class AutomationDisplay
{
    /// <summary>
    ///     Gets a short name for a trigger.
    /// </summary>
    /// <param name="trigger">The trigger.</param>
    public static string TriggerName(AutomationTriggerType trigger) => AutomationSchema.GetTrigger(trigger).Name;

    /// <summary>
    ///     Builds the list of a guild's automations.
    /// </summary>
    /// <param name="strings">The bot strings.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="automations">The automations.</param>
    public static EmbedBuilder List(GeneratedBotStrings strings, ulong guildId, IReadOnlyList<Automation> automations)
    {
        var lines = automations.Select(x => strings.AutomationListEntry(guildId, x.Id, x.Name, TriggerName(x.Trigger),
            x.Enabled ? strings.AutomationStateOn(guildId) : strings.AutomationStateOff(guildId)));

        return new EmbedBuilder().WithOkColor()
            .WithTitle(strings.AutomationListTitle(guildId))
            .WithDescription(string.Join('\n', lines).TrimTo(4000));
    }

    /// <summary>
    ///     Builds the details of one automation.
    /// </summary>
    /// <param name="strings">The bot strings.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="automation">The automation.</param>
    public static EmbedBuilder Show(GeneratedBotStrings strings, ulong guildId, Automation automation)
    {
        string Yes(bool value) => value ? strings.AutomationYes(guildId) : strings.AutomationNo(guildId);

        var conditions = Summarize(strings, guildId, AutomationNode.ParseList(automation.Conditions),
            AutomationSchema.FindCondition);
        var actions = Summarize(strings, guildId, AutomationNode.ParseList(automation.Actions),
            AutomationSchema.FindAction);
        var cooldown = automation.CooldownSeconds > 0
            ? strings.AutomationCooldownValue(guildId, automation.CooldownSeconds, automation.CooldownScope)
            : strings.AutomationCooldownNone(guildId);

        return new EmbedBuilder().WithOkColor()
            .WithTitle(strings.AutomationShowTitle(guildId, automation.Id, automation.Name).TrimTo(256))
            .WithDescription(automation.Enabled ? strings.AutomationStateOn(guildId) : strings.AutomationStateOff(guildId))
            .AddField(strings.AutomationFieldTrigger(guildId), TriggerName(automation.Trigger), true)
            .AddField(strings.AutomationFieldSettings(guildId),
                strings.AutomationSettingsLine(guildId, cooldown, Yes(automation.RetryOnFailure),
                    Yes(automation.IgnoreBots)))
            .AddField(strings.AutomationFieldConditions(guildId), conditions.TrimTo(1024))
            .AddField(strings.AutomationFieldActions(guildId), actions.TrimTo(1024));
    }

    /// <summary>
    ///     Builds the run log of one automation.
    /// </summary>
    /// <param name="strings">The bot strings.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="automation">The automation.</param>
    /// <param name="runs">The runs, newest first.</param>
    public static EmbedBuilder Log(GeneratedBotStrings strings, ulong guildId, Automation automation,
        IReadOnlyList<AutomationRun> runs)
    {
        var lines = runs.Select(x =>
        {
            var when = $"<t:{new DateTimeOffset(DateTime.SpecifyKind(x.RanAt, DateTimeKind.Utc)).ToUnixTimeSeconds()}:R>";
            var test = x.IsTest ? strings.AutomationLogTest(guildId) : "";
            return x.Success
                ? strings.AutomationLogOk(guildId, when, test)
                : strings.AutomationLogFail(guildId, when, (x.FailedActionIndex ?? 0) + 1,
                    x.FailedActionType ?? "?", x.Reason ?? "?", test);
        });

        return new EmbedBuilder().WithOkColor()
            .WithTitle(strings.AutomationLogTitle(guildId, automation.Name).TrimTo(256))
            .WithDescription(string.Join('\n', lines).TrimTo(4000));
    }

    /// <summary>
    ///     Builds the list of templates.
    /// </summary>
    /// <param name="strings">The bot strings.</param>
    /// <param name="guildId">The guild ID.</param>
    public static EmbedBuilder Templates(GeneratedBotStrings strings, ulong guildId) =>
        new EmbedBuilder().WithOkColor()
            .WithTitle(strings.AutomationTemplatesTitle(guildId))
            .WithDescription(string.Join('\n',
                AutomationTemplates.All.Select(x => strings.AutomationTemplateEntry(guildId, x.Id, x.Name,
                    x.Description))));

    /// <summary>
    ///     Writes why a template could not be made.
    /// </summary>
    /// <param name="strings">The bot strings.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="outcome">The outcome of creating it.</param>
    public static string TemplateError(GeneratedBotStrings strings, ulong guildId, AutomationTemplateOutcome outcome) =>
        outcome.Error switch
        {
            AutomationTemplateError.MissingParameter =>
                strings.AutomationTemplateMissingParameter(guildId, outcome.Detail ?? ""),
            AutomationTemplateError.NoBoosterRole => strings.AutomationTemplateNoBoosterRole(guildId),
            AutomationTemplateError.UnknownRole => strings.AutomationTemplateUnknownRole(guildId),
            AutomationTemplateError.Invalid => strings.AutomationTemplateInvalid(guildId, outcome.Detail ?? ""),
            _ => strings.AutomationTemplateUnknown(guildId)
        };

    private static string Summarize(GeneratedBotStrings strings, ulong guildId, List<AutomationNode> nodes,
        Func<string, AutomationNodeSpec?> find) =>
        nodes.Count == 0
            ? strings.AutomationNoneListed(guildId)
            : string.Join('\n', nodes.Select((x, i) => $"{i + 1}. {find(x.Type)?.Name ?? x.Type}"));
}
