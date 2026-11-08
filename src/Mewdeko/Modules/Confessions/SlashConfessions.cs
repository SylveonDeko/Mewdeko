using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Confessions.Services;

namespace Mewdeko.Modules.Confessions;

/// <summary>
///     Module for managing confessions.
/// </summary>
[Group("confessions", "Manage confessions.")]
public class SlashConfessions : MewdekoSlashModuleBase<ConfessionService>
{
    private readonly IBotCredentials credentials;
    private readonly GuildSettingsService guildSettings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SlashConfessions" /> class.
    /// </summary>
    /// <param name="guildSettings"></param>
    /// <param name="credentials"></param>
    public SlashConfessions(GuildSettingsService guildSettings, IBotCredentials credentials)
    {
        this.guildSettings = guildSettings;
        this.credentials = credentials;
    }


    /// <summary>
    ///     Sends a confession to the confession channel.
    /// </summary>
    /// <param name="confession">The confession message.</param>
    /// <param name="attachment">Optional attachment for the confession.</param>
    /// <example>/confess lefalaf.</example>
    [SlashCommand("confess", "Sends your confession to the confession channel.", true)]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Confess(string confession, IAttachment? attachment = null)
    {
        var attachUrl = attachment?.Url;
        if ((await guildSettings.GetGuildConfig(ctx.Guild.Id)).ConfessionChannel is 0)
        {
            await EphemeralReplyErrorAsync(Strings.ConfessionsNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (await Service.IsBlacklistedAsync(ctx.Guild.Id, ctx.User.Id).ConfigureAwait(false))
        {
            await EphemeralReplyErrorAsync(Strings.ConfessionsBlacklisted(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.SendConfession(ctx.Guild.Id, ctx.User, confession, ctx.Channel, ctx, attachUrl)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the confession channel.
    /// </summary>
    /// <param name="channel">The channel to set as the confession channel.</param>
    /// <example>/confessions channel #confessions</example>
    [SlashCommand("channel", "Set the confession channel")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task ConfessionChannel(ITextChannel? channel = null)
    {
        if (channel is null)
        {
            await Service.SetConfessionChannel(ctx.Guild, 0).ConfigureAwait(false);
            await ConfirmAsync(Strings.ConfessionsDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        var perms = currentUser.GetPermissions(channel);
        if (!perms.SendMessages || !perms.EmbedLinks)
        {
            await ErrorAsync(Strings.ConfessionsInvalidPerms(ctx.Guild.Id)).ConfigureAwait(false);
        }

        await Service.SetConfessionChannel(ctx.Guild, channel.Id).ConfigureAwait(false);
        await ConfirmAsync(Strings.ConfessionsChannelSet(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the confession log channel. Misuse of this feature will end up with me being 2m away from your house.
    /// </summary>
    /// <param name="channel">The channel to set as the confession log channel.</param>
    /// <example>/confessions logchannel #confessions</example>
    [SlashCommand("logchannel", "Set the confession channel")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task ConfessionLogChannel(ITextChannel? channel = null)
    {
        if (channel is null)
        {
            await Service.SetConfessionLogChannel(ctx.Guild, 0).ConfigureAwait(false);
            await ConfirmAsync(Strings.ConfessionsLoggingDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        var perms = currentUser.GetPermissions(channel);
        if (!perms.SendMessages || !perms.EmbedLinks)
        {
            await ErrorAsync(Strings.ConfessionsInvalidPerms(ctx.Guild.Id)).ConfigureAwait(false);
        }

        await Service.SetConfessionLogChannel(ctx.Guild, channel.Id).ConfigureAwait(false);
        await ErrorAsync(Strings.ConfessionsSpleen(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a user to the confession blacklist.
    /// </summary>
    /// <param name="user">The user to add to the confession blacklist.</param>
    /// <example>/confessions blacklist @user</example>
    [SlashCommand("blacklist", "Add a user to the confession blacklist")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task ConfessionBlacklist(IUser user)
    {
        var blacklists = (await guildSettings.GetGuildConfig(ctx.Guild.Id)).ConfessionBlacklist.Split(" ");
        if (blacklists.Length > 0)
        {
            if (blacklists.Contains(user.Id.ToString()))
            {
                await ErrorAsync(Strings.ConfessionsBlacklistedAlready(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.ToggleUserBlacklistAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
            await ConfirmAsync(Strings.ConfessionsBlacklistedAdded(ctx.Guild.Id, user.Mention)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Unblacklists a user from confessions.
    /// </summary>
    /// <param name="user">The user to unblacklist from confessions.</param>
    /// <example>/confessions unblacklist @user</example>
    [SlashCommand("unblacklist", "Unblacklists a user from confessions")]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task ConfessionUnblacklist(IUser user)
    {
        var blacklists = (await guildSettings.GetGuildConfig(ctx.Guild.Id)).ConfessionBlacklist.Split(" ");
        if (blacklists.Length > 0)
        {
            if (!blacklists.Contains(user.Id.ToString()))
            {
                await ErrorAsync(Strings.ConfessionsBlacklistedNot(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.ToggleUserBlacklistAsync(ctx.Guild.Id, user.Id).ConfigureAwait(false);
            await ConfirmAsync(Strings.ConfessionsBlacklistedRemoved(ctx.Guild.Id, user.Mention)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Reports a server for misuse of confessions.
    /// </summary>
    /// <param name="stringServerId">The ID of the server abusing confessions.</param>
    /// <param name="how">How are they abusing confessions? Include image links if possible.</param>
    /// <example>/confessions report 1234567890 They are abusing confessions.</example>
    [SlashCommand("report", "Reports a server for misuse of confessions")]
    public async Task ConfessReport(
        [Summary("ServerId", "The ID of the server abusing confessions")]
        string stringServerId,
        [Summary("description", "How are they abusing confessions? Include image links if possible.")]
        string how)
    {
        if (!ulong.TryParse(stringServerId, out var serverId))
        {
            await ErrorAsync(Strings.ConfessionsInvalidId(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var reportedGuild = await ((DiscordShardedClient)ctx.Client).Rest.GetGuildAsync(serverId).ConfigureAwait(false);
        var channel =
            await ((DiscordShardedClient)ctx.Client).Rest.GetChannelAsync(credentials.ConfessionReportChannelId)
                .ConfigureAwait(false) as ITextChannel;
        var eb = new EmbedBuilder().WithErrorColor().WithTitle(Strings.ConfessionsReportReceived(ctx.Guild.Id))
            .AddField(Strings.ConfessionsReport(ctx.Guild.Id), how)
            .AddField(Strings.ConfessionsReportUser(ctx.Guild.Id), $"{ctx.User} | {ctx.User.Id}")
            .AddField(Strings.ConfessionsServerId(ctx.Guild.Id), serverId);
        try
        {
            var invites = await reportedGuild.GetInvitesAsync().ConfigureAwait(false);
            eb.AddField(Strings.ConfessionsServerInvite(ctx.Guild.Id), invites.FirstOrDefault().Url);
        }
        catch
        {
            eb.AddField(Strings.ConfessionsServerInvite(ctx.Guild.Id), Strings.ConfessionsMissingInvite(ctx.Guild.Id));
        }

        await channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        await EphemeralReplyErrorAsync(Strings.ConfessionsReportSent(ctx.Guild.Id)).ConfigureAwait(false);
    }
}