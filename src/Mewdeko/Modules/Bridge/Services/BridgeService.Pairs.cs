using DataModel;
using Mewdeko.Modules.Bridge.Common;

namespace Mewdeko.Modules.Bridge.Services;

public partial class BridgeService
{
    string IBridgePairHost.Platform => ThisPlatform;

    bool IBridgePairHost.HasGuild(ulong guildId) =>
        client.GetGuild(guildId) is not null && ownership.IsMine(guildId);

    string IBridgePairHost.GuildName(ulong guildId) => client.GetGuild(guildId)?.Name ?? "";

    IReadOnlyList<BridgeChannelInfo> IBridgePairHost.TextChannels(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return [];

        return guild.TextChannels
            .OrderBy(x => x.Category?.Position ?? -1)
            .ThenBy(x => x.Position)
            .Select(x => new BridgeChannelInfo(x.Id, x.Name, x.Category?.Name ?? "", IsPublic(guild, x)))
            .ToList();
    }

    bool IBridgePairHost.IsBridged(ulong channelId) => byChannel.ContainsKey(channelId);

    bool IBridgePairHost.CanManageWebhooks(ulong guildId, ulong channelId) =>
        client.GetGuild(guildId) is { } guild && guild.GetTextChannel(channelId) is { } channel
                                              && guild.CurrentUser.GetPermissions(channel).ManageWebhooks;

    async Task<bool> IBridgePairHost.CanManageAsync(ulong guildId, ulong userId)
    {
        if (client.GetGuild(guildId) is not { } guild)
            return false;

        var user = await ((IGuild)guild).GetUserAsync(userId);
        return user is not null && (guild.OwnerId == userId || user.GuildPermissions.ManageGuild
                                    || user.GuildPermissions.Administrator);
    }

    IReadOnlyList<ChannelBridge> IBridgePairHost.BridgesOfPair(Guid pairId) =>
        byId.Values.Where(x => x.PairId == pairId).ToList();

    async Task<ChannelBridge?> IBridgePairHost.CreateBridgeAsync(ulong guildId, ulong channelId, ulong userId,
        Guid pairId, ulong remoteGuildId, string remoteGuildName, ulong remoteChannelId, string remoteChannelName)
    {
        var channel = client.GetGuild(guildId)?.GetTextChannel(channelId);
        if (channel is null || byChannel.ContainsKey(channelId))
            return null;

        var url = await EnsureWebhookAsync(channel, null);
        if (url is null)
            return null;

        var bridge = NewBridge(Guid.NewGuid(), guildId, channelId, url, userId, remoteGuildId, remoteGuildName,
            remoteChannelId, remoteChannelName, pairId);
        await InsertAsync(bridge);
        return bridge;
    }

    Task IBridgePairHost.RemoveBridgeAsync(ChannelBridge bridge) => RemoveAsync(bridge);

    Task IBridgePairHost.UnlinkLocalAsync(ChannelBridge bridge) => HandleUnlinkedAsync(bridge);

    async Task IBridgePairHost.ClearPairAsync(Guid pairId)
    {
        foreach (var bridge in byId.Values.Where(x => x.PairId == pairId).ToList())
        {
            bridge.PairId = null;
            await UpdateAsync(bridge);
        }
    }

    Task IBridgePairHost.PublishAsync(BridgeEnvelope envelope) => PublishAsync(envelope);

    async Task IBridgePairHost.NoticeAsync(ulong guildId, ulong channelId, string text)
    {
        try
        {
            if (client.GetGuild(guildId)?.GetTextChannel(channelId) is { } channel)
                await channel.SendConfirmAsync(text);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not post a bridge notice in channel {ChannelId}", channelId);
        }
    }

    Task IBridgePairHost.NoticeLinkedAsync(ChannelBridge bridge) =>
        ((IBridgePairHost)this).NoticeAsync(bridge.GuildId, bridge.ChannelId, strings.BridgeLinked(bridge.GuildId,
            bridge.RemoteChannelName, bridge.RemoteGuildName, BridgeSettings.PlatformLabel(BridgeProtocol.Fluxer)));

    private static bool IsPublic(SocketGuild guild, SocketTextChannel channel)
    {
        var everyone = guild.EveryoneRole;
        if (everyone.Permissions.Administrator)
            return true;

        var view = everyone.Permissions.ViewChannel;
        var overwrite = channel.GetPermissionOverwrite(everyone);
        if (overwrite is { } value)
        {
            if (value.ViewChannel == PermValue.Deny)
                view = false;
            else if (value.ViewChannel == PermValue.Allow)
                view = true;
        }

        return view;
    }

    private Task OnChannelCreated(SocketChannel channel) => ChannelChanged(channel);

    private Task OnChannelDestroyed(SocketChannel channel) => ChannelChanged(channel);

    private Task OnChannelUpdated(SocketChannel before, SocketChannel after) => ChannelChanged(after);

    private Task ChannelChanged(SocketChannel channel)
    {
        if (channel is SocketGuildChannel guildChannel)
            Pairs.ChannelsChanged(guildChannel.Guild.Id);

        return Task.CompletedTask;
    }

    private Task OnRoleUpdated(SocketRole before, SocketRole after)
    {
        if (after.Id == after.Guild.Id)
            Pairs.ChannelsChanged(after.Guild.Id);

        return Task.CompletedTask;
    }
}
