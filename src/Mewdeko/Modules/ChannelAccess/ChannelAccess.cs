using DataModel;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.ChannelAccess.Services;

namespace Mewdeko.Modules.ChannelAccess;

/// <summary>
///     Prefix commands for the channel access system, where members apply for locked channels and the
///     people already inside vote on whether to let them in.
/// </summary>
public class ChannelAccess : MewdekoModuleBase<ChannelAccessService>
{
    /// <summary>
    ///     Opens applications for a locked channel, granting the given role to approved applicants.
    /// </summary>
    /// <param name="channel">The locked channel to gate.</param>
    /// <param name="accessRole">
    ///     The role granted when an application passes. Leave it out to add approved applicants to the
    ///     channel individually instead.
    /// </param>
    /// <example>.casetup #inner-circle @Inner Circle</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task ChannelAccessSetup(ITextChannel channel, IRole? accessRole = null)
    {
        if (await Service.GetConfigAsync(ctx.Guild.Id, channel.Id) is not null)
        {
            await ErrorAsync(Strings.ChannelAccessGateExists(ctx.Guild.Id));
            return;
        }

        await Service.CreateConfigAsync(ctx.Guild.Id, channel.Id, accessRole?.Id, ctx.User.Id);
        await ConfirmAsync(accessRole is null
            ? Strings.ChannelAccessGateCreatedDirect(ctx.Guild.Id, channel.Mention)
            : Strings.ChannelAccessGateCreated(ctx.Guild.Id, channel.Mention, accessRole.Mention));
    }

    /// <summary>
    ///     Removes a channel's gate along with its questions and application history.
    /// </summary>
    /// <param name="channel">The gated channel.</param>
    /// <example>.caremove #inner-circle</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task ChannelAccessRemove(ITextChannel channel)
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
        if (config is null)
        {
            await ErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
            return;
        }

