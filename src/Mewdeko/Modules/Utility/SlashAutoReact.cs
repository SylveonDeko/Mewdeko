using DataModel;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

/// <summary>
///     Slash commands for rules that react to new messages in a channel.
/// </summary>
[Group("autoreact", "React to new messages in a channel")]
public class SlashAutoReact : MewdekoSlashSubmodule<AutoReactionService>
{
    /// <summary>
    ///     Adds a rule.
    /// </summary>
    /// <param name="channel">The text, announcement or forum channel.</param>
    /// <param name="emojis">The emojis to add, in order.</param>
    /// <param name="type">Which messages to react to.</param>
    [SlashCommand("add", "Add a rule that reacts to messages in a channel")]
    [RequireContext(ContextType.Guild)]
    [SlashAutoReactManager]
    public async Task Add(
        [ChannelTypes(ChannelType.Text, ChannelType.News, ChannelType.Forum)]
        IGuildChannel channel,
        [Summary("emojis", "Emojis to add, in order")]
        string emojis,
        [Summary("type", "Which messages to react to")]
        AutoReactionMessageType type = AutoReactionMessageType.Any)
    {
        var guildId = ctx.Guild.Id;
        var bad = AutoReactionService.TryParseEmojis(ctx.Guild, emojis, out var stored);
        if (bad is not null)
        {
            await EphemeralReplyErrorAsync(Strings.AutoReactBadEmoji(guildId, bad));
            return;
        }

        var (error, rule) = await Service.AddAsync(guildId, new AutoReaction
        {
            ChannelId = channel.Id, Emojis = stored, MessageTypes = type
        });
        if (rule is null)
        {
            await EphemeralReplyErrorAsync(Service.ErrorMessage(guildId, error, 0, "/"));
            return;
        }

        await ReplyConfirmAsync(Strings.AutoReactAdded(guildId, rule.Id, $"<#{channel.Id}>"));
        await WarnAboutPermsAsync(channel);
    }

