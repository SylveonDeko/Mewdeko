using DataModel;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Counting.Services;

namespace Mewdeko.Modules.Counting;

public partial class SlashCounting
{
    /// <summary>
    ///     Slash commands for configuring counting moderation settings.
    /// </summary>
    [Group("moderation", "Configure counting moderation settings")]
    public class SlashCountingModeration : MewdekoSlashSubmodule<CountingModerationService>
    {
        private string YesNo(bool value)
        {
            return value ? Strings.Yes(ctx.Guild.Id) : Strings.No(ctx.Guild.Id);
        }

        /// <summary>
        ///     Shows the current moderation configuration for the counting channel or guild defaults.
        /// </summary>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        [SlashCommand("show", "Show the moderation configuration for a counting channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CountmodShow(
            [Summary("channel", "Counting channel, defaults to current")]
            ITextChannel? channel = null)
        {
            await DeferAsync();
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
                embed.AddField("Enabled", YesNo(config.EnableModeration), true)
                    .AddField("Wrong Count Threshold", config.WrongCountThreshold.ToString(), true)
                    .AddField("Time Window", $"{config.TimeWindowHours} hours", true)
                    .AddField("Punishment", config.PunishmentAction.ToString(), true)
                    .AddField("Duration",
                        config.PunishmentDurationMinutes > 0
                            ? $"{config.PunishmentDurationMinutes} minutes"
                            : "Permanent",
                        true)
                    .AddField("Delete Ignored Messages", YesNo(config.DeleteIgnoredMessages), true);

                if (config.IgnoreRoles.Count != 0)
                    embed.AddField("Ignore Roles", string.Join(", ", config.IgnoreRoles.Select(r => $"<@&{r}>")));
                if (config.RequiredRoles.Count != 0)
                    embed.AddField("Required Roles",
                        string.Join(", ", config.RequiredRoles.Select(r => $"<@&{r}>")));
                if (config.BannedRoles.Count != 0)
                    embed.AddField("Banned Roles", string.Join(", ", config.BannedRoles.Select(r => $"<@&{r}>")));
            }

            if (guildDefaults != null)
            {
                embed.AddField("Guild Defaults",
                    $"Enabled: {YesNo(guildDefaults.EnableModeration)} | " +
                    $"Threshold: {guildDefaults.WrongCountThreshold} | " +
                    $"Window: {guildDefaults.TimeWindowHours}h | " +
                    $"Punishment: {(PunishmentAction)guildDefaults.PunishmentAction}");
            }

