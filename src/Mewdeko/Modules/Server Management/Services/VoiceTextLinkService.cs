using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.CustomVoice.Services;

namespace Mewdeko.Modules.Server_Management.Services;

/// <summary>
///     Gives members access to a text channel while they are in the voice channel it is linked to. A link on a
///     join-to-create hub covers every temporary channel made from it.
/// </summary>
public class VoiceTextLinkService : INService, IReadyExecutor
{
    private static readonly OverwritePermissions Grant = new OverwritePermissions().Modify(
        viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, readMessageHistory: PermValue.Allow);

    private readonly DiscordShardedClient client;
    private readonly CustomVoiceService customVoice;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ConcurrentDictionary<ulong, VoiceTextLink> links = new();
    private readonly ILogger<VoiceTextLinkService> logger;
    private readonly ConcurrentDictionary<ulong, ulong> temporaryHubs = new();
    private readonly GuildOwnershipService ownership;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VoiceTextLinkService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="customVoice">The custom voice service.</param>
    public VoiceTextLinkService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        EventHandler eventHandler, ILogger<VoiceTextLinkService> logger, CustomVoiceService customVoice,
        GuildOwnershipService ownership)
    {
        this.ownership = ownership;
        this.client = client;
        this.dbFactory = dbFactory;
        this.logger = logger;
        this.customVoice = customVoice;
        eventHandler.Subscribe("UserVoiceStateUpdated", "VoiceTextLinkService", OnUserVoiceStateUpdated);
        eventHandler.Subscribe("ChannelDestroyed", "VoiceTextLinkService", OnChannelDestroyed);
    }

    /// <summary>
    ///     Loads the links and fixes access that changed while the bot was offline.
    /// </summary>
    public async Task OnReadyAsync()
    {
        List<VoiceTextLink> all;
        await using (var db = await dbFactory.CreateConnectionAsync())
            all = await db.VoiceTextLinks.ToListAsync();

        foreach (var link in all)
            links[link.VoiceChannelId] = link;

        _ = Task.Run(async () =>
        {
            foreach (var link in all.Where(x => ownership.IsMine(x.GuildId)))
            {
                try
                {
                    await SyncAsync(link);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not sync the voice text link of {VoiceChannelId}",
                        link.VoiceChannelId);
                }
            }
        });
    }

    /// <summary>
    ///     Lists the links in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public List<VoiceTextLink> GetLinks(ulong guildId) =>
        links.Values.Where(x => x.GuildId == guildId).OrderBy(x => x.VoiceChannelId).ToList();

    /// <summary>
    ///     Links a voice channel to a text channel, replacing any earlier link of the voice channel, and gives the members
    ///     already in the voice channel access.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="voiceChannelId">The voice channel ID.</param>
    /// <param name="textChannelId">The text channel ID.</param>
    public async Task LinkAsync(ulong guildId, ulong voiceChannelId, ulong textChannelId)
    {
        var old = links.GetValueOrDefault(voiceChannelId);
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.VoiceTextLinks.Where(x => x.VoiceChannelId == voiceChannelId).DeleteAsync();
        var link = new VoiceTextLink
        {
            GuildId = guildId,
            VoiceChannelId = voiceChannelId,
            TextChannelId = textChannelId,
            DateAdded = DateTime.UtcNow
        };
        await db.InsertAsync(link);
        links[voiceChannelId] = link;

        if (old is not null && old.TextChannelId != textChannelId)
            await RevokeAllAsync(old);
        await SyncAsync(link);
    }

    /// <summary>
    ///     Removes a link and takes back the access it gave.
    /// </summary>
    /// <param name="voiceChannelId">The voice channel ID.</param>
    /// <returns>False if the voice channel had no link.</returns>
    public async Task<bool> UnlinkAsync(ulong voiceChannelId)
    {
        if (!links.TryRemove(voiceChannelId, out var link))
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.VoiceTextLinks.Where(x => x.VoiceChannelId == voiceChannelId).DeleteAsync();
        await RevokeAllAsync(link);
        return true;
    }

    private static bool IsGrant(OverwritePermissions perms) =>
        perms.AllowValue == Grant.AllowValue && perms.DenyValue == 0;

    private async Task SyncAsync(VoiceTextLink link)
    {
        var guild = client.GetGuild(link.GuildId);
        var text = guild?.GetChannel(link.TextChannelId);
        if (text is null)
            return;

        var channelIds = new List<ulong> { link.VoiceChannelId };
        channelIds.AddRange(await customVoice.GetTemporaryChannelIdsAsync(link.GuildId, link.VoiceChannelId));

        var inside = channelIds.Select(x => guild!.GetVoiceChannel(x)).Where(x => x is not null)
            .SelectMany(x => x.ConnectedUsers).Where(x => !x.IsBot).DistinctBy(x => x.Id).ToList();
        var insideIds = inside.Select(x => x.Id).ToHashSet();

        foreach (var overwrite in text.PermissionOverwrites.ToList())
        {
            if (overwrite.TargetType != PermissionTarget.User || insideIds.Contains(overwrite.TargetId)
                                                              || !IsGrant(overwrite.Permissions))
                continue;

            var user = guild!.GetUser(overwrite.TargetId);
            if (user is not null && !await IsLinkedToAsync(user, link))
                await text.RemovePermissionOverwriteAsync(user);
        }

        foreach (var user in inside)
            await GrantAsync(text, user);
    }

    private async Task<bool> IsLinkedToAsync(SocketGuildUser user, VoiceTextLink link) =>
        (await ResolveAsync(user.VoiceChannel))?.TextChannelId == link.TextChannelId;

    private async Task<VoiceTextLink> ResolveAsync(SocketVoiceChannel channel)
    {
        if (channel is null)
            return null;

        if (links.TryGetValue(channel.Id, out var direct))
            return direct;

        if (!links.Values.Any(x => x.GuildId == channel.Guild.Id))
            return null;

        if (!temporaryHubs.TryGetValue(channel.Id, out var hubId))
        {
            var found = await customVoice.GetHubIdAsync(channel.Guild.Id, channel.Id);
            if (found is not { } hub)
                return null;

            hubId = temporaryHubs[channel.Id] = hub;
        }

        return links.GetValueOrDefault(hubId);
    }

    private async Task RevokeAllAsync(VoiceTextLink link)
    {
        var guild = client.GetGuild(link.GuildId);
        var text = guild?.GetChannel(link.TextChannelId);
        if (text is null)
            return;

        foreach (var overwrite in text.PermissionOverwrites.ToList())
        {
            if (overwrite.TargetType != PermissionTarget.User || !IsGrant(overwrite.Permissions))
                continue;

            var user = guild!.GetUser(overwrite.TargetId);
            if (user is not null && !await IsLinkedToAsync(user, link))
                await text.RemovePermissionOverwriteAsync(user);
        }
    }

    private static async Task GrantAsync(SocketGuildChannel text, SocketGuildUser user)
    {
        if (text.GetPermissionOverwrite(user) is not null)
            return;

        await text.AddPermissionOverwriteAsync(user, Grant);
    }

    private static async Task RevokeAsync(SocketGuildChannel text, SocketGuildUser user)
    {
        if (text.GetPermissionOverwrite(user) is { } current && IsGrant(current))
            await text.RemovePermissionOverwriteAsync(user);
    }

    private async Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        if (user is not SocketGuildUser member || member.IsBot || before.VoiceChannel?.Id == after.VoiceChannel?.Id)
            return;

        try
        {
            var left = await ResolveAsync(before.VoiceChannel);
            var joined = await ResolveAsync(after.VoiceChannel);
            if (left is not null && left.TextChannelId != joined?.TextChannelId
                && member.Guild.GetChannel(left.TextChannelId) is { } leftText)
                await RevokeAsync(leftText, member);

            if (joined is not null && member.Guild.GetChannel(joined.TextChannelId) is { } joinedText)
                await GrantAsync(joinedText, member);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not update the voice text access of {UserId}", user.Id);
        }
    }

    private async Task OnChannelDestroyed(SocketChannel channel)
    {
        if (channel is SocketVoiceChannel && temporaryHubs.ContainsKey(channel.Id))
        {
            _ = Task.Delay(TimeSpan.FromMinutes(1)).ContinueWith(_ => temporaryHubs.TryRemove(channel.Id, out var removed));
        }

        try
        {
            var gone = links.Values.Where(x => x.VoiceChannelId == channel.Id || x.TextChannelId == channel.Id)
                .ToList();
            if (gone.Count == 0)
                return;

            await using var db = await dbFactory.CreateConnectionAsync();
            foreach (var link in gone)
            {
                links.TryRemove(link.VoiceChannelId, out _);
                await db.VoiceTextLinks.Where(x => x.VoiceChannelId == link.VoiceChannelId).DeleteAsync();
                if (link.VoiceChannelId == channel.Id)
                    await RevokeAllAsync(link);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not clear the voice text links of {ChannelId}", channel.Id);
        }
    }
}
