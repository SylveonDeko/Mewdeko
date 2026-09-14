using System.Text;
using Discord.Interactions;
using Discord.Net;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Xp.Models;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Xp;

/// <summary>
///     Slash commands for the XP system.
/// </summary>
[Group("xp", "Experience and levels")]
public partial class SlashXp(
    InteractiveService interactivity,
    ICurrencyService currencyService,
    XpRoleSyncService roleSyncService,
    ILogger<SlashXp> logger)
    : MewdekoSlashModuleBase<XpService>
{
    /// <summary>
    ///     Shows a user's XP card with their rank, level, and progress.
    /// </summary>
    /// <param name="user">The user to show the XP card for. If not specified, shows the caller's card.</param>
    [SlashCommand("rank", "Shows your or another user's XP card")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Rank([Summary("user", "The user to show the card for")] IGuildUser? user = null)
    {
        await DeferAsync();
        user ??= (IGuildUser)ctx.User;

        if (user.IsBot)
        {
            await ReplyErrorAsync(Strings.XpBotsNoRank(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        try
        {
            var stats = await Service.GetUserXpStatsAsync(ctx.Guild.Id, user.Id);

            switch (stats.TotalXp)
            {
                case 0 when user.Id == ctx.User.Id:
                    await ReplyErrorAsync(Strings.XpYouNoXp(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case 0:
                    await ReplyErrorAsync(Strings.XpUserNoXp(ctx.Guild.Id, user.ToString())).ConfigureAwait(false);
                    return;
                default:
                {
                    var cardStream = await Service.GenerateXpCardAsync(ctx.Guild.Id, user.Id);
                    await ctx.Interaction.FollowupWithFileAsync(cardStream, "xp.png",
                        Strings.XpCardFor(ctx.Guild.Id, user.ToString()));
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting XP card for {UserId} in {GuildId}", user.Id, ctx.Guild.Id);
            await ReplyErrorAsync(Strings.XpErrorGettingCard(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Shows a user's XP information in a text format that's accessible for screen readers.
    /// </summary>
    /// <param name="user">The user to show XP information for. If not specified, shows the caller's stats.</param>
    [SlashCommand("text-rank", "Shows XP stats as plain text, friendly for screen readers")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task TextRank([Summary("user", "The user to show stats for")] IGuildUser? user = null)
    {
        await DeferAsync();
        user ??= (IGuildUser)ctx.User;

        if (user.IsBot)
        {
            await ReplyErrorAsync(Strings.XpBotsNoRank(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        try
        {
            var stats = await Service.GetUserXpStatsAsync(ctx.Guild.Id, user.Id);

            switch (stats.TotalXp)
            {
                case 0 when user.Id == ctx.User.Id:
                    await ReplyErrorAsync(Strings.XpYouNoXp(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case 0:
                    await ReplyErrorAsync(Strings.XpUserNoXp(ctx.Guild.Id, user.ToString())).ConfigureAwait(false);
                    return;
            }

            var progressPercent = (int)((double)stats.LevelXp / stats.RequiredXp * 100);

            var response = new StringBuilder();
            response.AppendLine(Strings.TextRankHeader(ctx.Guild.Id, user.ToString()));
            response.AppendLine(Strings.TextRankLevel(ctx.Guild.Id, stats.Level));
            response.AppendLine(Strings.TextRankXp(ctx.Guild.Id, stats.TotalXp));
            response.AppendLine(
                Strings.TextRankProgress(ctx.Guild.Id, stats.LevelXp, stats.RequiredXp, progressPercent));
            response.AppendLine(Strings.TextRankServerPosition(ctx.Guild.Id, stats.Rank));

            if (stats.BonusXp > 0)
            {
                response.AppendLine(Strings.TextRankBonusXp(ctx.Guild.Id, stats.BonusXp));
            }

            await ctx.Interaction.FollowupAsync(response.ToString()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting text XP stats for {UserId} in {GuildId}", user.Id, ctx.Guild.Id);
            await ReplyErrorAsync(Strings.XpErrorGettingStats(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Shows the XP leaderboard for the server.
    /// </summary>
    /// <remarks>
    ///     When the server has currency in circulation a bare invocation asks which leaderboard was meant.
    ///     Servers with no economy go straight to XP, and naming a page always means XP.
    /// </remarks>
    /// <param name="page">Page number to display (starts at 1).</param>
    [SlashCommand("leaderboard", "Shows the XP leaderboard for the server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Leaderboard([Summary("page", "Page number to open on")] int? page = null)
    {
        if (page is null && await GuildHasEconomy())
        {
            var (embed, components) = LeaderboardRenderer.BuildPicker(ctx.Guild.Id, ctx.User.Id, Strings);
            await ctx.Interaction.RespondAsync(embed: embed, components: components);
            return;
        }

        await DeferAsync();

        var startPage = page ?? 1;
        if (startPage < 1)
            startPage = 1;

        var (entries, totalCount) = await Service.GetLeaderboardAsync(ctx.Guild.Id, startPage);

        if (entries.Count == 0)
        {
            await ReplyErrorAsync(Strings.XpLeaderboardEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var users = await ctx.Guild.GetUsersAsync();
        var userDict = users.ToDictionary(u => u.Id, u => u);

        const int pageSize = 10;
        var maxPageIndex = Math.Max(0, (int)Math.Ceiling(totalCount / (double)pageSize) - 1);

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(BuildPage)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(maxPageIndex)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
            TimeSpan.FromMinutes(60),
            InteractionResponseType.DeferredChannelMessageWithSource).ConfigureAwait(false);

        async Task<PageBuilder> BuildPage(int pageNum)
        {
            var pageData = await Service.GetLeaderboardAsync(ctx.Guild.Id, pageNum + 1);

            var lines = pageData.Users.Select(entry => Strings.XpLeaderboardLine(
                ctx.Guild.Id,
                entry.Rank,
                userDict.TryGetValue(entry.UserId, out var guildUser) ? guildUser.ToString() : entry.UserId.ToString(),
                entry.Level,
                entry.TotalXp));

            return new PageBuilder()
                .WithOkColor()
                .WithTitle(Strings.XpLeaderboardTitle(ctx.Guild.Id))
                .WithDescription(string.Join("\n", lines));
        }
    }

    /// <summary>
    ///     Whether anyone in this guild holds currency. Used to decide whether the leaderboard command
    ///     is ambiguous enough to be worth a prompt.
    /// </summary>
    private async Task<bool> GuildHasEconomy()
    {
        try
        {
            var balances = await currencyService.GetAllUserBalancesAsync(ctx.Guild.Id);
            return balances.Any(x => x.NetWorth > 0);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    ///     Shows the server's XP settings.
    /// </summary>
    [SlashCommand("settings", "Shows the server's XP settings")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task XpSettings()
    {
        await DeferAsync();
        var settings = await Service.GetGuildXpSettingsAsync(ctx.Guild.Id);
        var exclusions = new List<string>();

        var excludedUsers = await Service.GetExcludedItemsAsync(ctx.Guild.Id, ExcludedItemType.User);
        if (excludedUsers.Count > 0)
            exclusions.Add(Strings.XpExcludedUsers(ctx.Guild.Id, excludedUsers.Count));

        var excludedRoles = await Service.GetExcludedItemsAsync(ctx.Guild.Id, ExcludedItemType.Role);
        if (excludedRoles.Count > 0)
            exclusions.Add(Strings.XpExcludedRoles(ctx.Guild.Id, excludedRoles.Count));

        var excludedChannels = await Service.GetExcludedItemsAsync(ctx.Guild.Id, ExcludedItemType.Channel);
        if (excludedChannels.Count > 0)
            exclusions.Add(Strings.XpExcludedChannels(ctx.Guild.Id, excludedChannels.Count));

        var boostEvents = await Service.GetActiveBoostEventsAsync(ctx.Guild.Id);
        var boostInfo = boostEvents.Count > 0
            ? Strings.XpActiveBoostEvents(ctx.Guild.Id, boostEvents.Count)
            : Strings.XpNoActiveBoostEvents(ctx.Guild.Id);

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.XpSettingsTitle(ctx.Guild.Id, ctx.Guild.Name))
            .AddField(Strings.XpBasicSettings(ctx.Guild.Id),
                Strings.XpSettingsBasicInfo(
                    ctx.Guild.Id,
                    settings.XpPerMessage,
                    settings.MessageXpCooldown,
                    settings.VoiceXpPerMinute,
                    settings.VoiceXpTimeout,
                    settings.XpMultiplier,
                    ((XpCurveType)settings.XpCurveType).ToString(),
                    settings.FirstMessageBonus
                ))
            .AddField(Strings.XpGeneralSettings(ctx.Guild.Id),
                Strings.XpSettingsGeneralInfo(
                    ctx.Guild.Id,
                    settings.XpGainDisabled,
                    settings.ExclusiveRoleRewards
                ))
            .AddField(Strings.XpDisplaySettings(ctx.Guild.Id),
                Strings.XpSettingsDisplayInfo(
                    ctx.Guild.Id,
                    string.IsNullOrWhiteSpace(settings.CustomXpImageUrl)
                        ? Strings.No(ctx.Guild.Id)
                        : Strings.Yes(ctx.Guild.Id)
                ))
            .AddField(Strings.XpDecaySettings(ctx.Guild.Id),
                Strings.XpSettingsDecayInfo(
                    ctx.Guild.Id,
                    settings.EnableXpDecay,
                    settings.InactivityDaysBeforeDecay,
                    settings.DailyDecayPercentage
                ))
            .AddField(Strings.XpExclusions(ctx.Guild.Id),
                exclusions.Count > 0 ? string.Join("\n", exclusions) : Strings.XpNoExclusions(ctx.Guild.Id))
            .AddField(Strings.XpBoosts(ctx.Guild.Id), boostInfo);

        await ctx.Interaction.FollowupAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Synchronizes XP roles for the current user.
    /// </summary>
    [SlashCommand("sync-my-roles", "Synchronizes your level roles with your current XP level")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [InteractionRatelimit(300)]
    public async Task SyncMyXpRoles()
    {
        await DeferAsync();
        try
        {
            var result = await roleSyncService.SyncUserRolesAsync(ctx.Guild, ctx.User.Id);

            if (result.HasError)
            {
                await ReplyErrorAsync(result.ErrorMessage ?? Strings.XpRoleSyncFailed(ctx.Guild.Id))
                    .ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle(Strings.XpRoleSyncMyComplete(ctx.Guild.Id))
                .AddField(Strings.XpRolesAdded(ctx.Guild.Id),
                    Strings.XpRoleSyncRolesAdded(ctx.Guild.Id, result.RolesAdded), true)
                .AddField(Strings.XpRolesRemoved(ctx.Guild.Id),
                    Strings.XpRoleSyncRolesRemoved(ctx.Guild.Id, result.RolesRemoved), true)
                .WithOkColor()
                .WithTimestamp(DateTimeOffset.UtcNow);

            if (result.RolesAdded == 0 && result.RolesRemoved == 0)
            {
                embed.WithDescription(Strings.XpRoleSyncMyUpToDate(ctx.Guild.Id, result.Level));
            }
            else
            {
                embed.WithDescription(Strings.XpRoleSyncMyDesc(ctx.Guild.Id, result.Level));
            }

            await ctx.Interaction.FollowupAsync(embed: embed.Build());
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("cooldown"))
        {
            await ReplyErrorAsync(ex.Message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing roles for user {UserId} in guild {GuildId}", ctx.User.Id, ctx.Guild.Id);
            await ReplyErrorAsync(Strings.XpRoleSyncMyError(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Lists excluded users, roles, or channels.
    /// </summary>
    /// <param name="type">The type of exclusions to list: users, roles, or channels.</param>
    [SlashCommand("exclude-list", "Lists users, roles, or channels excluded from gaining XP")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task XpExcludeList([Summary("type", "The type of exclusions to list")] ExcludedItemType type)
    {
        await DeferAsync();

        string title;
        string typeName;

        switch (type)
        {
            case ExcludedItemType.User:
                title = Strings.XpExcludedUsersTitle(ctx.Guild.Id);
                typeName = "users";
                break;
            case ExcludedItemType.Role:
                title = Strings.XpExcludedRolesTitle(ctx.Guild.Id);
                typeName = "roles";
                break;
            case ExcludedItemType.Channel:
                title = Strings.XpExcludedChannelsTitle(ctx.Guild.Id);
                typeName = "channels";
                break;
            default:
                await ReplyErrorAsync(Strings.XpExcludeListInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
        }

        var items = await Service.GetExcludedItemsAsync(ctx.Guild.Id, type);

        if (items.Count == 0)
        {
            await ReplyErrorAsync(Strings.XpNoExcludedItems(ctx.Guild.Id, typeName)).ConfigureAwait(false);
            return;
        }

        var names = new List<string>();

        foreach (var id in items)
        {
            switch (type)
            {
                case ExcludedItemType.User:
                    var user = await ctx.Guild.GetUserAsync(id);
                    names.Add(user != null ? user.ToString() : id.ToString());
                    break;
                case ExcludedItemType.Role:
                    var role = ctx.Guild.GetRole(id);
                    names.Add(role != null ? role.Name : id.ToString());
                    break;
                case ExcludedItemType.Channel:
                    var channel = await ctx.Guild.GetTextChannelAsync(id);
                    names.Add(channel != null ? channel.Mention : id.ToString());
                    break;
            }
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(names.Count / 20)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
            TimeSpan.FromMinutes(60),
            InteractionResponseType.DeferredChannelMessageWithSource).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder()
                .WithOkColor()
                .WithTitle($"{title} ({names.Count})")
                .WithDescription(string.Join("\n", names.Skip(page * 20).Take(20)));
        }
    }

    /// <summary>
    ///     Administrative XP commands for managing user XP and exclusions.
    /// </summary>
    [Group("admin", "Manage user XP, exclusions, and role sync")]
    public class XpAdmin(XpRoleSyncService roleSyncService, ILogger<XpAdmin> logger)
        : MewdekoSlashSubmodule<XpService>
    {
        /// <summary>
        ///     Adds XP to a user.
        /// </summary>
        /// <param name="user">The user to add XP to.</param>
        /// <param name="amount">The amount of XP to add.</param>
        [SlashCommand("add", "Adds XP to a user")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task AddXp([Summary("user", "The user to add XP to")] IGuildUser user,
            [Summary("amount", "The amount of XP to add")]
            int amount)
        {
            if (amount <= 0)
            {
                await ReplyErrorAsync(Strings.XpAmountPositive(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (user.IsBot)
            {
                await ReplyErrorAsync(Strings.XpCantAddToBot(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.AddXpAsync(ctx.Guild.Id, user.Id, amount);
            await ReplyConfirmAsync(Strings.XpAdded(ctx.Guild.Id, amount, user.ToString())).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets a user's XP to a specific amount.
        /// </summary>
        /// <param name="user">The user to set XP for.</param>
        /// <param name="amount">The amount of XP to set.</param>
        [SlashCommand("set", "Sets a user's XP to a specific amount")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task SetXp([Summary("user", "The user to set XP for")] IGuildUser user,
            [Summary("amount", "The amount of XP to set")]
            long amount)
        {
            if (amount < 0)
            {
                await ReplyErrorAsync(Strings.XpAmountNotNegative(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (user.IsBot)
            {
                await ReplyErrorAsync(Strings.XpCantAddToBot(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetUserXpAsync(ctx.Guild.Id, user.Id, amount);
            await ReplyConfirmAsync(Strings.XpSet(ctx.Guild.Id, user.ToString(), amount)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Resets a user's XP to zero, or resets XP for the whole server when no user is given.
        /// </summary>
        /// <param name="user">The user to reset XP for. Leave empty to reset the whole server.</param>
        /// <param name="resetBonus">Whether to also reset bonus XP when resetting a single user.</param>
        /// <param name="confirm">Must be true to reset the whole server.</param>
        [SlashCommand("reset", "Resets XP for a user, or for the whole server when no user is given")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task ResetXp(
            [Summary("user", "The user to reset, leave empty for the whole server")]
            IGuildUser? user = null,
            [Summary("reset-bonus", "Also reset the user's bonus XP")]
            bool resetBonus = false,
            [Summary("confirm", "Must be true to reset the whole server")]
            bool confirm = false)
        {
            if (user != null)
            {
                await Service.ResetUserXpAsync(ctx.Guild.Id, user.Id, resetBonus);

                if (resetBonus)
                    await ReplyConfirmAsync(Strings.XpResetWithBonus(ctx.Guild.Id, user.ToString()))
                        .ConfigureAwait(false);
                else
                    await ReplyConfirmAsync(Strings.XpReset(ctx.Guild.Id, user.ToString())).ConfigureAwait(false);
                return;
            }

            if (!((IGuildUser)ctx.User).GuildPermissions.Administrator)
            {
                await ReplyErrorAsync(Strings.XpResetGuildAdmin(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (!confirm)
            {
                await ReplyErrorAsync(Strings.XpResetGuildConfirm(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await DeferAsync();
            await Service.ResetGuildXp(ctx.Guild.Id, true);
            await ReplyConfirmAsync(Strings.XpResetGuild(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Excludes a user, role, or channel from gaining XP. Exactly one target must be given.
        /// </summary>
        /// <param name="user">The user to exclude.</param>
        /// <param name="role">The role to exclude.</param>
        /// <param name="channel">The channel to exclude.</param>
        [SlashCommand("exclude", "Excludes a user, role, or channel from gaining XP")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task XpExclude([Summary("user", "The user to exclude")] IGuildUser? user = null,
            [Summary("role", "The role to exclude")]
            IRole? role = null,
            [Summary("channel", "The channel to exclude")]
            ITextChannel? channel = null)
        {
            var targets = (user != null ? 1 : 0) + (role != null ? 1 : 0) + (channel != null ? 1 : 0);
            if (targets != 1)
            {
                await ReplyErrorAsync(Strings.XpExcludeTargetRequired(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (user != null)
            {
                await Service.ExcludeItemAsync(ctx.Guild.Id, user.Id, ExcludedItemType.User);
                await ReplyConfirmAsync(Strings.XpUserExcluded(ctx.Guild.Id, user.ToString())).ConfigureAwait(false);
                return;
            }

            if (role != null)
            {
                await Service.ExcludeItemAsync(ctx.Guild.Id, role.Id, ExcludedItemType.Role);
                await ReplyConfirmAsync(Strings.XpRoleExcluded(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
                return;
            }

            await Service.ExcludeItemAsync(ctx.Guild.Id, channel!.Id, ExcludedItemType.Channel);
            await ReplyConfirmAsync(Strings.XpChannelExcluded(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Includes a previously excluded user, role, or channel for XP gain. Exactly one target must be given.
        /// </summary>
        /// <param name="user">The user to include.</param>
        /// <param name="role">The role to include.</param>
        /// <param name="channel">The channel to include.</param>
        [SlashCommand("include", "Re-includes a previously excluded user, role, or channel for XP gain")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task XpInclude([Summary("user", "The user to include")] IGuildUser? user = null,
            [Summary("role", "The role to include")]
            IRole? role = null,
            [Summary("channel", "The channel to include")]
            ITextChannel? channel = null)
        {
            var targets = (user != null ? 1 : 0) + (role != null ? 1 : 0) + (channel != null ? 1 : 0);
            if (targets != 1)
            {
                await ReplyErrorAsync(Strings.XpExcludeTargetRequired(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (user != null)
            {
                await Service.IncludeItemAsync(ctx.Guild.Id, user.Id, ExcludedItemType.User);
                await ReplyConfirmAsync(Strings.XpUserIncluded(ctx.Guild.Id, user.ToString())).ConfigureAwait(false);
                return;
            }

            if (role != null)
            {
                await Service.IncludeItemAsync(ctx.Guild.Id, role.Id, ExcludedItemType.Role);
                await ReplyConfirmAsync(Strings.XpRoleIncluded(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
                return;
            }

            await Service.IncludeItemAsync(ctx.Guild.Id, channel!.Id, ExcludedItemType.Channel);
            await ReplyConfirmAsync(Strings.XpChannelIncluded(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Synchronizes XP roles for all users in the guild.
        /// </summary>
        [SlashCommand("sync-all-roles", "Synchronizes level roles for every user in the server")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [InteractionRatelimit(3600)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task SyncAllXpRoles()
        {
            await DeferAsync();
            try
            {
                var embed = new EmbedBuilder()
                    .WithTitle(Strings.XpRoleSyncAllStarting(ctx.Guild.Id))
                    .WithOkColor()
                    .WithTimestamp(DateTimeOffset.UtcNow);

                var message = await ctx.Interaction.FollowupAsync(embed: embed.Build());

                var result = await roleSyncService.SyncAllUsersAsync(ctx.Guild, async progress =>
                {
                    var progressEmbed = new EmbedBuilder()
                        .WithTitle(Strings.XpRoleSyncProgress(ctx.Guild.Id))
                        .WithDescription(Strings.XpRoleSyncProgressDesc(ctx.Guild.Id, progress.CurrentUser,
                            progress.TotalUsers))
                        .AddField(Strings.XpProgress(ctx.Guild.Id),
                            Strings.XpRoleSyncProgressPercent(ctx.Guild.Id, progress.PercentComplete), true)
                        .AddField(Strings.XpEta(ctx.Guild.Id),
                            Strings.XpRoleSyncEta(ctx.Guild.Id, progress.EstimatedTimeRemaining.TotalMinutes), true)
                        .AddField(Strings.XpRolesModified(ctx.Guild.Id),
                            Strings.XpRoleSyncRolesModified(ctx.Guild.Id, progress.RolesAdded, progress.RolesRemoved),
                            true)
                        .AddField(Strings.XpErrors(ctx.Guild.Id),
                            Strings.XpRoleSyncUsersErrors(ctx.Guild.Id, progress.ErrorCount), true)
                        .WithColor(Color.Blue)
                        .WithTimestamp(DateTimeOffset.UtcNow);

                    try
                    {
                        await message.ModifyAsync(msg => msg.Embed = progressEmbed.Build());
                    }
                    catch (HttpException ex) when (ex.DiscordCode == DiscordErrorCode.WriteRatelimitReached)
                    {
                        logger.LogDebug("Hit rate limit updating progress message for guild {GuildId}", ctx.Guild.Id);
                    }
                    catch
                    {
                    }
                });

                var finalEmbed = new EmbedBuilder()
                    .WithTitle(Strings.XpRoleSyncComplete(ctx.Guild.Id))
                    .WithDescription(Strings.XpRoleSyncCompleteDesc(ctx.Guild.Id, result.ProcessedUsers,
                        result.Duration.TotalMinutes))
                    .AddField(Strings.XpUsersProcessed(ctx.Guild.Id),
                        Strings.XpRoleSyncUsersProcessed(ctx.Guild.Id, result.ProcessedUsers), true)
                    .AddField(Strings.XpUsersWithErrors(ctx.Guild.Id),
                        Strings.XpRoleSyncUsersErrors(ctx.Guild.Id, result.ErrorUsers), true)
                    .AddField(Strings.XpRolesAdded(ctx.Guild.Id),
                        Strings.XpRoleSyncRolesAdded(ctx.Guild.Id, result.RolesAdded), true)
                    .AddField(Strings.XpRolesRemoved(ctx.Guild.Id),
                        Strings.XpRoleSyncRolesRemoved(ctx.Guild.Id, result.RolesRemoved), true)
                    .WithColor(result.ErrorUsers > 0 ? Color.Gold : Color.Green)
                    .WithTimestamp(DateTimeOffset.UtcNow);

                await message.ModifyAsync(msg => msg.Embed = finalEmbed.Build());
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already in progress"))
            {
                await ReplyErrorAsync(Strings.XpRoleSyncAlreadyInProgress(ctx.Guild.Id)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during role sync for guild {GuildId}", ctx.Guild.Id);
                await ReplyErrorAsync(Strings.XpRoleSyncError(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Synchronizes XP roles for a specific user.
        /// </summary>
        /// <param name="user">The user to synchronize roles for.</param>
        [SlashCommand("sync-user-roles", "Synchronizes level roles for a specific user")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [InteractionRatelimit(60)]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task SyncUserXpRoles([Summary("user", "The user to synchronize roles for")] IUser user)
        {
            await DeferAsync();
            try
            {
                var result = await roleSyncService.SyncUserRolesAsync(ctx.Guild, user.Id);

                if (result.HasError)
                {
                    await ReplyErrorAsync(Strings.XpRoleSyncUserFailed(ctx.Guild.Id, user.Mention, result.ErrorMessage))
                        .ConfigureAwait(false);
                    return;
                }

                var embed = new EmbedBuilder()
                    .WithTitle(Strings.XpRoleSyncUserComplete(ctx.Guild.Id))
                    .WithDescription(Strings.XpRoleSyncUserDesc(ctx.Guild.Id, user.Mention, result.Level))
                    .AddField(Strings.XpRolesAdded(ctx.Guild.Id),
                        Strings.XpRoleSyncRolesAdded(ctx.Guild.Id, result.RolesAdded), true)
                    .AddField(Strings.XpRolesRemoved(ctx.Guild.Id),
                        Strings.XpRoleSyncRolesRemoved(ctx.Guild.Id, result.RolesRemoved), true)
                    .WithOkColor()
                    .WithTimestamp(DateTimeOffset.UtcNow);

                await ctx.Interaction.FollowupAsync(embed: embed.Build());
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("cooldown"))
            {
                await ReplyErrorAsync(Strings.XpRoleSyncUserCooldown(ctx.Guild.Id)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error syncing roles for user {UserId} in guild {GuildId}", user.Id, ctx.Guild.Id);
                await ReplyErrorAsync(Strings.XpRoleSyncUserError(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Server XP configuration commands.
    /// </summary>
    [Group("config", "Configure XP rates, multipliers, and curve")]
    public class XpConfig(ILogger<XpConfig> logger) : MewdekoSlashSubmodule<XpService>
    {
        /// <summary>
        ///     Sets the amount of XP gained per message.
        /// </summary>
        /// <param name="amount">The amount of XP to award per message.</param>
        [SlashCommand("message-xp", "Sets the amount of XP gained per message")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task SetMessageXp([Summary("amount", "XP awarded per message")] int amount)
        {
            switch (amount)
            {
                case <= 0:
                    await ReplyErrorAsync(Strings.XpAmountPositive(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case > XpService.MaxXpPerMessage:
                    await ReplyErrorAsync(Strings.XpMessageTooHigh(ctx.Guild.Id, XpService.MaxXpPerMessage))
                        .ConfigureAwait(false);
                    return;
                default:
                    await Service.UpdateGuildXpSettingsAsync(ctx.Guild.Id, settings => settings.XpPerMessage = amount);
                    await ReplyConfirmAsync(Strings.XpMessageSet(ctx.Guild.Id, amount)).ConfigureAwait(false);
                    break;
            }
        }

        /// <summary>
        ///     Sets the cooldown between message XP gains.
        /// </summary>
        /// <param name="seconds">The cooldown in seconds.</param>
        [SlashCommand("cooldown", "Sets the cooldown between message XP gains in seconds")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task SetXpCooldown([Summary("seconds", "Cooldown in seconds, 0 to disable")] int seconds)
        {
            if (seconds < 0)
            {
                await ReplyErrorAsync(Strings.XpCooldownNotNegative(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.UpdateGuildXpSettingsAsync(ctx.Guild.Id, settings => settings.MessageXpCooldown = seconds);

            if (seconds == 0)
                await ReplyConfirmAsync(Strings.XpCooldownDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.XpCooldownSet(ctx.Guild.Id, seconds)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the amount of XP gained per minute in voice channels.
        /// </summary>
        /// <param name="amount">The amount of XP to award per minute in voice.</param>
        [SlashCommand("voice-xp", "Sets the amount of XP gained per minute in voice channels")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task SetVoiceXp([Summary("amount", "XP awarded per minute in voice, 0 to disable")] int amount)
        {
            switch (amount)
            {
                case < 0:
                    await ReplyErrorAsync(Strings.XpAmountNotNegative(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case > XpService.MaxVoiceXpPerMinute:
                    await ReplyErrorAsync(Strings.XpVoiceTooHigh(ctx.Guild.Id, XpService.MaxVoiceXpPerMinute))
                        .ConfigureAwait(false);
                    return;
            }

            await Service.UpdateGuildXpSettingsAsync(ctx.Guild.Id, settings => settings.VoiceXpPerMinute = amount);

            if (amount == 0)
                await ReplyConfirmAsync(Strings.XpVoiceDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.XpVoiceSet(ctx.Guild.Id, amount)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the voice XP timeout (how long a user must be in voice to gain XP).
        /// </summary>
        /// <param name="minutes">The timeout in minutes.</param>
        [SlashCommand("voice-timeout", "Sets how long a user must be in voice before gaining XP")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task SetVoiceTimeout([Summary("minutes", "Timeout in minutes")] int minutes)
        {
            if (minutes <= 0)
            {
                await ReplyErrorAsync(Strings.XpTimeoutPositive(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.UpdateGuildXpSettingsAsync(ctx.Guild.Id, settings => settings.VoiceXpTimeout = minutes);
            await ReplyConfirmAsync(Strings.XpVoiceTimeoutSet(ctx.Guild.Id, minutes)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the server-wide XP multiplier.
        /// </summary>
        /// <param name="multiplier">The XP multiplier value.</param>
        [SlashCommand("multiplier", "Sets the server-wide XP multiplier")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task SetXpMultiplier(
            [Summary("multiplier", "The multiplier value, for example 1.5")]
            double multiplier)
        {
            if (multiplier <= 0)
            {
                await ReplyErrorAsync(Strings.XpMultiplierPositive(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.UpdateGuildXpSettingsAsync(ctx.Guild.Id, settings => settings.XpMultiplier = multiplier);
            await ReplyConfirmAsync(Strings.XpMultiplierSet(ctx.Guild.Id, multiplier)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the XP curve type used for level calculations.
        /// </summary>
        /// <param name="type">The XP curve type.</param>
        /// <param name="recompute">Whether to recompute all user levels (defaults to true).</param>
        [SlashCommand("curve", "Sets the XP curve type used for level calculations")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task SetXpCurve([Summary("type", "The XP curve type")] XpCurveType type,
            [Summary("recompute", "Recompute all user levels after changing the curve")]
            bool recompute = true)
        {
            await Service.UpdateGuildXpSettingsAsync(ctx.Guild.Id, settings => settings.XpCurveType = (int)type);

            if (!recompute)
            {
                await ReplyConfirmAsync(Strings.XpCurveSet(ctx.Guild.Id, type)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.XpCurveRecomputeStarted(ctx.Guild.Id, type)).ConfigureAwait(false);

            _ = Task.Run(async () =>
            {
                try
                {
                    await Service.RecomputeAllLevelsAsync(ctx.Guild.Id, type);

                    var channel = await ctx.Guild.GetTextChannelAsync(ctx.Channel.Id);
                    if (channel != null)
                    {
                        await channel.SendConfirmAsync(Strings.XpCurveRecomputeComplete(ctx.Guild.Id, type));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error recomputing levels for guild {GuildId}", ctx.Guild.Id);

                    var channel = await ctx.Guild.GetTextChannelAsync(ctx.Channel.Id);
                    if (channel != null)
                    {
                        await channel.SendErrorAsync(Strings.XpCurveRecomputeError(ctx.Guild.Id), Config);
                    }
                }
            });
        }

        /// <summary>
        ///     Sets whether role rewards are exclusive (only highest level role) or additive (all earned roles).
        /// </summary>
        /// <param name="exclusive">Whether role rewards should be exclusive.</param>
        [SlashCommand("exclusive", "Sets whether users keep only the highest level role or all earned roles")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task SetXpExclusive(
            [Summary("exclusive", "True to keep only the highest level role")]
            bool exclusive)
        {
            await Service.UpdateGuildXpSettingsAsync(ctx.Guild.Id,
                settings => settings.ExclusiveRoleRewards = exclusive);

            if (exclusive)
                await ReplyConfirmAsync(Strings.XpExclusiveEnabled(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.XpExclusiveDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets an XP multiplier for a channel.
        /// </summary>
        /// <param name="channel">The channel to set the multiplier for.</param>
        /// <param name="multiplier">The multiplier value.</param>
        [SlashCommand("channel-xp", "Sets an XP multiplier for a channel, 1.0 resets it")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task SetChannelXp(
            [Summary("channel", "The channel to set the multiplier for")]
            ITextChannel channel,
            [Summary("multiplier", "The multiplier value, 1.0 resets to default")]
            double multiplier)
        {
            if (multiplier <= 0)
            {
                await ReplyErrorAsync(Strings.XpMultiplierPositive(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetChannelMultiplierAsync(ctx.Guild.Id, channel.Id, multiplier);

            if (multiplier == 1.0)
                await ReplyConfirmAsync(Strings.XpChannelMultiplierReset(ctx.Guild.Id, channel.Mention))
                    .ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.XpChannelMultiplierSet(ctx.Guild.Id, channel.Mention, multiplier))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets an XP multiplier for a role.
        /// </summary>
        /// <param name="role">The role to set the multiplier for.</param>
        /// <param name="multiplier">The multiplier value.</param>
        [SlashCommand("role-xp", "Sets an XP multiplier for a role, 1.0 resets it")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task SetRoleXp([Summary("role", "The role to set the multiplier for")] IRole role,
            [Summary("multiplier", "The multiplier value, 1.0 resets to default")]
            double multiplier)
        {
            if (multiplier <= 0)
            {
                await ReplyErrorAsync(Strings.XpMultiplierPositive(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetRoleMultiplierAsync(ctx.Guild.Id, role.Id, multiplier);

            if (multiplier == 1.0)
                await ReplyConfirmAsync(Strings.XpRoleMultiplierReset(ctx.Guild.Id, role.Mention))
                    .ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.XpRoleMultiplierSet(ctx.Guild.Id, role.Mention, multiplier))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets how you are notified when you level up.
        /// </summary>
        /// <param name="type">The notification type: None, Channel, or Dm.</param>
        [SlashCommand("level-notif", "Sets how you are notified when you level up")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task LevelNotif(
            [Summary("type", "Where to send your level-up notifications")]
            XpNotificationType type)
        {
            await Service.SetUserNotificationPreferenceAsync(ctx.Guild.Id, ctx.User.Id, type);
            await ReplyConfirmAsync(Strings.XpNotificationSet(ctx.Guild.Id, type)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Level reward management commands.
    /// </summary>
    [Group("rewards", "Manage role and currency rewards for levels")]
    public class XpRewards(ICurrencyService currencyService, XpRewardManager xpRewardManager)
        : MewdekoSlashSubmodule<XpService>
    {
        /// <summary>
        ///     Sets a role reward for a specific level.
        /// </summary>
        /// <param name="level">The level that triggers the reward.</param>
        /// <param name="role">The role to award.</param>
        [SlashCommand("role-add", "Sets a role reward for a specific level")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task RoleReward([Summary("level", "The level that grants the role")] int level,
            [Summary("role", "The role to award")] IRole role)
        {
            if (level <= 0)
            {
                await ReplyErrorAsync(Strings.XpLevelPositive(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (role.Position >= ((IGuildUser)ctx.User).GetRoles().Max(r => r.Position))
            {
                await ReplyErrorAsync(Strings.XpRoleHierarchy(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await xpRewardManager.SetRoleRewardAsync(ctx.Guild.Id, level, role.Id);
            await ReplyConfirmAsync(Strings.XpRoleRewardSet(ctx.Guild.Id, level, role.Mention)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes a role reward for a specific level.
        /// </summary>
        /// <param name="level">The level to remove the reward from.</param>
        [SlashCommand("role-remove", "Removes the role reward for a specific level")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task RemoveRoleReward([Summary("level", "The level to remove the reward from")] int level)
        {
            if (level <= 0)
            {
                await ReplyErrorAsync(Strings.XpLevelPositive(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await xpRewardManager.SetRoleRewardAsync(ctx.Guild.Id, level, null);
            await ReplyConfirmAsync(Strings.XpRoleRewardRemoved(ctx.Guild.Id, level)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists all role rewards.
        /// </summary>
        [SlashCommand("role-list", "Lists all level role rewards")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task RoleRewards()
        {
            var rewards = await Service.GetRoleRewardsAsync(ctx.Guild.Id);

            if (rewards.Count == 0)
            {
                await ReplyErrorAsync(Strings.XpNoRoleRewards(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.XpRoleRewardsTitle(ctx.Guild.Id));

            var rewardLines = new List<string>();

            foreach (var reward in rewards.OrderBy(r => r.Level))
            {
                var role = ctx.Guild.GetRole(reward.RoleId);
                if (role != null)
                    rewardLines.Add(Strings.XpRoleRewardLine(ctx.Guild.Id, reward.Level, role.Mention));
            }

            embed.WithDescription(string.Join("\n", rewardLines));
            await ctx.Interaction.RespondAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Sets a currency reward for a specific level.
        /// </summary>
        /// <param name="level">The level that triggers the reward.</param>
        /// <param name="amount">The amount of currency to award.</param>
        [SlashCommand("currency-add", "Sets a currency reward for a specific level")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task CurrencyReward([Summary("level", "The level that grants the currency")] int level,
            [Summary("amount", "The amount of currency to award")]
            long amount)
        {
            if (level <= 0)
            {
                await ReplyErrorAsync(Strings.XpLevelPositive(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (amount <= 0)
            {
                await ReplyErrorAsync(Strings.XpAmountPositive(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await xpRewardManager.SetCurrencyRewardAsync(ctx.Guild.Id, level, amount);

            await ReplyConfirmAsync(Strings.XpCurrencyRewardSet(
                ctx.Guild.Id,
                level,
                amount,
                await currencyService.GetCurrencyEmote(ctx.Guild.Id)
            )).ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes a currency reward for a specific level.
        /// </summary>
        /// <param name="level">The level to remove the reward from.</param>
        [SlashCommand("currency-remove", "Removes the currency reward for a specific level")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task RemoveCurrencyReward([Summary("level", "The level to remove the reward from")] int level)
        {
            if (level <= 0)
            {
                await ReplyErrorAsync(Strings.XpLevelPositive(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await xpRewardManager.SetCurrencyRewardAsync(ctx.Guild.Id, level, 0);
            await ReplyConfirmAsync(Strings.XpCurrencyRewardRemoved(ctx.Guild.Id, level)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists all currency rewards.
        /// </summary>
        [SlashCommand("currency-list", "Lists all level currency rewards")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task CurrencyRewards()
        {
            var rewards = await Service.GetCurrencyRewardsAsync(ctx.Guild.Id);

            if (rewards.Count == 0)
            {
                await ReplyErrorAsync(Strings.XpNoCurrencyRewards(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.XpCurrencyRewardsTitle(ctx.Guild.Id));

            var rewardLines = new List<string>();
            var currencyName = await currencyService.GetCurrencyEmote(ctx.Guild.Id);

            foreach (var reward in rewards.OrderBy(r => r.Level))
            {
                rewardLines.Add(Strings.XpCurrencyRewardLine(ctx.Guild.Id, reward.Level, reward.Amount, currencyName));
            }

            embed.WithDescription(string.Join("\n", rewardLines));
            await ctx.Interaction.RespondAsync(embed: embed.Build());
        }
    }
}