            await ctx.Interaction.FollowupAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Sets guild-wide default moderation settings.
        /// </summary>
        /// <param name="enabled">Whether moderation is enabled by default.</param>
        /// <param name="threshold">Wrong count threshold before punishment.</param>
        /// <param name="windowHours">Time window in hours for tracking wrong counts.</param>
        /// <param name="punishment">Punishment action to apply.</param>
        /// <param name="durationMinutes">Punishment duration in minutes, 0 for permanent.</param>
        /// <param name="punishmentRole">Role to add when the punishment is AddRole.</param>
        [SlashCommand("defaults", "Set guild-wide default moderation settings")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task CountmodDefaults(
            [Summary("enabled", "Whether moderation is enabled by default")]
            bool enabled,
            [Summary("threshold", "Wrong count threshold before punishment")]
            int threshold = 3,
            [Summary("window-hours", "Time window in hours for tracking wrong counts")]
            int windowHours = 24,
            [Summary("punishment", "Punishment action to apply")]
            PunishmentAction punishment = PunishmentAction.Mute,
            [Summary("duration-minutes", "Punishment duration in minutes, 0 for permanent")]
            int durationMinutes = 0,
            [Summary("punishment-role", "Role to add when the punishment is AddRole")]
            IRole? punishmentRole = null)
        {
            await DeferAsync();
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
                    .AddField("Enabled", YesNo(enabled), true)
                    .AddField("Wrong Count Threshold", threshold.ToString(), true)
                    .AddField("Time Window", $"{windowHours} hours", true)
                    .AddField("Punishment", punishment.ToString(), true)
                    .AddField("Duration", durationMinutes > 0 ? $"{durationMinutes} minutes" : "Permanent", true);

                if (punishmentRole != null)
                    embed.AddField("Punishment Role", punishmentRole.Mention, true);

                await ctx.Interaction.FollowupAsync(embed: embed.Build());
            }
            else
            {
                await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Enables or disables moderation for a specific counting channel.
        /// </summary>
        /// <param name="enabled">Whether moderation is enabled.</param>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        [SlashCommand("enable", "Enable or disable moderation for a counting channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CountmodEnable(
            [Summary("enabled", "Whether moderation is enabled")]
            bool enabled,
            [Summary("channel", "Counting channel, defaults to current")]
            ITextChannel? channel = null)
        {
            channel ??= (ITextChannel)ctx.Channel;

            var config = new CountingModerationConfig
            {
                ChannelId = channel.Id, UseDefaults = false, EnableModeration = enabled
            };

            var success = await Service.SetChannelConfigAsync(channel.Id, config);

            if (success)
            {
                await ConfirmAsync(Strings.CountingModerationEnabled(ctx.Guild.Id,
                    enabled ? "enabled" : "disabled",
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
        /// <param name="threshold">Wrong count threshold between 1 and 100.</param>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        [SlashCommand("threshold", "Set the wrong count threshold for a counting channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CountmodThreshold(
            [Summary("threshold", "Wrong count threshold between 1 and 100")]
            int threshold,
            [Summary("channel", "Counting channel, defaults to current")]
            ITextChannel? channel = null)
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
        /// <param name="hours">Time window in hours between 1 and 168.</param>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        [SlashCommand("window", "Set the time window in hours for tracking wrong counts")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CountmodWindow(
            [Summary("hours", "Time window in hours between 1 and 168")]
            int hours,
            [Summary("channel", "Counting channel, defaults to current")]
            ITextChannel? channel = null)
        {
            if (hours is < 1 or > 168)
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
        /// <param name="punishment">Punishment action to apply.</param>
        /// <param name="durationMinutes">Punishment duration in minutes, 0 for permanent.</param>
        /// <param name="role">Role to add when the punishment is AddRole.</param>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        [SlashCommand("punishment", "Set the punishment for wrong counts in a counting channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CountmodPunishment(
            [Summary("punishment", "Punishment action to apply")]
            PunishmentAction punishment,
            [Summary("duration-minutes", "Punishment duration in minutes, 0 for permanent")]
            int durationMinutes = 0,
            [Summary("role", "Role to add when the punishment is AddRole")]
            IRole? role = null,
            [Summary("channel", "Counting channel, defaults to current")]
            ITextChannel? channel = null)
        {
            channel ??= (ITextChannel)ctx.Channel;

            if (punishment == PunishmentAction.AddRole && role == null)
            {
                await ErrorAsync(Strings.CountingModerationRoleRequired(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await DeferAsync();

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

                await ctx.Interaction.FollowupAsync(embed: embed.Build());
            }
            else
            {
                await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Sets roles to ignore from counting.
        /// </summary>
        /// <param name="deleteMessages">Whether to delete messages from ignored roles.</param>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        /// <param name="roles">Roles to ignore, space separated mentions, ids, or names.</param>
        [SlashCommand("ignore", "Set roles to ignore from counting")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CountmodIgnore(
            [Summary("delete-messages", "Whether to delete messages from ignored roles")]
            bool deleteMessages,
            [Summary("channel", "Counting channel, defaults to current")]
            ITextChannel? channel = null,
            [Summary("roles", "Roles to ignore, space separated")]
            IRole[]? roles = null)
        {
            await DeferAsync();
            channel ??= (ITextChannel)ctx.Channel;
            roles ??= [];

            var roleIds = string.Join(",", roles.Select(r => r.Id));

            var config = new CountingModerationConfig
            {
                ChannelId = channel.Id,
                UseDefaults = false,
                IgnoreRoles = roleIds,
                DeleteIgnoredMessages = deleteMessages
            };

            var success = await Service.SetChannelConfigAsync(channel.Id, config);

            if (success)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(Strings.CountingModerationIgnoreRolesUpdatedTitle(ctx.Guild.Id))
                    .WithDescription(Strings.CountingModerationIgnoreRolesUpdatedDesc(ctx.Guild.Id, channel.Mention))
                    .WithColor(Mewdeko.OkColor)
                    .AddField("Delete Messages", YesNo(deleteMessages), true);

                embed.AddField("Ignored Roles",
                    roles.Any() ? string.Join(", ", roles.Select(r => r.Mention)) : Strings.None(ctx.Guild.Id));

                await ctx.Interaction.FollowupAsync(embed: embed.Build());
            }
            else
            {
                await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Sets roles required for counting.
        /// </summary>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        /// <param name="roles">Required roles, space separated mentions, ids, or names.</param>
        [SlashCommand("required", "Set roles required for counting")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CountmodRequired(
            [Summary("channel", "Counting channel, defaults to current")]
            ITextChannel? channel = null,
            [Summary("roles", "Required roles, space separated")]
            IRole[]? roles = null)
        {
            await DeferAsync();
            channel ??= (ITextChannel)ctx.Channel;
            roles ??= [];

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
                    .WithDescription(
                        Strings.CountingModerationRequiredRolesUpdatedDesc(ctx.Guild.Id, channel.Mention))
                    .WithColor(Mewdeko.OkColor);

                if (roles.Any())
                    embed.AddField("Required Roles", string.Join(", ", roles.Select(r => r.Mention)));
                else
                    embed.AddField("Required Roles", "None (everyone can count)");

                await ctx.Interaction.FollowupAsync(embed: embed.Build());
            }
            else
            {
                await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Sets roles banned from counting.
        /// </summary>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        /// <param name="roles">Banned roles, space separated mentions, ids, or names.</param>
        [SlashCommand("banned", "Set roles banned from counting")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CountmodBanned(
            [Summary("channel", "Counting channel, defaults to current")]
            ITextChannel? channel = null,
            [Summary("roles", "Banned roles, space separated")]
            IRole[]? roles = null)
        {
            await DeferAsync();
            channel ??= (ITextChannel)ctx.Channel;
            roles ??= [];

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
                    embed.AddField("Banned Roles", Strings.None(ctx.Guild.Id));

                await ctx.Interaction.FollowupAsync(embed: embed.Build());
            }
            else
            {
                await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Resets a channel to use guild defaults.
        /// </summary>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        [SlashCommand("reset", "Reset a counting channel to use guild default moderation")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CountmodReset(
            [Summary("channel", "Counting channel, defaults to current")]
            ITextChannel? channel = null)
        {
            channel ??= (ITextChannel)ctx.Channel;

            var config = new CountingModerationConfig
            {
                ChannelId = channel.Id, UseDefaults = true
            };

            var success = await Service.SetChannelConfigAsync(channel.Id, config);

            if (success)
            {
                await ConfirmAsync(Strings.CountingModerationReset(ctx.Guild.Id, channel.Mention))
                    .ConfigureAwait(false);
            }
            else
            {
                await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Sets a tiered punishment for a specific wrong count number.
        /// </summary>
        /// <param name="count">The wrong count number that triggers this punishment.</param>
        /// <param name="punishment">Punishment action to apply.</param>
        /// <param name="durationMinutes">Punishment duration in minutes, 0 for permanent.</param>
        /// <param name="role">Role to add when the punishment is AddRole.</param>
        /// <param name="channel">The counting channel, or omit for guild defaults.</param>
        [SlashCommand("punish", "Set a tiered punishment for a specific wrong count number")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CountmodPunish(
            [Summary("count", "Wrong count number that triggers this punishment")]
            int count,
            [Summary("punishment", "Punishment action to apply")]
            PunishmentAction punishment,
            [Summary("duration-minutes", "Punishment duration in minutes, 0 for permanent")]
            int durationMinutes = 0,
            [Summary("role", "Role to add when the punishment is AddRole")]
            IRole? role = null,
            [Summary("channel", "Counting channel, omit for guild defaults")]
            ITextChannel? channel = null)
        {
            if (punishment == PunishmentAction.AddRole && role == null)
            {
                await ErrorAsync(Strings.CountingModerationRoleRequired(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await DeferAsync();

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

                await ctx.Interaction.FollowupAsync(embed: embed.Build());
            }
            else
            {
                await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Removes a tiered punishment for a specific wrong count number.
        /// </summary>
        /// <param name="count">The wrong count number whose punishment to remove.</param>
        /// <param name="channel">The counting channel, or omit for guild defaults.</param>
        [SlashCommand("unpunish", "Remove a tiered punishment for a specific wrong count number")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CountmodUnpunish(
            [Summary("count", "Wrong count number whose punishment to remove")]
            int count,
            [Summary("channel", "Counting channel, omit for guild defaults")]
            ITextChannel? channel = null)
        {
            var success = await Service.RemoveTieredPunishmentAsync(ctx.Guild.Id, channel?.Id, count);

            if (success)
            {
                await ConfirmAsync(Strings.CountingModerationPunishmentRemoved(ctx.Guild.Id, count,
                    channel?.Mention ?? "guild defaults")).ConfigureAwait(false);
            }
            else
            {
                await ErrorAsync(Strings.CountingModerationPunishmentNotFound(ctx.Guild.Id, count))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Lists all tiered punishments for the guild or specific channel.
        /// </summary>
        /// <param name="channel">The counting channel, or omit for guild defaults.</param>
        [SlashCommand("punish-list", "List all tiered punishments for the guild or a channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CountmodPunishList(
            [Summary("channel", "Counting channel, omit for guild defaults")]
            ITextChannel? channel = null)
        {
            await DeferAsync();
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

            await ctx.Interaction.FollowupAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Clears wrong counts for a user in a counting channel.
        /// </summary>
        /// <param name="user">The user whose wrong counts to clear.</param>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        [SlashCommand("clear", "Clear wrong counts for a user in a counting channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageMessages)]
        public async Task CountmodClear(
            [Summary("user", "User whose wrong counts to clear")]
            IGuildUser user,
            [Summary("channel", "Counting channel, defaults to current")]
            ITextChannel? channel = null)
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
                await ErrorAsync(Strings.CountingModerationNoWrongCounts(ctx.Guild.Id, user.Mention,
                        channel.Mention))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Clears wrong counts for all users in a counting channel.
        /// </summary>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        [SlashCommand("clear-all", "Clear wrong counts for all users in a counting channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageMessages)]
        public async Task CountmodClearAll(
            [Summary("channel", "Counting channel, defaults to current")]
            ITextChannel? channel = null)
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
        /// <param name="punish">Whether to punish non-number messages.</param>
        /// <param name="delete">Whether to delete non-number messages.</param>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        [SlashCommand("non-numbers", "Configure non-number message handling for a counting channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CountmodNonnumbers(
            [Summary("punish", "Whether to punish non-number messages")]
            bool punish,
            [Summary("delete", "Whether to delete non-number messages")]
            bool delete = true,
            [Summary("channel", "Counting channel, defaults to current")]
            ITextChannel? channel = null)
        {
            await DeferAsync();
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
                    .AddField(Strings.CountingModerationPunishNonNumbersField(ctx.Guild.Id), YesNo(punish), true)
                    .AddField(Strings.CountingModerationDeleteNonNumbersField(ctx.Guild.Id), YesNo(delete), true);

                await ctx.Interaction.FollowupAsync(embed: embed.Build());
            }
            else
            {
                await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Configures message edit protection for a counting channel.
        /// </summary>
        /// <param name="punish">Whether to punish edited count messages.</param>
        /// <param name="delete">Whether to delete edited count messages.</param>
        /// <param name="channel">The counting channel. Defaults to current channel.</param>
        [SlashCommand("edits", "Configure message edit protection for a counting channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        public async Task CountmodEdits(
            [Summary("punish", "Whether to punish edited count messages")]
            bool punish,
            [Summary("delete", "Whether to delete edited count messages")]
            bool delete = true,
            [Summary("channel", "Counting channel, defaults to current")]
            ITextChannel? channel = null)
        {
            await DeferAsync();
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
                    .AddField(Strings.CountingModerationPunishEditsField(ctx.Guild.Id), YesNo(punish), true)
                    .AddField(Strings.CountingModerationDeleteEditsField(ctx.Guild.Id), YesNo(delete), true);

                await ctx.Interaction.FollowupAsync(embed: embed.Build());
            }
            else
            {
                await ErrorAsync(Strings.CountingModerationConfigFailed(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }
    }
}