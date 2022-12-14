using System.Threading.Tasks;
using Discord.Commands;
using Humanizer;
using Humanizer.Localisation;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Server_Management.Services;
using Mewdeko.Services.Settings;
using PermValue = Discord.PermValue;

namespace Mewdeko.Modules.Server_Management;

public partial class ServerManagement
{
    [Group]
    public class ChannelCommands : MewdekoSubmodule<ServerManagementService>
    {
        private readonly BotConfigService config;

        public ChannelCommands(BotConfigService config) => this.config = config;

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task LockCheck()
        {
            var msg = await ctx.Channel.SendMessageAsync(
                $"{config.Data.LoadingEmote} Making sure role permissions don't get in the way of lockdown...").ConfigureAwait(false);
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
                    $"{config.Data.SuccessEmote} Roles checked! You may now run the lockdown command.").ConfigureAwait(false);
            }
            else
            {
                await msg.ModifyAsync(x => x.Content =
                    $"{config.Data.SuccessEmote} Roles checked! No roles are in the way of the lockdown command.").ConfigureAwait(false);
            }
        }

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
                    $"{config.Data.ErrorEmote} Please run the Lockcheck command as you have roles that will get in the way of lockdown").ConfigureAwait(false);
                return;
            }

            if (!ctx.Guild.EveryoneRole.Permissions.SendMessages)
            {
                await ctx.Channel.SendErrorAsync(
                    $"{config.Data.ErrorEmote} Server is already in lockdown!").ConfigureAwait(false);
            }
            else
            {
                var everyonerole = ctx.Guild.EveryoneRole;
                var newperms = everyonerole.Permissions.Modify(sendMessages: false);
                await everyonerole.ModifyAsync(x => x.Permissions = newperms).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync("Server has been locked down!").ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task MoveTo(IVoiceChannel channel)
        {
            var use = ctx.User as IGuildUser;
            if (use.VoiceChannel == null)
            {
                await ctx.Channel.SendErrorAsync(
                    $"{config.Data.SuccessEmote} You need to be in a voice channel for this!").ConfigureAwait(false);
                return;
            }

            await use.ModifyAsync(x => x.Channel = new Optional<IVoiceChannel>(channel)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Succesfully moved you to {Format.Bold(channel.Name)}").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels)]
        public async Task MoveUserTo(IGuildUser user, IVoiceChannel channel)
        {
            if (user.VoiceChannel == null)
            {
                await ctx.Channel.SendErrorAsync("The user must be in a voice channel for this!").ConfigureAwait(false);
                return;
            }

            await user.ModifyAsync(x => x.Channel = new Optional<IVoiceChannel>(channel)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Succesfully moved {user.Mention} to {Format.Bold(channel.Name)}").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Grab(IGuildUser user)
        {
            var vc = ((IGuildUser)ctx.User).VoiceChannel;
            if (vc == null)
            {
                await ctx.Channel.SendErrorAsync("You need to be in a voice channel to use this!").ConfigureAwait(false);
                return;
            }

            if (user.VoiceChannel == null)
            {
                await ctx.Channel.SendErrorAsync(
                    $"{user.Mention} needs to be in a voice channel for this to work!").ConfigureAwait(false);
                return;
            }

            await user.ModifyAsync(x => x.Channel = new Optional<IVoiceChannel>(vc)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Grabbed {user.Mention} from {user.VoiceChannel.Name} to your VC!").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels)]
        public async Task Unlockdown()
        {
            if (ctx.Guild.EveryoneRole.Permissions.SendMessages)
            {
                await ctx.Channel.SendErrorAsync($"{config.Data.ErrorEmote} Server is not locked down!").ConfigureAwait(false);
                return;
            }

            var everyonerole = ctx.Guild.EveryoneRole;
            var newperms = everyonerole.Permissions.Modify(sendMessages: true);
            await everyonerole.ModifyAsync(x => x.Permissions = newperms).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync("Server has been unlocked!").ConfigureAwait(false);
        }

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
                    "https://pa1.narvii.com/6463/6494fab512c8f2ac0d652c44dae78be4cb644569_hq.gif").ConfigureAwait(false);
            }
        }

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
                await ctx.Channel.SendMessageAsync($"{config.Data.SuccessEmote} Locked down {tch.Mention}").ConfigureAwait(false);
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

        [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
        public async Task CreateCatAndTxtChannels(string catName, params string[] channels)
        {
            var eb = new EmbedBuilder();
            eb.WithOkColor();
            eb.WithDescription(
                $"{config.Data.LoadingEmote} Creating the Category {catName} with {channels.Length} Text Channels!");
            var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            var cat = await ctx.Guild.CreateCategoryAsync(catName).ConfigureAwait(false);
            foreach (var i in channels) await ctx.Guild.CreateTextChannelAsync(i, x => x.CategoryId = cat.Id).ConfigureAwait(false);

            var eb2 = new EmbedBuilder();
            eb2.WithDescription($"{config.Data.SuccessEmote} Created the category {catName} with {channels.Length} Text Channels!");
            eb2.WithOkColor();
            await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
        public async Task CreateCatAndVcChannels(string catName, params string[] channels)
        {
            var eb = new EmbedBuilder();
            eb.WithOkColor();
            eb.WithDescription(
                $"{config.Data.LoadingEmote} Creating the Category {catName} with {channels.Length} Voice Channels");
            var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            var cat = await ctx.Guild.CreateCategoryAsync(catName).ConfigureAwait(false);
            foreach (var i in channels) await ctx.Guild.CreateVoiceChannelAsync(i, x => x.CategoryId = cat.Id).ConfigureAwait(false);

            var eb2 = new EmbedBuilder();
            eb2.WithDescription($"Created the category {catName} with {channels.Length} Voice Channels!");
            eb2.WithOkColor();
            await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
        }

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

        [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
        public async Task CreateCatTxtChans(ICategoryChannel chan, params string[] channels)
        {
            var eb = new EmbedBuilder();
            eb.WithOkColor();
            eb.WithDescription(
                $"{config.Data.LoadingEmote} Adding {channels.Length} Text Channels to {chan.Name}");
            var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            foreach (var i in channels) await ctx.Guild.CreateTextChannelAsync(i, x => x.CategoryId = chan.Id).ConfigureAwait(false);

            var eb2 = new EmbedBuilder();
            eb2.WithDescription($"Added {channels.Length} Text Channels to {chan.Name}!");
            eb2.WithOkColor();
            await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
        }

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
                await ctx.Channel.SendMessageAsync($"{config.Data.SuccessEmote} Unlocked {tch.Mention}").ConfigureAwait(false);
            }
            else
            {
                var currentPerms = channel.GetPermissionOverwrite(ctx.Guild.EveryoneRole) ??
                                   new OverwritePermissions();
                await channel.AddPermissionOverwriteAsync(ctx.Guild.EveryoneRole,
                    currentPerms.Modify(sendMessages: PermValue.Inherit)).ConfigureAwait(false);
                await ctx.Channel.SendMessageAsync($"{config.Data.SuccessEmote} Unlocked {channel.Mention}").ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels), Priority(0)]
        public static async Task Slowmode(StoopidTime time, ITextChannel channel) => await InternalSlowmode(channel, (int)time.Time.TotalSeconds).ConfigureAwait(false);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels), Priority(1)]
        public async Task Slowmode(StoopidTime time) => await InternalSlowmode(ctx.Channel as ITextChannel, (int)time.Time.TotalSeconds).ConfigureAwait(false);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels), Priority(2)]
        public static async Task Slowmode(ITextChannel channel) => await InternalSlowmode(channel).ConfigureAwait(false);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels), Priority(4)]
        public async Task Slowmode() => await InternalSlowmode((ITextChannel)ctx.Channel).ConfigureAwait(false);

        private static async Task InternalSlowmode(ITextChannel channel, int time = 0)
        {
            switch (time)
            {
                case 0:
                    switch (channel.SlowModeInterval)
                    {
                        case 0:
                            await channel.ModifyAsync(x => x.SlowModeInterval = 60).ConfigureAwait(false);
                            await channel.SendConfirmAsync($"Slowmode enabled in {channel.Mention} for 1 Minute.").ConfigureAwait(false);
                            return;
                        case > 0:
                            await channel.ModifyAsync(x => x.SlowModeInterval = 0).ConfigureAwait(false);
                            await channel.SendConfirmAsync($"Slowmode disabled in {channel.Mention}.").ConfigureAwait(false);
                            break;
                    }

                    return;
                case >= 21600:
                    await channel.SendErrorAsync(
                        "The max discord allows for slowmode is 6 hours! Please try again with a lower value.").ConfigureAwait(false);
                    break;
                default:
                    await channel.ModifyAsync(x => x.SlowModeInterval = time).ConfigureAwait(false);
                    await channel.SendConfirmAsync(
                        $"Slowmode enabled in {channel.Mention} for {TimeSpan.FromSeconds(time).Humanize(maxUnit: TimeUnit.Hour)}").ConfigureAwait(false);
                    break;
            }
        }
    }
}