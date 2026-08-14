using System.Text;
using Discord.Commands;
using Discord.Net;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Xp.Extensions;
using Mewdeko.Modules.Xp.Models;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Xp;

/// <summary>
///     Module for managing XP system functionality.
/// </summary>
public class Xp(
    InteractiveService serv,
    ICurrencyService currencyService,
    XpRoleSyncService roleSyncService,
    ILogger<Xp> logger,
    DiscordShardedClient client,
    XpRewardManager xpRewardManager)
    : MewdekoModuleBase<XpService>
{
    /// <summary>
    ///     Shows a user's XP card with their rank, level, and progress.
    /// </summary>
    /// <param name="user">The user to show the XP card for. If not specified, shows the caller's card.</param>
    /// <example>.rank</example>
    /// <example>.rank @user</example>
    [Cmd]
    [Aliases]
    public async Task Rank([Remainder] IGuildUser? user = null)
    {
        user ??= (IGuildUser)ctx.User;

        // Prevent showing ranks for bots
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
                // Check if user has any XP
                case 0 when user.Id == ctx.User.Id:
                    await ReplyErrorAsync(Strings.XpYouNoXp(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case 0:
                    await ReplyErrorAsync(Strings.XpUserNoXp(ctx.Guild.Id, user.ToString())).ConfigureAwait(false);
                    return;
                default:
                {
                    // Generate XP card
                    var cardPath = await Service.GenerateXpCardAsync(ctx.Guild.Id, user.Id);
                    await ctx.Channel.SendFileAsync(cardPath, "xp.png",
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
    /// <example>.textrank</example>
    /// <example>.textrank @user</example>
    [Cmd]
    [Aliases]
    public async Task TextRank([Remainder] IGuildUser? user = null)
    {
        user ??= (IGuildUser)ctx.User;

        // Prevent showing ranks for bots
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
                // Check if user has any XP
                case 0 when user.Id == ctx.User.Id:
                    await ReplyErrorAsync(Strings.XpYouNoXp(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                case 0:
                    await ReplyErrorAsync(Strings.XpUserNoXp(ctx.Guild.Id, user.ToString())).ConfigureAwait(false);
                    return;
            }

            // Calculate progress percentage
            var progressPercent = (int)((double)stats.LevelXp / stats.RequiredXp * 100);

            // Format the text response in an accessible way
            var response = new StringBuilder();
            response.AppendLine(Strings.TextRankHeader(ctx.Guild.Id, user.ToString()));
            response.AppendLine(Strings.TextRankLevel(ctx.Guild.Id, stats.Level));
            response.AppendLine(Strings.TextRankXp(ctx.Guild.Id, stats.TotalXp));
            response.AppendLine(
                Strings.TextRankProgress(ctx.Guild.Id, stats.LevelXp, stats.RequiredXp, progressPercent));
            response.AppendLine(Strings.TextRankServerPosition(ctx.Guild.Id, stats.Rank));

            // Check if user has bonus XP
            if (stats.BonusXp > 0)
            {
                response.AppendLine(Strings.TextRankBonusXp(ctx.Guild.Id, stats.BonusXp));
            }

            await ReplyAsync(response.ToString()).ConfigureAwait(false);
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
    ///     XP and currency both answer to this command name. When the server has currency in
    ///     circulation a bare invocation asks which one was meant; servers with no economy go straight
    ///     to XP as before, and naming a page always means XP.
    /// </remarks>
    /// <param name="page">Page number to display (starts at 1).</param>
    /// <example>.leaderboard</example>
    /// <example>.leaderboard 2</example>
    [Cmd]
    [Aliases]
    public async Task Leaderboard(int? page = null)
    {
        if (page is null && await GuildHasEconomy())
        {
            var (embed, components) = LeaderboardRenderer.BuildPicker(ctx.Guild.Id, ctx.User.Id, Strings);
            await ctx.Channel.SendMessageAsync(embed: embed, components: components);
            return;
        }

        if (!await LeaderboardRenderer.SendXpAsync(ctx.Guild, ctx.User, ctx.Channel, serv, Service, Strings,
                page ?? 1))
            await ReplyErrorAsync(Strings.XpLeaderboardEmpty(ctx.Guild.Id)).ConfigureAwait(false);
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
    ///     Adds XP to a user.
    /// </summary>
    /// <param name="user">The user to add XP to.</param>
    /// <param name="amount">The amount of XP to add.</param>
    /// <example>.addxp @user 100</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AddXp(IGuildUser user, int amount)
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
    /// <example>.setxp @user 1000</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetXp(IGuildUser user, long amount)
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
    ///     Resets a user's XP to zero.
    /// </summary>
    /// <param name="user">The user to reset XP for.</param>
    /// <param name="resetBonus">Whether to also reset bonus XP. Defaults to false.</param>
    /// <example>.resetxp @user</example>
    /// <example>.resetxp @user true</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task ResetXp(IGuildUser user, bool resetBonus = false)
    {
        await Service.ResetUserXpAsync(ctx.Guild.Id, user.Id, resetBonus);

        if (resetBonus)
            await ReplyConfirmAsync(Strings.XpResetWithBonus(ctx.Guild.Id, user.ToString())).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.XpReset(ctx.Guild.Id, user.ToString())).ConfigureAwait(false);
    }

    /// <summary>
    ///     Resets a user's XP to zero.
    /// </summary>
    /// <example>.resetxp @user</example>
    /// <example>.resetxp @user true</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ResetXp()
    {
        await Service.ResetGuildXp(ctx.Guild.Id, true);
        await ReplyConfirmAsync(Strings.XpResetGuild(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets how users are notified when they level up.
    /// </summary>
    /// <param name="type">The notification type: None, Channel, or DM.</param>
    /// <example>.levelnotif Channel</example>
    [Cmd]
    [Aliases]
    public async Task LevelNotif(XpNotificationType type)
    {
        await Service.SetUserNotificationPreferenceAsync(ctx.Guild.Id, ctx.User.Id, type);
        await ReplyConfirmAsync(Strings.XpNotificationSet(ctx.Guild.Id, type)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows the server's XP settings.
    /// </summary>
    /// <example>.xpsettings</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpSettings()
    {
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

        await ctx.Channel.EmbedAsync(embed);
    }

    /// <summary>
    ///     Sets the amount of XP gained per message.
    /// </summary>
    /// <param name="amount">The amount of XP to award per message.</param>
    /// <example>.setmsgxp 5</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetMessageXp(int amount)
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
    /// <example>.setxpcooldown 60</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetXpCooldown(int seconds)
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
    /// <example>.setvoicexp 2</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetVoiceXp(int amount)
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
    /// <example>.setvoicetimeout 5</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetVoiceTimeout(int minutes)
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
    /// <example>.setxpmultiplier 1.5</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetXpMultiplier(double multiplier)
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
    /// <param name="type">The XP curve type: Standard, Linear, Accelerated, Decelerated, Custom, or Legacy.</param>
    /// <param name="recompute">Whether to recompute all user levels (defaults to false).</param>
    /// <example>.setxpcurve Flat</example>
    /// <example>.setxpcurve Legacy true</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetXpCurve(XpCurveType type, bool recompute = true)
    {
        await Service.UpdateGuildXpSettingsAsync(ctx.Guild.Id, settings => settings.XpCurveType = (int)type);

        if (!recompute)
        {
            await ReplyConfirmAsync(Strings.XpCurveSet(ctx.Guild.Id, type)).ConfigureAwait(false);
            return;
        }

        // Inform user that recomputation is starting
        await ReplyConfirmAsync(Strings.XpCurveRecomputeStarted(ctx.Guild.Id, type)).ConfigureAwait(false);

        // Recompute all levels in background task to avoid blocking
        _ = Task.Run(async () =>
        {
            try
            {
                await Service.RecomputeAllLevelsAsync(ctx.Guild.Id, type);

                // Try to send follow-up message when complete
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
    /// <example>.setxpexclusive true</example>
    /// <example>.setxpexclusive false</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetXpExclusive(bool exclusive)
    {
        await Service.UpdateGuildXpSettingsAsync(ctx.Guild.Id, settings => settings.ExclusiveRoleRewards = exclusive);

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
    /// <example>.setchannelxp #general 2.0</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetChannelXp(ITextChannel channel, double multiplier)
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
    /// <example>.setrolexp "VIP Members" 1.5</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task SetRoleXp(IRole role, double multiplier)
    {
        if (multiplier <= 0)
        {
            await ReplyErrorAsync(Strings.XpMultiplierPositive(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.SetRoleMultiplierAsync(ctx.Guild.Id, role.Id, multiplier);

        if (multiplier == 1.0)
            await ReplyConfirmAsync(Strings.XpRoleMultiplierReset(ctx.Guild.Id, role.Mention)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.XpRoleMultiplierSet(ctx.Guild.Id, role.Mention, multiplier))
                .ConfigureAwait(false);
    }

    /// <summary>
    ///     Creates a temporary XP boost event.
    /// </summary>
    /// <param name="time">How long the boost should last.</param>
    /// <param name="multiplier">The XP multiplier for the boost.</param>
    /// <param name="name">The name of the boost event.</param>
    /// <example>.xpboost 2h 2.0 Weekend XP Event</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpBoost(StoopidTime time, double multiplier, [Remainder] string name)
    {
        if (time.Time.TotalMinutes < 5)
        {
            await ReplyErrorAsync(Strings.XpBoostTooShort(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (multiplier <= 1.0)
        {
            await ReplyErrorAsync(Strings.XpBoostMultiplierTooLow(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            await ReplyErrorAsync(Strings.XpBoostNeedsName(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var startTime = DateTime.UtcNow;
        var endTime = startTime + time.Time;

        var boostEvent = await Service.CreateXpBoostEventAsync(
            ctx.Guild.Id,
            name,
            multiplier,
            startTime,
            endTime
        );

        await ReplyConfirmAsync(Strings.XpBoostCreated(
            ctx.Guild.Id,
            boostEvent.Name,
            boostEvent.Multiplier,
            time.Time.Humanize()
        )).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists active XP boost events.
    /// </summary>
    /// <example>.xpboosts</example>
    [Cmd]
    [Aliases]
    public async Task XpBoosts()
    {
        var boosts = await Service.GetActiveBoostEventsAsync(ctx.Guild.Id);

        if (boosts.Count == 0)
        {
            await ReplyErrorAsync(Strings.XpNoActiveBoosts(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.XpActiveBoostsTitle(ctx.Guild.Id));

        foreach (var boost in boosts)
        {
            var timeLeft = boost.EndTime - DateTime.UtcNow;
            embed.AddField(
                $"{boost.Name} ({boost.Multiplier}x)",
                Strings.XpBoostTimeLeft(ctx.Guild.Id, timeLeft.Humanize())
            );
        }

        await ctx.Channel.EmbedAsync(embed);
    }

    /// <summary>
    ///     Cancels an XP boost event.
    /// </summary>
    /// <param name="eventId">ID of the event to cancel.</param>
    /// <example>.cancelboost 3</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task CancelBoost(int eventId)
    {
        var success = await Service.CancelXpBoostEventAsync(eventId);

        if (success)
            await ReplyConfirmAsync(Strings.XpBoostCancelled(ctx.Guild.Id, eventId)).ConfigureAwait(false);
        else
            await ReplyErrorAsync(Strings.XpBoostNotFound(ctx.Guild.Id, eventId)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Excludes a user from gaining XP.
    /// </summary>
    /// <param name="user">The user to exclude.</param>
    /// <example>.xpexclude @user</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpExclude(IGuildUser user)
    {
        await Service.ExcludeItemAsync(ctx.Guild.Id, user.Id, ExcludedItemType.User);
        await ReplyConfirmAsync(Strings.XpUserExcluded(ctx.Guild.Id, user.ToString())).ConfigureAwait(false);
    }

    /// <summary>
    ///     Excludes a role from gaining XP.
    /// </summary>
    /// <param name="role">The role to exclude.</param>
    /// <example>.xpexclude @role</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpExclude(IRole role)
    {
        await Service.ExcludeItemAsync(ctx.Guild.Id, role.Id, ExcludedItemType.Role);
        await ReplyConfirmAsync(Strings.XpRoleExcluded(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Excludes a channel from gaining XP.
    /// </summary>
    /// <param name="channel">The channel to exclude.</param>
    /// <example>.xpexclude #channel</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpExclude(ITextChannel channel)
    {
        await Service.ExcludeItemAsync(ctx.Guild.Id, channel.Id, ExcludedItemType.Channel);
        await ReplyConfirmAsync(Strings.XpChannelExcluded(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Includes a previously excluded user for XP gain.
    /// </summary>
    /// <param name="user">The user to include.</param>
    /// <example>.xpinclude @user</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpInclude(IGuildUser user)
    {
        await Service.IncludeItemAsync(ctx.Guild.Id, user.Id, ExcludedItemType.User);
        await ReplyConfirmAsync(Strings.XpUserIncluded(ctx.Guild.Id, user.ToString())).ConfigureAwait(false);
    }

    /// <summary>
    ///     Includes a previously excluded role for XP gain.
    /// </summary>
    /// <param name="role">The role to include.</param>
    /// <example>.xpinclude @role</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpInclude(IRole role)
    {
        await Service.IncludeItemAsync(ctx.Guild.Id, role.Id, ExcludedItemType.Role);
        await ReplyConfirmAsync(Strings.XpRoleIncluded(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Includes a previously excluded channel for XP gain.
    /// </summary>
    /// <param name="channel">The channel to include.</param>
    /// <example>.xpinclude #channel</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpInclude(ITextChannel channel)
    {
        await Service.IncludeItemAsync(ctx.Guild.Id, channel.Id, ExcludedItemType.Channel);
        await ReplyConfirmAsync(Strings.XpChannelIncluded(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists excluded users, roles, or channels.
    /// </summary>
    /// <param name="type">The type of exclusions to list: users, roles, or channels.</param>
    /// <example>.xpexcludelist users</example>
    [Cmd]
    [Aliases]
    public async Task XpExcludeList(string type = null)
    {
        type = type?.ToLower();

        if (string.IsNullOrWhiteSpace(type) || type != "users" && type != "roles" && type != "channels")
        {
            await ReplyErrorAsync(Strings.XpExcludeListInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        ExcludedItemType itemType;
        string title;

        switch (type)
        {
            case "users":
                itemType = ExcludedItemType.User;
                title = Strings.XpExcludedUsersTitle(ctx.Guild.Id);
                break;
            case "roles":
                itemType = ExcludedItemType.Role;
                title = Strings.XpExcludedRolesTitle(ctx.Guild.Id);
                break;
            case "channels":
                itemType = ExcludedItemType.Channel;
                title = Strings.XpExcludedChannelsTitle(ctx.Guild.Id);
                break;
            default:
                return;
        }

        var items = await Service.GetExcludedItemsAsync(ctx.Guild.Id, itemType);

        if (items.Count == 0)
        {
            await ReplyErrorAsync(Strings.XpNoExcludedItems(ctx.Guild.Id, type)).ConfigureAwait(false);
            return;
        }

        var names = new List<string>();

        foreach (var id in items)
        {
            switch (itemType)
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

        await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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
    ///     Sets a role reward for a specific level.
    /// </summary>
    /// <param name="level">The level that triggers the reward.</param>
    /// <param name="role">The role to award.</param>
    /// <example>.rolereward 10 @VIP</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task RoleReward(int level, IRole role)
    {
        if (level <= 0)
        {
            await ReplyErrorAsync(Strings.XpLevelPositive(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        // Check if the bot can assign this role
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
    /// <example>.removerr 10</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageRoles)]
    public async Task RemoveRoleReward(int level)
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
    /// <example>.rolerewards</example>
    [Cmd]
    [Aliases]
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
        await ctx.Channel.EmbedAsync(embed);
    }

    /// <summary>
    ///     Sets a currency reward for a specific level.
    /// </summary>
    /// <param name="level">The level that triggers the reward.</param>
    /// <param name="amount">The amount of currency to award.</param>
    /// <example>.currencyreward 5 100</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task CurrencyReward(int level, long amount)
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
    /// <example>.removecr 5</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task RemoveCurrencyReward(int level)
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
    /// <example>.currencyrewards</example>
    [Cmd]
    [Aliases]
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
        await ctx.Channel.EmbedAsync(embed);
    }

    /// <summary>
    ///     Creates a new XP competition.
    /// </summary>
    /// <param name="time">How long the competition should last.</param>
    /// <param name="type">The competition type: MostGained, ReachLevel, or HighestTotal.</param>
    /// <param name="targetLevel">For ReachLevel competitions, the target level.</param>
    /// <param name="name">The name of the competition.</param>
    /// <example>.xpcompetition 1d MostGained Weekly XP Race</example>
    /// <example>.xpcompetition 3d ReachLevel 20 First to Level 20</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task XpCompetition(StoopidTime time, XpCompetitionType type, int targetLevel = 0,
        [Remainder] string name = null)
    {
        if (time.Time.TotalMinutes < 60)
        {
            await ReplyErrorAsync(Strings.XpCompetitionTooShort(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            await ReplyErrorAsync(Strings.XpCompetitionNeedsName(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (type == XpCompetitionType.ReachLevel && targetLevel <= 0)
        {
            await ReplyErrorAsync(Strings.XpCompetitionNeedsLevel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var startTime = DateTime.UtcNow;
        var endTime = startTime + time.Time;

        var competition = await Service.CreateCompetitionAsync(
            ctx.Guild.Id,
            name,
            type,
            startTime,
            endTime,
            targetLevel,
            ctx.Channel.Id // Use current channel for announcements
        );

        // Start the competition
        await Service.StartCompetitionAsync(competition.Id);

        await ReplyConfirmAsync(Strings.XpCompetitionCreated(
            ctx.Guild.Id,
            competition.Name,
            competition.Type.ToString(),
            time.Time.Humanize(),
            competition.Type == (int)XpCompetitionType.ReachLevel ? competition.TargetLevel.ToString() : ""
        )).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a reward for a competition placement.
    /// </summary>
    /// <param name="competitionId">The ID of the competition.</param>
    /// <param name="position">The position to reward (1 for first place, etc.).</param>
    /// <param name="type">The type of reward: Role, XP, or Currency.</param>
    /// <param name="reward">For Role rewards, the role to award; for XP or Currency rewards, the amount as a string.</param>
    /// <example>.addcompetitionreward 1 1 Role @Winner</example>
    /// <example>.addcompetitionreward 1 2 XP 1000</example>
    /// <example>.addcompetitionreward 1 3 Currency 500</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AddCompetitionReward(int competitionId, int position, string type, [Remainder] string reward)
    {
        if (position <= 0)
        {
            await ReplyErrorAsync(Strings.XpCompetitionPositionInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        type = type.ToLower();

        if (type != "role" && type != "xp" && type != "currency")
        {
            await ReplyErrorAsync(Strings.XpCompetitionRewardInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        switch (type)
        {
            case "role":
                var roleMatch = await ctx.Guild.GetRoleAsync(MentionUtils.ParseRole(reward));
                if (roleMatch == null)
                {
                    await ReplyErrorAsync(Strings.XpCompetitionRoleNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                await Service.AddCompetitionRewardAsync(competitionId, position, roleMatch.Id);
                await ReplyConfirmAsync(Strings.XpCompetitionRoleRewardAdded(
                    ctx.Guild.Id,
                    position,
                    roleMatch.Mention,
                    competitionId
                )).ConfigureAwait(false);
                break;

            case "xp":
                if (!int.TryParse(reward, out var xpAmount) || xpAmount <= 0)
                {
                    await ReplyErrorAsync(Strings.XpCompetitionAmountInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                await Service.AddCompetitionRewardAsync(competitionId, position, 0, xpAmount);
                await ReplyConfirmAsync(Strings.XpCompetitionXpRewardAdded(
                    ctx.Guild.Id,
                    position,
                    xpAmount,
                    competitionId
                )).ConfigureAwait(false);
                break;

            case "currency":
                if (!long.TryParse(reward, out var currencyAmount) || currencyAmount <= 0)
                {
                    await ReplyErrorAsync(Strings.XpCompetitionAmountInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                await Service.AddCompetitionRewardAsync(competitionId, position, 0, 0, currencyAmount);
                await ReplyConfirmAsync(Strings.XpCompetitionCurrencyRewardAdded(
                    ctx.Guild.Id,
                    position,
                    currencyAmount,
                    await currencyService.GetCurrencyEmote(ctx.Guild.Id),
                    competitionId
                )).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    ///     Lists active XP competitions.
    /// </summary>
    /// <example>.xpcompetitions</example>
    [Cmd]
    [Aliases]
    public async Task XpCompetitions()
    {
        var competitions = await Service.GetActiveCompetitionsAsync(ctx.Guild.Id);

        if (competitions.Count == 0)
        {
            await ReplyErrorAsync(Strings.XpNoActiveCompetitions(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.XpActiveCompetitionsTitle(ctx.Guild.Id));

        foreach (var comp in competitions)
        {
            var timeLeft = comp.EndTime - DateTime.UtcNow;
            var description = Strings.XpCompetitionInfo(
                ctx.Guild.Id,
                comp.Type.ToString(),
                timeLeft.Humanize(),
                comp.Type == (int)XpCompetitionType.ReachLevel ? comp.TargetLevel.ToString() : ""
            );

            embed.AddField(comp.Name, description);
        }

        await ctx.Channel.EmbedAsync(embed);
    }

    /// <summary>
    ///     Shows the leaderboard for an active competition.
    /// </summary>
    /// <param name="competitionId">ID of the competition.</param>
    /// <example>.competitionlb 1</example>
    [Cmd]
    [Aliases]
    public async Task CompetitionLeaderboard(int competitionId)
    {
        var competition = await Service.GetCompetitionAsync(competitionId);

        if (competition == null || competition.GuildId != ctx.Guild.Id)
        {
            await ReplyErrorAsync(Strings.XpCompetitionNotFound(ctx.Guild.Id, competitionId)).ConfigureAwait(false);
            return;
        }

        var entries = await Service.GetCompetitionEntriesAsync(competitionId);

        if (entries.Count == 0)
        {
            await ReplyErrorAsync(Strings.XpCompetitionNoEntries(ctx.Guild.Id, competition.Name)).ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.XpCompetitionLeaderboardTitle(ctx.Guild.Id, competition.Name));

        // Sort entries based on competition type
        List<(string Username, string Value, int Position)> leaderboard = new();

        switch ((XpCompetitionType)competition.Type)
        {
            case XpCompetitionType.MostGained:
                var gainedSorted = entries.OrderByDescending(e => e.CurrentXp - e.StartingXp).ToList();
                for (var i = 0; i < Math.Min(10, gainedSorted.Count); i++)
                {
                    var entry = gainedSorted[i];
                    var user = await ctx.Guild.GetUserAsync(entry.UserId);
                    var username = user?.ToString() ?? entry.UserId.ToString();
                    var gained = entry.CurrentXp - entry.StartingXp;
                    leaderboard.Add((username, gained.ToString("N0"), i + 1));
                }

                break;

            case XpCompetitionType.ReachLevel:
                var levelSorted = entries
                    .Where(e => e.AchievedTargetAt != null)
                    .OrderBy(e => e.AchievedTargetAt)
                    .ToList();

                for (var i = 0; i < Math.Min(10, levelSorted.Count); i++)
                {
                    var entry = levelSorted[i];
                    var user = await ctx.Guild.GetUserAsync(entry.UserId);
                    var username = user?.ToString() ?? entry.UserId.ToString();
                    var achievedAt = (entry.AchievedTargetAt ?? DateTime.UtcNow).ToString("yyyy-MM-dd HH:mm:ss");
                    leaderboard.Add((username, achievedAt, i + 1));
                }

                break;

            case XpCompetitionType.HighestTotal:
                var totalSorted = entries.OrderByDescending(e => e.CurrentXp).ToList();
                for (var i = 0; i < Math.Min(10, totalSorted.Count); i++)
                {
                    var entry = totalSorted[i];
                    var user = await ctx.Guild.GetUserAsync(entry.UserId);
                    var username = user?.ToString() ?? entry.UserId.ToString();
                    leaderboard.Add((username, entry.CurrentXp.ToString("N0"), i + 1));
                }

                break;
        }

        // Build description from leaderboard entries
        var description = new List<string>();
        foreach (var entry in leaderboard)
        {
            description.Add(Strings.XpCompetitionLeaderboardLine(
                ctx.Guild.Id,
                entry.Position,
                entry.Username,
                entry.Value
            ));
        }

        if (description.Count == 0)
        {
            description.Add(Strings.XpCompetitionNoQualifiedEntries(ctx.Guild.Id));
        }

        embed.WithDescription(string.Join("\n", description));

        // Add time information
        var timeLeft = competition.EndTime - DateTime.UtcNow;
        if (timeLeft.TotalSeconds > 0)
        {
            embed.WithFooter(Strings.XpCompetitionTimeLeft(ctx.Guild.Id, timeLeft.Humanize()));
        }
        else
        {
            embed.WithFooter(Strings.XpCompetitionEnded(ctx.Guild.Id));
        }

        await ctx.Channel.EmbedAsync(embed);
    }

    /// <summary>
    ///     Ends a competition early and distributes rewards.
    /// </summary>
    /// <param name="competitionId">ID of the competition to end.</param>
    /// <example>.endcompetition 1</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task EndCompetition(int competitionId)
    {
        var competition = await Service.GetCompetitionAsync(competitionId);

        if (competition == null || competition.GuildId != ctx.Guild.Id)
        {
            await ReplyErrorAsync(Strings.XpCompetitionNotFound(ctx.Guild.Id, competitionId)).ConfigureAwait(false);
            return;
        }

        if (competition.EndTime < DateTime.UtcNow)
        {
            await ReplyErrorAsync(Strings.XpCompetitionAlreadyEnded(ctx.Guild.Id, competition.Name))
                .ConfigureAwait(false);
            return;
        }

        await Service.FinalizeCompetitionAsync(competitionId);
        await ReplyConfirmAsync(Strings.XpCompetitionManuallyEnded(ctx.Guild.Id, competition.Name))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Synchronizes XP roles for all users in the guild.
    /// </summary>
    [Cmd]
    [Aliases]
    [Ratelimit(3600)]
    [UserPerm(GuildPermission.Administrator)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task SyncAllXpRoles()
    {
        try
        {
            var embed = new EmbedBuilder()
                .WithTitle(Strings.XpRoleSyncAllStarting(ctx.Guild.Id))
                .WithOkColor()
                .WithTimestamp(DateTimeOffset.UtcNow);

            var message = await ctx.Channel.EmbedAsync(embed);

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
                        Strings.XpRoleSyncRolesModified(ctx.Guild.Id, progress.RolesAdded, progress.RolesRemoved), true)
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
                    // Ignore other errors on progress updates
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
    ///     Synchronizes XP roles for the current user.
    /// </summary>
    [Cmd]
    [Aliases]
    [Ratelimit(300)]
    public async Task SyncMyXpRoles()
    {
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

            await ctx.Channel.EmbedAsync(embed);
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
    ///     Synchronizes XP roles for a specific user.
    /// </summary>
    /// <param name="user">The user to synchronize roles for.</param>
    [Cmd]
    [Aliases]
    [Ratelimit(60)]
    [UserPerm(GuildPermission.ManageRoles)]
    [BotPerm(GuildPermission.ManageRoles)]
    public async Task SyncUserXpRoles(IUser user)
    {
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

            await ctx.Channel.EmbedAsync(embed);
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

    /// <summary>
    ///     Sets the channel where level-up notifications will be sent.
    /// </summary>
    /// <param name="channel">The channel to send level-up notifications to, or null to use the current channel.</param>
    /// <example>.levelupchannel #level-ups</example>
    /// <example>.levelupchannel</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task LevelUpChannel(ITextChannel? channel = null)
    {
        await Service.SetLevelUpChannelAsync(ctx.Guild.Id, channel?.Id ?? 0);

        if (channel != null)
        {
            await ReplyConfirmAsync(Strings.LevelUpChannelSet(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.LevelUpChannelCleared(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds a custom level-up message template with placeholder support.
    /// </summary>
    /// <param name="message">The message template with placeholders like %xp.user.mention%, %xp.level.new%, etc.</param>
    /// <example>.addlevelupmessage Congratulations %xp.user.mention%! You've reached level %xp.level.new%!</example>
    /// <example>
    ///     .addlevelupmessage {"title": "Level Up!", "description": "%xp.user% reached level %xp.level.new%!", "color":
    ///     "Green"}
    /// </example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AddLevelUpMessage([Remainder] string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            await ReplyErrorAsync(Strings.LevelUpMessageEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.AddLevelUpMessageAsync(ctx.Guild.Id, message);
        await ReplyConfirmAsync(Strings.LevelUpMessageAdded(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all custom level-up messages for this server.
    /// </summary>
    /// <example>.levelupmessages</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task LevelUpMessages()
    {
        var messages = await Service.GetLevelUpMessagesAsync(ctx.Guild.Id);

        if (messages.Count == 0)
        {
            await ReplyErrorAsync(Strings.LevelUpNoCustomMessages(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(messages.Count / 5)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await serv.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask;
            var embed = new PageBuilder()
                .WithTitle(Strings.LevelUpMessagesTitle(ctx.Guild.Id))
                .WithOkColor();

            var startIndex = page * 5;
            var messagesToShow = messages.Skip(startIndex).Take(5);

            foreach (var msg in messagesToShow)
            {
                var truncated = msg.MessageContent.Length > 200
                    ? msg.MessageContent[..200] + "..."
                    : msg.MessageContent;

                embed.AddField(
                    $"#{msg.Id} {(msg.IsEnabled ? "✅" : "❌")}",
                    $"```{truncated}```");
            }

            return embed;
        }
    }

    /// <summary>
    ///     Removes a custom level-up message by ID.
    /// </summary>
    /// <param name="messageId">The ID of the message to remove (shown in .levelupmessages).</param>
    /// <example>.removelevelupmessage 3</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task RemoveLevelUpMessage(int messageId)
    {
        var success = await Service.RemoveLevelUpMessageAsync(ctx.Guild.Id, messageId);

        if (success)
        {
            await ReplyConfirmAsync(Strings.LevelUpMessageRemoved(ctx.Guild.Id, messageId)).ConfigureAwait(false);
        }
        else
        {
            await ReplyErrorAsync(Strings.LevelUpMessageNotFound(ctx.Guild.Id, messageId)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Enables or disables a custom level-up message.
    /// </summary>
    /// <param name="messageId">The ID of the message to toggle.</param>
    /// <param name="enabled">Whether to enable (true) or disable (false) the message.</param>
    /// <example>.togglelevelupmessage 3 true</example>
    /// <example>.togglelevelupmessage 3 false</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task ToggleLevelUpMessage(int messageId, bool enabled)
    {
        var success = await Service.ToggleLevelUpMessageAsync(ctx.Guild.Id, messageId, enabled);

        if (success)
        {
            var status = enabled ? Strings.Enabled(ctx.Guild.Id) : Strings.Disabled(ctx.Guild.Id);
            await ReplyConfirmAsync(Strings.LevelUpMessageToggled(ctx.Guild.Id, messageId, status))
                .ConfigureAwait(false);
        }
        else
        {
            await ReplyErrorAsync(Strings.LevelUpMessageNotFound(ctx.Guild.Id, messageId)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Tests a level-up message template by sending it to the current channel.
    /// </summary>
    /// <param name="message">The message template to test, or leave empty to test a random custom message.</param>
    /// <example>.testlevelupmessage Congratulations %xp.user.mention%! You've reached level %xp.level.new%!</example>
    /// <example>.testlevelupmessage</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task TestLevelUpMessage([Remainder] string? message = null)
    {
        var testUser = (IGuildUser)ctx.User;

        // If no message provided, use a random custom message or fall back to default
        if (string.IsNullOrWhiteSpace(message))
        {
            var customMessages = await Service.GetLevelUpMessagesAsync(ctx.Guild.Id);
            if (customMessages.Count > 0)
            {
                var random = new Random();
                message = customMessages[random.Next(customMessages.Count)].MessageContent;
            }
            else
            {
                var settings = await Service.GetGuildXpSettingsAsync(ctx.Guild.Id);
                message = settings.LevelUpMessage;
            }
        }

        // Get user preferences for ping testing
        var userPrefs = await Service.GetUserPreferencesAsync(ctx.User.Id);
        var pingsDisabled = userPrefs?.LevelUpPingsDisabled ?? false;

        // Build test replacer
        var replacer = new ReplacementBuilder()
            .WithDefault(testUser, ctx.Channel, ctx.Guild as SocketGuild, client)
            .WithXpPlaceholders(
                testUser,
                ctx.Guild,
                ctx.Channel as ITextChannel,
                9,
                10,
                5000,
                200,
                1000,
                25,
                5,
                ctx.User,
                pingsDisabled)
            .Build();

        // Process the message
        var processedMessage = ReplacementBuilderExtensions.ProcessLevelUpMessage(
            message, replacer, testUser, pingsDisabled);

        // Send the test message
        try
        {
            if (SmartEmbed.TryParse(processedMessage, ctx.Guild.Id, out var embed, out var plainText,
                    out var components))
            {
                await ctx.Channel.SendMessageAsync(plainText, embeds: embed, components: components?.Build());
            }
            else
            {
                await ctx.Channel.SendMessageAsync(
                    embed: new EmbedBuilder()
                        .WithColor(Color.Green)
                        .WithDescription(processedMessage)
                        .WithTitle(Strings.LevelUpTestTitle(ctx.Guild.Id))
                        .Build()
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error testing level-up message in guild {GuildId}", ctx.Guild.Id);
            await ReplyErrorAsync(Strings.LevelUpTestError(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Toggles level-up ping notifications for yourself.
    /// </summary>
    /// <param name="enabled">Whether to enable (true) or disable (false) level-up pings.</param>
    /// <example>.leveluppings true</example>
    /// <example>.leveluppings false</example>
    [Cmd]
    [Aliases]
    public async Task LevelUpPings(bool enabled)
    {
        await Service.SetUserLevelUpPingsAsync(ctx.User.Id, !enabled); // Note: LevelUpPingsDisabled is the inverse

        var status = enabled ? Strings.Enabled(ctx.Guild?.Id ?? 0) : Strings.Disabled(ctx.Guild?.Id ?? 0);
        await ReplyConfirmAsync(Strings.LevelUpPingsToggled(ctx.Guild?.Id ?? 0, status)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows available level-up placeholders that can be used in custom messages.
    /// </summary>
    /// <example>.levelupplaceholders</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task LevelUpPlaceholders()
    {
        var embed = new EmbedBuilder()
            .WithTitle(Strings.LevelUpPlaceholdersTitle(ctx.Guild.Id))
            .WithDescription(Strings.LevelUpPlaceholdersDesc(ctx.Guild.Id))
            .WithOkColor()
            .AddField(Strings.LevelUpPlaceholdersUser(ctx.Guild.Id),
                "`%xp.user%` - User (respects ping settings)\n" +
                "`%xp.user.mention%` - User mention (respects ping settings)\n" +
                "`%xp.user.name%` - Username\n" +
                "`%xp.user.displayname%` - Display name\n" +
                "`%xp.user.nickname%` - Nickname (or username)\n" +
                "`%xp.user.avatar%` - Avatar URL\n" +
                "`%xp.user.id%` - User ID")
            .AddField(Strings.LevelUpPlaceholdersLevel(ctx.Guild.Id),
                "`%xp.level.old%` - Previous level\n" +
                "`%xp.level.new%` - New level\n" +
                "`%xp.level.current%` - Current level (same as new)\n" +
                "`%xp.level.next%` - Next level\n" +
                "`%xp.level.difference%` - Level increase")
            .AddField(Strings.LevelUpPlaceholdersXp(ctx.Guild.Id),
                "`%xp.total%` - Total XP\n" +
                "`%xp.current%` - XP in current level\n" +
                "`%xp.needed%` - XP needed for next level\n" +
                "`%xp.remaining%` - XP remaining to next level\n" +
                "`%xp.gained%` - XP gained this session\n" +
                "`%xp.progress%` - Progress percentage")
            .AddField(Strings.LevelUpPlaceholdersOther(ctx.Guild.Id),
                "`%xp.rank%` - Current rank\n" +
                "`%xp.rank.ordinal%` - Rank in words (first, second)\n" +
                "`%xp.rank.suffix%` - Rank suffix (st, nd, rd, th)\n" +
                "`%xp.guild.name%` - Server name\n" +
                "`%xp.time%` - Current time\n" +
                "`%xp.timestamp%` - Discord timestamp")
            .AddField(Strings.LevelUpPlaceholdersLegacy(ctx.Guild.Id),
                "`%user%`, `%user.mention%`, `%level%`, `%xp%`, `%rank%`, `%server%`, `%guild%`");

        await ctx.Channel.EmbedAsync(embed);
    }
}