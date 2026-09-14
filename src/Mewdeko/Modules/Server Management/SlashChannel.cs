using System.Net.Http;
using Discord.Interactions;
using Humanizer;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Server_Management.Services;
using Mewdeko.Services.Settings;
using PermValue = Discord.PermValue;

namespace Mewdeko.Modules.Server_Management;

/// <summary>
///     Slash commands for channel operations such as lockdowns, locking, slowmode, webhooks and permission overwrites.
/// </summary>
/// <param name="config">The bot configuration settings.</param>
/// <param name="httpFactory">The http client factory used to download webhook avatars.</param>
[Group("channel", "Lock, unlock, nuke, slowmode and permission tools for channels")]
public class SlashChannel(BotConfigService config, IHttpClientFactory httpFactory)
    : MewdekoSlashModuleBase<ChannelCommandService>
{
    private static ulong GetRawPermissionValue(IEnumerable<ChannelPermission> permissions)
    {
        return permissions.Aggregate<ChannelPermission, ulong>(0,
            (current, permission) => current | (ulong)permission);
    }

    private async Task<ChannelPermission[]?> ParseChannelPermissions(string permissions)
    {
        var parsed = permissions
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => Enum.TryParse<ChannelPermission>(x, true, out var perm) ? perm : (ChannelPermission?)null)
            .Where(x => x.HasValue)
            .Select(x => x.Value)
            .Distinct()
            .ToArray();
        if (parsed.Length > 0) return parsed;
        await ErrorAsync(Strings.InvalidChannelPermissions(ctx.Guild.Id,
            string.Join(", ", Enum.GetNames<ChannelPermission>()))).ConfigureAwait(false);
        return null;
    }

    private static OverwritePermissions BuildOverwrite(OverwritePermissions? currentPerms, PermValue perm,
        ulong newPermsRaw)
    {
        if (currentPerms == null)
        {
            return perm == PermValue.Allow
                ? new OverwritePermissions(newPermsRaw, 0)
                : new OverwritePermissions(0, newPermsRaw);
        }

        var allowPermsRaw = GetRawPermissionValue(currentPerms.Value.ToAllowList());
        var denyPermsRaw = GetRawPermissionValue(currentPerms.Value.ToDenyList());

        switch (perm)
        {
            case PermValue.Allow:
                allowPermsRaw |= newPermsRaw;
                denyPermsRaw &= ~newPermsRaw;
                break;
            case PermValue.Deny:
                denyPermsRaw |= newPermsRaw;
                allowPermsRaw &= ~newPermsRaw;
                break;
            default:
                allowPermsRaw &= ~newPermsRaw;
                denyPermsRaw &= ~newPermsRaw;
                break;
        }

        return new OverwritePermissions(allowPermsRaw, denyPermsRaw);
    }

    private Task SendPermControlResult(PermValue perm, string mention, IGuildChannel channel,
        IEnumerable<ChannelPermission> perms)
    {
        var list = string.Join("\n", perms.Select(e => e.ToString()));
        return perm switch
        {
            PermValue.Inherit => ConfirmAsync(Strings.PermissionsInherit(ctx.Guild.Id, mention, channel, list)),
            PermValue.Allow => ConfirmAsync(Strings.PermissionsAllowedForUser(ctx.Guild.Id, mention,
                channel.ToString(), list)),
            _ => ConfirmAsync(Strings.PermissionsDeniedForUser(ctx.Guild.Id, mention, channel.ToString(), list))
        };
    }

    /// <summary>
    ///     Locks down the server based on the specified lockdown type (Joins, Readonly, Full).
    /// </summary>
    /// <param name="lockdownType">The type of lockdown to apply.</param>
    /// <param name="action">The action to take against new users who try to join during the lockdown (Kick or Ban).</param>
    /// <param name="overrideCheck">Whether to proceed with the lockdown regardless of permission issues.</param>
    [SlashCommand("lockdown", "Locks the server down against joins, messages, or both")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Lockdown(
        [Summary("type", "Joins, Readonly or Full")]
        ServerManagement.LockdownType lockdownType = ServerManagement.LockdownType.Readonly,
        [Summary("action", "What to do with users joining during a join lockdown")]
        PunishmentAction action = PunishmentAction.Ban,
        [Summary("override", "Continue even if permission checks fail")]
        bool overrideCheck = false)
    {
        await DeferAsync().ConfigureAwait(false);
        var embed = new EmbedBuilder()
            .WithDescription(Strings.LockdownInProgress(ctx.Guild.Id, config.Data.LoadingEmote))
            .WithColor(Mewdeko.OkColor);

        var loadingMessage = await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);

        if (lockdownType is ServerManagement.LockdownType.Joins or ServerManagement.LockdownType.Full)
        {
            if (action is not (PunishmentAction.Kick or PunishmentAction.Ban))
            {
                embed.WithDescription(Strings.JoinLockdownInvalidAction(ctx.Guild.Id))
                    .WithErrorColor();
                await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                return;
            }

            var missingActionPermissions =
                await Service.CheckJoinLockdownActionPermissions(ctx.Guild, action).ConfigureAwait(false);
            if (missingActionPermissions.Count != 0)
            {
                var missingPermsText = string.Join(", ", missingActionPermissions);
                embed.WithDescription(Strings.LockdownPermCheckFail(ctx.Guild.Id, missingPermsText))
                    .WithErrorColor();
                await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                return;
            }
        }

        if (lockdownType is ServerManagement.LockdownType.Full or ServerManagement.LockdownType.Readonly)
        {
            var missingPermissions =
                await Service.CheckLockdownPermissions(ctx.Guild, overrideCheck).ConfigureAwait(false);
            if (missingPermissions.Count != 0)
            {
                var missingPermsText = string.Join(", ", missingPermissions);
                embed.WithDescription(Strings.LockdownPermCheckFail(ctx.Guild.Id, missingPermsText))
                    .WithErrorColor();
                await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);

                if (!overrideCheck)
                    return;
            }

            await Service.StoreOriginalPermissions(ctx.Guild).ConfigureAwait(false);
        }

        var check = await Service.LockdownGuild(ctx.Guild, lockdownType, action).ConfigureAwait(false);
        if (check.Item1)
        {
            embed.WithDescription(Strings.LockdownAlreadyEnabled(ctx.Guild.Id, check.Item2))
                .WithErrorColor();
            await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
            return;
        }

        switch (lockdownType)
        {
            case ServerManagement.LockdownType.Joins:
                embed.WithDescription(Strings.LockdownJoinsEnabled(ctx.Guild.Id, ctx.Guild.Name, action.ToString()))
                    .WithColor(Mewdeko.OkColor);
                await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                break;

            case ServerManagement.LockdownType.Readonly:
                await Service.ApplyLockdown(ctx.Guild).ConfigureAwait(false);
                embed.WithDescription(Strings.LockdownReadonlyEnabled(ctx.Guild.Id, ctx.Guild.Name))
                    .WithColor(Mewdeko.OkColor);
                await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                break;

            case ServerManagement.LockdownType.Full:
                await Service.ApplyLockdown(ctx.Guild).ConfigureAwait(false);
                embed.WithDescription(Strings.LockdownFullEnabled(ctx.Guild.Id, ctx.Guild.Name, action.ToString()))
                    .WithColor(Mewdeko.OkColor);
                await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    ///     Lifts the lockdown based on the specified lockdown type (Joins, Readonly, Full).
    /// </summary>
    /// <param name="lockdownType">The type of lockdown to lift.</param>
    [SlashCommand("lift-lockdown", "Lifts a join, readonly or full lockdown")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task LiftLockdown(
        [Summary("type", "Joins, Readonly or Full")]
        ServerManagement.LockdownType lockdownType = ServerManagement.LockdownType.Readonly)
    {
        await DeferAsync().ConfigureAwait(false);
        var embed = new EmbedBuilder()
            .WithDescription(Strings.LockdownLiftInProgress(ctx.Guild.Id, config.Data.LoadingEmote))
            .WithColor(Mewdeko.OkColor);

        var loadingMessage = await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);

        switch (lockdownType)
        {
            case ServerManagement.LockdownType.Joins:
                if (Service.IsGuildInLockdown(ctx.Guild, ServerManagement.LockdownType.Joins))
                {
                    await Service.LiftLockdown(ctx.Guild).ConfigureAwait(false);
                    embed.WithDescription(Strings.LockdownJoinsDisabled(ctx.Guild.Id, ctx.Guild.Name))
                        .WithColor(Mewdeko.OkColor);
                    await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                }
                else
                {
                    embed.WithDescription(Strings.NoLockdownJoins(ctx.Guild.Id))
                        .WithErrorColor();
                    await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                }

                break;

            case ServerManagement.LockdownType.Readonly:
                await Service.LiftLockdown(ctx.Guild).ConfigureAwait(false);
                await Service.RestoreOriginalPermissions(ctx.Guild).ConfigureAwait(false);
                embed.WithDescription(Strings.LockdownReadonlyDisabled(ctx.Guild.Id, ctx.Guild.Name))
                    .WithColor(Mewdeko.OkColor);
                await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                break;

            case ServerManagement.LockdownType.Full:
                await Service.LiftLockdown(ctx.Guild).ConfigureAwait(false);
                await Service.RestoreOriginalPermissions(ctx.Guild).ConfigureAwait(false);
                embed.WithDescription(Strings.LockdownFullDisabled(ctx.Guild.Id, ctx.Guild.Name))
                    .WithColor(Mewdeko.OkColor);
                await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                await Service.LiftLockdown(ctx.Guild).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    ///     Moves the command issuer to a specified voice channel.
    /// </summary>
    /// <param name="channel">The target voice channel to move the user to.</param>
    [SlashCommand("move-to", "Moves you to the given voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task MoveTo([Summary("channel", "The voice channel to move to")] IVoiceChannel channel)
    {
        var use = ctx.User as IGuildUser;
        if (use?.VoiceChannel == null)
        {
            await ErrorAsync(Strings.MovetoNoVoice(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await use.ModifyAsync(x => x.Channel = new Optional<IVoiceChannel>(channel)).ConfigureAwait(false);
        await ConfirmAsync(Strings.MovetoSuccess(ctx.Guild.Id, Format.Bold(channel.Name))).ConfigureAwait(false);
    }

    /// <summary>
    ///     Moves a specified user to a given voice channel.
    /// </summary>
    /// <param name="user">The user to be moved.</param>
    /// <param name="channel">The target voice channel to move the user to.</param>
    [SlashCommand("move-user-to", "Moves a user to the given voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task MoveUserTo([Summary("user", "The user to move")] IGuildUser user,
        [Summary("channel", "The voice channel to move them to")]
        IVoiceChannel channel)
    {
        if (user.VoiceChannel == null)
        {
            await ErrorAsync(Strings.MoveuserNotInVoice(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await user.ModifyAsync(x => x.Channel = new Optional<IVoiceChannel>(channel)).ConfigureAwait(false);
        await ConfirmAsync(Strings.MoveuserSuccess(ctx.Guild.Id, user.Mention, Format.Bold(channel.Name)))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Brings a user to the command issuer's current voice channel.
    /// </summary>
    /// <param name="user">The user to grab and move.</param>
    [SlashCommand("grab", "Pulls a user into your current voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Grab([Summary("user", "The user to grab")] IGuildUser user)
    {
        var vc = ((IGuildUser)ctx.User).VoiceChannel;
        if (vc == null)
        {
            await ErrorAsync(Strings.GrabNotInVoice(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (user.VoiceChannel == null)
        {
            await ErrorAsync(Strings.GrabUserNotInVoice(ctx.Guild.Id, user.Mention)).ConfigureAwait(false);
            return;
        }

        await user.ModifyAsync(x => x.Channel = new Optional<IVoiceChannel>(vc)).ConfigureAwait(false);
        await ConfirmAsync(Strings.GrabSuccess(ctx.Guild.Id, user.Mention, vc.Name)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Unlocks the server by allowing @everyone to send messages again.
    /// </summary>
    [SlashCommand("unlockdown", "Restores send messages for @everyone at the role level")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task Unlockdown()
    {
        if (ctx.Guild.EveryoneRole.Permissions.SendMessages)
        {
            await ErrorAsync(Strings.UnlockdownNotLocked(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var everyonerole = ctx.Guild.EveryoneRole;
        var newperms = everyonerole.Permissions.Modify(sendMessages: true);
        await everyonerole.ModifyAsync(x => x.Permissions = newperms).ConfigureAwait(false);
        await ConfirmAsync(Strings.UnlockdownSuccess(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes and recreates a text channel, effectively "nuking" it.
    /// </summary>
    /// <param name="chan3">Optional channel to nuke. Defaults to the current channel.</param>
    [SlashCommand("nuke", "Deletes and recreates a text channel with the same settings")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task Nuke([Summary("channel", "The channel to nuke, defaults to this one")] ITextChannel? chan3 = null)
    {
        var embed = new EmbedBuilder
        {
            Color = Mewdeko.ErrorColor, Description = Strings.NukeConfirm(ctx.Guild.Id)
        };
        if (!await PromptUserConfirmAsync(embed, ctx.User.Id).ConfigureAwait(false)) return;
        var chan = chan3 ?? ctx.Channel as ITextChannel;
        if (chan == null) return;

        await chan.DeleteAsync().ConfigureAwait(false);

        var chan2 = await ctx.Guild.CreateTextChannelAsync(chan.Name, x =>
        {
            x.Position = chan.Position;
            if (chan.Topic is not null) x.Topic = chan.Topic;
            x.PermissionOverwrites = new Optional<IEnumerable<Overwrite>>(chan.PermissionOverwrites);
            x.IsNsfw = chan.IsNsfw;
            x.CategoryId = chan.CategoryId;
            x.SlowModeInterval = chan.SlowModeInterval;
        }).ConfigureAwait(false);

        await chan2.SendMessageAsync("https://pa1.narvii.com/6463/6494fab512c8f2ac0d652c44dae78be4cb644569_hq.gif")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Denies @everyone from sending messages in a channel.
    /// </summary>
    /// <param name="channel">The channel to lock. Defaults to the current channel.</param>
    [SlashCommand("lock", "Stops @everyone from sending messages in a channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [RequireBotPermission(GuildPermission.ManageMessages)]
    public async Task Lock(
        [Summary("channel", "The channel to lock, defaults to this one")]
        ITextChannel? channel = null)
    {
        channel ??= ctx.Channel as ITextChannel;
        if (channel == null) return;
        var currentPerms = channel.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ?? new OverwritePermissions();
        await channel.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
            currentPerms.Modify(sendMessages: PermValue.Deny)).ConfigureAwait(false);
        await ctx.Interaction.RespondAsync(Strings.LockSuccess(ctx.Guild.Id, config.Data.SuccessEmote,
            channel.Mention)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Unlocks a specific channel, allowing everyone to send messages again.
    /// </summary>
    /// <param name="channel">The channel to unlock. Defaults to the current channel.</param>
    [SlashCommand("unlock", "Lets @everyone send messages in a channel again")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    [RequireBotPermission(GuildPermission.ManageMessages)]
    public async Task Unlock(
        [Summary("channel", "The channel to unlock, defaults to this one")]
        ITextChannel? channel = null)
    {
        channel ??= ctx.Channel as ITextChannel;
        if (channel == null) return;
        var currentPerms = channel.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ?? new OverwritePermissions();
        await channel.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
            currentPerms.Modify(sendMessages: PermValue.Inherit)).ConfigureAwait(false);
        await ctx.Interaction.RespondAsync(Strings.ChannelUnlocked(ctx.Guild.Id, config.Data.SuccessEmote,
            channel.Mention)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets, toggles or removes slowmode in a channel.
    /// </summary>
    /// <param name="time">The slowmode duration. Omit to toggle between off and one minute.</param>
    /// <param name="channel">The channel to apply slowmode to. Defaults to the current channel.</param>
    [SlashCommand("slowmode", "Sets or toggles slowmode in a channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task Slowmode(
        [Summary("time", "Slowmode interval, e.g. 30s or 5m. Omit to toggle")]
        TimeSpan? time = null,
        [Summary("channel", "The channel, defaults to this one")]
        ITextChannel? channel = null)
    {
        channel ??= ctx.Channel as ITextChannel;
        if (channel == null) return;
        var seconds = time.HasValue ? (int)time.Value.TotalSeconds : 0;

        switch (seconds)
        {
            case 0:
                switch (channel.SlowModeInterval)
                {
                    case 0:
                        await channel.ModifyAsync(x => x.SlowModeInterval = 60).ConfigureAwait(false);
                        await ConfirmAsync(Strings.SlowmodeEnabledOneMinute(ctx.Guild.Id, channel.Mention))
                            .ConfigureAwait(false);
                        return;
                    case > 0:
                        await channel.ModifyAsync(x => x.SlowModeInterval = 0).ConfigureAwait(false);
                        await ConfirmAsync(Strings.SlowmodeDisabled(ctx.Guild.Id, channel.Mention))
                            .ConfigureAwait(false);
                        break;
                }

                return;
            case >= 21600:
                await ErrorAsync(Strings.SlowmodeMax(ctx.Guild.Id)).ConfigureAwait(false);
                break;
            default:
                await channel.ModifyAsync(x => x.SlowModeInterval = seconds).ConfigureAwait(false);
                await ConfirmAsync(Strings.SlowmodeEnabledFor(ctx.Guild.Id, channel.Mention,
                    TimeSpan.FromSeconds(seconds).Humanize(maxUnit: TimeUnit.Hour))).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    ///     Creates a webhook in a text channel with an optional custom avatar.
    /// </summary>
    /// <param name="channel">The channel to create the webhook in.</param>
    /// <param name="name">The webhook name.</param>
    /// <param name="avatar">An optional image attachment to use as the avatar.</param>
    /// <param name="avatarUrl">An optional image url to use as the avatar.</param>
    [SlashCommand("create-webhook", "Creates a webhook in a channel and DMs you the url")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task CreateWebhook([Summary("channel", "The channel for the webhook")] ITextChannel channel,
        [Summary("name", "The webhook name")] string name,
        [Summary("avatar", "Image to use as the avatar")]
        IAttachment? avatar = null,
        [Summary("avatar-url", "Image url to use as the avatar")]
        string? avatarUrl = null)
    {
        await DeferAsync().ConfigureAwait(false);
        if (avatarUrl == null && avatar != null && avatar.Url.IsImage())
            avatarUrl = avatar.Url;

        if (avatarUrl != null)
        {
            using var http = httpFactory.CreateClient();
            using var sr = await http.GetAsync(avatarUrl, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var imgStream = imgData.ToStream();
            await using var _ = imgStream.ConfigureAwait(false);
            var wh = await channel.CreateWebhookAsync(name, imgStream).ConfigureAwait(false);
            await ctx.Interaction.FollowupAsync(Strings.WebhookCreated(ctx.Guild.Id, config.Data.SuccessEmote,
                wh.Name, channel.Mention)).ConfigureAwait(false);
            await ctx.User.SendErrorAsync(Strings.WebhookCreatedDontShare(ctx.Guild.Id, wh.Id, wh.Token))
                .ConfigureAwait(false);
        }
        else
        {
            var wh = await channel.CreateWebhookAsync(name).ConfigureAwait(false);
            await ctx.Interaction.FollowupAsync(Strings.WebhookCreatedSimple(ctx.Guild.Id, config.Data.SuccessEmote,
                wh.Name, channel.Mention)).ConfigureAwait(false);
            await ctx.User.SendErrorAsync(Strings.WebhookCreatedDontShare(ctx.Guild.Id, wh.Id, wh.Token))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Allows, denies, or resets specific channel permissions for a role or a user.
    /// </summary>
    /// <param name="channel">The channel for which permissions are being modified.</param>
    /// <param name="permissions">Space separated channel permission names.</param>
    /// <param name="perm">The action to perform (Allow, Deny, Inherit).</param>
    /// <param name="role">The role to which the permission modifications apply.</param>
    /// <param name="user">The user to which the permission modifications apply.</param>
    [SlashCommand("perm-control", "Allows, denies or inherits channel permissions for a role or user")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task PermControl([Summary("channel", "The channel to modify")] IGuildChannel channel,
        [Summary("permissions", "Space separated permission names, e.g. SendMessages ViewChannel")]
        string permissions,
        [Summary("action", "Allow, Deny or Inherit")]
        PermValue perm,
        [Summary("role", "The role to modify")]
        IRole? role = null,
        [Summary("user", "The user to modify")]
        IGuildUser? user = null)
    {
        if (role is null == user is null)
        {
            await ReplyErrorAsync(Strings.PermControlRoleOrUser(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var perms = await ParseChannelPermissions(permissions).ConfigureAwait(false);
        if (perms is null) return;
        var newPermsRaw = GetRawPermissionValue(perms);
        var mention = role?.Mention ?? user!.Mention;
        var currentPerms = role is not null
            ? channel.GetPermissionOverwrite(role)
            : channel.GetPermissionOverwrite(user!);
        if (currentPerms == null && perm == PermValue.Inherit)
        {
            await ConfirmAsync(Strings.PermissionsInherit(ctx.Guild.Id, mention, channel,
                string.Join("\n", perms.Select(e => e.ToString())))).ConfigureAwait(false);
            return;
        }

        var result = BuildOverwrite(currentPerms, perm, newPermsRaw);
        if (role is not null)
            await channel.AddPermissionOverwriteAsync(role, result).ConfigureAwait(false);
        else
            await channel.AddPermissionOverwriteAsync(user!, result).ConfigureAwait(false);
        await SendPermControlResult(perm, mention, channel, perms).ConfigureAwait(false);
    }

    /// <summary>
    ///     Commands for creating categories and batches of channels.
    /// </summary>
    /// <param name="config">The bot configuration settings.</param>
    [Group("create", "Create categories and batches of channels")]
    public class ChannelCreate(BotConfigService config) : MewdekoSlashSubmodule<ChannelCommandService>
    {
        private static string[] SplitNames(string names)
        {
            return names.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        ///     Adds multiple text channels to an existing category.
        /// </summary>
        /// <param name="chan">The target category channel.</param>
        /// <param name="channels">Space separated names of the text channels to be added.</param>
        [SlashCommand("category-text", "Adds text channels to an existing category")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CreateCatTxtChans([Summary("category", "The category to add to")] ICategoryChannel chan,
            [Summary("names", "Space separated channel names")]
            string channels)
        {
            var names = SplitNames(channels);
            await DeferAsync().ConfigureAwait(false);
            var eb = new EmbedBuilder();
            eb.WithOkColor();
            eb.WithDescription(
                Strings.AddingTextChannels(ctx.Guild.Id, config.Data.LoadingEmote, names.Length, chan.Name));
            var msg = await ctx.Interaction.FollowupAsync(embed: eb.Build()).ConfigureAwait(false);
            foreach (var i in names)
                await ctx.Guild.CreateTextChannelAsync(i, x => x.CategoryId = chan.Id).ConfigureAwait(false);

            var eb2 = new EmbedBuilder();
            eb2.WithDescription(Strings.AddedTextChannels(ctx.Guild.Id, names.Length.ToString(), chan.Name));
            eb2.WithOkColor();
            await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds multiple voice channels to an existing category.
        /// </summary>
        /// <param name="chan">The target category channel.</param>
        /// <param name="channels">Space separated names of the voice channels to be added.</param>
        [SlashCommand("category-voice", "Adds voice channels to an existing category")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CreateCatVcChans([Summary("category", "The category to add to")] ICategoryChannel chan,
            [Summary("names", "Space separated channel names")]
            string channels)
        {
            var names = SplitNames(channels);
            await DeferAsync().ConfigureAwait(false);
            var eb = new EmbedBuilder();
            eb.WithOkColor();
            eb.WithDescription(
                Strings.AddingVoiceChannels(ctx.Guild.Id, config.Data.LoadingEmote, names.Length, chan.Name));
            var msg = await ctx.Interaction.FollowupAsync(embed: eb.Build()).ConfigureAwait(false);
            foreach (var i in names)
                await ctx.Guild.CreateVoiceChannelAsync(i, x => x.CategoryId = chan.Id).ConfigureAwait(false);

            var eb2 = new EmbedBuilder();
            eb2.WithDescription(Strings.ChannelsAddedVoice(ctx.Guild.Id, names.Length, chan.Name));
            eb2.WithOkColor();
            await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Creates a category and the specified text channels inside it.
        /// </summary>
        /// <param name="catName">The name of the category to create.</param>
        /// <param name="channels">Space separated names of the text channels to create.</param>
        [SlashCommand("category-with-text", "Creates a category with text channels in it")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CreateCatAndTxtChannels([Summary("category", "The category name")] string catName,
            [Summary("names", "Space separated channel names")]
            string channels)
        {
            var names = SplitNames(channels);
            await DeferAsync().ConfigureAwait(false);
            var eb = new EmbedBuilder();
            eb.WithOkColor()
                .WithDescription(Strings.CreatingCategory(ctx.Guild.Id, config.Data.LoadingEmote, catName,
                    names.Length, "Text"));
            var msg = await ctx.Interaction.FollowupAsync(embed: eb.Build()).ConfigureAwait(false);
            var cat = await ctx.Guild.CreateCategoryAsync(catName).ConfigureAwait(false);
            foreach (var i in names)
                await ctx.Guild.CreateTextChannelAsync(i, x => x.CategoryId = cat.Id).ConfigureAwait(false);

            var eb2 = new EmbedBuilder();
            eb2.WithDescription(Strings.CreatedCategory(ctx.Guild.Id, catName, names.Length, "Text"));
            eb2.WithOkColor();
            await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Creates a category and the specified voice channels inside it.
        /// </summary>
        /// <param name="catName">The name of the category to create.</param>
        /// <param name="channels">Space separated names of the voice channels to create.</param>
        [SlashCommand("category-with-voice", "Creates a category with voice channels in it")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CreateCatAndVcChannels([Summary("category", "The category name")] string catName,
            [Summary("names", "Space separated channel names")]
            string channels)
        {
            var names = SplitNames(channels);
            await DeferAsync().ConfigureAwait(false);
            var eb = new EmbedBuilder();
            eb.WithOkColor()
                .WithDescription(Strings.CreatingCategory(ctx.Guild.Id, config.Data.LoadingEmote, catName,
                    names.Length, "Voice"));
            var msg = await ctx.Interaction.FollowupAsync(embed: eb.Build()).ConfigureAwait(false);
            var cat = await ctx.Guild.CreateCategoryAsync(catName).ConfigureAwait(false);
            foreach (var i in names)
                await ctx.Guild.CreateVoiceChannelAsync(i, x => x.CategoryId = cat.Id).ConfigureAwait(false);

            var eb2 = new EmbedBuilder();
            eb2.WithDescription(Strings.CreatedCategory(ctx.Guild.Id, catName, names.Length, "Voice"));
            eb2.WithOkColor();
            await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
        }
    }
}