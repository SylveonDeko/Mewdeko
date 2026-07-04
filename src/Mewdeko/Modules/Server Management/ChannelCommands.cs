using System.Net.Http;
using Discord.Commands;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Server_Management.Services;
using Mewdeko.Services.Settings;
using PermValue = Discord.PermValue;

namespace Mewdeko.Modules.Server_Management;

public partial class ServerManagement
{
    /// <summary>
    ///     Represents the type of lockdown that can be applied to the server.
    /// </summary>
    public enum LockdownType
    {
        /// <summary>
        ///     Lockdown to prevent new user joins.
        /// </summary>
        Joins,

        /// <summary>
        ///     Lockdown to make the server read-only by removing send message permissions for @everyone.
        /// </summary>
        Readonly,

        /// <summary>
        ///     Full lockdown: prevent joins and make the server read-only.
        /// </summary>
        Full
    }

    /// <summary>
    ///     Manages channel-specific operations such as locking, unlocking, and modifying slowmode settings.
    /// </summary>
    [Group]
    public class ChannelCommands(BotConfigService config, HttpClient http) : MewdekoSubmodule<ChannelCommandService>
    {
        /// <summary>
        ///     Locks down the server based on the specified lockdown type (Joins, Readonly, Full).
        /// </summary>
        /// <param name="lockdownType">
        ///     The type of lockdown to apply. It can be <see cref="LockdownType.Joins" />,
        ///     <see cref="LockdownType.Readonly" />, or <see cref="LockdownType.Full" />.
        /// </param>
        /// <param name="action">Optional: The action to take against new users who try to join during the lockdown (Kick or Ban).</param>
        /// <param name="overrideCheck">
        ///     Optional: Specifies whether to override permission checks and proceed with the lockdown
        ///     regardless of permission issues.
        /// </param>
        /// <returns>A task that represents the asynchronous operation of locking down the server.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task Lockdown(LockdownType lockdownType = LockdownType.Readonly,
            PunishmentAction action = PunishmentAction.Ban,
            bool overrideCheck = false)
        {
            var embed = new EmbedBuilder()
                .WithDescription(Strings.LockdownInProgress(ctx.Guild.Id, config.Data.LoadingEmote))
                .WithColor(Mewdeko.OkColor);

            var loadingMessage = await ctx.Channel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);

            if (lockdownType is LockdownType.Joins or LockdownType.Full)
            {
                if (action is not (PunishmentAction.Kick or PunishmentAction.Ban))
                {
                    embed.WithDescription("Join lockdown action must be `Kick` or `Ban`.")
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

            if (lockdownType is LockdownType.Full or LockdownType.Readonly)
            {
                var missingPermissions =
                    await Service.CheckLockdownPermissions(ctx.Guild, overrideCheck).ConfigureAwait(false);
                if (missingPermissions.Count != 0)
                {
                    var missingPermsText = string.Join(", ", missingPermissions);
                    embed.WithDescription(Strings.LockdownPermCheckFail(ctx.Guild.Id,
                            missingPermsText))
                        .WithErrorColor();
                    await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);

                    if (!overrideCheck)
                        return;
                }

                await Service.StoreOriginalPermissions(ctx.Guild).ConfigureAwait(false);
            }

            var check = await Service.LockdownGuild(ctx.Guild, lockdownType, action);
            if (check.Item1)
            {
                embed.WithDescription(Strings.LockdownAlreadyEnabled(ctx.Guild.Id, check.Item2))
                    .WithErrorColor();
                await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                return;
            }


            switch (lockdownType)
            {
                case LockdownType.Joins:
                    embed.WithDescription(Strings.LockdownJoinsEnabled(ctx.Guild.Id, ctx.Guild.Name,
                            action.ToString()))
                        .WithColor(Mewdeko.OkColor);
                    await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                    break;

                case LockdownType.Readonly:
                    await Service.ApplyLockdown(ctx.Guild).ConfigureAwait(false);
                    embed.WithDescription(
                            Strings.LockdownReadonlyEnabled(ctx.Guild.Id, ctx.Guild.Name))
                        .WithColor(Mewdeko.OkColor);
                    await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                    break;

                case LockdownType.Full:
                    await Service.ApplyLockdown(ctx.Guild).ConfigureAwait(false);
                    embed.WithDescription(Strings.LockdownFullEnabled(ctx.Guild.Id, ctx.Guild.Name,
                            action.ToString()))
                        .WithColor(Mewdeko.OkColor);
                    await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                    break;
            }
        }

