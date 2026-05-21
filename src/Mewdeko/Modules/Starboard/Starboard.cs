using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Starboard.Services;

namespace Mewdeko.Modules.Starboard;

/// <summary>
///     Module for managing multiple starboard configurations in a guild.
/// </summary>
/// <param name="guildSettings">The guildsettings service.</param>
/// <param name="interactiveService">The interactiveservice service.</param>
public class Starboard(GuildSettingsService guildSettings, InteractiveService interactiveService)
    : MewdekoSubmodule<StarboardService>
{
    /// <summary>
    ///     Enum representing the mode for whitelisting or blacklisting channels for starboard.
    /// </summary>
    public enum WhitelistMode
    {
        /// <summary>
        ///     Whitelist mode. Only whitelisted channels are checked for starboard posts.
        /// </summary>
        Whitelist = 0,

        /// <summary>
        ///     Alias for Whitelist mode.
        /// </summary>
        Wl = 0,

        /// <summary>
        ///     Alias for Whitelist mode.
        /// </summary>
        White = 0,

        /// <summary>
        ///     Blacklist mode. Blacklisted channels are not checked for starboard posts.
        /// </summary>
        Blacklist = 1,

        /// <summary>
        ///     Alias for Blacklist mode.
        /// </summary>
        Bl = 1,

        /// <summary>
        ///     Alias for Blacklist mode.
        /// </summary>
        Black = 1
    }

    /// <summary>
    ///     Creates a new starboard configuration for the guild.
    /// </summary>
    /// <param name="channel">The channel where starred messages will be posted.</param>
    /// <param name="emote">The emote to use for starring messages.</param>
    /// <param name="threshold">The number of stars required to post a message.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CreateStarboard(ITextChannel channel, IEmote emote, int threshold = 1)
    {
        try
        {
            await ctx.Message.AddReactionAsync(emote);
        }
        catch
        {
            await ctx.Channel.SendErrorAsync(Strings.InvalidEmote(ctx.Guild.Id), Config);
            return;
        }

        try
        {
            await Service.CreateStarboard(ctx.Guild, channel.Id, emote.ToString(), threshold);
            await ctx.Channel.SendConfirmAsync(Strings.StarboardCreated(ctx.Guild.Id, channel.Mention, emote.ToString(),
                threshold));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already used"))
        {
            await ctx.Channel.SendErrorAsync(Strings.StarboardEmoteInUse(ctx.Guild.Id), Config);
        }
    }

    /// <summary>
    ///     Removes a starboard configuration from the guild.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to remove.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task DeleteStarboard(int starboardId)
    {
        if (await Service.DeleteStarboard(ctx.Guild, starboardId))
            await ctx.Channel.SendConfirmAsync(Strings.StarboardRemoved(ctx.Guild.Id, starboardId));
        else
            await ctx.Channel.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Lists all starboard configurations in the guild.
    /// </summary>
    [Cmd]
    [Aliases]
    public async Task ListStarboards()
    {
        var starboards = Service.GetStarboards(ctx.Guild.Id);
        if (!starboards.Any())
        {
            await ctx.Channel.SendErrorAsync(Strings.NoStarboardsConfigured(ctx.Guild.Id), Config)
                .ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(starboards.Count - 1)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

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
    ///     Sets whether bots are allowed to be counted for a specific starboard.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="enabled">Whether to allow bots to be counted.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardAllowBots(int starboardId, bool enabled)
    {
        if (await Service.SetAllowBots(ctx.Guild, starboardId, enabled))
        {
            if (enabled)
                await ctx.Channel.SendConfirmAsync(Strings.StarboardBotsEnabled(ctx.Guild.Id, starboardId));
            else
                await ctx.Channel.SendConfirmAsync(Strings.StarboardBotsDisabled(ctx.Guild.Id, starboardId));
        }
        else
            await ctx.Channel.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Sets whether to remove starboard posts when reactions are cleared.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="enabled">Whether to remove posts when reactions are cleared.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardRemoveOnReactionsClear(int starboardId, bool enabled)
    {
        if (await Service.SetRemoveOnClear(ctx.Guild, starboardId, enabled))
        {
            if (enabled)
                await ctx.Channel.SendConfirmAsync(
                    Strings.StarboardReactionsClearRemoveEnabled(ctx.Guild.Id, starboardId));
            else
                await ctx.Channel.SendConfirmAsync(
                    Strings.StarboardReactionsClearRemoveDisabled(ctx.Guild.Id, starboardId));
        }
        else
            await ctx.Channel.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Sets whether to remove starboard posts when the original message is deleted.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="enabled">Whether to remove posts when the original message is deleted.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardRemoveOnDelete(int starboardId, bool enabled)
    {
        if (await Service.SetRemoveOnDelete(ctx.Guild, starboardId, enabled))
        {
            if (enabled)
                await ctx.Channel.SendConfirmAsync(Strings.StarboardDeleteRemoveEnabled(ctx.Guild.Id, starboardId));
            else
                await ctx.Channel.SendConfirmAsync(Strings.StarboardDeleteRemoveDisabled(ctx.Guild.Id, starboardId));
        }
        else
            await ctx.Channel.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Sets whether to remove starboard posts when they fall below the threshold.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="enabled">Whether to remove posts when they fall below threshold.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardRemoveOnBelowThreshold(int starboardId, bool enabled)
    {
        if (await Service.SetRemoveBelowThreshold(ctx.Guild, starboardId, enabled))
        {
            if (enabled)
                await ctx.Channel.SendConfirmAsync(
                    Strings.StarboardBelowThresholdRemoveEnabled(ctx.Guild.Id, starboardId));
            else
                await ctx.Channel.SendConfirmAsync(
                    Strings.StarboardBelowThresholdRemoveDisabled(ctx.Guild.Id, starboardId));
        }
        else
            await ctx.Channel.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Sets the whitelist/blacklist mode for a starboard configuration.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="mode">The whitelist/blacklist mode to set.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardWlMode(int starboardId, WhitelistMode mode)
    {
        if (await Service.SetUseBlacklist(ctx.Guild, starboardId, mode > 0))
        {
            if (mode > 0)
                await ctx.Channel.SendConfirmAsync(Strings.StarboardBlacklistEnabled(ctx.Guild.Id, starboardId));
            else
                await ctx.Channel.SendConfirmAsync(Strings.StarboardWhitelistEnabled(ctx.Guild.Id, starboardId));
        }
        else
            await ctx.Channel.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Toggles whether a channel is checked for a specific starboard.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="channel">The channel to toggle.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task StarboardChToggle(int starboardId, [Remainder] ITextChannel channel)
    {
        var (wasAdded, config) = await Service.ToggleChannel(ctx.Guild, starboardId, channel.Id.ToString());
        if (config == null)
        {
            await ctx.Channel.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
            return;
        }

        var mode = config.UseBlacklist ? "blacklist" : "whitelist";
        if (wasAdded)
        {
            await ctx.Channel.SendConfirmAsync(
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
            await ctx.Channel.SendConfirmAsync(
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
    ///     Sets the repost threshold for a starboard configuration.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="threshold">The repost threshold to set.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task SetRepostThreshold(int starboardId, int threshold)
    {
        if (await Service.SetRepostThreshold(ctx.Guild, starboardId, threshold))
        {
            if (threshold == 0)
                await ctx.Channel.SendConfirmAsync(Strings.StarboardRepostingDisabled(ctx.Guild.Id, starboardId));
            else
                await ctx.Channel.SendConfirmAsync(
                    Strings.StarboardRepostThresholdSet(ctx.Guild.Id, starboardId, threshold));
        }
        else
            await ctx.Channel.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Sets the star threshold for a starboard configuration.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="threshold">The star threshold to set.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task SetStarThreshold(int starboardId, int threshold)
    {
        if (await Service.SetStarThreshold(ctx.Guild, starboardId, threshold))
            await ctx.Channel.SendConfirmAsync(Strings.StarboardThresholdSet(ctx.Guild.Id, starboardId, threshold));
        else
            await ctx.Channel.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Adds an emote to an existing starboard configuration.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="emote">The emote to add.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task AddStarboardEmote(int starboardId, IEmote emote)
    {
        try
        {
            await ctx.Message.AddReactionAsync(emote);
        }
        catch
        {
            await ctx.Channel.SendErrorAsync(Strings.InvalidEmote(ctx.Guild.Id), Config);
            return;
        }

        try
        {
            if (await Service.AddEmoteToStarboard(ctx.Guild, starboardId, emote.ToString()))
                await ctx.Channel.SendConfirmAsync(Strings.StarboardEmoteAdded(ctx.Guild.Id, emote.ToString(),
                    starboardId));
            else
                await ctx.Channel.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already used"))
        {
            await ctx.Channel.SendErrorAsync(Strings.StarboardEmoteInUse(ctx.Guild.Id), Config);
        }
    }

    /// <summary>
    ///     Removes an emote from an existing starboard configuration.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="emote">The emote to remove.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task RemoveStarboardEmote(int starboardId, IEmote emote)
    {
        try
        {
            if (await Service.RemoveEmoteFromStarboard(ctx.Guild, starboardId, emote.ToString()))
                await ctx.Channel.SendConfirmAsync(Strings.StarboardEmoteRemoved(ctx.Guild.Id, emote.ToString(),
                    starboardId));
            else
                await ctx.Channel.SendErrorAsync(
                    Strings.StarboardEmoteNotFound(ctx.Guild.Id, emote.ToString(), starboardId), Config);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot remove the last emote"))
        {
            await ctx.Channel.SendErrorAsync(Strings.StarboardCannotRemoveLastEmote(ctx.Guild.Id, starboardId), Config);
        }
    }

    /// <summary>
    ///     Shows starboard statistics for the guild.
    /// </summary>
    [Cmd]
    [Aliases]
    public async Task StarboardStats()
    {
        var stats = await Service.GetStarboardStats(ctx.Guild.Id);
        if (stats == null)
        {
            await ctx.Channel.SendErrorAsync(Strings.NoStarboardStats(ctx.Guild.Id), Config);
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

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Shows starboard statistics for a specific user.
    /// </summary>
    /// <param name="user">The user to show stats for. Defaults to command invoker.</param>
    [Cmd]
    [Aliases]
    public async Task StarboardUserStats(IUser user = null)
    {
        user ??= ctx.User;
        var stats = await Service.GetUserStarboardStats(ctx.Guild.Id, user.Id);
        if (stats == null)
        {
            await ctx.Channel.SendErrorAsync(Strings.NoStarboardStats(ctx.Guild.Id), Config);
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

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }
}