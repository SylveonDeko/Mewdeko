using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

/// <summary>
///     Slash commands for managing Discord's native AutoMod rules and what Mewdeko does when they fire.
/// </summary>
[Group("discord-automod", "Manage Discord AutoMod rules and what happens when they fire")]
public class SlashDiscordAutoMod : MewdekoSlashModuleBase<DiscordAutoModService>
{
    /// <summary>
    ///     The rule types that can be created.
    /// </summary>
    public enum RuleType
    {
        /// <summary>
        ///     Keywords.
        /// </summary>
        Keyword,

        /// <summary>
        ///     Discord's preset word lists.
        /// </summary>
        Preset,

        /// <summary>
        ///     Mention spam.
        /// </summary>
        Mention,

        /// <summary>
        ///     Spam.
        /// </summary>
        Spam
    }

    /// <summary>
    ///     The options that can be changed on a rule.
    /// </summary>
    public enum EditOption
    {
        /// <summary>
        ///     The rule name.
        /// </summary>
        Name,

        /// <summary>
        ///     Turns the rule on.
        /// </summary>
        Enable,

        /// <summary>
        ///     Turns the rule off.
        /// </summary>
        Disable,

        /// <summary>
        ///     The keywords.
        /// </summary>
        Keywords,

        /// <summary>
        ///     The regex patterns, separated by semicolons.
        /// </summary>
        Regex,

        /// <summary>
        ///     The words let through.
        /// </summary>
        Allow,

        /// <summary>
        ///     The presets.
        /// </summary>
        Presets,

        /// <summary>
        ///     The mention limit.
        /// </summary>
        MentionLimit,

        /// <summary>
        ///     Mention raid protection.
        /// </summary>
        RaidProtection,

        /// <summary>
        ///     The block message action.
        /// </summary>
        Block,

        /// <summary>
        ///     The alert channel action.
        /// </summary>
        Alert,

        /// <summary>
        ///     The timeout action.
        /// </summary>
        Timeout,

        /// <summary>
        ///     The exempt roles.
        /// </summary>
        ExemptRoles,

        /// <summary>
        ///     The exempt channels.
        /// </summary>
        ExemptChannels
    }

    /// <summary>
    ///     The options that can be changed on a follow-up.
    /// </summary>
    public enum FollowUpOption
    {
        /// <summary>
        ///     Shows the follow-up.
        /// </summary>
        Show,

        /// <summary>
        ///     Warns the member.
        /// </summary>
        Warn,

        /// <summary>
        ///     Times the member out or mutes them.
        /// </summary>
        Punish,

        /// <summary>
        ///     Kicks or bans after repeated hits.
        /// </summary>
        Escalate,

        /// <summary>
        ///     The log channel.
        /// </summary>
        LogChannel,

        /// <summary>
        ///     Removes the follow-up.
        /// </summary>
        Clear
    }

