using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Twitch.Services;

namespace Mewdeko.Modules.Twitch;

/// <summary>
///     Slash command module for configuring the Twitch bot integration per guild.
/// </summary>
[Group("twitch", "Twitch integration configuration")]
public class SlashTwitch : MewdekoSlashModuleBase<TwitchService>
{
    /// <summary>
    ///     Sets the Twitch channel the bot should join and enables the command processor for this guild.
    /// </summary>
    /// <param name="twitchChannel">The Twitch channel name (with or without the # prefix).</param>
    /// <param name="prefix">The command prefix to use in Twitch chat. Defaults to <c>!</c>.</param>
    [SlashCommand("set", "Set the Twitch channel for this server")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Set(string twitchChannel, string prefix = "!")
    {
        await DeferAsync(true).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(Service.BotUsername))
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithErrorColor()
                .WithDescription(Strings.TwitchNotConfigured(ctx.Guild.Id))
                .Build(), ephemeral: true).ConfigureAwait(false);
            return;
        }

        await Service.JoinChannelAsync(ctx.Guild.Id, twitchChannel, prefix).ConfigureAwait(false);

        var channel = twitchChannel.TrimStart('#').ToLowerInvariant();
        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchChannelSet(ctx.Guild.Id, channel, prefix))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes the Twitch channel configuration for this guild and leaves the channel.
    /// </summary>
    [SlashCommand("remove", "Remove the Twitch channel configuration for this server")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Remove()
    {
        await DeferAsync(true).ConfigureAwait(false);

        var config = await Service.GetConfigAsync(ctx.Guild.Id).ConfigureAwait(false);
        if (config is null)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithErrorColor()
                .WithDescription(Strings.TwitchNotSet(ctx.Guild.Id))
                .Build(), ephemeral: true).ConfigureAwait(false);
            return;
        }

        var channel = config.TwitchChannel;
        await Service.LeaveChannelAsync(ctx.Guild.Id).ConfigureAwait(false);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchChannelRemoved(ctx.Guild.Id, channel))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows the current Twitch configuration for this guild.
    /// </summary>
    [SlashCommand("config", "Show the current Twitch configuration")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Config()
    {
        await DeferAsync(true).ConfigureAwait(false);

        var config = await Service.GetConfigAsync(ctx.Guild.Id).ConfigureAwait(false);
        if (config is null)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithErrorColor()
                .WithDescription(Strings.TwitchNotSet(ctx.Guild.Id))
                .Build(), ephemeral: true).ConfigureAwait(false);
            return;
        }

        var goLiveValue = config.GoLiveChannelId.HasValue
            ? $"<#{config.GoLiveChannelId.Value}>"
            : "Not set";

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.TwitchConfigTitle(ctx.Guild.Id))
            .AddField(Strings.TwitchConfigChannel(ctx.Guild.Id), $"#{config.TwitchChannel}", true)
            .AddField(Strings.TwitchConfigPrefix(ctx.Guild.Id), $"`{config.CommandPrefix}`", true)
            .AddField(Strings.TwitchConfigEnabled(ctx.Guild.Id), config.Enabled ? "Yes" : "No", true)
            .AddField(Strings.TwitchConfigGolive(ctx.Guild.Id), goLiveValue, true);

        if (!string.IsNullOrWhiteSpace(config.Language))
            embed.AddField("Language", config.Language, true);

        await FollowupAsync(embed: embed.Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the Discord channel to post go-live notifications in, with an optional custom message.
    ///     The message supports <c>%streamer%</c>, <c>%title%</c>, <c>%game%</c>, and <c>%url%</c> placeholders.
    /// </summary>
    /// <param name="channel">The Discord text channel for go-live notifications.</param>
    /// <param name="message">Optional custom message template.</param>
    [SlashCommand("golive-channel", "Set the Discord channel for go-live notifications")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task SetGoLive(ITextChannel channel, string? message = null)
    {
        await DeferAsync(true).ConfigureAwait(false);

        var config = await Service.GetConfigAsync(ctx.Guild.Id).ConfigureAwait(false);
        if (config is null)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithErrorColor()
                .WithDescription(Strings.TwitchNotSet(ctx.Guild.Id))
                .Build(), ephemeral: true).ConfigureAwait(false);
            return;
        }

        await Service.SetGoLiveChannelAsync(ctx.Guild.Id, channel.Id, message).ConfigureAwait(false);

        var desc = message is not null
            ? Strings.TwitchGoliveMessageSet(ctx.Guild.Id, channel.Mention)
            : Strings.TwitchGoliveSet(ctx.Guild.Id, channel.Mention);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(desc)
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Clears the go-live notification channel for this guild.
    /// </summary>
    [SlashCommand("golive-clear", "Clear the go-live notification channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task ClearGoLive()
    {
        await DeferAsync(true).ConfigureAwait(false);

        var config = await Service.GetConfigAsync(ctx.Guild.Id).ConfigureAwait(false);
        if (config is null)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithErrorColor()
                .WithDescription(Strings.TwitchNotSet(ctx.Guild.Id))
                .Build(), ephemeral: true).ConfigureAwait(false);
            return;
        }

        await Service.SetGoLiveChannelAsync(ctx.Guild.Id, 0, null).ConfigureAwait(false);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchGoliveCleared(ctx.Guild.Id))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Links a Discord user to their Twitch username for this guild.
    ///     This allows the bot to recognise them when they chat on Twitch.
    /// </summary>
    /// <param name="user">The Discord user to link.</param>
    /// <param name="twitchUsername">Their Twitch login name.</param>
    [SlashCommand("link", "Link a Discord user to their Twitch account")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Link(IGuildUser user, string twitchUsername)
    {
        await DeferAsync(true).ConfigureAwait(false);

        await Service.LinkAccountAsync(ctx.Guild.Id, user.Id, twitchUsername).ConfigureAwait(false);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchLinkSet(ctx.Guild.Id, user.Mention, twitchUsername.ToLowerInvariant()))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes the Twitch account link for a Discord user in this guild.
    /// </summary>
    /// <param name="user">The Discord user whose link should be removed.</param>
    [SlashCommand("unlink", "Remove a Discord user's Twitch account link")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Unlink(IGuildUser user)
    {
        await DeferAsync(true).ConfigureAwait(false);

        var existing = await Service.GetLinkedTwitchUsernameAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
        if (existing is null)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithErrorColor()
                .WithDescription(Strings.TwitchLinkNotFound(ctx.Guild.Id))
                .Build(), ephemeral: true).ConfigureAwait(false);
            return;
        }

        await Service.UnlinkAccountAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchLinkRemoved(ctx.Guild.Id, user.Mention))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all Discord-to-Twitch account links for this guild.
    /// </summary>
    [SlashCommand("links", "List all Twitch account links for this server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Links()
    {
        await DeferAsync(true).ConfigureAwait(false);

        var links = await Service.GetAllLinksAsync(ctx.Guild.Id).ConfigureAwait(false);
        if (links.Count == 0)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription(Strings.TwitchLinksEmpty(ctx.Guild.Id))
                .Build(), ephemeral: true).ConfigureAwait(false);
            return;
        }

        var entries = links.Select(l =>
            Strings.TwitchLinkEntry(ctx.Guild.Id, $"<@{l.DiscordUserId}>", l.TwitchUsername));

        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.TwitchLinksTitle(ctx.Guild.Id))
            .WithDescription(string.Join("\n", entries))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets a language override for this guild's Twitch channel, independent of the Discord guild locale.
    /// </summary>
    /// <param name="languageTag">
    ///     A BCP-47 language tag such as <c>en-US</c>, <c>de-DE</c>, or <c>ja-JP</c>.
    ///     Pass an empty string to reset to the guild default.
    /// </param>
    [SlashCommand("language", "Set the language for Twitch chat responses (overrides guild locale)")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Language(string languageTag)
    {
        await DeferAsync(true).ConfigureAwait(false);

        var config = await Service.GetConfigAsync(ctx.Guild.Id).ConfigureAwait(false);
        if (config is null)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithErrorColor()
                .WithDescription(Strings.TwitchNotSet(ctx.Guild.Id))
                .Build(), ephemeral: true).ConfigureAwait(false);
            return;
        }

        var tag = languageTag.Trim();
        await Service.SetChannelLanguageAsync(ctx.Guild.Id, string.IsNullOrWhiteSpace(tag) ? null : tag)
            .ConfigureAwait(false);

        var desc = string.IsNullOrWhiteSpace(tag)
            ? Strings.TwitchLanguageCleared(ctx.Guild.Id)
            : Strings.TwitchLanguageSet(ctx.Guild.Id, tag);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(desc)
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Updates the command prefix used in Twitch chat for this guild.
    /// </summary>
    /// <param name="prefix">The new prefix character or string.</param>
    [SlashCommand("prefix", "Change the Twitch chat command prefix")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Prefix(string prefix)
    {
        await DeferAsync(true).ConfigureAwait(false);

        var config = await Service.GetConfigAsync(ctx.Guild.Id).ConfigureAwait(false);
        if (config is null)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithErrorColor()
                .WithDescription(Strings.TwitchNotSet(ctx.Guild.Id))
                .Build(), ephemeral: true).ConfigureAwait(false);
            return;
        }

        await Service.JoinChannelAsync(ctx.Guild.Id, config.TwitchChannel, prefix).ConfigureAwait(false);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchPrefixSet(ctx.Guild.Id, prefix))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }
}