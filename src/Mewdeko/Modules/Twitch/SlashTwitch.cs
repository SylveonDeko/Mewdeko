using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Twitch.Common;
using Mewdeko.Modules.Twitch.Services;

namespace Mewdeko.Modules.Twitch;

/// <summary>
///     Shared implementation for all Twitch slash commands. Nested subcommand-group classes in
///     <see cref="SlashTwitch" /> inherit from this base directly instead of from <see cref="SlashTwitch" /> itself,
///     so that the top-level attributed commands declared on <see cref="SlashTwitch" /> are not re-registered
///     (and collided on) under every nested group.
/// </summary>
public abstract class TwitchSlashModuleBase : MewdekoSlashModuleBase<TwitchService>
{
    /// <summary>
    ///     Sets the Twitch channel the bot should join and enables the command processor for this guild.
    /// </summary>
    /// <param name="twitchChannel">The Twitch channel name (with or without the # prefix).</param>
    /// <param name="prefix">The command prefix to use in Twitch chat. Defaults to <c>!</c>.</param>
    public async Task Set(string twitchChannel, string prefix = "!")
    {
        await DeferAsync(true).ConfigureAwait(false);

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
    ///     Enables the existing Twitch integration for this guild.
    /// </summary>
    public async Task Enable()
    {
        await DeferAsync(true).ConfigureAwait(false);
        var ok = await Service.SetEnabledAsync(ctx.Guild.Id, true).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithColor(ok ? Mewdeko.OkColor : Mewdeko.ErrorColor)
            .WithDescription(ok ? Strings.TwitchEnabled(ctx.Guild.Id) : Strings.TwitchNotSet(ctx.Guild.Id))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Disables the Twitch integration without deleting its configuration.
    /// </summary>
    public async Task Disable()
    {
        await DeferAsync(true).ConfigureAwait(false);
        var ok = await Service.SetEnabledAsync(ctx.Guild.Id, false).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithColor(ok ? Mewdeko.OkColor : Mewdeko.ErrorColor)
            .WithDescription(ok ? Strings.TwitchDisabled(ctx.Guild.Id) : Strings.TwitchNotSet(ctx.Guild.Id))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows the current Twitch configuration for this guild.
    /// </summary>
    public new async Task Config()
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
    ///     Sets the Discord channel to post Twitch subscription notifications in.
    /// </summary>
    public async Task SubChannel(ITextChannel channel, string? message = null)
    {
        await DeferAsync(true).ConfigureAwait(false);
        await Service.SetSubNotificationChannelAsync(ctx.Guild.Id, channel.Id, message).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchSubSet(ctx.Guild.Id, channel.Mention))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Clears Twitch subscription notifications.
    /// </summary>
    public async Task SubClear()
    {
        await DeferAsync(true).ConfigureAwait(false);
        await Service.SetSubNotificationChannelAsync(ctx.Guild.Id, 0, null).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchSubCleared(ctx.Guild.Id))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the Discord channel to post Twitch raid notifications in.
    /// </summary>
    public async Task RaidChannel(ITextChannel channel, string? message = null)
    {
        await DeferAsync(true).ConfigureAwait(false);
        await Service.SetRaidNotificationChannelAsync(ctx.Guild.Id, channel.Id, message).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchRaidSet(ctx.Guild.Id, channel.Mention))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Clears Twitch raid notifications.
    /// </summary>
    public async Task RaidClear()
    {
        await DeferAsync(true).ConfigureAwait(false);
        await Service.SetRaidNotificationChannelAsync(ctx.Guild.Id, 0, null).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchRaidCleared(ctx.Guild.Id))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Links a Discord user to their Twitch username for this guild.
    ///     This allows the bot to recognise them when they chat on Twitch.
    /// </summary>
    /// <param name="user">The Discord user to link.</param>
    /// <param name="twitchUsername">Their Twitch login name.</param>
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
    ///     Claims a self-service Twitch link code generated in Twitch chat.
    /// </summary>
    /// <param name="code">The claim code from the Twitch <c>!link</c> command.</param>
    public async Task Claim(string code)
    {
        await DeferAsync(true).ConfigureAwait(false);
        var result = await Service.ClaimLinkCodeAsync(ctx.Guild.Id, ctx.User.Id, code).ConfigureAwait(false);
        var description = result.Success
            ? Strings.TwitchClaimSuccess(ctx.Guild.Id, result.TwitchUsername)
            : result.ErrorKey == "twitch_claim_expired"
                ? Strings.TwitchClaimExpired(ctx.Guild.Id)
                : Strings.TwitchClaimInvalid(ctx.Guild.Id);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithColor(result.Success ? Mewdeko.OkColor : Mewdeko.ErrorColor)
            .WithDescription(description)
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds or updates a custom Twitch chat command.
    /// </summary>
    public async Task CommandAdd(string name, string response, string permission = "Everyone", int cooldownSeconds = 0)
    {
        await DeferAsync(true).ConfigureAwait(false);
        if (!Enum.TryParse<TwitchCommandPermission>(permission, true, out var parsedPermission))
            parsedPermission = TwitchCommandPermission.Everyone;

        var command = await Service.UpsertCustomCommandAsync(ctx.Guild.Id, name, response, parsedPermission,
            cooldownSeconds).ConfigureAwait(false);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchCommandSaved(ctx.Guild.Id, command.Name))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a custom Twitch chat command.
    /// </summary>
    public async Task CommandRemove(string name)
    {
        await DeferAsync(true).ConfigureAwait(false);
        var removed = await Service.RemoveCustomCommandAsync(ctx.Guild.Id, name).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithColor(removed ? Mewdeko.OkColor : Mewdeko.ErrorColor)
            .WithDescription(removed
                ? Strings.TwitchCommandRemoved(ctx.Guild.Id, name)
                : Strings.TwitchCommandNotFound(ctx.Guild.Id))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists custom Twitch chat commands.
    /// </summary>
    public async Task CommandList()
    {
        await DeferAsync(true).ConfigureAwait(false);
        var commands = await Service.GetCustomCommandsAsync(ctx.Guild.Id).ConfigureAwait(false);
        var description = commands.Count == 0
            ? Strings.TwitchCommandsEmpty(ctx.Guild.Id)
            : string.Join("\n", commands.Select(c =>
                Strings.TwitchCommandEntry(ctx.Guild.Id, c.Name,
                    ((TwitchCommandPermission)c.PermissionLevel).ToString(),
                    c.CooldownSeconds, c.UseCount)));

        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.TwitchCommandsTitle(ctx.Guild.Id))
            .WithDescription(description)
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets a named Twitch counter value.
    /// </summary>
    public async Task CounterSet(string name, int value)
    {
        await DeferAsync(true).ConfigureAwait(false);
        var updated = await Service.SetCounterAsync(ctx.Guild.Id, name, value).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchCounterSet(ctx.Guild.Id, name, updated))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists Twitch counters for this guild.
    /// </summary>
    public async Task CounterList()
    {
        await DeferAsync(true).ConfigureAwait(false);
        var counters = await Service.GetCountersAsync(ctx.Guild.Id).ConfigureAwait(false);
        var description = counters.Count == 0
            ? Strings.TwitchCountersEmpty(ctx.Guild.Id)
            : string.Join("\n", counters.Select(c => Strings.TwitchCounterEntry(ctx.Guild.Id, c.Name, c.Value)));

        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.TwitchCountersTitle(ctx.Guild.Id))
            .WithDescription(description)
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a Twitch permission-level to Discord role sync mapping.
    /// </summary>
    public async Task RoleSyncAdd(string twitchRank, IRole role)
    {
        await DeferAsync(true).ConfigureAwait(false);
        if (!Enum.TryParse<TwitchPermissionLevel>(twitchRank, true, out var level))
            level = TwitchPermissionLevel.Subscriber;

        await Service.UpsertRoleSyncMappingAsync(ctx.Guild.Id, level, role.Id).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchRolesyncSaved(ctx.Guild.Id, level.ToString(), role.Mention))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a Twitch role sync mapping.
    /// </summary>
    public async Task RoleSyncRemove(string twitchRank, IRole role)
    {
        await DeferAsync(true).ConfigureAwait(false);
        if (!Enum.TryParse<TwitchPermissionLevel>(twitchRank, true, out var level))
            level = TwitchPermissionLevel.Subscriber;

        var removed = await Service.RemoveRoleSyncMappingAsync(ctx.Guild.Id, level, role.Id).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithColor(removed ? Mewdeko.OkColor : Mewdeko.ErrorColor)
            .WithDescription(removed
                ? Strings.TwitchRolesyncRemoved(ctx.Guild.Id, level.ToString(), role.Mention)
                : Strings.TwitchRolesyncNotFound(ctx.Guild.Id))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists Twitch role sync mappings.
    /// </summary>
    public async Task RoleSyncList()
    {
        await DeferAsync(true).ConfigureAwait(false);
        var mappings = await Service.GetRoleSyncMappingsAsync(ctx.Guild.Id).ConfigureAwait(false);
        var description = mappings.Count == 0
            ? Strings.TwitchRolesyncEmpty(ctx.Guild.Id)
            : string.Join("\n", mappings.Select(m =>
                Strings.TwitchRolesyncEntry(ctx.Guild.Id, ((TwitchPermissionLevel)m.PermissionLevel).ToString(),
                    $"<@&{m.RoleId}>")));

        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.TwitchRolesyncTitle(ctx.Guild.Id))
            .WithDescription(description)
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the Discord channel for end-of-stream recap posts.
    /// </summary>
    public async Task RecapChannel(ITextChannel channel)
    {
        await DeferAsync(true).ConfigureAwait(false);
        await Service.SetStreamRecapChannelAsync(ctx.Guild.Id, channel.Id, true).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchRecapSet(ctx.Guild.Id, channel.Mention))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Clears stream recap posts for this guild.
    /// </summary>
    public async Task RecapClear()
    {
        await DeferAsync(true).ConfigureAwait(false);
        await Service.SetStreamRecapChannelAsync(ctx.Guild.Id, 0, false).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchRecapCleared(ctx.Guild.Id))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds or updates a channel point redemption action.
    /// </summary>
    public async Task RedemptionAdd(
        string rewardTitle,
        string? twitchResponse = null,
        ITextChannel? discordChannel = null,
        string? discordMessage = null)
    {
        await DeferAsync(true).ConfigureAwait(false);
        var action = await Service.UpsertRedemptionActionAsync(ctx.Guild.Id, rewardTitle, twitchResponse,
            discordChannel?.Id, discordMessage).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchRedemptionSaved(ctx.Guild.Id, action.RewardTitle))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a channel point redemption action.
    /// </summary>
    public async Task RedemptionRemove(string rewardTitle)
    {
        await DeferAsync(true).ConfigureAwait(false);
        var removed = await Service.RemoveRedemptionActionAsync(ctx.Guild.Id, rewardTitle).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithColor(removed ? Mewdeko.OkColor : Mewdeko.ErrorColor)
            .WithDescription(removed
                ? Strings.TwitchRedemptionRemoved(ctx.Guild.Id, rewardTitle)
                : Strings.TwitchRedemptionNotFound(ctx.Guild.Id))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists channel point redemption actions.
    /// </summary>
    public async Task RedemptionList()
    {
        await DeferAsync(true).ConfigureAwait(false);
        var actions = await Service.GetRedemptionActionsAsync(ctx.Guild.Id).ConfigureAwait(false);
        var description = actions.Count == 0
            ? Strings.TwitchRedemptionsEmpty(ctx.Guild.Id)
            : string.Join("\n", actions.Select(a => Strings.TwitchRedemptionEntry(ctx.Guild.Id, a.RewardTitle,
                a.DiscordChannelId.HasValue ? $"<#{a.DiscordChannelId.Value}>" : "-",
                string.IsNullOrWhiteSpace(a.TwitchResponse) ? "-" : "yes")));

        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.TwitchRedemptionsTitle(ctx.Guild.Id))
            .WithDescription(description)
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Updates the Twitch stream title and/or category.
    /// </summary>
    public async Task StreamInfo(string? title = null, string? category = null)
    {
        await DeferAsync(true).ConfigureAwait(false);
        var ok = await Service.UpdateStreamInfoAsync(ctx.Guild.Id, title, category).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithColor(ok ? Mewdeko.OkColor : Mewdeko.ErrorColor)
            .WithDescription(ok
                ? Strings.TwitchStreamInfoUpdated(ctx.Guild.Id)
                : Strings.TwitchStreamInfoFailed(ctx.Guild.Id))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Creates a clip for the configured Twitch channel.
    /// </summary>
    public async Task Clip()
    {
        await DeferAsync(true).ConfigureAwait(false);
        var clipUrl = await Service.CreateClipAsync(ctx.Guild.Id).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithColor(string.IsNullOrWhiteSpace(clipUrl) ? Mewdeko.ErrorColor : Mewdeko.OkColor)
            .WithDescription(string.IsNullOrWhiteSpace(clipUrl)
                ? Strings.TwitchClipFailed(ctx.Guild.Id)
                : Strings.TwitchClipCreated(ctx.Guild.Id, clipUrl))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the Twitch schedule chat message.
    /// </summary>
    public async Task ScheduleSet(string message)
    {
        await DeferAsync(true).ConfigureAwait(false);
        await Service.SetScheduleMessageAsync(ctx.Guild.Id, message).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchScheduleSet(ctx.Guild.Id))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Clears the Twitch schedule chat message.
    /// </summary>
    public async Task ScheduleClear()
    {
        await DeferAsync(true).ConfigureAwait(false);
        await Service.SetScheduleMessageAsync(ctx.Guild.Id, null).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchScheduleCleared(ctx.Guild.Id))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the Twitch socials chat message.
    /// </summary>
    public async Task SocialsSet(string message)
    {
        await DeferAsync(true).ConfigureAwait(false);
        await Service.SetSocialsMessageAsync(ctx.Guild.Id, message).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchSocialsSet(ctx.Guild.Id))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Clears the Twitch socials chat message.
    /// </summary>
    public async Task SocialsClear()
    {
        await DeferAsync(true).ConfigureAwait(false);
        await Service.SetSocialsMessageAsync(ctx.Guild.Id, null).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchSocialsCleared(ctx.Guild.Id))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds or updates a Twitch raid target suggestion.
    /// </summary>
    public async Task RaidTargetAdd(string twitchLogin, string? note = null)
    {
        await DeferAsync(true).ConfigureAwait(false);
        var target = await Service.UpsertRaidTargetAsync(ctx.Guild.Id, twitchLogin, note).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.TwitchRaidtargetSaved(ctx.Guild.Id, target.TwitchLogin))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a Twitch raid target suggestion.
    /// </summary>
    public async Task RaidTargetRemove(string twitchLogin)
    {
        await DeferAsync(true).ConfigureAwait(false);
        var removed = await Service.RemoveRaidTargetAsync(ctx.Guild.Id, twitchLogin).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithColor(removed ? Mewdeko.OkColor : Mewdeko.ErrorColor)
            .WithDescription(removed
                ? Strings.TwitchRaidtargetRemoved(ctx.Guild.Id, twitchLogin)
                : Strings.TwitchRaidtargetNotFound(ctx.Guild.Id))
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists configured Twitch raid target suggestions.
    /// </summary>
    public async Task RaidTargetList()
    {
        await DeferAsync(true).ConfigureAwait(false);
        var targets = await Service.GetRaidTargetsAsync(ctx.Guild.Id).ConfigureAwait(false);
        var description = targets.Count == 0
            ? Strings.TwitchRaidtargetsEmpty(ctx.Guild.Id)
            : string.Join("\n", targets.Select(t => Strings.TwitchRaidtargetEntry(ctx.Guild.Id, t.TwitchLogin,
                string.IsNullOrWhiteSpace(t.Note) ? "-" : t.Note)));

        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.TwitchRaidtargetsTitle(ctx.Guild.Id))
            .WithDescription(description)
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows Twitch integration health and configuration status.
    /// </summary>
    public async Task Status()
    {
        await DeferAsync(true).ConfigureAwait(false);
        var status = await Service.GetStatusSummaryAsync(ctx.Guild.Id).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.TwitchStatusTitle(ctx.Guild.Id))
            .WithDescription($"```text\n{status}\n```")
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets a language override for this guild's Twitch channel, independent of the Discord guild locale.
    /// </summary>
    /// <param name="languageTag">
    ///     A BCP-47 language tag such as <c>en-US</c>, <c>de-DE</c>, or <c>ja-JP</c>.
    ///     Pass an empty string to reset to the guild default.
    /// </param>
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

    /// <summary>
    ///     Adds or updates a repeating Twitch chat message timer.
    /// </summary>
    public async Task TimerAdd(
        string name,
        string messages,
        int intervalMinutes = 10,
        int minChatMessages = 5,
        bool onlineOnly = true,
        bool randomizeMessages = false)
    {
        await DeferAsync(true).ConfigureAwait(false);
        var timer = await Service.UpsertTimerAsync(ctx.Guild.Id, name, messages, intervalMinutes, minChatMessages,
            onlineOnly, randomizeMessages, true).ConfigureAwait(false);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription($"Saved Twitch timer **{timer.Name}**.")
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a repeating Twitch chat message timer.
    /// </summary>
    public async Task TimerRemove(string name)
    {
        await DeferAsync(true).ConfigureAwait(false);
        var removed = await Service.RemoveTimerAsync(ctx.Guild.Id, name).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithColor(removed ? Mewdeko.OkColor : Mewdeko.ErrorColor)
            .WithDescription(removed ? $"Removed Twitch timer **{name}**." : "No Twitch timer found with that name.")
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists repeating Twitch chat message timers.
    /// </summary>
    public async Task TimerList()
    {
        await DeferAsync(true).ConfigureAwait(false);
        var timers = await Service.GetTimersAsync(ctx.Guild.Id).ConfigureAwait(false);
        var description = timers.Count == 0
            ? "No Twitch timers are configured."
            : string.Join("\n", timers.Select(t =>
                $"**{t.Name}** - every {t.IntervalMinutes}m, min chat {t.MinChatMessages}, {(t.OnlineOnly ? "online only" : "always")}, {(t.Enabled ? "enabled" : "disabled")}"));

        await FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithTitle("Twitch Timers")
            .WithDescription(description)
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Enables a repeating Twitch chat message timer.
    /// </summary>
    public async Task TimerEnable(string name)
    {
        await SetTimerStateAsync(name, true).ConfigureAwait(false);
    }

    /// <summary>
    ///     Disables a repeating Twitch chat message timer.
    /// </summary>
    public async Task TimerDisable(string name)
    {
        await SetTimerStateAsync(name, false).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sends a repeating Twitch chat message timer immediately.
    /// </summary>
    public async Task TimerTest(string name)
    {
        await DeferAsync(true).ConfigureAwait(false);
        var sent = await Service.TestTimerAsync(ctx.Guild.Id, name).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithColor(sent is null ? Mewdeko.ErrorColor : Mewdeko.OkColor)
            .WithDescription(sent is null ? "No Twitch timer found with that name." : $"Sent timer message:\n{sent}")
            .Build(), ephemeral: true).ConfigureAwait(false);
    }

    private async Task SetTimerStateAsync(string name, bool enabled)
    {
        await DeferAsync(true).ConfigureAwait(false);
        var updated = await Service.SetTimerEnabledAsync(ctx.Guild.Id, name, enabled).ConfigureAwait(false);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithColor(updated ? Mewdeko.OkColor : Mewdeko.ErrorColor)
            .WithDescription(updated
                ? $"{(enabled ? "Enabled" : "Disabled")} Twitch timer **{name}**."
                : "No Twitch timer found with that name.")
            .Build(), ephemeral: true).ConfigureAwait(false);
    }
}

/// <summary>
///     Slash command module for configuring the Twitch bot integration per guild.
/// </summary>
[Group("twitch", "Twitch integration configuration")]
public class SlashTwitch : TwitchSlashModuleBase
{
    /// <summary>
    ///     Sets the Twitch channel the bot should join and enables the command processor for this guild.
    /// </summary>
    [SlashCommand("set", "Set the Twitch channel for this server")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public Task SetChannel(string twitchChannel, string prefix = "!")
    {
        return Set(twitchChannel, prefix);
    }

    /// <summary>
    ///     Removes the Twitch channel configuration for this guild and leaves the channel.
    /// </summary>
    [SlashCommand("remove", "Remove the Twitch channel configuration for this server")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public Task RemoveChannel()
    {
        return Remove();
    }

    /// <summary>
    ///     Enables the existing Twitch integration for this guild.
    /// </summary>
    [SlashCommand("enable", "Enable the Twitch integration")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public Task EnableIntegration()
    {
        return Enable();
    }

    /// <summary>
    ///     Disables the Twitch integration without deleting its configuration.
    /// </summary>
    [SlashCommand("disable", "Disable the Twitch integration")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public Task DisableIntegration()
    {
        return Disable();
    }

    /// <summary>
    ///     Shows the current Twitch configuration for this guild.
    /// </summary>
    [SlashCommand("config", "Show the current Twitch configuration")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public Task ShowConfig()
    {
        return Config();
    }

    /// <summary>
    ///     Claims a self-service Twitch link code generated in Twitch chat.
    /// </summary>
    [SlashCommand("claim", "Claim a Twitch account link code")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public Task ClaimCode(string code)
    {
        return Claim(code);
    }

    /// <summary>
    ///     Updates the Twitch stream title and/or category.
    /// </summary>
    [SlashCommand("stream-info", "Update the Twitch stream title and/or category")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public Task UpdateStreamInfo(string? title = null, string? category = null)
    {
        return StreamInfo(title, category);
    }

    /// <summary>
    ///     Creates a clip for the configured Twitch channel.
    /// </summary>
    [SlashCommand("clip", "Create a Twitch clip for the current stream")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public Task CreateClip()
    {
        return Clip();
    }

    /// <summary>
    ///     Shows Twitch integration health and configuration status.
    /// </summary>
    [SlashCommand("status", "Show Twitch integration health")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public Task ShowStatus()
    {
        return Status();
    }

    /// <summary>
    ///     Sets a language override for this guild's Twitch channel, independent of the Discord guild locale.
    /// </summary>
    [SlashCommand("language", "Set the language for Twitch chat responses (overrides guild locale)")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public Task SetLanguage(string languageTag)
    {
        return Language(languageTag);
    }

    /// <summary>
    ///     Updates the command prefix used in Twitch chat for this guild.
    /// </summary>
    [SlashCommand("prefix", "Change the Twitch chat command prefix")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public Task SetChannelPrefix(string prefix)
    {
        return Prefix(prefix);
    }

    /// <summary>
    ///     Nested slash group for Twitch custom command, counter, and info-command management.
    /// </summary>
    [Group("commands", "Manage Twitch chat commands")]
    public class TwitchCommandSlashGroup : TwitchSlashModuleBase
    {
        /// <summary>
        ///     Adds or updates a custom Twitch chat command.
        /// </summary>
        [SlashCommand("add", "Add or update a custom Twitch chat command")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task Add(string name, string response, string permission = "Everyone", int cooldownSeconds = 0)
        {
            return CommandAdd(name, response, permission, cooldownSeconds);
        }

        /// <summary>
        ///     Removes a custom Twitch chat command.
        /// </summary>
        [SlashCommand("remove", "Remove a custom Twitch chat command")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task Remove(string name)
        {
            return CommandRemove(name);
        }

        /// <summary>
        ///     Lists custom Twitch chat commands.
        /// </summary>
        [SlashCommand("list", "List custom Twitch chat commands")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public Task List()
        {
            return CommandList();
        }

        /// <summary>
        ///     Sets a named Twitch counter value.
        /// </summary>
        [SlashCommand("counter-set", "Set a Twitch counter value")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task CounterSetValue(string name, int value)
        {
            return CounterSet(name, value);
        }

        /// <summary>
        ///     Lists Twitch counters for this guild.
        /// </summary>
        [SlashCommand("counter-list", "List Twitch counters")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public Task CounterListValues()
        {
            return CounterList();
        }

        /// <summary>
        ///     Sets the Twitch schedule chat response.
        /// </summary>
        [SlashCommand("schedule-set", "Set the Twitch schedule chat response")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task SetSchedule(string message)
        {
            return ScheduleSet(message);
        }

        /// <summary>
        ///     Clears the Twitch schedule chat response.
        /// </summary>
        [SlashCommand("schedule-clear", "Clear the Twitch schedule chat response")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task ClearSchedule()
        {
            return ScheduleClear();
        }

        /// <summary>
        ///     Sets the Twitch socials chat response.
        /// </summary>
        [SlashCommand("socials-set", "Set the Twitch socials chat response")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task SetSocials(string message)
        {
            return SocialsSet(message);
        }

        /// <summary>
        ///     Clears the Twitch socials chat response.
        /// </summary>
        [SlashCommand("socials-clear", "Clear the Twitch socials chat response")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task ClearSocials()
        {
            return SocialsClear();
        }
    }

    /// <summary>
    ///     Nested slash group for Twitch repeating chat message timers.
    /// </summary>
    [Group("timers", "Manage Twitch repeating chat messages")]
    public class TwitchTimerSlashGroup : TwitchSlashModuleBase
    {
        /// <summary>
        ///     Adds or updates a repeating Twitch chat message timer.
        /// </summary>
        [SlashCommand("add", "Add or update a repeating Twitch chat message")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task Add(
            string name,
            string messages,
            int intervalMinutes = 10,
            int minChatMessages = 5,
            bool onlineOnly = true,
            bool randomizeMessages = false)
        {
            return TimerAdd(name, messages, intervalMinutes, minChatMessages, onlineOnly, randomizeMessages);
        }

        /// <summary>
        ///     Removes a repeating Twitch chat message timer.
        /// </summary>
        [SlashCommand("remove", "Remove a repeating Twitch chat message")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task Remove(string name)
        {
            return TimerRemove(name);
        }

        /// <summary>
        ///     Lists repeating Twitch chat message timers.
        /// </summary>
        [SlashCommand("list", "List repeating Twitch chat messages")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public Task List()
        {
            return TimerList();
        }

        /// <summary>
        ///     Enables a repeating Twitch chat message timer.
        /// </summary>
        [SlashCommand("enable", "Enable a repeating Twitch chat message")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task Enable(string name)
        {
            return TimerEnable(name);
        }

        /// <summary>
        ///     Disables a repeating Twitch chat message timer.
        /// </summary>
        [SlashCommand("disable", "Disable a repeating Twitch chat message")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task Disable(string name)
        {
            return TimerDisable(name);
        }

        /// <summary>
        ///     Sends a repeating Twitch chat message timer immediately.
        /// </summary>
        [SlashCommand("test", "Send a repeating Twitch chat message now")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task Test(string name)
        {
            return TimerTest(name);
        }
    }

    /// <summary>
    ///     Nested slash group for Twitch event notification templates.
    /// </summary>
    [Group("events", "Manage Twitch event notifications")]
    public class TwitchEventSlashGroup : TwitchSlashModuleBase
    {
        /// <summary>
        ///     Sets the Discord channel for go-live notifications.
        /// </summary>
        [SlashCommand("golive-channel", "Set the Discord channel for go-live notifications")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task SetGoLiveChannel(ITextChannel channel, string? message = null)
        {
            return SetGoLive(channel, message);
        }

        /// <summary>
        ///     Clears go-live notifications.
        /// </summary>
        [SlashCommand("golive-clear", "Clear the go-live notification channel")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task ClearGoLiveChannel()
        {
            return ClearGoLive();
        }

        /// <summary>
        ///     Sets the Discord channel for subscription notifications.
        /// </summary>
        [SlashCommand("sub-channel", "Set the Discord channel for Twitch subscription notifications")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task SetSubChannel(ITextChannel channel, string? message = null)
        {
            return SubChannel(channel, message);
        }

        /// <summary>
        ///     Clears subscription notifications.
        /// </summary>
        [SlashCommand("sub-clear", "Disable Twitch subscription notifications")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task ClearSubChannel()
        {
            return SubClear();
        }

        /// <summary>
        ///     Sets the Discord channel for raid notifications.
        /// </summary>
        [SlashCommand("raid-channel", "Set the Discord channel for Twitch raid notifications")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task SetRaidChannel(ITextChannel channel, string? message = null)
        {
            return RaidChannel(channel, message);
        }

        /// <summary>
        ///     Clears raid notifications.
        /// </summary>
        [SlashCommand("raid-clear", "Disable Twitch raid notifications")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task ClearRaidChannel()
        {
            return RaidClear();
        }

        /// <summary>
        ///     Sets the Discord channel for stream recap posts.
        /// </summary>
        [SlashCommand("recap-channel", "Set the Discord channel for stream recaps")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task SetRecapChannel(ITextChannel channel)
        {
            return RecapChannel(channel);
        }

        /// <summary>
        ///     Clears stream recap posts.
        /// </summary>
        [SlashCommand("recap-clear", "Disable stream recap posts")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task ClearRecapChannel()
        {
            return RecapClear();
        }
    }

    /// <summary>
    ///     Nested slash group for Twitch account links.
    /// </summary>
    [Group("links", "Manage Discord to Twitch account links")]
    public class TwitchLinkSlashGroup : TwitchSlashModuleBase
    {
        /// <summary>
        ///     Links a Discord user to their Twitch account.
        /// </summary>
        [SlashCommand("add", "Link a Discord user to their Twitch account")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task AddLink(IGuildUser user, string twitchUsername)
        {
            return Link(user, twitchUsername);
        }

        /// <summary>
        ///     Removes a Discord user's Twitch account link.
        /// </summary>
        [SlashCommand("remove", "Remove a Discord user's Twitch account link")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task RemoveLink(IGuildUser user)
        {
            return Unlink(user);
        }

        /// <summary>
        ///     Lists all Twitch account links for this server.
        /// </summary>
        [SlashCommand("list", "List all Twitch account links for this server")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public Task ListLinks()
        {
            return Links();
        }
    }

    /// <summary>
    ///     Nested slash group for Twitch role sync mappings.
    /// </summary>
    [Group("rolesync", "Manage Twitch role sync mappings")]
    public class TwitchRoleSyncSlashGroup : TwitchSlashModuleBase
    {
        /// <summary>
        ///     Adds a Twitch rank to Discord role sync mapping.
        /// </summary>
        [SlashCommand("add", "Sync a Twitch rank to a Discord role")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task AddRoleSync(string twitchRank, IRole role)
        {
            return RoleSyncAdd(twitchRank, role);
        }

        /// <summary>
        ///     Removes a Twitch role sync mapping.
        /// </summary>
        [SlashCommand("remove", "Remove a Twitch role sync mapping")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task RemoveRoleSync(string twitchRank, IRole role)
        {
            return RoleSyncRemove(twitchRank, role);
        }

        /// <summary>
        ///     Lists Twitch role sync mappings.
        /// </summary>
        [SlashCommand("list", "List Twitch role sync mappings")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public Task ListRoleSync()
        {
            return RoleSyncList();
        }
    }

    /// <summary>
    ///     Nested slash group for Twitch channel point redemption actions.
    /// </summary>
    [Group("redemptions", "Manage Twitch channel point redemption actions")]
    public class TwitchRedemptionSlashGroup : TwitchSlashModuleBase
    {
        /// <summary>
        ///     Adds or updates a channel point redemption action.
        /// </summary>
        [SlashCommand("add", "Add a channel point redemption action")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task AddRedemption(
            string rewardTitle,
            string? twitchResponse = null,
            ITextChannel? discordChannel = null,
            string? discordMessage = null)
        {
            return RedemptionAdd(rewardTitle, twitchResponse, discordChannel, discordMessage);
        }

        /// <summary>
        ///     Removes a channel point redemption action.
        /// </summary>
        [SlashCommand("remove", "Remove a channel point redemption action")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task RemoveRedemption(string rewardTitle)
        {
            return RedemptionRemove(rewardTitle);
        }

        /// <summary>
        ///     Lists channel point redemption actions.
        /// </summary>
        [SlashCommand("list", "List channel point redemption actions")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public Task ListRedemptions()
        {
            return RedemptionList();
        }
    }

    /// <summary>
    ///     Nested slash group for Twitch raid target suggestions.
    /// </summary>
    [Group("raidtargets", "Manage Twitch raid target suggestions")]
    public class TwitchRaidTargetSlashGroup : TwitchSlashModuleBase
    {
        /// <summary>
        ///     Adds or updates a Twitch raid target suggestion.
        /// </summary>
        [SlashCommand("add", "Add a Twitch raid target suggestion")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task AddRaidTarget(string twitchLogin, string? note = null)
        {
            return RaidTargetAdd(twitchLogin, note);
        }

        /// <summary>
        ///     Removes a Twitch raid target suggestion.
        /// </summary>
        [SlashCommand("remove", "Remove a Twitch raid target suggestion")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        [CheckPermissions]
        public Task RemoveRaidTarget(string twitchLogin)
        {
            return RaidTargetRemove(twitchLogin);
        }

        /// <summary>
        ///     Lists configured Twitch raid target suggestions.
        /// </summary>
        [SlashCommand("list", "List Twitch raid target suggestions")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public Task ListRaidTargets()
        {
            return RaidTargetList();
        }
    }
}