    /// <summary>
    ///     Lists the server's native AutoMod rules.
    /// </summary>
    [SlashCommand("list", "Lists the AutoMod rules in this server")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task List()
    {
        var guildId = ctx.Guild.Id;
        var (failure, rules) = await Service.ListAsync(ctx.Guild);
        if (failure is not null)
        {
            await ReplyErrorAsync(failure.Message);
            return;
        }

        if (rules.Count == 0)
        {
            await ReplyErrorAsync(Strings.AutomodListEmpty(guildId));
            return;
        }

        await RespondAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.AutomodListTitle(guildId))
            .WithDescription(string.Join("\n\n", rules.Select(x => Service.FormatRule(guildId, x))))
            .Build(), allowedMentions: AllowedMentions.None);
    }

    /// <summary>
    ///     Creates a native AutoMod rule.
    /// </summary>
    /// <param name="type">The kind of rule.</param>
    /// <param name="name">The rule name.</param>
    /// <param name="value">Keywords, presets or the mention limit, depending on the type.</param>
    [SlashCommand("create", "Creates an AutoMod rule that blocks matching messages")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Create(
        [Summary("type", "The kind of rule")] RuleType type,
        [Summary("name", "The rule name")] string name,
        [Summary("value", "Keywords, presets (profanity, sexual, slurs) or the mention limit")]
        string? value = null)
    {
        var spec = new AutoModRuleSpec
        {
            Name = name
        };

        switch (type)
        {
            case RuleType.Keyword:
                spec.Kind = AutoModRuleKind.Keyword;
                spec.Keywords = value is null
                    ? []
                    : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToList();
                break;
            case RuleType.Preset:
                spec.Kind = AutoModRuleKind.Preset;
                spec.Presets = DiscordAutoModService.ParsePresets(value ?? "");
                break;
            case RuleType.Mention:
                spec.Kind = AutoModRuleKind.MentionSpam;
                spec.MentionLimit = int.TryParse(value, out var limit) ? limit : 0;
                break;
            default:
                spec.Kind = AutoModRuleKind.Spam;
                break;
        }

        await ReplyAsync(await Service.CreateAsync(ctx.Guild, spec));
    }

    /// <summary>
    ///     Changes a native AutoMod rule.
    /// </summary>
    /// <param name="rule">The rule ID or name.</param>
    /// <param name="option">The option to change.</param>
    /// <param name="value">The value, such as keywords, a channel, a time or off.</param>
    [SlashCommand("edit", "Changes an AutoMod rule")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Edit(
        [Summary("rule", "The rule ID or name")] string rule,
        [Summary("option", "What to change")] EditOption option,
        [Summary("value", "Comma separated list, channel, time or off")]
        string? value = null)
    {
        var found = await FindAsync(rule);
        if (found is null)
            return;

        await ReplyAsync(await Service.EditAsync(ctx.Guild, found, option.ToString(), value));
    }

    /// <summary>
    ///     Deletes a native AutoMod rule.
    /// </summary>
    /// <param name="rule">The rule ID or name.</param>
    [SlashCommand("delete", "Deletes an AutoMod rule")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Delete([Summary("rule", "The rule ID or name")] string rule)
    {
        var found = await FindAsync(rule);
        if (found is null)
            return;

        await ReplyAsync(await Service.DeleteAsync(ctx.Guild, found.Id));
    }

    /// <summary>
    ///     Shows or changes what Mewdeko does when a native AutoMod rule fires.
    /// </summary>
    /// <param name="rule">The rule ID or name.</param>
    /// <param name="option">What to change.</param>
    /// <param name="value">The value, such as on, a reason, timeout 10m, kick 3 1h, a channel or off.</param>
    [SlashCommand("followup", "Shows or sets what Mewdeko does when a rule fires")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task FollowUp(
        [Summary("rule", "The rule ID or name")] string rule,
        [Summary("option", "What to change")] FollowUpOption option = FollowUpOption.Show,
        [Summary("value", "on, off, a reason, timeout 10m, mute 1h, kick 3 1h, ban 5 1d or a channel")]
        string? value = null)
    {
        var guildId = ctx.Guild.Id;
        var found = await FindAsync(rule);
        if (found is null)
            return;

        if (option == FollowUpOption.Show)
        {
            var followUp = await Service.GetFollowUpAsync(guildId, found.Id);
            if (followUp is null)
            {
                await ReplyErrorAsync(Strings.AutomodFollowupNone(guildId));
                return;
            }

            await RespondAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.AutomodFollowupTitle(guildId, found.Name))
                .WithDescription(Service.FormatFollowUp(guildId, followUp))
                .Build(), allowedMentions: AllowedMentions.None);
            return;
        }

        var (ok, message) = await Service.SetFollowUpOptionAsync(ctx.Guild, found.Id, option.ToString(), value);
        if (ok)
            await ReplyConfirmAsync(message);
        else
            await ReplyErrorAsync(message);
    }

    private async Task<IAutoModRule?> FindAsync(string rule)
    {
        if (!await DiscordAutoModService.CanManageAsync(ctx.Guild))
        {
            await ReplyErrorAsync(Strings.AutomodNeedPermission(ctx.Guild.Id));
            return null;
        }

        var found = await Service.FindRuleAsync(ctx.Guild, rule);
        if (found is null)
            await ReplyErrorAsync(Strings.AutomodNotFound(ctx.Guild.Id));
        return found;
    }

    private Task ReplyAsync(AutoModResult result) =>
        result.Ok ? ReplyConfirmAsync(result.Message) : ReplyErrorAsync(result.Message);
}
