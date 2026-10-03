using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Modules.Achievements.Services;

namespace Mewdeko.Modules.Achievements;

/// <summary>
///     Slash commands for achievements, badges, and their settings.
/// </summary>
/// <param name="views">Builds the embeds and cards.</param>
/// <param name="interactive">Sends paginated lists.</param>
[Group("achievements", "Achievements, badges, and progress")]
public class SlashAchievements(AchievementViewService views, InteractiveService interactive)
    : MewdekoSlashModuleBase<AchievementService>
{
    /// <summary>
    ///     Shows your or someone else's achievement overview.
    /// </summary>
    /// <param name="user">The member, or yourself.</param>
    [SlashCommand("overview", "Shows your or someone else's achievement progress")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Overview(IGuildUser? user = null)
    {
        var target = await ResolveTargetAsync(user, AchievementPrivacyArea.Achievements);
        if (target is null)
            return;

        var prefix = await Service.GetPrefixAsync(ctx.Guild);
        var embed = await views.OverviewAsync((SocketGuild)ctx.Guild, target, prefix);
        await RespondAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Lists the achievements in one category with progress.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <param name="user">The member, or yourself.</param>
    [SlashCommand("category", "Shows every achievement in one category with progress")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Category(
        [Summary("category", "The category")] [Autocomplete(typeof(AchievementCategoryAutocompleter))]
        string category,
        IGuildUser? user = null)
    {
        var target = await ResolveTargetAsync(user, AchievementPrivacyArea.Achievements);
        if (target is null)
            return;

        var pages = await views.CategoryPagesAsync((SocketGuild)ctx.Guild, target, category);
        if (pages is null)
        {
            await EphemeralReplyErrorAsync(Strings.AchievementCategoryUnknown(ctx.Guild.Id, category));
            return;
        }

        await SendPagesAsync(pages);
    }

    /// <summary>
    ///     Lists every achievement with progress.
    /// </summary>
    /// <param name="user">The member, or yourself.</param>
    [SlashCommand("list", "Shows every achievement with progress")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task List(IGuildUser? user = null)
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
    [SlashCommand("latest", "Shows the five most recent unlocks")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Latest(IGuildUser? user = null)
    {
        var target = await ResolveTargetAsync(user, AchievementPrivacyArea.Achievements);
        if (target is null)
            return;

        var embed = await views.TopFiveAsync((SocketGuild)ctx.Guild, target, false);
        await RespondAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Shows the five best unlocks.
    /// </summary>
    /// <param name="user">The member, or yourself.</param>
    [SlashCommand("highest", "Shows the five best unlocks")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Highest(IGuildUser? user = null)
    {
        var target = await ResolveTargetAsync(user, AchievementPrivacyArea.Achievements);
        if (target is null)
            return;

        var embed = await views.TopFiveAsync((SocketGuild)ctx.Guild, target, true);
        await RespondAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Searches achievements by name or description.
    /// </summary>
    /// <param name="query">What to look for.</param>
    [SlashCommand("search", "Finds achievements by name or description")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Search(string query)
    {
        var embed = await views.SearchAsync((SocketGuild)ctx.Guild, (SocketGuildUser)ctx.User, query);
        await RespondAsync(embed: embed.Build(), ephemeral: true);
    }

    /// <summary>
    ///     Shows achievements earned across every server.
    /// </summary>
    /// <param name="user">The user, or yourself.</param>
    [SlashCommand("global", "Shows achievements earned across every server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Global(IUser? user = null)
    {
        var target = user ?? ctx.User;
        var refusal = await views.PrivacyRefusalAsync(ctx.Guild.Id, ctx.User, target, AchievementPrivacyArea.Achievements);
        if (refusal is not null)
        {
            await EphemeralReplyErrorAsync(refusal);
            return;
        }

        var embed = await views.GlobalAsync(ctx.Guild.Id, target);
        await RespondAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Shows the achievement leaderboard.
    /// </summary>
    /// <param name="sort">Points, unlocked, or recent.</param>
    /// <param name="global">Rank across every server instead of this one.</param>
    [SlashCommand("leaderboard", "Shows who has earned the most")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Leaderboard(AchievementLeaderboardSort sort = AchievementLeaderboardSort.Points,
        bool global = false)
    {
        var paginator = await views.LeaderboardAsync(global ? null : (SocketGuild)ctx.Guild, ctx.User, sort,
            ctx.Guild.Id);
        if (paginator is null)
        {
            await EphemeralReplyErrorAsync(Strings.AchievementLeaderboardEmpty(ctx.Guild.Id));
            return;
        }

        await interactive.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(30));
    }

    /// <summary>
    ///     Shows an achievement profile card.
    /// </summary>
    /// <param name="user">The member, or yourself.</param>
    /// <param name="global">Show totals across every server.</param>
    [SlashCommand("card", "Shows an achievement profile card")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Card(IGuildUser? user = null, bool global = false)
    {
        var target = await ResolveTargetAsync(user, AchievementPrivacyArea.Profile);
        if (target is null)
            return;

        await DeferAsync();
        await using var card = await views.CardAsync(global ? null : (SocketGuild)ctx.Guild, target, ctx.Guild.Id);
        var fileName = Strings.AchievementCardFileName(ctx.Guild.Id);
        await FollowupWithFileAsync(card, fileName);
    }

    /// <summary>
    ///     Draws one achievement as a member sees it: unlocked, or locked with their progress.
    /// </summary>
    /// <param name="achievement">The achievement.</param>
    /// <param name="user">The member, or yourself.</param>
    [SlashCommand("view", "Draws an achievement as an image, unlocked or with how close you are")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task View(
        [Summary("achievement", "The achievement")] [Autocomplete(typeof(AchievementAutocompleter))]
        string achievement,
        IGuildUser? user = null)
    {
        var target = await ResolveTargetAsync(user, AchievementPrivacyArea.Achievements);
        if (target is null)
            return;

        var catalog = await Service.GetCatalogAsync(ctx.Guild.Id);
        var def = AchievementViewService.FindAchievement(catalog, achievement);
        var progress = def is null
            ? null
            : (await Service.GetProgressAsync((SocketGuild)ctx.Guild, target.Id, d => d.Key == def.Key)).FirstOrDefault();
        var staff = ((IGuildUser)ctx.User).GuildPermissions.ManageGuild;
        if (def is null || progress is null || def.Hidden && !progress.Unlocked && !staff &&
            !Service.GetSettings(ctx.Guild.Id).Row.RevealHidden)
        {
            await EphemeralReplyErrorAsync(Strings.AchievementUnknown(ctx.Guild.Id, achievement));
            return;
        }

        await DeferAsync();
        await using var image = await Service.RenderProgressImageAsync(target, progress, catalog);
        if (image is null)
        {
            await FollowupAsync(Strings.AchievementImageFailed(ctx.Guild.Id), ephemeral: true);
            return;
        }

        await FollowupWithFileAsync(image, AchievementService.UnlockImageFileName);
    }

    /// <summary>
    ///     Shows every number achievements track for a member.
    /// </summary>
    /// <param name="user">The member, or yourself.</param>
    [SlashCommand("stats", "Shows every number achievements track")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Stats(IGuildUser? user = null)
    {
        var target = await ResolveTargetAsync(user, AchievementPrivacyArea.Profile);
        if (target is null)
            return;

        await DeferAsync();
        var embed = await views.StatsAsync((SocketGuild)ctx.Guild, target);
        await FollowupAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Sets who can see part of your achievements.
    /// </summary>
    /// <param name="area">Profile, achievements, badges, or leaderboard.</param>
    /// <param name="visibility">Everyone or only you.</param>
    [SlashCommand("privacy", "Sets who can see your profile, achievements, badges, and leaderboard spot")]
    [CheckPermissions]
    public async Task Privacy(AchievementPrivacyArea area, AchievementVisibility visibility)
    {
        await Service.SetPrivacyAsync(ctx.User.Id, area, visibility);
        var guildId = ctx.Guild?.Id ?? 0;
        await EphemeralReplyConfirmAsync(visibility == AchievementVisibility.Everyone
            ? Strings.AchievementPrivacyPublic(guildId, area.ToString())
            : Strings.AchievementPrivacyPrivate(guildId, area.ToString()));
    }

    /// <summary>
    ///     Turns achievement DMs, unlock messages, or mentions on or off for you.
    /// </summary>
    /// <param name="option">Dm, message, or mention.</param>
    /// <param name="enabled">On or off.</param>
    [SlashCommand("notify", "Turns unlock DMs, messages, or mentions on or off for you")]
    [CheckPermissions]
    public async Task Notify(AchievementNotifyOption option, bool enabled)
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
        await EphemeralReplyConfirmAsync(enabled
            ? Strings.AchievementNotifyOn(guildId, option.ToString())
            : Strings.AchievementNotifyOff(guildId, option.ToString()));
    }

    private async Task<SocketGuildUser?> ResolveTargetAsync(IGuildUser? user, AchievementPrivacyArea area)
    {
        var target = (SocketGuildUser)(user ?? ctx.User);
        if (!Service.IsEnabled(ctx.Guild.Id) && user is null)
        {
            var prefix = await Service.GetPrefixAsync(ctx.Guild);
            await EphemeralReplyErrorAsync(Strings.AchievementServerOff(ctx.Guild.Id, prefix));
            return null;
        }

        var refusal = await views.PrivacyRefusalAsync(ctx.Guild.Id, ctx.User, target, area);
        if (refusal is null)
            return target;

        await EphemeralReplyErrorAsync(refusal);
        return null;
    }

    private async Task SendPagesAsync(IReadOnlyList<PageBuilder> pages)
    {
        if (pages.Count == 1)
        {
            await RespondAsync(embed: pages[0].GetEmbedBuilder().Build());
            return;
        }

        var paginator = new StaticPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPages(pages)
            .WithFooter(PaginatorFooter.None)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();
        await interactive.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(30));
    }

    /// <summary>
    ///     Badge commands.
    /// </summary>
    /// <param name="views">Builds the embeds.</param>
    /// <param name="interactive">Sends paginated lists.</param>
    [Group("badge", "Show off badges on your profile")]
    public class BadgeCommands(AchievementViewService views, InteractiveService interactive)
        : MewdekoSlashSubmodule<AchievementService>
    {
        /// <summary>
        ///     Shows your or someone else's badges.
        /// </summary>
        /// <param name="user">The member, or yourself.</param>
        [SlashCommand("inventory", "Shows every badge you or someone else owns")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Inventory(IGuildUser? user = null)
        {
            var target = (SocketGuildUser)(user ?? ctx.User);
            var refusal = await views.PrivacyRefusalAsync(ctx.Guild.Id, ctx.User, target, AchievementPrivacyArea.Badges);
            if (refusal is not null)
            {
                await EphemeralReplyErrorAsync(refusal);
                return;
            }

            var pages = await views.BadgePagesAsync((SocketGuild)ctx.Guild, target);
            if (pages.Count == 1)
            {
                await RespondAsync(embed: pages[0].GetEmbedBuilder().Build());
                return;
            }

            var paginator = new StaticPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPages(pages)
                .WithFooter(PaginatorFooter.None)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();
            await interactive.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(30));
        }

        /// <summary>
        ///     Puts a badge in one of your four profile slots.
        /// </summary>
        /// <param name="slot">1 to 4.</param>
        /// <param name="badge">The badge.</param>
        [SlashCommand("equip", "Puts a badge in one of your four profile slots")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Equip(
            [Summary("slot", "Slot 1 to 4")] [MinValue(1)] [MaxValue(4)]
            int slot,
            [Summary("badge", "A badge you own")] [Autocomplete(typeof(AchievementOwnedBadgeAutocompleter))]
            string badge)
        {
            var result = await Service.EquipBadgeAsync(ctx.Guild.Id, ctx.User.Id, slot, badge);
            if (!result.Success)
            {
                await EphemeralReplyErrorAsync(views.DescribeError(ctx.Guild.Id, result.Error));
                return;
            }

            await EphemeralReplyConfirmAsync(Strings.AchievementBadgeEquipped(ctx.Guild.Id, result.Value!.Name, slot));
        }

        /// <summary>
        ///     Empties one of your profile slots.
        /// </summary>
        /// <param name="slot">1 to 4.</param>
        [SlashCommand("remove", "Empties one of your profile slots")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Remove([Summary("slot", "Slot 1 to 4")] [MinValue(1)] [MaxValue(4)] int slot)
        {
            await Service.UnequipBadgeAsync(ctx.Guild.Id, ctx.User.Id, slot);
            await EphemeralReplyConfirmAsync(Strings.AchievementBadgeRemoved(ctx.Guild.Id, slot));
        }

        /// <summary>
        ///     Shows what a badge is and how to earn it.
        /// </summary>
        /// <param name="badge">The badge.</param>
        [SlashCommand("preview", "Shows what a badge is and how to earn it")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Preview(
            [Summary("badge", "Any badge")] [Autocomplete(typeof(AchievementBadgeAutocompleter))]
            string badge)
        {
            var catalog = await Service.GetCatalogAsync(ctx.Guild.Id);
            var found = AchievementService.FindBadge(catalog, badge);
            if (found is null)
            {
                await EphemeralReplyErrorAsync(views.DescribeError(ctx.Guild.Id, AchievementError.BadgeInvalid));
                return;
            }

            var owned = (await Service.GetOwnedBadgesAsync(ctx.Guild.Id, ctx.User.Id)).Any(b => b.Key == found.Key);
            await RespondAsync(embed: views.BadgePreview(ctx.Guild.Id, found, owned).Build());
        }
    }

    /// <summary>
    ///     Staff commands for running achievements.
    /// </summary>
    /// <param name="views">Builds the embeds.</param>
    /// <param name="interactive">Sends paginated lists.</param>
    [Group("manage", "Run achievements for this server")]
    public class ManageCommands(AchievementViewService views, InteractiveService interactive)
        : MewdekoSlashSubmodule<AchievementService>
    {
        /// <summary>
        ///     Lists every achievement with its key, state, and unlock count.
        /// </summary>
        [SlashCommand("list", "Lists every achievement with its key, state, and unlock count")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public async Task List()
        {
            var pages = await views.AdminListAsync((SocketGuild)ctx.Guild);
            var paginator = new StaticPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPages(pages)
                .WithFooter(PaginatorFooter.None)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();
            await interactive.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(30), ephemeral: true);
        }

        /// <summary>
        ///     Starts or pauses earning achievements for the server.
        /// </summary>
        /// <param name="enabled">True to let members earn, false to pause.</param>
        [SlashCommand("toggle", "Starts or pauses earning achievements in this server")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public async Task Toggle(bool enabled)
        {
            await Service.SetEnabledAsync(ctx.Guild.Id, enabled);
            await ConfirmAsync(enabled
                ? Strings.AchievementEnabledServer(ctx.Guild.Id)
                : Strings.AchievementDisabledServer(ctx.Guild.Id));
        }

        /// <summary>
        ///     Sets or clears the channel unlocks are announced in.
        /// </summary>
        /// <param name="channel">The channel, or nothing to clear.</param>
        [SlashCommand("log-channel", "Sets or clears the channel unlocks are announced in")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public async Task LogChannel(ITextChannel? channel = null)
        {
            await Service.UpdateSettingsAsync(ctx.Guild.Id, row => row.LogChannelId = channel?.Id);
            await ConfirmAsync(channel is null
                ? Strings.AchievementLogCleared(ctx.Guild.Id)
                : Strings.AchievementLogSet(ctx.Guild.Id, channel.Mention));
        }

        /// <summary>
        ///     Sets where unlocks are announced and whether members get DMs by default.
        /// </summary>
        /// <param name="mode">Where unlocks go.</param>
        /// <param name="dmByDefault">DM members unless they turn it off.</param>
        /// <param name="mention">Mention members in unlock messages.</param>
        [SlashCommand("announce", "Sets where unlocks are announced")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public async Task Announce(AchievementAnnounceMode mode, bool? dmByDefault = null, bool? mention = null)
        {
            await Service.UpdateSettingsAsync(ctx.Guild.Id, row =>
            {
                row.AnnounceMode = (int)mode;
                if (dmByDefault is not null)
                    row.DmByDefault = dmByDefault.Value;
                if (mention is not null)
                    row.MentionUsers = mention.Value;
            });
            await ConfirmAsync(Strings.AchievementAnnounceSet(ctx.Guild.Id, mode.ToString()));
        }

        /// <summary>
        ///     Sets the unlock message, or clears it to use the default embed.
        /// </summary>
        /// <param name="message">Text or embed JSON with placeholders, or nothing to clear.</param>
        [SlashCommand("message", "Sets the unlock message, or clears it to use the default")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public async Task Message(string? message = null)
        {
            await Service.UpdateSettingsAsync(ctx.Guild.Id,
                row => row.UnlockMessage = string.IsNullOrWhiteSpace(message) ? null : message);
            await ConfirmAsync(string.IsNullOrWhiteSpace(message)
                ? Strings.AchievementMessageCleared(ctx.Guild.Id)
                : Strings.AchievementMessageSet(ctx.Guild.Id));
        }

        /// <summary>
        ///     Gives XP for every achievement point earned.
        /// </summary>
        /// <param name="amount">XP per point, 0 to turn off.</param>
        [SlashCommand("xp-per-point", "Gives XP for every achievement point earned")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public async Task XpPerPoint([MinValue(0)] [MaxValue(1000)] int amount)
        {
            await Service.UpdateSettingsAsync(ctx.Guild.Id, row => row.XpPerPoint = amount);
            await ConfirmAsync(Strings.AchievementXpPerPointSet(ctx.Guild.Id, amount));
        }

        /// <summary>
        ///     Activates or deactivates one achievement.
        /// </summary>
        /// <param name="achievement">The achievement.</param>
        /// <param name="enabled">True to activate, false to deactivate.</param>
        [SlashCommand("set-enabled", "Activates or deactivates one achievement")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public async Task SetEnabled(
            [Summary("achievement", "The achievement")] [Autocomplete(typeof(AchievementAutocompleter))]
            string achievement,
            bool enabled)
        {
            var result = await Service.SetAchievementEnabledAsync(ctx.Guild.Id, achievement, enabled);
            if (!result.Success)
            {
                await EphemeralReplyErrorAsync(views.DescribeError(ctx.Guild.Id, result.Error));
                return;
            }

            await ConfirmAsync(enabled
                ? Strings.AchievementTurnedOn(ctx.Guild.Id, result.Value!.Name)
                : Strings.AchievementTurnedOff(ctx.Guild.Id, result.Value!.Name));
        }

        /// <summary>
        ///     Activates or deactivates a whole category.
        /// </summary>
        /// <param name="category">The category.</param>
        /// <param name="enabled">True to activate, false to deactivate.</param>
        [SlashCommand("category", "Activates or deactivates a whole category")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public async Task CategoryToggle(
            [Summary("category", "The category")] [Autocomplete(typeof(AchievementCategoryAutocompleter))]
            string category,
            bool enabled)
        {
            var catalog = await Service.GetCatalogAsync(ctx.Guild.Id);
            var found = AchievementViewService.FindCategory(catalog, category);
            if (found is null)
            {
                await EphemeralReplyErrorAsync(Strings.AchievementCategoryUnknown(ctx.Guild.Id, category));
                return;
            }

            var settings = Service.GetSettings(ctx.Guild.Id);
            var disabled = settings.DisabledCategories.ToHashSet();
            if (enabled)
                disabled.Remove(found.Key);
            else
                disabled.Add(found.Key);
            await Service.SetCategoryLayoutAsync(ctx.Guild.Id, settings.CategoryOrder, disabled);
            await ConfirmAsync(enabled
                ? Strings.AchievementCategoryOn(ctx.Guild.Id, found.Name)
                : Strings.AchievementCategoryOff(ctx.Guild.Id, found.Name));
        }

        /// <summary>
        ///     Hands an achievement to a member.
        /// </summary>
        /// <param name="user">The member.</param>
        /// <param name="achievement">The achievement.</param>
        [SlashCommand("grant", "Hands an achievement to a member")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public async Task Grant(IGuildUser user,
            [Summary("achievement", "The achievement")] [Autocomplete(typeof(AchievementAutocompleter))]
            string achievement)
        {
            var result = await Service.GrantAsync((SocketGuildUser)user, achievement, ctx.User.Id);
            if (!result.Success)
            {
                await EphemeralReplyErrorAsync(views.DescribeError(ctx.Guild.Id, result.Error));
                return;
            }

            await ConfirmAsync(Strings.AchievementGranted(ctx.Guild.Id, result.Value!.Name, user.Mention));
        }

        /// <summary>
        ///     Takes an achievement from a member.
        /// </summary>
        /// <param name="user">The member.</param>
        /// <param name="achievement">The achievement.</param>
        [SlashCommand("revoke", "Takes an achievement from a member")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public async Task Revoke(IGuildUser user,
            [Summary("achievement", "The achievement")] [Autocomplete(typeof(AchievementAutocompleter))]
            string achievement)
        {
            var catalog = await Service.GetCatalogAsync(ctx.Guild.Id);
            var result = await Service.RevokeAsync(ctx.Guild.Id, user.Id, achievement);
            if (!result.Success || !catalog.ByKey.TryGetValue(achievement, out var def))
            {
                await EphemeralReplyErrorAsync(views.DescribeError(ctx.Guild.Id,
                    result.Success ? AchievementError.NotFound : result.Error));
                return;
            }

            await ConfirmAsync(Strings.AchievementRevoked(ctx.Guild.Id, def.Name, user.Mention));
        }

        /// <summary>
        ///     Clears every achievement a member has in the server.
        /// </summary>
        /// <param name="user">The member.</param>
        [SlashCommand("reset", "Clears every achievement a member has in this server")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public async Task Reset(IGuildUser user)
        {
            if (!await PromptUserConfirmAsync(Strings.AchievementResetConfirm(ctx.Guild.Id, user.Mention), ctx.User.Id))
                return;

            var cleared = await Service.ResetMemberAsync(ctx.Guild.Id, user.Id);
            await ConfirmAsync(Strings.AchievementResetDone(ctx.Guild.Id, cleared, user.Mention));
        }

        /// <summary>
        ///     Makes an achievement unlocked by reaching a number.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="metric">What to count.</param>
        /// <param name="goal">The number to reach.</param>
        /// <param name="grade">Bronze to Champion.</param>
        /// <param name="description">What it takes, or blank to describe the goal.</param>
        /// <param name="icon">An icon name, a server emoji, or an image link.</param>
        /// <param name="hidden">Keep it secret until unlocked.</param>
        [SlashCommand("create-goal", "Makes an achievement unlocked by reaching a number")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public async Task CreateGoal(string name,
            [Summary("metric", "What to count")] [Autocomplete(typeof(AchievementMetricAutocompleter))]
            string metric,
            [MinValue(1)] long goal,
            AchievementGrade grade = AchievementGrade.Bronze,
            string? description = null,
            [Summary("icon", "An icon name, a server emoji, or an image link")]
            [Autocomplete(typeof(AchievementIconAutocompleter))]
            string? icon = null,
            bool hidden = false)
        {
            if (!Enum.TryParse<AchievementMetric>(metric, true, out var parsed))
            {
                await EphemeralReplyErrorAsync(views.DescribeError(ctx.Guild.Id, AchievementError.MetricInvalid));
                return;
            }

            await CreateAsync(new CustomAchievementDraft
            {
                Name = name,
                Description = description,
                Icon = icon,
                Hidden = hidden,
                Grade = grade,
                Trigger = AchievementTrigger.Metric,
                Metric = parsed,
                Threshold = goal
            });
        }

        /// <summary>
        ///     Makes an achievement unlocked by saying a phrase.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="phrase">The phrase.</param>
        /// <param name="grade">Bronze to Champion.</param>
        /// <param name="channel">Only count it in this channel.</param>
        /// <param name="description">What it takes.</param>
        /// <param name="icon">An icon name, a server emoji, or an image link.</param>
        /// <param name="hidden">Keep it secret until unlocked.</param>
        [SlashCommand("create-phrase", "Makes an achievement unlocked by saying a phrase")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task CreatePhrase(string name, string phrase, AchievementGrade grade = AchievementGrade.Bronze,
            ITextChannel? channel = null, string? description = null,
            [Summary("icon", "An icon name, a server emoji, or an image link")]
            [Autocomplete(typeof(AchievementIconAutocompleter))]
            string? icon = null,
            bool hidden = true)
        {
            return CreateAsync(new CustomAchievementDraft
            {
                Name = name,
                Description = description,
                Icon = icon,
                Hidden = hidden,
                Grade = grade,
                Trigger = AchievementTrigger.Keyword,
                Keyword = phrase,
                ChannelId = channel?.Id
            });
        }

        /// <summary>
        ///     Makes an achievement unlocked by reacting with an emoji.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="reaction">The emoji to react with.</param>
        /// <param name="grade">Bronze to Champion.</param>
        /// <param name="channel">Only count it in this channel.</param>
        /// <param name="description">What it takes.</param>
        /// <param name="hidden">Keep it secret until unlocked.</param>
        [SlashCommand("create-reaction", "Makes an achievement unlocked by reacting with an emoji")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task CreateReaction(string name, string reaction, AchievementGrade grade = AchievementGrade.Bronze,
            ITextChannel? channel = null, string? description = null, bool hidden = false)
        {
            return CreateAsync(new CustomAchievementDraft
            {
                Name = name,
                Description = description,
                Hidden = hidden,
                Grade = grade,
                Trigger = AchievementTrigger.Reaction,
                Keyword = reaction,
                ChannelId = channel?.Id
            });
        }

        /// <summary>
        ///     Makes an achievement only staff can hand out.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="grade">Bronze to Champion.</param>
        /// <param name="description">What it is for.</param>
        /// <param name="icon">An icon name, a server emoji, or an image link.</param>
        [SlashCommand("create-manual", "Makes an achievement only staff can hand out")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task CreateManual(string name, AchievementGrade grade = AchievementGrade.Bronze,
            string? description = null,
            [Summary("icon", "An icon name, a server emoji, or an image link")]
            [Autocomplete(typeof(AchievementIconAutocompleter))]
            string? icon = null)
        {
            return CreateAsync(new CustomAchievementDraft
            {
                Name = name,
                Description = description,
                Icon = icon,
                Grade = grade,
                Trigger = AchievementTrigger.Manual
            });
        }

        /// <summary>
        ///     Deletes an achievement the server made.
        /// </summary>
        /// <param name="achievement">The achievement.</param>
        [SlashCommand("delete", "Deletes an achievement this server made")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public async Task Delete(
            [Summary("achievement", "The achievement")] [Autocomplete(typeof(AchievementAutocompleter))]
            string achievement)
        {
            if (!AchievementCatalog.TryParseCustomKey(achievement, out var id))
            {
                await EphemeralReplyErrorAsync(Strings.AchievementDeleteBuiltIn(ctx.Guild.Id));
                return;
            }

            var catalog = await Service.GetCatalogAsync(ctx.Guild.Id);
            if (!catalog.ByKey.TryGetValue(achievement, out var def))
            {
                await EphemeralReplyErrorAsync(views.DescribeError(ctx.Guild.Id, AchievementError.NotFound));
                return;
            }

            if (!await PromptUserConfirmAsync(Strings.AchievementDeleteConfirm(ctx.Guild.Id, def.Name), ctx.User.Id))
                return;

            await Service.DeleteCustomAsync(ctx.Guild.Id, id);
            await ConfirmAsync(Strings.AchievementDeleted(ctx.Guild.Id, def.Name));
        }

        /// <summary>
        ///     Sets the rewards for unlocking an achievement.
        /// </summary>
        /// <param name="achievement">The achievement.</param>
        /// <param name="role">A role to give.</param>
        /// <param name="currency">Currency to give.</param>
        /// <param name="xp">XP to give.</param>
        /// <param name="clear">Remove every reward.</param>
        [SlashCommand("reward", "Sets what members get for unlocking an achievement")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public async Task Reward(
            [Summary("achievement", "The achievement")] [Autocomplete(typeof(AchievementAutocompleter))]
            string achievement,
            IRole? role = null,
            [MinValue(0)] long? currency = null,
            [MinValue(0)] int? xp = null,
            bool clear = false)
        {
            var result = clear
                ? await Service.SetRewardsAsync((SocketGuild)ctx.Guild, achievement, 0, 0, 0)
                : await Service.SetRewardsAsync((SocketGuild)ctx.Guild, achievement, role?.Id, currency, xp);
            if (!result.Success)
            {
                await EphemeralReplyErrorAsync(views.DescribeError(ctx.Guild.Id, result.Error));
                return;
            }

            var updated = result.Value!;
            var roleText = updated.RoleRewardId is { } roleRewardId ? MentionUtils.MentionRole(roleRewardId) : "-";
            await ConfirmAsync(Strings.AchievementRewardsSet(ctx.Guild.Id, updated.Name, roleText,
                updated.CurrencyReward.ToString("N0"), updated.XpReward.ToString("N0")));
        }

        /// <summary>
        ///     Sets the icon an achievement shows, or clears it to use its category's.
        /// </summary>
        /// <param name="achievement">The achievement.</param>
        /// <param name="icon">An icon name, a server emoji, or an image link. Leave empty to clear.</param>
        [SlashCommand("icon", "Sets the icon an achievement shows")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public async Task Icon(
            [Summary("achievement", "The achievement")] [Autocomplete(typeof(AchievementAutocompleter))]
            string achievement,
            [Summary("icon", "An icon name, a server emoji, or an image link; empty uses the category's")]
            [Autocomplete(typeof(AchievementIconAutocompleter))]
            string? icon = null)
        {
            var result = await Service.SetIconAsync((SocketGuild)ctx.Guild, achievement, icon);
            if (!result.Success)
            {
                await EphemeralReplyErrorAsync(views.DescribeError(ctx.Guild.Id, result.Error));
                return;
            }

            await ConfirmAsync(string.IsNullOrWhiteSpace(icon)
                ? Strings.AchievementIconCleared(ctx.Guild.Id, result.Value!.Name)
                : Strings.AchievementIconSet(ctx.Guild.Id, result.Value!.Name));
        }

        /// <summary>
        ///     Stops a role from earning achievements, or lets it earn again.
        /// </summary>
        /// <param name="role">The role.</param>
        [SlashCommand("exclude-role", "Stops a role from earning achievements, or lets it earn again")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public async Task ExcludeRole(IRole role)
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
        [SlashCommand("exclude-channel", "Stops a channel from earning achievements, or lets it earn again")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public async Task ExcludeChannel(IGuildChannel channel)
        {
            var excluded = await Service.ToggleExclusionAsync(ctx.Guild.Id, channel.Id, false);
            var mention = MentionUtils.MentionChannel(channel.Id);
            await ConfirmAsync(excluded
                ? Strings.AchievementExcluded(ctx.Guild.Id, mention)
                : Strings.AchievementIncluded(ctx.Guild.Id, mention));
        }

        private async Task CreateAsync(CustomAchievementDraft draft)
        {
            var result = await Service.CreateCustomAsync((SocketGuild)ctx.Guild, ctx.User.Id, draft);
            if (!result.Success)
            {
                await EphemeralReplyErrorAsync(views.DescribeError(ctx.Guild.Id, result.Error));
                return;
            }

            var def = result.Value!;
            await ConfirmAsync(Strings.AchievementCreated(ctx.Guild.Id, def.Name, def.Key, def.Description));
        }
    }
}
