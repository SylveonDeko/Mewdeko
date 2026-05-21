using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.Starboard.Services;

namespace Mewdeko.Modules.Starboard;

/// <summary>
///     Class for managing starboard commands via slash commands.
/// </summary>
[Group("starboard", "Manage the starboard!")]
public class StarboardSlash(GuildSettingsService guildSettings, InteractiveService interactiveService)
    : MewdekoSlashSubmodule<StarboardService>
{
    /// <summary>
    ///     Creates a new starboard configuration.
    /// </summary>
    /// <param name="channel">The channel to send starred messages to.</param>
    /// <param name="emoteText">The emote to use for starring messages.</param>
    /// <param name="threshold">The number of stars required.</param>
    [SlashCommand("create", "Create a new starboard configuration")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task CreateStarboard(ITextChannel channel, string emoteText, int threshold = 1)
    {
        await DeferAsync();
        var emote = emoteText.ToIEmote();
        var msg = await ctx.Interaction.SendEphemeralFollowupConfirmAsync("Testing emote...");
        try
        {
            await msg.AddReactionAsync(emote);
        }
        catch
        {
            await msg.DeleteAsync();
            await ctx.Interaction.SendErrorAsync(Strings.EmoteCannotBeUsed(ctx.Guild.Id), Config);
            return;
        }

        try
        {
            await Service.CreateStarboard(ctx.Guild, channel.Id, emote.ToString(), threshold);
            await msg.DeleteAsync();
            await ctx.Interaction.SendConfirmAsync(Strings.StarboardCreated(ctx.Guild.Id, channel.Mention,
                emote.ToString(),
                threshold));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already used"))
        {
            await msg.DeleteAsync();
            await ctx.Interaction.SendErrorAsync(Strings.StarboardEmoteInUse(ctx.Guild.Id), Config);
        }
    }

    /// <summary>
    ///     Deletes a starboard configuration.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to delete.</param>
    [SlashCommand("delete", "Delete a starboard configuration")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task DeleteStarboard(
        [Summary("starboard", "The starboard to delete")] [Autocomplete(typeof(StarboardAutocompleter))]
        int starboardId)
    {
        if (await Service.DeleteStarboard(ctx.Guild, starboardId))
            await ctx.Interaction.SendConfirmAsync(Strings.StarboardRemoved(ctx.Guild.Id, starboardId));
        else
            await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Lists all starboard configurations in the guild.
    /// </summary>
    /// <summary>
    ///     Lists all starboard configurations in the guild.
    /// </summary>
    [SlashCommand("list", "List all starboard configurations")]
    public async Task ListStarboardsSlash()
    {
        var starboards = Service.GetStarboards(ctx.Guild.Id);
        if (!starboards.Any())
        {
            await ctx.Interaction.SendErrorAsync(Strings.NoStarboardsConfigured(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        await ctx.Interaction.SendConfirmAsync(Strings.Loading(ctx.Guild.Id)).ConfigureAwait(false);

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(starboards.Count - 1)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await ctx.Interaction.DeleteOriginalResponseAsync().ConfigureAwait(false);
        await interactiveService.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            var starboard = starboards.Skip(page).FirstOrDefault();
            var channel = await ctx.Guild.GetTextChannelAsync(starboard.StarboardChannelId).ConfigureAwait(false);
            return new PageBuilder()
                .WithOkColor()
                .WithTitle(Strings.StarboardConfigurations(ctx.Guild.Id, starboard.Id))
                .WithDescription(Strings.StarboardConfigDetails(
                    ctx.Guild.Id,
                    channel?.Mention ?? "Channel Not Found",
                    starboard.Emote,
                    starboard.Threshold,
                    starboard.AllowBots,
                    starboard.UseBlacklist,
                    starboard.RemoveOnDelete,
                    starboard.RemoveOnReactionsClear,
                    starboard.RemoveOnBelowThreshold,
                    starboard.RepostThreshold
                ));
        }
    }

    /// <summary>
    ///     Set after how many messages Mewdeko reposts a starboard message.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to modify.</param>
    /// <param name="threshold">The threshold value for reposting.</param>
    [SlashCommand("repostthreshold", "Set after how many messages mewdeko reposts a starboard message")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task SetRepostThreshold(
        [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
        int starboardId,
        [Summary("threshold", "The number of messages before reposting")]
        int threshold)
    {
        if (await Service.SetRepostThreshold(ctx.Guild, starboardId, threshold))
        {
            if (threshold == 0)
                await ctx.Interaction.SendConfirmAsync(Strings.StarboardRepostingDisabled(ctx.Guild.Id, starboardId));
            else
                await ctx.Interaction.SendConfirmAsync(
                    Strings.StarboardRepostThresholdSet(ctx.Guild.Id, starboardId, threshold));
        }
        else
            await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Sets the number of stars required for a message to be added to the starboard.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to modify.</param>
    /// <param name="threshold">The number of stars required.</param>
    [SlashCommand("threshold", "Set how many stars are needed for a message to be added")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task SetStarThreshold(
        [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
        int starboardId,
        [Summary("threshold", "The number of stars required")]
        int threshold)
    {
        if (await Service.SetStarThreshold(ctx.Guild, starboardId, threshold))
            await ctx.Interaction.SendConfirmAsync(Strings.StarboardThresholdSet(ctx.Guild.Id, starboardId, threshold));
        else
            await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Adds a channel to the whitelist/blacklist for a starboard.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to modify.</param>
    /// <param name="channel">The channel to toggle.</param>
    [SlashCommand("channel-toggle", "Toggle a channel in the whitelist/blacklist")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardChToggle(
        [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
        int starboardId,
        ITextChannel channel)
    {
        var (wasAdded, config) = await Service.ToggleChannel(ctx.Guild, starboardId, channel.Id.ToString());
        if (config == null)
        {
            await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
            return;
        }

        var mode = config.UseBlacklist ? "blacklist" : "whitelist";
        if (wasAdded)
        {
            await ctx.Interaction.SendConfirmAsync(
                Strings.StarboardChannelAdded(
                    ctx.Guild.Id,
                    channel.Mention,
                    starboardId,
                    mode,
                    await guildSettings.GetPrefix(ctx.Guild)
                )
            );
        }
        else
        {
            await ctx.Interaction.SendConfirmAsync(
                Strings.StarboardChannelRemoved(
                    ctx.Guild.Id,
                    channel.Mention,
                    starboardId,
                    mode,
                    await guildSettings.GetPrefix(ctx.Guild)
                )
            );
        }
    }

    /// <summary>
    ///     Sets whether a starboard uses whitelist or blacklist mode.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to modify.</param>
    /// <param name="mode">The mode to set.</param>
    [SlashCommand("whitelist-mode", "Set whether to use whitelist or blacklist mode")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardWlMode(
        [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
        int starboardId,
        Starboard.WhitelistMode mode)
    {
        if (await Service.SetUseBlacklist(ctx.Guild, starboardId, mode > 0))
        {
            if (mode > 0)
                await ctx.Interaction.SendConfirmAsync(Strings.StarboardBlacklistEnabled(ctx.Guild.Id, starboardId));
            else
                await ctx.Interaction.SendConfirmAsync(Strings.StarboardWhitelistEnabled(ctx.Guild.Id, starboardId));
        }
        else
            await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Sets whether to remove starboard posts when reactions are cleared.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to modify.</param>
    /// <param name="enabled">Whether to enable the feature.</param>
    [SlashCommand("removeonreactionclear", "Set whether to remove posts when reactions are cleared")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardRemoveOnReactionsClear(
        [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
        int starboardId,
        bool enabled)
    {
        if (await Service.SetRemoveOnClear(ctx.Guild, starboardId, enabled))
        {
            if (enabled)
                await ctx.Interaction.SendConfirmAsync(
                    Strings.StarboardReactionsClearRemoveEnabled(ctx.Guild.Id, starboardId));
            else
                await ctx.Interaction.SendConfirmAsync(
                    Strings.StarboardReactionsClearRemoveDisabled(ctx.Guild.Id, starboardId));
        }
        else
            await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Sets whether to remove starboard posts when the original message is deleted.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to modify.</param>
    /// <param name="enabled">Whether to enable the feature.</param>
    [SlashCommand("removeondelete", "Set whether to remove posts when source is deleted")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardRemoveOnDelete(
        [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
        int starboardId,
        bool enabled)
    {
        if (await Service.SetRemoveOnDelete(ctx.Guild, starboardId, enabled))
        {
            if (enabled)
                await ctx.Interaction.SendConfirmAsync(Strings.StarboardDeleteRemoveEnabled(ctx.Guild.Id, starboardId));
            else
                await ctx.Interaction.SendConfirmAsync(
                    Strings.StarboardDeleteRemoveDisabled(ctx.Guild.Id, starboardId));
        }
        else
            await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Sets whether to remove starboard posts when they fall below the threshold.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to modify.</param>
    /// <param name="enabled">Whether to enable the feature.</param>
    [SlashCommand("removeonbelowthreshold", "Set whether to remove posts when below threshold")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardRemoveOnBelowThreshold(
        [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
        int starboardId,
        bool enabled)
    {
        if (await Service.SetRemoveBelowThreshold(ctx.Guild, starboardId, enabled))
        {
            if (enabled)
                await ctx.Interaction.SendConfirmAsync(
                    Strings.StarboardBelowThresholdRemoveEnabled(ctx.Guild.Id, starboardId));
            else
                await ctx.Interaction.SendConfirmAsync(
                    Strings.StarboardBelowThresholdRemoveDisabled(ctx.Guild.Id, starboardId));
        }
        else
            await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Sets whether to allow bot messages to be starred.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to modify.</param>
    /// <param name="enabled">Whether to enable the feature.</param>
    [SlashCommand("allowbots", "Set whether to allow bot messages to be starred")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardAllowBots(
        [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
        int starboardId,
        bool enabled)
    {
        if (await Service.SetAllowBots(ctx.Guild, starboardId, enabled))
        {
            if (enabled)
                await ctx.Interaction.SendConfirmAsync(Strings.StarboardBotsEnabled(ctx.Guild.Id, starboardId));
            else
                await ctx.Interaction.SendConfirmAsync(Strings.StarboardBotsDisabled(ctx.Guild.Id, starboardId));
        }
        else
            await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Adds an emote to an existing starboard configuration.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to modify.</param>
    /// <param name="emoteText">The emote to add.</param>
    [SlashCommand("add-emote", "Add an emote to an existing starboard")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task AddStarboardEmote(
        [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
        int starboardId,
        [Summary("emote", "The emote to add")] string emoteText)
    {
        await DeferAsync();
        var emote = emoteText.ToIEmote();
        var msg = await ctx.Interaction.SendEphemeralFollowupConfirmAsync("Testing emote...");
        try
        {
            await msg.AddReactionAsync(emote);
        }
        catch
        {
            await msg.DeleteAsync();
            await ctx.Interaction.SendErrorAsync(Strings.EmoteCannotBeUsed(ctx.Guild.Id), Config);
            return;
        }

        try
        {
            if (await Service.AddEmoteToStarboard(ctx.Guild, starboardId, emote.ToString()))
            {
                await msg.DeleteAsync();
                await ctx.Interaction.SendConfirmAsync(Strings.StarboardEmoteAdded(ctx.Guild.Id, emote.ToString(),
                    starboardId));
            }
            else
            {
                await msg.DeleteAsync();
                await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already used"))
        {
            await msg.DeleteAsync();
            await ctx.Interaction.SendErrorAsync(Strings.StarboardEmoteInUse(ctx.Guild.Id), Config);
        }
    }

    /// <summary>
    ///     Removes an emote from an existing starboard configuration.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to modify.</param>
    /// <param name="emoteText">The emote to remove.</param>
    [SlashCommand("remove-emote", "Remove an emote from an existing starboard")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task RemoveStarboardEmote(
        [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
        int starboardId,
        [Summary("emote", "The emote to remove")]
        string emoteText)
    {
        var emote = emoteText.ToIEmote();

        try
        {
            if (await Service.RemoveEmoteFromStarboard(ctx.Guild, starboardId, emote.ToString()))
                await ctx.Interaction.SendConfirmAsync(Strings.StarboardEmoteRemoved(ctx.Guild.Id, emote.ToString(),
                    starboardId));
            else
                await ctx.Interaction.SendErrorAsync(
                    Strings.StarboardEmoteNotFound(ctx.Guild.Id, emote.ToString(), starboardId), Config);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot remove the last emote"))
        {
            await ctx.Interaction.SendErrorAsync(Strings.StarboardCannotRemoveLastEmote(ctx.Guild.Id, starboardId),
                Config);
        }
    }

    /// <summary>
    ///     Shows starboard statistics for the guild.
    /// </summary>
    [SlashCommand("stats", "Show starboard statistics for this guild")]
    [CheckPermissions]
    public async Task StarboardStats()
    {
        var stats = await Service.GetStarboardStats(ctx.Guild.Id);
        if (stats == null)
        {
            await ctx.Interaction.SendErrorAsync(Strings.NoStarboardStats(ctx.Guild.Id), Config);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.StarboardStatistics(ctx.Guild.Id))
            .AddField(Strings.TotalStarredMessages(ctx.Guild.Id), stats.TotalStarredMessages, true)
            .AddField(Strings.TotalStars(ctx.Guild.Id), stats.TotalStars, true);

        if (stats.MostStarredUser != null)
        {
            var user = await ctx.Guild.GetUserAsync(stats.MostStarredUser.UserId);
            eb.AddField(Strings.MostStarredUser(ctx.Guild.Id),
                $"{user?.Mention ?? "Unknown User"} ({stats.MostStarredUser.TotalStars} ⭐)", true);
        }

        if (stats.MostActiveChannel != null)
        {
            var channel = await ctx.Guild.GetTextChannelAsync(stats.MostActiveChannel.ChannelId);
            eb.AddField(Strings.MostActiveChannel(ctx.Guild.Id),
                $"{channel?.Mention ?? "Unknown Channel"} ({stats.MostActiveChannel.TotalStars} ⭐)", true);
        }

        if (stats.MostActiveStarrer != null)
        {
            var user = await ctx.Guild.GetUserAsync(stats.MostActiveStarrer.UserId);
            eb.AddField(Strings.MostActiveStarrer(ctx.Guild.Id),
                $"{user?.Mention ?? "Unknown User"} ({stats.MostActiveStarrer.StarsGiven} given)", true);
        }

        await ctx.Interaction.RespondAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Shows starboard statistics for a specific user.
    /// </summary>
    /// <param name="user">The user to show stats for. Defaults to command invoker.</param>
    [SlashCommand("user-stats", "Show starboard statistics for a specific user")]
    [CheckPermissions]
    public async Task UserStarboardStats(
        [Summary("user", "The user to show stats for (defaults to yourself)")]
        IUser user = null)
    {
        user ??= ctx.User;
        var stats = await Service.GetUserStarboardStats(ctx.Guild.Id, user.Id);
        if (stats == null)
        {
            await ctx.Interaction.SendErrorAsync(Strings.NoStarboardStats(ctx.Guild.Id), Config);
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.StarboardUserStatistics(ctx.Guild.Id, user.Username))
            .WithThumbnailUrl(user.GetAvatarUrl())
            .AddField(Strings.MessagesStarred(ctx.Guild.Id), stats.MessagesStarred, true)
            .AddField(Strings.StarsReceived(ctx.Guild.Id), stats.StarsReceived, true)
            .AddField(Strings.StarsGiven(ctx.Guild.Id), stats.StarsGiven, true);

        if (stats.TopStarredPosts.Any())
        {
            var topPosts = string.Join("\n", stats.TopStarredPosts.Select(p =>
                $"{p.Emote}: [{p.MessageId}](https://discord.com/channels/{ctx.Guild.Id}/{ctx.Channel.Id}/{p.MessageId}) ({p.StarCount} ⭐)"));
            eb.AddField(Strings.TopStarredPosts(ctx.Guild.Id), topPosts);
        }

        if (stats.MostStarredUsers.Any())
        {
            var starredUsers = string.Join("\n", stats.MostStarredUsers.Select(u =>
                $"<@{u.UserId}> ({u.Count} ⭐)"));
            eb.AddField(Strings.MostStarredUsers(ctx.Guild.Id), starredUsers, true);
        }

        if (stats.TopFans.Any())
        {
            var fans = string.Join("\n", stats.TopFans.Select(f =>
                $"<@{f.UserId}> ({f.Count} ⭐)"));
            eb.AddField(Strings.TopFans(ctx.Guild.Id), fans, true);
        }

        await ctx.Interaction.RespondAsync(embed: eb.Build());
    }
}