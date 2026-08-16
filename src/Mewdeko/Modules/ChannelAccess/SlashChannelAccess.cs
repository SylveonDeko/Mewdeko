using DataModel;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.ChannelAccess.Services;

namespace Mewdeko.Modules.ChannelAccess;

/// <summary>
///     Lets members apply for access to locked channels and lets the people already inside vote them in.
/// </summary>
[Group("channelaccess", "Apply for and vote on access to locked channels.")]
public class SlashChannelAccess : MewdekoSlashModuleBase<ChannelAccessService>
{
    private readonly ILogger<SlashChannelAccess> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SlashChannelAccess" /> class.
    /// </summary>
    /// <param name="logger">The logger instance for structured logging.</param>
    public SlashChannelAccess(ILogger<SlashChannelAccess> logger)
    {
        this.logger = logger;
    }

    #region Setup

    /// <summary>
    ///     Opens applications for a locked channel, granting the given role to approved applicants.
    /// </summary>
    /// <param name="channel">The locked channel to gate.</param>
    /// <param name="accessRole">
    ///     The role granted when an application passes. Leave it out to add approved applicants to the
    ///     channel individually instead.
    /// </param>
    /// <example>/channelaccess setup #inner-circle @Inner Circle</example>
    [SlashCommand("setup", "Opens applications for a locked channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Setup(ITextChannel channel,
        [Summary("access_role", "Role granted on approval. Leave empty to add applicants to the channel directly")]
        IRole? accessRole = null)
    {
        if (await Service.GetConfigAsync(ctx.Guild.Id, channel.Id) is not null)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessGateExists(ctx.Guild.Id));
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
    /// <example>/channelaccess remove #inner-circle</example>
    [SlashCommand("remove", "Removes a channel's access gate")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Remove(ITextChannel channel)
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
        if (config is null)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
            return;
        }

        await Service.DeleteConfigAsync(config.Id);
        await ConfirmAsync(Strings.ChannelAccessGateDeleted(ctx.Guild.Id, channel.Mention));
    }

