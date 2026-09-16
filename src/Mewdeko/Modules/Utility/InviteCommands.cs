using System.IO;
using System.Text;
using DataModel;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Provides commands for managing and viewing invite-related information.
    /// </summary>
    [Group]
    public class InviteCommands : MewdekoSubmodule<InviteCountService>
    {
        private readonly InteractiveService interactiveService;

        /// <summary>
        ///     Initializes a new instance of the <see cref="InviteCommands" /> class.
        /// </summary>
        /// <param name="serv">The interactive service for handling paginated responses.</param>
        public InviteCommands(InteractiveService serv)
        {
            interactiveService = serv;
        }

        /// <summary>
        ///     Displays a user's invite breakdown and rank.
        /// </summary>
        /// <param name="user">The user to check invites for. If null, checks for the command user.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Invites(IUser? user = null)
        {
            user ??= Context.User;
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

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Displays the current invite settings for the guild.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task InviteSettings()
        {
            var settings = await Service.GetInviteCountSettingsAsync(Context.Guild.Id);
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.InviteSettingsTitle(ctx.Guild.Id))
                .AddField(Strings.InviteSettingTracking(ctx.Guild.Id), GetEnDis(settings.IsEnabled), true)
                .AddField(Strings.InviteSettingRemoveOnLeave(ctx.Guild.Id), GetEnDis(settings.RemoveInviteOnLeave),
                    true)
                .AddField(Strings.InviteSettingMinAge(ctx.Guild.Id),
                    settings.MinAccountAge == TimeSpan.Zero ? "Off" : $"{settings.MinAccountAge.TotalDays:0.#}d", true)
                .AddField(Strings.InviteSettingRejoins(ctx.Guild.Id), GetEnDis(settings.CountRejoins), true)
                .AddField(Strings.InviteSettingNoAvatar(ctx.Guild.Id), GetEnDis(settings.FakeOnNoAvatar), true)
                .AddField(Strings.InviteSettingLinkChannel(ctx.Guild.Id),
                    settings.LinkChannelId.HasValue ? MentionUtils.MentionChannel(settings.LinkChannelId.Value) : "-",
                    true)
                .AddField(Strings.InviteSettingLogChannel(ctx.Guild.Id),
                    settings.LogChannelId.HasValue ? MentionUtils.MentionChannel(settings.LogChannelId.Value) : "-",
                    true);

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Toggles invite tracking for the guild.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task ToggleInviteTracking()
        {
            var newState = await Service.SetInviteTrackingEnabledAsync(Context.Guild.Id,
                !(await Service.GetInviteCountSettingsAsync(Context.Guild.Id)).IsEnabled);
            await ReplyConfirmAsync(newState
                ? Strings.InviteTrackingEnabled(Context.Guild.Id)
                : Strings.InviteTrackingDisabled(Context.Guild.Id));
        }

        /// <summary>
        ///     Toggles whether invites should be removed when a user leaves the guild.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task ToggleRemoveInviteOnLeave()
        {
            var newState = await Service.SetRemoveInviteOnLeaveAsync(Context.Guild.Id,
                !(await Service.GetInviteCountSettingsAsync(Context.Guild.Id)).RemoveInviteOnLeave);
            await ReplyConfirmAsync(newState
                ? Strings.RemoveInviteOnLeaveEnabled(Context.Guild.Id)
                : Strings.RemoveInviteOnLeaveDisabled(Context.Guild.Id));
        }

        /// <summary>
        ///     Toggles whether rejoining members earn their inviter a regular invite again or are flagged as fake.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task ToggleCountRejoins()
        {
            var newState = await Service.SetCountRejoinsAsync(Context.Guild.Id,
                !(await Service.GetInviteCountSettingsAsync(Context.Guild.Id)).CountRejoins);
            await ReplyConfirmAsync(newState
                ? Strings.InviteRejoinsCounted(ctx.Guild.Id)
                : Strings.InviteRejoinsFake(ctx.Guild.Id));
        }

        /// <summary>
        ///     Toggles whether members without an avatar are flagged as fake joins.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task ToggleFakeNoAvatar()
        {
            var newState = await Service.SetFakeOnNoAvatarAsync(Context.Guild.Id,
                !(await Service.GetInviteCountSettingsAsync(Context.Guild.Id)).FakeOnNoAvatar);
            await ReplyConfirmAsync(newState
                ? Strings.InviteNoAvatarFakeOn(ctx.Guild.Id)
                : Strings.InviteNoAvatarFakeOff(ctx.Guild.Id));
        }

        /// <summary>
        ///     Sets the minimum account age required for an invite to be counted.
        /// </summary>
        /// <param name="days">The minimum age in days. Zero disables the check.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task SetMinAccountAge(int days)
        {
            if (days is < 0 or > 300)
            {
                await ReplyErrorAsync(Strings.InviteMinAgeRange(ctx.Guild.Id));
                return;
            }

            await Service.SetMinAccountAgeAsync(Context.Guild.Id, TimeSpan.FromDays(days));
            await ReplyConfirmAsync(Strings.MinAccountAgeSet(ctx.Guild.Id, days));
        }

        /// <summary>
        ///     Sets the channel that personal invite links are created for.
        /// </summary>
        /// <param name="channel">The channel, or null to use the system channel.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task InviteLinkChannel(ITextChannel? channel = null)
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
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task InviteLogChannel(ITextChannel? channel = null)
        {
            await Service.SetLogChannelAsync(ctx.Guild.Id, channel?.Id);
            await ReplyConfirmAsync(channel == null
                ? Strings.InviteLogChannelCleared(ctx.Guild.Id)
                : Strings.InviteLogChannelSet(ctx.Guild.Id, channel.Mention));
        }

        /// <summary>
        ///     Displays a leaderboard of users with the most invites.
        /// </summary>
        /// <param name="range">The time window.</param>
        /// <param name="role">Only include inviters with this role.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task InviteLeaderboard(StatsRange range = StatsRange.AllTime, IRole? role = null)
        {
            var leaderboard = await Service.GetInviteLeaderboardAsync(Context.Guild, range, role?.Id, 200);

            if (leaderboard.Count == 0)
            {
                await ReplyErrorAsync(Strings.NoInviteData(ctx.Guild.Id));
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(Context.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex((leaderboard.Count - 1) / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactiveService.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);
            return;

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder().WithOkColor()
                    .WithTitle(Strings.InviteLeaderboardTitle(ctx.Guild.Id) + $" ({range.DisplayName()})")
                    .WithDescription(string.Join("\n", leaderboard.Skip(page * 10).Take(10)
                        .Select((x, i) => FormatLeaderboardLine(page * 10 + i + 1, x))));
            }
        }

        private string FormatLeaderboardLine(int rank, InviteLeaderboardEntry x)
        {
            return Strings.InviteLeaderboardEntry(ctx.Guild.Id, rank, x.Username, x.Total, x.Regular, x.Left,
                x.Fake, x.Bonus);
        }

        /// <summary>
        ///     Displays growth analytics: joins, leaves, retention, fake joins, join sources and top codes.
        /// </summary>
        /// <param name="range">The time window.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task InviteStats(StatsRange range = StatsRange.Monthly)
        {
            var stats = await Service.GetAnalyticsAsync(ctx.Guild, range);
            await ctx.Channel.SendMessageAsync(embed: BuildAnalyticsEmbed(stats).Build());
        }

        private EmbedBuilder BuildAnalyticsEmbed(InviteAnalytics stats)
        {
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

            return eb;
        }

        /// <summary>
        ///     Displays who invited a specific user to the guild, and how.
        /// </summary>
        /// <param name="user">The user to check. If null, checks for the command user.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task WhoInvited(IUser? user = null)
        {
            user ??= Context.User;
            var record = await Service.GetJoinRecordAsync(ctx.Guild.Id, user.Id);
            var inviter = await Service.GetInviter(user.Id, Context.Guild);

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

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Displays the members invited by a specific user.
        /// </summary>
        /// <param name="user">The user whose invites to check. If null, checks for the command user.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task InvitedUsers(IUser? user = null)
        {
            user ??= Context.User;
            var records = await Service.GetInvitedRecordsAsync(ctx.Guild.Id, user.Id);
            await SendInvitedListAsync(records, Strings.InvitedUsersTitle(ctx.Guild.Id, user.Username));
        }

        /// <summary>
        ///     Displays the members who joined through an invite code.
        /// </summary>
        /// <param name="code">The invite code or URL.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task InvitedByCode(string code)
        {
            var records = await Service.GetInvitedRecordsAsync(ctx.Guild.Id, code: code);
            await SendInvitedListAsync(records,
                Strings.InvitedByCodeTitle(ctx.Guild.Id, InviteCountService.NormalizeCode(code)));
        }

        /// <summary>
        ///     Displays the members who joined through any code carrying a label.
        /// </summary>
        /// <param name="label">The label.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task InvitedByLabel([Remainder] string label)
        {
            var records = await Service.GetInvitedRecordsAsync(ctx.Guild.Id, label: label);
            await SendInvitedListAsync(records, Strings.InvitedByLabelTitle(ctx.Guild.Id, label));
        }

        private async Task SendInvitedListAsync(List<InvitedBy> records, string title)
        {
            if (records.Count == 0)
            {
                await ReplyErrorAsync(Strings.NoInvitedUsersFound(ctx.Guild.Id));
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(Context.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex((records.Count - 1) / 15)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactiveService.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);
            return;

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder().WithOkColor()
                    .WithTitle(title)
                    .WithDescription(string.Join("\n", records.Skip(page * 15).Take(15).Select(FormatRecord)));
            }
        }

        private static string FormatRecord(InvitedBy record)
        {
            var when = record.DateAdded.HasValue
                ? TimestampTag.FromDateTime(record.DateAdded.Value, TimestampTagStyles.ShortDate).ToString()
                : "?";
            var flags = new List<string>();
            if (record.IsFake) flags.Add($"fake: {(InviteFakeReason)record.FakeReason}");
            if (record.LeftAt.HasValue) flags.Add("left");
            var suffix = flags.Count > 0 ? $" ({string.Join(", ", flags)})" : "";
            return $"<@{record.UserId}> {when}{suffix}";
        }

        /// <summary>
        ///     Lists the invite codes a user has created.
        /// </summary>
        /// <param name="user">The user. If null, uses the command user.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [BotPerm(GuildPermission.ManageGuild)]
        public async Task InviteCodes(IUser? user = null)
        {
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

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Shows your personal invite link, creating a permanent one when you have none.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [BotPerm(GuildPermission.CreateInstantInvite)]
        public async Task InviteLink()
        {
            var invite = await Service.GetOrCreateUserLinkAsync(ctx.Guild, (IGuildUser)ctx.User);
            if (invite == null)
            {
                await ReplyErrorAsync(Strings.InviteLinkFailed(ctx.Guild.Id));
                return;
            }

            await ReplyConfirmAsync(Strings.InviteLinkYours(ctx.Guild.Id, invite.Url, invite.Uses ?? 0));
        }

        /// <summary>
        ///     Adds regular invites to a user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="amount">How many to add.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task AddInvites(IGuildUser user, int amount)
        {
            if (amount <= 0)
            {
                await ReplyErrorAsync(Strings.InviteAmountPositive(ctx.Guild.Id));
                return;
            }

            var row = await Service.AdjustInvitesAsync(ctx.Guild.Id, user.Id, amount);
            await ReplyConfirmAsync(Strings.InvitesAdded(ctx.Guild.Id, amount, user.Mention, row.Count));
        }

        /// <summary>
        ///     Removes regular invites from a user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="amount">How many to remove.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task RemoveInvites(IGuildUser user, int amount)
        {
            if (amount <= 0)
            {
                await ReplyErrorAsync(Strings.InviteAmountPositive(ctx.Guild.Id));
                return;
            }

            var row = await Service.AdjustInvitesAsync(ctx.Guild.Id, user.Id, -amount);
            await ReplyConfirmAsync(Strings.InvitesRemoved(ctx.Guild.Id, amount, user.Mention, row.Count));
        }

        /// <summary>
        ///     Grants bonus invites to a user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="amount">How many to grant.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task AddBonusInvites(IGuildUser user, int amount)
        {
            if (amount <= 0)
            {
                await ReplyErrorAsync(Strings.InviteAmountPositive(ctx.Guild.Id));
                return;
            }

            var row = await Service.AdjustInvitesAsync(ctx.Guild.Id, user.Id, bonus: amount);
            await ReplyConfirmAsync(Strings.BonusInvitesAdded(ctx.Guild.Id, amount, user.Mention, row.Count));
        }

        /// <summary>
        ///     Takes bonus invites away from a user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="amount">How many to remove.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task RemoveBonusInvites(IGuildUser user, int amount)
        {
            if (amount <= 0)
            {
                await ReplyErrorAsync(Strings.InviteAmountPositive(ctx.Guild.Id));
                return;
            }

            var row = await Service.AdjustInvitesAsync(ctx.Guild.Id, user.Id, bonus: -amount);
            await ReplyConfirmAsync(Strings.BonusInvitesRemoved(ctx.Guild.Id, amount, user.Mention, row.Count));
        }

        /// <summary>
        ///     Marks some of a user's invites as fake.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="amount">How many to flag.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task AddFakeInvites(IGuildUser user, int amount)
        {
            if (amount <= 0)
            {
                await ReplyErrorAsync(Strings.InviteAmountPositive(ctx.Guild.Id));
                return;
            }

            var row = await Service.AdjustInvitesAsync(ctx.Guild.Id, user.Id, fake: amount);
            await ReplyConfirmAsync(Strings.FakeInvitesAdded(ctx.Guild.Id, amount, user.Mention, row.Count));
        }

        /// <summary>
        ///     Clears fake invites from a user.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="amount">How many to clear.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task RemoveFakeInvites(IGuildUser user, int amount)
        {
            if (amount <= 0)
            {
                await ReplyErrorAsync(Strings.InviteAmountPositive(ctx.Guild.Id));
                return;
            }

            var row = await Service.AdjustInvitesAsync(ctx.Guild.Id, user.Id, fake: -amount);
            await ReplyConfirmAsync(Strings.FakeInvitesRemoved(ctx.Guild.Id, amount, user.Mention, row.Count));
        }

        /// <summary>
        ///     Resets a user's invites.
        /// </summary>
        /// <param name="user">The user.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task ResetInvites(IGuildUser user)
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
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ResetInvites(InviteResetScope scope)
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
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        [BotPerm(GuildPermission.ManageGuild)]
        public async Task SyncInvites(IGuildUser? user = null)
        {
            var raised = await Service.SyncInvitesAsync(ctx.Guild, user?.Id);
            await ReplyConfirmAsync(Strings.InvitesSynced(ctx.Guild.Id, raised));
        }

        /// <summary>
        ///     Deletes an invite code from the server.
        /// </summary>
        /// <param name="code">The code or URL.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        [BotPerm(GuildPermission.ManageGuild)]
        public async Task DeleteInvite(string code)
        {
            var deleted = await Service.DeleteInviteAsync(ctx.Guild, code);
            await (deleted
                ? ReplyConfirmAsync(Strings.InviteDeleted(ctx.Guild.Id, InviteCountService.NormalizeCode(code)))
                : ReplyErrorAsync(Strings.InviteNotFound(ctx.Guild.Id)));
        }

        /// <summary>
        ///     Deletes every unlabelled invite code used at most a given number of times.
        /// </summary>
        /// <param name="maxUses">Codes used more than this are kept.</param>
        /// <param name="channel">Only purge codes pointing at this channel.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        [BotPerm(GuildPermission.ManageGuild)]
        public async Task PurgeInvites(int maxUses = 0, ITextChannel? channel = null)
        {
            if (!await PromptUserConfirmAsync(Strings.InvitePurgeConfirm(ctx.Guild.Id, maxUses), ctx.User.Id))
                return;

            var deleted = await Service.PurgeInviteCodesAsync(ctx.Guild, maxUses, [], [],
                channel == null ? [] : [channel.Id], []);
            await ReplyConfirmAsync(Strings.InvitesPurged(ctx.Guild.Id, deleted));
        }

        /// <summary>
        ///     Toggles whether a user can earn invite credit.
        /// </summary>
        /// <param name="user">The user.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task InviteBlacklist(IGuildUser user)
        {
            await ToggleExclusionAsync(user.Id, InviteExclusionKind.BlacklistedUser, user.Mention);
        }

        /// <summary>
        ///     Toggles whether holders of a role can earn invite credit.
        /// </summary>
        /// <param name="role">The role.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task InviteBlacklist(IRole role)
        {
            await ToggleExclusionAsync(role.Id, InviteExclusionKind.BlacklistedRole, role.Mention);
        }

        /// <summary>
        ///     Toggles whether a user is hidden from invite leaderboards.
        /// </summary>
        /// <param name="user">The user.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task InviteHide(IGuildUser user)
        {
            await ToggleExclusionAsync(user.Id, InviteExclusionKind.HiddenUser, user.Mention);
        }

        private async Task ToggleExclusionAsync(ulong targetId, InviteExclusionKind kind, string mention)
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

        /// <summary>
        ///     Lists blacklisted inviters, blacklisted roles and hidden users.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task InviteExclusions()
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

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Labels an invite code so it shows up by name in stats and placeholders.
        /// </summary>
        /// <param name="code">The code or URL.</param>
        /// <param name="label">The label text.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task InviteLabelSet(string code, [Remainder] string label)
        {
            if (label.Length > 64)
            {
                await ReplyErrorAsync(Strings.InviteLabelTooLong(ctx.Guild.Id));
                return;
            }

            var saved = await Service.SetLabelAsync(ctx.Guild.Id, code, label);
            await ReplyConfirmAsync(Strings.InviteLabelSet(ctx.Guild.Id, saved.InviteCode, saved.Label));
        }

        /// <summary>
        ///     Sets or clears the role granted to members who join through a labelled code.
        /// </summary>
        /// <param name="code">The code or URL.</param>
        /// <param name="role">The role, or null to clear it.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        [BotPerm(GuildPermission.ManageRoles)]
        public async Task InviteLabelRole(string code, IRole? role = null)
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
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task InviteLabelRemove(string code)
        {
            var removed = await Service.RemoveLabelAsync(ctx.Guild.Id, code);
            await (removed
                ? ReplyConfirmAsync(Strings.InviteLabelRemoved(ctx.Guild.Id, InviteCountService.NormalizeCode(code)))
                : ReplyErrorAsync(Strings.InviteLabelMissing(ctx.Guild.Id)));
        }

        /// <summary>
        ///     Lists labelled invite codes.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task InviteLabels()
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

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Exports the invite leaderboard as a CSV file.
        /// </summary>
        /// <param name="range">The time window.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        [Ratelimit(300)]
        public async Task InviteExport(StatsRange range = StatsRange.AllTime)
        {
            var csv = await Service.ExportLeaderboardCsvAsync(ctx.Guild, range);
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            await ctx.Channel.SendFileAsync(stream, $"invites-{range}.csv", Strings.InviteExportDone(ctx.Guild.Id));
        }

        /// <summary>
        ///     Exports the members a user invited as a CSV file.
        /// </summary>
        /// <param name="user">The inviter. If null, exports every witnessed join.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        [Ratelimit(300)]
        public async Task InvitedExport(IUser? user = null)
        {
            var csv = await Service.ExportInvitedListCsvAsync(ctx.Guild, user?.Id, null, null);
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            await ctx.Channel.SendFileAsync(stream, $"invited-{user?.Id.ToString() ?? "all"}.csv",
                Strings.InviteExportDone(ctx.Guild.Id));
        }

        /// <summary>
        ///     Bans every member a user invited.
        /// </summary>
        /// <param name="inviter">The inviter.</param>
        /// <param name="reason">The ban reason.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        [BotPerm(GuildPermission.BanMembers)]
        public async Task InviteMassBan(IUser inviter, [Remainder] string? reason = null)
        {
            var records = await Service.GetInvitedRecordsAsync(ctx.Guild.Id, inviter.Id, includeLeft: false);
            if (records.Count == 0)
            {
                await ReplyErrorAsync(Strings.NoInvitedUsers(ctx.Guild.Id, inviter.Username));
                return;
            }

            if (!await PromptUserConfirmAsync(Strings.InviteMassBanConfirm(ctx.Guild.Id, records.Count,
                    inviter.Mention), ctx.User.Id))
                return;

            var (banned, failed) = await Service.MassBanAsync(ctx.Guild, inviter.Id, null,
                reason ?? Strings.InviteMassBanReason(ctx.Guild.Id, ctx.User.ToString()));
            await ReplyConfirmAsync(Strings.InviteMassBanDone(ctx.Guild.Id, banned, failed));
        }

        /// <summary>
        ///     Bans every member who joined through an invite code.
        /// </summary>
        /// <param name="code">The code or URL.</param>
        /// <param name="reason">The ban reason.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        [BotPerm(GuildPermission.BanMembers)]
        public async Task InviteMassBanCode(string code, [Remainder] string? reason = null)
        {
            var records = await Service.GetInvitedRecordsAsync(ctx.Guild.Id, code: code, includeLeft: false);
            if (records.Count == 0)
            {
                await ReplyErrorAsync(Strings.NoInvitedUsersFound(ctx.Guild.Id));
                return;
            }

            if (!await PromptUserConfirmAsync(Strings.InviteMassBanConfirm(ctx.Guild.Id, records.Count,
                    $"`{InviteCountService.NormalizeCode(code)}`"), ctx.User.Id))
                return;

            var (banned, failed) = await Service.MassBanAsync(ctx.Guild, null, code,
                reason ?? Strings.InviteMassBanReason(ctx.Guild.Id, ctx.User.ToString()));
            await ReplyConfirmAsync(Strings.InviteMassBanDone(ctx.Guild.Id, banned, failed));
        }

        private static string GetEnDis(bool endis)
        {
            return endis ? "Enabled" : "Disabled";
        }
    }
}