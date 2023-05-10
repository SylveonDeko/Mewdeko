using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Confessions.Services;

namespace Mewdeko.Modules.Confessions;

[Group("confessions", "Manage confessions.")]
public class SlashConfessions : MewdekoSlashModuleBase<ConfessionService>
{
    private readonly GuildSettingsService guildSettings;
    private readonly IBotCredentials credentials;

    public SlashConfessions(GuildSettingsService guildSettings, IBotCredentials credentials)
    {
        this.guildSettings = guildSettings;
        this.credentials = credentials;
    }


    [SlashCommand("confess", "Sends your confession to the confession channel.", true), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Confess(string confession, IAttachment? attachment = null)
    {
        var blacklists = (await guildSettings.GetGuildConfig(ctx.Guild.Id)).ConfessionBlacklist.Split(" ");
        var attachUrl = attachment?.Url;
        if ((await guildSettings.GetGuildConfig(ctx.Guild.Id)).ConfessionChannel is 0)
        {
            await EphemeralReplyErrorLocalizedAsync("confessions_none").ConfigureAwait(false);
            return;
        }

        if (blacklists.Length > 0)
        {
            if (blacklists.Contains(ctx.User.Id.ToString()))
            {
                await EphemeralReplyErrorLocalizedAsync("confessions_blacklisted").ConfigureAwait(false);
                return;
            }

            await Service.SendConfession(ctx.Guild.Id, ctx.User, confession, ctx.Channel, ctx, attachUrl).ConfigureAwait(false);
        }
        else
        {
            await Service.SendConfession(ctx.Guild.Id, ctx.User, confession, ctx.Channel, ctx, attachUrl).ConfigureAwait(false);
        }
    }

    [SlashCommand("channel", "Set the confession channel"), SlashUserPerm(GuildPermission.ManageChannels), RequireContext(ContextType.Guild), CheckPermissions]
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
        await ConfirmLocalizedAsync("confessions_channel_set", channel.Id).ConfigureAwait(false);
    }

    [SlashCommand("logchannel", "Set the confession channel"), SlashUserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task ConfessionLogChannel(ITextChannel? channel = null)
    {
        if (channel is null)
        {
            await Service.SetConfessionLogChannel(ctx.Guild, 0).ConfigureAwait(false);
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
        await ErrorLocalizedAsync("confessions_spleen").ConfigureAwait(false);
    }

    [SlashCommand("blacklist", "Add a user to the confession blacklist"), SlashUserPerm(GuildPermission.ManageChannels), RequireContext(ContextType.Guild), CheckPermissions]
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
            await ConfirmLocalizedAsync("confessions_blacklisted_added", user.Mention).ConfigureAwait(false);
        }
    }

    [SlashCommand("unblacklist", "Unblacklists a user from confessions"), SlashUserPerm(GuildPermission.ManageChannels), RequireContext(ContextType.Guild), CheckPermissions]
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
            await ConfirmLocalizedAsync("confessions_blacklisted_removed", user.Mention).ConfigureAwait(false);
        }
    }

    [SlashCommand("report", "Reports a server for misuse of confessions")]
    public async Task ConfessReport([Summary("ServerId", "The ID of the server abusing confessions")] string stringServerId,
        [Summary("description", "How are they abusing confessions? Include image links if possible.")] string how)
    {
        if (!ulong.TryParse(stringServerId, out var serverId))
        {
            await ErrorLocalizedAsync("confessions_invalid_id").ConfigureAwait(false);
            return;
        }

        var reportedGuild = await ((DiscordSocketClient)ctx.Client).Rest.GetGuildAsync(serverId).ConfigureAwait(false);
        var channel = await ((DiscordSocketClient)ctx.Client).Rest.GetChannelAsync(credentials.ConfessionReportChannelId).ConfigureAwait(false) as ITextChannel;
        var eb = new EmbedBuilder().WithErrorColor().WithTitle(GetText("confessions_report_received"))
            .AddField(GetText("confessions_report"), how)
            .AddField(GetText("confessions_report_user"), $"{ctx.User} | {ctx.User.Id}")
            .AddField(GetText("confessions_server_id"), serverId);
        try
        {
            var invites = await reportedGuild.GetInvitesAsync().ConfigureAwait(false);
            eb.AddField(GetText("confessions_server_invite"), invites.FirstOrDefault().Url);
        }
        catch
        {
            eb.AddField(GetText("confessions_server_invite"), GetText("confessions_missing_invite"));
        }

        await channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        await EphemeralReplyErrorLocalizedAsync("confessions_report_sent").ConfigureAwait(false);
    }
}