    /// <summary>
    ///     Lists every gate in the server with its current thresholds.
    /// </summary>
    /// <example>/channelaccess list</example>
    [SlashCommand("list", "Lists the server's access gates")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task ListGates()
    {
        var configs = await Service.GetConfigsAsync(ctx.Guild.Id);
        if (configs.Count == 0)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessNoGates(ctx.Guild.Id));
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

        await ctx.Interaction.RespondAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Changes a gate's settings. Every option is optional, so pass only what you want to change.
    /// </summary>
    /// <param name="channel">The gated channel.</param>
    /// <param name="enabled">Whether applications are accepted.</param>
    /// <param name="approvals">Approving votes needed to let someone in.</param>
    /// <param name="denials">Denying votes needed to turn someone away.</param>
    /// <param name="voteHours">How long a vote stays open, in hours. 0 for no limit.</param>
    /// <param name="onExpiry">What happens when the voting window closes.</param>
    /// <param name="reviewChannel">Where applications get posted for voting.</param>
    /// <param name="logChannel">Where decisions get logged.</param>
    /// <param name="voterRole">Role allowed to vote, instead of everyone with access.</param>
    /// <param name="pingRole">Role pinged when a new application arrives.</param>
    /// <param name="anonymousApplicant">Hide who applied until the vote closes.</param>
    /// <param name="anonymousVotes">Hide who voted which way.</param>
    /// <param name="allowAbstain">Offer an abstain button.</param>
    /// <param name="minAccountAgeDays">Minimum Discord account age to apply.</param>
    /// <param name="minServerAgeDays">Minimum time in the server to apply.</param>
    /// <param name="reapplyCooldownHours">How long a rejected applicant must wait to try again.</param>
    /// <param name="dmOnDecision">DM the applicant when a decision lands.</param>
    /// <example>/channelaccess config channel:#inner-circle approvals:5 vote_hours:48</example>
    [SlashCommand("config", "Changes an access gate's settings")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Settings(
        ITextChannel channel,
        bool? enabled = null,
        int? approvals = null,
        int? denials = null,
        [Summary("vote_hours", "How long a vote stays open, in hours. 0 for no limit")]
        int? voteHours = null,
        [Summary("on_expiry", "What happens when the voting window closes")]
        AccessExpiryBehavior? onExpiry = null,
        [Summary("review_channel", "Where applications get posted for voting")]
        ITextChannel? reviewChannel = null,
        [Summary("log_channel", "Where decisions get logged")]
        ITextChannel? logChannel = null,
        [Summary("voter_role", "Role allowed to vote, instead of everyone with access")]
        IRole? voterRole = null,
        [Summary("ping_role", "Role pinged when a new application arrives")]
        IRole? pingRole = null,
        [Summary("anonymous_applicant", "Hide who applied until the vote closes")]
        bool? anonymousApplicant = null,
        [Summary("anonymous_votes", "Hide who voted which way")]
        bool? anonymousVotes = null,
        [Summary("allow_abstain", "Offer an abstain button")]
        bool? allowAbstain = null,
        [Summary("min_account_age_days", "Minimum Discord account age to apply")]
        int? minAccountAgeDays = null,
        [Summary("min_server_age_days", "Minimum time in the server to apply")]
        int? minServerAgeDays = null,
        [Summary("reapply_cooldown_hours", "How long a rejected applicant waits before trying again")]
        int? reapplyCooldownHours = null,
        [Summary("dm_on_decision", "DM the applicant when a decision lands")]
        bool? dmOnDecision = null)
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
        if (config is null)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
            return;
        }

        if (enabled is not null) config.Enabled = enabled.Value;
        if (approvals is not null) config.RequiredApprovals = Math.Max(0, approvals.Value);
        if (denials is not null) config.RequiredDenials = Math.Max(0, denials.Value);
        if (voteHours is not null) config.VoteDurationHours = Math.Max(0, voteHours.Value);
        if (onExpiry is not null) config.OnExpiry = (int)onExpiry.Value;
        if (reviewChannel is not null) config.ReviewChannelId = reviewChannel.Id;
        if (logChannel is not null) config.LogChannelId = logChannel.Id;
        if (voterRole is not null) config.VoterRoleId = voterRole.Id;
        if (pingRole is not null) config.PingRoleId = pingRole.Id;
        if (anonymousApplicant is not null) config.AnonymousApplicant = anonymousApplicant.Value;
        if (anonymousVotes is not null) config.AnonymousVotes = anonymousVotes.Value;
        if (allowAbstain is not null) config.AllowAbstain = allowAbstain.Value;
        if (minAccountAgeDays is not null) config.MinAccountAgeDays = Math.Max(0, minAccountAgeDays.Value);
        if (minServerAgeDays is not null) config.MinServerAgeDays = Math.Max(0, minServerAgeDays.Value);
        if (reapplyCooldownHours is not null) config.ReapplyCooldownHours = Math.Max(0, reapplyCooldownHours.Value);
        if (dmOnDecision is not null) config.DmOnDecision = dmOnDecision.Value;

        await Service.UpdateConfigAsync(config);
        await ConfirmAsync(Strings.ChannelAccessConfigUpdated(ctx.Guild.Id));
    }

