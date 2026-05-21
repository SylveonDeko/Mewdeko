using DataModel;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Counting.Services;

namespace Mewdeko.Modules.Counting;

/// <summary>
///     Commands for configuring counting moderation settings.
/// </summary>
public class CountingModeration : MewdekoModuleBase<CountingModerationService>
{
    /// <summary>
    ///     Shows the current moderation configuration for the counting channel or guild defaults.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CountmodShow(ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var config = await Service.GetModerationConfigAsync(channel.Id);
        var guildDefaults = await Service.GetGuildDefaultsAsync(ctx.Guild.Id);

        var embed = new EmbedBuilder()
            .WithTitle(Strings.CountingModerationShowTitle(ctx.Guild.Id, channel.Name))
            .WithColor(Mewdeko.OkColor);

        if (config == null)
        {
            embed.WithDescription(Strings.CountingModerationShowNoConfig(ctx.Guild.Id));
        }
        else
        {
            embed.AddField("Enabled", config.EnableModeration ? "✅ Yes" : "❌ No", true)
                .AddField("Wrong Count Threshold", config.WrongCountThreshold.ToString(), true)
                .AddField("Time Window", $"{config.TimeWindowHours} hours", true)
                .AddField("Punishment", config.PunishmentAction.ToString(), true)
                .AddField("Duration",
                    config.PunishmentDurationMinutes > 0 ? $"{config.PunishmentDurationMinutes} minutes" : "Permanent",
                    true)
                .AddField("Delete Ignored Messages", config.DeleteIgnoredMessages ? "✅ Yes" : "❌ No", true);

            if (config.IgnoreRoles.Count != 0)
                embed.AddField("Ignore Roles", string.Join(", ", config.IgnoreRoles.Select(r => $"<@&{r}>")));
            if (config.RequiredRoles.Count != 0)
                embed.AddField("Required Roles", string.Join(", ", config.RequiredRoles.Select(r => $"<@&{r}>")));
            if (config.BannedRoles.Count != 0)
                embed.AddField("Banned Roles", string.Join(", ", config.BannedRoles.Select(r => $"<@&{r}>")));
        }