        /// <summary>
        ///     Lifts the lockdown based on the specified lockdown type (Joins, Readonly, Full).
        /// </summary>
        /// <param name="lockdownType">
        ///     The type of lockdown to lift. It can be <see cref="LockdownType.Joins" />,
        ///     <see cref="LockdownType.Readonly" />, or <see cref="LockdownType.Full" />.
        /// </param>
        /// <returns>A task that represents the asynchronous operation of lifting the lockdown.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task LiftLockdown(LockdownType lockdownType = LockdownType.Readonly)
        {
            var embed = new EmbedBuilder()
                .WithDescription(Strings.LockdownLiftInProgress(ctx.Guild.Id, config.Data.LoadingEmote))
                .WithColor(Mewdeko.OkColor);

            var loadingMessage = await ctx.Channel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);

            switch (lockdownType)
            {
                case LockdownType.Joins:
                    if (Service.IsGuildInLockdown(ctx.Guild, LockdownType.Joins))
                    {
                        await Service.LiftLockdown(ctx.Guild);
                        embed.WithDescription(Strings.LockdownJoinsDisabled(ctx.Guild.Id,
                                ctx.Guild.Name))
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

                case LockdownType.Readonly:
                    await Service.LiftLockdown(ctx.Guild);
                    await Service.RestoreOriginalPermissions(ctx.Guild).ConfigureAwait(false);
                    embed.WithDescription(Strings.LockdownReadonlyDisabled(ctx.Guild.Id,
                            ctx.Guild.Name))
                        .WithColor(Mewdeko.OkColor);
                    await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                    break;

                case LockdownType.Full:
                    await Service.LiftLockdown(ctx.Guild);
                    await Service.RestoreOriginalPermissions(ctx.Guild).ConfigureAwait(false);
                    embed.WithDescription(Strings.LockdownFullDisabled(ctx.Guild.Id, ctx.Guild.Name))
                        .WithColor(Mewdeko.OkColor);
                    await loadingMessage.ModifyAsync(x => x.Embed = embed.Build()).ConfigureAwait(false);
                    await Service.LiftLockdown(ctx.Guild);
                    break;
            }
        }