    /// <summary>
    ///     Changes a rule. Lists take mentions or IDs, words take commas, and none clears a list.
    /// </summary>
    [SlashCommand("edit", "Change a rule")]
    [RequireContext(ContextType.Guild)]
    [SlashAutoReactManager]
    public async Task Edit(
        [Summary("rule", "The rule number")] int id,
        [ChannelTypes(ChannelType.Text, ChannelType.News, ChannelType.Forum)]
        IGuildChannel? channel = null,
        [Summary("emojis", "Emojis to add, in order")]
        string? emojis = null,
        [Summary("type", "Which messages to react to")]
        AutoReactionMessageType? type = null,
        [Summary("ignore-bots", "Skip messages from bots")]
        bool? ignoreBots = null,
        [Summary("ignore-webhooks", "Skip messages from webhooks")]
        bool? ignoreWebhooks = null,
        [Summary("ignore-replies", "Skip replies")]
        bool? ignoreReplies = null,
        [Summary("allowed-roles", "Roles that can trigger it, or none")]
        string? allowedRoles = null,
        [Summary("ignored-roles", "Roles that never trigger it, or none")]
        string? ignoredRoles = null,
        [Summary("ignored-members", "Members that never trigger it, or none")]
        string? ignoredMembers = null,
        [Summary("skip-words", "Words that stop it, separated by commas, or none")]
        string? skipWords = null,
        [Summary("required-words", "Words of which one is needed, separated by commas, or none")]
        string? requiredWords = null,
        [Summary("delete-other", "Delete messages that do not fit the type")]
        bool? deleteOther = null)
    {
        var guildId = ctx.Guild.Id;
        var storedEmojis = "";
        if (emojis is not null)
        {
            var bad = AutoReactionService.TryParseEmojis(ctx.Guild, emojis, out storedEmojis);
            if (bad is not null)
            {
                await EphemeralReplyErrorAsync(Strings.AutoReactBadEmoji(guildId, bad));
                return;
            }
        }

        var skip = Words(skipWords);
        var required = Words(requiredWords);
        if (skip is null || required is null)
        {
            await EphemeralReplyErrorAsync(Strings.AutoReactBadWords(guildId, AutoReactionService.MaxWords,
                AutoReactionService.MaxWordLength));
            return;
        }

        var (error, _) = await Service.UpdateAsync(guildId, id, x =>
        {
            if (channel is not null)
                x.ChannelId = channel.Id;
            if (emojis is not null)
                x.Emojis = storedEmojis;
            if (type is { } messageType)
                x.MessageTypes = messageType;
            if (ignoreBots is { } bots)
                x.IgnoreBots = bots;
            if (ignoreWebhooks is { } webhooks)
                x.IgnoreWebhooks = webhooks;
            if (ignoreReplies is { } replies)
                x.IgnoreReplies = replies;
            if (allowedRoles is not null)
                x.AllowedRoleIds = Ids(allowedRoles);
            if (ignoredRoles is not null)
                x.IgnoredRoleIds = Ids(ignoredRoles);
            if (ignoredMembers is not null)
                x.IgnoredUserIds = Ids(ignoredMembers);
            if (skipWords is not null)
                x.SkipWords = skip;
            if (requiredWords is not null)
                x.RequiredWords = required;
            if (deleteOther is { } delete)
                x.DeleteNonMatching = delete;
        });

        if (error != AutoReactionError.None)
        {
            await EphemeralReplyErrorAsync(Service.ErrorMessage(guildId, error, id, "/"));
            return;
        }

        await ReplyConfirmAsync(Strings.AutoReactUpdated(guildId, id));
        if (channel is not null)
            await WarnAboutPermsAsync(channel);
        if (deleteOther == true && await Service.GetRuleAsync(guildId, id) is { } rule &&
            await ctx.Guild.GetChannelAsync(rule.ChannelId) is { } ruleChannel &&
            !(await ctx.Guild.GetCurrentUserAsync()).GetPermissions(ruleChannel).ManageMessages)
            await FollowupAsync(Strings.AutoReactMissingDeletePerm(guildId, $"<#{ruleChannel.Id}>"), ephemeral: true);
    }

    /// <summary>
    ///     Moves a rule in the order, which sets the order its reactions are added in.
    /// </summary>
    /// <param name="id">The rule number.</param>
    /// <param name="position">The new position, counting from 1.</param>
    [SlashCommand("move", "Move a rule in the order")]
    [RequireContext(ContextType.Guild)]
    [SlashAutoReactManager]
    public async Task Move([Summary("rule", "The rule number")] int id,
        [Summary("position", "The new position in the list")] [MinValue(1)]
        int position)
    {
        var (found, moved) = await Service.MoveAsync(ctx.Guild.Id, id, position);
        if (found)
            await ReplyConfirmAsync(Strings.AutoReactMoved(ctx.Guild.Id, id, moved));
        else
            await EphemeralReplyErrorAsync(Strings.AutoReactNotFound(ctx.Guild.Id, id, "/"));
    }

