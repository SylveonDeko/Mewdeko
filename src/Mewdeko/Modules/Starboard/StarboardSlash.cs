using Discord.Interactions;
using Fergun.Interactive;
using Humanizer;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Starboard.Common;
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
                ) + "\n" + Service.DescribeExtras(ctx.Guild.Id, starboard));
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
    ///     Toggles a channel whose messages are posted to a starboard without needing stars.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to modify.</param>
    /// <param name="channel">The channel to toggle.</param>
    [SlashCommand("autostar", "Toggle a channel whose messages are posted without needing stars")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardAutoStar(
        [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
        int starboardId,
        ITextChannel channel)
    {
        var (wasAdded, config) = await Service.ToggleAutoStarChannel(ctx.Guild, starboardId, channel.Id);
        if (config == null)
        {
            await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
            return;
        }

        await ctx.Interaction.SendConfirmAsync(wasAdded
            ? Strings.StarboardAutostarAdded(ctx.Guild.Id, channel.Mention, starboardId)
            : Strings.StarboardAutostarRemoved(ctx.Guild.Id, channel.Mention, starboardId));
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
    ///     Sets whether a member's own star on their message counts.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to modify.</param>
    /// <param name="enabled">Whether self stars count.</param>
    [SlashCommand("self-star", "Set whether a member's own star on their message counts")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardSelfStar(
        [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
        int starboardId,
        bool enabled)
    {
        if (await Service.SetAllowSelfStar(ctx.Guild, starboardId, enabled))
        {
            if (enabled)
                await ctx.Interaction.SendConfirmAsync(Strings.StarboardSelfStarEnabled(ctx.Guild.Id, starboardId));
            else
                await ctx.Interaction.SendConfirmAsync(Strings.StarboardSelfStarDisabled(ctx.Guild.Id, starboardId));
        }
        else
            await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Sets whether messages from NSFW channels can be starred onto a non-NSFW starboard.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to modify.</param>
    /// <param name="enabled">Whether NSFW messages can be starred.</param>
    [SlashCommand("nsfw", "Set whether NSFW channel messages can be starred onto a non-NSFW starboard")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardNsfw(
        [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
        int starboardId,
        bool enabled)
    {
        if (await Service.SetAllowNsfw(ctx.Guild, starboardId, enabled))
        {
            if (enabled)
                await ctx.Interaction.SendConfirmAsync(Strings.StarboardNsfwEnabled(ctx.Guild.Id, starboardId));
            else
                await ctx.Interaction.SendConfirmAsync(Strings.StarboardNsfwDisabled(ctx.Guild.Id, starboardId));
        }
        else
            await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Stops a starboard from accepting new stars, for a set time or until unlocked.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to lock.</param>
    /// <param name="duration">How long to lock for, such as 2h. Locks until unlocked when empty.</param>
    [SlashCommand("lock", "Stop a starboard from accepting new stars")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardLock(
        [Summary("starboard", "The starboard to lock")] [Autocomplete(typeof(StarboardAutocompleter))]
        int starboardId,
        [Summary("duration", "How long to lock for, such as 2h")]
        string? duration = null)
    {
        StoopidTime? time = null;
        if (!string.IsNullOrWhiteSpace(duration))
        {
            try
            {
                time = StoopidTime.FromInput(duration);
            }
            catch (ArgumentException)
            {
            }

            if (time == null || time.Time <= TimeSpan.Zero)
            {
                await ctx.Interaction.SendErrorAsync(Strings.StarboardInvalidDuration(ctx.Guild.Id), Config);
                return;
            }
        }

        var until = time == null ? DateTime.UtcNow.AddYears(100) : DateTime.UtcNow.Add(time.Time);
        if (await Service.SetLockedUntil(ctx.Guild, starboardId, until))
        {
            if (time == null)
                await ctx.Interaction.SendConfirmAsync(Strings.StarboardLockedIndefinitely(ctx.Guild.Id, starboardId));
            else
                await ctx.Interaction.SendConfirmAsync(Strings.StarboardLocked(ctx.Guild.Id, starboardId,
                    $"<t:{new DateTimeOffset(until).ToUnixTimeSeconds()}:f>"));
        }
        else
            await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Lets a locked starboard accept stars again.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to unlock.</param>
    [SlashCommand("unlock", "Let a locked starboard accept stars again")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardUnlock(
        [Summary("starboard", "The starboard to unlock")] [Autocomplete(typeof(StarboardAutocompleter))]
        int starboardId)
    {
        if (await Service.SetLockedUntil(ctx.Guild, starboardId, null))
            await ctx.Interaction.SendConfirmAsync(Strings.StarboardUnlocked(ctx.Guild.Id, starboardId));
        else
            await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Commands for finding starred messages.
    /// </summary>
    [Group("posts", "Find starred messages")]
    public class Posts : MewdekoSlashSubmodule<StarboardService>
    {
        /// <summary>
        ///     Shows a starred message by the ID of the original message or its starboard post.
        /// </summary>
        /// <param name="messageId">The ID of the starred message or its starboard post.</param>
        [SlashCommand("jump", "Show a starred message by its ID")]
        public async Task StarboardJump(
            [Summary("message-id", "The ID of the starred message or its starboard post")]
            string messageId)
        {
            if (!ulong.TryParse(messageId, out var id) || Service.GetPostLink(ctx.Guild.Id, id) is not { } link)
            {
                await ctx.Interaction.SendErrorAsync(Strings.StarboardPostNotFound(ctx.Guild.Id), Config);
                return;
            }

            await ctx.Interaction.RespondAsync(Strings.StarboardJumpResult(ctx.Guild.Id, link));
        }

        /// <summary>
        ///     Shows a random starred message.
        /// </summary>
        /// <param name="starboardId">Limits the pick to one starboard when set.</param>
        [SlashCommand("random", "Show a random starred message")]
        public async Task StarboardRandom(
            [Summary("starboard", "Only pick from this starboard")] [Autocomplete(typeof(StarboardAutocompleter))]
            int? starboardId = null)
        {
            var link = Service.GetRandomPostLink(ctx.Guild.Id, starboardId);
            if (link == null)
                await ctx.Interaction.SendErrorAsync(Strings.StarboardNoPosts(ctx.Guild.Id), Config);
            else
                await ctx.Interaction.RespondAsync(Strings.StarboardJumpResult(ctx.Guild.Id, link));
        }
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
    ///     Turns a starboard on or off without deleting it.
    /// </summary>
    /// <param name="starboardId">The ID of the starboard to modify.</param>
    /// <param name="enabled">Whether the starboard is on.</param>
    [SlashCommand("enabled", "Turn a starboard on or off without deleting it")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardEnabled(
        [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
        int starboardId,
        bool enabled)
    {
        if (await Service.SetEnabled(ctx.Guild, starboardId, enabled))
            await ctx.Interaction.SendConfirmAsync(enabled
                ? Strings.StarboardEnabled(ctx.Guild.Id, starboardId)
                : Strings.StarboardDisabled(ctx.Guild.Id, starboardId));
        else
            await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
    }

    /// <summary>
    ///     Creates a starboard channel only the bot can post in, and a starboard that posts to it.
    /// </summary>
    /// <param name="emoteText">The emote to use for starring messages.</param>
    /// <param name="threshold">The number of stars required.</param>
    /// <param name="name">The name of the channel to create.</param>
    [SlashCommand("setup", "Create a starboard channel only I can post in, and a starboard for it")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [RequireBotPermission(GuildPermission.ManageChannels)]
    [CheckPermissions]
    public async Task StarboardSetup(string emoteText, int threshold = 1,
        [Summary("name", "The name of the channel")] string name = "starboard")
    {
        await DeferAsync();
        var emote = emoteText.ToIEmote();
        var msg = await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.Loading(ctx.Guild.Id));
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
            var created = await Service.CreateStarboardWithChannel(ctx.Guild, name, emote.ToString(), threshold);
            await msg.DeleteAsync();
            if (created is not { } result)
            {
                await ctx.Interaction.SendErrorAsync(Strings.StarboardSetupFailed(ctx.Guild.Id), Config);
                return;
            }

            await ctx.Interaction.SendConfirmAsync(Strings.StarboardSetupCreated(ctx.Guild.Id,
                result.Channel.Mention, result.StarboardId, emote.ToString(), threshold));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already used"))
        {
            await msg.DeleteAsync();
            await ctx.Interaction.SendErrorAsync(Strings.StarboardEmoteInUse(ctx.Guild.Id), Config);
        }
    }

    /// <summary>
    ///     Commands for a starboard's post design.
    /// </summary>
    [Group("design", "Design how starboard posts look")]
    public class Design : MewdekoSlashSubmodule<StarboardService>
    {
        /// <summary>
        ///     Sets the post design of a starboard from embed json with starboard placeholders.
        /// </summary>
        /// <param name="starboardId">The ID of the starboard to modify.</param>
        /// <param name="design">The embed json of the design.</param>
        [SlashCommand("set", "Set a starboard's post design from embed json with starboard placeholders")]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [CheckPermissions]
        public async Task StarboardDesign(
            [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
            int starboardId,
            [Summary("json", "The embed json of the design")]
            string design)
        {
            switch (await Service.SetPostDesign(ctx.Guild, starboardId, design))
            {
                case StarboardDesignResult.Saved:
                    await ctx.Interaction.SendConfirmAsync(Strings.StarboardDesignSet(ctx.Guild.Id, starboardId));
                    break;
                case StarboardDesignResult.Invalid:
                    await ctx.Interaction.SendErrorAsync(Strings.StarboardDesignInvalid(ctx.Guild.Id), Config);
                    break;
                default:
                    await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
                    break;
            }
        }

        /// <summary>
        ///     Puts a starboard back on the default post layout.
        /// </summary>
        /// <param name="starboardId">The ID of the starboard to modify.</param>
        [SlashCommand("reset", "Put a starboard back on the default post layout")]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [CheckPermissions]
        public async Task StarboardDesignReset(
            [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
            int starboardId)
        {
            if (await Service.SetPostDesign(ctx.Guild, starboardId, null) == StarboardDesignResult.Saved)
                await ctx.Interaction.SendConfirmAsync(Strings.StarboardDesignReset(ctx.Guild.Id, starboardId));
            else
                await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
        }

        /// <summary>
        ///     Sends a preview of a starboard post for a message here.
        /// </summary>
        /// <param name="starboardId">The ID of the starboard to preview.</param>
        /// <param name="messageId">The ID of the message to preview with.</param>
        [SlashCommand("test", "Send a preview of a starboard post for a message here")]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [CheckPermissions]
        public async Task StarboardDesignTest(
            [Summary("starboard", "The starboard to preview")] [Autocomplete(typeof(StarboardAutocompleter))]
            int starboardId,
            [Summary("message-id", "The message to preview with, the latest member message when empty")]
            string? messageId = null)
        {
            await DeferAsync(true);
            ulong? id = ulong.TryParse(messageId, out var parsed) ? parsed : null;
            var sample = await StarboardService.FindSampleMessageAsync(ctx.Channel, id);
            if (sample == null)
            {
                await ctx.Interaction.SendErrorAsync(Strings.StarboardDesignNoMessage(ctx.Guild.Id), Config);
                return;
            }

            if (await Service.SendDesignTestAsync(ctx.Guild, starboardId, (ITextChannel)ctx.Channel, sample))
                await ctx.Interaction.DeleteOriginalResponseAsync();
            else
                await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
        }

        /// <summary>
        ///     Lists the placeholders a starboard post design can use.
        /// </summary>
        [SlashCommand("placeholders", "List the placeholders a starboard post design can use")]
        public async Task StarboardPlaceholders()
            => await ctx.Interaction.SendConfirmAsync(Strings.StarboardPlaceholders(ctx.Guild.Id,
                string.Join(" ", StarboardPostDesign.Placeholders.Select(x => $"`{x}`"))));
    }

    /// <summary>
    ///     Commands for which stars count and how a starboard reacts and reposts.
    /// </summary>
    [Group("rules", "Choose which stars count and how posts are reacted to and reposted")]
    public class Rules : MewdekoSlashSubmodule<StarboardService>
    {
        /// <summary>
        ///     Sets which of a starboard's emotes the bot reacts with on its posts.
        /// </summary>
        /// <param name="starboardId">The ID of the starboard to modify.</param>
        /// <param name="mode">None, first or all.</param>
        [SlashCommand("auto-react", "Set which of a starboard's emotes I react with on its posts")]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [CheckPermissions]
        public async Task StarboardAutoReactMode(
            [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
            int starboardId,
            StarboardAutoReact mode)
        {
            if (!await Service.SetAutoReact(ctx.Guild, starboardId, mode))
            {
                await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
                return;
            }

            await ctx.Interaction.SendConfirmAsync(mode switch
            {
                StarboardAutoReact.First => Strings.StarboardAutoReactFirst(ctx.Guild.Id, starboardId),
                StarboardAutoReact.All => Strings.StarboardAutoReactAll(ctx.Guild.Id, starboardId),
                _ => Strings.StarboardAutoReactNone(ctx.Guild.Id, starboardId)
            });
        }

        /// <summary>
        ///     Sets whether every reaction a member adds counts when a starboard has several emotes.
        /// </summary>
        /// <param name="starboardId">The ID of the starboard to modify.</param>
        /// <param name="enabled">Whether every reaction counts instead of one per member.</param>
        [SlashCommand("multi-react", "Set whether every reaction a member adds counts")]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [CheckPermissions]
        public async Task StarboardMultiReact(
            [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
            int starboardId,
            bool enabled)
        {
            if (await Service.SetCountMultipleReactions(ctx.Guild, starboardId, enabled))
                await ctx.Interaction.SendConfirmAsync(enabled
                    ? Strings.StarboardMultiReactEnabled(ctx.Guild.Id, starboardId)
                    : Strings.StarboardMultiReactDisabled(ctx.Guild.Id, starboardId));
            else
                await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
        }

        /// <summary>
        ///     Sets whether the bot removes stars that cannot count.
        /// </summary>
        /// <param name="starboardId">The ID of the starboard to modify.</param>
        /// <param name="enabled">Whether rejected stars are removed.</param>
        [SlashCommand("remove-invalid", "Set whether I remove stars that cannot count")]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [CheckPermissions]
        public async Task StarboardRemoveInvalid(
            [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
            int starboardId,
            bool enabled)
        {
            if (await Service.SetRemoveInvalidStars(ctx.Guild, starboardId, enabled))
                await ctx.Interaction.SendConfirmAsync(enabled
                    ? Strings.StarboardRemoveInvalidEnabled(ctx.Guild.Id, starboardId)
                    : Strings.StarboardRemoveInvalidDisabled(ctx.Guild.Id, starboardId));
            else
                await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
        }

        /// <summary>
        ///     Toggles a role that is allowed to star. When any role is listed, only those roles count.
        /// </summary>
        /// <param name="starboardId">The ID of the starboard to modify.</param>
        /// <param name="role">The role to toggle.</param>
        [SlashCommand("allow-role", "Toggle a role that is allowed to star")]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [CheckPermissions]
        public async Task StarboardAllowRole(
            [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
            int starboardId,
            IRole role)
        {
            var (wasAdded, config) = await Service.ToggleStarRole(ctx.Guild, starboardId, role.Id, false);
            if (config == null)
            {
                await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
                return;
            }

            await ctx.Interaction.SendConfirmAsync(wasAdded
                ? Strings.StarboardAllowRoleAdded(ctx.Guild.Id, role.Mention, starboardId)
                : Strings.StarboardAllowRoleRemoved(ctx.Guild.Id, role.Mention, starboardId));
        }

        /// <summary>
        ///     Toggles a role whose stars never count.
        /// </summary>
        /// <param name="starboardId">The ID of the starboard to modify.</param>
        /// <param name="role">The role to toggle.</param>
        [SlashCommand("deny-role", "Toggle a role whose stars never count")]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [CheckPermissions]
        public async Task StarboardDenyRole(
            [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
            int starboardId,
            IRole role)
        {
            var (wasAdded, config) = await Service.ToggleStarRole(ctx.Guild, starboardId, role.Id, true);
            if (config == null)
            {
                await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
                return;
            }

            await ctx.Interaction.SendConfirmAsync(wasAdded
                ? Strings.StarboardDenyRoleAdded(ctx.Guild.Id, role.Mention, starboardId)
                : Strings.StarboardDenyRoleRemoved(ctx.Guild.Id, role.Mention, starboardId));
        }

        /// <summary>
        ///     Sets how old a message must be before it can be starred.
        /// </summary>
        /// <param name="starboardId">The ID of the starboard to modify.</param>
        /// <param name="duration">The minimum age, such as 10m. Removes the limit when empty.</param>
        [SlashCommand("min-age", "Set how old a message must be before it can be starred")]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [CheckPermissions]
        public async Task StarboardMinAge(
            [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
            int starboardId,
            [Summary("duration", "The minimum age, such as 10m")]
            string? duration = null)
        {
            if (!StarboardService.TryParseMinutes(duration, out var minutes))
            {
                await ctx.Interaction.SendErrorAsync(Strings.StarboardInvalidDuration(ctx.Guild.Id), Config);
                return;
            }

            if (!await Service.SetMessageAge(ctx.Guild, starboardId, minutes, null))
            {
                await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
                return;
            }

            await ctx.Interaction.SendConfirmAsync(minutes == 0
                ? Strings.StarboardMinAgeCleared(ctx.Guild.Id, starboardId)
                : Strings.StarboardMinAgeSet(ctx.Guild.Id, starboardId, TimeSpan.FromMinutes(minutes).Humanize(2)));
        }

        /// <summary>
        ///     Sets how old a message can be and still be starred.
        /// </summary>
        /// <param name="starboardId">The ID of the starboard to modify.</param>
        /// <param name="duration">The maximum age, such as 2d. Removes the limit when empty.</param>
        [SlashCommand("max-age", "Set how old a message can be and still be starred")]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [CheckPermissions]
        public async Task StarboardMaxAge(
            [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
            int starboardId,
            [Summary("duration", "The maximum age, such as 2d")]
            string? duration = null)
        {
            if (!StarboardService.TryParseMinutes(duration, out var minutes))
            {
                await ctx.Interaction.SendErrorAsync(Strings.StarboardInvalidDuration(ctx.Guild.Id), Config);
                return;
            }

            if (!await Service.SetMessageAge(ctx.Guild, starboardId, null, minutes))
            {
                await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
                return;
            }

            await ctx.Interaction.SendConfirmAsync(minutes == 0
                ? Strings.StarboardMaxAgeCleared(ctx.Guild.Id, starboardId)
                : Strings.StarboardMaxAgeSet(ctx.Guild.Id, starboardId, TimeSpan.FromMinutes(minutes).Humanize(2)));
        }

        /// <summary>
        ///     Sets the least time between reposts of the same starboard post.
        /// </summary>
        /// <param name="starboardId">The ID of the starboard to modify.</param>
        /// <param name="duration">The cooldown, such as 1h. Removes the cooldown when empty.</param>
        [SlashCommand("repost-cooldown", "Set the least time between reposts of the same post")]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [CheckPermissions]
        public async Task StarboardRepostCooldown(
            [Summary("starboard", "The starboard to modify")] [Autocomplete(typeof(StarboardAutocompleter))]
            int starboardId,
            [Summary("duration", "The cooldown, such as 1h")]
            string? duration = null)
        {
            if (!StarboardService.TryParseMinutes(duration, out var minutes))
            {
                await ctx.Interaction.SendErrorAsync(Strings.StarboardInvalidDuration(ctx.Guild.Id), Config);
                return;
            }

            if (!await Service.SetRepostCooldown(ctx.Guild, starboardId, minutes))
            {
                await ctx.Interaction.SendErrorAsync(Strings.StarboardNotFound(ctx.Guild.Id, starboardId), Config);
                return;
            }

            await ctx.Interaction.SendConfirmAsync(minutes == 0
                ? Strings.StarboardRepostCooldownCleared(ctx.Guild.Id, starboardId)
                : Strings.StarboardRepostCooldownSet(ctx.Guild.Id, starboardId,
                    TimeSpan.FromMinutes(minutes).Humanize(2)));
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