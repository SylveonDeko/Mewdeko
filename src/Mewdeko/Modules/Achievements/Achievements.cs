using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Modules.Achievements.Services;

namespace Mewdeko.Modules.Achievements;

/// <summary>
///     Text commands for achievements, badges, and their settings.
/// </summary>
/// <param name="views">Builds the embeds and cards.</param>
/// <param name="interactive">Sends paginated lists.</param>
public class Achievements(AchievementViewService views, InteractiveService interactive)
    : MewdekoModuleBase<AchievementService>
{
    #region Members

    /// <summary>
    ///     Shows your or someone else's achievement overview.
    /// </summary>
    /// <param name="user">The member, or yourself.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task AchOverview(IGuildUser? user = null)
    {
        var target = await ResolveTargetAsync(user, AchievementPrivacyArea.Achievements);
        if (target is null)
            return;

        var prefix = await Service.GetPrefixAsync(ctx.Guild);
        var embed = await views.OverviewAsync((SocketGuild)ctx.Guild, target, prefix);
        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Lists the achievements in one category with progress.
    /// </summary>
    /// <param name="category">The category key or name.</param>
    /// <param name="user">The member, or yourself.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task AchCategory(string category, IGuildUser? user = null)
    {
        var target = await ResolveTargetAsync(user, AchievementPrivacyArea.Achievements);
        if (target is null)
            return;

        var pages = await views.CategoryPagesAsync((SocketGuild)ctx.Guild, target, category);
        if (pages is null)
        {
            await ErrorAsync(Strings.AchievementCategoryUnknown(ctx.Guild.Id, category));
            return;
        }

        await SendPagesAsync(pages);
    }

    /// <summary>
    ///     Lists every achievement with progress.
    /// </summary>
    /// <param name="user">The member, or yourself.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task AchList(IGuildUser? user = null)
    {
        var target = await ResolveTargetAsync(user, AchievementPrivacyArea.Achievements);
        if (target is null)
            return;

        var pages = await views.CategoryPagesAsync((SocketGuild)ctx.Guild, target, null);
        await SendPagesAsync(pages!);
    }

    /// <summary>
    ///     Shows the five most recent unlocks.
    /// </summary>
    /// <param name="user">The member, or yourself.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task AchLatest(IGuildUser? user = null)
    {
        var target = await ResolveTargetAsync(user, AchievementPrivacyArea.Achievements);
        if (target is null)
            return;

        var embed = await views.TopFiveAsync((SocketGuild)ctx.Guild, target, false);
        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Shows the five best unlocks.
    /// </summary>
    /// <param name="user">The member, or yourself.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task AchHighest(IGuildUser? user = null)
    {
        var target = await ResolveTargetAsync(user, AchievementPrivacyArea.Achievements);
        if (target is null)
            return;

        var embed = await views.TopFiveAsync((SocketGuild)ctx.Guild, target, true);
        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Searches achievements by name or description.
    /// </summary>
    /// <param name="query">What to look for.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task AchSearch([Remainder] string query)
    {
        var embed = await views.SearchAsync((SocketGuild)ctx.Guild, (SocketGuildUser)ctx.User, query);
        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Shows achievements earned across every server.
    /// </summary>
    /// <param name="user">The user, or yourself.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task AchGlobal(IUser? user = null)
    {
        var target = user ?? ctx.User;
        var refusal = await views.PrivacyRefusalAsync(ctx.Guild.Id, ctx.User, target, AchievementPrivacyArea.Achievements);
        if (refusal is not null)
        {
            await ErrorAsync(refusal);
            return;
        }

        var embed = await views.GlobalAsync(ctx.Guild.Id, target);
        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Shows the server's achievement leaderboard.
    /// </summary>
    /// <param name="sort">Points, unlocked, or recent.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task AchLeaderboard(AchievementLeaderboardSort sort = AchievementLeaderboardSort.Points)
    {
        var paginator = await views.LeaderboardAsync((SocketGuild)ctx.Guild, ctx.User, sort, ctx.Guild.Id);
        if (paginator is null)
        {
            await ErrorAsync(Strings.AchievementLeaderboardEmpty(ctx.Guild.Id));
            return;
        }

        await interactive.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(30));
    }

    /// <summary>
    ///     Shows the global achievement leaderboard.
    /// </summary>
    /// <param name="sort">Points, unlocked, or recent.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task AchGlobalLeaderboard(AchievementLeaderboardSort sort = AchievementLeaderboardSort.Points)
    {
        var paginator = await views.LeaderboardAsync(null, ctx.User, sort, ctx.Guild.Id);
        if (paginator is null)
        {
            await ErrorAsync(Strings.AchievementLeaderboardEmpty(ctx.Guild.Id));
            return;
        }

        await interactive.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(30));
    }

    /// <summary>
    ///     Shows an achievement profile card.
    /// </summary>
    /// <param name="user">The member, or yourself.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task AchCard(IGuildUser? user = null)
    {
        var target = await ResolveTargetAsync(user, AchievementPrivacyArea.Profile);
        if (target is null)
            return;

        await using var card = await views.CardAsync((SocketGuild)ctx.Guild, target, ctx.Guild.Id);
        var fileName = Strings.AchievementCardFileName(ctx.Guild.Id);
        await ctx.Channel.SendFileAsync(card, fileName);
    }

    /// <summary>
    ///     Shows every number achievements track for a member.
    /// </summary>
    /// <param name="user">The member, or yourself.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public Task AchStats(IGuildUser? user = null)
    {
        return StatsAsync(user);
    }

    /// <summary>
    ///     Draws one achievement as a member sees it: unlocked, or locked with their progress.
    /// </summary>
    /// <param name="achievement">The key or name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public Task AchView([Remainder] string achievement)
    {
        return ViewAsync(achievement, null);
    }

    /// <summary>
    ///     Draws one achievement as another member sees it.
    /// </summary>
    /// <param name="user">The member.</param>
    /// <param name="achievement">The key or name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public Task AchView(IGuildUser user, [Remainder] string achievement)
    {
        return ViewAsync(achievement, user);
    }

    /// <summary>
    ///     Sets the icon an achievement shows, or clears it to use its category's.
    /// </summary>
    /// <param name="achievement">The key or name, in quotes when it has spaces.</param>
    /// <param name="icon">An icon name like trophy, a server emoji, or an https image link.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AchIcon(string achievement, [Remainder] string? icon = null)
    {
        var def = await FindAsync(achievement);
        if (def is null)
            return;

        var result = await Service.SetIconAsync((SocketGuild)ctx.Guild, def.Key, icon);
        if (!result.Success)
        {
            await ErrorAsync(views.DescribeError(ctx.Guild.Id, result.Error));
            return;
        }

        await ConfirmAsync(string.IsNullOrWhiteSpace(icon)
            ? Strings.AchievementIconCleared(ctx.Guild.Id, result.Value!.Name)
            : Strings.AchievementIconSet(ctx.Guild.Id, result.Value!.Name));
    }

    private async Task ViewAsync(string achievement, IGuildUser? user)
    {
        var target = await ResolveTargetAsync(user, AchievementPrivacyArea.Achievements);
        if (target is null)
            return;

        var def = await FindAsync(achievement);
        if (def is null)
            return;

        var catalog = await Service.GetCatalogAsync(ctx.Guild.Id);
        var progress = (await Service.GetProgressAsync((SocketGuild)ctx.Guild, target.Id, d => d.Key == def.Key))
            .FirstOrDefault();
        var staff = ((IGuildUser)ctx.User).GuildPermissions.ManageGuild;
        if (progress is null || def.Hidden && !progress.Unlocked && !staff &&
            !Service.GetSettings(ctx.Guild.Id).Row.RevealHidden)
        {
            await ErrorAsync(Strings.AchievementUnknown(ctx.Guild.Id, achievement));
            return;
        }

        await using var image = await Service.RenderProgressImageAsync(target, progress, catalog);
        if (image is null)
        {
            await ErrorAsync(Strings.AchievementImageFailed(ctx.Guild.Id));
            return;
        }

        await ctx.Channel.SendFileAsync(image, AchievementService.UnlockImageFileName);
    }

    private async Task StatsAsync(IGuildUser? user)
    {
        var target = await ResolveTargetAsync(user, AchievementPrivacyArea.Profile);
        if (target is null)
            return;

        var embed = await views.StatsAsync((SocketGuild)ctx.Guild, target);
        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Shows your or someone else's badges.
    /// </summary>
    /// <param name="user">The member, or yourself.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Badges(IGuildUser? user = null)
    {
        var target = await ResolveTargetAsync(user, AchievementPrivacyArea.Badges);
        if (target is null)
            return;

        var pages = await views.BadgePagesAsync((SocketGuild)ctx.Guild, target);
        await SendPagesAsync(pages);
    }

    /// <summary>
    ///     Puts a badge in one of your four profile slots.
    /// </summary>
    /// <param name="slot">1 to 4.</param>
    /// <param name="badge">The badge key from badges.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task BadgeEquip(int slot, string badge)
    {
        var result = await Service.EquipBadgeAsync(ctx.Guild.Id, ctx.User.Id, slot, badge.Trim());
        if (!result.Success)
        {
            await ErrorAsync(views.DescribeError(ctx.Guild.Id, result.Error));
            return;
        }

        await ConfirmAsync(Strings.AchievementBadgeEquipped(ctx.Guild.Id, result.Value!.Name, slot));
    }

    /// <summary>
    ///     Empties one of your profile slots.
    /// </summary>
    /// <param name="slot">1 to 4.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task BadgeRemove(int slot)
    {
        if (!await Service.UnequipBadgeAsync(ctx.Guild.Id, ctx.User.Id, slot))
        {
            await ErrorAsync(views.DescribeError(ctx.Guild.Id, AchievementError.BadgeInvalid));
            return;
        }

        await ConfirmAsync(Strings.AchievementBadgeRemoved(ctx.Guild.Id, slot));
    }

    /// <summary>
    ///     Shows what a badge is and how to earn it.
    /// </summary>
    /// <param name="badge">The badge key.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task BadgePreview(string badge)
    {
        var catalog = await Service.GetCatalogAsync(ctx.Guild.Id);
        var found = AchievementService.FindBadge(catalog, badge.Trim());
        if (found is null)
        {
            await ErrorAsync(views.DescribeError(ctx.Guild.Id, AchievementError.BadgeInvalid));
            return;
        }

        var owned = (await Service.GetOwnedBadgesAsync(ctx.Guild.Id, ctx.User.Id)).Any(b => b.Key == found.Key);
        await ctx.Channel.SendMessageAsync(embed: views.BadgePreview(ctx.Guild.Id, found, owned).Build());
    }

    /// <summary>
    ///     Sets who can see part of your achievements.
    /// </summary>
    /// <param name="area">Profile, achievements, badges, or leaderboard.</param>
    /// <param name="visibility">Everyone or only me.</param>
    [Cmd]
    [Aliases]
    public async Task AchPrivacy(AchievementPrivacyArea area, AchievementVisibility visibility)
    {
        await Service.SetPrivacyAsync(ctx.User.Id, area, visibility);
        var guildId = ctx.Guild?.Id ?? 0;
        await ConfirmAsync(visibility == AchievementVisibility.Everyone
            ? Strings.AchievementPrivacyPublic(guildId, area.ToString())
            : Strings.AchievementPrivacyPrivate(guildId, area.ToString()));
    }

    /// <summary>
    ///     Turns achievement DMs, unlock messages, or mentions on or off for you.
    /// </summary>
    /// <param name="option">Dm, message, or mention.</param>
    /// <param name="enabled">On or off.</param>
    [Cmd]
    [Aliases]
    public async Task AchNotify(AchievementNotifyOption option, bool enabled)
    {
        await Service.UpdateUserSettingsAsync(ctx.User.Id, row =>
        {
            switch (option)
            {
                case AchievementNotifyOption.Dm:
                    row.DmUnlocks = (int)(enabled ? AchievementDmPreference.Always : AchievementDmPreference.Never);
                    break;
                case AchievementNotifyOption.Message:
                    row.ShowInLog = enabled;
                    break;
                case AchievementNotifyOption.Mention:
                    row.MentionMe = enabled;
                    break;
            }
        });

        var guildId = ctx.Guild?.Id ?? 0;
        await ConfirmAsync(enabled
            ? Strings.AchievementNotifyOn(guildId, option.ToString())
            : Strings.AchievementNotifyOff(guildId, option.ToString()));
    }

    #endregion

    #region Staff

    /// <summary>
    ///     Lists every achievement with its key, state, and unlock count.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AchManage()
    {
        var pages = await views.AdminListAsync((SocketGuild)ctx.Guild);
        await SendPagesAsync(pages);
    }

    /// <summary>
    ///     Turns achievements on or off for the server.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AchToggle()
    {
        var enabled = !Service.IsEnabled(ctx.Guild.Id);
        await Service.SetEnabledAsync(ctx.Guild.Id, enabled);
        await ConfirmAsync(enabled
            ? Strings.AchievementEnabledServer(ctx.Guild.Id)
            : Strings.AchievementDisabledServer(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets or clears the channel unlocks are announced in.
    /// </summary>
    /// <param name="channel">The channel, or nothing to clear.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AchLogChannel(ITextChannel? channel = null)
    {
        await Service.UpdateSettingsAsync(ctx.Guild.Id, row => row.LogChannelId = channel?.Id);
        await ConfirmAsync(channel is null
            ? Strings.AchievementLogCleared(ctx.Guild.Id)
            : Strings.AchievementLogSet(ctx.Guild.Id, channel.Mention));
    }

    /// <summary>
    ///     Sets where unlocks are announced.
    /// </summary>
    /// <param name="mode">Auto, Here, LogChannel, DmOnly, or Silent.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AchAnnounce(AchievementAnnounceMode mode)
    {
        await Service.UpdateSettingsAsync(ctx.Guild.Id, row => row.AnnounceMode = (int)mode);
        await ConfirmAsync(Strings.AchievementAnnounceSet(ctx.Guild.Id, mode.ToString()));
    }

    /// <summary>
    ///     Sets whether members get unlocks in their DMs unless they choose otherwise.
    /// </summary>
    /// <param name="enabled">On or off.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AchDmDefault(bool enabled)
    {
        await Service.UpdateSettingsAsync(ctx.Guild.Id, row => row.DmByDefault = enabled);
        await ConfirmAsync(enabled
            ? Strings.AchievementDmDefaultOn(ctx.Guild.Id)
            : Strings.AchievementDmDefaultOff(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets the unlock message, or clears it to use the default embed.
    /// </summary>
    /// <param name="message">Text or embed JSON with placeholders, or nothing to clear.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AchMessage([Remainder] string? message = null)
    {
        if (message?.Length > AchievementService.MessageLength)
        {
            await ErrorAsync(Strings.AchievementErrorTooLong(ctx.Guild.Id));
            return;
        }

        await Service.UpdateSettingsAsync(ctx.Guild.Id, row => row.UnlockMessage = string.IsNullOrWhiteSpace(message) ? null : message);
        await ConfirmAsync(string.IsNullOrWhiteSpace(message)
            ? Strings.AchievementMessageCleared(ctx.Guild.Id)
            : Strings.AchievementMessageSet(ctx.Guild.Id));
    }

    /// <summary>
    ///     Gives XP for every achievement point earned.
    /// </summary>
    /// <param name="amount">XP per point, 0 to turn off.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AchXpPerPoint(int amount)
    {
        amount = Math.Clamp(amount, 0, 1000);
        await Service.UpdateSettingsAsync(ctx.Guild.Id, row => row.XpPerPoint = amount);
        await ConfirmAsync(Strings.AchievementXpPerPointSet(ctx.Guild.Id, amount));
    }

    /// <summary>
    ///     Turns one achievement on.
    /// </summary>
    /// <param name="achievement">The key or name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public Task AchEnable([Remainder] string achievement)
    {
        return SetAchievementEnabledAsync(achievement, true);
    }

    /// <summary>
    ///     Turns one achievement off. Members who unlocked it keep it.
    /// </summary>
    /// <param name="achievement">The key or name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public Task AchDisable([Remainder] string achievement)
    {
        return SetAchievementEnabledAsync(achievement, false);
    }

    /// <summary>
    ///     Turns a whole category on or off.
    /// </summary>
    /// <param name="category">The category key or name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AchCategoryToggle([Remainder] string category)
    {
        var catalog = await Service.GetCatalogAsync(ctx.Guild.Id);
        var found = AchievementViewService.FindCategory(catalog, category);
        if (found is null)
        {
            await ErrorAsync(Strings.AchievementCategoryUnknown(ctx.Guild.Id, category));
            return;
        }

        var settings = Service.GetSettings(ctx.Guild.Id);
        var disabled = settings.DisabledCategories.ToHashSet();
        var nowOff = disabled.Add(found.Key);
        if (!nowOff)
            disabled.Remove(found.Key);
        await Service.SetCategoryLayoutAsync(ctx.Guild.Id, settings.CategoryOrder, disabled);
        await ConfirmAsync(nowOff
            ? Strings.AchievementCategoryOff(ctx.Guild.Id, found.Name)
            : Strings.AchievementCategoryOn(ctx.Guild.Id, found.Name));
    }

    /// <summary>
    ///     Hands an achievement to a member.
    /// </summary>
    /// <param name="user">The member.</param>
    /// <param name="achievement">The key or name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AchGrant(IGuildUser user, [Remainder] string achievement)
    {
        var def = await FindAsync(achievement);
        if (def is null)
            return;

        var result = await Service.GrantAsync((SocketGuildUser)user, def.Key, ctx.User.Id);
        if (!result.Success)
        {
            await ErrorAsync(views.DescribeError(ctx.Guild.Id, result.Error));
            return;
        }

        await ConfirmAsync(Strings.AchievementGranted(ctx.Guild.Id, def.Name, user.Mention));
    }

    /// <summary>
    ///     Takes an achievement from a member.
    /// </summary>
    /// <param name="user">The member.</param>
    /// <param name="achievement">The key or name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AchRevoke(IGuildUser user, [Remainder] string achievement)
    {
        var def = await FindAsync(achievement);
        if (def is null)
            return;

        var result = await Service.RevokeAsync(ctx.Guild.Id, user.Id, def.Key);
        if (!result.Success)
        {
            await ErrorAsync(views.DescribeError(ctx.Guild.Id, result.Error));
            return;
        }

        await ConfirmAsync(Strings.AchievementRevoked(ctx.Guild.Id, def.Name, user.Mention));
    }

    /// <summary>
    ///     Clears every achievement a member has in the server.
    /// </summary>
    /// <param name="user">The member.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AchReset(IGuildUser user)
    {
        if (!await PromptUserConfirmAsync(Strings.AchievementResetConfirm(ctx.Guild.Id, user.Mention), ctx.User.Id))
            return;

        var cleared = await Service.ResetMemberAsync(ctx.Guild.Id, user.Id);
        await ConfirmAsync(Strings.AchievementResetDone(ctx.Guild.Id, cleared, user.Mention));
    }

    /// <summary>
    ///     Makes an achievement unlocked by reaching a number.
    /// </summary>
    /// <param name="metric">What to count.</param>
    /// <param name="goal">The number to reach.</param>
    /// <param name="grade">Bronze to Champion.</param>
    /// <param name="name">The name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public Task AchCreate(AchievementMetric metric, long goal, AchievementGrade grade, [Remainder] string name)
    {
        return CreateAsync(new CustomAchievementDraft
        {
            Name = name,
            Grade = grade,
            Trigger = AchievementTrigger.Metric,
            Metric = metric,
            Threshold = goal
        });
    }

    /// <summary>
    ///     Makes an achievement unlocked by saying a phrase.
    /// </summary>
    /// <param name="phrase">The phrase, in quotes when it has spaces.</param>
    /// <param name="grade">Bronze to Champion.</param>
    /// <param name="name">The name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public Task AchCreatePhrase(string phrase, AchievementGrade grade, [Remainder] string name)
    {
        return CreateAsync(new CustomAchievementDraft
        {
            Name = name,
            Grade = grade,
            Trigger = AchievementTrigger.Keyword,
            Keyword = phrase
        });
    }

    /// <summary>
    ///     Makes an achievement unlocked by reacting with an emoji.
    /// </summary>
    /// <param name="emoji">The emoji.</param>
    /// <param name="grade">Bronze to Champion.</param>
    /// <param name="name">The name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public Task AchCreateReaction(string emoji, AchievementGrade grade, [Remainder] string name)
    {
        return CreateAsync(new CustomAchievementDraft
        {
            Name = name,
            Grade = grade,
            Trigger = AchievementTrigger.Reaction,
            Keyword = emoji
        });
    }

    /// <summary>
    ///     Makes an achievement only staff can hand out.
    /// </summary>
    /// <param name="grade">Bronze to Champion.</param>
    /// <param name="name">The name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public Task AchCreateManual(AchievementGrade grade, [Remainder] string name)
    {
        return CreateAsync(new CustomAchievementDraft
        {
            Name = name,
            Grade = grade,
            Trigger = AchievementTrigger.Manual
        });
    }

    /// <summary>
    ///     Deletes an achievement the server made.
    /// </summary>
    /// <param name="achievement">The key or name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AchDelete([Remainder] string achievement)
    {
        var def = await FindAsync(achievement);
        if (def is null)
            return;

        if (!def.IsCustom)
        {
            await ErrorAsync(Strings.AchievementDeleteBuiltIn(ctx.Guild.Id));
            return;
        }

        if (!await PromptUserConfirmAsync(Strings.AchievementDeleteConfirm(ctx.Guild.Id, def.Name), ctx.User.Id))
            return;

        await Service.DeleteCustomAsync(ctx.Guild.Id, def.CustomId);
        await ConfirmAsync(Strings.AchievementDeleted(ctx.Guild.Id, def.Name));
    }

    /// <summary>
    ///     Gives a role to members who unlock an achievement.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <param name="achievement">The key or name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    [BotPerm(GuildPermission.ManageRoles)]
    public Task AchRewardRole(IRole role, [Remainder] string achievement)
    {
        return SetRewardAsync(achievement, role.Id, null, null);
    }

    /// <summary>
    ///     Gives currency to members who unlock an achievement.
    /// </summary>
    /// <param name="amount">The amount, 0 to clear.</param>
    /// <param name="achievement">The key or name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public Task AchRewardCurrency(long amount, [Remainder] string achievement)
    {
        return SetRewardAsync(achievement, null, amount, null);
    }

    /// <summary>
    ///     Gives XP to members who unlock an achievement.
    /// </summary>
    /// <param name="amount">The amount, 0 to clear.</param>
    /// <param name="achievement">The key or name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public Task AchRewardXp(int amount, [Remainder] string achievement)
    {
        return SetRewardAsync(achievement, null, null, amount);
    }

    /// <summary>
    ///     Removes every reward from an achievement.
    /// </summary>
    /// <param name="achievement">The key or name.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public Task AchRewardClear([Remainder] string achievement)
    {
        return SetRewardAsync(achievement, 0, 0, 0);
    }

    /// <summary>
    ///     Stops a role from earning achievements, or lets it earn again.
    /// </summary>
    /// <param name="role">The role.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AchExcludeRole(IRole role)
    {
        var excluded = await Service.ToggleExclusionAsync(ctx.Guild.Id, role.Id, true);
        await ConfirmAsync(excluded
            ? Strings.AchievementExcluded(ctx.Guild.Id, role.Mention)
            : Strings.AchievementIncluded(ctx.Guild.Id, role.Mention));
    }

    /// <summary>
    ///     Stops activity in a channel from earning achievements, or lets it earn again.
    /// </summary>
    /// <param name="channel">The channel.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task AchExcludeChannel(IGuildChannel channel)
    {
        var excluded = await Service.ToggleExclusionAsync(ctx.Guild.Id, channel.Id, false);
        var mention = MentionUtils.MentionChannel(channel.Id);
        await ConfirmAsync(excluded
            ? Strings.AchievementExcluded(ctx.Guild.Id, mention)
            : Strings.AchievementIncluded(ctx.Guild.Id, mention));
    }

    #endregion

    private async Task<SocketGuildUser?> ResolveTargetAsync(IGuildUser? user, AchievementPrivacyArea area)
    {
        var target = (SocketGuildUser)(user ?? ctx.User);
        if (!Service.IsEnabled(ctx.Guild.Id) && target.Id == ctx.User.Id && user is null)
        {
            var prefix = await Service.GetPrefixAsync(ctx.Guild);
            await ErrorAsync(Strings.AchievementServerOff(ctx.Guild.Id, prefix));
            return null;
        }

        var refusal = await views.PrivacyRefusalAsync(ctx.Guild.Id, ctx.User, target, area);
        if (refusal is null)
            return target;

        await ErrorAsync(refusal);
        return null;
    }

    private async Task<AchievementDefinition?> FindAsync(string input)
    {
        var catalog = await Service.GetCatalogAsync(ctx.Guild.Id);
        var def = AchievementViewService.FindAchievement(catalog, input);
        if (def is null)
            await ErrorAsync(Strings.AchievementUnknown(ctx.Guild.Id, input));
        return def;
    }

    private async Task SetAchievementEnabledAsync(string input, bool enabled)
    {
        var def = await FindAsync(input);
        if (def is null)
            return;

        var result = await Service.SetAchievementEnabledAsync(ctx.Guild.Id, def.Key, enabled);
        if (!result.Success)
        {
            await ErrorAsync(views.DescribeError(ctx.Guild.Id, result.Error));
            return;
        }

        await ConfirmAsync(enabled
            ? Strings.AchievementTurnedOn(ctx.Guild.Id, def.Name)
            : Strings.AchievementTurnedOff(ctx.Guild.Id, def.Name));
    }

    private async Task CreateAsync(CustomAchievementDraft draft)
    {
        var result = await Service.CreateCustomAsync((SocketGuild)ctx.Guild, ctx.User.Id, draft);
        if (!result.Success)
        {
            await ErrorAsync(views.DescribeError(ctx.Guild.Id, result.Error));
            return;
        }

        var def = result.Value!;
        await ConfirmAsync(Strings.AchievementCreated(ctx.Guild.Id, def.Name, def.Key, def.Description));
    }

    private async Task SetRewardAsync(string input, ulong? roleId, long? currency, int? xp)
    {
        var def = await FindAsync(input);
        if (def is null)
            return;

        var result = await Service.SetRewardsAsync((SocketGuild)ctx.Guild, def.Key, roleId, currency, xp);
        if (!result.Success)
        {
            await ErrorAsync(views.DescribeError(ctx.Guild.Id, result.Error));
            return;
        }

        var updated = result.Value!;
        var role = updated.RoleRewardId is { } roleRewardId ? MentionUtils.MentionRole(roleRewardId) : "-";
        await ConfirmAsync(Strings.AchievementRewardsSet(ctx.Guild.Id, updated.Name, role,
            updated.CurrencyReward.ToString("N0"), updated.XpReward.ToString("N0")));
    }

    private async Task SendPagesAsync(IReadOnlyList<PageBuilder> pages)
    {
        if (pages.Count == 1)
        {
            await ctx.Channel.SendMessageAsync(embed: pages[0].GetEmbedBuilder().Build());
            return;
        }

        var paginator = new StaticPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPages(pages)
            .WithFooter(PaginatorFooter.None)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();
        await interactive.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(30));
    }
}
