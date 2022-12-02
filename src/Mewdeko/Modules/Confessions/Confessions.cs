using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Confessions.Services;

namespace Mewdeko.Modules.Confessions;

public class Confessions : MewdekoModuleBase<ConfessionService>
{
    private readonly GuildSettingsService guildSettings;

    public Confessions(GuildSettingsService guildSettings) => this.guildSettings = guildSettings;

    [Cmd, Aliases, RequireContext(ContextType.DM)]
    public async Task Confess(ulong serverId, string? confession = null)
    {
        var gc = await guildSettings.GetGuildConfig(serverId);
        var attachment = ctx.Message.Attachments.FirstOrDefault().Url;
        var user = ctx.User as SocketUser;
        if (user!.MutualGuilds.Select(x => x.Id).Contains(serverId))
        {
            if (gc.ConfessionChannel is 0)
            {
                await ctx.Channel.SendErrorAsync("This server does not have confessions enabled!").ConfigureAwait(false);
                return;
            }

            if (gc.ConfessionBlacklist.Split(" ").Length > 0)
            {
                if (gc.ConfessionBlacklist.Split(" ").Contains(ctx.User.Id.ToString()))
                {
                    await ctx.Channel.SendErrorAsync("You are blacklisted from suggestions in that server!").ConfigureAwait(false);
                    return;
                }

                await Service.SendConfession(serverId, ctx.User, confession, ctx.Channel, null, attachment).ConfigureAwait(false);
            }
            else
            {
                await Service.SendConfession(serverId, ctx.User, confession, ctx.Channel, null, attachment).ConfigureAwait(false);
            }
        }
        else
        {
            await ctx.Channel.SendErrorAsync("You aren't in any servers that have my confessions enabled!").ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels), RequireContext(ContextType.Guild)]
    public async Task ConfessionChannel(ITextChannel? channel = null)
    {
        if (channel is null)
        {
            await Service.SetConfessionChannel(ctx.Guild, 0).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync("Confessions disabled!").ConfigureAwait(false);
            return;
        }

        var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        var perms = currentUser.GetPermissions(channel);
        if (!perms.SendMessages || !perms.EmbedLinks)
        {
            await ctx.Channel.SendErrorAsync(
                "I don't have proper perms there! Please make sure to enable EmbedLinks and SendMessages in that channel for me!").ConfigureAwait(false);
        }

        await Service.SetConfessionChannel(ctx.Guild, channel.Id).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync($"Set {channel.Mention} as the Confession Channel!").ConfigureAwait(false);
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task ConfessionLogChannel(ITextChannel? channel = null)
    {
        if (channel is null)
        {
            await Service.SetConfessionChannel(ctx.Guild, 0).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync("Confessions logging disabled!").ConfigureAwait(false);
            return;
        }

        var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        var perms = currentUser.GetPermissions(channel);
        if (!perms.SendMessages || !perms.EmbedLinks)
        {
            await ctx.Channel.SendErrorAsync(
                "I don't have proper perms there! Please make sure to enable EmbedLinks and SendMessages in that channel for me!").ConfigureAwait(false);
        }

        await Service.SetConfessionLogChannel(ctx.Guild, channel.Id).ConfigureAwait(false);
        await ctx.Channel.SendErrorAsync(
                $"Set {channel.Mention} as the Confession Log Channel. \n***Keep in mind if I find you misusing this function I will find out, blacklist this server. And tear out whatever reproductive organs you have.***")
            .ConfigureAwait(false);
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels), RequireContext(ContextType.Guild)]
    public async Task ConfessionBlacklist(IUser user)
    {
        var blacklists = (await guildSettings.GetGuildConfig(ctx.Guild.Id)).ConfessionBlacklist.Split(" ");
        if (blacklists.Length > 0)
        {
            if (blacklists.Contains(user.Id.ToString()))
            {
                await ctx.Channel.SendErrorAsync("This user is already blacklisted!").ConfigureAwait(false);
                return;
            }

            await Service.ToggleUserBlacklistAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Added {user.Mention} to the confession blacklist!!").ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels), RequireContext(ContextType.Guild)]
    public async Task ConfessionUnblacklist(IUser user)
    {
        var blacklists = (await guildSettings.GetGuildConfig(ctx.Guild.Id)).ConfessionBlacklist.Split(" ");
        if (blacklists.Length > 0)
        {
            if (!blacklists.Contains(user.Id.ToString()))
            {
                await ctx.Channel.SendErrorAsync("This user is not blacklisted!").ConfigureAwait(false);
                return;
            }

            await Service.ToggleUserBlacklistAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Removed {user.Mention} from the confession blacklist!!").ConfigureAwait(false);
        }
    }
}