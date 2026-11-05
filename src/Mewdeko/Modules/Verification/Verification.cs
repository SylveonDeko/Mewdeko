using System.Text.RegularExpressions;
using DataModel;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Verification.Services;

namespace Mewdeko.Modules.Verification;

/// <summary>
///     Commands for making new members pass a captcha before they get access.
/// </summary>
/// <param name="guildSettings">The guild settings service, for the prefix.</param>
public class Verification(GuildSettingsService guildSettings) : MewdekoModuleBase<VerificationService>
{
    private static readonly Regex Snowflake = new(@"\d{15,21}", RegexOptions.Compiled);

    /// <summary>
    ///     Shows or changes the verification gate.
    /// </summary>
    /// <param name="option">
    ///     show, setup, enable, disable, channel, unverifiedrole, verifiedrole, attempts, timeout, kickonfail,
    ///     logchannel, panel, permissions or verifyexisting.
    /// </param>
    /// <param name="value">The value for the option.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task VerificationGate(string option = "show", [Remainder] string? value = null)
    {
        var guildId = ctx.Guild.Id;
        var prefix = $"{await guildSettings.GetPrefix(ctx.Guild)}verification";
        value = value?.Trim();
        var off = string.Equals(value, "off", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(value, "none", StringComparison.OrdinalIgnoreCase);

        switch (option.ToLowerInvariant())
        {
            case "show":
                await ctx.Channel.SendMessageAsync(
                    embed: Service.BuildSummary(guildId, await Service.GetSettingsAsync(guildId)).Build(),
                    allowedMentions: AllowedMentions.None);
                return;
            case "setup":
                await SetupAsync(value, prefix);
                return;
            case "enable":
            case "on":
                await ApplyAsync(x => x.Enabled = true, _ => Strings.VerificationEnabled(guildId, prefix));
                return;
            case "disable":
            case "off":
                await DisableAsync();
                return;
            case "panel":
                if (await Service.PostPanelAsync(guildId))
                    await ReplyConfirmAsync(Strings.VerificationPanelPosted(guildId));
                else
                    await ReplyErrorAsync(Strings.VerificationNoChannel(guildId, prefix));
                return;
            case "permissions":
                await PermissionsAsync(prefix);
                return;
            case "verifyexisting":
                await VerifyExistingAsync();
                return;
            case "kickonfail":
            {
                var on = value is not null && (value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
                                               value.Equals("true", StringComparison.OrdinalIgnoreCase));
                await ApplyAsync(x => x.KickOnFail = on,
                    _ => on ? Strings.VerificationKickOn(guildId) : Strings.VerificationKickOff(guildId));
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            await ReplyErrorAsync(Strings.VerificationMissingArgument(guildId, prefix));
            return;
        }

        switch (option.ToLowerInvariant())
        {
            case "channel":
            {
                var channel = ParseChannel(value);
                if (channel is null)
                {
                    await ReplyErrorAsync(Strings.VerificationMissingArgument(guildId, prefix));
                    return;
                }

                await ApplyAsync(x =>
                {
                    x.ChannelId = channel.Id;
                    x.MessageId = null;
                }, _ => Strings.VerificationChannelSet(guildId, channel.Mention));
                break;
            }
            case "logchannel":
            {
                if (off)
                {
                    await ApplyAsync(x => x.LogChannelId = null, _ => Strings.VerificationLogChannelOff(guildId));
                    return;
                }

                var channel = ParseChannel(value);
                if (channel is null)
                {
                    await ReplyErrorAsync(Strings.VerificationMissingArgument(guildId, prefix));
                    return;
                }

                await ApplyAsync(x => x.LogChannelId = channel.Id,
                    _ => Strings.VerificationLogChannelSet(guildId, channel.Mention));
                break;
            }
            case "unverifiedrole":
            case "verifiedrole":
            {
                var unverified = option.Equals("unverifiedrole", StringComparison.OrdinalIgnoreCase);
                var role = off ? null : ParseRole(value);
                if (!off && role is null)
                {
                    await ReplyErrorAsync(Strings.VerificationNoRole(guildId));
                    return;
                }

                await ApplyAsync(x =>
                {
                    if (unverified)
                        x.UnverifiedRoleId = role?.Id;
                    else
                        x.VerifiedRoleId = role?.Id;
                }, _ => role is null
                    ? Strings.VerificationRoleCleared(guildId)
                    : Strings.VerificationRoleSet(guildId, role.Mention));
                break;
            }
            case "attempts":
            {
                if (!int.TryParse(value, out var attempts))
                {
                    await ReplyErrorAsync(Strings.VerificationInvalidAttempts(guildId));
                    return;
                }

                await ApplyAsync(x => x.MaxAttempts = attempts, _ => Strings.VerificationAttemptsSet(guildId, attempts));
                break;
            }
            case "timeout":
            {
                if (!TryParseMinutes(value, out var minutes))
                {
                    await ReplyErrorAsync(Strings.VerificationInvalidTimeout(guildId));
                    return;
                }

                await ApplyAsync(x => x.TimeoutMinutes = minutes, _ => Strings.VerificationTimeoutSet(guildId, minutes));
                break;
            }
            default:
                await ReplyErrorAsync(Strings.VerificationMissingArgument(guildId, prefix));
                break;
        }
    }

    private async Task SetupAsync(string? value, string prefix)
    {
        var guildId = ctx.Guild.Id;
        ITextChannel? channel = null;
        if (!string.IsNullOrWhiteSpace(value))
        {
            channel = ParseChannel(value);
            if (channel is null)
            {
                await ReplyErrorAsync(Strings.VerificationMissingArgument(guildId, prefix));
                return;
            }
        }

        var result = await Service.SetupAsync(ctx.Guild, channel);
        if (result is null)
        {
            await ReplyErrorAsync(Strings.VerificationSetupFailed(guildId));
            return;
        }

        await ReplyConfirmAsync(Strings.VerificationSetupDone(guildId, result.Mention, prefix));
    }

    private async Task DisableAsync()
    {
        var guildId = ctx.Guild.Id;
        var (_, _, restored) = await Service.UpdateAsync(guildId, x => x.Enabled = false);
        await ReplyConfirmAsync(Strings.VerificationDisabled(guildId, restored));
    }

    private async Task PermissionsAsync(string prefix)
    {
        var guildId = ctx.Guild.Id;
        var plan = await Service.PlanPermissionsAsync(guildId);
        if (plan.Count == 0)
        {
            await ReplyErrorAsync(Strings.VerificationNothingToChange(guildId, prefix));
            return;
        }

        if (!await PromptUserConfirmAsync(new EmbedBuilder().WithDescription(Service.DescribePlan(guildId, plan)),
                ctx.User.Id))
            return;

        var (changed, failed) = await Service.ApplyPermissionsAsync(guildId);
        await ReplyConfirmAsync(Strings.VerificationPermissionsApplied(guildId, changed, failed));
    }

    private async Task VerifyExistingAsync()
    {
        var guildId = ctx.Guild.Id;
        var setting = await Service.GetSettingsAsync(guildId);
        if (setting.UnverifiedRoleId is null && setting.VerifiedRoleId is null)
        {
            await ReplyErrorAsync(Strings.VerificationMissingRoles(guildId));
            return;
        }

        var count = await Service.CountExistingAsync(guildId);
        if (count == 0)
        {
            await ReplyErrorAsync(Strings.VerificationNobodyToVerify(guildId));
            return;
        }

        if (!await PromptUserConfirmAsync(Strings.VerificationConfirmBulk(guildId, count), ctx.User.Id))
            return;

        var started = await Service.VerifyExistingAsync(guildId);
        await ReplyConfirmAsync(Strings.VerificationBulkStarted(guildId, started ?? 0));
    }

    private async Task ApplyAsync(Action<VerificationSetting> change, Func<VerificationSetting, string> success)
    {
        var guildId = ctx.Guild.Id;
        var (error, setting, _) = await Service.UpdateAsync(guildId, change);
        if (error == VerificationError.None)
            await ReplyConfirmAsync(success(setting));
        else
            await ReplyErrorAsync(Service.DescribeError(guildId, error));
    }

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

    private IRole? ParseRole(string input)
    {
        var match = Snowflake.Match(input);
        var role = match.Success && ulong.TryParse(match.Value, out var id)
            ? ctx.Guild.GetRole(id)
            : ctx.Guild.Roles.FirstOrDefault(x => x.Name.Equals(input, StringComparison.OrdinalIgnoreCase));
        return role is not null && role.Id != ctx.Guild.Id ? role : null;
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
