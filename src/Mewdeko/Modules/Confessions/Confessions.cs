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
                await ErrorLocalizedAsync("confessions_none").ConfigureAwait(false);
                return;
            }

            if (gc.ConfessionBlacklist.Split(" ").Length > 0)
            {
                if (gc.ConfessionBlacklist.Split(" ").Contains(ctx.User.Id.ToString()))
                {
                    await ErrorLocalizedAsync("confessions_blacklisted").ConfigureAwait(false);
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
            await ErrorLocalizedAsync("confessions_none_any").ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels), RequireContext(ContextType.Guild)]
    public async Task ConfessionChannel(ITextChannel? channel = null)
    {
        if (channel is null)
        {
            await Service.SetConfessionChannel(ctx.Guild, 0).ConfigureAwait(false);
            await ConfirmLocalizedAsync("confessions_disabled").ConfigureAwait(false);
            return;
        }

        var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        var perms = currentUser.GetPermissions(channel);
        if (!perms.SendMessages || !perms.EmbedLinks)
        {
            await ErrorLocalizedAsync("confessions_invalid_perms").ConfigureAwait(false);
        }

        await Service.SetConfessionChannel(ctx.Guild, channel.Id).ConfigureAwait(false);
        await ConfirmLocalizedAsync("confessions_channel_set").ConfigureAwait(false);
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task ConfessionLogChannel(ITextChannel? channel = null)
    {
        if (channel is null)
        {
            await Service.SetConfessionChannel(ctx.Guild, 0).ConfigureAwait(false);
            await ConfirmLocalizedAsync("confessions_logging_disabled").ConfigureAwait(false);
            return;
        }

        var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        var perms = currentUser.GetPermissions(channel);
        if (!perms.SendMessages || !perms.EmbedLinks)
        {
            await ErrorLocalizedAsync("confessions_invalid_perms").ConfigureAwait(false);
        }

        await Service.SetConfessionLogChannel(ctx.Guild, channel.Id).ConfigureAwait(false);
        await ErrorLocalizedAsync("confessions_spleen", channel.Mention);
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels), RequireContext(ContextType.Guild)]
    public async Task ConfessionBlacklist(IUser user)
    {
        var blacklists = (await guildSettings.GetGuildConfig(ctx.Guild.Id)).ConfessionBlacklist.Split(" ");
        if (blacklists.Length > 0)
        {
            if (blacklists.Contains(user.Id.ToString()))
            {
                await ErrorLocalizedAsync("confessions_blacklisted_already").ConfigureAwait(false);
                return;
            }

            await Service.ToggleUserBlacklistAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
            await ConfirmLocalizedAsync("confessions_blacklisted_added", user.Mention);
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
                await ErrorLocalizedAsync("confessions_blacklisted_not").ConfigureAwait(false);
                return;
            }

            await Service.ToggleUserBlacklistAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
            await ConfirmLocalizedAsync("confessions_blacklisted_removed", user.Mention);
        }
    }
}
