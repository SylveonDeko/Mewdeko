using DataModel;
using Discord.Interactions;
using Humanizer;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

/// <summary>
///     Slash commands for the caps filter, attachment spam, media-only channel and unsafe file rules, and for the extra
///     options on the existing protections.
/// </summary>
/// <param name="protection">The protection service.</param>
/// <param name="extras">The automod extras service.</param>
[Group("automod", "Caps, attachment spam, media-only channels and unsafe files")]
public class SlashAutomod(ProtectionService protection, AutomodExtrasService extras)
    : MewdekoSlashModuleBase<AutomodService>
{
    /// <summary>
    ///     Sets the channel every automod action is posted in, or turns the log off.
    /// </summary>
    /// <param name="channel">The channel, or nothing to turn the log off.</param>
    [SlashCommand("log", "Sets the channel every automod action is posted in")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Log([Summary("channel", "The log channel, empty to turn it off")] ITextChannel? channel = null)
    {
        await extras.UpdateAsync(ctx.Guild.Id, x => x.LogChannelId = channel?.Id).ConfigureAwait(false);
        await ReplyConfirmAsync(channel is null
            ? Strings.AutomodLogCleared(ctx.Guild.Id)
            : Strings.AutomodLogSet(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the staff channel flagged messages are sent to for review, or turns review off.
    /// </summary>
    /// <param name="channel">The channel, or nothing to turn review off.</param>
    [SlashCommand("review", "Sets the staff channel flagged messages are sent to for review")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Review(
        [Summary("channel", "The review channel, empty to turn it off")]
        ITextChannel? channel = null)
    {
        await extras.UpdateAsync(ctx.Guild.Id, x => x.ReviewChannelId = channel?.Id).ConfigureAwait(false);
        await ReplyConfirmAsync(channel is null
            ? Strings.AutomodReviewCleared(ctx.Guild.Id)
            : Strings.AutomodReviewSet(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles a role or channel as exempt from every automod rule, protection and filter.
    /// </summary>
    /// <param name="role">The role to toggle.</param>
    /// <param name="channel">The channel to toggle.</param>
    [SlashCommand("server-exempt", "Toggles a role or channel as exempt from all automod")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task ServerExempt(
        [Summary("role", "The role to toggle")]
        IRole? role = null,
        [Summary("channel", "The channel to toggle")]
        IChannel? channel = null)
    {
        if (role is null == channel is null)
        {
            await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var id = role?.Id ?? channel!.Id;
        var mention = role?.Mention ?? $"<#{channel!.Id}>";
        var added = await extras.ToggleExemptAsync(ctx.Guild.Id, id, role is not null).ConfigureAwait(false);
        await ReplyConfirmAsync(added
            ? Strings.AutomodServerExemptAdded(ctx.Guild.Id, mention)
            : Strings.AutomodServerExemptRemoved(ctx.Guild.Id, mention)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Handles the approve and dismiss buttons on a message sent for review.
    /// </summary>
    /// <param name="decision">Either approve or dismiss.</param>
    /// <param name="rule">The rule that flagged the message.</param>
    /// <param name="userId">The member who sent the message.</param>
    [ComponentInteraction("automodreview:*:*:*", true)]
    public async Task HandleReview(string decision, string rule, string userId)
    {
        var guildId = ctx.Guild.Id;
        if (ctx.User is not IGuildUser { GuildPermissions: var permissions } reviewer ||
            !(permissions.Administrator || permissions.ManageMessages || permissions.KickMembers ||
              permissions.BanMembers || permissions.ModerateMembers) ||
            !int.TryParse(rule, out var ruleId) || !ulong.TryParse(userId, out var memberId))
        {
            await ctx.Interaction.RespondAsync(Strings.AutomodReviewNoPerms(guildId), ephemeral: true)
                .ConfigureAwait(false);
            return;
        }

        var approve = decision == "approve";
        var inter = (IComponentInteraction)ctx.Interaction;
        var embed = inter.Message.Embeds.First().ToEmbedBuilder().WithFooter(approve
            ? Strings.AutomodReviewApproved(guildId, reviewer.Username)
            : Strings.AutomodReviewDismissed(guildId, reviewer.Username));
        await inter.UpdateAsync(x =>
        {
            x.Embed = embed.Build();
            x.Components = new ComponentBuilder().Build();
        }).ConfigureAwait(false);

        if (!approve || Service.Get(guildId, (AutomodRule)ruleId) is not { } setting ||
            await ctx.Guild.GetUserAsync(memberId).ConfigureAwait(false) is not SocketGuildUser member)
            return;

        await Service.ApplyPunishmentsAsync(setting, member).ConfigureAwait(false);
        await extras.LogAsync(guildId, memberId, Service.RuleName(guildId, setting.Rule), null,
            AutomodService.DescribeActions(setting.Actions & (AutomodAction.Warn | AutomodAction.Timeout |
                                                              AutomodAction.Mute | AutomodAction.Kick |
                                                              AutomodAction.Ban)), null).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows every automod rule and the extra options on the existing protections.
    /// </summary>
    [SlashCommand("show", "Shows the current automod settings")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Show()
    {
        await ctx.Interaction.RespondAsync(embed: Service.BuildOverview(ctx.Guild.Id, protection).Build())
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Turns the caps filter on or off.
    /// </summary>
    /// <param name="enable">Whether the filter is on.</param>
    /// <param name="minLength">The shortest message that is checked.</param>
    /// <param name="percent">The share of letters that must be uppercase.</param>
    /// <param name="actions">The actions to apply, for example "delete warn".</param>
    [SlashCommand("caps", "Turns the caps filter on or off")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Caps(
        [Summary("enable", "Turn the filter on or off")]
        bool enable,
        [Summary("min-length", "Shortest message that is checked")]
        int minLength = 20,
        [Summary("percent", "Share of letters that must be uppercase")]
        int percent = 70,
        [Summary("actions", "For example: delete warn dm")]
        string? actions = "delete")
    {
        if (enable && (minLength is < 1 or > 2000 || percent is < 1 or > 100))
        {
            await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await SetAsync(AutomodRule.Caps, enable, actions, x =>
        {
            x.MinLength = minLength;
            x.CapsPercent = percent;
        }).ConfigureAwait(false);
    }

    /// <summary>
    ///     Turns attachment spam protection on or off.
    /// </summary>
    /// <param name="enable">Whether the rule is on.</param>
    /// <param name="max">How many attachments one member may post in the window.</param>
    /// <param name="seconds">The length of the window in seconds.</param>
    /// <param name="actions">The actions to apply, for example "delete timeout".</param>
    [SlashCommand("attachments", "Turns attachment spam protection on or off")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Attachments(
        [Summary("enable", "Turn the rule on or off")]
        bool enable,
        [Summary("max", "Attachments allowed in the window")]
        int max = 5,
        [Summary("seconds", "Length of the window")]
        int seconds = 10,
        [Summary("actions", "For example: delete timeout")]
        string? actions = "delete")
    {
        if (enable && (max is < 1 or > 100 || seconds is < 1 or > 600))
        {
            await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await SetAsync(AutomodRule.Attachments, enable, actions, x =>
        {
            x.MaxAttachments = max;
            x.WindowSeconds = seconds;
        }).ConfigureAwait(false);
    }

    /// <summary>
    ///     Turns media-only channels on or off.
    /// </summary>
    /// <param name="enable">Whether the rule is on.</param>
    /// <param name="actions">The actions to apply, for example "delete dm".</param>
    [SlashCommand("media", "Turns media-only channels on or off")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task Media(
        [Summary("enable", "Turn the rule on or off")]
        bool enable,
        [Summary("actions", "For example: delete dm")]
        string? actions = "delete")
    {
        return SetAsync(AutomodRule.Media, enable, actions, _ =>
        {
        });
    }

    /// <summary>
    ///     Adds a channel to the media-only list, or removes it.
    /// </summary>
    /// <param name="channel">The channel to toggle.</param>
    [SlashCommand("media-channel", "Adds or removes a media-only channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task MediaChannel([Summary("channel", "The channel to toggle")] IChannel channel)
    {
        var added = await Service.ToggleChannelAsync(ctx.Guild.Id, AutomodRule.Media, channel.Id)
            .ConfigureAwait(false);
        var mention = $"<#{channel.Id}>";
        await ReplyConfirmAsync(added
            ? Strings.AutomodMediaChannelAdded(ctx.Guild.Id, mention)
            : Strings.AutomodMediaChannelRemoved(ctx.Guild.Id, mention)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Turns image-only channels on or off.
    /// </summary>
    /// <param name="enable">Whether the rule is on.</param>
    /// <param name="actions">The actions to apply, for example "delete dm".</param>
    [SlashCommand("image-only", "Turns image-only channels on or off")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task ImageOnly(
        [Summary("enable", "Turn the rule on or off")]
        bool enable,
        [Summary("actions", "For example: delete dm")]
        string? actions = "delete")
    {
        return SetAsync(AutomodRule.ImageOnly, enable, actions, _ =>
        {
        });
    }

    /// <summary>
    ///     Adds a channel to the image-only list, or removes it.
    /// </summary>
    /// <param name="channel">The channel to toggle.</param>
    [SlashCommand("image-channel", "Adds or removes an image-only channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task ImageChannel([Summary("channel", "The channel to toggle")] IChannel channel)
    {
        return ToggleChannelAsync(AutomodRule.ImageOnly, channel);
    }

    /// <summary>
    ///     Turns video-only channels on or off.
    /// </summary>
    /// <param name="enable">Whether the rule is on.</param>
    /// <param name="actions">The actions to apply, for example "delete dm".</param>
    [SlashCommand("video-only", "Turns video-only channels on or off")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task VideoOnly(
        [Summary("enable", "Turn the rule on or off")]
        bool enable,
        [Summary("actions", "For example: delete dm")]
        string? actions = "delete")
    {
        return SetAsync(AutomodRule.VideoOnly, enable, actions, _ =>
        {
        });
    }

    /// <summary>
    ///     Adds a channel to the video-only list, or removes it.
    /// </summary>
    /// <param name="channel">The channel to toggle.</param>
    [SlashCommand("video-channel", "Adds or removes a video-only channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task VideoChannel([Summary("channel", "The channel to toggle")] IChannel channel)
    {
        return ToggleChannelAsync(AutomodRule.VideoOnly, channel);
    }

    /// <summary>
    ///     Turns commands-only channels on or off.
    /// </summary>
    /// <param name="enable">Whether the rule is on.</param>
    /// <param name="actions">The actions to apply, for example "delete message".</param>
    [SlashCommand("commands-only", "Turns commands-only channels on or off")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task CommandsOnly(
        [Summary("enable", "Turn the rule on or off")]
        bool enable,
        [Summary("actions", "For example: delete message")]
        string? actions = "delete")
    {
        return SetAsync(AutomodRule.Commands, enable, actions, _ =>
        {
        });
    }

    /// <summary>
    ///     Adds a channel to the commands-only list, or removes it.
    /// </summary>
    /// <param name="channel">The channel to toggle.</param>
    [SlashCommand("commands-channel", "Adds or removes a commands-only channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task CommandsChannel([Summary("channel", "The channel to toggle")] IChannel channel)
    {
        return ToggleChannelAsync(AutomodRule.Commands, channel);
    }

    /// <summary>
    ///     Turns emoji spam protection on or off.
    /// </summary>
    /// <param name="enable">Whether the rule is on.</param>
    /// <param name="max">How many emojis a message may hold.</param>
    /// <param name="actions">The actions to apply, for example "delete warn".</param>
    [SlashCommand("emoji", "Turns emoji spam protection on or off")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Emoji(
        [Summary("enable", "Turn the rule on or off")]
        bool enable,
        [Summary("max", "Emojis allowed in one message")]
        int max = 10,
        [Summary("actions", "For example: delete warn")]
        string? actions = "delete")
    {
        if (enable && max is < 1 or > 200)
        {
            await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await SetAsync(AutomodRule.Emoji, enable, actions, x => x.MaxEmojis = max).ConfigureAwait(false);
    }

    /// <summary>
    ///     Turns spoiler spam protection on or off.
    /// </summary>
    /// <param name="enable">Whether the rule is on.</param>
    /// <param name="max">How many spoilers a message may hold.</param>
    /// <param name="actions">The actions to apply, for example "delete warn".</param>
    [SlashCommand("spoilers", "Turns spoiler spam protection on or off")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Spoilers(
        [Summary("enable", "Turn the rule on or off")]
        bool enable,
        [Summary("max", "Spoilers allowed in one message")]
        int max = 3,
        [Summary("actions", "For example: delete warn")]
        string? actions = "delete")
    {
        if (enable && max is < 1 or > 100)
        {
            await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await SetAsync(AutomodRule.Spoilers, enable, actions, x => x.MaxSpoilers = max).ConfigureAwait(false);
    }

    /// <summary>
    ///     Turns the zalgo text filter on or off.
    /// </summary>
    /// <param name="enable">Whether the rule is on.</param>
    /// <param name="marks">How many combining marks one character may carry.</param>
    /// <param name="actions">The actions to apply, for example "delete warn".</param>
    [SlashCommand("zalgo", "Turns the zalgo text filter on or off")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Zalgo(
        [Summary("enable", "Turn the rule on or off")]
        bool enable,
        [Summary("marks", "Combining marks allowed on one character")]
        int marks = 4,
        [Summary("actions", "For example: delete warn")]
        string? actions = "delete")
    {
        if (enable && marks is < 1 or > 30)
        {
            await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await SetAsync(AutomodRule.Zalgo, enable, actions, x => x.ZalgoThreshold = marks).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets whether messages in threads of media-only channels are allowed.
    /// </summary>
    /// <param name="allow">Whether threads are allowed.</param>
    [SlashCommand("media-threads", "Allows or checks messages in threads of media-only channels")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task MediaThreads([Summary("allow", "Allow messages in threads")] bool allow)
    {
        await Service.UpdateAsync(ctx.Guild.Id, AutomodRule.Media, x => x.AllowThreads = allow)
            .ConfigureAwait(false);
        await ReplyConfirmAsync(allow
            ? Strings.AutomodMediaThreadsOn(ctx.Guild.Id)
            : Strings.AutomodMediaThreadsOff(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Turns unsafe file blocking on or off.
    /// </summary>
    /// <param name="enable">Whether the rule is on.</param>
    /// <param name="actions">The actions to apply, for example "delete warn".</param>
    [SlashCommand("files", "Turns unsafe file blocking on or off")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public Task Files(
        [Summary("enable", "Turn the rule on or off")]
        bool enable,
        [Summary("actions", "For example: delete warn")]
        string? actions = "delete")
    {
        return SetAsync(AutomodRule.Files, enable, actions, _ =>
        {
        });
    }

    /// <summary>
    ///     Shows the blocked file types, adds or removes one, or puts the list back to the default.
    /// </summary>
    /// <param name="extension">The extension to toggle, or "reset". Leave empty to show the list.</param>
    [SlashCommand("file-type", "Shows, adds or removes a blocked file type")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task FileType([Summary("extension", "Extension to toggle, or reset")] string? extension = null)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            var list = AutomodService.GetExtensions(Service.Get(ctx.Guild.Id, AutomodRule.Files));
            await ReplyConfirmAsync(Strings.AutomodExtList(ctx.Guild.Id, string.Join(", ", list)))
                .ConfigureAwait(false);
            return;
        }

        if (extension.Trim().Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            await Service.ResetExtensionsAsync(ctx.Guild.Id).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.AutomodExtReset(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var normalized = extension.Trim().TrimStart('.').ToLowerInvariant();
        if (normalized.Length is 0 or > 16 || !normalized.All(char.IsLetterOrDigit))
        {
            await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var added = await Service.ToggleExtensionAsync(ctx.Guild.Id, normalized).ConfigureAwait(false);
        await ReplyConfirmAsync(added
            ? Strings.AutomodExtAdded(ctx.Guild.Id, normalized)
            : Strings.AutomodExtRemoved(ctx.Guild.Id, normalized)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets how long timeouts and mutes from a rule last.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="time">The duration.</param>
    [SlashCommand("duration", "Sets how long a rule's timeouts and mutes last")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Duration(
        [Summary("rule", "The rule")] AutomodRule rule,
        [Summary("time", "For example 10m or 1h")]
        TimeSpan time)
    {
        if (time < TimeSpan.FromMinutes(1))
        {
            await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (time.Days > 28)
        {
            await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.UpdateAsync(ctx.Guild.Id, rule, x => x.ActionMinutes = (int)time.TotalMinutes)
            .ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.AutomodDurationSet(ctx.Guild.Id, Service.RuleName(ctx.Guild.Id, rule),
            time.Humanize())).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the message a rule sends in the channel, or puts the default back.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="text">The message, or nothing for the default.</param>
    [SlashCommand("message", "Sets the message a rule sends in the channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Message(
        [Summary("rule", "The rule")] AutomodRule rule,
        [Summary("text", "The message, %user% mentions the member. Empty for the default")]
        string? text = null)
    {
        var custom = string.IsNullOrWhiteSpace(text) ? null : text.Trim().TrimTo(500);
        await Service.UpdateAsync(ctx.Guild.Id, rule, x => x.ActionMessage = custom).ConfigureAwait(false);
        var name = Service.RuleName(ctx.Guild.Id, rule);
        await ReplyConfirmAsync(custom is null
            ? Strings.AutomodMessageReset(ctx.Guild.Id, name)
            : Strings.AutomodMessageSet(ctx.Guild.Id, name)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles a role or channel as exempt from a rule.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="role">The role to toggle.</param>
    /// <param name="channel">The channel to toggle.</param>
    [SlashCommand("exempt", "Toggles a role or channel as exempt from a rule")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Exempt(
        [Summary("rule", "The rule")] AutomodRule rule,
        [Summary("role", "The role to toggle")]
        IRole? role = null,
        [Summary("channel", "The channel to toggle")]
        IChannel? channel = null)
    {
        if (role is null == channel is null)
        {
            await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var name = Service.RuleName(ctx.Guild.Id, rule);
        if (role is not null)
        {
            var added = await Service.ToggleExemptAsync(ctx.Guild.Id, rule, role.Id, true).ConfigureAwait(false);
            await ReplyConfirmAsync(added
                ? Strings.AutomodExemptRoleAdded(ctx.Guild.Id, role.Mention, name)
                : Strings.AutomodExemptRoleRemoved(ctx.Guild.Id, role.Mention, name)).ConfigureAwait(false);
            return;
        }

        var addedChannel = await Service.ToggleExemptAsync(ctx.Guild.Id, rule, channel!.Id, false)
            .ConfigureAwait(false);
        await ReplyConfirmAsync(addedChannel
            ? Strings.AutomodExemptChannelAdded(ctx.Guild.Id, $"<#{channel.Id}>", name)
            : Strings.AutomodExemptChannelRemoved(ctx.Guild.Id, $"<#{channel.Id}>", name)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets whether an existing protection also deletes the offending message and DMs the member.
    /// </summary>
    /// <param name="target">The protection.</param>
    /// <param name="deleteMessage">Whether to delete the message.</param>
    /// <param name="notifyUser">Whether to DM the member.</param>
    [SlashCommand("protection-options", "Makes a protection delete the message or DM the member")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task ProtectionOptions(
        [Summary("protection", "The protection")]
        ProtectionOptionTarget target,
        [Summary("delete-message", "Delete the offending message")]
        bool deleteMessage,
        [Summary("dm-member", "Send the member a DM")]
        bool notifyUser)
    {
        var name = AutomodService.ProtectionName(target);
        var result = await protection.SetProtectionOptionsAsync(ctx.Guild.Id, target,
            target == ProtectionOptionTarget.Pattern ? null : deleteMessage, notifyUser).ConfigureAwait(false);

        switch (result)
        {
            case ProtectionOptionResult.NotEnabled:
                await ReplyErrorAsync(Strings.ProtectionOptionNotEnabled(ctx.Guild.Id, name)).ConfigureAwait(false);
                return;
            case ProtectionOptionResult.Unsupported:
                await ReplyErrorAsync(Strings.ProtectionOptionUnsupported(ctx.Guild.Id, name)).ConfigureAwait(false);
                return;
        }

        await ReplyConfirmAsync(Strings.ProtectionOptionsSet(ctx.Guild.Id, name, deleteMessage, notifyUser))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles a role or channel as exempt from Anti-Mass-Mention, or a role as exempt from Anti-Pattern.
    /// </summary>
    /// <param name="target">The protection.</param>
    /// <param name="role">The role to toggle.</param>
    /// <param name="channel">The channel to toggle.</param>
    [SlashCommand("protection-exempt", "Toggles a role or channel as exempt from a protection")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task ProtectionExempt(
        [Summary("protection", "Anti-Mass-Mention or Anti-Pattern")]
        ProtectionOptionTarget target,
        [Summary("role", "The role to toggle")]
        IRole? role = null,
        [Summary("channel", "The channel to toggle")]
        IChannel? channel = null)
    {
        if (role is null == channel is null)
        {
            await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var id = role?.Id ?? channel!.Id;
        var mention = role?.Mention ?? $"<#{channel!.Id}>";
        var name = AutomodService.ProtectionName(target);
        var (result, added) = await protection.ToggleProtectionExemptAsync(ctx.Guild.Id, target, id, role is not null)
            .ConfigureAwait(false);

        switch (result)
        {
            case ProtectionOptionResult.NotEnabled:
                await ReplyErrorAsync(Strings.ProtectionOptionNotEnabled(ctx.Guild.Id, name)).ConfigureAwait(false);
                return;
            case ProtectionOptionResult.Unsupported:
                await ReplyErrorAsync(Strings.ProtectionOptionUnsupported(ctx.Guild.Id, name)).ConfigureAwait(false);
                return;
        }

        await ReplyConfirmAsync(added
            ? Strings.ProtectionExemptAdded(ctx.Guild.Id, mention, name)
            : Strings.ProtectionExemptRemoved(ctx.Guild.Id, mention, name)).ConfigureAwait(false);
    }

    private async Task ToggleChannelAsync(AutomodRule rule, IChannel channel)
    {
        var added = await Service.ToggleChannelAsync(ctx.Guild.Id, rule, channel.Id).ConfigureAwait(false);
        var mention = $"<#{channel.Id}>";
        var name = Service.RuleName(ctx.Guild.Id, rule);
        await ReplyConfirmAsync(added
            ? Strings.AutomodChannelAdded(ctx.Guild.Id, mention, name)
            : Strings.AutomodChannelRemoved(ctx.Guild.Id, mention, name)).ConfigureAwait(false);
    }

    private async Task SetAsync(AutomodRule rule, bool enable, string? actions, Action<AutomodRuleSetting> change)
    {
        var (ok, message) = enable
            ? await Service.EnableRuleAsync(ctx.Guild.Id, rule, actions, change).ConfigureAwait(false)
            : await Service.DisableRuleAsync(ctx.Guild.Id, rule).ConfigureAwait(false);
        await (ok ? ReplyConfirmAsync(message) : ReplyErrorAsync(message)).ConfigureAwait(false);
    }
}
