using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Confessions.Services;

namespace Mewdeko.Modules.Confessions;
[Group("confessions", "Manage confessions.")]
public class SlashConfessions : MewdekoSlashModuleBase<ConfessionService>
{
    private readonly Mewdeko _bot;

    public SlashConfessions(Mewdeko bot)
        => _bot = bot;

    [SlashCommand("confess", "Sends your confession to the confession channel.", true), RequireContext(ContextType.Guild), CheckPermissions, BlacklistCheck]
    public async Task Confess(string confession, IAttachment? attachment = null)
    {
        var blacklists = _bot.GetGuildConfig(ctx.Guild.Id).ConfessionBlacklist.Split(" ");
        var attachUrl = attachment?.Url;
        if (_bot.GetGuildConfig(ctx.Guild.Id).ConfessionChannel is 0)
        {
            await ctx.Interaction.SendEphemeralErrorAsync("This server does not have confessions enabled!");
            return;
        }
        if (blacklists.Any())
        {
            if (blacklists.Contains(ctx.User.Id.ToString()))
            {
                await ctx.Interaction.SendEphemeralErrorAsync("You are blacklisted from confessions here!!");
                return;
            }
            await Service.SendConfession(ctx.Guild.Id, ctx.User, confession, ctx.Channel, ctx, attachUrl);
        }
        else
        {
            await Service.SendConfession(ctx.Guild.Id, ctx.User, confession, ctx.Channel, ctx, attachUrl);
        }
    }

    [SlashCommand("channel", "Set the confession channel"), SlashUserPerm(GuildPermission.ManageChannels), RequireContext(ContextType.Guild), CheckPermissions, BlacklistCheck]
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
            await ctx.Interaction.SendErrorAsync(
                "I don't have proper perms there! Please make sure to enable EmbedLinks and SendMessages in that channel for me!");
        }

        await Service.SetConfessionChannel(ctx.Guild, channel.Id);
        await ctx.Interaction.SendConfirmAsync($"Set {channel.Mention} as the Confession Channel!");
    }

    [SlashCommand("logchannel", "Set the confession channel"), SlashUserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild), CheckPermissions, BlacklistCheck]
    public async Task ConfessionLogChannel(ITextChannel? channel = null)
    {
        if (channel is null)
        {
            await Service.SetConfessionLogChannel(ctx.Guild, 0);
            await ctx.Channel.SendConfirmAsync("Confessions logging disabled!");
            return;
        }
        var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
        var perms = currentUser.GetPermissions(channel);
        if (!perms.SendMessages || !perms.EmbedLinks)
        {
            await ctx.Interaction.SendErrorAsync(
                "I don't have proper perms there! Please make sure to enable EmbedLinks and SendMessages in that channel for me!");
        }

        await Service.SetConfessionLogChannel(ctx.Guild, channel.Id);
        await ctx.Interaction.SendErrorAsync($"Set {channel.Mention} as the Confession Log Channel. \n***Keep in mind if I find you misusing this function I will find out, blacklist this server. And tear out whatever reproductive organs you have.***");
    }

    [SlashCommand("blacklist", "Add a user to the confession blacklist"), SlashUserPerm(GuildPermission.ManageChannels), RequireContext(ContextType.Guild), CheckPermissions, BlacklistCheck]
    public async Task ConfessionBlacklist(IUser user)
    {
        var blacklists = _bot.GetGuildConfig(ctx.Guild.Id).ConfessionBlacklist.Split(" ");
        if (blacklists.Any())
        {
            if (blacklists.Contains(user.Id.ToString()))
            {
                await ctx.Interaction.SendErrorAsync("This user is already blacklisted!");
                return;
            }

            await Service.ToggleUserBlacklistAsync(ctx.Guild.Id, user.Id);
            await ctx.Interaction.SendConfirmAsync($"Added {user.Mention} to the confession blacklist!!");
        }
    }

    [SlashCommand("unblacklist", "Unblacklists a user from confessions"), SlashUserPerm(GuildPermission.ManageChannels), RequireContext(ContextType.Guild), CheckPermissions, BlacklistCheck]
    public async Task ConfessionUnblacklist(IUser user)
    {
        var blacklists = _bot.GetGuildConfig(ctx.Guild.Id).ConfessionBlacklist.Split(" ");
        if (blacklists.Any())
        {
            if (!blacklists.Contains(user.Id.ToString()))
            {
                await ctx.Interaction.SendErrorAsync("This user is not blacklisted!");
                return;
            }

            await Service.ToggleUserBlacklistAsync(ctx.Guild.Id, user.Id);
            await ctx.Interaction.SendConfirmAsync($"Removed {user.Mention} from the confession blacklist!!");
        }
    }

    [SlashCommand("report", "Reports a server for misuse of confessions"), BlacklistCheck]
    public async Task ConfessionsReport([Summary("ServerId", "The ID of the server abusing confessions")] string stringServerId, [Summary("description", "How are they abusing confessions? Include image links if possible.")] string how)
    {
        if (!ulong.TryParse(stringServerId, out var serverId))
        {
            await ctx.Interaction.SendErrorAsync("The ID you provided was invalid!");
            return;
        }

        var reportedGuild = await ((DiscordSocketClient)ctx.Client).Rest.GetGuildAsync(serverId);
        var channel = await ((DiscordSocketClient)ctx.Client).Rest.GetChannelAsync(_bot.Credentials.ConfessionReportChannelId).ConfigureAwait(false) as ITextChannel;
        var eb = new EmbedBuilder().WithErrorColor().WithTitle("Confessions Abuse Report Recieved")
                                   .AddField("Report", how)
                                   .AddField("Report User", $"{ctx.User} | {ctx.User.Id}")
                                   .AddField("Server ID", serverId);
        try
        {
            var invites = await reportedGuild.GetInvitesAsync();
            eb.AddField("Server Invite", invites.FirstOrDefault().Url);
        }
        catch
        {
            eb.AddField("Server Invite", "Unable to get invite due to missing permissions or no available invites.");
        }

        await channel.SendMessageAsync(embed: eb.Build());
        await ctx.Interaction.SendEphemeralErrorAsync(
            "Report sent. If you want to join and add on to it use the link below.");
    }
}
