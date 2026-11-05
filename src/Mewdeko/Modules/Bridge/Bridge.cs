using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Bridge.Common;
using Mewdeko.Modules.Bridge.Services;

namespace Mewdeko.Modules.Bridge;

/// <summary>
///     Commands for bridging a channel to a channel on Fluxer.
/// </summary>
/// <param name="guildSettings">The guild settings service, for the prefix.</param>
public class Bridge(GuildSettingsService guildSettings) : MewdekoSubmodule<BridgeService>
{
    /// <summary>
    ///     Creates, links, lists, pauses, resumes, removes or configures a channel bridge.
    /// </summary>
    /// <param name="action">create, link, list, settings, pause, resume, remove, allowbots, direction, allowrole, blockrole, block, unblock, minage, invites, filter, nameformat, pair, pairs, channels, connect, proposals, accept, decline, share, hide, approval or unpair.</param>
    /// <param name="argument">The value for the action, such as the link code or the roles.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task BridgeCommand(string action = "list", [Remainder] string? argument = null)
    {
        var guildId = ctx.Guild.Id;
        var prefix = await guildSettings.GetPrefix(ctx.Guild);
        var bridge = Service.GetBridge(ctx.Channel.Id);

        if (bridge is null && action.ToLowerInvariant() is "settings" or "direction" or "allowrole" or "blockrole"
                or "block" or "unblock" or "minage" or "invites" or "filter" or "nameformat")
        {
            await ReplyErrorAsync(Strings.BridgeNotBridged(guildId));
            return;
        }

        switch (action.ToLowerInvariant())
        {
            case "create":
            {
                var (error, code, _) = await Service.CreateCodeAsync(ctx.Guild, (ITextChannel)ctx.Channel, ctx.User.Id);
                if (error != BridgeError.None)
                    await ReplyErrorAsync(ErrorText(error));
                else
                    await ReplyConfirmAsync(Strings.BridgeCodeCreated(guildId, code, prefix));
                break;
            }
            case "link":
            {
                if (string.IsNullOrWhiteSpace(argument))
                {
                    await ReplyErrorAsync(Strings.BridgeMissingArgument(guildId, prefix));
                    return;
                }

                var (error, linked) = await Service.LinkAsync(ctx.Guild, (ITextChannel)ctx.Channel, ctx.User.Id,
                    argument);
                if (error != BridgeError.None || linked is null)
                    await ReplyErrorAsync(ErrorText(error));
                else
                    await ReplyConfirmAsync(Strings.BridgeLinked(guildId, linked.RemoteChannelName,
                        linked.RemoteGuildName, "Fluxer"));
                break;
            }
            case "list":
            {
                var bridges = Service.GetBridges(guildId);
                if (bridges.Count == 0)
                {
                    await ReplyErrorAsync(Strings.BridgeListEmpty(guildId));
                    return;
                }

                var lines = bridges.Select(x => Strings.BridgeListItem(guildId, x.ChannelId, x.RemoteChannelName,
                    x.RemoteGuildName, "Fluxer", x.Paused ? Strings.BridgeListPaused(guildId) : ""));
                await ctx.Channel.SendConfirmAsync(Strings.BridgeListTitle(guildId), string.Join('\n', lines));
                break;
            }
            case "pause":
            case "resume":
            {
                if (bridge is null)
                {
                    await ReplyErrorAsync(Strings.BridgeNotBridged(guildId));
                    return;
                }

                var pause = action.Equals("pause", StringComparison.OrdinalIgnoreCase);
                await Service.SetPausedAsync(bridge, pause);
                await ReplyConfirmAsync(pause ? Strings.BridgePaused(guildId) : Strings.BridgeResumed(guildId));
                break;
            }
            case "remove":
            {
                if (bridge is null)
                {
                    await ReplyErrorAsync(Strings.BridgeNotBridged(guildId));
                    return;
                }

                await Service.RemoveAsync(bridge);
                await ReplyConfirmAsync(Strings.BridgeRemoved(guildId));
                break;
            }
            case "allowbots":
            {
                if (bridge is null)
                {
                    await ReplyErrorAsync(Strings.BridgeNotBridged(guildId));
                    return;
                }

                if (!bool.TryParse(argument, out var allow))
                {
                    await ReplyErrorAsync(Strings.BridgeMissingBool(guildId, prefix));
                    return;
                }

                await Service.SetAllowBotsAsync(bridge, allow);
                await ReplyConfirmAsync(allow
                    ? Strings.BridgeAllowbotsOn(guildId)
                    : Strings.BridgeAllowbotsOff(guildId));
                break;
            }
            case "settings":
                await ctx.Channel.SendConfirmAsync(Strings.BridgeSettingsTitle(guildId),
                    Service.DescribeOptions(bridge!));
                break;
            case "direction":
            {
                var direction = argument?.Trim().ToLowerInvariant() switch
                {
                    "both" => BridgeDirection.Both,
                    "send" => BridgeDirection.SendOnly,
                    "receive" => BridgeDirection.ReceiveOnly,
                    _ => (BridgeDirection?)null
                };
                if (direction is null)
                {
                    await ReplyErrorAsync(Strings.BridgeDirectionUsage(guildId, prefix));
                    return;
                }

                await Service.SetOptionsAsync(bridge!, bridge!.OptionsOf() with
                {
                    Direction = direction.Value
                });
                await ReplyConfirmAsync(Strings.BridgeDirectionSet(guildId,
                    Service.DirectionName(guildId, direction.Value)));
                break;
            }
            case "allowrole":
            case "blockrole":
            {
                var allow = action.Equals("allowrole", StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrWhiteSpace(argument))
                {
                    await ReplyErrorAsync(Strings.BridgeRolesUsage(guildId, prefix, action.ToLowerInvariant()));
                    return;
                }

                var clear = argument.Trim().Equals("none", StringComparison.OrdinalIgnoreCase);
                var roles = clear
                    ? []
                    : BridgeSettings.ParseRoleIds(argument, ctx.Guild.Roles.Select(x => (x.Id, x.Name)).ToList());
                if (!clear && roles.Count == 0)
                {
                    await ReplyErrorAsync(Strings.BridgeRolesUnknown(guildId));
                    return;
                }

                var options = bridge!.OptionsOf();
                await Service.SetOptionsAsync(bridge, allow
                    ? options with { AllowedRoleIds = roles }
                    : options with { BlockedRoleIds = roles });
                var list = string.Join(' ', roles.Select(x => $"<@&{x}>"));
                await ReplyConfirmAsync((allow, clear) switch
                {
                    (true, true) => Strings.BridgeAllowroleCleared(guildId),
                    (true, false) => Strings.BridgeAllowroleSet(guildId, list),
                    (false, true) => Strings.BridgeBlockroleCleared(guildId),
                    _ => Strings.BridgeBlockroleSet(guildId, list)
                });
                break;
            }
            case "block":
            case "unblock":
            {
                var block = action.Equals("block", StringComparison.OrdinalIgnoreCase);
                if (BridgeSettings.ParseUserId(argument) is not { } userId)
                {
                    await ReplyErrorAsync(Strings.BridgeBlockUsage(guildId, prefix, action.ToLowerInvariant()));
                    return;
                }

                var options = bridge!.OptionsOf();
                var blocked = options.BlockedUserIds.ToList();
                if (block == blocked.Contains(userId))
                {
                    await ReplyErrorAsync(block ? Strings.BridgeBlockExists(guildId) : Strings.BridgeBlockMissing(guildId));
                    return;
                }

                if (block)
                    blocked.Add(userId);
                else
                    blocked.Remove(userId);

                await Service.SetOptionsAsync(bridge, options with { BlockedUserIds = blocked });
                await ReplyConfirmAsync(block
                    ? Strings.BridgeBlockAdded(guildId, userId)
                    : Strings.BridgeBlockRemoved(guildId, userId));
                break;
            }
            case "minage":
            {
                if (!BridgeSettings.TryParseMinAge(argument, out var minutes))
                {
                    await ReplyErrorAsync(Strings.BridgeMinageUsage(guildId, prefix));
                    return;
                }

                await Service.SetOptionsAsync(bridge!, bridge!.OptionsOf() with
                {
                    MinMemberAgeMinutes = minutes
                });
                await ReplyConfirmAsync(minutes == 0
                    ? Strings.BridgeMinageOff(guildId)
                    : Strings.BridgeMinageSet(guildId, BridgeService.DescribeMinAge(minutes)));
                break;
            }
            case "invites":
            case "filter":
            {
                var invites = action.Equals("invites", StringComparison.OrdinalIgnoreCase);
                if (ParseToggle(argument) is not { } on)
                {
                    await ReplyErrorAsync(Strings.BridgeToggleUsage(guildId, prefix, action.ToLowerInvariant()));
                    return;
                }

                var options = bridge!.OptionsOf();
                await Service.SetOptionsAsync(bridge, invites
                    ? options with { BlockInvites = on }
                    : options with { UseWordFilter = on });
                await ReplyConfirmAsync((invites, on) switch
                {
                    (true, true) => Strings.BridgeInvitesOn(guildId),
                    (true, false) => Strings.BridgeInvitesOff(guildId),
                    (false, true) => Strings.BridgeFilterOn(guildId),
                    _ => Strings.BridgeFilterOff(guildId)
                });
                break;
            }
            case "nameformat":
            {
                var reset = argument?.Trim().Equals("reset", StringComparison.OrdinalIgnoreCase) == true;
                var format = reset ? BridgeSettings.DefaultNameFormat : argument?.Trim();
                if (!BridgeSettings.IsValidNameFormat(format))
                {
                    await ReplyErrorAsync(Strings.BridgeNameformatUsage(guildId, prefix, "{name}", "{platform}",
                        "{community}"));
                    return;
                }

                await Service.SetOptionsAsync(bridge!, bridge!.OptionsOf() with
                {
                    NameFormat = format!
                });
                await ReplyConfirmAsync(reset
                    ? Strings.BridgeNameformatReset(guildId)
                    : Strings.BridgeNameformatSet(guildId, format!));
                break;
            }
            case "pair":
            case "pairs":
            case "channels":
            case "connect":
            case "proposals":
            case "accept":
            case "decline":
            case "share":
            case "hide":
            case "approval":
            case "unpair":
            {
                var reply = await Service.Pairs.RunAsync(action, argument, guildId, ctx.User.Id, ctx.Channel.Id,
                    prefix);
                if (!reply.Success)
                    await ReplyErrorAsync(reply.Text);
                else if (reply.Title is null)
                    await ReplyConfirmAsync(reply.Text);
                else
                    await ctx.Channel.SendConfirmAsync(reply.Title, reply.Text);
                break;
            }
            default:
                await ReplyErrorAsync(Strings.BridgeUnknownAction(guildId, prefix));
                break;
        }
    }

    private static bool? ParseToggle(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "on" or "true" or "yes" => true,
        "off" or "false" or "no" => false,
        _ => null
    };

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
