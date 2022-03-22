using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Confessions.Services;

namespace Mewdeko.Modules.Confessions;

public class Confessions : MewdekoModuleBase<ConfessionService>
{
    private readonly Mewdeko _bot;

    public Confessions(Mewdeko bot) 
        => _bot = bot;

    [MewdekoCommand, Aliases, RequireContext(ContextType.DM), BlacklistCheck]
    public async Task Confess(ulong serverId, string? confession = null)
    {
        var gc = _bot.GetGuildConfig(serverId);
        var attachment = ctx.Message.Attachments.FirstOrDefault().Url ?? null;
        var user = ctx.User as SocketUser;
        if (user!.MutualGuilds.Select(x => x.Id).Contains(serverId))
        {
            if (gc.ConfessionChannel is 0)
            {
                await ctx.Channel.SendErrorAsync("This server does not have confessions enabled!");
                return;
            }
            if (gc.ConfessionBlacklist.Split(" ").Any())
            {
                if (gc.ConfessionBlacklist.Split(" ").Contains(ctx.User.Id.ToString()))
                {
                    await ctx.Channel.SendErrorAsync("You are blacklisted from suggestions in that server!");
                    return;
                }
                await Service.SendConfession(serverId, ctx.User, confession, ctx.Channel, null, attachment);
            }
            else
            {
                await Service.SendConfession(serverId, ctx.User, confession, ctx.Channel, null, attachment);
            }
        }
        else
        {
            await ctx.Channel.SendErrorAsync("You aren't in any servers that have my confessions enabled!");
        }
    }

    [MewdekoCommand, Aliases, UserPerm(GuildPermission.ManageChannels), RequireContext(ContextType.Guild)]
    public async Task ConfessionChannel(ITextChannel? channel = null)
    {
        if (channel is null)
        {
            await Service.SetConfessionChannel(ctx.Guild, 0);
            await ctx.Channel.SendConfirmAsync("Confessions disabled!");
            return;
        }
        var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
        var perms = currentUser.GetPermissions(channel);
        if (!perms.SendMessages || !perms.EmbedLinks)
        {
            await ctx.Channel.SendErrorAsync(
                "I don't have proper perms there! Please make sure to enable EmbedLinks and SendMessages in that channel for me!");
        }

        await Service.SetConfessionChannel(ctx.Guild, channel.Id);
        await ctx.Channel.SendConfirmAsync($"Set {channel.Mention} as the Confession Channel!");
    }

    [MewdekoCommand, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task ConfessionLogChannel(ITextChannel? channel = null)
    {
        if (channel is null)
        {
            await Service.SetConfessionChannel(ctx.Guild, 0);
            await ctx.Channel.SendConfirmAsync("Confessions logging disabled!");
            return;
        }
        var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
        var perms = currentUser.GetPermissions(channel);
        if (!perms.SendMessages || !perms.EmbedLinks)
        {
            await ctx.Channel.SendErrorAsync(
                "I don't have proper perms there! Please make sure to enable EmbedLinks and SendMessages in that channel for me!");
        }

        await Service.SetConfessionLogChannel(ctx.Guild, channel.Id);
        await ctx.Channel.SendErrorAsync($"Set {channel.Mention} as the Confession Log Channel. \n***Keep in mind if I find you misusing this function I will find out, blacklist this server. And tear out whatever reproductive organs you have.***");
    }

    [MewdekoCommand, Aliases, UserPerm(GuildPermission.ManageChannels), RequireContext(ContextType.Guild)]
    public async Task ConfessionBlacklist(IUser user)
    {
        var blacklists = _bot.GetGuildConfig(ctx.Guild.Id).ConfessionBlacklist.Split(" ");
        if (blacklists.Any())
        {
            if (blacklists.Contains(user.Id.ToString()))
            {
                await ctx.Channel.SendErrorAsync("This user is already blacklisted!");
                return;
            }

            await Service.ToggleUserBlacklistAsync(ctx.Guild.Id, user.Id);
            await ctx.Channel.SendConfirmAsync($"Added {user.Mention} to the confession blacklist!!");
        }
    }
    
    [MewdekoCommand, Aliases, UserPerm(GuildPermission.ManageChannels), RequireContext(ContextType.Guild)]
    public async Task ConfessionUnblacklist(IUser user)
    {
        var blacklists = _bot.GetGuildConfig(ctx.Guild.Id).ConfessionBlacklist.Split(" ");
        if (blacklists.Any())
        {
            if (!blacklists.Contains(user.Id.ToString()))
            {
                await ctx.Channel.SendErrorAsync("This user is not blacklisted!");
                return;
            }

            await Service.ToggleUserBlacklistAsync(ctx.Guild.Id, user.Id);
            await ctx.Channel.SendConfirmAsync($"Removed {user.Mention} from the confession blacklist!!");
        }
    }
}