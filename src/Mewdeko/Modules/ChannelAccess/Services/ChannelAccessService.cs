using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.ChannelAccess.Services;

/// <summary>
///     Runs the channel access system: gates on locked channels, applications to join them,
///     and votes cast by the members who already have access.
/// </summary>
public class ChannelAccessService : INService, IReadyExecutor, IDisposable
{
    /// <summary>
    ///     The maximum number of application questions a gate can have, capped by Discord's modal limit.
    /// </summary>
    public const int MaxQuestions = 5;

    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<ChannelAccessService> logger;
    private readonly GeneratedBotStrings strings;
    private readonly SemaphoreSlim voteLock = new(1, 1);
    private Timer? expiryTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChannelAccessService" /> class.
    /// </summary>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="strings">The localization service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public ChannelAccessService(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        GeneratedBotStrings strings,
        ILogger<ChannelAccessService> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.strings = strings;
        this.logger = logger;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        expiryTimer?.Dispose();
        voteLock.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        expiryTimer = new Timer(_ => _ = ProcessExpiredApplicationsAsync(), null, TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
        return Task.CompletedTask;
    }

    #region Helpers

    private async Task LogAsync(ChannelAccessConfig config, string message)
    {
        if (config.LogChannelId is not { } logChannelId || logChannelId == 0)
            return;

        try
        {
            if (client.GetGuild(config.GuildId)?.GetTextChannel(logChannelId) is { } channel)
                await channel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithColor(Color.Blue)
                    .WithDescription(message)
                    .WithCurrentTimestamp()
                    .Build());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed writing channel access log for guild {GuildId}", config.GuildId);
        }
    }

    #endregion

    #region Configuration

    /// <summary>
    ///     Gets every access gate configured in a guild.
    /// </summary>
    /// <param name="guildId">The guild to look up.</param>
    /// <returns>The guild's gates, oldest first.</returns>
    public async Task<List<ChannelAccessConfig>> GetConfigsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ChannelAccessConfigs
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets the gate attached to a specific channel.
    /// </summary>
    /// <param name="guildId">The guild the channel belongs to.</param>
    /// <param name="channelId">The gated channel.</param>
    /// <returns>The gate, or null if the channel is not gated.</returns>
    public async Task<ChannelAccessConfig?> GetConfigAsync(ulong guildId, ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ChannelAccessConfigs
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.ChannelId == channelId);
    }

    /// <summary>
    ///     Gets a gate by its primary key.
    /// </summary>
    /// <param name="id">The gate id.</param>
    /// <returns>The gate, or null if it no longer exists.</returns>
    public async Task<ChannelAccessConfig?> GetConfigByIdAsync(int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ChannelAccessConfigs.FirstOrDefaultAsync(x => x.Id == id);
    }

    /// <summary>
    ///     Creates a gate for a channel. Pass a role to hand out on approval, or leave it null to add
    ///     approved applicants to the channel individually with a permission overwrite.
    /// </summary>
    /// <param name="guildId">The guild the gate belongs to.</param>
    /// <param name="channelId">The channel being gated.</param>
    /// <param name="accessRoleId">The role granted on approval, or null to add applicants directly.</param>
    /// <param name="creatorId">The user setting the gate up.</param>
    /// <returns>The newly created gate.</returns>
    public async Task<ChannelAccessConfig> CreateConfigAsync(ulong guildId, ulong channelId, ulong? accessRoleId,
        ulong creatorId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var config = new ChannelAccessConfig
        {
            GuildId = guildId,
            ChannelId = channelId,
            AccessRoleId = accessRoleId,
            GrantMode = (int)(accessRoleId is null ? AccessGrantMode.ChannelPermission : AccessGrantMode.Role),
            Enabled = true,
            RequiredApprovals = 3,
            RequiredDenials = 3,
            VoteDurationHours = 72,
            OnExpiry = (int)AccessExpiryBehavior.Deny,
            AllowAbstain = true,
            AnonymousVotes = false,
            AnonymousApplicant = false,
            MinAccountAgeDays = 0,
            MinServerAgeDays = 0,
            ReapplyCooldownHours = 168,
            DmOnDecision = true,
            CreatedBy = creatorId,
            DateAdded = DateTime.UtcNow
        };

        config.Id = await db.InsertWithInt32IdentityAsync(config);
        return config;
    }

    /// <summary>
    ///     Persists changes made to a gate.
    /// </summary>
    /// <param name="config">The modified gate.</param>
    public async Task UpdateConfigAsync(ChannelAccessConfig config)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);
    }

    /// <summary>
    ///     Deletes a gate along with its questions, applications and votes.
    /// </summary>
    /// <param name="configId">The gate to remove.</param>
    public async Task DeleteConfigAsync(int configId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.ChannelAccessConfigs.Where(x => x.Id == configId).DeleteAsync();
    }

    #endregion

    #region Questions

    /// <summary>
    ///     Gets a gate's application questions in display order.
    /// </summary>
    /// <param name="configId">The gate to read.</param>
    /// <returns>The ordered questions.</returns>
    public async Task<List<ChannelAccessQuestion>> GetQuestionsAsync(int configId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ChannelAccessQuestions
            .Where(x => x.ConfigId == configId)
            .OrderBy(x => x.Position)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    /// <summary>
    ///     Appends a question to a gate's application form.
    /// </summary>
    /// <param name="configId">The gate to add to.</param>
    /// <param name="question">The question text.</param>
    /// <param name="required">Whether the applicant must answer.</param>
    /// <param name="paragraph">Whether the answer box is multi-line.</param>
    /// <param name="placeholder">Optional placeholder text.</param>
    /// <returns>True if added, false if the gate already has the maximum number of questions.</returns>
    public async Task<bool> AddQuestionAsync(int configId, string question, bool required, bool paragraph,
        string? placeholder)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var count = await db.ChannelAccessQuestions.CountAsync(x => x.ConfigId == configId);
        if (count >= MaxQuestions)
            return false;

        await db.InsertAsync(new ChannelAccessQuestion
        {
            ConfigId = configId,
            Position = count,
            Question = question.Length > 45 ? question[..45] : question,
            Placeholder = placeholder,
            Required = required,
            Paragraph = paragraph,
            MinLength = 0,
            MaxLength = paragraph ? 1000 : 200,
            DateAdded = DateTime.UtcNow
        });

        return true;
    }

    /// <summary>
    ///     Removes a question by its one-based display position and closes the gap.
    /// </summary>
    /// <param name="configId">The gate to edit.</param>
    /// <param name="position">The one-based position shown to admins.</param>
    /// <returns>True if a question was removed.</returns>
    public async Task<bool> RemoveQuestionAsync(int configId, int position)
    {
        var questions = await GetQuestionsAsync(configId);
        if (position < 1 || position > questions.Count)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.ChannelAccessQuestions.Where(x => x.Id == questions[position - 1].Id).DeleteAsync();

        questions.RemoveAt(position - 1);
        for (var i = 0; i < questions.Count; i++)
        {
            var id = questions[i].Id;
            var index = i;
            await db.ChannelAccessQuestions.Where(x => x.Id == id)
                .Set(x => x.Position, index)
                .UpdateAsync();
        }

        return true;
    }

    #endregion

    #region Blacklist

    /// <summary>
    ///     Checks whether a user is barred from applying, either to this gate or guild wide.
    /// </summary>
    /// <param name="guildId">The guild to check in.</param>
    /// <param name="configId">The gate being applied to.</param>
    /// <param name="userId">The applicant.</param>
    /// <returns>True if the user is blacklisted.</returns>
    public async Task<bool> IsBlacklistedAsync(ulong guildId, int configId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ChannelAccessBlacklists
            .AnyAsync(x => x.GuildId == guildId && x.UserId == userId &&
                           (x.ConfigId == null || x.ConfigId == configId));
    }

    /// <summary>
    ///     Bars a user from applying. A null gate id bars them from every gate in the guild.
    /// </summary>
    /// <param name="guildId">The guild to bar them in.</param>
    /// <param name="configId">The gate, or null for all gates.</param>
    /// <param name="userId">The user to bar.</param>
    /// <param name="addedBy">The staff member adding the entry.</param>
    /// <param name="reason">Optional reason.</param>
    public async Task AddBlacklistAsync(ulong guildId, int? configId, ulong userId, ulong addedBy, string? reason)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var exists = await db.ChannelAccessBlacklists
            .AnyAsync(x => x.GuildId == guildId && x.UserId == userId && x.ConfigId == configId);
        if (exists)
            return;

        await db.InsertAsync(new ChannelAccessBlacklist
        {
            GuildId = guildId,
            ConfigId = configId,
            UserId = userId,
            AddedBy = addedBy,
            Reason = reason,
            DateAdded = DateTime.UtcNow
        });
    }

    /// <summary>
    ///     Lifts a blacklist entry.
    /// </summary>
    /// <param name="guildId">The guild the entry is in.</param>
    /// <param name="configId">The gate, or null for the guild wide entry.</param>
    /// <param name="userId">The user to unbar.</param>
    /// <returns>True if an entry was removed.</returns>
    public async Task<bool> RemoveBlacklistAsync(ulong guildId, int? configId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var removed = await db.ChannelAccessBlacklists
            .Where(x => x.GuildId == guildId && x.UserId == userId && x.ConfigId == configId)
            .DeleteAsync();
        return removed > 0;
    }

    /// <summary>
    ///     Gets every blacklist entry in a guild.
    /// </summary>
    /// <param name="guildId">The guild to read.</param>
    /// <returns>The blacklist entries.</returns>
    public async Task<List<ChannelAccessBlacklist>> GetBlacklistAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ChannelAccessBlacklists.Where(x => x.GuildId == guildId).ToListAsync();
    }

    #endregion

    #region Applications

    /// <summary>
    ///     Checks every requirement that would stop a user from opening an application.
    /// </summary>
    /// <param name="config">The gate being applied to.</param>
    /// <param name="user">The prospective applicant.</param>
    /// <returns>A tuple of whether they may apply and, if not, a human readable reason.</returns>
    public async Task<(bool CanApply, string? Reason)> CanApplyAsync(ChannelAccessConfig config, IGuildUser user)
    {
        if (!config.Enabled)
            return (false, "Applications for this channel are closed right now.");

        if (HasAccess(config, user))
            return (false, "You already have access to that channel.");

        if (await IsBlacklistedAsync(config.GuildId, config.Id, user.Id))
            return (false, "You are blocked from applying for this channel.");

        if (config.MinAccountAgeDays > 0 &&
            (DateTime.UtcNow - user.CreatedAt.UtcDateTime).TotalDays < config.MinAccountAgeDays)
            return (false, $"Your account must be at least {config.MinAccountAgeDays} days old to apply.");

        if (config.MinServerAgeDays > 0)
        {
            var joined = user.JoinedAt?.UtcDateTime;
            if (joined is null || (DateTime.UtcNow - joined.Value).TotalDays < config.MinServerAgeDays)
                return (false, $"You must have been in this server for {config.MinServerAgeDays} days to apply.");
        }

        await using var db = await dbFactory.CreateConnectionAsync();

        var pending = await db.ChannelAccessApplications
            .AnyAsync(x => x.ConfigId == config.Id && x.UserId == user.Id &&
                           x.Status == (int)AccessApplicationStatus.Pending);
        if (pending)
            return (false, "You already have an application open for this channel.");

        if (config.ReapplyCooldownHours > 0)
        {
            var cutoff = DateTime.UtcNow - TimeSpan.FromHours(config.ReapplyCooldownHours);
            var lastRejected = await db.ChannelAccessApplications
                .Where(x => x.ConfigId == config.Id && x.UserId == user.Id &&
                            (x.Status == (int)AccessApplicationStatus.Denied ||
                             x.Status == (int)AccessApplicationStatus.Expired) &&
                            x.ResolvedAt != null && x.ResolvedAt > cutoff)
                .OrderByDescending(x => x.ResolvedAt)
                .FirstOrDefaultAsync();

            if (lastRejected?.ResolvedAt is not null)
            {
                var retryAt = lastRejected.ResolvedAt.Value.AddHours(config.ReapplyCooldownHours);
                return (false,
                    $"You were turned down recently. You can apply again <t:{new DateTimeOffset(retryAt, TimeSpan.Zero).ToUnixTimeSeconds()}:R>.");
            }
        }

        return (true, null);
    }

    /// <summary>
    ///     Stores an application, posts it for voting and pings the voter role if one is set.
    /// </summary>
    /// <param name="config">The gate applied to.</param>
    /// <param name="user">The applicant.</param>
    /// <param name="answers">The applicant's answers, in question order.</param>
    /// <returns>The stored application, or null if the review channel could not be reached.</returns>
    public async Task<ChannelAccessApplication?> SubmitApplicationAsync(ChannelAccessConfig config, IGuildUser user,
        List<(int? QuestionId, string Question, string Answer)> answers)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var application = new ChannelAccessApplication
        {
            ConfigId = config.Id,
            GuildId = config.GuildId,
            UserId = user.Id,
            Status = (int)AccessApplicationStatus.Pending,
            ExpiresAt = config.VoteDurationHours > 0
                ? DateTime.UtcNow.AddHours(config.VoteDurationHours)
                : null,
            DateAdded = DateTime.UtcNow
        };

        application.Id = await db.InsertWithInt32IdentityAsync(application);

        for (var i = 0; i < answers.Count; i++)
        {
            await db.InsertAsync(new ChannelAccessAnswer
            {
                ApplicationId = application.Id,
                QuestionId = answers[i].QuestionId,
                Position = i,
                Question = answers[i].Question,
                Answer = answers[i].Answer
            });
        }

        var reviewChannelId = config.ReviewChannelId ?? config.ChannelId;
        if (client.GetGuild(config.GuildId)?.GetTextChannel(reviewChannelId) is not { } review)
        {
            logger.LogWarning("Channel access review channel {ChannelId} missing for gate {ConfigId}", reviewChannelId,
                config.Id);
            return application;
        }

        var embed = await BuildApplicationEmbedAsync(config, application, user);
        var components = BuildVoteComponents(config, application);
        var ping = config.PingRoleId is { } pingRole and not 0 ? $"<@&{pingRole}>" : null;

        var message = await review.SendMessageAsync(ping, embed: embed.Build(), components: components,
            allowedMentions: new AllowedMentions
            {
                RoleIds = config.PingRoleId is { } r and not 0 ? [r] : []
            });

        application.MessageChannelId = review.Id;
        application.MessageId = message.Id;
        await db.ChannelAccessApplications.Where(x => x.Id == application.Id)
            .Set(x => x.MessageChannelId, review.Id)
            .Set(x => x.MessageId, message.Id)
            .UpdateAsync();

        await LogAsync(config, $"📨 New access application **#{application.Id}** for <#{config.ChannelId}>.");
        return application;
    }

    /// <summary>
    ///     Gets an application by id.
    /// </summary>
    /// <param name="id">The application id.</param>
    /// <returns>The application, or null if it does not exist.</returns>
    public async Task<ChannelAccessApplication?> GetApplicationAsync(int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ChannelAccessApplications.FirstOrDefaultAsync(x => x.Id == id);
    }

    /// <summary>
    ///     Gets the answers attached to an application, in question order.
    /// </summary>
    /// <param name="applicationId">The application to read.</param>
    /// <returns>The ordered answers.</returns>
    public async Task<List<ChannelAccessAnswer>> GetAnswersAsync(int applicationId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ChannelAccessAnswers
            .Where(x => x.ApplicationId == applicationId)
            .OrderBy(x => x.Position)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets the open applications in a guild, optionally limited to one gate.
    /// </summary>
    /// <param name="guildId">The guild to read.</param>
    /// <param name="configId">An optional gate filter.</param>
    /// <returns>The pending applications, oldest first.</returns>
    public async Task<List<ChannelAccessApplication>> GetPendingApplicationsAsync(ulong guildId, int? configId = null)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ChannelAccessApplications
            .Where(x => x.GuildId == guildId && x.Status == (int)AccessApplicationStatus.Pending &&
                        (configId == null || x.ConfigId == configId))
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets applications in a guild, newest first, optionally filtered by gate and status.
    /// </summary>
    /// <param name="guildId">The guild to read.</param>
    /// <param name="configId">An optional gate filter.</param>
    /// <param name="status">An optional status filter.</param>
    /// <param name="limit">The maximum number of applications to return.</param>
    /// <returns>The matching applications.</returns>
    public async Task<List<ChannelAccessApplication>> GetApplicationsAsync(ulong guildId, int? configId = null,
        AccessApplicationStatus? status = null, int limit = 100)
    {
        var statusValue = status is null ? (int?)null : (int)status.Value;

        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ChannelAccessApplications
            .Where(x => x.GuildId == guildId &&
                        (configId == null || x.ConfigId == configId) &&
                        (statusValue == null || x.Status == statusValue))
            .OrderByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets a user's application history in a guild, newest first.
    /// </summary>
    /// <param name="guildId">The guild to read.</param>
    /// <param name="userId">The applicant.</param>
    /// <returns>The user's applications.</returns>
    public async Task<List<ChannelAccessApplication>> GetUserApplicationsAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ChannelAccessApplications
            .Where(x => x.GuildId == guildId && x.UserId == userId)
            .OrderByDescending(x => x.Id)
            .ToListAsync();
    }

    #endregion

    #region Voting

    /// <summary>
    ///     Gets the votes cast on an application.
    /// </summary>
    /// <param name="applicationId">The application to read.</param>
    /// <returns>The votes.</returns>
    public async Task<List<ChannelAccessVote>> GetVotesAsync(int applicationId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ChannelAccessVotes.Where(x => x.ApplicationId == applicationId).ToListAsync();
    }

    /// <summary>
    ///     Checks whether a member is allowed to vote on a gate's applications. Members qualify by
    ///     holding the voter role when one is set, otherwise by already having access to the channel.
    /// </summary>
    /// <param name="config">The gate being voted on.</param>
    /// <param name="user">The prospective voter.</param>
    /// <returns>True if the member may vote.</returns>
    public bool CanVote(ChannelAccessConfig config, IGuildUser user)
    {
        if (config.VoterRoleId is { } voterRole and not 0)
            return user.RoleIds.Contains(voterRole);

        return HasAccess(config, user) || user.GuildPermissions.ManageRoles;
    }

    /// <summary>
    ///     Checks whether a member can already get into the gated channel. Role gates look at the access
    ///     role; channel permission gates check what the member can actually see on the channel, which
    ///     covers both a personal overwrite and access inherited from any role.
    /// </summary>
    /// <param name="config">The gate to check against.</param>
    /// <param name="user">The member to check.</param>
    /// <returns>True if the member is already inside.</returns>
    public bool HasAccess(ChannelAccessConfig config, IGuildUser user)
    {
        if ((AccessGrantMode)config.GrantMode == AccessGrantMode.Role)
            return config.AccessRoleId is { } accessRole && user.RoleIds.Contains(accessRole);

        var channel = client.GetGuild(config.GuildId)?.GetChannel(config.ChannelId);
        if (channel is null)
            return false;

        if (channel.GetPermissionOverwrite(user) is { ViewChannel: PermValue.Allow })
            return true;

        return user is SocketGuildUser socketUser && socketUser.GetPermissions(channel).ViewChannel;
    }

    /// <summary>
    ///     Records, changes or clears a member's vote and resolves the application if a threshold is met.
    /// </summary>
    /// <param name="applicationId">The application being voted on.</param>
    /// <param name="user">The voter.</param>
    /// <param name="vote">1 to approve, -1 to deny, 0 to abstain.</param>
    /// <returns>What happened to the vote.</returns>
    public async Task<AccessVoteResult> CastVoteAsync(int applicationId, IGuildUser user, int vote)
    {
        await voteLock.WaitAsync();
        try
        {
            var application = await GetApplicationAsync(applicationId);
            if (application is null || application.Status != (int)AccessApplicationStatus.Pending)
                return AccessVoteResult.NotPending;

            var config = await GetConfigByIdAsync(application.ConfigId);
            if (config is null)
                return AccessVoteResult.NotPending;

            if (application.UserId == user.Id)
                return AccessVoteResult.OwnApplication;

            if (!CanVote(config, user))
                return AccessVoteResult.NotEligible;

            await using var db = await dbFactory.CreateConnectionAsync();
            var existing = await db.ChannelAccessVotes
                .FirstOrDefaultAsync(x => x.ApplicationId == applicationId && x.UserId == user.Id);

            AccessVoteResult result;
            if (existing is null)
            {
                await db.InsertAsync(new ChannelAccessVote
                {
                    ApplicationId = applicationId, UserId = user.Id, Vote = vote, DateAdded = DateTime.UtcNow
                });
                result = AccessVoteResult.Recorded;
            }
            else if (existing.Vote == vote)
            {
                await db.ChannelAccessVotes.Where(x => x.Id == existing.Id).DeleteAsync();
                result = AccessVoteResult.Removed;
            }
            else
            {
                await db.ChannelAccessVotes.Where(x => x.Id == existing.Id)
                    .Set(x => x.Vote, vote)
                    .Set(x => x.DateAdded, DateTime.UtcNow)
                    .UpdateAsync();
                result = AccessVoteResult.Changed;
            }

            var votes = await db.ChannelAccessVotes.Where(x => x.ApplicationId == applicationId).ToListAsync();
            var approvals = votes.Count(x => x.Vote == 1);
            var denials = votes.Count(x => x.Vote == -1);

            if (config.RequiredApprovals > 0 && approvals >= config.RequiredApprovals)
                await ResolveApplicationAsync(application, AccessApplicationStatus.Approved, null,
                    "Reached the approval threshold.");
            else if (config.RequiredDenials > 0 && denials >= config.RequiredDenials)
                await ResolveApplicationAsync(application, AccessApplicationStatus.Denied, null,
                    "Reached the denial threshold.");
            else
                await RefreshApplicationMessageAsync(application);

            return result;
        }
        finally
        {
            voteLock.Release();
        }
    }

    #endregion

    #region Resolution

    /// <summary>
    ///     Closes an application, granting the access role on approval and notifying the applicant.
    /// </summary>
    /// <param name="application">The application to close.</param>
    /// <param name="status">The outcome to record.</param>
    /// <param name="resolvedBy">The staff member who forced the outcome, if any.</param>
    /// <param name="reason">A short reason shown to the applicant.</param>
    /// <returns>True if the application was closed.</returns>
    public async Task<bool> ResolveApplicationAsync(ChannelAccessApplication application,
        AccessApplicationStatus status, ulong? resolvedBy, string? reason)
    {
        if (application.Status != (int)AccessApplicationStatus.Pending)
            return false;

        var config = await GetConfigByIdAsync(application.ConfigId);
        if (config is null)
            return false;

        await using (var db = await dbFactory.CreateConnectionAsync())
        {
            var updated = await db.ChannelAccessApplications
                .Where(x => x.Id == application.Id && x.Status == (int)AccessApplicationStatus.Pending)
                .Set(x => x.Status, (int)status)
                .Set(x => x.ResolvedAt, DateTime.UtcNow)
                .Set(x => x.ResolvedBy, resolvedBy)
                .Set(x => x.ResolutionReason, reason)
                .UpdateAsync();

            if (updated == 0)
                return false;
        }

        application.Status = (int)status;
        application.ResolvedAt = DateTime.UtcNow;
        application.ResolvedBy = resolvedBy;
        application.ResolutionReason = reason;

        var guild = client.GetGuild(config.GuildId);
        var member = guild?.GetUser(application.UserId);

        if (status == AccessApplicationStatus.Approved && member is not null)
            await GrantAccessAsync(config, member);

        await RefreshApplicationMessageAsync(application);

        if (config.DmOnDecision && member is not null &&
            status is AccessApplicationStatus.Approved or AccessApplicationStatus.Denied)
        {
            var text = status == AccessApplicationStatus.Approved
                ? $"✅ Your application for <#{config.ChannelId}> in **{guild!.Name}** was approved. Welcome in!"
                : $"❌ Your application for <#{config.ChannelId}> in **{guild!.Name}** was not accepted." +
                  (config.ReapplyCooldownHours > 0
                      ? $" You can apply again in {config.ReapplyCooldownHours} hours."
                      : string.Empty);

            try
            {
                await member.SendMessageAsync(text);
            }
            catch
            {
                // The applicant has DMs closed. Nothing to do.
            }
        }

        await LogAsync(config,
            $"{(status == AccessApplicationStatus.Approved ? "✅" : "❌")} Application **#{application.Id}** for " +
            $"<@{application.UserId}> was {status.ToString().ToLowerInvariant()}" +
            $"{(resolvedBy is not null ? $" by <@{resolvedBy}>" : " by vote")}.");

        return true;
    }

    /// <summary>
    ///     Lets an approved applicant into the channel, either by handing them the access role or by
    ///     writing them a personal permission overwrite on the channel itself.
    /// </summary>
    /// <param name="config">The gate that was applied to.</param>
    /// <param name="member">The approved applicant.</param>
    public async Task GrantAccessAsync(ChannelAccessConfig config, IGuildUser member)
    {
        try
        {
            if ((AccessGrantMode)config.GrantMode == AccessGrantMode.Role)
            {
                var role = client.GetGuild(config.GuildId)?.GetRole(config.AccessRoleId ?? 0);
                if (role is null)
                {
                    logger.LogWarning("Channel access gate {ConfigId} has no usable access role", config.Id);
                    return;
                }

                await member.AddRoleAsync(role);
                return;
            }

            if (client.GetGuild(config.GuildId)?.GetChannel(config.ChannelId) is not { } channel)
            {
                logger.LogWarning("Channel access gate {ConfigId} points at a missing channel", config.Id);
                return;
            }

            var existing = channel.GetPermissionOverwrite(member) ?? new OverwritePermissions();
            await channel.AddPermissionOverwriteAsync(member, existing.Modify(viewChannel: PermValue.Allow));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed granting channel access for gate {ConfigId} in guild {GuildId}", config.Id,
                config.GuildId);
        }
    }

    /// <summary>
    ///     Rewrites an application's message so the tally and buttons match the current state.
    /// </summary>
    /// <param name="application">The application whose message should be refreshed.</param>
    public async Task RefreshApplicationMessageAsync(ChannelAccessApplication application)
    {
        if (application.MessageChannelId is null || application.MessageId is null)
            return;

        var config = await GetConfigByIdAsync(application.ConfigId);
        if (config is null)
            return;

        var guild = client.GetGuild(application.GuildId);
        if (guild?.GetTextChannel(application.MessageChannelId.Value) is not { } channel)
            return;

        try
        {
            if (await channel.GetMessageAsync(application.MessageId.Value) is not IUserMessage message)
                return;

            var applicant = guild.GetUser(application.UserId) as IGuildUser;
            var embed = await BuildApplicationEmbedAsync(config, application, applicant);
            var components = application.Status == (int)AccessApplicationStatus.Pending
                ? BuildVoteComponents(config, application)
                : new ComponentBuilder().Build();

            await message.ModifyAsync(m =>
            {
                m.Embed = embed.Build();
                m.Components = components;
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed refreshing channel access application {ApplicationId}", application.Id);
        }
    }

    /// <summary>
    ///     Closes out applications whose voting window has run out, following each gate's expiry behaviour.
    /// </summary>
    public async Task ProcessExpiredApplicationsAsync()
    {
        try
        {
            List<ChannelAccessApplication> expired;
            await using (var db = await dbFactory.CreateConnectionAsync())
            {
                expired = await db.ChannelAccessApplications
                    .Where(x => x.Status == (int)AccessApplicationStatus.Pending && x.ExpiresAt != null &&
                                x.ExpiresAt <= DateTime.UtcNow)
                    .ToListAsync();
            }

            foreach (var application in expired)
            {
                var config = await GetConfigByIdAsync(application.ConfigId);
                if (config is null)
                    continue;

                if ((AccessExpiryBehavior)config.OnExpiry == AccessExpiryBehavior.StayOpen)
                    continue;

                var status = AccessApplicationStatus.Denied;
                if ((AccessExpiryBehavior)config.OnExpiry == AccessExpiryBehavior.Majority)
                {
                    var votes = await GetVotesAsync(application.Id);
                    status = votes.Count(x => x.Vote == 1) > votes.Count(x => x.Vote == -1)
                        ? AccessApplicationStatus.Approved
                        : AccessApplicationStatus.Denied;
                }

                await ResolveApplicationAsync(application, status, null, "The voting window closed.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed processing expired channel access applications");
        }
    }

    #endregion

    #region Rendering

    /// <summary>
    ///     Builds the review embed for an application, including the live vote tally.
    /// </summary>
    /// <param name="config">The gate the application belongs to.</param>
    /// <param name="application">The application to render.</param>
    /// <param name="applicant">The applicant, if they are still in the guild.</param>
    /// <returns>The embed builder for the application message.</returns>
    public async Task<EmbedBuilder> BuildApplicationEmbedAsync(ChannelAccessConfig config,
        ChannelAccessApplication application, IGuildUser? applicant)
    {
        var status = (AccessApplicationStatus)application.Status;
        var votes = await GetVotesAsync(application.Id);
        var approvals = votes.Count(x => x.Vote == 1);
        var denials = votes.Count(x => x.Vote == -1);
        var abstains = votes.Count(x => x.Vote == 0);

        var embed = new EmbedBuilder()
            .WithTitle(strings.ChannelAccessApplicationTitle(config.GuildId, application.Id))
            .WithColor(status switch
            {
                AccessApplicationStatus.Approved => Color.Green,
                AccessApplicationStatus.Denied => Color.Red,
                AccessApplicationStatus.Pending => Color.Blue,
                _ => Color.LightGrey
            })
            .AddField("Channel", $"<#{config.ChannelId}>", true)
            .AddField("Status", status.ToString(), true);

        if (config.AnonymousApplicant && status == AccessApplicationStatus.Pending)
        {
            embed.AddField("Applicant", "Hidden until the vote closes", true);
        }
        else
        {
            embed.AddField("Applicant", $"<@{application.UserId}>", true);
            if (applicant is not null)
            {
                embed.WithThumbnailUrl(applicant.GetAvatarUrl() ?? applicant.GetDefaultAvatarUrl());
                embed.AddField("Account created",
                    $"<t:{applicant.CreatedAt.ToUnixTimeSeconds()}:R>", true);
                if (applicant.JoinedAt is { } joined)
                    embed.AddField("Joined server", $"<t:{joined.ToUnixTimeSeconds()}:R>", true);
            }
        }

        foreach (var answer in await GetAnswersAsync(application.Id))
        {
            embed.AddField(answer.Question.Length > 256 ? answer.Question[..256] : answer.Question,
                answer.Answer.Length > 1024 ? answer.Answer[..1021] + "..." : answer.Answer);
        }

        var tally = $"✅ **{approvals}**/{config.RequiredApprovals} • ❌ **{denials}**/{config.RequiredDenials}";
        if (config.AllowAbstain)
            tally += $" • 🤷 **{abstains}**";
        embed.AddField("Votes", tally);

        if (status == AccessApplicationStatus.Pending && application.ExpiresAt is { } expires)
        {
            embed.WithFooter(strings.ChannelAccessVotingCloses(config.GuildId))
                .WithTimestamp(new DateTimeOffset(expires, TimeSpan.Zero));
        }
        else if (status != AccessApplicationStatus.Pending)
        {
            var closer = application.ResolvedBy is { } resolver ? $" by <@{resolver}>" : string.Empty;
            embed.AddField("Outcome",
                $"{status}{closer}" +
                (string.IsNullOrWhiteSpace(application.ResolutionReason)
                    ? string.Empty
                    : $"\n{application.ResolutionReason}"));
        }

        return embed;
    }

    /// <summary>
    ///     Builds the vote buttons for an open application.
    /// </summary>
    /// <param name="config">The gate the application belongs to.</param>
    /// <param name="application">The open application.</param>
    /// <returns>The built component set.</returns>
    public MessageComponent BuildVoteComponents(ChannelAccessConfig config, ChannelAccessApplication application)
    {
        var builder = new ComponentBuilder()
            .WithButton("Approve", $"chanacc:vote:{application.Id}:1", ButtonStyle.Success, new Emoji("✅"))
            .WithButton("Deny", $"chanacc:vote:{application.Id}:-1", ButtonStyle.Danger, new Emoji("❌"));

        if (config.AllowAbstain)
            builder.WithButton("Abstain", $"chanacc:vote:{application.Id}:0", ButtonStyle.Secondary, new Emoji("🤷"));

        builder.WithButton("Breakdown", $"chanacc:breakdown:{application.Id}", ButtonStyle.Secondary,
            new Emoji("📊"));

        return builder.Build();
    }

    /// <summary>
    ///     Posts the apply panel for a gate in a channel and remembers where it went.
    /// </summary>
    /// <param name="config">The gate the panel applies to.</param>
    /// <param name="targetChannelId">The channel to post the panel in.</param>
    /// <returns>The panel message id, or null if the target channel could not be reached.</returns>
    public async Task<ulong?> PostPanelAsync(ChannelAccessConfig config, ulong targetChannelId)
    {
        var guild = client.GetGuild(config.GuildId);
        if (guild?.GetTextChannel(targetChannelId) is not { } target)
            return null;

        var gatedChannel = guild.GetTextChannel(config.ChannelId);
        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(strings.ChannelAccessPanelTitle(config.GuildId, gatedChannel?.Name ?? "channel"))
            .WithDescription(strings.ChannelAccessPanelBody(config.GuildId))
            .Build();

        var message = await target.SendMessageAsync(embed: embed, components: BuildPanelComponents(config));

        config.PanelChannelId = target.Id;
        config.PanelMessageId = message.Id;
        await UpdateConfigAsync(config);

        return message.Id;
    }

    /// <summary>
    ///     Builds the apply panel that admins post in a public channel.
    /// </summary>
    /// <param name="config">The gate the panel applies to.</param>
    /// <returns>The button row for the panel message.</returns>
    public MessageComponent BuildPanelComponents(ChannelAccessConfig config)
    {
        return new ComponentBuilder()
            .WithButton("Apply for access", $"chanacc:apply:{config.Id}", ButtonStyle.Primary, new Emoji("📝"))
            .Build();
    }

    #endregion
}