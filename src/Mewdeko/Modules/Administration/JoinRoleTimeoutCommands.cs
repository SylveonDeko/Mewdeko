using System.Text.RegularExpressions;
using DataModel;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Commands for acting on members who still lack a required role some time after joining.
    /// </summary>
    /// <param name="guildSettings">The guild settings service, for the prefix.</param>
    [Group]
    public class JoinRoleTimeoutCommands(GuildSettingsService guildSettings)
        : MewdekoSubmodule<JoinRoleTimeoutService>
    {
        private static readonly Regex Snowflake = new(@"\d{15,21}", RegexOptions.Compiled);

        /// <summary>
        ///     Shows or changes the join role timeout.
        /// </summary>
        /// <param name="option">
        ///     show, enable, disable, timeout, required, exempt, action, warn, logchannel, existing, warnmessage,
        ///     actionmessage or pending.
        /// </param>
        /// <param name="value">The value for the option.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task JoinRoleTimeout(string option = "show", [Remainder] string? value = null)
        {
            var guildId = ctx.Guild.Id;
            var prefix = await guildSettings.GetPrefix(ctx.Guild);
            value = value?.Trim();

            switch (option.ToLowerInvariant())
            {
                case "show":
                    await ShowAsync();
                    return;
                case "pending":
                    await PendingAsync();
                    return;
                case "enable":
                case "on":
                    await ApplyAsync(x => x.Enabled = true, setting => Strings.JrtEnabled(guildId,
                        JoinRoleTimeoutService.FormatMinutes(setting.TimeoutMinutes)), prefix);
                    return;
                case "disable":
                case "off":
                    await ApplyAsync(x => x.Enabled = false, _ => Strings.JrtDisabled(guildId), prefix);
                    return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                await ReplyErrorAsync(Strings.JrtMissingArgument(guildId, prefix));
                return;
            }

            switch (option.ToLowerInvariant())
            {
                case "timeout":
                {
                    if (!TryParseMinutes(value, out var minutes))
                    {
                        await ReplyErrorAsync(Strings.JrtInvalidTime(guildId));
                        return;
                    }

                    await ApplyAsync(x => x.TimeoutMinutes = minutes,
                        _ => Strings.JrtTimeoutSet(guildId, JoinRoleTimeoutService.FormatMinutes(minutes)), prefix);
                    break;
                }
                case "required":
                case "exempt":
                {
                    var required = option.Equals("required", StringComparison.OrdinalIgnoreCase);
                    if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
                    {
                        await ApplyAsync(x =>
                        {
                            if (required)
                                x.RequiredRoleIds = "";
                            else
                                x.ExemptRoleIds = "";
                        }, _ => required ? Strings.JrtRolesRequiredCleared(guildId) : Strings.JrtRolesExemptCleared(guildId),
                            prefix);
                        return;
                    }

                    var roles = ParseRoles(value);
                    if (roles.Count == 0)
                    {
                        await ReplyErrorAsync(Strings.JrtNoRolesFound(guildId));
                        return;
                    }

                    var ids = JoinRoleTimeoutService.JoinIds(roles.Select(x => x.Id));
                    var mentions = string.Join(' ', roles.Select(x => x.Mention));
                    await ApplyAsync(x =>
                    {
                        if (required)
                            x.RequiredRoleIds = ids;
                        else
                            x.ExemptRoleIds = ids;
                    }, _ => required
                        ? Strings.JrtRolesRequiredSet(guildId, mentions)
                        : Strings.JrtRolesExemptSet(guildId, mentions), prefix);
                    break;
                }
                case "action":
                    await SetActionAsync(value, prefix);
                    break;
                case "warn":
                {
                    if (value.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                        value.Equals("none", StringComparison.OrdinalIgnoreCase))
                    {
                        await ApplyAsync(x => x.WarnBeforeMinutes = null, _ => Strings.JrtWarnOff(guildId), prefix);
                        return;
                    }

                    if (!TryParseMinutes(value, out var minutes))
                    {
                        await ReplyErrorAsync(Strings.JrtInvalidTime(guildId));
                        return;
                    }

                    await ApplyAsync(x => x.WarnBeforeMinutes = minutes,
                        _ => Strings.JrtWarnSet(guildId, JoinRoleTimeoutService.FormatMinutes(minutes)), prefix);
                    break;
                }
                case "logchannel":
                {
                    if (value.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                        value.Equals("none", StringComparison.OrdinalIgnoreCase))
                    {
                        await ApplyAsync(x => x.LogChannelId = null, _ => Strings.JrtLogChannelOff(guildId), prefix);
                        return;
                    }

                    var channel = ParseChannel(value);
                    if (channel is null)
                    {
                        await ReplyErrorAsync(Strings.JrtMissingArgument(guildId, prefix));
                        return;
                    }

                    await ApplyAsync(x => x.LogChannelId = channel.Id,
                        _ => Strings.JrtLogChannelSet(guildId, channel.Mention), prefix);
                    break;
                }
                case "existing":
                {
                    var on = value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
                             value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    await ApplyAsync(x => x.IncludeExistingMembers = on,
                        _ => on ? Strings.JrtExistingOn(guildId) : Strings.JrtExistingOff(guildId), prefix);
                    break;
                }
                case "warnmessage":
                {
                    var reset = value.Equals("default", StringComparison.OrdinalIgnoreCase);
                    await ApplyAsync(x => x.WarnMessage = reset ? null : value,
                        _ => reset ? Strings.JrtMessageWarnReset(guildId) : Strings.JrtMessageWarnSet(guildId), prefix);
                    break;
                }
                case "actionmessage":
                {
                    var reset = value.Equals("default", StringComparison.OrdinalIgnoreCase);
                    await ApplyAsync(x => x.ActionMessage = reset ? null : value,
                        _ => reset ? Strings.JrtMessageActionReset(guildId) : Strings.JrtMessageActionSet(guildId),
                        prefix);
                    break;
                }
                default:
                    await ReplyErrorAsync(Strings.JrtMissingArgument(guildId, prefix));
                    break;
            }
        }

        private async Task SetActionAsync(string value, string prefix)
        {
            var guildId = ctx.Guild.Id;
            var parts = value.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var permissions = (await ctx.Guild.GetCurrentUserAsync()).GuildPermissions;

            JoinRoleTimeoutAction action;
            string missing;
            switch (parts[0].ToLowerInvariant())
            {
                case "kick":
                    action = JoinRoleTimeoutAction.Kick;
                    missing = permissions.KickMembers ? "" : "Kick Members";
                    break;
                case "ban":
                    action = JoinRoleTimeoutAction.Ban;
                    missing = permissions.BanMembers ? "" : "Ban Members";
                    break;
                case "addrole":
                    action = JoinRoleTimeoutAction.AddRole;
                    missing = permissions.ManageRoles ? "" : "Manage Roles";
                    break;
                case "removeroles":
                    action = JoinRoleTimeoutAction.RemoveRoles;
                    missing = permissions.ManageRoles ? "" : "Manage Roles";
                    break;
                case "timeout":
                case "mute":
                    action = JoinRoleTimeoutAction.Timeout;
                    missing = permissions.ModerateMembers ? "" : "Moderate Members";
                    break;
                default:
                    await ReplyErrorAsync(Strings.JrtActionUnknown(guildId));
                    return;
            }

            if (missing.Length > 0)
            {
                await ReplyErrorAsync(Strings.JrtNeedPermission(guildId, missing));
                return;
            }

            ulong? roleId = null;
            if (action == JoinRoleTimeoutAction.AddRole)
            {
                var role = parts.Length > 1 ? ParseRoles(parts[1]).FirstOrDefault() : null;
                if (role is null)
                {
                    await ReplyErrorAsync(Strings.JrtMissingActionRole(guildId, prefix));
                    return;
                }

                roleId = role.Id;
            }

            await ApplyAsync(x =>
            {
                x.Action = action;
                x.ActionRoleId = roleId;
            }, _ => Strings.JrtActionSet(guildId, ActionName(action)), prefix);
        }

        private async Task ApplyAsync(Action<JoinRoleTimeoutSetting> change,
            Func<JoinRoleTimeoutSetting, string> success, string prefix)
        {
            var guildId = ctx.Guild.Id;
            var (error, setting) = await Service.UpdateAsync(guildId, change);
            switch (error)
            {
                case JoinRoleTimeoutError.None:
                    await ReplyConfirmAsync(success(setting));
                    break;
                case JoinRoleTimeoutError.InvalidTimeout:
                    await ReplyErrorAsync(Strings.JrtInvalidTimeout(guildId));
                    break;
                case JoinRoleTimeoutError.InvalidWarn:
                    await ReplyErrorAsync(Strings.JrtInvalidWarn(guildId));
                    break;
                case JoinRoleTimeoutError.MissingActionRole:
                    await ReplyErrorAsync(Strings.JrtMissingActionRole(guildId, prefix));
                    break;
                case JoinRoleTimeoutError.MissingRequiredRoles:
                    await ReplyErrorAsync(Strings.JrtMissingRequired(guildId, prefix));
                    break;
            }
        }

        private async Task ShowAsync()
        {
            var guildId = ctx.Guild.Id;
            var setting = await Service.GetSettingsAsync(guildId);
            var (count, _) = await Service.GetPendingAsync(guildId, 1);
            var none = Strings.JrtNone(guildId);

            var required = JoinRoleTimeoutService.ParseIds(setting.RequiredRoleIds).Select(x => $"<@&{x}>").ToList();
            var exempt = JoinRoleTimeoutService.ParseIds(setting.ExemptRoleIds).Select(x => $"<@&{x}>").ToList();
            var action = ActionName(setting.Action);
            if (setting is { Action: JoinRoleTimeoutAction.AddRole, ActionRoleId: { } roleId })
                action = $"{action} (<@&{roleId}>)";

            await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.JrtShowTitle(guildId))
                .WithDescription(Strings.JrtShowBody(guildId,
                    OnOff(setting.Enabled),
                    JoinRoleTimeoutService.FormatMinutes(setting.TimeoutMinutes),
                    required.Count == 0 ? none : string.Join(' ', required),
                    exempt.Count == 0 ? none : string.Join(' ', exempt),
                    action,
                    setting.WarnBeforeMinutes is { } warn ? JoinRoleTimeoutService.FormatMinutes(warn) : none,
                    setting.LogChannelId is { } log ? $"<#{log}>" : none,
                    OnOff(setting.IncludeExistingMembers),
                    count))
                .Build(), allowedMentions: AllowedMentions.None);
        }

        private async Task PendingAsync()
        {
            var guildId = ctx.Guild.Id;
            var setting = await Service.GetSettingsAsync(guildId);
            var (count, next) = await Service.GetPendingAsync(guildId, 10);
            if (count == 0)
            {
                await ReplyErrorAsync(Strings.JrtPendingEmpty(guildId));
                return;
            }

            var lines = next.Select(x =>
            {
                var left = x.JoinedAt.AddMinutes(setting.TimeoutMinutes) - DateTime.UtcNow;
                var minutes = left <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(left.TotalMinutes);
                return Strings.JrtPendingEntry(guildId, x.UserId, JoinRoleTimeoutService.FormatMinutes(minutes));
            });

            await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.JrtPendingTitle(guildId, count))
                .WithDescription(string.Join('\n', lines))
                .Build(), allowedMentions: AllowedMentions.None);
        }

        private string OnOff(bool value) => value ? Strings.JrtOn(ctx.Guild.Id) : Strings.JrtOff(ctx.Guild.Id);

        private string ActionName(JoinRoleTimeoutAction action) => action switch
        {
            JoinRoleTimeoutAction.Kick => Strings.JrtActionKick(ctx.Guild.Id),
            JoinRoleTimeoutAction.Ban => Strings.JrtActionBan(ctx.Guild.Id),
            JoinRoleTimeoutAction.AddRole => Strings.JrtActionAddrole(ctx.Guild.Id),
            JoinRoleTimeoutAction.RemoveRoles => Strings.JrtActionRemoveroles(ctx.Guild.Id),
            _ => Strings.JrtActionTimeout(ctx.Guild.Id)
        };

        private static bool TryParseMinutes(string input, out int minutes)
        {
            minutes = 0;
            try
            {
                minutes = (int)Math.Ceiling(StoopidTime.FromInput(input.Replace(" ", "").ToLowerInvariant()).Time
                    .TotalMinutes);
                return minutes > 0;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private List<IRole> ParseRoles(string input)
        {
            var tokens = input.Contains(',')
                ? input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var roles = new List<IRole>();
            foreach (var token in tokens)
            {
                var match = Snowflake.Match(token);
                var role = match.Success && ulong.TryParse(match.Value, out var id)
                    ? ctx.Guild.GetRole(id)
                    : ctx.Guild.Roles.FirstOrDefault(x => x.Name.Equals(token, StringComparison.OrdinalIgnoreCase));
                if (role is not null && role.Id != ctx.Guild.Id && roles.All(x => x.Id != role.Id))
                    roles.Add(role);
            }

            return roles;
        }

        private ITextChannel? ParseChannel(string input)
        {
            if (ctx.Guild is not SocketGuild guild)
                return null;

            var match = Snowflake.Match(input);
            if (match.Success && ulong.TryParse(match.Value, out var id))
                return guild.GetTextChannel(id);

            return guild.TextChannels
                .FirstOrDefault(x => x.Name.Equals(input.TrimStart('#'), StringComparison.OrdinalIgnoreCase));
        }
    }
}
