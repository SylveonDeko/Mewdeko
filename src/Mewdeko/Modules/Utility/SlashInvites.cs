using System.IO;
using System.Text;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

/// <summary>
///     Provides slash commands for managing and viewing invite-related information.
/// </summary>
[Group("invites", "Invite tracking, leaderboards and analytics")]
public class SlashInvites(InteractiveService interactivity) : MewdekoSlashModuleBase<InviteCountService>
{
    /// <summary>
    ///     Displays a user's invite breakdown and rank.
    /// </summary>
    /// <param name="user">The user to check invites for. If null, checks for the command user.</param>
    [SlashCommand("show", "Shows how many invites a user has, with a breakdown")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Invites(IUser? user = null)
    {
        user ??= ctx.User;
        var breakdown = await Service.GetInviteBreakdownAsync(ctx.Guild.Id, user.Id);
        var rank = await Service.GetRankAsync(ctx.Guild.Id, user.Id);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithAuthor(user.ToString(), user.RealAvatarUrl().ToString())
            .WithTitle(Strings.UserInviteCount(ctx.Guild.Id, user.Username, breakdown.Count))
            .WithDescription(Strings.InviteBreakdown(ctx.Guild.Id, breakdown.Regular, breakdown.Left,
                breakdown.Fake, breakdown.Bonus))
            .WithFooter(rank.HasValue
                ? Strings.InviteRankFooter(ctx.Guild.Id, rank.Value)
                : Strings.InviteRankNone(ctx.Guild.Id));

        await RespondAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Displays who invited a specific user to the guild, and how.
    /// </summary>
    /// <param name="user">The user to check. If null, checks for the command user.</param>
    [SlashCommand("who-invited", "Shows who invited a user and how they joined")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task WhoInvited(IUser? user = null)
    {
        user ??= ctx.User;
        var record = await Service.GetJoinRecordAsync(ctx.Guild.Id, user.Id);
        var inviter = await Service.GetInviter(user.Id, ctx.Guild);

        if (record == null || inviter == null && record.JoinType == (int)InviteJoinType.Unknown)
        {
            await ReplyErrorAsync(Strings.NoInviterFound(ctx.Guild.Id, user.Username));
            return;
        }

        var joinType = (InviteJoinType)record.JoinType;
        var description = joinType switch
        {
            InviteJoinType.Vanity => Strings.InviterVanity(ctx.Guild.Id, user.Username),
            InviteJoinType.Bot => Strings.InviterBot(ctx.Guild.Id, user.Username),
            _ when inviter != null => Strings.InviterFound(ctx.Guild.Id, user.Username, inviter.Username),
            _ => Strings.NoInviterFound(ctx.Guild.Id, user.Username)
        };

        var eb = new EmbedBuilder().WithOkColor().WithDescription(description);
        if (record.InviteCode != null && joinType == InviteJoinType.Invite)
            eb.AddField(Strings.InviteCodeField(ctx.Guild.Id), $"`{record.InviteCode}`", true);
        if (record.IsFake)
            eb.AddField(Strings.InviteFlaggedField(ctx.Guild.Id), ((InviteFakeReason)record.FakeReason).ToString(),
                true);
        if (record.DateAdded.HasValue)
            eb.AddField(Strings.InviteJoinedField(ctx.Guild.Id),
                TimestampTag.FromDateTime(record.DateAdded.Value, TimestampTagStyles.Relative).ToString(), true);

        await RespondAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Displays the members invited by a user, or who joined through a code or label.
    /// </summary>
    /// <param name="user">The inviter.</param>
    /// <param name="code">An invite code or URL.</param>
    /// <param name="label">An invite label.</param>
    /// <param name="includeLeft">Whether to include members who have since left.</param>
    [SlashCommand("invited-list", "Lists members invited by a user, code or label")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task InvitedList(IUser? user = null, string? code = null, string? label = null,
        bool includeLeft = true)
    {
        await DeferAsync();
        if (user == null && code == null && label == null)
            user = ctx.User;

        var records = await Service.GetInvitedRecordsAsync(ctx.Guild.Id, user?.Id, code, label, includeLeft);
        if (records.Count == 0)
        {
            await ReplyErrorAsync(Strings.NoInvitedUsersFound(ctx.Guild.Id));
            return;
        }

        var title = user != null
            ? Strings.InvitedUsersTitle(ctx.Guild.Id, user.Username)
            : code != null
                ? Strings.InvitedByCodeTitle(ctx.Guild.Id, InviteCountService.NormalizeCode(code))
                : Strings.InvitedByLabelTitle(ctx.Guild.Id, label!);

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex((records.Count - 1) / 15)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(60), InteractionResponseType.DeferredChannelMessageWithSource)
            .ConfigureAwait(false);
        return;

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithOkColor()
                .WithTitle(title)
                .WithDescription(string.Join("\n", records.Skip(page * 15).Take(15).Select(record =>
                {
                    var when = record.DateAdded.HasValue
                        ? TimestampTag.FromDateTime(record.DateAdded.Value, TimestampTagStyles.ShortDate).ToString()
                        : "?";
                    var flags = new List<string>();
                    if (record.IsFake) flags.Add($"fake: {(InviteFakeReason)record.FakeReason}");
                    if (record.LeftAt.HasValue) flags.Add("left");
                    return $"<@{record.UserId}> {when}" + (flags.Count > 0 ? $" ({string.Join(", ", flags)})" : "");
                })));
        }
    }

    /// <summary>
    ///     Displays a leaderboard of users with the most invites.
    /// </summary>
    /// <param name="range">The time window.</param>
    /// <param name="role">Only include inviters with this role.</param>
    [SlashCommand("leaderboard", "Shows the users with the most invites")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task InviteLeaderboard(StatsRange range = StatsRange.AllTime, IRole? role = null)
    {
        await DeferAsync();
        var leaderboard = await Service.GetInviteLeaderboardAsync(ctx.Guild, range, role?.Id, 200);

        if (leaderboard.Count == 0)
        {
            await ReplyErrorAsync(Strings.NoInviteData(ctx.Guild.Id));
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex((leaderboard.Count - 1) / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(60), InteractionResponseType.DeferredChannelMessageWithSource)
            .ConfigureAwait(false);
        return;

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithOkColor()
                .WithTitle(Strings.InviteLeaderboardTitle(ctx.Guild.Id) + $" ({range.DisplayName()})")
                .WithDescription(string.Join("\n", leaderboard.Skip(page * 10).Take(10)
                    .Select((x, i) => Strings.InviteLeaderboardEntry(ctx.Guild.Id, page * 10 + i + 1, x.Username,
                        x.Total, x.Regular, x.Left, x.Fake, x.Bonus))));
        }
    }

    /// <summary>
    ///     Displays growth analytics: joins, leaves, retention, fake joins, join sources and top codes.
    /// </summary>
    /// <param name="range">The time window.</param>
    [SlashCommand("stats", "Shows growth analytics: joins, leaves, retention and join sources")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task InviteStats(StatsRange range = StatsRange.Monthly)
    {
        await DeferAsync();
        var stats = await Service.GetAnalyticsAsync(ctx.Guild, range);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.InviteStatsTitle(ctx.Guild.Id, stats.Range.DisplayName()))
            .AddField(Strings.InviteStatsJoins(ctx.Guild.Id), stats.Joins.ToString("N0"), true)
            .AddField(Strings.InviteStatsLeaves(ctx.Guild.Id), stats.Leaves.ToString("N0"), true)
            .AddField(Strings.InviteStatsNet(ctx.Guild.Id), stats.NetGrowth.ToString("+#,0;-#,0;0"), true)
            .AddField(Strings.InviteStatsRetention(ctx.Guild.Id),
                stats.Retention.HasValue ? $"{stats.Retention.Value:P0}" : "-", true)
            .AddField(Strings.InviteStatsFake(ctx.Guild.Id), stats.FakeJoins.ToString("N0"), true)
            .AddField(Strings.InviteStatsSources(ctx.Guild.Id),
                Strings.InviteStatsSourcesValue(ctx.Guild.Id, stats.ViaInvite, stats.ViaVanity, stats.ViaBot,
                    stats.Unknown), true);

        if (stats.TopInviters.Count > 0)
        {
            eb.AddField(Strings.InviteStatsTopInviters(ctx.Guild.Id), string.Join("\n",
                stats.TopInviters.Select((x, i) => $"{i + 1}. {x.Username}: **{x.Total}**")));
        }

        if (stats.TopCodes.Count > 0)
        {
            eb.AddField(Strings.InviteStatsTopCodes(ctx.Guild.Id), string.Join("\n",
                stats.TopCodes.Take(5).Select(x =>
                    $"`{x.Code}`{(x.Label != null ? $" ({x.Label})" : "")}: **{x.Joins}**")));
        }

        await FollowupAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Lists the invite codes a user has created.
    /// </summary>
    /// <param name="user">The user. If null, uses the command user.</param>
    [SlashCommand("codes", "Lists the invite codes a user has created")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task InviteCodes(IUser? user = null)
    {
        await DeferAsync(true);
        user ??= ctx.User;
        var codes = await Service.GetUserInviteCodesAsync(ctx.Guild, user.Id);
        if (codes.Count == 0)
        {
            await ReplyErrorAsync(Strings.InviteCodesNone(ctx.Guild.Id, user.Username));
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.InviteCodesTitle(ctx.Guild.Id, user.Username))
            .WithDescription(string.Join("\n", codes.OrderByDescending(x => x.Uses ?? 0).Take(25).Select(x =>
                $"`{x.Code}` <#{x.ChannelId}> uses: **{x.Uses ?? 0}**" +
                (x.MaxUses is > 0 ? $"/{x.MaxUses}" : "") +
                (x.MaxAge is > 0 ? " (expires)" : ""))));

        await FollowupAsync(embed: eb.Build(), ephemeral: true);
    }

    /// <summary>
    ///     Shows your personal invite link, creating a permanent one when you have none.
    /// </summary>
    [SlashCommand("link", "Shows your personal invite link, creating one if needed")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.CreateInstantInvite)]
    [CheckPermissions]
    public async Task InviteLink()
    {
        await DeferAsync(true);
        var invite = await Service.GetOrCreateUserLinkAsync(ctx.Guild, (IGuildUser)ctx.User);
        if (invite == null)
        {
            await ReplyErrorAsync(Strings.InviteLinkFailed(ctx.Guild.Id));
            return;
        }

        await FollowupAsync(embed: new EmbedBuilder().WithOkColor()
                .WithDescription(Strings.InviteLinkYours(ctx.Guild.Id, invite.Url, invite.Uses ?? 0)).Build(),
            ephemeral: true);
    }

    /// <summary>
    ///     Invite tracking settings.
    /// </summary>
    [Group("settings", "Configure invite tracking")]
    public class InviteSettingsCommands : MewdekoSlashSubmodule<InviteCountService>
    {
        /// <summary>
        ///     Displays the current invite settings for the guild.
        /// </summary>
        [SlashCommand("show", "Shows the invite tracking settings")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task Show()
        {
            var settings = await Service.GetInviteCountSettingsAsync(ctx.Guild.Id);
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.InviteSettingsTitle(ctx.Guild.Id))
                .AddField(Strings.InviteSettingTracking(ctx.Guild.Id), EnDis(settings.IsEnabled), true)
                .AddField(Strings.InviteSettingRemoveOnLeave(ctx.Guild.Id), EnDis(settings.RemoveInviteOnLeave),
                    true)
                .AddField(Strings.InviteSettingMinAge(ctx.Guild.Id),
                    settings.MinAccountAge == TimeSpan.Zero ? "Off" : $"{settings.MinAccountAge.TotalDays:0.#}d", true)
                .AddField(Strings.InviteSettingRejoins(ctx.Guild.Id), EnDis(settings.CountRejoins), true)
                .AddField(Strings.InviteSettingNoAvatar(ctx.Guild.Id), EnDis(settings.FakeOnNoAvatar), true)
                .AddField(Strings.InviteSettingLinkChannel(ctx.Guild.Id),
                    settings.LinkChannelId.HasValue ? MentionUtils.MentionChannel(settings.LinkChannelId.Value) : "-",
                    true)
                .AddField(Strings.InviteSettingLogChannel(ctx.Guild.Id),
                    settings.LogChannelId.HasValue ? MentionUtils.MentionChannel(settings.LogChannelId.Value) : "-",
                    true);

            await RespondAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Enables or disables invite tracking.
        /// </summary>
        /// <param name="enabled">The new state.</param>
        [SlashCommand("tracking", "Enables or disables invite tracking")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task Tracking(bool enabled)
        {
            await Service.SetInviteTrackingEnabledAsync(ctx.Guild.Id, enabled);
            await ReplyConfirmAsync(enabled
                ? Strings.InviteTrackingEnabled(ctx.Guild.Id)
                : Strings.InviteTrackingDisabled(ctx.Guild.Id));
        }

        /// <summary>
        ///     Sets whether invites are removed when the invited member leaves.
        /// </summary>
        /// <param name="enabled">The new state.</param>
        [SlashCommand("remove-on-leave", "Whether inviters lose credit when their members leave")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task RemoveOnLeave(bool enabled)
        {
            await Service.SetRemoveInviteOnLeaveAsync(ctx.Guild.Id, enabled);
            await ReplyConfirmAsync(enabled
                ? Strings.RemoveInviteOnLeaveEnabled(ctx.Guild.Id)
                : Strings.RemoveInviteOnLeaveDisabled(ctx.Guild.Id));
        }

        /// <summary>
        ///     Sets whether rejoining members count as regular or fake invites.
        /// </summary>
        /// <param name="countRejoins">True to count rejoins, false to flag them as fake.</param>
        [SlashCommand("count-rejoins", "Whether rejoining members earn a regular invite or are flagged as fake")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task CountRejoins(bool countRejoins)
        {
            await Service.SetCountRejoinsAsync(ctx.Guild.Id, countRejoins);
            await ReplyConfirmAsync(countRejoins
                ? Strings.InviteRejoinsCounted(ctx.Guild.Id)
                : Strings.InviteRejoinsFake(ctx.Guild.Id));
        }

        /// <summary>
        ///     Sets whether members without an avatar are flagged as fake joins.
        /// </summary>
        /// <param name="enabled">The new state.</param>
        [SlashCommand("fake-no-avatar", "Whether members without an avatar are flagged as fake")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task FakeNoAvatar(bool enabled)
        {
            await Service.SetFakeOnNoAvatarAsync(ctx.Guild.Id, enabled);
            await ReplyConfirmAsync(enabled
                ? Strings.InviteNoAvatarFakeOn(ctx.Guild.Id)
                : Strings.InviteNoAvatarFakeOff(ctx.Guild.Id));
        }

        /// <summary>
        ///     Sets the minimum account age required for an invite to be counted.
        /// </summary>
        /// <param name="days">The minimum age in days. Zero disables the check.</param>
        [SlashCommand("min-account-age", "Accounts younger than this many days are flagged as fake (0 to disable)")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task MinAccountAge([MinValue(0)] [MaxValue(300)] int days)
        {
            await Service.SetMinAccountAgeAsync(ctx.Guild.Id, TimeSpan.FromDays(days));
            await ReplyConfirmAsync(Strings.MinAccountAgeSet(ctx.Guild.Id, days));
        }

        /// <summary>
        ///     Sets the channel that personal invite links are created for.
        /// </summary>
        /// <param name="channel">The channel, or null to use the system channel.</param>
        [SlashCommand("link-channel", "The channel personal invite links point at")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task LinkChannel(ITextChannel? channel = null)
        {
            await Service.SetLinkChannelAsync(ctx.Guild.Id, channel?.Id);
            await ReplyConfirmAsync(channel == null
                ? Strings.InviteLinkChannelCleared(ctx.Guild.Id)
                : Strings.InviteLinkChannelSet(ctx.Guild.Id, channel.Mention));
        }

        /// <summary>
        ///     Sets the channel that receives join and leave attribution embeds.
        /// </summary>
        /// <param name="channel">The channel, or null to disable.</param>
        [SlashCommand("log-channel", "Channel for join and leave attribution logs")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task LogChannel(ITextChannel? channel = null)
        {
            await Service.SetLogChannelAsync(ctx.Guild.Id, channel?.Id);
            await ReplyConfirmAsync(channel == null
                ? Strings.InviteLogChannelCleared(ctx.Guild.Id)
                : Strings.InviteLogChannelSet(ctx.Guild.Id, channel.Mention));
        }

        private static string EnDis(bool endis)
        {
            return endis ? "Enabled" : "Disabled";
        }
    }

    /// <summary>
    ///     Manual adjustments to invite tallies.
    /// </summary>
    [Group("manage", "Add, remove, reset and sync invites")]
    public class InviteManageCommands : MewdekoSlashSubmodule<InviteCountService>
    {
        /// <summary>
        ///     What a CSV export contains.
        /// </summary>
        public enum InviteExportKind
        {
            /// <summary>
            ///     The inviter leaderboard.
            /// </summary>
            Leaderboard,

            /// <summary>
            ///     Witnessed joins, optionally filtered to one inviter.
            /// </summary>
            InvitedList
        }

        /// <summary>
        ///     Adds regular invites to a user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="amount">How many to add.</param>
        [SlashCommand("add", "Adds regular invites to a user")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task Add(IGuildUser user, [MinValue(1)] int amount)
        {
            var row = await Service.AdjustInvitesAsync(ctx.Guild.Id, user.Id, amount);
            await ReplyConfirmAsync(Strings.InvitesAdded(ctx.Guild.Id, amount, user.Mention, row.Count));
        }

        /// <summary>
        ///     Removes regular invites from a user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="amount">How many to remove.</param>
        [SlashCommand("remove", "Removes regular invites from a user")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task Remove(IGuildUser user, [MinValue(1)] int amount)
        {
            var row = await Service.AdjustInvitesAsync(ctx.Guild.Id, user.Id, -amount);
            await ReplyConfirmAsync(Strings.InvitesRemoved(ctx.Guild.Id, amount, user.Mention, row.Count));
        }

        /// <summary>
        ///     Grants bonus invites to a user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="amount">How many to grant.</param>
        [SlashCommand("add-bonus", "Grants bonus invites to a user")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task AddBonus(IGuildUser user, [MinValue(1)] int amount)
        {
            var row = await Service.AdjustInvitesAsync(ctx.Guild.Id, user.Id, bonus: amount);
            await ReplyConfirmAsync(Strings.BonusInvitesAdded(ctx.Guild.Id, amount, user.Mention, row.Count));
        }

        /// <summary>
        ///     Takes bonus invites away from a user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="amount">How many to remove.</param>
        [SlashCommand("remove-bonus", "Takes bonus invites away from a user")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task RemoveBonus(IGuildUser user, [MinValue(1)] int amount)
        {
            var row = await Service.AdjustInvitesAsync(ctx.Guild.Id, user.Id, bonus: -amount);
            await ReplyConfirmAsync(Strings.BonusInvitesRemoved(ctx.Guild.Id, amount, user.Mention, row.Count));
        }

        /// <summary>
        ///     Marks some of a user's invites as fake.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="amount">How many to flag.</param>
        [SlashCommand("add-fake", "Marks some of a user's invites as fake")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task AddFake(IGuildUser user, [MinValue(1)] int amount)
        {
            var row = await Service.AdjustInvitesAsync(ctx.Guild.Id, user.Id, fake: amount);
            await ReplyConfirmAsync(Strings.FakeInvitesAdded(ctx.Guild.Id, amount, user.Mention, row.Count));
        }

        /// <summary>
        ///     Clears fake invites from a user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="amount">How many to clear.</param>
        [SlashCommand("remove-fake", "Clears fake invites from a user")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task RemoveFake(IGuildUser user, [MinValue(1)] int amount)
        {
            var row = await Service.AdjustInvitesAsync(ctx.Guild.Id, user.Id, fake: -amount);
            await ReplyConfirmAsync(Strings.FakeInvitesRemoved(ctx.Guild.Id, amount, user.Mention, row.Count));
        }

        /// <summary>
        ///     Resets a user's invites.
        /// </summary>
        /// <param name="user">The user.</param>
        [SlashCommand("reset-user", "Resets a user's invites to zero")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task ResetUser(IGuildUser user)
        {
            if (!await PromptUserConfirmAsync(Strings.InviteResetUserConfirm(ctx.Guild.Id, user.Mention), ctx.User.Id))
                return;

            await Service.ResetInvitesAsync(ctx.Guild.Id, user.Id);
            await ReplyConfirmAsync(Strings.InviteResetUserDone(ctx.Guild.Id, user.Mention));
        }

        /// <summary>
        ///     Resets invites for the whole server, or only for inviters who have left.
        /// </summary>
        /// <param name="scope">What to reset.</param>
        [SlashCommand("reset", "Resets invites for the whole server or for members who left")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task Reset(InviteResetScope scope)
        {
            var confirm = scope == InviteResetScope.Server
                ? Strings.InviteResetServerConfirm(ctx.Guild.Id)
                : Strings.InviteResetLeftConfirm(ctx.Guild.Id);
            if (!await PromptUserConfirmAsync(confirm, ctx.User.Id))
                return;

            var count = await Service.ResetInvitesAsync(ctx.Guild, scope);
            await ReplyConfirmAsync(Strings.InviteResetScopeDone(ctx.Guild.Id, count));
        }

        /// <summary>
        ///     Imports invite use counts from Discord, raising inviters' regular totals to match.
        /// </summary>
        /// <param name="user">Only sync this user, or null for everyone.</param>
        [SlashCommand("sync", "Imports invite use counts from Discord")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [RequireBotPermission(GuildPermission.ManageGuild)]
        public async Task Sync(IGuildUser? user = null)
        {
            await DeferAsync();
            var raised = await Service.SyncInvitesAsync(ctx.Guild, user?.Id);
            await FollowupAsync(embed: new EmbedBuilder().WithOkColor()
                .WithDescription(Strings.InvitesSynced(ctx.Guild.Id, raised)).Build());
        }

        /// <summary>
        ///     Deletes an invite code from the server.
        /// </summary>
        /// <param name="code">The code or URL.</param>
        [SlashCommand("delete-code", "Deletes an invite code from the server")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [RequireBotPermission(GuildPermission.ManageGuild)]
        public async Task DeleteCode(string code)
        {
            var deleted = await Service.DeleteInviteAsync(ctx.Guild, code);
            await (deleted
                ? ReplyConfirmAsync(Strings.InviteDeleted(ctx.Guild.Id, InviteCountService.NormalizeCode(code)))
                : ReplyErrorAsync(Strings.InviteNotFound(ctx.Guild.Id)));
        }

        /// <summary>
        ///     Deletes unlabelled invite codes matching filters.
        /// </summary>
        /// <param name="maxUses">Codes used more than this are kept.</param>
        /// <param name="includeUser">Only codes created by this user.</param>
        /// <param name="excludeUser">Never delete codes created by this user.</param>
        /// <param name="includeChannel">Only codes pointing at this channel.</param>
        /// <param name="excludeChannel">Never delete codes pointing at this channel.</param>
        [SlashCommand("purge-codes", "Deletes unlabelled invite codes matching filters")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [RequireBotPermission(GuildPermission.ManageGuild)]
        public async Task PurgeCodes([MinValue(0)] int maxUses = 0, IUser? includeUser = null,
            IUser? excludeUser = null, IGuildChannel? includeChannel = null, IGuildChannel? excludeChannel = null)
        {
            if (!await PromptUserConfirmAsync(Strings.InvitePurgeConfirm(ctx.Guild.Id, maxUses), ctx.User.Id))
                return;

            var deleted = await Service.PurgeInviteCodesAsync(ctx.Guild, maxUses,
                includeUser == null ? [] : [includeUser.Id],
                excludeUser == null ? [] : [excludeUser.Id],
                includeChannel == null ? [] : [includeChannel.Id],
                excludeChannel == null ? [] : [excludeChannel.Id]);
            await ReplyConfirmAsync(Strings.InvitesPurged(ctx.Guild.Id, deleted));
        }

        /// <summary>
        ///     Bans every member a user invited or who joined through a code.
        /// </summary>
        /// <param name="inviter">The inviter.</param>
        /// <param name="code">The invite code.</param>
        /// <param name="reason">The ban reason.</param>
        [SlashCommand("massban", "Bans every member invited by a user or through a code")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.BanMembers)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        public async Task MassBan(IUser? inviter = null, string? code = null, string? reason = null)
        {
            if (inviter == null && code == null)
            {
                await ReplyErrorAsync(Strings.InviteMassBanTarget(ctx.Guild.Id));
                return;
            }

            var records = await Service.GetInvitedRecordsAsync(ctx.Guild.Id, inviter?.Id, code, includeLeft: false);
            if (records.Count == 0)
            {
                await ReplyErrorAsync(Strings.NoInvitedUsersFound(ctx.Guild.Id));
                return;
            }

            var target = inviter?.Mention ?? $"`{InviteCountService.NormalizeCode(code!)}`";
            if (!await PromptUserConfirmAsync(Strings.InviteMassBanConfirm(ctx.Guild.Id, records.Count, target),
                    ctx.User.Id))
                return;

            var (banned, failed) = await Service.MassBanAsync(ctx.Guild, inviter?.Id, code,
                reason ?? Strings.InviteMassBanReason(ctx.Guild.Id, ctx.User.ToString()));
            await ReplyConfirmAsync(Strings.InviteMassBanDone(ctx.Guild.Id, banned, failed));
        }

        /// <summary>
        ///     Exports the leaderboard or a list of invited members as CSV.
        /// </summary>
        /// <param name="what">What to export.</param>
        /// <param name="range">The leaderboard window.</param>
        /// <param name="user">The inviter for an invited list export.</param>
        [SlashCommand("export", "Exports the leaderboard or invited members as a CSV file")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [InteractionRatelimit(300)]
        public async Task Export(InviteExportKind what, StatsRange range = StatsRange.AllTime, IUser? user = null)
        {
            await DeferAsync();
            var csv = what == InviteExportKind.Leaderboard
                ? await Service.ExportLeaderboardCsvAsync(ctx.Guild, range)
                : await Service.ExportInvitedListCsvAsync(ctx.Guild, user?.Id, null, null);

            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            await FollowupWithFileAsync(stream, $"{what}-{range}.csv".ToLowerInvariant(),
                Strings.InviteExportDone(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Inviter blacklists and hidden leaderboard members.
    /// </summary>
    [Group("exclusions", "Blacklist inviters or hide members from the leaderboard")]
    public class InviteExclusionCommands : MewdekoSlashSubmodule<InviteCountService>
    {
        /// <summary>
        ///     Toggles whether a user can earn invite credit.
        /// </summary>
        /// <param name="user">The user.</param>
        [SlashCommand("blacklist-user", "Toggles whether a user can earn invite credit")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task BlacklistUser(IGuildUser user)
        {
            await ToggleAsync(user.Id, InviteExclusionKind.BlacklistedUser, user.Mention);
        }

        /// <summary>
        ///     Toggles whether holders of a role can earn invite credit.
        /// </summary>
        /// <param name="role">The role.</param>
        [SlashCommand("blacklist-role", "Toggles whether holders of a role can earn invite credit")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task BlacklistRole(IRole role)
        {
            await ToggleAsync(role.Id, InviteExclusionKind.BlacklistedRole, role.Mention);
        }

        /// <summary>
        ///     Toggles whether a user is hidden from invite leaderboards.
        /// </summary>
        /// <param name="user">The user.</param>
        [SlashCommand("hide-user", "Toggles whether a user is hidden from the leaderboard")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task HideUser(IGuildUser user)
        {
            await ToggleAsync(user.Id, InviteExclusionKind.HiddenUser, user.Mention);
        }

        /// <summary>
        ///     Lists blacklisted inviters, blacklisted roles and hidden users.
        /// </summary>
        [SlashCommand("list", "Lists blacklisted inviters, roles and hidden users")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task List()
        {
            var users = await Service.GetExclusionsAsync(ctx.Guild.Id, InviteExclusionKind.BlacklistedUser);
            var roles = await Service.GetExclusionsAsync(ctx.Guild.Id, InviteExclusionKind.BlacklistedRole);
            var hidden = await Service.GetExclusionsAsync(ctx.Guild.Id, InviteExclusionKind.HiddenUser);

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.InviteExclusionsTitle(ctx.Guild.Id))
                .AddField(Strings.InviteExclusionsUsers(ctx.Guild.Id),
                    users.Count == 0 ? "-" : string.Join(", ", users.Select(x => $"<@{x}>")))
                .AddField(Strings.InviteExclusionsRoles(ctx.Guild.Id),
                    roles.Count == 0 ? "-" : string.Join(", ", roles.Select(x => $"<@&{x}>")))
                .AddField(Strings.InviteExclusionsHidden(ctx.Guild.Id),
                    hidden.Count == 0 ? "-" : string.Join(", ", hidden.Select(x => $"<@{x}>")));

            await RespondAsync(embed: eb.Build());
        }

        private async Task ToggleAsync(ulong targetId, InviteExclusionKind kind, string mention)
        {
            if (await Service.AddExclusionAsync(ctx.Guild.Id, targetId, kind))
            {
                await ReplyConfirmAsync(kind == InviteExclusionKind.HiddenUser
                    ? Strings.InviteHidden(ctx.Guild.Id, mention)
                    : Strings.InviteBlacklisted(ctx.Guild.Id, mention));
                return;
            }

            await Service.RemoveExclusionAsync(ctx.Guild.Id, targetId, kind);
            await ReplyConfirmAsync(kind == InviteExclusionKind.HiddenUser
                ? Strings.InviteUnhidden(ctx.Guild.Id, mention)
                : Strings.InviteUnblacklisted(ctx.Guild.Id, mention));
        }
    }

    /// <summary>
    ///     Labelled invite codes.
    /// </summary>
    [Group("labels", "Name invite codes and grant roles to members who use them")]
    public class InviteLabelCommands : MewdekoSlashSubmodule<InviteCountService>
    {
        /// <summary>
        ///     Labels an invite code so it shows up by name in stats and placeholders.
        /// </summary>
        /// <param name="code">The code or URL.</param>
        /// <param name="label">The label text.</param>
        /// <param name="role">A role granted to members who join through the code.</param>
        [SlashCommand("set", "Labels an invite code, optionally granting a role on join")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task Set(string code, [MaxLength(64)] string label, IRole? role = null)
        {
            var saved = await Service.SetLabelAsync(ctx.Guild.Id, code, label, role?.Id);
            await ReplyConfirmAsync(Strings.InviteLabelSet(ctx.Guild.Id, saved.InviteCode, saved.Label));
        }

        /// <summary>
        ///     Sets or clears the role granted to members who join through a labelled code.
        /// </summary>
        /// <param name="code">The code or URL.</param>
        /// <param name="role">The role, or null to clear it.</param>
        [SlashCommand("role", "Sets or clears the role granted on join through a labelled code")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task Role(string code, IRole? role = null)
        {
            if (await Service.GetLabelAsync(ctx.Guild.Id, code) == null)
            {
                await ReplyErrorAsync(Strings.InviteLabelMissing(ctx.Guild.Id));
                return;
            }

            await Service.SetLabelRoleAsync(ctx.Guild.Id, code, role?.Id);
            await ReplyConfirmAsync(role == null
                ? Strings.InviteLabelRoleCleared(ctx.Guild.Id, InviteCountService.NormalizeCode(code))
                : Strings.InviteLabelRoleSet(ctx.Guild.Id, InviteCountService.NormalizeCode(code), role.Mention));
        }

        /// <summary>
        ///     Removes the label from an invite code.
        /// </summary>
        /// <param name="code">The code or URL.</param>
        [SlashCommand("remove", "Removes the label from an invite code")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task Remove(string code)
        {
            var removed = await Service.RemoveLabelAsync(ctx.Guild.Id, code);
            await (removed
                ? ReplyConfirmAsync(Strings.InviteLabelRemoved(ctx.Guild.Id, InviteCountService.NormalizeCode(code)))
                : ReplyErrorAsync(Strings.InviteLabelMissing(ctx.Guild.Id)));
        }

        /// <summary>
        ///     Lists labelled invite codes.
        /// </summary>
        [SlashCommand("list", "Lists labelled invite codes")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task List()
        {
            var labels = await Service.GetLabelsAsync(ctx.Guild.Id);
            if (labels.Count == 0)
            {
                await ReplyErrorAsync(Strings.InviteLabelsNone(ctx.Guild.Id));
                return;
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.InviteLabelsTitle(ctx.Guild.Id))
                .WithDescription(string.Join("\n", labels.Take(40).Select(x =>
                    $"`{x.InviteCode}`: **{x.Label}**" +
                    (x.RoleId.HasValue ? $" role <@&{x.RoleId}>" : "") +
                    (x.OwnerUserId.HasValue ? $" owner <@{x.OwnerUserId}>" : ""))));

            await RespondAsync(embed: eb.Build());
        }
    }
}