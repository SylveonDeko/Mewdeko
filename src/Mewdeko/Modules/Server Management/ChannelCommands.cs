using System.Net.Http;
using Discord.Commands;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Services.Settings;
using PermValue = Discord.PermValue;

namespace Mewdeko.Modules.Server_Management;

public partial class ServerManagement
{
    /// <summary>
    /// Manages channel-specific operations such as locking, unlocking, and modifying slowmode settings.
    /// </summary>
    [Group]
    public class ChannelCommands(BotConfigService config, HttpClient http) : MewdekoSubmodule
    {
        /// <summary>
        /// Checks roles for SendMessages permission and modifies them as necessary before a lockdown.
        /// </summary>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task LockCheck()
        {
            var msg = await ctx.Channel.SendMessageAsync(
                    $"{config.Data.LoadingEmote} Making sure role permissions don't get in the way of lockdown...")
                .ConfigureAwait(false);
            var roles = Context.Guild.Roles.ToList().FindAll(x =>
                x.Id != Context.Guild.Id && x.Permissions.SendMessages && x.Position <
                ((SocketGuild)ctx.Guild).CurrentUser.GetRoles().Max(r => r.Position));
            if (roles.Count > 0)
            {
                foreach (var i in roles)
                {
                    var perms = i.Permissions;
                    var newperms = perms.Modify(sendMessages: false);
                    await i.ModifyAsync(x => x.Permissions = newperms).ConfigureAwait(false);
                }

                await msg.ModifyAsync(x => x.Content =
                        $"{config.Data.SuccessEmote} Roles checked! You may now run the lockdown command.")
                    .ConfigureAwait(false);
            }
            else
            {
                await msg.ModifyAsync(x => x.Content =
                        $"{config.Data.SuccessEmote} Roles checked! No roles are in the way of the lockdown command.")
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Locks down the server by setting SendMessages permission for @everyone to false.
        /// </summary>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels)]
        public async Task LockDown()
        {
            var roles = Context.Guild.Roles.ToList().FindAll(x =>
                x.Id != Context.Guild.Id && x.Permissions.SendMessages && x.Position <
                ((SocketGuild)ctx.Guild).CurrentUser.GetRoles().Max(r => r.Position));
            if (roles.Count > 0)
            {
                await ctx.Channel.SendErrorAsync(
                        $"{config.Data.ErrorEmote} Please run the Lockcheck command as you have roles that will get in the way of lockdown",
                        Config)
                    .ConfigureAwait(false);
                return;
            }

            if (!ctx.Guild.EveryoneRole.Permissions.SendMessages)
            {
                await ctx.Channel.SendErrorAsync(
                    $"{config.Data.ErrorEmote} Server is already in lockdown!", Config).ConfigureAwait(false);
            }
            else
            {
                var everyonerole = ctx.Guild.EveryoneRole;
                var newperms = everyonerole.Permissions.Modify(sendMessages: false);
                await everyonerole.ModifyAsync(x => x.Permissions = newperms).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync("Server has been locked down!").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Moves the command issuer to a specified voice channel.
        /// </summary>
        /// <param name="channel">The target voice channel to move the user to.</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task MoveTo(IVoiceChannel channel)
        {
            var use = ctx.User as IGuildUser;
            if (use.VoiceChannel == null)
            {
                await ctx.Channel.SendErrorAsync(
                        $"{config.Data.SuccessEmote} You need to be in a voice channel for this!", Config)
                    .ConfigureAwait(false);
                return;
            }

            await use.ModifyAsync(x => x.Channel = new Optional<IVoiceChannel>(channel)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Succesfully moved you to {Format.Bold(channel.Name)}")
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Moves a specified user to a given voice channel.
        /// </summary>
        /// <param name="user">The user to be moved.</param>
        /// <param name="channel">The target voice channel to move the user to.</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels)]
        public async Task MoveUserTo(IGuildUser user, IVoiceChannel channel)
        {
            if (user.VoiceChannel == null)
            {
                await ctx.Channel.SendErrorAsync("The user must be in a voice channel for this!", Config)
                    .ConfigureAwait(false);
                return;
            }

            await user.ModifyAsync(x => x.Channel = new Optional<IVoiceChannel>(channel)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Succesfully moved {user.Mention} to {Format.Bold(channel.Name)}")
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Brings a user to the command issuer's current voice channel.
        /// </summary>
        /// <param name="user">The user to grab and move.</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Grab(IGuildUser user)
        {
            var vc = ((IGuildUser)ctx.User).VoiceChannel;
            if (vc == null)
            {
                await ctx.Channel.SendErrorAsync("You need to be in a voice channel to use this!", Config)
                    .ConfigureAwait(false);
                return;
            }

            if (user.VoiceChannel == null)
            {
                await ctx.Channel.SendErrorAsync(
                    $"{user.Mention} needs to be in a voice channel for this to work!", Config).ConfigureAwait(false);
                return;
            }

            await user.ModifyAsync(x => x.Channel = new Optional<IVoiceChannel>(vc)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Grabbed {user.Mention} from {user.VoiceChannel.Name} to your VC!")
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Unlocks the server by allowing @everyone to send messages again.
        /// </summary>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels)]
        public async Task Unlockdown()
        {
            if (ctx.Guild.EveryoneRole.Permissions.SendMessages)
            {
                await ctx.Channel.SendErrorAsync($"{config.Data.ErrorEmote} Server is not locked down!", Config)
                    .ConfigureAwait(false);
                return;
            }

            var everyonerole = ctx.Guild.EveryoneRole;
            var newperms = everyonerole.Permissions.Modify(sendMessages: true);
            await everyonerole.ModifyAsync(x => x.Permissions = newperms).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync("Server has been unlocked!").ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes and recreates a text channel, effectively "nuking" it.
        /// </summary>
        /// <param name="chan3">Optional parameter to specify a channel to nuke.</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels)]
        public async Task Nuke(ITextChannel? chan3 = null)
        {
            var embed = new EmbedBuilder
            {
                Color = Mewdeko.ErrorColor,
                Description =
                    "Are you sure you want to nuke this channel? This will delete the entire channel and remake it."
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
        /// Enables or disables slowmode in a channel, with customizable duration.
        /// </summary>
        /// <param name="channel">Optional parameter to specify a channel to apply slowmode.</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageMessages), BotPerm(GuildPermission.ManageMessages)]
        public async Task Lock(SocketTextChannel? channel = null)
        {
            if (channel == null)
            {
                var tch = ctx.Channel as SocketTextChannel;
                var currentPerms = tch.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ??
                                   new OverwritePermissions();
                await tch.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                    currentPerms.Modify(sendMessages: PermValue.Deny)).ConfigureAwait(false);
                await ctx.Channel.SendMessageAsync($"{config.Data.SuccessEmote} Locked down {tch.Mention}")
                    .ConfigureAwait(false);
            }
            else
            {
                var currentPerms = channel.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ??
                                   new OverwritePermissions();
                await channel.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                    currentPerms.Modify(sendMessages: PermValue.Deny)).ConfigureAwait(false);
                await ctx.Channel.SendMessageAsync(
                    $"{config.Data.SuccessEmote} Locked down {channel.Mention}").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Creates a category and the specified channels in a server.
        /// </summary>
        /// <param name="catName">The name of the category to create</param>
        /// <param name="channels">The names of the channels to create</param>
        [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
        public async Task CreateCatAndTxtChannels(string catName, params string[] channels)
        {
            var eb = new EmbedBuilder();
            eb.WithOkColor();
            eb.WithDescription(
                $"{config.Data.LoadingEmote} Creating the Category {catName} with {channels.Length} Text Channels!");
            var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            var cat = await ctx.Guild.CreateCategoryAsync(catName).ConfigureAwait(false);
            foreach (var i in channels)
                await ctx.Guild.CreateTextChannelAsync(i, x => x.CategoryId = cat.Id).ConfigureAwait(false);

            var eb2 = new EmbedBuilder();
            eb2.WithDescription(
                $"{config.Data.SuccessEmote} Created the category {catName} with {channels.Length} Text Channels!");
            eb2.WithOkColor();
            await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a category with multiple voice channels in it.
        /// </summary>
        /// <param name="catName">The name of the category to be created.</param>
        /// <param name="channels">The names of the voice channels to be created within the category.</param>
        [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
        public async Task CreateCatAndVcChannels(string catName, params string[] channels)
        {
            var eb = new EmbedBuilder();
            eb.WithOkColor();
            eb.WithDescription(
                $"{config.Data.LoadingEmote} Creating the Category {catName} with {channels.Length} Voice Channels");
            var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            var cat = await ctx.Guild.CreateCategoryAsync(catName).ConfigureAwait(false);
            foreach (var i in channels)
                await ctx.Guild.CreateVoiceChannelAsync(i, x => x.CategoryId = cat.Id).ConfigureAwait(false);

            var eb2 = new EmbedBuilder();
            eb2.WithDescription($"Created the category {catName} with {channels.Length} Voice Channels!");
            eb2.WithOkColor();
            await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds multiple voice channels to an existing category.
        /// </summary>
        /// <param name="chan">The target category channel.</param>
        /// <param name="channels">The names of the voice channels to be added.</param>
        [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
        public async Task CreateCatVcChans(ICategoryChannel chan, params string[] channels)
        {
            var eb = new EmbedBuilder();
            eb.WithOkColor();
            eb.WithDescription(
                $"{config.Data.LoadingEmote} Adding {channels.Length} Voice Channels to {chan.Name}");
            var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            foreach (var i in channels)
                await ctx.Guild.CreateVoiceChannelAsync(i, x => x.CategoryId = chan.Id).ConfigureAwait(false);

            var eb2 = new EmbedBuilder();
            eb2.WithDescription($"Added {channels.Length} Voice Channels to {chan.Name}!");
            eb2.WithOkColor();
            await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds multiple text channels to an existing category.
        /// </summary>
        /// <param name="chan">The target category channel.</param>
        /// <param name="channels">The names of the text channels to be added.</param>
        [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
        public async Task CreateCatTxtChans(ICategoryChannel chan, params string[] channels)
        {
            var eb = new EmbedBuilder();
            eb.WithOkColor();
            eb.WithDescription(
                $"{config.Data.LoadingEmote} Adding {channels.Length} Text Channels to {chan.Name}");
            var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            foreach (var i in channels)
                await ctx.Guild.CreateTextChannelAsync(i, x => x.CategoryId = chan.Id).ConfigureAwait(false);

            var eb2 = new EmbedBuilder();
            eb2.WithDescription($"Added {channels.Length} Text Channels to {chan.Name}!");
            eb2.WithOkColor();
            await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
        }

        /// <summary>
        /// Unlocks a specific channel, allowing everyone to send messages again.
        /// </summary>
        /// <param name="channel">The channel to be unlocked. If null, unlocks the current channel.</param>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageMessages), BotPerm(GuildPermission.ManageMessages)]
        public async Task Unlock(SocketTextChannel? channel = null)
        {
            if (channel == null)
            {
                var tch = ctx.Channel as SocketTextChannel;
                var currentPerms = tch.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ??
                                   new OverwritePermissions();
                await tch.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                    currentPerms.Modify(sendMessages: PermValue.Inherit)).ConfigureAwait(false);
                await ctx.Channel.SendMessageAsync($"{config.Data.SuccessEmote} Unlocked {tch.Mention}")
                    .ConfigureAwait(false);
            }
            else
            {
                var currentPerms = channel.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ??
                                   new OverwritePermissions();
                await channel.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                    currentPerms.Modify(sendMessages: PermValue.Inherit)).ConfigureAwait(false);
                await ctx.Channel.SendMessageAsync($"{config.Data.SuccessEmote} Unlocked {channel.Mention}")
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Applies or removes slowmode settings in a channel.
        /// </summary>
        /// <param name="time">The duration for slowmode. Use 0 or omit to toggle or remove slowmode.</param>
        /// <param name="channel">The channel to apply slowmode to. If omitted, applies to the current channel.</param>
        /// <remarks>
        /// This command allows for detailed control over a channel's slowmode settings, including enabling, disabling, or adjusting the duration.
        /// </remarks>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels)]
        public Task Slowmode(StoopidTime time, ITextChannel channel) =>
            InternalSlowmode(channel, (int)time.Time.TotalSeconds);

        /// <summary>
        /// Sets the slowmode interval for the current text channel.
        /// </summary>
        /// <param name="time">The duration for slowmode, specified in various time formats (e.g., "1m", "30s").</param>
        /// <remarks>
        /// This command sets a specific slowmode interval for the channel from which the command is invoked.
        /// It requires the user to have the "Manage Channels" permission.
        /// The slowmode interval specifies how long each user must wait before sending another message in the channel.
        /// </remarks>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageChannels)]
        public Task Slowmode(StoopidTime time) =>
            InternalSlowmode(ctx.Channel as ITextChannel, (int)time.Time.TotalSeconds);

        /// <summary>
        /// Sets or removes the slowmode interval for a specified text channel.
        /// </summary>
        /// <param name="channel">The text channel to apply the slowmode settings to.</param>
        /// <remarks>
        /// This variant of the slowmode command allows specifying a particular text channel by mentioning it or using its ID.
        /// If the slowmode interval is not specified, this command will remove the slowmode setting from the channel.
        /// It requires the user to have the "Manage Channels" permission.
        /// </remarks>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageChannels)]
        public Task Slowmode(ITextChannel channel) =>
            InternalSlowmode(channel);

        /// <summary>
        /// Toggles the slowmode interval for the current text channel between off and a default interval.
        /// </summary>
        /// <remarks>
        /// If the current text channel has slowmode disabled, this command will enable it with a default interval.
        /// If slowmode is already enabled, it will be disabled.
        /// This command provides a quick way to toggle slowmode on and off without specifying a duration.
        /// It requires the user to have the "Manage Channels" permission.
        /// </remarks>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageChannels)]
        public Task Slowmode() => InternalSlowmode((ITextChannel)ctx.Channel);

        private async Task InternalSlowmode(ITextChannel channel, int time = 0)
        {
            switch (time)
            {
                case 0:
                    switch (channel.SlowModeInterval)
                    {
                        case 0:
                            await channel.ModifyAsync(x => x.SlowModeInterval = 60).ConfigureAwait(false);
                            await channel.SendConfirmAsync($"Slowmode enabled in {channel.Mention} for 1 Minute.")
                                .ConfigureAwait(false);
                            return;
                        case > 0:
                            await channel.ModifyAsync(x => x.SlowModeInterval = 0).ConfigureAwait(false);
                            await channel.SendConfirmAsync($"Slowmode disabled in {channel.Mention}.")
                                .ConfigureAwait(false);
                            break;
                    }

                    return;
                case >= 21600:
                    await channel.SendErrorAsync(
                            "The max discord allows for slowmode is 6 hours! Please try again with a lower value.",
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
        /// Creates a webhook in a text channel with an optional custom avatar.
        /// </summary>
        /// <remarks>
        /// The webhook name and avatar can be customized. If the avatar is omitted, the default avatar is used.
        /// If an image is attached to the command message, it will be used as the webhook's avatar.
        /// </remarks>
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
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
                        $"{config.Data.SuccessEmote} Created webhook {wh.Name} in {channel.Mention}. The url will be dmed to you.")
                    .ConfigureAwait(false);
                await ctx.User
                    .SendErrorAsync(
                        $"***DO NOT SHARE THIS WITH ANYONE***\nUrl: https://discordapp.com/api/webhooks/{wh.Id}/{wh.Token}")
                    .ConfigureAwait(false);
                sr.Dispose();
                await imgStream.DisposeAsync();
            }
            else
            {
                var wh = await channel.CreateWebhookAsync(name).ConfigureAwait(false);
                await ctx.Channel
                    .SendMessageAsync($"{config.Data.SuccessEmote} Created webhook {wh.Name} in {channel.Mention}")
                    .ConfigureAwait(false);
                await ctx.User
                    .SendErrorAsync(
                        $"***DO NOT SHARE THIS WITH ANYONE***\nUrl: https://discordapp.com/api/webhooks/{wh.Id}/{wh.Token}")
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Deletes multiple channels at once, use careflly, and dont dpo this to a lower perm. Just plase dont. I am not responsible for your dumbnation.
        /// </summary>
        /// <param name="channels"></param>
        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator),
         BotPerm(ChannelPermission.ManageChannels)]
        public async Task DeleteChannels(params IGuildChannel[] channels)
        {

        }
    }
}