        /// <summary>
        ///     Moves the command issuer to a specified voice channel.
        /// </summary>
        /// <param name="channel">The target voice channel to move the user to.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task MoveTo(IVoiceChannel channel)
        {
            var use = ctx.User as IGuildUser;
            if (use.VoiceChannel == null)
            {
                await ctx.Channel.SendErrorAsync(Strings.MovetoNoVoice(ctx.Guild.Id), Config);
                return;
            }

            await use.ModifyAsync(x => x.Channel = new Optional<IVoiceChannel>(channel)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.MovetoSuccess(ctx.Guild.Id, Format.Bold(channel.Name)))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Moves a specified user to a given voice channel.
        /// </summary>
        /// <param name="user">The user to be moved.</param>
        /// <param name="channel">The target voice channel to move the user to.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task MoveUserTo(IGuildUser user, IVoiceChannel channel)
        {
            if (user.VoiceChannel == null)
            {
                await ctx.Channel.SendErrorAsync(Strings.MoveuserNotInVoice(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            await user.ModifyAsync(x => x.Channel = new Optional<IVoiceChannel>(channel)).ConfigureAwait(false);
            await ctx.Channel
                .SendConfirmAsync(Strings.MoveuserSuccess(ctx.Guild.Id, user.Mention, Format.Bold(channel.Name)))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Brings a user to the command issuer's current voice channel.
        /// </summary>
        /// <param name="user">The user to grab and move.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Grab(IGuildUser user)
        {
            var vc = ((IGuildUser)ctx.User).VoiceChannel;
            if (vc == null)
            {
                await ctx.Channel.SendErrorAsync(Strings.GrabNotInVoice(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            if (user.VoiceChannel == null)
            {
                await ctx.Channel.SendErrorAsync(Strings.GrabUserNotInVoice(ctx.Guild.Id, user.Mention), Config);
                return;
            }

            await user.ModifyAsync(x => x.Channel = new Optional<IVoiceChannel>(vc)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.GrabSuccess(ctx.Guild.Id, user.Mention, user.VoiceChannel.Name))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Unlocks the server by allowing @everyone to send messages again.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task Unlockdown()
        {
            if (ctx.Guild.EveryoneRole.Permissions.SendMessages)
            {
                await ctx.Channel.SendErrorAsync(Strings.UnlockdownNotLocked(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            var everyonerole = ctx.Guild.EveryoneRole;
            var newperms = everyonerole.Permissions.Modify(sendMessages: true);
            await everyonerole.ModifyAsync(x => x.Permissions = newperms).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.UnlockdownSuccess(ctx.Guild.Id));
        }

        /// <summary>
        ///     Deletes and recreates a text channel, effectively "nuking" it.
        /// </summary>
        /// <param name="chan3">Optional parameter to specify a channel to nuke.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task Nuke(ITextChannel? chan3 = null)
        {
            var embed = new EmbedBuilder
            {
                Color = Mewdeko.ErrorColor, Description = Strings.NukeConfirm(ctx.Guild.Id)
            };
            if (!await PromptUserConfirmAsync(embed, ctx.User.Id).ConfigureAwait(false)) return;
            ITextChannel chan;
            if (chan3 is null)
                chan = ctx.Channel as ITextChannel;
            else
                chan = chan3;

            if (chan != null)
            {
                try
                {
                    await chan.DeleteAsync().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

                var chan2 = await ctx.Guild.CreateTextChannelAsync(chan.Name, x =>
                {
                    x.Position = chan.Position;
                    if (chan.Topic is not null) x.Topic = chan.Topic;
                    x.PermissionOverwrites = new Optional<IEnumerable<Overwrite>>(chan.PermissionOverwrites);
                    x.IsNsfw = chan.IsNsfw;
                    x.CategoryId = chan.CategoryId;
                    x.SlowModeInterval = chan.SlowModeInterval;
                }).ConfigureAwait(false);

                await chan2.SendMessageAsync(
                        "https://pa1.narvii.com/6463/6494fab512c8f2ac0d652c44dae78be4cb644569_hq.gif")
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Enables or disables slowmode in a channel, with customizable duration.
        /// </summary>
        /// <param name="channel">Optional parameter to specify a channel to apply slowmode.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [BotPerm(GuildPermission.ManageMessages)]
        public async Task Lock(SocketTextChannel? channel = null)
        {
            if (channel == null)
            {
                var tch = ctx.Channel as SocketTextChannel;
                var currentPerms = tch.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ??
                                   new OverwritePermissions();
                await tch.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                    currentPerms.Modify(sendMessages: PermValue.Deny)).ConfigureAwait(false);
                await ctx.Channel
                    .SendMessageAsync(Strings.LockSuccess(ctx.Guild.Id, config.Data.SuccessEmote, tch.Mention))
                    .ConfigureAwait(false);
            }
            else
            {
                var currentPerms = channel.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ??
                                   new OverwritePermissions();
                await channel.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                    currentPerms.Modify(sendMessages: PermValue.Deny)).ConfigureAwait(false);
                await ctx.Channel.SendMessageAsync(Strings.LockSuccess(ctx.Guild.Id, config.Data.SuccessEmote,
                    channel.Mention));
            }
        }

        /// <summary>
        ///     Creates a category and the specified channels in a server.
        /// </summary>
        /// <param name="catName">The name of the category to create</param>
        /// <param name="channels">The names of the channels to create</param>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task CreateCatAndTxtChannels(string catName, params string[] channels)
        {
            var eb = new EmbedBuilder();
            eb.WithOkColor()
                .WithDescription(Strings.CreatingCategory(ctx.Guild.Id, config.Data.LoadingEmote, catName,
                    channels.Length, "Text"));
            var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            var cat = await ctx.Guild.CreateCategoryAsync(catName).ConfigureAwait(false);
            foreach (var i in channels)
                await ctx.Guild.CreateTextChannelAsync(i, x => x.CategoryId = cat.Id).ConfigureAwait(false);

            var eb2 = new EmbedBuilder();
            eb2.WithDescription(Strings.CreatedCategory(ctx.Guild.Id, catName, channels.Length, "Text"));
            eb2.WithOkColor();
            await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Creates a category with multiple voice channels in it.
        /// </summary>
        /// <param name="catName">The name of the category to be created.</param>
        /// <param name="channels">The names of the voice channels to be created within the category.</param>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task CreateCatAndVcChannels(string catName, params string[] channels)
        {
            var eb = new EmbedBuilder();
            eb.WithOkColor()
                .WithDescription(Strings.CreatingCategory(ctx.Guild.Id, config.Data.LoadingEmote, catName,
                    channels.Length, "Voice"));
            var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            var cat = await ctx.Guild.CreateCategoryAsync(catName).ConfigureAwait(false);
            foreach (var i in channels)
                await ctx.Guild.CreateVoiceChannelAsync(i, x => x.CategoryId = cat.Id).ConfigureAwait(false);

            var eb2 = new EmbedBuilder();
            eb2.WithDescription(Strings.CreatedCategory(ctx.Guild.Id, catName, channels.Length, "Voice"));
            eb2.WithOkColor();
            await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds multiple voice channels to an existing category.
        /// </summary>
        /// <param name="chan">The target category channel.</param>
        /// <param name="channels">The names of the voice channels to be added.</param>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task CreateCatVcChans(ICategoryChannel chan, params string[] channels)
        {
            var eb = new EmbedBuilder();
            eb.WithOkColor();
            eb.WithDescription(
                Strings.AddingVoiceChannels(ctx.Guild.Id, config.Data.LoadingEmote, channels.Length, chan.Name));
            var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            foreach (var i in channels)
                await ctx.Guild.CreateVoiceChannelAsync(i, x => x.CategoryId = chan.Id).ConfigureAwait(false);

            var eb2 = new EmbedBuilder();
            eb2.WithDescription(Strings.ChannelsAddedVoice(ctx.Guild.Id, channels.Length, chan.Name));
            eb2.WithOkColor();
            await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds multiple text channels to an existing category.
        /// </summary>
        /// <param name="chan">The target category channel.</param>
        /// <param name="channels">The names of the text channels to be added.</param>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task CreateCatTxtChans(ICategoryChannel chan, params string[] channels)
        {
            var eb = new EmbedBuilder();
            eb.WithOkColor();
            eb.WithDescription(
                Strings.AddingTextChannels(ctx.Guild.Id, config.Data.LoadingEmote, channels.Length, chan.Name));
            var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            foreach (var i in channels)
                await ctx.Guild.CreateTextChannelAsync(i, x => x.CategoryId = chan.Id).ConfigureAwait(false);

            var eb2 = new EmbedBuilder();
            eb2.WithDescription(Strings.AddedTextChannels(ctx.Guild.Id, channels.Length.ToString(), chan.Name));
            eb2.WithOkColor();
            await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Unlocks a specific channel, allowing everyone to send messages again.
        /// </summary>
        /// <param name="channel">The channel to be unlocked. If null, unlocks the current channel.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        [BotPerm(GuildPermission.ManageMessages)]
        public async Task Unlock(SocketTextChannel? channel = null)
        {
            if (channel == null)
            {
                var tch = ctx.Channel as SocketTextChannel;
                var currentPerms = tch.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ??
                                   new OverwritePermissions();
                await tch.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                    currentPerms.Modify(sendMessages: PermValue.Inherit)).ConfigureAwait(false);
                await ctx.Channel
                    .SendMessageAsync(Strings.ChannelUnlocked(ctx.Guild.Id, config.Data.SuccessEmote, tch.Mention))
                    .ConfigureAwait(false);
            }
            else
            {
                var currentPerms = channel.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ??
                                   new OverwritePermissions();
                await channel.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                    currentPerms.Modify(sendMessages: PermValue.Inherit)).ConfigureAwait(false);
                await ctx.Channel
                    .SendMessageAsync(Strings.ChannelUnlocked(ctx.Guild.Id, config.Data.SuccessEmote, channel.Mention))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Applies or removes slowmode settings in a channel.
        /// </summary>
        /// <param name="time">The duration for slowmode. Use 0 or omit to toggle or remove slowmode.</param>
        /// <param name="channel">The channel to apply slowmode to. If omitted, applies to the current channel.</param>
        /// <remarks>
        ///     This command allows for detailed control over a channel's slowmode settings, including enabling, disabling, or
        ///     adjusting the duration.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public Task Slowmode(StoopidTime time, ITextChannel channel)
        {
            return InternalSlowmode(channel, (int)time.Time.TotalSeconds);
        }

        /// <summary>
        ///     Sets the slowmode interval for the current text channel.
        /// </summary>
        /// <param name="time">The duration for slowmode, specified in various time formats (e.g., "1m", "30s").</param>
        /// <remarks>
        ///     This command sets a specific slowmode interval for the channel from which the command is invoked.
        ///     It requires the user to have the "Manage Channels" permission.
        ///     The slowmode interval specifies how long each user must wait before sending another message in the channel.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public Task Slowmode(StoopidTime time)
        {
            return InternalSlowmode(ctx.Channel as ITextChannel, (int)time.Time.TotalSeconds);
        }

        /// <summary>
        ///     Sets or removes the slowmode interval for a specified text channel.
        /// </summary>
        /// <param name="channel">The text channel to apply the slowmode settings to.</param>
        /// <remarks>
        ///     This variant of the slowmode command allows specifying a particular text channel by mentioning it or using its ID.
        ///     If the slowmode interval is not specified, this command will remove the slowmode setting from the channel.
        ///     It requires the user to have the "Manage Channels" permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public Task Slowmode(ITextChannel channel)
        {
            return InternalSlowmode(channel);
        }

        /// <summary>
        ///     Toggles the slowmode interval for the current text channel between off and a default interval.
        /// </summary>
        /// <remarks>
        ///     If the current text channel has slowmode disabled, this command will enable it with a default interval.
        ///     If slowmode is already enabled, it will be disabled.
        ///     This command provides a quick way to toggle slowmode on and off without specifying a duration.
        ///     It requires the user to have the "Manage Channels" permission.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public Task Slowmode()
        {
            return InternalSlowmode((ITextChannel)ctx.Channel);
        }

        private async Task InternalSlowmode(ITextChannel channel, int time = 0)
        {
            switch (time)
            {
                case 0:
                    switch (channel.SlowModeInterval)
                    {
                        case 0:
                            await channel.ModifyAsync(x => x.SlowModeInterval = 60).ConfigureAwait(false);
                            await channel
                                .SendConfirmAsync(Strings.SlowmodeEnabledOneMinute(ctx.Guild.Id, channel.Mention))
                                .ConfigureAwait(false);
                            return;
                        case > 0:
                            await channel.ModifyAsync(x => x.SlowModeInterval = 0).ConfigureAwait(false);
                            await channel.SendConfirmAsync(Strings.SlowmodeDisabled(ctx.Guild.Id, channel.Mention))
                                .ConfigureAwait(false);
                            break;
                    }

                    return;
                case >= 21600:
                    await channel.SendErrorAsync(
                            Strings.SlowmodeMax(ctx.Guild.Id),
                            Config)
                        .ConfigureAwait(false);
                    break;
                default:
                    await channel.ModifyAsync(x => x.SlowModeInterval = time).ConfigureAwait(false);
                    await channel.SendConfirmAsync(
                            $"Slowmode enabled in {channel.Mention} for {TimeSpan.FromSeconds(time).Humanize(maxUnit: TimeUnit.Hour)}")
                        .ConfigureAwait(false);
                    break;
            }
        }

        /// <summary>
        ///     Creates a webhook in a text channel with an optional custom avatar.
        /// </summary>
        /// <remarks>
        ///     The webhook name and avatar can be customized. If the avatar is omitted, the default avatar is used.
        ///     If an image is attached to the command message, it will be used as the webhook's avatar.
        /// </remarks>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task CreateWebhook(ITextChannel channel, string name, string avatar = null)
        {
            if (ctx.Message.Attachments.Any())
            {
                if (avatar == null)
                {
                    if (ctx.Message.Attachments.First().Url.IsImage())
                    {
                        avatar = ctx.Message.Attachments.First().Url;
                    }
                }
            }

            if (avatar != null)
            {
                using var sr = await http.GetAsync(avatar, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var imgStream = imgData.ToStream();
                var wh = await channel.CreateWebhookAsync(name, imgStream).ConfigureAwait(false);
                await ctx.Channel
                    .SendMessageAsync(
                        Strings.WebhookCreated(ctx.Guild.Id, config.Data.SuccessEmote, wh.Name, channel.Mention))
                    .ConfigureAwait(false);
                await ctx.User
                    .SendErrorAsync(
                        Strings.WebhookCreatedDontShare(ctx.Guild.Id, wh.Id, wh.Token))
                    .ConfigureAwait(false);
                sr.Dispose();
                await imgStream.DisposeAsync();
            }
            else
            {
                var wh = await channel.CreateWebhookAsync(name).ConfigureAwait(false);
                await ctx.Channel
                    .SendMessageAsync(Strings.WebhookCreatedSimple(ctx.Guild.Id, config.Data.SuccessEmote, wh.Name,
                        channel.Mention))
                    .ConfigureAwait(false);
                await ctx.User
                    .SendErrorAsync(
                        Strings.WebhookCreatedDontShare(ctx.Guild.Id, wh.Id, wh.Token))
                    .ConfigureAwait(false);
            }
        }
    }
}