    /// <summary>
    ///     Posts a button people can click to apply for a gated channel.
    /// </summary>
    /// <param name="channel">The gated channel.</param>
    /// <param name="target">Where to post the panel. Defaults to the current channel.</param>
    /// <example>/channelaccess panel #inner-circle #rules</example>
    [SlashCommand("panel", "Posts an apply button for a gated channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Panel(ITextChannel channel, ITextChannel? target = null)
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
        if (config is null)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
            return;
        }

        target ??= ctx.Channel as ITextChannel;
        if (target is null)
            return;

        if (await Service.PostPanelAsync(config, target.Id) is null)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessPanelFailed(ctx.Guild.Id));
            return;
        }

        await EphemeralReplyConfirmAsync(Strings.ChannelAccessPanelPosted(ctx.Guild.Id, target.Mention));
    }

    #endregion

    #region Questions

    /// <summary>
    ///     Adds a question to a gate's application form. A gate can have up to five.
    /// </summary>
    /// <param name="channel">The gated channel.</param>
    /// <param name="question">The question text, up to 45 characters.</param>
    /// <param name="required">Whether the applicant must answer it.</param>
    /// <param name="paragraph">Whether the answer box is multi-line.</param>
    /// <param name="placeholder">Optional grey hint text inside the box.</param>
    /// <example>/channelaccess questionadd #inner-circle "Why do you want in?"</example>
    [SlashCommand("questionadd", "Adds a question to a gate's application form")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task QuestionAdd(ITextChannel channel, string question, bool required = true,
        bool paragraph = true, string? placeholder = null)
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
        if (config is null)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
            return;
        }

        if (!await Service.AddQuestionAsync(config.Id, question, required, paragraph, placeholder))
        {
            await EphemeralReplyErrorAsync(
                Strings.ChannelAccessQuestionLimit(ctx.Guild.Id, ChannelAccessService.MaxQuestions));
            return;
        }

        await ConfirmAsync(Strings.ChannelAccessQuestionAdded(ctx.Guild.Id));
    }

    /// <summary>
    ///     Removes a question from a gate's application form by its listed position.
    /// </summary>
    /// <param name="channel">The gated channel.</param>
    /// <param name="position">The one-based position shown by questionlist.</param>
    /// <example>/channelaccess questionremove #inner-circle 2</example>
    [SlashCommand("questionremove", "Removes a question from a gate's application form")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task QuestionRemove(ITextChannel channel, int position)
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
        if (config is null)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
            return;
        }

        if (!await Service.RemoveQuestionAsync(config.Id, position))
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessQuestionMissing(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.ChannelAccessQuestionRemoved(ctx.Guild.Id));
    }

    /// <summary>
    ///     Shows a gate's application questions in the order applicants see them.
    /// </summary>
    /// <param name="channel">The gated channel.</param>
    /// <example>/channelaccess questionlist #inner-circle</example>
    [SlashCommand("questionlist", "Shows a gate's application questions")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task QuestionList(ITextChannel channel)
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
        if (config is null)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
            return;
        }

        var questions = await Service.GetQuestionsAsync(config.Id);
        if (questions.Count == 0)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessNoQuestions(ctx.Guild.Id));
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.ChannelAccessQuestionsTitle(ctx.Guild.Id, channel.Mention))
            .WithDescription(string.Join("\n",
                questions.Select((q, i) =>
                    $"`{i + 1}.` {q.Question}{(q.Required ? string.Empty : " *(optional)*")}")));

        await ctx.Interaction.RespondAsync(embed: embed.Build());
    }

    #endregion

    #region Applying

    /// <summary>
    ///     Applies for access to a locked channel.
    /// </summary>
    /// <param name="channel">The channel to apply for.</param>
    /// <example>/channelaccess apply #inner-circle</example>
    [SlashCommand("apply", "Applies for access to a locked channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Apply(ITextChannel channel)
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
        if (config is null)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
            return;
        }

        await StartApplicationAsync(config.Id);
    }

    /// <summary>
    ///     Pulls back one of your own open applications.
    /// </summary>
    /// <param name="applicationId">The application id shown on the review message.</param>
    /// <example>/channelaccess withdraw 12</example>
    [SlashCommand("withdraw", "Withdraws one of your open applications")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Withdraw(int applicationId)
    {
        var application = await Service.GetApplicationAsync(applicationId);
        if (application is null || application.GuildId != ctx.Guild.Id)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessApplicationNotFound(ctx.Guild.Id));
            return;
        }

        if (application.UserId != ctx.User.Id)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessNotYourApplication(ctx.Guild.Id));
            return;
        }

        if (application.Status != (int)AccessApplicationStatus.Pending)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessApplicationClosed(ctx.Guild.Id));
            return;
        }

        await Service.ResolveApplicationAsync(application, AccessApplicationStatus.Withdrawn, ctx.User.Id,
            Strings.ChannelAccessWithdrawnReason(ctx.Guild.Id));
        await EphemeralReplyConfirmAsync(Strings.ChannelAccessResolved(ctx.Guild.Id, applicationId, "withdrawn"));
    }

    /// <summary>
    ///     Shows a user's application history, or your own if no user is given.
    /// </summary>
    /// <param name="user">The user to look up.</param>
    /// <example>/channelaccess history @someone</example>
    [SlashCommand("history", "Shows a user's application history")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task History(IGuildUser? user = null)
    {
        user ??= ctx.User as IGuildUser;
        if (user is null)
            return;

        if (user.Id != ctx.User.Id && !((IGuildUser)ctx.User).GuildPermissions.ManageRoles)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessNotYourApplication(ctx.Guild.Id));
            return;
        }

        var applications = await Service.GetUserApplicationsAsync(ctx.Guild.Id, user.Id);
        if (applications.Count == 0)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessNoHistory(ctx.Guild.Id));
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

        await ctx.Interaction.RespondAsync(embed: embed.Build(), ephemeral: true);
    }

    #endregion

    #region Review

    /// <summary>
    ///     Lists the applications still waiting on a decision.
    /// </summary>
    /// <param name="channel">Optionally limit the list to one gated channel.</param>
    /// <example>/channelaccess pending</example>
    [SlashCommand("pending", "Lists applications still waiting on a decision")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Pending(ITextChannel? channel = null)
    {
        int? configId = null;
        if (channel is not null)
        {
            var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
            if (config is null)
            {
                await EphemeralReplyErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
                return;
            }

            configId = config.Id;
        }

        var applications = await Service.GetPendingApplicationsAsync(ctx.Guild.Id, configId);
        if (applications.Count == 0)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessNoPending(ctx.Guild.Id));
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

        await ctx.Interaction.RespondAsync(embed: embed.Build(), ephemeral: true);
    }

    /// <summary>
    ///     Forces an application through regardless of the vote count.
    /// </summary>
    /// <param name="applicationId">The application id.</param>
    /// <param name="reason">An optional note shown on the closed application.</param>
    /// <example>/channelaccess approve 12 vouched by staff</example>
    [SlashCommand("approve", "Approves an application regardless of the vote count")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Approve(int applicationId, string? reason = null)
    {
        await ForceResolveAsync(applicationId, AccessApplicationStatus.Approved, reason);
    }

    /// <summary>
    ///     Turns down an application regardless of the vote count.
    /// </summary>
    /// <param name="applicationId">The application id.</param>
    /// <param name="reason">An optional note shown on the closed application.</param>
    /// <example>/channelaccess deny 12 not a good fit</example>
    [SlashCommand("deny", "Denies an application regardless of the vote count")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Deny(int applicationId, string? reason = null)
    {
        await ForceResolveAsync(applicationId, AccessApplicationStatus.Denied, reason);
    }

    #endregion

    #region Blacklist

    /// <summary>
    ///     Blocks a user from applying, either for one channel or for every gated channel.
    /// </summary>
    /// <param name="user">The user to block.</param>
    /// <param name="channel">The gated channel, or leave empty to block them everywhere.</param>
    /// <param name="reason">An optional note for other staff.</param>
    /// <example>/channelaccess blacklistadd @someone</example>
    [SlashCommand("blacklistadd", "Blocks a user from applying for channel access")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task BlacklistAdd(IUser user, ITextChannel? channel = null, string? reason = null)
    {
        int? configId = null;
        if (channel is not null)
        {
            var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
            if (config is null)
            {
                await EphemeralReplyErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
                return;
            }

            configId = config.Id;
        }

        await Service.AddBlacklistAsync(ctx.Guild.Id, configId, user.Id, ctx.User.Id, reason);
        await ConfirmAsync(Strings.ChannelAccessBlacklistAdded(ctx.Guild.Id, user.Mention));
    }

    /// <summary>
    ///     Lifts a block so a user can apply again.
    /// </summary>
    /// <param name="user">The blocked user.</param>
    /// <param name="channel">The gated channel the block was set on, if it was channel specific.</param>
    /// <example>/channelaccess blacklistremove @someone</example>
    [SlashCommand("blacklistremove", "Lets a blocked user apply again")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task BlacklistRemove(IUser user, ITextChannel? channel = null)
    {
        int? configId = null;
        if (channel is not null)
        {
            var config = await Service.GetConfigAsync(ctx.Guild.Id, channel.Id);
            if (config is null)
            {
                await EphemeralReplyErrorAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id));
                return;
            }

            configId = config.Id;
        }

        if (!await Service.RemoveBlacklistAsync(ctx.Guild.Id, configId, user.Id))
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessBlacklistNotFound(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.ChannelAccessBlacklistRemoved(ctx.Guild.Id, user.Mention));
    }

    /// <summary>
    ///     Lists everyone blocked from applying in this server.
    /// </summary>
    /// <example>/channelaccess blacklist</example>
    [SlashCommand("blacklist", "Lists everyone blocked from applying")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [CheckPermissions]
    public async Task Blacklist()
    {
        var entries = await Service.GetBlacklistAsync(ctx.Guild.Id);
        if (entries.Count == 0)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessBlacklistEmpty(ctx.Guild.Id));
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

        await ctx.Interaction.RespondAsync(embed: embed.Build(), ephemeral: true);
    }

    #endregion

    #region Components

    /// <summary>
    ///     Handles the apply button on a posted access panel.
    /// </summary>
    /// <param name="configIdRaw">The gate id embedded in the button id.</param>
    [ComponentInteraction("chanacc:apply:*", true)]
    [RequireContext(ContextType.Guild)]
    public async Task HandleApplyButton(string configIdRaw)
    {
        if (!int.TryParse(configIdRaw, out var configId))
            return;

        await StartApplicationAsync(configId);
    }

    /// <summary>
    ///     Handles a vote button on an application review message.
    /// </summary>
    /// <param name="applicationIdRaw">The application id embedded in the button id.</param>
    /// <param name="voteRaw">The vote value embedded in the button id.</param>
    [ComponentInteraction("chanacc:vote:*:*", true)]
    [RequireContext(ContextType.Guild)]
    public async Task HandleVoteButton(string applicationIdRaw, string voteRaw)
    {
        if (!int.TryParse(applicationIdRaw, out var applicationId) || !int.TryParse(voteRaw, out var vote))
            return;

        await DeferAsync(true);

        var result = await Service.CastVoteAsync(applicationId, (IGuildUser)ctx.User, vote);
        var message = result switch
        {
            AccessVoteResult.Recorded => Strings.ChannelAccessVoteRecorded(ctx.Guild.Id),
            AccessVoteResult.Changed => Strings.ChannelAccessVoteChanged(ctx.Guild.Id),
            AccessVoteResult.Removed => Strings.ChannelAccessVoteRemoved(ctx.Guild.Id),
            AccessVoteResult.NotEligible => Strings.ChannelAccessVoteNotEligible(ctx.Guild.Id),
            AccessVoteResult.OwnApplication => Strings.ChannelAccessVoteOwn(ctx.Guild.Id),
            _ => Strings.ChannelAccessVoteNotPending(ctx.Guild.Id)
        };

        await FollowupAsync(message, ephemeral: true);
    }

    /// <summary>
    ///     Shows who voted which way on an application, unless the gate hides vote details.
    /// </summary>
    /// <param name="applicationIdRaw">The application id embedded in the button id.</param>
    [ComponentInteraction("chanacc:breakdown:*", true)]
    [RequireContext(ContextType.Guild)]
    public async Task HandleBreakdownButton(string applicationIdRaw)
    {
        if (!int.TryParse(applicationIdRaw, out var applicationId))
            return;

        await DeferAsync(true);

        var application = await Service.GetApplicationAsync(applicationId);
        if (application is null || application.GuildId != ctx.Guild.Id)
        {
            await FollowupAsync(Strings.ChannelAccessApplicationNotFound(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var config = await Service.GetConfigByIdAsync(application.ConfigId);
        if (config is null)
            return;

        if (!Service.CanVote(config, (IGuildUser)ctx.User))
        {
            await FollowupAsync(Strings.ChannelAccessVoteNotEligible(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var votes = await Service.GetVotesAsync(applicationId);
        if (votes.Count == 0)
        {
            await FollowupAsync(Strings.ChannelAccessNoVotes(ctx.Guild.Id), ephemeral: true);
            return;
        }

        if (config.AnonymousVotes && !((IGuildUser)ctx.User).GuildPermissions.ManageRoles)
        {
            await FollowupAsync(Strings.ChannelAccessVotesHidden(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.ChannelAccessBreakdownTitle(ctx.Guild.Id, applicationId))
            .WithDescription(string.Join("\n", votes
                .OrderByDescending(x => x.Vote)
                .Take(50)
                .Select(x => $"{x.Vote switch { 1 => "✅", -1 => "❌", _ => "🤷" }} <@{x.UserId}>")));

        await FollowupAsync(embed: embed.Build(), ephemeral: true);
    }

    /// <summary>
    ///     Handles the submitted application form and posts it for voting.
    /// </summary>
    /// <param name="configIdRaw">The gate id embedded in the modal id.</param>
    [ModalInteraction("chanacc:applymodal:*", true)]
    [RequireContext(ContextType.Guild)]
    public async Task HandleApplyModal(string configIdRaw)
    {
        if (!int.TryParse(configIdRaw, out var configId))
            return;

        await DeferAsync(true);

        var config = await Service.GetConfigByIdAsync(configId);
        if (config is null || config.GuildId != ctx.Guild.Id)
        {
            await FollowupAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var user = (IGuildUser)ctx.User;
        var (canApply, reason) = await Service.CanApplyAsync(config, user);
        if (!canApply)
        {
            await FollowupAsync(reason, ephemeral: true);
            return;
        }

        var questions = await Service.GetQuestionsAsync(configId);
        var submitted = ((IModalInteraction)ctx.Interaction).Data.Components.ToList();

        var answers = new List<(int?, string, string)>();
        for (var i = 0; i < questions.Count; i++)
        {
            var value = submitted.FirstOrDefault(x => x.CustomId == $"q{i}")?.Value;
            answers.Add((questions[i].Id, questions[i].Question,
                string.IsNullOrWhiteSpace(value) ? "*(no answer)*" : value));
        }

        await SubmitAndReplyAsync(config, user, answers);
    }

    #endregion

    #region Helpers

    private string DescribeGrant(ChannelAccessConfig config)
    {
        return (AccessGrantMode)config.GrantMode == AccessGrantMode.Role
            ? $"<@&{config.AccessRoleId}>"
            : Strings.ChannelAccessGrantDirect(ctx.Guild.Id);
    }

    private async Task StartApplicationAsync(int configId)
    {
        var config = await Service.GetConfigByIdAsync(configId);
        if (config is null || config.GuildId != ctx.Guild.Id)
        {
            await ctx.Interaction.RespondAsync(Strings.ChannelAccessNoGate(ctx.Guild.Id), ephemeral: true);
            return;
        }

        var user = (IGuildUser)ctx.User;
        var (canApply, reason) = await Service.CanApplyAsync(config, user);
        if (!canApply)
        {
            await ctx.Interaction.RespondAsync(reason, ephemeral: true);
            return;
        }

        var questions = await Service.GetQuestionsAsync(config.Id);
        if (questions.Count == 0)
        {
            await DeferAsync(true);
            await SubmitAndReplyAsync(config, user, []);
            return;
        }

        var channelName = (await ctx.Guild.GetTextChannelAsync(config.ChannelId))?.Name ?? "channel";
        var title = $"Apply for #{channelName}";
        var modal = new ModalBuilder()
            .WithCustomId($"chanacc:applymodal:{config.Id}")
            .WithTitle(title.Length > 45 ? title[..45] : title);

        for (var i = 0; i < questions.Count; i++)
        {
            var question = questions[i];
            modal.AddTextInput(question.Question, $"q{i}",
                question.Paragraph ? TextInputStyle.Paragraph : TextInputStyle.Short,
                question.Placeholder,
                question.MinLength == 0 ? null : question.MinLength,
                question.MaxLength,
                question.Required);
        }

        await ctx.Interaction.RespondWithModalAsync(modal.Build());
    }

    private async Task SubmitAndReplyAsync(ChannelAccessConfig config, IGuildUser user,
        List<(int?, string, string)> answers)
    {
        try
        {
            var application = await Service.SubmitApplicationAsync(config, user,
                answers.Select(x => (x.Item1, x.Item2, x.Item3)).ToList());

            await FollowupAsync(
                application is null
                    ? Strings.ChannelAccessApplyFailed(ctx.Guild.Id)
                    : Strings.ChannelAccessApplied(ctx.Guild.Id), ephemeral: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed submitting channel access application in guild {GuildId}", ctx.Guild.Id);
            await FollowupAsync(Strings.ChannelAccessApplyFailed(ctx.Guild.Id), ephemeral: true);
        }
    }

    private async Task ForceResolveAsync(int applicationId, AccessApplicationStatus status, string? reason)
    {
        var application = await Service.GetApplicationAsync(applicationId);
        if (application is null || application.GuildId != ctx.Guild.Id)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessApplicationNotFound(ctx.Guild.Id));
            return;
        }

        if (application.Status != (int)AccessApplicationStatus.Pending)
        {
            await EphemeralReplyErrorAsync(Strings.ChannelAccessApplicationClosed(ctx.Guild.Id));
            return;
        }

        await Service.ResolveApplicationAsync(application, status, ctx.User.Id,
            reason ?? Strings.ChannelAccessClosedByReason(ctx.Guild.Id, ctx.User.ToString()));
        await ConfirmAsync(Strings.ChannelAccessResolved(ctx.Guild.Id, applicationId,
            status.ToString().ToLowerInvariant()));
    }

    #endregion
}