        await Service.DeleteConfigAsync(config.Id);
        await ConfirmAsync(Strings.ChannelAccessGateDeleted(ctx.Guild.Id, channel.Mention));
    }

    /// <summary>
    ///     Lists every gate in the server with its current thresholds.
    /// </summary>
    /// <example>.calist</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task ChannelAccessList()
    {
        var configs = await Service.GetConfigsAsync(ctx.Guild.Id);
        if (configs.Count == 0)
        {
            await ErrorAsync(Strings.ChannelAccessNoGates(ctx.Guild.Id));
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.ChannelAccessGateListTitle(ctx.Guild.Id));

        foreach (var config in configs)
        {
            embed.AddField($"#{config.Id} • <#{config.ChannelId}>",
                $"Grants: {DescribeGrant(config)}\n" +
                $"Thresholds: ✅ {config.RequiredApprovals} / ❌ {config.RequiredDenials}\n" +
                $"Window: {(config.VoteDurationHours > 0 ? $"{config.VoteDurationHours}h" : "no limit")} " +
                $"({(AccessExpiryBehavior)config.OnExpiry} on expiry)\n" +
                $"Status: {(config.Enabled ? "open" : "closed")}");
        }

        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Changes one setting on a gate. Run it without a setting to see everything you can change.
    /// </summary>
    /// <param name="channel">The gated channel.</param>
    /// <param name="setting">The setting name.</param>
    /// <param name="value">The new value.</param>
    /// <example>.caconfig #inner-circle approvals 5</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task ChannelAccessConfig(ITextChannel channel, string? setting = null,
        [Remainder] string? value = null)
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
        if (config is null)
        {
            await ErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
            return;
        }

        if (string.IsNullOrWhiteSpace(setting) || string.IsNullOrWhiteSpace(value))
        {
            await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.ChannelAccessSettingsTitle(ctx.Guild.Id))
                .WithDescription(Strings.ChannelAccessSettingsList(ctx.Guild.Id))
                .Build());
            return;
        }

        var applied = ApplySetting(config, setting.ToLowerInvariant(), value.Trim());
        if (!applied)
        {
            await ErrorAsync(Strings.ChannelAccessSettingUnknown(ctx.Guild.Id, setting));
            return;
        }

        await Service.UpdateConfigAsync(config);
        await ConfirmAsync(Strings.ChannelAccessConfigUpdated(ctx.Guild.Id));
    }

    /// <summary>
    ///     Posts a button people can click to apply for a gated channel.
    /// </summary>
    /// <param name="channel">The gated channel.</param>
    /// <param name="target">Where to post the panel. Defaults to the current channel.</param>
    /// <example>.capanel #inner-circle #rules</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task ChannelAccessPanel(ITextChannel channel, ITextChannel? target = null)
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
        if (config is null)
        {
            await ErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
            return;
        }

        target ??= (ITextChannel)ctx.Channel;

        if (await Service.PostPanelAsync(config, target.Id) is null)
        {
            await ErrorAsync(Strings.ChannelAccessPanelFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.ChannelAccessPanelPosted(ctx.Guild.Id, target.Mention));
    }

    /// <summary>
    ///     Adds a question to a gate's application form. A gate can have up to five.
    /// </summary>
    /// <param name="channel">The gated channel.</param>
    /// <param name="question">The question text, up to 45 characters.</param>
    /// <example>.caqadd #inner-circle Why do you want in?</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task ChannelAccessQuestionAdd(ITextChannel channel, [Remainder] string question)
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
        if (config is null)
        {
            await ErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
            return;
        }

        if (!await Service.AddQuestionAsync(config.Id, question, true, true, null))
        {
            await ErrorAsync(Strings.ChannelAccessQuestionLimit(ctx.Guild.Id, ChannelAccessService.MaxQuestions));
            return;
        }

        await ConfirmAsync(Strings.ChannelAccessQuestionAdded(ctx.Guild.Id));
    }

    /// <summary>
    ///     Removes a question from a gate's application form by its listed position.
    /// </summary>
    /// <param name="channel">The gated channel.</param>
    /// <param name="position">The one-based position shown by the question list.</param>
    /// <example>.caqremove #inner-circle 2</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task ChannelAccessQuestionRemove(ITextChannel channel, int position)
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
        if (config is null)
        {
            await ErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
            return;
        }

        if (!await Service.RemoveQuestionAsync(config.Id, position))
        {
            await ErrorAsync(Strings.ChannelAccessQuestionMissing(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.ChannelAccessQuestionRemoved(ctx.Guild.Id));
    }

    /// <summary>
    ///     Shows a gate's application questions in the order applicants see them.
    /// </summary>
    /// <param name="channel">The gated channel.</param>
    /// <example>.caqlist #inner-circle</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task ChannelAccessQuestionList(ITextChannel channel)
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
        if (config is null)
        {
            await ErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
            return;
        }

        var questions = await Service.GetQuestionsAsync(config.Id);
        if (questions.Count == 0)
        {
            await ErrorAsync(Strings.ChannelAccessNoQuestions(ctx.Guild.Id));
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.ChannelAccessQuestionsTitle(ctx.Guild.Id, channel.Mention))
            .WithDescription(string.Join("\n",
                questions.Select((q, i) =>
                    $"`{i + 1}.` {q.Question}{(q.Required ? string.Empty : " *(optional)*")}")));

        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Posts the apply button for a gated channel so you can fill in the application form.
    /// </summary>
    /// <param name="channel">The channel to apply for.</param>
    /// <example>.caapply #inner-circle</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task ChannelAccessApply(ITextChannel channel)
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
        if (config is null)
        {
            await ErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
            return;
        }

        var (canApply, reason) = await Service.CanApplyAsync(config, (IGuildUser)ctx.User);
        if (!canApply)
        {
            await ErrorAsync(reason!);
            return;
        }

        await ctx.Channel.SendMessageAsync(Strings.ChannelAccessApplyPrompt(ctx.Guild.Id, channel.Mention),
            components: Service.BuildPanelComponents(config));
    }

    /// <summary>
    ///     Pulls back one of your own open applications.
    /// </summary>
    /// <param name="applicationId">The application id shown on the review message.</param>
    /// <example>.cawithdraw 12</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task ChannelAccessWithdraw(int applicationId)
    {
        var application = await Service.GetApplicationAsync(applicationId);
        if (application is null || application.GuildId != ctx.Guild.Id)
        {
            await ErrorAsync(Strings.ChannelAccessApplicationNotFound(ctx.Guild.Id));
            return;
        }

        if (application.UserId != ctx.User.Id)
        {
            await ErrorAsync(Strings.ChannelAccessNotYourApplication(ctx.Guild.Id));
            return;
        }

        if (application.Status != (int)AccessApplicationStatus.Pending)
        {
            await ErrorAsync(Strings.ChannelAccessApplicationClosed(ctx.Guild.Id));
            return;
        }

        await Service.ResolveApplicationAsync(application, AccessApplicationStatus.Withdrawn, ctx.User.Id,
            Strings.ChannelAccessWithdrawnReason(ctx.Guild.Id));
        await ConfirmAsync(Strings.ChannelAccessResolved(ctx.Guild.Id, applicationId, "withdrawn"));
    }

    /// <summary>
    ///     Shows a user's application history, or your own if no user is given.
    /// </summary>
    /// <param name="user">The user to look up.</param>
    /// <example>.cahistory @someone</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task ChannelAccessHistory(IGuildUser? user = null)
    {
        user ??= (IGuildUser)ctx.User;

        if (user.Id != ctx.User.Id && !((IGuildUser)ctx.User).GuildPermissions.ManageRoles)
        {
            await ErrorAsync(Strings.ChannelAccessNotYourApplication(ctx.Guild.Id));
            return;
        }

        var applications = await Service.GetUserApplicationsAsync(ctx.Guild.Id, user.Id);
        if (applications.Count == 0)
        {
            await ErrorAsync(Strings.ChannelAccessNoHistory(ctx.Guild.Id));
            return;
        }

        var lines = new List<string>();
        foreach (var application in applications.Take(20))
        {
            var config = await Service.GetConfigByIdAsync(application.ConfigId);
            var channelMention = config is null ? "deleted gate" : $"<#{config.ChannelId}>";
            lines.Add(
                $"`#{application.Id}` {channelMention} • **{(AccessApplicationStatus)application.Status}** • <t:{new DateTimeOffset(application.DateAdded ?? DateTime.UtcNow, TimeSpan.Zero).ToUnixTimeSeconds()}:R>");
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.ChannelAccessHistoryTitle(ctx.Guild.Id, user.ToString()))
            .WithDescription(string.Join("\n", lines));

        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Lists the applications still waiting on a decision.
    /// </summary>
    /// <param name="channel">Optionally limit the list to one gated channel.</param>
    /// <example>.capending</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task ChannelAccessPending(ITextChannel? channel = null)
    {
        int? configId = null;
        if (channel is not null)
        {
            var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
            if (config is null)
            {
                await ErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
                return;
            }

            configId = config.Id;
        }

        var applications = await Service.GetPendingApplicationsAsync(ctx.Guild.Id, configId);
        if (applications.Count == 0)
        {
            await ErrorAsync(Strings.ChannelAccessNoPending(ctx.Guild.Id));
            return;
        }

        var lines = new List<string>();
        foreach (var application in applications.Take(20))
        {
            var config = await Service.GetConfigByIdAsync(application.ConfigId);
            var votes = await Service.GetVotesAsync(application.Id);
            var applicantName = config?.AnonymousApplicant == true ? "hidden" : $"<@{application.UserId}>";
            lines.Add(
                $"`#{application.Id}` {applicantName} → <#{config?.ChannelId}> • ✅ {votes.Count(x => x.Vote == 1)} ❌ {votes.Count(x => x.Vote == -1)}");
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.ChannelAccessPendingTitle(ctx.Guild.Id))
            .WithDescription(string.Join("\n", lines));

        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Forces an application through regardless of the vote count.
    /// </summary>
    /// <param name="applicationId">The application id.</param>
    /// <param name="reason">An optional note shown on the closed application.</param>
    /// <example>.caapprove 12 vouched by staff</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task ChannelAccessApprove(int applicationId, [Remainder] string? reason = null)
    {
        await ForceResolveAsync(applicationId, AccessApplicationStatus.Approved, reason);
    }

    /// <summary>
    ///     Turns down an application regardless of the vote count.
    /// </summary>
    /// <param name="applicationId">The application id.</param>
    /// <param name="reason">An optional note shown on the closed application.</param>
    /// <example>.cadeny 12 not a good fit</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task ChannelAccessDeny(int applicationId, [Remainder] string? reason = null)
    {
        await ForceResolveAsync(applicationId, AccessApplicationStatus.Denied, reason);
    }

    /// <summary>
    ///     Blocks a user from applying, either for one channel or for every gated channel.
    /// </summary>
    /// <param name="user">The user to block.</param>
    /// <param name="channel">The gated channel, or leave empty to block them everywhere.</param>
    /// <example>.cabladd @someone</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task ChannelAccessBlacklistAdd(IUser user, ITextChannel? channel = null)
    {
        int? configId = null;
        if (channel is not null)
        {
            var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
            if (config is null)
            {
                await ErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
                return;
            }

            configId = config.Id;
        }

        await Service.AddBlacklistAsync(ctx.Guild.Id, configId, user.Id, ctx.User.Id, null);
        await ConfirmAsync(Strings.ChannelAccessBlacklistAdded(ctx.Guild.Id, user.Mention));
    }

    /// <summary>
    ///     Lifts a block so a user can apply again.
    /// </summary>
    /// <param name="user">The blocked user.</param>
    /// <param name="channel">The gated channel the block was set on, if it was channel specific.</param>
    /// <example>.cablremove @someone</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task ChannelAccessBlacklistRemove(IUser user, ITextChannel? channel = null)
    {
        int? configId = null;
        if (channel is not null)
        {
            var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
            if (config is null)
            {
                await ErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
                return;
            }

            configId = config.Id;
        }

        if (!await Service.RemoveBlacklistAsync(ctx.Guild.Id, configId, user.Id))
        {
            await ErrorAsync(Strings.ChannelAccessBlacklistNotFound(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.ChannelAccessBlacklistRemoved(ctx.Guild.Id, user.Mention));
    }

    /// <summary>
    ///     Lists everyone blocked from applying in this server.
    /// </summary>
    /// <example>.cablacklist</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task ChannelAccessBlacklist()
    {
        var entries = await Service.GetBlacklistAsync(ctx.Guild.Id);
        if (entries.Count == 0)
        {
            await ErrorAsync(Strings.ChannelAccessBlacklistEmpty(ctx.Guild.Id));
            return;
        }

        var lines = new List<string>();
        foreach (var entry in entries.Take(25))
        {
            var scope = "all gates";
            if (entry.ConfigId is { } id)
            {
                var config = await Service.GetConfigByIdAsync(id);
                scope = config is null ? "deleted gate" : $"<#{config.ChannelId}>";
            }

            lines.Add($"<@{entry.UserId}> • {scope}" +
                      (string.IsNullOrWhiteSpace(entry.Reason) ? string.Empty : $" • {entry.Reason}"));
        }

        var embed = new EmbedBuilder()
            .WithErrorColor()
            .WithTitle(Strings.ChannelAccessBlacklistTitle(ctx.Guild.Id))
            .WithDescription(string.Join("\n", lines));

        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    private string DescribeGrant(ChannelAccessConfig config)
    {
        return (AccessGrantMode)config.GrantMode == AccessGrantMode.Role
            ? $"<@&{config.AccessRoleId}>"
            : Strings.ChannelAccessGrantDirect(ctx.Guild.Id);
    }

    private async Task ForceResolveAsync(int applicationId, AccessApplicationStatus status, string? reason)
    {
        var application = await Service.GetApplicationAsync(applicationId);
        if (application is null || application.GuildId != ctx.Guild.Id)
        {
            await ErrorAsync(Strings.ChannelAccessApplicationNotFound(ctx.Guild.Id));
            return;
        }

        if (application.Status != (int)AccessApplicationStatus.Pending)
        {
            await ErrorAsync(Strings.ChannelAccessApplicationClosed(ctx.Guild.Id));
            return;
        }

        await Service.ResolveApplicationAsync(application, status, ctx.User.Id,
            reason ?? Strings.ChannelAccessClosedByReason(ctx.Guild.Id, ctx.User.ToString()));
        await ConfirmAsync(Strings.ChannelAccessResolved(ctx.Guild.Id, applicationId,
            status.ToString().ToLowerInvariant()));
    }

    private static bool ApplySetting(ChannelAccessConfig config, string setting, string value)
    {
        switch (setting)
        {
            case "enabled":
                if (!bool.TryParse(value, out var enabled)) return false;
                config.Enabled = enabled;
                return true;
            case "approvals":
                if (!int.TryParse(value, out var approvals)) return false;
                config.RequiredApprovals = Math.Max(0, approvals);
                return true;
            case "denials":
                if (!int.TryParse(value, out var denials)) return false;
                config.RequiredDenials = Math.Max(0, denials);
                return true;
            case "votehours":
                if (!int.TryParse(value, out var voteHours)) return false;
                config.VoteDurationHours = Math.Max(0, voteHours);
                return true;
            case "onexpiry":
                if (!Enum.TryParse<AccessExpiryBehavior>(value, true, out var behavior)) return false;
                config.OnExpiry = (int)behavior;
                return true;
            case "reviewchannel":
                if (!TryParseId(value, out var reviewChannel)) return false;
                config.ReviewChannelId = reviewChannel;
                return true;
            case "logchannel":
                if (!TryParseId(value, out var logChannel)) return false;
                config.LogChannelId = logChannel;
                return true;
            case "voterrole":
                if (!TryParseId(value, out var voterRole)) return false;
                config.VoterRoleId = voterRole;
                return true;
            case "pingrole":
                if (!TryParseId(value, out var pingRole)) return false;
                config.PingRoleId = pingRole;
                return true;
            case "anonymousapplicant":
                if (!bool.TryParse(value, out var anonApplicant)) return false;
                config.AnonymousApplicant = anonApplicant;
                return true;
            case "anonymousvotes":
                if (!bool.TryParse(value, out var anonVotes)) return false;
                config.AnonymousVotes = anonVotes;
                return true;
            case "allowabstain":
                if (!bool.TryParse(value, out var allowAbstain)) return false;
                config.AllowAbstain = allowAbstain;
                return true;
            case "minaccountage":
                if (!int.TryParse(value, out var minAccountAge)) return false;
                config.MinAccountAgeDays = Math.Max(0, minAccountAge);
                return true;
            case "minserverage":
                if (!int.TryParse(value, out var minServerAge)) return false;
                config.MinServerAgeDays = Math.Max(0, minServerAge);
                return true;
            case "reapplycooldown":
                if (!int.TryParse(value, out var cooldown)) return false;
                config.ReapplyCooldownHours = Math.Max(0, cooldown);
                return true;
            case "dmondecision":
                if (!bool.TryParse(value, out var dmOnDecision)) return false;
                config.DmOnDecision = dmOnDecision;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseId(string value, out ulong id)
    {
        var trimmed = value.Trim('<', '>', '#', '@', '&', '!');
        return ulong.TryParse(trimmed, out id);
    }
}