    /// <summary>
    ///     Shows or sets the role whose members can manage auto reactions without Manage Server.
    /// </summary>
    /// <param name="role">The role. Leave empty to show the current one.</param>
    /// <param name="clear">Remove the manager role.</param>
    [SlashCommand("manager", "Set the role that can manage auto reactions without Manage Server")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Manager([Summary("role", "The manager role")] IRole? role = null,
        [Summary("clear", "Remove the manager role")]
        bool clear = false)
    {
        var guildId = ctx.Guild.Id;
        if (clear)
        {
            await Service.SetManagerRoleAsync(guildId, null);
            await ReplyConfirmAsync(Strings.AutoReactManagerCleared(guildId));
            return;
        }

        if (role is not null)
        {
            await Service.SetManagerRoleAsync(guildId, role.Id);
            await ReplyConfirmAsync(Strings.AutoReactManagerSet(guildId, role.Mention));
            return;
        }

        if (await Service.GetManagerRoleAsync(guildId) is { } current)
            await EphemeralReplyConfirmAsync(Strings.AutoReactManagerCurrent(guildId, $"<@&{current}>"));
        else
            await EphemeralReplyErrorAsync(Strings.AutoReactManagerNone(guildId, "/"));
    }

    /// <summary>
    ///     Removes a rule.
    /// </summary>
    /// <param name="id">The rule number.</param>
    [SlashCommand("remove", "Remove a rule")]
    [RequireContext(ContextType.Guild)]
    [SlashAutoReactManager]
    public async Task Remove([Summary("rule", "The rule number")] int id)
    {
        if (await Service.RemoveAsync(ctx.Guild.Id, id))
            await ReplyConfirmAsync(Strings.AutoReactRemoved(ctx.Guild.Id, id));
        else
            await EphemeralReplyErrorAsync(Strings.AutoReactNotFound(ctx.Guild.Id, id, "/"));
    }

    /// <summary>
    ///     Turns a rule on or off.
    /// </summary>
    /// <param name="id">The rule number.</param>
    /// <param name="enabled">Whether the rule is on.</param>
    [SlashCommand("toggle", "Turn a rule on or off")]
    [RequireContext(ContextType.Guild)]
    [SlashAutoReactManager]
    public async Task Toggle([Summary("rule", "The rule number")] int id, bool enabled)
    {
        var guildId = ctx.Guild.Id;
        var (error, _) = await Service.UpdateAsync(guildId, id, x => x.Enabled = enabled);
        if (error != AutoReactionError.None)
            await EphemeralReplyErrorAsync(Service.ErrorMessage(guildId, error, id, "/"));
        else
            await ReplyConfirmAsync(enabled ? Strings.AutoReactEnabled(guildId, id) : Strings.AutoReactDisabled(guildId, id));
    }

    /// <summary>
    ///     Lists the rules.
    /// </summary>
    [SlashCommand("list", "List the rules")]
    [RequireContext(ContextType.Guild)]
    [SlashAutoReactManager]
    public async Task List()
    {
        var rules = await Service.GetRulesAsync(ctx.Guild.Id);
        if (rules.Count == 0)
        {
            await EphemeralReplyErrorAsync(Strings.AutoReactListEmpty(ctx.Guild.Id, "/"));
            return;
        }

        await RespondAsync(embed: Service.ListEmbed(ctx.Guild.Id, rules), allowedMentions: AllowedMentions.None);
    }

    /// <summary>
    ///     Shows one rule.
    /// </summary>
    /// <param name="id">The rule number.</param>
    [SlashCommand("show", "Show a rule")]
    [RequireContext(ContextType.Guild)]
    [SlashAutoReactManager]
    public async Task Show([Summary("rule", "The rule number")] int id)
    {
        var rule = await Service.GetRuleAsync(ctx.Guild.Id, id);
        if (rule is null)
            await EphemeralReplyErrorAsync(Strings.AutoReactNotFound(ctx.Guild.Id, id, "/"));
        else
            await RespondAsync(embed: Service.ShowEmbed(rule), allowedMentions: AllowedMentions.None);
    }

    private static string Ids(string text) =>
        text.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)
            ? ""
            : AutoReactionService.JoinIds(AutoReactionService.ExtractIds(text));

    private static string? Words(string? text) =>
        text is null ? "" :
        text.Trim().Equals("none", StringComparison.OrdinalIgnoreCase) ? "" :
        AutoReactionService.JoinWords(text.Split(','));

    private async Task WarnAboutPermsAsync(IGuildChannel channel)
    {
        var me = await ctx.Guild.GetCurrentUserAsync();
        var perms = me.GetPermissions(channel);
        if (!perms.AddReactions || !perms.ReadMessageHistory)
            await FollowupAsync(Strings.AutoReactMissingPerms(ctx.Guild.Id, $"<#{channel.Id}>"), ephemeral: true);
    }
}