        if (guildDefaults != null)
        {
            embed.AddField("Guild Defaults",
                $"Enabled: {(guildDefaults.EnableModeration ? "✅" : "❌")} | " +
                $"Threshold: {guildDefaults.WrongCountThreshold} | " +
                $"Window: {guildDefaults.TimeWindowHours}h | " +
                $"Punishment: {(PunishmentAction)guildDefaults.PunishmentAction}");
        }

        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Sets guild-wide default moderation settings.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task CountmodDefaults(bool enabled, int threshold = 3, int windowHours = 24,
        PunishmentAction punishment = PunishmentAction.Mute, int durationMinutes = 0, IRole? punishmentRole = null)
    {
        var defaults = new CountingModerationDefaults
        {
            GuildId = ctx.Guild.Id,
            EnableModeration = enabled,
            WrongCountThreshold = threshold,
            TimeWindowHours = windowHours,
            PunishmentAction = (int)punishment,
            PunishmentDurationMinutes = durationMinutes,
            PunishmentRoleId = punishmentRole?.Id
        };

        var success = await Service.SetGuildDefaultsAsync(ctx.Guild.Id, defaults);

        if (success)
        {
            var embed = new EmbedBuilder()
                .WithTitle(Strings.CountingModerationGuildDefaultsTitle(ctx.Guild.Id))
                .WithDescription(Strings.CountingModerationGuildDefaultsDesc(ctx.Guild.Id, ctx.Guild.Name))
                .WithColor(Mewdeko.OkColor)
                .AddField("Enabled", enabled ? "✅ Yes" : "❌ No", true)
                .AddField("Wrong Count Threshold", threshold.ToString(), true)
                .AddField("Time Window", $"{windowHours} hours", true)
                .AddField("Punishment", punishment.ToString(), true)
                .AddField("Duration", durationMinutes > 0 ? $"{durationMinutes} minutes" : "Permanent", true);

            if (punishmentRole != null)
                embed.AddField("Punishment Role", punishmentRole.Mention, true);

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }
        else
        {
            await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Enables or disables moderation for a specific counting channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CountmodEnable(bool enabled, ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var config = new CountingModerationConfig
        {
            ChannelId = channel.Id, UseDefaults = false, EnableModeration = enabled
        };

        var success = await Service.SetChannelConfigAsync(channel.Id, config);

        if (success)
        {
            await ConfirmAsync(Strings.CountingModerationEnabled(ctx.Guild.Id, enabled ? "enabled" : "disabled",
                channel.Mention)).ConfigureAwait(false);
        }
        else
        {
            await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets the wrong count threshold for a counting channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CountmodThreshold(int threshold, ITextChannel? channel = null)
    {
        if (threshold is < 1 or > 100)
        {
            await ErrorAsync(Strings.CountingModerationThresholdInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        channel ??= (ITextChannel)ctx.Channel;

        var config = new CountingModerationConfig
        {
            ChannelId = channel.Id, UseDefaults = false, WrongCountThreshold = threshold
        };

        var success = await Service.SetChannelConfigAsync(channel.Id, config);

        if (success)
        {
            await ConfirmAsync(Strings.CountingModerationThresholdSet(ctx.Guild.Id, threshold, channel.Mention))
                .ConfigureAwait(false);
        }
        else
        {
            await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets the time window for tracking wrong counts.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CountmodWindow(int hours, ITextChannel? channel = null)
    {
        if (hours is < 1 or > 168) // Max 1 week
        {
            await ErrorAsync(Strings.CountingModerationWindowInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        channel ??= (ITextChannel)ctx.Channel;

        var config = new CountingModerationConfig
        {
            ChannelId = channel.Id, UseDefaults = false, TimeWindowHours = hours
        };

        var success = await Service.SetChannelConfigAsync(channel.Id, config);

        if (success)
        {
            await ConfirmAsync(Strings.CountingModerationWindowSet(ctx.Guild.Id, hours, channel.Mention))
                .ConfigureAwait(false);
        }
        else
        {
            await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets the punishment for wrong counts.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CountmodPunishment(PunishmentAction punishment, int durationMinutes = 0,
        IRole? role = null, ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        if (punishment == PunishmentAction.AddRole && role == null)
        {
            await ErrorAsync(Strings.CountingModerationRoleRequired(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var config = new CountingModerationConfig
        {
            ChannelId = channel.Id,
            UseDefaults = false,
            PunishmentAction = (int)punishment,
            PunishmentDurationMinutes = durationMinutes,
            PunishmentRoleId = role?.Id
        };

        var success = await Service.SetChannelConfigAsync(channel.Id, config);

        if (success)
        {
            var embed = new EmbedBuilder()
                .WithTitle(Strings.CountingModerationPunishmentUpdatedTitle(ctx.Guild.Id))
                .WithDescription(Strings.CountingModerationPunishmentUpdatedDesc(ctx.Guild.Id, channel.Mention))
                .WithColor(Mewdeko.OkColor)
                .AddField("Punishment", punishment.ToString(), true)
                .AddField("Duration", durationMinutes > 0 ? $"{durationMinutes} minutes" : "Permanent", true);

            if (role != null)
                embed.AddField("Role", role.Mention, true);

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }
        else
        {
            await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets roles to ignore from counting.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CountmodIgnore(bool deleteMessages, ITextChannel? channel = null, params IRole[] roles)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var roleIds = string.Join(",", roles.Select(r => r.Id));

        var config = new CountingModerationConfig
        {
            ChannelId = channel.Id, UseDefaults = false, IgnoreRoles = roleIds, DeleteIgnoredMessages = deleteMessages
        };

        var success = await Service.SetChannelConfigAsync(channel.Id, config);

        if (success)
        {
            var embed = new EmbedBuilder()
                .WithTitle(Strings.CountingModerationIgnoreRolesUpdatedTitle(ctx.Guild.Id))
                .WithDescription(Strings.CountingModerationIgnoreRolesUpdatedDesc(ctx.Guild.Id, channel.Mention))
                .WithColor(Mewdeko.OkColor)
                .AddField("Delete Messages", deleteMessages ? "✅ Yes" : "❌ No", true);

            embed.AddField("Ignored Roles", roles.Any() ? string.Join(", ", roles.Select(r => r.Mention)) : "None");

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }
        else
        {
            await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets roles required for counting.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CountmodRequired(ITextChannel? channel = null, params IRole[] roles)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var roleIds = string.Join(",", roles.Select(r => r.Id));

        var config = new CountingModerationConfig
        {
            ChannelId = channel.Id, UseDefaults = false, RequiredRoles = roleIds
        };

        var success = await Service.SetChannelConfigAsync(channel.Id, config);

        if (success)
        {
            var embed = new EmbedBuilder()
                .WithTitle(Strings.CountingModerationRequiredRolesUpdatedTitle(ctx.Guild.Id))
                .WithDescription(Strings.CountingModerationRequiredRolesUpdatedDesc(ctx.Guild.Id, channel.Mention))
                .WithColor(Mewdeko.OkColor);

            if (roles.Any())
                embed.AddField("Required Roles", string.Join(", ", roles.Select(r => r.Mention)));
            else
                embed.AddField("Required Roles", "None (everyone can count)");

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }
        else
        {
            await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets roles banned from counting.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CountmodBanned(ITextChannel? channel = null, params IRole[] roles)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var roleIds = string.Join(",", roles.Select(r => r.Id));

        var config = new CountingModerationConfig
        {
            ChannelId = channel.Id, UseDefaults = false, BannedRoles = roleIds
        };

        var success = await Service.SetChannelConfigAsync(channel.Id, config);

        if (success)
        {
            var embed = new EmbedBuilder()
                .WithTitle(Strings.CountingModerationBannedRolesUpdatedTitle(ctx.Guild.Id))
                .WithDescription(Strings.CountingModerationBannedRolesUpdatedDesc(ctx.Guild.Id, channel.Mention))
                .WithColor(Mewdeko.OkColor);

            if (roles.Any())
                embed.AddField("Banned Roles", string.Join(", ", roles.Select(r => r.Mention)));
            else
                embed.AddField("Banned Roles", "None");

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }
        else
        {
            await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Resets a channel to use guild defaults.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CountmodReset(ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var config = new CountingModerationConfig
        {
            ChannelId = channel.Id, UseDefaults = true
        };

        var success = await Service.SetChannelConfigAsync(channel.Id, config);

        if (success)
        {
            await ConfirmAsync(Strings.CountingModerationReset(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
        }
        else
        {
            await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sets a tiered punishment for a specific wrong count number.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CountmodPunish(int count, PunishmentAction punishment, int durationMinutes = 0,
        IRole? role = null, ITextChannel? channel = null)
    {
        if (punishment == PunishmentAction.AddRole && role == null)
        {
            await ErrorAsync(Strings.CountingModerationRoleRequired(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var success = await Service.SetTieredPunishmentAsync(ctx.Guild.Id, channel?.Id, count, punishment,
            durationMinutes, role?.Id);

        if (success)
        {
            var embed = new EmbedBuilder()
                .WithTitle(Strings.CountingModerationPunishmentSetTitle(ctx.Guild.Id))
                .WithDescription(Strings.CountingModerationPunishmentSetDesc(ctx.Guild.Id, count,
                    channel?.Mention ?? Strings.CountingModerationGuildDefaults(ctx.Guild.Id)))
                .WithColor(Mewdeko.OkColor)
                .AddField(Strings.CountingModerationWrongCountField(ctx.Guild.Id), count.ToString(), true)
                .AddField(Strings.CountingModerationPunishmentField(ctx.Guild.Id), punishment.ToString(), true)
                .AddField(Strings.CountingModerationDurationField(ctx.Guild.Id),
                    durationMinutes > 0
                        ? Strings.CountingModerationDurationMinutes(ctx.Guild.Id, durationMinutes)
                        : Strings.CountingModerationPermanent(ctx.Guild.Id), true);

            if (role != null)
                embed.AddField(Strings.CountingModerationRoleField(ctx.Guild.Id), role.Mention, true);

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }
        else
        {
            await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Removes a tiered punishment for a specific wrong count number.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CountmodUnpunish(int count, ITextChannel? channel = null)
    {
        var success = await Service.RemoveTieredPunishmentAsync(ctx.Guild.Id, channel?.Id, count);

        if (success)
        {
            await ConfirmAsync(Strings.CountingModerationPunishmentRemoved(ctx.Guild.Id, count,
                channel?.Mention ?? "guild defaults")).ConfigureAwait(false);
        }
        else
        {
            await ErrorAsync(Strings.CountingModerationPunishmentNotFound(ctx.Guild.Id, count)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Lists all tiered punishments for the guild or specific channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CountmodPunishList(ITextChannel? channel = null)
    {
        var punishments = await Service.GetTieredPunishmentsAsync(ctx.Guild.Id, channel?.Id);

        if (punishments.Count == 0)
        {
            await ErrorAsync(Strings.CountingModerationNoPunishments(ctx.Guild.Id,
                channel?.Mention ?? "guild defaults")).ConfigureAwait(false);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(Strings.CountingModerationPunishmentListTitle(ctx.Guild.Id,
                channel?.Name ?? Strings.CountingModerationGuildDefaults(ctx.Guild.Id)))
            .WithColor(Mewdeko.OkColor);

        foreach (var punishment in punishments.Take(10))
        {
            var duration = punishment.Time > 0
                ? Strings.CountingModerationDurationMinutes(ctx.Guild.Id, punishment.Time)
                : Strings.CountingModerationPermanent(ctx.Guild.Id);
            var role = punishment.RoleId.HasValue ? $" <@&{punishment.RoleId}>" : "";

            embed.AddField(Strings.CountingModerationWrongCountNumber(ctx.Guild.Id, punishment.Count),
                $"{(PunishmentAction)punishment.Punishment} {duration}{role}", true);
        }

        if (punishments.Count > 10)
            embed.WithFooter(Strings.CountingModerationMorePunishments(ctx.Guild.Id, punishments.Count - 10));

        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Clears wrong counts for a user in a counting channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task CountmodClear(IGuildUser user, ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var success = await Service.ClearUserWrongCountsAsync(channel.Id, user.Id);

        if (success)
        {
            await ConfirmAsync(
                    Strings.CountingModerationWrongCountsCleared(ctx.Guild.Id, user.Mention, channel.Mention))
                .ConfigureAwait(false);
        }
        else
        {
            await ErrorAsync(Strings.CountingModerationNoWrongCounts(ctx.Guild.Id, user.Mention, channel.Mention))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Clears wrong counts for all users in a counting channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageMessages)]
    public async Task CountmodClearAll(ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var success = await Service.ClearChannelWrongCountsAsync(channel.Id);

        if (success)
        {
            await ConfirmAsync(Strings.CountingModerationAllWrongCountsCleared(ctx.Guild.Id, channel.Mention))
                .ConfigureAwait(false);
        }
        else
        {
            await ErrorAsync(Strings.CountingModerationNoWrongCountsInChannel(ctx.Guild.Id, channel.Mention))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Configures non-number message handling for a counting channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CountmodNonnumbers(bool punish, bool delete = true, ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var config = new CountingModerationConfig
        {
            ChannelId = channel.Id, UseDefaults = false, PunishNonNumbers = punish, DeleteNonNumbers = delete
        };

        var success = await Service.SetChannelConfigAsync(channel.Id, config);

        if (success)
        {
            var embed = new EmbedBuilder()
                .WithTitle(Strings.CountingModerationNonNumberSettingsTitle(ctx.Guild.Id))
                .WithDescription(Strings.CountingModerationNonNumberSettingsDesc(ctx.Guild.Id, channel.Mention))
                .WithColor(Mewdeko.OkColor)
                .AddField(Strings.CountingModerationPunishNonNumbersField(ctx.Guild.Id), punish ? "✅" : "❌", true)
                .AddField(Strings.CountingModerationDeleteNonNumbersField(ctx.Guild.Id), delete ? "✅" : "❌", true);

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }
        else
        {
            await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Configures message edit protection for a counting channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CountmodEdits(bool punish, bool delete = true, ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var config = new CountingModerationConfig
        {
            ChannelId = channel.Id, UseDefaults = false, PunishEdits = punish, DeleteEdits = delete
        };

        var success = await Service.SetChannelConfigAsync(channel.Id, config);

        if (success)
        {
            var embed = new EmbedBuilder()
                .WithTitle(Strings.CountingModerationEditSettingsTitle(ctx.Guild.Id))
                .WithDescription(Strings.CountingModerationEditSettingsDesc(ctx.Guild.Id, channel.Mention))
                .WithColor(Mewdeko.OkColor)
                .AddField(Strings.CountingModerationPunishEditsField(ctx.Guild.Id), punish ? "✅" : "❌", true)
                .AddField(Strings.CountingModerationDeleteEditsField(ctx.Guild.Id), delete ? "✅" : "❌", true);

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }
        else
        {
            await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }
}