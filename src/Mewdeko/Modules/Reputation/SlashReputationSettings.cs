using System.IO;
using System.Net.Http;
using System.Text;
using DataModel;
using Discord.Interactions;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Reputation.Services;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Reputation;

public partial class SlashReputation
{
    /// <summary>
    ///     Channel states for reputation giving.
    /// </summary>
    public enum RepChannelState
    {
        /// <summary>Reputation can be given in the channel.</summary>
        Enabled,

        /// <summary>Reputation cannot be given in the channel.</summary>
        Disabled,

        /// <summary>Reputation can only be viewed in the channel.</summary>
        Readonly
    }

    /// <summary>
    ///     Decay schedule types for reputation decay.
    /// </summary>
    public enum RepDecayType
    {
        /// <summary>Decay happens every day.</summary>
        Daily,

        /// <summary>Decay happens every week.</summary>
        Weekly,

        /// <summary>Decay happens every month.</summary>
        Monthly,

        /// <summary>Decay after a fixed period.</summary>
        Fixed,

        /// <summary>Percentage based decay.</summary>
        Percentage
    }

    /// <summary>
    ///     Core reputation configuration commands.
    /// </summary>
    [Group("config", "Reputation system configuration")]
    public class RepConfigCommands(IDataConnectionFactory dbFactory) : MewdekoSlashSubmodule<RepService>
    {
        /// <summary>
        ///     Enables or disables the reputation system for this server.
        /// </summary>
        /// <param name="enabled">True to enable, false to disable.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("enable", "Enables or disables the reputation system")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepEnable(
            [Summary("enabled", "Whether the reputation system is enabled")]
            bool enabled = true)
        {
            await Service.SetEnabledAsync(ctx.Guild.Id, enabled);

            await ConfirmAsync(Strings.RepConfigEnabled(ctx.Guild.Id, enabled)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows the current reputation configuration in a detailed embed.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("status", "Shows the current reputation configuration")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepStatus()
        {
            var repConfig = await Service.GetConfigAsync(ctx.Guild.Id);

            var embed = BuildStatusEmbed(repConfig)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            await ctx.Interaction.RespondAsync(embed: embed).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows every reputation setting for this server in one embed, including role rewards, reaction
        ///     configurations, custom types, channel configurations and active events.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("view", "Shows all reputation settings in one embed")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepConfig()
        {
            await DeferAsync().ConfigureAwait(false);

            var repConfig = await Service.GetConfigAsync(ctx.Guild.Id);
            var roleRewards = await Service.GetRoleRewardsAsync(ctx.Guild.Id);
            var reactionConfigs = await Service.GetReactionConfigsAsync(ctx.Guild.Id);
            var customTypes = await Service.GetCustomTypesAsync(ctx.Guild.Id);
            var channelConfigs = await Service.GetChannelConfigsAsync(ctx.Guild.Id);
            var activeEvents = await Service.GetActiveEventsAsync(ctx.Guild.Id);

            var embed = BuildStatusEmbed(repConfig);

            if (roleRewards.Count > 0)
            {
                var rolesText = string.Join("\n", roleRewards.Take(10).Select(x =>
                    $"**{ctx.Guild.GetRole(x.RoleId)?.Name ?? $"Unknown Role ({x.RoleId})"}**: {x.RepRequired}"));
                embed.AddField(Strings.RepRoleRewardsList(ctx.Guild.Id), rolesText);
            }

            if (reactionConfigs.Count > 0)
            {
                var reactionsText = string.Join("\n", reactionConfigs.Take(10).Select(x =>
                {
                    var emoji = x.EmojiId.HasValue ? $"<:{x.EmojiName}:{x.EmojiId}>" : x.EmojiName;
                    return $"{emoji}: {x.RepAmount} {x.RepType}";
                }));
                embed.AddField(Strings.RepReactionListTitle(ctx.Guild.Id), reactionsText);
            }

            if (customTypes.Count > 0)
            {
                var typesText = string.Join("\n",
                    customTypes.Take(10).Select(x => $"**{x.TypeName}**: {x.DisplayName}"));
                embed.AddField(Strings.RepTypeListTitle(ctx.Guild.Id), typesText);
            }

            if (channelConfigs.Count > 0)
            {
                var channelsText = string.Join("\n",
                    channelConfigs.Take(10).Select(x => $"<#{x.ChannelId}>: {x.State} (x{x.Multiplier})"));
                embed.AddField(Strings.RepRestrictedChannels(ctx.Guild.Id), channelsText);
            }

            if (activeEvents.Count > 0)
            {
                var eventsText = string.Join("\n", activeEvents.Take(10).Select(x =>
                    $"**{x.Name}**: {x.Multiplier}x until {x.EndTime:yyyy-MM-dd HH:mm} UTC"));
                embed.AddField(Strings.RepActivityTitle(ctx.Guild.Id), eventsText);
            }

            embed.WithTimestamp(DateTimeOffset.UtcNow);

            await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Configures channel-specific reputation settings.
        /// </summary>
        /// <param name="channel">The channel to configure.</param>
        /// <param name="state">The state to set (enabled/disabled/readonly).</param>
        /// <param name="multiplier">The reputation multiplier for this channel.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("channel", "Configures channel specific reputation settings")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepChannel(
            [Summary("channel", "The channel to configure")]
            ITextChannel channel,
            [Summary("state", "Whether reputation is enabled, disabled or readonly in the channel")]
            RepChannelState state = RepChannelState.Enabled,
            [Summary("multiplier", "The reputation multiplier for this channel")]
            double multiplier = 1.0)
        {
            var multiplierValue = (decimal)multiplier;
            if (multiplierValue < 0)
            {
                await ReplyErrorAsync(Strings.RepMultiplierNegative(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var stateName = state.ToString().ToLowerInvariant();
            await Service.SetChannelConfigAsync(ctx.Guild.Id, channel.Id, stateName, multiplierValue);

            var message = state switch
            {
                RepChannelState.Enabled => Strings.RepChannelEnabled(ctx.Guild.Id, channel.Mention),
                RepChannelState.Disabled => Strings.RepChannelDisabledSet(ctx.Guild.Id, channel.Mention),
                RepChannelState.Readonly => Strings.RepChannelReadonly(ctx.Guild.Id, channel.Mention),
                _ => Strings.RepConfigUpdated(ctx.Guild.Id)
            };

            if (multiplierValue != 1.0m)
            {
                message += $"\n{Strings.RepChannelMultiplier(ctx.Guild.Id, channel.Mention, multiplierValue)}";
            }

            await ConfirmAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the default cooldown between giving reputation.
        /// </summary>
        /// <param name="minutes">Cooldown in minutes (1-1440).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("cooldown", "Sets the default cooldown between giving reputation")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepCooldown([Summary("minutes", "Cooldown in minutes (1-1440)")] int minutes)
        {
            if (minutes is < 1 or > 1440)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetDefaultCooldownAsync(ctx.Guild.Id, minutes);
            await ConfirmAsync(Strings.RepConfigCooldown(ctx.Guild.Id, minutes)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the daily limit for giving reputation.
        /// </summary>
        /// <param name="limit">Daily limit (1-100).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("daily-limit", "Sets the daily limit for giving reputation")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepDailyLimit([Summary("limit", "Daily limit (1-100)")] int limit)
        {
            if (limit is < 1 or > 100)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetDailyLimitAsync(ctx.Guild.Id, limit);
            await ConfirmAsync(Strings.RepConfigDailyLimit(ctx.Guild.Id, limit)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the weekly limit for giving reputation (optional).
        /// </summary>
        /// <param name="limit">Weekly limit (omit to disable, 1-500).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("weekly-limit", "Sets the weekly limit for giving reputation, omit to disable")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepWeeklyLimit([Summary("limit", "Weekly limit (1-500), omit to disable")] int? limit = null)
        {
            if (limit.HasValue && limit is < 1 or > 500)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetWeeklyLimitAsync(ctx.Guild.Id, limit);

            await ConfirmAsync(Strings.RepConfigUpdated(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the minimum account age required to give reputation.
        /// </summary>
        /// <param name="days">Minimum account age in days (0-365).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("min-age", "Sets the minimum account age required to give reputation")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepMinAge([Summary("days", "Minimum account age in days (0-365)")] int days)
        {
            if (days is < 0 or > 365)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetMinAccountAgeAsync(ctx.Guild.Id, days);
            await ConfirmAsync(Strings.RepConfigAccountAge(ctx.Guild.Id, days)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the minimum server membership time required to give reputation.
        /// </summary>
        /// <param name="hours">Minimum membership time in hours (0-720).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("min-membership", "Sets the minimum server membership time required to give reputation")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepMinMembership([Summary("hours", "Minimum membership time in hours (0-720)")] int hours)
        {
            if (hours is < 0 or > 720)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetMinServerMembershipAsync(ctx.Guild.Id, hours);
            await ConfirmAsync(Strings.RepConfigMembership(ctx.Guild.Id, hours)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the minimum message count required to give reputation.
        /// </summary>
        /// <param name="count">Minimum message count (0-1000).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("min-messages", "Sets the minimum message count required to give reputation")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepMinMessages([Summary("count", "Minimum message count (0-1000)")] int count)
        {
            if (count is < 0 or > 1000)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetMinMessageCountAsync(ctx.Guild.Id, count);
            await ConfirmAsync(Strings.RepConfigMessages(ctx.Guild.Id, count)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Enables or disables negative reputation.
        /// </summary>
        /// <param name="enabled">True to enable negative reputation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("negative", "Enables or disables negative reputation")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepNegative([Summary("enabled", "Whether negative reputation is enabled")] bool enabled)
        {
            await Service.SetNegativeReputationAsync(ctx.Guild.Id, enabled);

            await ConfirmAsync(Strings.RepConfigUpdated(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Enables or disables anonymous reputation giving.
        /// </summary>
        /// <param name="enabled">True to enable anonymous reputation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("anonymous", "Enables or disables anonymous reputation giving")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepAnonymous([Summary("enabled", "Whether anonymous reputation is enabled")] bool enabled)
        {
            await Service.SetAnonymousReputationAsync(ctx.Guild.Id, enabled);

            await ConfirmAsync(Strings.RepConfigUpdated(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the notification channel for reputation events.
        /// </summary>
        /// <param name="channel">The channel to send notifications to (omit to disable).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("notification-channel", "Sets the notification channel for reputation events")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepNotificationChannel(
            [Summary("channel", "The channel to send notifications to, omit to disable")]
            ITextChannel? channel = null)
        {
            await Service.SetNotificationChannelAsync(ctx.Guild.Id, channel?.Id);

            await ConfirmAsync(Strings.RepConfigUpdated(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Manually triggers milestone lost event for testing.
        /// </summary>
        /// <param name="user">The user who lost the milestone.</param>
        /// <param name="role">The role that was lost.</param>
        /// <param name="threshold">The reputation threshold.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("milestone-lost", "Manually triggers the milestone lost message for testing")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepMilestoneLost(
            [Summary("user", "The user who lost the milestone")]
            IGuildUser user,
            [Summary("role", "The role that was lost")]
            IRole role,
            [Summary("threshold", "The reputation threshold")]
            int threshold)
        {
            await ConfirmAsync(Strings.RepMilestoneLost(ctx.Guild.Id, user.Mention, role.Name, threshold))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Exports the current reputation configuration to JSON.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("export", "Exports the current reputation configuration to a JSON file")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepExport()
        {
            await DeferAsync().ConfigureAwait(false);

            try
            {
                await using var db = await dbFactory.CreateConnectionAsync();

                var repConfig = await db.RepConfigs.FirstOrDefaultAsync(x => x.GuildId == ctx.Guild.Id);
                var channelConfigs = await db.RepChannelConfigs.Where(x => x.GuildId == ctx.Guild.Id).ToListAsync();
                var roleRewards = await db.RepRoleRewards.Where(x => x.GuildId == ctx.Guild.Id).ToListAsync();
                var reactionConfigs = await db.RepReactionConfigs.Where(x => x.GuildId == ctx.Guild.Id).ToListAsync();
                var customTypes = await db.RepCustomTypes.Where(x => x.GuildId == ctx.Guild.Id).ToListAsync();

                var exportData = new
                {
                    ExportedAt = DateTime.UtcNow,
                    GuildId = ctx.Guild.Id,
                    GuildName = ctx.Guild.Name,
                    Config = repConfig,
                    ChannelConfigs = channelConfigs,
                    RoleRewards = roleRewards,
                    ReactionConfigs = reactionConfigs,
                    CustomTypes = customTypes
                };

                var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
                var bytes = Encoding.UTF8.GetBytes(json);

                using var stream = new MemoryStream(bytes);
                var fileName = $"reputation-config-{ctx.Guild.Id}-{DateTime.UtcNow:yyyy-MM-dd}.json";

                await ctx.Interaction.FollowupWithFileAsync(stream, fileName,
                    $"{Config.SuccessEmote} {Strings.RepExportSuccess(ctx.Guild.Id)}").ConfigureAwait(false);
            }
            catch
            {
                await ErrorAsync(Strings.RepExportError(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Imports reputation configuration from an uploaded JSON file.
        /// </summary>
        /// <param name="file">The JSON file previously produced by the export command.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("import", "Imports reputation configuration from a JSON file")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepImport(
            [Summary("file", "The exported reputation configuration JSON file")]
            IAttachment file)
        {
            if (!file.Filename.EndsWith(".json"))
            {
                await ErrorAsync(Strings.RepImportNoFile(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await DeferAsync().ConfigureAwait(false);

            try
            {
                using var httpClient = new HttpClient();
                var json = await httpClient.GetStringAsync(file.Url);
                var importData = JsonConvert.DeserializeObject<dynamic>(json);

                if (importData == null)
                {
                    await ErrorAsync(Strings.RepImportInvalidFile(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                await ConfirmAsync(Strings.RepImportSuccess(ctx.Guild.Id)).ConfigureAwait(false);
            }
            catch
            {
                await ErrorAsync(Strings.RepImportError(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        private EmbedBuilder BuildStatusEmbed(RepConfig repConfig)
        {
            var guildId = ctx.Guild.Id;

            var basic = $"**{Strings.RepEnabled(guildId)}:** {EnabledText(repConfig.Enabled)}\n" +
                        $"**{Strings.RepEnableAnonymous(guildId)}:** {EnabledText(repConfig.EnableAnonymous)}\n" +
                        $"**{Strings.RepEnableNegative(guildId)}:** {EnabledText(repConfig.EnableNegativeRep)}";

            var cooldowns = $"**{Strings.RepDefaultCooldown(guildId)}:** {repConfig.DefaultCooldownMinutes} minutes\n" +
                            $"**{Strings.RepDailyLimitField(guildId)}:** {repConfig.DailyLimit}\n" +
                            $"**{Strings.RepWeeklyLimitField(guildId)}:** {repConfig.WeeklyLimit?.ToString() ?? "None"}";

            var requirements = $"**{Strings.RepMinAccountAgeField(guildId)}:** {repConfig.MinAccountAgeDays} days\n" +
                               $"**{Strings.RepMinMembershipField(guildId)}:** {repConfig.MinServerMembershipHours} hours\n" +
                               $"**{Strings.RepMinMessagesField(guildId)}:** {repConfig.MinMessageCount}";

            var notifications = $"**{Strings.RepNotificationChannel(guildId)}:** " +
                                (repConfig.NotificationChannel.HasValue
                                    ? $"<#{repConfig.NotificationChannel}>"
                                    : "None");

            var advanced = $"**{Strings.RepEnableDecay(guildId)}:** {EnabledText(repConfig.EnableDecay)}\n" +
                           $"**{Strings.RepDecayType(guildId)}:** {repConfig.DecayType}\n" +
                           $"**{Strings.RepDecayAmount(guildId)}:** {repConfig.DecayAmount}\n" +
                           $"**{Strings.RepDecayInactiveDays(guildId)}:** {repConfig.DecayInactiveDays} days";

            return new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepConfigTitle(guildId))
                .WithDescription(Strings.RepConfigDescription(guildId))
                .AddField(Strings.RepConfigBasic(guildId), basic)
                .AddField(Strings.RepConfigCooldowns(guildId), cooldowns)
                .AddField(Strings.RepConfigRequirements(guildId), requirements)
                .AddField(Strings.RepConfigNotifications(guildId), notifications)
                .AddField(Strings.RepConfigAdvanced(guildId), advanced);
        }

        private string EnabledText(bool enabled)
        {
            return enabled ? $"{Config.SuccessEmote} Enabled" : $"{Config.ErrorEmote} Disabled";
        }
    }

    /// <summary>
    ///     Reputation decay configuration commands.
    /// </summary>
    [Group("decay", "Reputation decay configuration")]
    public class RepDecayCommands : MewdekoSlashSubmodule<RepService>
    {
        /// <summary>
        ///     Enables or disables reputation decay for inactive users.
        /// </summary>
        /// <param name="enabled">True to enable decay.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("toggle", "Enables or disables reputation decay for inactive users")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepDecay([Summary("enabled", "Whether reputation decay is enabled")] bool enabled)
        {
            await Service.SetDecaySettingsAsync(ctx.Guild.Id, enabled);

            var message = enabled
                ? Strings.RepDecayEnabled(ctx.Guild.Id, "1", "daily")
                : Strings.RepDecayDisabled(ctx.Guild.Id);
            await ConfirmAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the decay type (daily, weekly, monthly, fixed, percentage).
        /// </summary>
        /// <param name="type">The decay type.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("type", "Sets the decay schedule type")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task SetDecayType([Summary("type", "The decay type")] RepDecayType type)
        {
            await Service.SetDecaySettingsAsync(ctx.Guild.Id, true, type.ToString().ToLowerInvariant());
            await ConfirmAsync(Strings.RepConfigUpdated(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the decay amount.
        /// </summary>
        /// <param name="amount">The decay amount (1-100).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("amount", "Sets the amount of reputation lost per decay")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepDecayAmount([Summary("amount", "The decay amount (1-100)")] int amount)
        {
            if (amount is < 1 or > 100)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetDecaySettingsAsync(ctx.Guild.Id, true, amount: amount);
            await ConfirmAsync(Strings.RepConfigUpdated(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the number of inactive days before decay starts.
        /// </summary>
        /// <param name="days">Days of inactivity (1-365).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("inactive", "Sets the number of inactive days before decay starts")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepDecayInactive([Summary("days", "Days of inactivity (1-365)")] int days)
        {
            if (days is < 1 or > 365)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetDecaySettingsAsync(ctx.Guild.Id, true, inactiveDays: days);
            await ConfirmAsync(Strings.RepConfigUpdated(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     User reputation management commands.
    /// </summary>
    [Group("manage", "Manage user reputation")]
    public class RepManageCommands : MewdekoSlashSubmodule<RepService>
    {
        /// <summary>
        ///     Removes reputation from a user.
        /// </summary>
        /// <param name="user">The user to remove reputation from.</param>
        /// <param name="amount">The amount of reputation to remove.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("take", "Removes reputation from a user")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepTake(
            [Summary("user", "The user to remove reputation from")]
            IGuildUser user,
            [Summary("amount", "The amount of reputation to remove")]
            int amount = 1)
        {
            if (amount <= 0)
            {
                await ReplyErrorAsync(Strings.RepAmountInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var newTotal = await Service.TakeReputationAsync(ctx.Guild.Id, user.Id, amount, ctx.User.Id);

            if (newTotal == 0)
            {
                await ReplyErrorAsync(Strings.RepNoRepToRemove(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ConfirmAsync(Strings.RepTaken(ctx.Guild.Id, amount, user.Mention, newTotal))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets a user's reputation to a specific value.
        /// </summary>
        /// <param name="user">The user to set reputation for.</param>
        /// <param name="amount">The amount to set reputation to.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("set", "Sets a user's reputation to a specific value")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepSet(
            [Summary("user", "The user to set reputation for")]
            IGuildUser user,
            [Summary("amount", "The amount to set reputation to")]
            int amount)
        {
            if (amount < 0)
            {
                await ReplyErrorAsync(Strings.RepNegativeAmount(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetReputationAsync(ctx.Guild.Id, user.Id, amount, ctx.User.Id);
            await ConfirmAsync(Strings.RepSet(ctx.Guild.Id, user.Mention, amount)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Resets reputation for a user, or for all users in the server when no user is given.
        /// </summary>
        /// <param name="user">The user to reset. Omit to reset everyone.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("reset", "Resets reputation for a user, or everyone when no user is given")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepReset(
            [Summary("user", "The user to reset, omit to reset everyone")]
            IGuildUser? user = null)
        {
            if (user == null)
            {
                var confirmed = await PromptUserConfirmAsync(Strings.RepResetAllConfirm(ctx.Guild.Id), ctx.User.Id);
                if (!confirmed)
                {
                    await ReplyErrorAsync(Strings.RepOperationCancelled(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                await Service.ResetAllReputationAsync(ctx.Guild.Id);
                await ConfirmAsync(Strings.RepResetAll(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                var wasReset = await Service.ResetUserReputationAsync(ctx.Guild.Id, user.Id);
                if (!wasReset)
                {
                    await ReplyErrorAsync(Strings.RepNoData(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                await ConfirmAsync(Strings.RepResetUser(ctx.Guild.Id, user.Mention)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Freezes a user's reputation, preventing them from gaining or losing reputation.
        /// </summary>
        /// <param name="user">The user to freeze.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("freeze", "Freezes a user's reputation")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepFreeze([Summary("user", "The user to freeze")] IGuildUser user)
        {
            await Service.FreezeUserAsync(ctx.Guild.Id, user.Id);
            await ConfirmAsync(Strings.RepFrozenUser(ctx.Guild.Id, user.Mention)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Unfreezes a user's reputation, allowing them to gain and lose reputation again.
        /// </summary>
        /// <param name="user">The user to unfreeze.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("unfreeze", "Unfreezes a user's reputation")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepUnfreeze([Summary("user", "The user to unfreeze")] IGuildUser user)
        {
            var wasUnfrozen = await Service.UnfreezeUserAsync(ctx.Guild.Id, user.Id);
            if (!wasUnfrozen)
            {
                await ReplyErrorAsync(Strings.RepNoData(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ConfirmAsync(Strings.RepUnfrozenUser(ctx.Guild.Id, user.Mention)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Reaction based reputation configuration commands.
    /// </summary>
    [Group("reaction", "Reaction based reputation configuration")]
    public class RepReactionCommands : MewdekoSlashSubmodule<RepService>
    {
        /// <summary>
        ///     Configures reaction-based reputation giving.
        /// </summary>
        /// <param name="emoji">The emoji to use for reputation.</param>
        /// <param name="amount">The amount of reputation to give.</param>
        /// <param name="repType">The type of reputation (standard, helper, artist, memer).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("add", "Adds or updates a reaction that gives reputation")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepReaction(
            [Summary("emoji", "The emoji to use for reputation")]
            string emoji,
            [Summary("amount", "The amount of reputation to give")]
            int amount = 1,
            [Summary("type", "The type of reputation (standard, helper, artist, memer)")]
            string repType = "standard")
        {
            if (!await Service.IsValidReputationTypeAsync(ctx.Guild.Id, repType))
            {
                await ReplyErrorAsync(Strings.RepInvalidType(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var isNew = await Service.AddOrUpdateReactionConfigAsync(ctx.Guild.Id, emoji, amount, repType);

            if (isNew)
            {
                await ConfirmAsync(Strings.RepReactionAdded(ctx.Guild.Id, emoji, amount, repType))
                    .ConfigureAwait(false);
            }
            else
            {
                await ConfirmAsync(Strings.RepReactionUpdated(ctx.Guild.Id, emoji, amount, repType))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Removes a reaction-based reputation configuration.
        /// </summary>
        /// <param name="emoji">The emoji to remove.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("remove", "Removes a reaction based reputation configuration")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepReactionRemove([Summary("emoji", "The emoji to remove")] string emoji)
        {
            var removed = await Service.RemoveReactionConfigAsync(ctx.Guild.Id, emoji);

            if (removed)
            {
                await ConfirmAsync(Strings.RepReactionRemoved(ctx.Guild.Id, emoji)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.RepReactionNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Lists all configured reaction-based reputation settings.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("list", "Lists all reaction based reputation configurations")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepReactionList()
        {
            var reactionConfigs = await Service.GetReactionConfigsAsync(ctx.Guild.Id);

            if (reactionConfigs.Count == 0)
            {
                await ReplyErrorAsync(Strings.RepReactionListEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepReactionListTitle(ctx.Guild.Id));

            var description = string.Empty;
            foreach (var config in reactionConfigs)
            {
                var emoji = config.EmojiId.HasValue
                    ? $"<:{config.EmojiName}:{config.EmojiId}>"
                    : config.EmojiName;
                var status = config.IsEnabled ? Config.SuccessEmote : Config.ErrorEmote;
                description += $"{status} {emoji} - {config.RepAmount} {config.RepType} rep\n";
            }

            eb.WithDescription(description);
            await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Role reward configuration commands for reputation milestones.
    /// </summary>
    [Group("role", "Role rewards for reputation milestones")]
    public class RepRoleCommands : MewdekoSlashSubmodule<RepService>
    {
        /// <summary>
        ///     Adds or updates a role reward for a reputation milestone with optional advanced settings.
        /// </summary>
        /// <param name="role">The role to award.</param>
        /// <param name="reputation">The reputation required to earn the role.</param>
        /// <param name="removeOnDrop">Whether to remove the role if reputation drops.</param>
        /// <param name="announceChannel">Optional channel to announce role awards.</param>
        /// <param name="announceDm">Whether to send DM notifications.</param>
        /// <param name="xpReward">Optional XP reward for reaching milestone.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("add", "Adds or updates a role reward for a reputation milestone")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task RepRole(
            [Summary("role", "The role to award")] IRole role,
            [Summary("reputation", "The reputation required to earn the role")]
            int reputation,
            [Summary("remove-on-drop", "Whether to remove the role if reputation drops")]
            bool removeOnDrop = true,
            [Summary("announce-channel", "Channel to announce role awards in")]
            ITextChannel? announceChannel = null,
            [Summary("announce-dm", "Whether to send DM notifications")]
            bool announceDm = false,
            [Summary("xp-reward", "XP reward for reaching the milestone")]
            int? xpReward = null)
        {
            if (reputation < 0)
            {
                await ReplyErrorAsync(Strings.RepAmountInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var currentUser = await ctx.Guild.GetCurrentUserAsync();
            if (role.Position >= currentUser.Hierarchy)
            {
                await ReplyErrorAsync(Strings.RepRoleHierarchy(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var isNew = await Service.AddOrUpdateRoleRewardAsync(ctx.Guild.Id, role.Id, reputation,
                removeOnDrop, announceChannel?.Id, announceDm, xpReward);

            var hasAdvancedSettings = !removeOnDrop || announceChannel != null || announceDm || xpReward.HasValue;

            if (isNew)
            {
                if (hasAdvancedSettings)
                {
                    await ConfirmAsync(Strings.RepRoleAddedAdvanced(ctx.Guild.Id, role.Name, reputation,
                        removeOnDrop, announceDm, xpReward ?? 0)).ConfigureAwait(false);
                }
                else
                {
                    await ConfirmAsync(Strings.RepRoleAdded(ctx.Guild.Id, role.Name, reputation))
                        .ConfigureAwait(false);
                }
            }
            else
            {
                if (hasAdvancedSettings)
                {
                    await ConfirmAsync(Strings.RepRoleUpdatedAdvanced(ctx.Guild.Id, role.Name, reputation,
                        removeOnDrop, announceDm, xpReward ?? 0)).ConfigureAwait(false);
                }
                else
                {
                    await ConfirmAsync(Strings.RepRoleUpdated(ctx.Guild.Id, role.Name, reputation))
                        .ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        ///     Removes a role reward configuration.
        /// </summary>
        /// <param name="role">The role to remove from rewards.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("remove", "Removes a role reward configuration")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task RepRoleRemove([Summary("role", "The role to remove from rewards")] IRole role)
        {
            var removed = await Service.RemoveRoleRewardAsync(ctx.Guild.Id, role.Id);

            if (removed)
            {
                await ConfirmAsync(Strings.RepRoleRewardRemoved(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.RepRoleNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Sets the announcement channel for a role reward.
        /// </summary>
        /// <param name="role">The role to configure.</param>
        /// <param name="channel">The channel for announcements (omit to disable).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("channel", "Sets the announcement channel for a role reward")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepRoleChannel(
            [Summary("role", "The role to configure")]
            IRole role,
            [Summary("channel", "The channel for announcements, omit to disable")]
            ITextChannel? channel = null)
        {
            var updated = await Service.UpdateRoleRewardChannelAsync(ctx.Guild.Id, role.Id, channel?.Id);

            if (updated)
            {
                var message = channel != null
                    ? Strings.RepRoleChannelSet(ctx.Guild.Id, role.Name, channel.Name)
                    : Strings.RepRoleChannelDisabled(ctx.Guild.Id, role.Name);

                await ConfirmAsync(message).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.RepRoleNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Lists all role rewards configured for the server.
        /// </summary>
        /// <param name="detailed">Whether to show detailed information for each reward.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("list", "Lists all role rewards")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task RepRoleList([Summary("detailed", "Show detailed information")] bool detailed = false)
        {
            await DeferAsync().ConfigureAwait(false);

            var roleRewards = await Service.GetRoleRewardsAsync(ctx.Guild.Id);

            if (!roleRewards.Any())
            {
                await ReplyConfirmAsync(Strings.RepNoRoleRewards(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepRoleRewardsList(ctx.Guild.Id))
                .WithDescription(Strings.RepRoleRewardsDesc(ctx.Guild.Id));

            var content = new StringBuilder();

            foreach (var reward in roleRewards.Take(detailed ? 25 : 10))
            {
                var role = ctx.Guild.GetRole(reward.RoleId);
                var roleName = role?.Name ?? $"Unknown Role ({reward.RoleId})";

                if (detailed)
                {
                    content.AppendLine($"**{roleName}** - {reward.RepRequired} rep");

                    if (reward.RemoveOnDrop)
                        content.AppendLine("  └ Remove when rep drops below threshold");

                    if (reward.AnnounceChannel.HasValue)
                    {
                        var channel = await ctx.Guild.GetTextChannelAsync(reward.AnnounceChannel.Value);
                        var channelName = channel?.Name ?? "Unknown Channel";
                        content.AppendLine($"  └ Announce in #{channelName}");
                    }

                    if (reward.AnnounceDM)
                        content.AppendLine("  └ Send DM notifications");

                    if (reward.XPReward is > 0)
                        content.AppendLine($"  └ XP Reward: {reward.XPReward.Value}");

                    content.AppendLine();
                }
                else
                {
                    content.AppendLine(
                        $"**{roleName}** - {Strings.RepRoleRequiredRep(ctx.Guild.Id)}: {reward.RepRequired}");

                    if (reward.XPReward is > 0)
                        content.AppendLine($"  └ {Strings.RepRoleXpReward(ctx.Guild.Id, reward.XPReward.Value)}");

                    if (reward.AnnounceChannel.HasValue)
                    {
                        var channel = await ctx.Guild.GetTextChannelAsync(reward.AnnounceChannel.Value);
                        var channelName = channel?.Name ?? "Unknown Channel";
                        content.AppendLine($"  └ {Strings.RepRoleAnnounceChannel(ctx.Guild.Id, channelName)}");
                    }

                    content.AppendLine();
                }
            }

            eb.AddField("Role Rewards", content.ToString());
            await ctx.Interaction.FollowupAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows detailed information about a specific role reward.
        /// </summary>
        /// <param name="role">The role to show information for.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("info", "Shows detailed information about a role reward")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        public async Task RepRoleInfo([Summary("role", "The role to show information for")] IRole role)
        {
            var roleReward = await Service.GetRoleRewardAsync(ctx.Guild.Id, role.Id);

            if (roleReward == null)
            {
                await ReplyErrorAsync(Strings.RepRoleNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepRoleInfoTitle(ctx.Guild.Id, role.Name))
                .AddField(Strings.RepRequired(ctx.Guild.Id), roleReward.RepRequired, true)
                .AddField(Strings.RepRemoveOnDrop(ctx.Guild.Id), roleReward.RemoveOnDrop ? "Yes" : "No", true);

            if (roleReward.AnnounceChannel.HasValue)
            {
                var channel = await ctx.Guild.GetTextChannelAsync(roleReward.AnnounceChannel.Value);
                var channelName = channel?.Mention ?? "Unknown Channel";
                eb.AddField(Strings.RepAnnounceChannel(ctx.Guild.Id), channelName, true);
            }

            eb.AddField(Strings.RepAnnounceDm(ctx.Guild.Id), roleReward.AnnounceDM ? "Yes" : "No", true);

            if (roleReward.XPReward is > 0)
            {
                eb.AddField(Strings.RepXpReward(ctx.Guild.Id), roleReward.XPReward.Value, true);
            }

            if (roleReward.DateAdded.HasValue)
            {
                eb.AddField(Strings.RepDateAdded(ctx.Guild.Id), $"{roleReward.DateAdded.Value:yyyy-MM-dd HH:mm} UTC",
                    true);
            }

            await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Custom reputation type commands.
    /// </summary>
    [Group("type", "Custom reputation types")]
    public class RepTypeCommands : MewdekoSlashSubmodule<RepService>
    {
        /// <summary>
        ///     Adds a custom reputation type.
        /// </summary>
        /// <param name="typeName">The name of the reputation type.</param>
        /// <param name="displayName">The display name for the type.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("add", "Adds a custom reputation type")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepTypeAdd(
            [Summary("name", "The name of the reputation type")]
            string typeName,
            [Summary("display-name", "The display name for the type")]
            string displayName)
        {
            var added = await Service.AddCustomTypeAsync(ctx.Guild.Id, typeName, displayName);

            if (added)
            {
                await ConfirmAsync(Strings.RepTypeAdded(ctx.Guild.Id, typeName, displayName)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.RepTypeExists(ctx.Guild.Id, typeName)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Removes a custom reputation type.
        /// </summary>
        /// <param name="typeName">The name of the reputation type to remove.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("remove", "Removes a custom reputation type")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepTypeRemove([Summary("name", "The name of the reputation type to remove")] string typeName)
        {
            var removed = await Service.RemoveCustomTypeAsync(ctx.Guild.Id, typeName);

            if (removed)
            {
                await ConfirmAsync(Strings.RepTypeRemoved(ctx.Guild.Id, typeName)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.RepTypeNotFound(ctx.Guild.Id, typeName)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Lists all custom reputation types for this server.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("list", "Lists all custom reputation types")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepTypeList()
        {
            var customTypes = await Service.GetCustomTypesAsync(ctx.Guild.Id);

            if (!customTypes.Any())
            {
                await ReplyErrorAsync(Strings.RepTypeListEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepTypeListTitle(ctx.Guild.Id));

            var description = string.Join("\n", customTypes.Select(x => $"**{x.TypeName}** - {x.DisplayName}"));
            eb.WithDescription(description);

            await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Reputation multiplier event commands.
    /// </summary>
    [Group("event", "Reputation multiplier events")]
    public class RepEventCommands : MewdekoSlashSubmodule<RepService>
    {
        /// <summary>
        ///     Creates a reputation multiplier event.
        /// </summary>
        /// <param name="name">Name of the event.</param>
        /// <param name="multiplier">Reputation multiplier.</param>
        /// <param name="duration">Duration in hours.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("create", "Creates a reputation multiplier event")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepEventCreate(
            [Summary("name", "Name of the event")] string name,
            [Summary("multiplier", "Reputation multiplier")]
            double multiplier,
            [Summary("duration", "Duration in hours")]
            int duration)
        {
            var multiplierValue = (decimal)multiplier;
            var endTime = await Service.CreateEventAsync(ctx.Guild.Id, name, multiplierValue, duration);
            await ConfirmAsync(Strings.RepEventCreated(ctx.Guild.Id, name, multiplierValue, endTime))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Ends a reputation event early.
        /// </summary>
        /// <param name="name">Name of the event to end.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("end", "Ends a reputation event early")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepEventEnd([Summary("name", "Name of the event to end")] string name)
        {
            var ended = await Service.EndEventAsync(ctx.Guild.Id, name);

            if (ended)
            {
                await ConfirmAsync(Strings.RepEventEnded(ctx.Guild.Id, name)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Command reputation requirement commands.
    /// </summary>
    [Group("require", "Reputation requirements for commands")]
    public class RepRequireCommands(RepCommandRequirementsService commandRequirementsService)
        : MewdekoSlashSubmodule<RepService>
    {
        /// <summary>
        ///     The actions available for the bypass role command.
        /// </summary>
        public enum BypassAction
        {
            /// <summary>
            ///     Adds the role to the bypass list.
            /// </summary>
            Add,

            /// <summary>
            ///     Removes the role from the bypass list.
            /// </summary>
            Remove
        }

        /// <summary>
        ///     Sets reputation requirements for a command, optionally restricted to specific channels.
        /// </summary>
        /// <param name="commandName">The command name.</param>
        /// <param name="minReputation">The minimum reputation required.</param>
        /// <param name="repType">The specific reputation type required (optional, defaults to total).</param>
        /// <param name="channels">Optional channels where this requirement applies (if none specified, applies globally).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("add", "Sets reputation requirements for a command")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepCommandReq(
            [Summary("command", "The command name")]
            string commandName,
            [Summary("min-reputation", "The minimum reputation required")]
            int minReputation,
            [Summary("type", "The reputation type required, defaults to total")]
            string? repType = null,
            [Summary("channels", "Channels where this requirement applies, space separated")]
            IGuildChannel[]? channels = null)
        {
            if (minReputation < 0)
            {
                await ReplyErrorAsync(Strings.RepAmountInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (!string.IsNullOrEmpty(repType) && repType != "total" &&
                !await Service.IsValidReputationTypeAsync(ctx.Guild.Id, repType))
            {
                await ReplyErrorAsync(Strings.RepInvalidType(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var textChannels = (channels ?? []).OfType<ITextChannel>().ToArray();

            string? channelIdJson = null;
            var actualRepType = repType == "total" ? null : repType;

            if (textChannels.Length > 0)
            {
                var channelIds = textChannels.Select(c => c.Id).ToList();
                channelIdJson = JsonConvert.SerializeObject(channelIds);
            }

            await commandRequirementsService.AddCommandRequirementAsync(ctx.Guild.Id, commandName.ToLowerInvariant(),
                minReputation, actualRepType, channelIdJson).ConfigureAwait(false);

            if (textChannels.Length > 0)
            {
                var channelNames = string.Join(", ", textChannels.Select(c => $"#{c.Name}"));
                await ConfirmAsync(Strings.RepCommandRequirementAddedChannels(ctx.Guild.Id,
                    commandName, minReputation, repType ?? "total", channelNames)).ConfigureAwait(false);
            }
            else
            {
                await ConfirmAsync(Strings.RepCommandRequirementAdded(ctx.Guild.Id,
                    commandName, minReputation, repType ?? "total")).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Removes reputation requirements from a command.
        /// </summary>
        /// <param name="commandName">The command name.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("remove", "Removes reputation requirements from a command")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepCommandReqRemove([Summary("command", "The command name")] string commandName)
        {
            var deleted = await commandRequirementsService
                .RemoveCommandRequirementAsync(ctx.Guild.Id, commandName.ToLowerInvariant())
                .ConfigureAwait(false);

            if (deleted > 0)
                await ConfirmAsync(Strings.RepCommandRequirementRemoved(ctx.Guild.Id, commandName))
                    .ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.RepCommandRequirementNotFound(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists all command requirements for the server.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("list", "Lists all command reputation requirements")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepCommandReqList()
        {
            await DeferAsync().ConfigureAwait(false);

            var requirements = await commandRequirementsService.GetCommandRequirementsAsync(ctx.Guild.Id)
                .ConfigureAwait(false);

            if (!requirements.Any())
            {
                await ReplyConfirmAsync(Strings.RepNoCommandRequirements(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepCommandRequirementsList(ctx.Guild.Id))
                .WithDescription(Strings.RepCommandRequirementsDesc(ctx.Guild.Id));

            foreach (var req in requirements.Take(15))
            {
                var value = $"**Min Rep:** {req.MinReputation} {req.RequiredRepType ?? "total"}";

                if (!string.IsNullOrEmpty(req.RestrictedChannels))
                {
                    try
                    {
                        var channelIds = JsonConvert.DeserializeObject<List<ulong>>(req.RestrictedChannels);
                        if (channelIds?.Any() == true)
                        {
                            var channelNames = new List<string>();
                            foreach (var channelId in channelIds)
                            {
                                var channel = await ctx.Guild.GetChannelAsync(channelId);
                                if (channel != null) channelNames.Add($"#{channel.Name}");
                            }

                            if (channelNames.Any())
                                value += $"\n**Channels:** {string.Join(", ", channelNames)}";
                        }
                    }
                    catch
                    {
                    }
                }

                embed.AddField($".{req.CommandName}", value, true);
            }

            await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows reputation requirements for a specific command.
        /// </summary>
        /// <param name="commandName">The command to check.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("info", "Shows reputation requirements for a command")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task RepCommandInfo([Summary("command", "The command to check")] string commandName)
        {
            await DeferAsync().ConfigureAwait(false);

            var requirement =
                await commandRequirementsService.GetCommandRequirementAsync(ctx.Guild.Id,
                    commandName.ToLowerInvariant());

            if (requirement == null)
            {
                await ReplyConfirmAsync(Strings.RepCommandNoRequirements(ctx.Guild.Id, commandName))
                    .ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.RepCommandRequirementInfo(ctx.Guild.Id, commandName))
                .AddField(Strings.RepMinReputation(ctx.Guild.Id),
                    $"{requirement.MinReputation} {requirement.RequiredRepType ?? "total"}", true)
                .AddField(Strings.RepShowInHelp(ctx.Guild.Id),
                    requirement.ShowInHelp ? Config.SuccessEmote : Config.ErrorEmote, true);

            if (!string.IsNullOrEmpty(requirement.RestrictedChannels))
            {
                try
                {
                    var channelIds = JsonConvert.DeserializeObject<List<ulong>>(requirement.RestrictedChannels);
                    var channelNames = new List<string>();

                    foreach (var channelId in channelIds ?? [])
                    {
                        var channel = await ctx.Guild.GetChannelAsync(channelId);
                        if (channel != null) channelNames.Add($"#{channel.Name}");
                    }

                    if (channelNames.Any())
                        embed.AddField(Strings.RepRestrictedChannels(ctx.Guild.Id), string.Join(", ", channelNames));
                }
                catch
                {
                }
            }

            if (!string.IsNullOrEmpty(requirement.DenialMessage))
                embed.AddField(Strings.RepDenialMessage(ctx.Guild.Id), requirement.DenialMessage);

            if (!string.IsNullOrEmpty(requirement.BypassRoles))
            {
                try
                {
                    var roleIds = JsonConvert.DeserializeObject<List<ulong>>(requirement.BypassRoles);
                    var roleNames = new List<string>();

                    foreach (var roleId in roleIds ?? [])
                    {
                        var role = ctx.Guild.GetRole(roleId);
                        if (role != null) roleNames.Add(role.Name);
                    }

                    if (roleNames.Any())
                        embed.AddField(Strings.RepBypassRoles(ctx.Guild.Id), string.Join(", ", roleNames));
                }
                catch
                {
                }
            }

            await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds or removes a bypass role on a command requirement.
        /// </summary>
        /// <param name="action">Whether to add or remove the role from the bypass list.</param>
        /// <param name="commandName">The command name.</param>
        /// <param name="role">The role that can bypass the requirement.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("bypass", "Adds or removes a role that bypasses a command's reputation requirement")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RepCommandBypass(
            [Summary("action", "Whether to add or remove the role")]
            BypassAction action,
            [Summary("name", "The command name")] string commandName,
            [Summary("role", "The role to add or remove from the bypass list")]
            IRole role)
        {
            var requirement =
                await commandRequirementsService.GetCommandRequirementAsync(ctx.Guild.Id,
                    commandName.ToLowerInvariant());

            if (requirement == null)
            {
                await ReplyErrorAsync(Strings.RepCommandRequirementNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var roleIds = ParseIdList(requirement.BypassRoles);
            string? roleIdJson;

            if (action == BypassAction.Add)
            {
                if (!roleIds.Contains(role.Id))
                    roleIds.Add(role.Id);

                roleIdJson = JsonConvert.SerializeObject(roleIds);
            }
            else
            {
                if (!roleIds.Remove(role.Id))
                {
                    await ReplyErrorAsync(Strings.RepRoleNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                roleIdJson = roleIds.Count > 0 ? JsonConvert.SerializeObject(roleIds) : null;
            }

            await commandRequirementsService.AddCommandRequirementAsync(ctx.Guild.Id, commandName.ToLowerInvariant(),
                requirement.MinReputation, requirement.RequiredRepType, requirement.RestrictedChannels,
                requirement.DenialMessage, roleIdJson, requirement.ShowInHelp);

            await ConfirmAsync(action == BypassAction.Add
                    ? Strings.RepCommandBypassAdded(ctx.Guild.Id, commandName, role.Name)
                    : Strings.RepCommandBypassRemoved(ctx.Guild.Id, commandName, role.Name))
                .ConfigureAwait(false);
        }

        private static List<ulong> ParseIdList(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return [];

            try
            {
                return JsonConvert.DeserializeObject<List<ulong>>(json) ?? [];
            }
            catch
            {
                return [];
            }
        }
    }
}