using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Counting.Common;
using Mewdeko.Modules.Counting.Services;
using Swan;

namespace Mewdeko.Modules.Counting;

/// <summary>
/// Slash command module for managing counting channels and functionality.
/// </summary>
/// <param name="interactive">The interactive service for handling user interactions.</param>
/// <param name="countingService">The main counting service.</param>
/// <param name="statsService">The counting statistics service.</param>
/// <param name="moderationService">The counting moderation service.</param>
[Group("counting", "Commands for managing counting channels")]
public partial class SlashCounting(
    InteractiveService interactive,
    CountingService countingService,
    CountingStatsService statsService,
    CountingModerationService moderationService)
    : MewdekoSlashModuleBase<CountingService>
{
    /// <summary>
    /// Sets up counting in the current channel or a specified channel.
    /// </summary>
    /// <param name="channel">The channel to set up counting in. Defaults to current channel.</param>
    /// <param name="startNumber">The number to start counting from. Defaults to 1.</param>
    /// <param name="increment">The increment for each count. Defaults to 1.</param>
    [SlashCommand("setup", "Set up counting in a channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task CountingSetup(
        [Summary("channel", "Channel to set up counting in")]
        ITextChannel? channel = null,
        [Summary("start", "Number to start counting from")]
        long startNumber = 1,
        [Summary("increment", "Increment for each count")]
        int increment = 1)
    {
        await DeferAsync();

        channel ??= (ITextChannel)ctx.Channel;

        if (increment <= 0)
        {
            await ErrorAsync(Strings.CountingInvalidIncrement(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var result = await countingService.SetupCountingChannelAsync(ctx.Guild.Id, channel.Id, startNumber, increment);

        if (!result.Success)
        {
            await ErrorAsync(Strings.CountingSetupFailed(ctx.Guild.Id, result.ErrorMessage ?? "Unknown error"))
                .ConfigureAwait(false);
            return;
        }

        await ConfirmAsync(Strings.CountingSetupSuccess(ctx.Guild.Id, channel.Mention, startNumber, increment))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Shows the current status of counting in a channel.
    /// </summary>
    /// <param name="channel">The channel to check. Defaults to current channel.</param>
    [SlashCommand("status", "Show the current status of counting in a channel")]
    [RequireContext(ContextType.Guild)]
    public async Task CountingStatus(
        [Summary("channel", "Channel to check status for")]
        ITextChannel? channel = null)
    {
        await DeferAsync();

        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await countingService.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            return;
        }

        var config = await countingService.GetCountingConfigAsync(channel.Id);
        var stats = await countingService.GetChannelStatsAsync(channel.Id);

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.CountingStatusTitle(ctx.Guild.Id, channel.Name))
            .AddField(Strings.CountingCurrentNumber(ctx.Guild.Id), countingChannel.CurrentNumber.ToString("N0"), true)
            .AddField(Strings.CountingHighestNumber(ctx.Guild.Id), $"{countingChannel.HighestNumber:N0}", true)
            .AddField(Strings.CountingTotalCounts(ctx.Guild.Id), countingChannel.TotalCounts.ToString("N0"), true)
            .AddField(Strings.CountingParticipants(ctx.Guild.Id), stats?.TotalParticipants.ToString("N0") ?? "0", true)
            .AddField(Strings.CountingPattern(ctx.Guild.Id), ((CountingPattern)(config?.Pattern ?? 0)).ToString(), true)
            .AddField(Strings.CountingIncrement(ctx.Guild.Id), countingChannel.Increment.ToString(), true);

        if (countingChannel.LastUserId > 0)
        {
            embed.AddField(Strings.CountingLastUser(ctx.Guild.Id), $"<@{countingChannel.LastUserId}>", true);
        }

        if (config != null)
        {
            var configText = new List<string>();
            if (!config.AllowRepeatedUsers) configText.Add(Strings.CountingNoRepeats(ctx.Guild.Id));
            if (config.Cooldown > 0) configText.Add(Strings.CountingCooldown(ctx.Guild.Id, config.Cooldown));
            if (config.MaxNumber > 0)
                configText.Add(Strings.CountingMaxNumber(ctx.Guild.Id, config.MaxNumber.ToString("N0")));
            if (config.ResetOnError) configText.Add(Strings.CountingResetOnError(ctx.Guild.Id));
            if (config.DeleteWrongMessages) configText.Add(Strings.CountingDeleteWrongMessages(ctx.Guild.Id));

            if (configText.Any())
            {
                embed.AddField(Strings.CountingConfiguration(ctx.Guild.Id), string.Join("\n", configText));
            }
        }

        await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    /// Shows configuration settings for a counting channel.
    /// </summary>
    /// <param name="channel">The channel to show configuration for.</param>
    [SlashCommand("config", "Show configuration settings for a counting channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task CountingConfig(
        [Summary("channel", "Channel to show config for")]
        ITextChannel? channel = null)
    {
        await DeferAsync();

        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await countingService.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            return;
        }

        var config = await countingService.GetCountingConfigAsync(channel.Id);
        if (config == null)
        {
            await ErrorAsync(Strings.CountingConfigNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.CountingConfigTitle(ctx.Guild.Id, channel.Name))
            .AddField(Strings.CountingAllowRepeats(ctx.Guild.Id), config.AllowRepeatedUsers ? "✅" : "❌", true)
            .AddField(Strings.CountingCooldownSeconds(ctx.Guild.Id), config.Cooldown.ToString(), true)
            .AddField(Strings.CountingMaxNumberLimit(ctx.Guild.Id),
                config.MaxNumber > 0 ? config.MaxNumber.ToString("N0") : Strings.CountingUnlimited(ctx.Guild.Id), true)
            .AddField(Strings.CountingResetOnErrorSetting(ctx.Guild.Id), config.ResetOnError ? "✅" : "❌", true)
            .AddField(Strings.CountingDeleteWrongSetting(ctx.Guild.Id), config.DeleteWrongMessages ? "✅" : "❌", true)
            .AddField(Strings.CountingPatternType(ctx.Guild.Id), ((CountingPattern)config.Pattern).ToString(), true)
            .AddField(Strings.CountingNumberBaseSetting(ctx.Guild.Id), config.NumberBase.ToString(), true)
            .AddField(Strings.CountingAchievementsSetting(ctx.Guild.Id), config.EnableAchievements ? "✅" : "❌", true)
            .AddField(Strings.CountingCompetitionsSetting(ctx.Guild.Id), config.EnableCompetitions ? "✅" : "❌", true);

        if (!string.IsNullOrEmpty(config.SuccessEmote))
        {
            embed.AddField(Strings.CountingSuccessEmote(ctx.Guild.Id), config.SuccessEmote, true);
        }

        if (!string.IsNullOrEmpty(config.ErrorEmote))
        {
            embed.AddField(Strings.CountingErrorEmote(ctx.Guild.Id), config.ErrorEmote, true);
        }

        await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    /// Resets the counting in a channel to a specific number.
    /// </summary>
    /// <param name="channel">The channel to reset.</param>
    /// <param name="newNumber">The number to reset to. Defaults to 0.</param>
    /// <param name="reason">The reason for the reset.</param>
    [SlashCommand("reset", "Reset the counting in a channel to a specific number")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task CountingReset(
        [Summary("channel", "Channel to reset")]
        ITextChannel? channel = null,
        [Summary("number", "Number to reset to")]
        long newNumber = 0,
        [Summary("reason", "Reason for the reset")]
        string? reason = null)
    {
        await DeferAsync();

        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await countingService.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            return;
        }

        var confirmEmbed = new EmbedBuilder()
            .WithErrorColor()
            .WithTitle(Strings.CountingResetConfirmTitle(ctx.Guild.Id))
            .WithDescription(Strings.CountingResetConfirm(ctx.Guild.Id, channel.Mention, countingChannel.CurrentNumber,
                newNumber))
            .Build();

        var component = new ComponentBuilder()
            .WithButton(Strings.Confirm(ctx.Guild.Id), "counting_reset_yes", ButtonStyle.Danger)
            .WithButton(Strings.Cancel(ctx.Guild.Id), "counting_reset_no", ButtonStyle.Secondary)
            .Build();

        var msg = await FollowupAsync(embed: confirmEmbed, components: component).ConfigureAwait(false);

        var response = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id);

        if (response is null or "counting_reset_no")
        {
            await msg.ModifyAsync(x =>
            {
                x.Content = Strings.CountingResetCancelled(ctx.Guild.Id);
                x.Embed = null;
                x.Components = null;
            }).ConfigureAwait(false);
            return;
        }

        var success = await countingService.ResetCountingChannelAsync(channel.Id, newNumber, ctx.User.Id, reason);

        await msg.ModifyAsync(x =>
        {
            x.Content = success
                ? Strings.CountingResetSuccess(ctx.Guild.Id, channel.Mention, newNumber)
                : Strings.CountingResetFailed(ctx.Guild.Id);
            x.Embed = null;
            x.Components = null;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Shows counting statistics for a user or channel.
    /// </summary>
    /// <param name="user">The user to show stats for. Defaults to the command invoker.</param>
    /// <param name="channel">The channel to check stats in. Defaults to current channel.</param>
    [SlashCommand("stats", "Show counting statistics for a user")]
    [RequireContext(ContextType.Guild)]
    public async Task CountingStats(
        [Summary("user", "User to show stats for")]
        IGuildUser? user = null,
        [Summary("channel", "Channel to check stats in")]
        ITextChannel? channel = null)
    {
        await DeferAsync();

        user ??= (IGuildUser)ctx.User;
        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await countingService.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            return;
        }

        var userStats = await statsService.GetUserStatsAsync(channel.Id, user.Id);
        if (userStats == null)
        {
            await ErrorAsync(Strings.CountingNoUserStats(ctx.Guild.Id, user.DisplayName, channel.Mention))
                .ConfigureAwait(false);
            return;
        }

        var rank = await statsService.GetUserRankAsync(channel.Id, user.Id);

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.CountingUserStatsTitle(ctx.Guild.Id, user.DisplayName, channel.Name))
            .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
            .AddField(Strings.CountingRank(ctx.Guild.Id), rank?.ToString() ?? "N/A", true)
            .AddField(Strings.CountingContributions(ctx.Guild.Id), userStats.ContributionsCount.ToString("N0"), true)
            .AddField(Strings.CountingCurrentStreak(ctx.Guild.Id), userStats.CurrentStreak.ToString("N0"), true)
            .AddField(Strings.CountingHighestStreak(ctx.Guild.Id), userStats.HighestStreak.ToString("N0"), true)
            .AddField(Strings.CountingAccuracy(ctx.Guild.Id), $"{userStats.Accuracy:F1}%", true)
            .AddField(Strings.CountingErrors(ctx.Guild.Id), userStats.ErrorsCount.ToString("N0"), true)
            .AddField(Strings.CountingTotalNumbersCounted(ctx.Guild.Id), userStats.TotalNumbersCounted.ToString("N0"),
                true);

        if (userStats.LastContribution.HasValue)
        {
            embed.AddField(Strings.CountingLastContribution(ctx.Guild.Id),
                $"<t:{((DateTimeOffset)userStats.LastContribution.Value).ToUnixTimeSeconds()}:R>", true);
        }

        await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    /// Shows the leaderboard for a counting channel.
    /// </summary>
    /// <param name="type">The type of leaderboard to show.</param>
    /// <param name="channel">The channel to show the leaderboard for.</param>
    [SlashCommand("leaderboard", "Show the leaderboard for a counting channel")]
    [RequireContext(ContextType.Guild)]
    public async Task CountingLeaderboard(
        [Summary("type", "Type of leaderboard to show")]
        [Choice("Contributions", "contributions")]
        [Choice("Highest Streak", "streak")]
        [Choice("Accuracy", "accuracy")]
        [Choice("Total Numbers", "totalnumbers")]
        string type = "contributions",
        [Summary("channel", "Channel to show leaderboard for")]
        ITextChannel? channel = null)
    {
        await DeferAsync();

        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await countingService.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            return;
        }

        if (!Enum.TryParse<LeaderboardType>(type, true, out var leaderboardType))
        {
            await ErrorAsync(Strings.CountingInvalidLeaderboardType(ctx.Guild.Id,
                string.Join(", ", Enum.GetNames<LeaderboardType>()))).ConfigureAwait(false);
            return;
        }

        var leaderboard = await statsService.GetLeaderboardAsync(channel.Id, leaderboardType, 20);
        if (!leaderboard.Any())
        {
            await ErrorAsync(Strings.CountingNoLeaderboardData(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            return;
        }

        var pages = leaderboard
            .Chunk(10)
            .Select<CountingLeaderboardEntry[], PageBuilder>((chunk, pageIndex) =>
            {
                var description = new List<string>();
                foreach (var entry in chunk)
                {
                    var value = leaderboardType switch
                    {
                        LeaderboardType.Contributions => entry.ContributionsCount.ToString("N0"),
                        LeaderboardType.Streak => entry.HighestStreak.ToString("N0"),
                        LeaderboardType.Accuracy => $"{entry.Accuracy:F1}%",
                        LeaderboardType.TotalNumbers => entry.TotalNumbersCounted.ToString("N0"),
                        _ => entry.ContributionsCount.ToString("N0")
                    };

                    var rankEmoji = entry.Rank switch
                    {
                        1 => "🥇",
                        2 => "🥈",
                        3 => "🥉",
                        _ => $"`{entry.Rank}.`"
                    };

                    description.Add($"{rankEmoji} <@{entry.UserId}> - **{value}**");
                }

                return new PageBuilder()
                    .WithOkColor()
                    .WithTitle(Strings.CountingLeaderboardTitle(ctx.Guild.Id, channel.Name, leaderboardType.ToString()))
                    .WithDescription(string.Join("\n", description))
                    .WithFooter(Strings.CountingLeaderboardPage(ctx.Guild.Id, pageIndex + 1,
                        (leaderboard.Count + 9) / 10));
            })
            .ToList();

        var paginator = new StaticPaginatorBuilder()
            .WithUsers(ctx.User)
            .WithPages(pages)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .WithActionOnTimeout(ActionOnStop.DeleteMessage)
            .Build();

        await interactive.SendPaginatorAsync(paginator, Context.Interaction,
            responseType: InteractionResponseType.DeferredChannelMessageWithSource).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a save point for the current counting progress.
    /// </summary>
    /// <param name="channel">The channel to save.</param>
    /// <param name="reason">The reason for creating the save point.</param>
    [SlashCommand("save", "Create a save point for the current counting progress")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task CountingSave(
        [Summary("channel", "Channel to save")]
        ITextChannel? channel = null,
        [Summary("reason", "Reason for creating save point")]
        string? reason = null)
    {
        await DeferAsync();

        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await countingService.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            return;
        }

        var saveId = await countingService.CreateSavePointAsync(channel.Id, ctx.User.Id, reason);
        if (saveId > 0)
        {
            await ConfirmAsync(Strings.CountingSaveSuccess(ctx.Guild.Id, channel.Mention, countingChannel.CurrentNumber,
                saveId)).ConfigureAwait(false);
        }
        else
        {
            await ErrorAsync(Strings.CountingSaveFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Restores counting from a previous save point.
    /// </summary>
    /// <param name="saveId">The ID of the save point to restore from.</param>
    /// <param name="channel">The channel to restore.</param>
    [SlashCommand("restore", "Restore counting from a previous save point")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task CountingRestore(
        [Summary("saveid", "ID of save point to restore from")]
        int saveId,
        [Summary("channel", "Channel to restore")]
        ITextChannel? channel = null)
    {
        await DeferAsync();

        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await countingService.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            return;
        }

        var confirmEmbed = new EmbedBuilder()
            .WithErrorColor()
            .WithTitle(Strings.CountingRestoreConfirmTitle(ctx.Guild.Id))
            .WithDescription(Strings.CountingRestoreConfirm(ctx.Guild.Id, channel.Mention, saveId,
                countingChannel.CurrentNumber))
            .Build();

        var component = new ComponentBuilder()
            .WithButton(Strings.Confirm(ctx.Guild.Id), "counting_restore_yes", ButtonStyle.Danger)
            .WithButton(Strings.Cancel(ctx.Guild.Id), "counting_restore_no", ButtonStyle.Secondary)
            .Build();

        var msg = await FollowupAsync(embed: confirmEmbed, components: component).ConfigureAwait(false);

        var response = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id);

        if (response is null or "counting_restore_no")
        {
            await msg.ModifyAsync(x =>
            {
                x.Content = Strings.CountingRestoreCancelled(ctx.Guild.Id);
                x.Embed = null;
                x.Components = null;
            }).ConfigureAwait(false);
            return;
        }

        var success = await countingService.RestoreFromSaveAsync(channel.Id, saveId, ctx.User.Id);

        await msg.ModifyAsync(x =>
        {
            x.Content = success
                ? Strings.CountingRestoreSuccess(ctx.Guild.Id, channel.Mention, saveId)
                : Strings.CountingRestoreFailed(ctx.Guild.Id, saveId);
            x.Embed = null;
            x.Components = null;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Disables counting in a channel and optionally purges all data.
    /// </summary>
    /// <param name="channel">The channel to disable counting in. Defaults to current channel.</param>
    /// <param name="purgeData">Whether to delete all counting data for this channel.</param>
    /// <param name="reason">The reason for disabling counting.</param>
    [SlashCommand("disable", "Disable counting in a channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task CountingDisable(ITextChannel? channel = null, bool purgeData = false, string? reason = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await Service.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention));
            return;
        }

        var success = await Service.DisableCountingChannelAsync(channel.Id, ctx.User.Id, reason);

        if (!success)
        {
            await ErrorAsync(Strings.CountingDisableFailed(ctx.Guild.Id));
            return;
        }

        if (purgeData)
        {
            var purgeSuccess = await Service.PurgeCountingChannelAsync(channel.Id, ctx.User.Id, reason);
            if (purgeSuccess)
            {
                await ConfirmAsync(Strings.CountingPurged(ctx.Guild.Id, channel.Mention));
            }
            else
            {
                await ErrorAsync(Strings.CountingPurgeFailed(ctx.Guild.Id));
            }
        }
        else
        {
            await ConfirmAsync(Strings.CountingDisabled(ctx.Guild.Id, channel.Mention));
        }
    }

    /// <summary>
    ///     Bans a user from counting in a channel.
    /// </summary>
    /// <param name="user">The user to ban from counting.</param>
    /// <param name="channel">The counting channel.</param>
    /// <param name="duration">Duration in minutes (0 for permanent).</param>
    /// <param name="reason">The reason for the ban.</param>
    [SlashCommand("ban", "Ban a user from counting")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task CountingBan(IGuildUser user, ITextChannel? channel = null, int duration = 0,
        string? reason = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await Service.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention));
            return;
        }

        TimeSpan? banDuration = duration > 0 ? TimeSpan.FromMinutes(duration) : null;
        var success =
            await moderationService.BanUserFromCountingAsync(channel.Id, user.Id, ctx.User.Id, banDuration, reason);

        if (!success)
        {
            await ErrorAsync(Strings.CountingBanFailed(ctx.Guild.Id));
            return;
        }

        var durationText = banDuration != null
            ? Strings.CountingBanDuration(ctx.Guild.Id, banDuration.Humanize())
            : Strings.CountingBanPermanent(ctx.Guild.Id);
        await ConfirmAsync(Strings.CountingUserBanned(ctx.Guild.Id, user.Mention, channel.Mention, durationText));
    }

    /// <summary>
    ///     Unbans a user from counting in a channel.
    /// </summary>
    /// <param name="user">The user to unban from counting.</param>
    /// <param name="channel">The counting channel.</param>
    /// <param name="reason">The reason for the unban.</param>
    [SlashCommand("unban", "Unban a user from counting")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task CountingUnban(IGuildUser user, ITextChannel? channel = null, string? reason = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await Service.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention));
            return;
        }

        var success = await moderationService.UnbanUserFromCountingAsync(channel.Id, user.Id, ctx.User.Id, reason);

        if (!success)
        {
            await ErrorAsync(Strings.CountingUnbanFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.CountingUserUnbanned(ctx.Guild.Id, user.Mention, channel.Mention));
    }

    /// <summary>
    ///     Temporarily timeouts a user from counting.
    /// </summary>
    /// <param name="user">The user to timeout from counting.</param>
    /// <param name="duration">Duration of the timeout (e.g., 30m, 2h).</param>
    /// <param name="channel">The counting channel. Defaults to current channel.</param>
    /// <param name="reason">The reason for the timeout.</param>
    [SlashCommand("timeout", "Temporarily timeout a user from counting")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task CountingTimeout(
        [Summary("user", "User to timeout from counting")]
        IGuildUser user,
        [Summary("duration", "Duration of the timeout, e.g. 30m or 2h")]
        TimeSpan duration,
        [Summary("channel", "Counting channel, defaults to current")]
        ITextChannel? channel = null,
        [Summary("reason", "Reason for the timeout")]
        string? reason = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var countingChannel = await Service.GetCountingChannelAsync(channel.Id);
        if (countingChannel == null)
        {
            await ErrorAsync(Strings.CountingNotSetup(ctx.Guild.Id, channel.Mention));
            return;
        }

        var success =
            await moderationService.BanUserFromCountingAsync(channel.Id, user.Id, ctx.User.Id, duration, reason);

        if (!success)
        {
            await ErrorAsync(Strings.CountingTimeoutFailed(ctx.Guild.Id));
            return;
        }

        await ConfirmAsync(Strings.CountingUserTimedOut(ctx.Guild.Id, user.Mention, channel.Mention,
            duration.Humanize()));
    }

    /// <summary>
    /// Lists all active counting channels in the server.
    /// </summary>
    [SlashCommand("list", "List all counting channels in the server")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task CountingList()
    {
        await DeferAsync();

        var channels = await countingService.GetGuildCountingChannelsAsync(ctx.Guild.Id);
        if (!channels.Any())
        {
            await ErrorAsync(Strings.CountingNoChannelsInGuild(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.CountingChannelsTitle(ctx.Guild.Id, ctx.Guild.Name))
            .WithDescription(Strings.CountingChannelsCount(ctx.Guild.Id, channels.Count));

        foreach (var channel in channels.Take(10))
        {
            var channelObj = await ctx.Guild.GetTextChannelAsync(channel.ChannelId);
            var channelName = channelObj?.Name ?? Strings.CountingDeletedChannel(ctx.Guild.Id);
            var channelMention = channelObj?.Mention ?? $"#{channelName}";

            embed.AddField($"{channelMention}",
                Strings.CountingChannelInfo(ctx.Guild.Id, channel.CurrentNumber.ToString("N0"),
                    channel.TotalCounts.ToString("N0"), channel.HighestNumber.ToString("N0")),
                true);
        }

        if (channels.Count > 10)
        {
            embed.WithFooter(Strings.CountingMoreChannels(ctx.Guild.Id, channels.Count - 10));
        }

        await FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
    }
}