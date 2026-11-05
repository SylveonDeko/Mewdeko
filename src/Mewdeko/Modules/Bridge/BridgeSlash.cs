using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Bridge.Common;
using Mewdeko.Modules.Bridge.Services;

namespace Mewdeko.Modules.Bridge;

/// <summary>
///     Slash commands for bridging a channel to a channel on Fluxer.
/// </summary>
[Group("bridge", "Connect a channel to a channel on Fluxer")]
public partial class BridgeSlash : MewdekoSlashSubmodule<BridgeService>
{
    /// <summary>
    ///     Creates a code to finish the bridge from the other platform.
    /// </summary>
    [SlashCommand("create", "Create a code to connect this channel from Fluxer")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Create()
    {
        var (error, code, _) = await Service.CreateCodeAsync(ctx.Guild, (ITextChannel)ctx.Channel, ctx.User.Id);
        if (error != BridgeError.None)
        {
            await EphemeralReplyErrorAsync(ErrorText(error));
            return;
        }

        await EphemeralReplyConfirmAsync(Strings.BridgeCodeCreated(ctx.Guild.Id, code, "/"));
    }

    /// <summary>
    ///     Bridges this channel with the channel a code was created for.
    /// </summary>
    /// <param name="code">The code from the other platform.</param>
    [SlashCommand("link", "Connect this channel using a code from Fluxer")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Link(string code)
    {
        await DeferAsync(true);
        var (error, bridge) = await Service.LinkAsync(ctx.Guild, (ITextChannel)ctx.Channel, ctx.User.Id, code);
        if (error != BridgeError.None || bridge is null)
        {
            await EphemeralReplyErrorAsync(ErrorText(error));
            return;
        }

        await EphemeralReplyConfirmAsync(Strings.BridgeLinked(ctx.Guild.Id, bridge.RemoteChannelName,
            bridge.RemoteGuildName, "Fluxer"));
    }

    /// <summary>
    ///     Lists the bridged channels of the server.
    /// </summary>
    [SlashCommand("list", "List the bridged channels")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task List()
    {
        var bridges = Service.GetBridges(ctx.Guild.Id);
        if (bridges.Count == 0)
        {
            await EphemeralReplyErrorAsync(Strings.BridgeListEmpty(ctx.Guild.Id));
            return;
        }

        var lines = bridges.Select(x => Strings.BridgeListItem(ctx.Guild.Id, x.ChannelId, x.RemoteChannelName,
            x.RemoteGuildName, "Fluxer", x.Paused ? Strings.BridgeListPaused(ctx.Guild.Id) : ""));
        await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
            .WithTitle(Strings.BridgeListTitle(ctx.Guild.Id)).WithDescription(string.Join('\n', lines)).Build(),
            ephemeral: true);
    }

    /// <summary>
    ///     Pauses the bridge on this channel.
    /// </summary>
    [SlashCommand("pause", "Pause the bridge on this channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Pause()
    {
        if (Service.GetBridge(ctx.Channel.Id) is not { } bridge)
        {
            await EphemeralReplyErrorAsync(Strings.BridgeNotBridged(ctx.Guild.Id));
            return;
        }

        await Service.SetPausedAsync(bridge, true);
        await EphemeralReplyConfirmAsync(Strings.BridgePaused(ctx.Guild.Id));
    }

    /// <summary>
    ///     Resumes the bridge on this channel.
    /// </summary>
    [SlashCommand("resume", "Resume the bridge on this channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Resume()
    {
        if (Service.GetBridge(ctx.Channel.Id) is not { } bridge)
        {
            await EphemeralReplyErrorAsync(Strings.BridgeNotBridged(ctx.Guild.Id));
            return;
        }

        await Service.SetPausedAsync(bridge, false);
        await EphemeralReplyConfirmAsync(Strings.BridgeResumed(ctx.Guild.Id));
    }

    /// <summary>
    ///     Removes the bridge on this channel.
    /// </summary>
    [SlashCommand("remove", "Remove the bridge on this channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Remove()
    {
        if (Service.GetBridge(ctx.Channel.Id) is not { } bridge)
        {
            await EphemeralReplyErrorAsync(Strings.BridgeNotBridged(ctx.Guild.Id));
            return;
        }

        await Service.RemoveAsync(bridge);
        await EphemeralReplyConfirmAsync(Strings.BridgeRemoved(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets whether messages from bots are relayed across this channel's bridge.
    /// </summary>
    /// <param name="allow">Whether bots are relayed.</param>
    [SlashCommand("allowbots", "Choose whether messages from bots are relayed")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task AllowBots(bool allow)
    {
        if (Service.GetBridge(ctx.Channel.Id) is not { } bridge)
        {
            await EphemeralReplyErrorAsync(Strings.BridgeNotBridged(ctx.Guild.Id));
            return;
        }

        await Service.SetAllowBotsAsync(bridge, allow);
        await EphemeralReplyConfirmAsync(allow
            ? Strings.BridgeAllowbotsOn(ctx.Guild.Id)
            : Strings.BridgeAllowbotsOff(ctx.Guild.Id));
    }

    /// <summary>
    ///     Shows every option of the bridge on this channel.
    /// </summary>
    [SlashCommand("settings", "Show the options of the bridge on this channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Settings()
    {
        if (Service.GetBridge(ctx.Channel.Id) is not { } bridge)
        {
            await EphemeralReplyErrorAsync(Strings.BridgeNotBridged(ctx.Guild.Id));
            return;
        }

        await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
            .WithTitle(Strings.BridgeSettingsTitle(ctx.Guild.Id)).WithDescription(Service.DescribeOptions(bridge))
            .Build(), ephemeral: true);
    }

    /// <summary>
    ///     Sets which way messages flow on this channel.
    /// </summary>
    /// <param name="direction">Both, send only or receive only.</param>
    [SlashCommand("direction", "Choose which way messages flow on this channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Direction(BridgeDirection direction)
    {
        if (Service.GetBridge(ctx.Channel.Id) is not { } bridge)
        {
            await EphemeralReplyErrorAsync(Strings.BridgeNotBridged(ctx.Guild.Id));
            return;
        }

        await Service.SetOptionsAsync(bridge, bridge.OptionsOf() with
        {
            Direction = direction
        });
        await EphemeralReplyConfirmAsync(Strings.BridgeDirectionSet(ctx.Guild.Id,
            Service.DirectionName(ctx.Guild.Id, direction)));
    }

    /// <summary>
    ///     Sets the roles that may be relayed.
    /// </summary>
    /// <param name="roles">Roles to allow, or none.</param>
    [SlashCommand("allowrole", "Only relay members with these roles, or none for everyone")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public Task AllowRole(string roles) => SetRolesAsync(roles, true);

    /// <summary>
    ///     Sets the roles that are never relayed.
    /// </summary>
    /// <param name="roles">Roles to block, or none.</param>
    [SlashCommand("blockrole", "Never relay members with these roles, or none to clear")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public Task BlockRole(string roles) => SetRolesAsync(roles, false);

    /// <summary>
    ///     Stops relaying a user.
    /// </summary>
    /// <param name="user">The user to block.</param>
    [SlashCommand("block", "Never relay a user from this channel")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public Task Block(IUser user) => SetBlockedAsync(user, true);

    /// <summary>
    ///     Relays a blocked user again.
    /// </summary>
    /// <param name="user">The user to unblock.</param>
    [SlashCommand("unblock", "Relay a blocked user from this channel again")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public Task Unblock(IUser user) => SetBlockedAsync(user, false);

    /// <summary>
    ///     Sets how long members must have been in the server to be relayed.
    /// </summary>
    /// <param name="duration">A duration such as 10m, 2h or 7d, or off.</param>
    [SlashCommand("minage", "Only relay members who joined at least this long ago")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task MinAge(string duration)
    {
        if (Service.GetBridge(ctx.Channel.Id) is not { } bridge)
        {
            await EphemeralReplyErrorAsync(Strings.BridgeNotBridged(ctx.Guild.Id));
            return;
        }

        if (!BridgeSettings.TryParseMinAge(duration, out var minutes))
        {
            await EphemeralReplyErrorAsync(Strings.BridgeMinageUsage(ctx.Guild.Id, "/"));
            return;
        }

        await Service.SetOptionsAsync(bridge, bridge.OptionsOf() with
        {
            MinMemberAgeMinutes = minutes
        });
        await EphemeralReplyConfirmAsync(minutes == 0
            ? Strings.BridgeMinageOff(ctx.Guild.Id)
            : Strings.BridgeMinageSet(ctx.Guild.Id, BridgeService.DescribeMinAge(minutes)));
    }

    /// <summary>
    ///     Sets whether messages with invite links are dropped.
    /// </summary>
    /// <param name="block">Whether invites are blocked.</param>
    [SlashCommand("invites", "Choose whether messages with invite links are blocked")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Invites(bool block)
    {
        if (Service.GetBridge(ctx.Channel.Id) is not { } bridge)
        {
            await EphemeralReplyErrorAsync(Strings.BridgeNotBridged(ctx.Guild.Id));
            return;
        }

        await Service.SetOptionsAsync(bridge, bridge.OptionsOf() with
        {
            BlockInvites = block
        });
        await EphemeralReplyConfirmAsync(block
            ? Strings.BridgeInvitesOn(ctx.Guild.Id)
            : Strings.BridgeInvitesOff(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets whether messages with filtered words are dropped.
    /// </summary>
    /// <param name="enabled">Whether the word filter is used.</param>
    [SlashCommand("filter", "Choose whether messages with filtered words are blocked")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task Filter(bool enabled)
    {
        if (Service.GetBridge(ctx.Channel.Id) is not { } bridge)
        {
            await EphemeralReplyErrorAsync(Strings.BridgeNotBridged(ctx.Guild.Id));
            return;
        }

        await Service.SetOptionsAsync(bridge, bridge.OptionsOf() with
        {
            UseWordFilter = enabled
        });
        await EphemeralReplyConfirmAsync(enabled
            ? Strings.BridgeFilterOn(ctx.Guild.Id)
            : Strings.BridgeFilterOff(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets how incoming authors are named.
    /// </summary>
    /// <param name="format">A format containing {name}, or reset.</param>
    [SlashCommand("nameformat", "Set how authors from the other side are named")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task NameFormat(string format)
    {
        if (Service.GetBridge(ctx.Channel.Id) is not { } bridge)
        {
            await EphemeralReplyErrorAsync(Strings.BridgeNotBridged(ctx.Guild.Id));
            return;
        }

        var reset = format.Trim().Equals("reset", StringComparison.OrdinalIgnoreCase);
        var value = reset ? BridgeSettings.DefaultNameFormat : format.Trim();
        if (!BridgeSettings.IsValidNameFormat(value))
        {
            await EphemeralReplyErrorAsync(Strings.BridgeNameformatUsage(ctx.Guild.Id, "/", "{name}", "{platform}",
                "{community}"));
            return;
        }

        await Service.SetOptionsAsync(bridge, bridge.OptionsOf() with
        {
            NameFormat = value
        });
        await EphemeralReplyConfirmAsync(reset
            ? Strings.BridgeNameformatReset(ctx.Guild.Id)
            : Strings.BridgeNameformatSet(ctx.Guild.Id, value));
    }

    private async Task SetRolesAsync(string text, bool allow)
    {
        if (Service.GetBridge(ctx.Channel.Id) is not { } bridge)
        {
            await EphemeralReplyErrorAsync(Strings.BridgeNotBridged(ctx.Guild.Id));
            return;
        }

        var clear = text.Trim().Equals("none", StringComparison.OrdinalIgnoreCase);
        var roles = clear
            ? []
            : BridgeSettings.ParseRoleIds(text, ctx.Guild.Roles.Select(x => (x.Id, x.Name)).ToList());
        if (!clear && roles.Count == 0)
        {
            await EphemeralReplyErrorAsync(Strings.BridgeRolesUnknown(ctx.Guild.Id));
            return;
        }

        var options = bridge.OptionsOf();
        await Service.SetOptionsAsync(bridge, allow
            ? options with { AllowedRoleIds = roles }
            : options with { BlockedRoleIds = roles });
        var list = string.Join(' ', roles.Select(x => $"<@&{x}>"));
        await EphemeralReplyConfirmAsync((allow, clear) switch
        {
            (true, true) => Strings.BridgeAllowroleCleared(ctx.Guild.Id),
            (true, false) => Strings.BridgeAllowroleSet(ctx.Guild.Id, list),
            (false, true) => Strings.BridgeBlockroleCleared(ctx.Guild.Id),
            _ => Strings.BridgeBlockroleSet(ctx.Guild.Id, list)
        });
    }

    private async Task SetBlockedAsync(IUser user, bool block)
    {
        if (Service.GetBridge(ctx.Channel.Id) is not { } bridge)
        {
            await EphemeralReplyErrorAsync(Strings.BridgeNotBridged(ctx.Guild.Id));
            return;
        }

        var options = bridge.OptionsOf();
        var blocked = options.BlockedUserIds.ToList();
        if (block == blocked.Contains(user.Id))
        {
            await EphemeralReplyErrorAsync(block
                ? Strings.BridgeBlockExists(ctx.Guild.Id)
                : Strings.BridgeBlockMissing(ctx.Guild.Id));
            return;
        }

        if (block)
            blocked.Add(user.Id);
        else
            blocked.Remove(user.Id);

        await Service.SetOptionsAsync(bridge, options with { BlockedUserIds = blocked });
        await EphemeralReplyConfirmAsync(block
            ? Strings.BridgeBlockAdded(ctx.Guild.Id, user.Id)
            : Strings.BridgeBlockRemoved(ctx.Guild.Id, user.Id));
    }

    private string ErrorText(BridgeError error) => error switch
    {
        BridgeError.NoPermission => Strings.BridgeNoPermission(ctx.Guild.Id),
        BridgeError.NeedWebhooks => Strings.BridgeNeedWebhooks(ctx.Guild.Id),
        BridgeError.AlreadyBridged => Strings.BridgeAlreadyBridged(ctx.Guild.Id),
        BridgeError.InvalidCode => Strings.BridgeInvalidCode(ctx.Guild.Id),
        BridgeError.SamePlatform => Strings.BridgeSamePlatform(ctx.Guild.Id),
        BridgeError.ChannelNotFound => Strings.BridgeChannelMissing(ctx.Guild.Id),
        BridgeError.NotBridged => Strings.BridgeNotBridged(ctx.Guild.Id),
        _ => Strings.BridgeLinkFailed(ctx.Guild.Id)
    };
}
