using System.IO;
using System.Net.Http;
using System.Text;
using DataModel;
using Discord.Commands;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Configs;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Reputation.Common;
using Mewdeko.Services.Strings;
using Newtonsoft.Json;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Reputation.Services;

/// <summary>
///     Service for interactive reputation system configuration management.
/// </summary>
public class RepConfigService : INService
{
    // Active configuration sessions
    private readonly ConcurrentDictionary<ulong, ConfigSession> activeSessions = new();
    private readonly BotConfig config;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<RepConfigService> logger;
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RepConfigService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="strings">The localized bot strings.</param>
    /// <param name="config">The bot configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public RepConfigService(
        IDataConnectionFactory dbFactory,
        GeneratedBotStrings strings,
        BotConfig config,
        ILogger<RepConfigService> logger)
    {
        this.dbFactory = dbFactory;
        this.strings = strings;
        this.config = config;
        this.logger = logger;
    }

    /// <summary>
    ///     Shows the interactive configuration menu for a guild.
    /// </summary>
    /// <param name="ctx">The command context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ShowConfigurationMenuAsync(ICommandContext ctx)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var repConfig = await GetOrCreateConfigAsync(db, ctx.Guild.Id);

        var session = new ConfigSession
        {
            GuildId = ctx.Guild.Id,
            UserId = ctx.User.Id,
            ChannelId = ctx.Channel.Id,
            Config = repConfig,
            CurrentCategory = ConfigCategory.Basic
        };

        activeSessions.TryAdd(ctx.Guild.Id, session);

        var componentsV2 = BuildConfigurationComponentsV2(ConfigCategory.Basic, repConfig)
            .WithTextDisplay($"# {strings.RepConfigTitle(ctx.Guild.Id)}")
            .WithTextDisplay($"*{GetCategoryDescription(ConfigCategory.Basic, ctx.Guild.Id)}*");

        var message =
            await ctx.Channel.SendMessageAsync(components: componentsV2.Build(), flags: MessageFlags.ComponentsV2);
        session.MessageId = message.Id;

        // Set up timeout to clean up session
        _ = Task.Delay(TimeSpan.FromMinutes(15)).ContinueWith(_ =>
        {
            activeSessions.TryRemove(ctx.Guild.Id, out var _);
        });
    }

    /// <summary>
    ///     Shows the current configuration status in a detailed embed.
    /// </summary>
    /// <param name="ctx">The command context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ShowConfigurationStatusAsync(ICommandContext ctx)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var repConfig = await GetOrCreateConfigAsync(db, ctx.Guild.Id);

        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(strings.RepConfigTitle(ctx.Guild.Id))
            .WithDescription(strings.RepConfigDescription(ctx.Guild.Id))
            .AddField(strings.RepConfigBasic(ctx.Guild.Id), BuildBasicConfigText(repConfig, ctx.Guild.Id))
            .AddField(strings.RepConfigCooldowns(ctx.Guild.Id), BuildCooldownConfigText(repConfig, ctx.Guild.Id))
            .AddField(strings.RepConfigRequirements(ctx.Guild.Id), BuildRequirementsConfigText(repConfig, ctx.Guild.Id))
            .AddField(strings.RepConfigNotifications(ctx.Guild.Id),
                BuildNotificationsConfigText(repConfig, ctx.Guild.Id))
            .AddField(strings.RepConfigAdvanced(ctx.Guild.Id), BuildAdvancedConfigText(repConfig, ctx.Guild.Id))
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        await ctx.Channel.SendMessageAsync(embed: embed);
    }

    /// <summary>
    ///     Exports the current reputation configuration to JSON.
    /// </summary>
    /// <param name="ctx">The command context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExportConfigurationAsync(ICommandContext ctx)
    {
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

            await ctx.Channel.SendFileAsync(stream, fileName,
                $"{config.SuccessEmote} {strings.RepExportSuccess(ctx.Guild.Id)}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error exporting reputation configuration for guild {GuildId}", ctx.Guild.Id);
            await ctx.Channel.SendErrorAsync(strings.RepExportError(ctx.Guild.Id), config);
        }
    }

    /// <summary>
    ///     Imports reputation configuration from uploaded JSON file.
    /// </summary>
    /// <param name="ctx">The command context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ImportConfigurationAsync(ICommandContext ctx)
    {
        var attachment = ctx.Message.Attachments.FirstOrDefault();
        if (attachment == null || !attachment.Filename.EndsWith(".json"))
        {
            await ctx.Channel.SendErrorAsync(strings.RepImportNoFile(ctx.Guild.Id), config);
            return;
        }

        try
        {
            using var httpClient = new HttpClient();
            var json = await httpClient.GetStringAsync(attachment.Url);
            var importData = JsonConvert.DeserializeObject<dynamic>(json);

            if (importData == null)
            {
                await ctx.Channel.SendErrorAsync(strings.RepImportInvalidFile(ctx.Guild.Id), config);
                return;
            }

            // TODO: Implement import logic with validation
            // This would deserialize and validate the configuration data
            // then update the database accordingly

            await ctx.Channel.SendConfirmAsync(strings.RepImportSuccess(ctx.Guild.Id));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error importing reputation configuration for guild {GuildId}", ctx.Guild.Id);
            await ctx.Channel.SendErrorAsync(strings.RepImportError(ctx.Guild.Id), config);
        }
    }

    /// <summary>
    ///     Builds the configuration embed for a specific category.
    /// </summary>
    /// <param name="repConfig">The reputation configuration.</param>
    /// <param name="category">The configuration category.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <returns>The configuration embed.</returns>
    private Embed BuildConfigurationEmbed(RepConfig repConfig, ConfigCategory category, ulong guildId)
    {
        var embed = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(strings.RepConfigInteractiveTitle(guildId))
            .WithDescription(GetCategoryDescription(category, guildId));

        return category switch
        {
            ConfigCategory.Basic => embed
                .AddField(strings.RepEnabled(guildId), repConfig.Enabled ? "✅" : "❌", true)
                .AddField(strings.RepEnableAnonymous(guildId), repConfig.EnableAnonymous ? "✅" : "❌", true)
                .AddField(strings.RepEnableNegative(guildId), repConfig.EnableNegativeRep ? "✅" : "❌", true)
                .Build(),

            ConfigCategory.Cooldowns => embed
                .AddField(strings.RepDefaultCooldown(guildId), $"{repConfig.DefaultCooldownMinutes} minutes", true)
                .AddField(strings.RepDailyLimitField(guildId), repConfig.DailyLimit.ToString(), true)
                .AddField(strings.RepWeeklyLimitField(guildId), repConfig.WeeklyLimit?.ToString() ?? "None", true)
                .Build(),

            ConfigCategory.Requirements => embed
                .AddField(strings.RepMinAccountAgeField(guildId), $"{repConfig.MinAccountAgeDays} days", true)
                .AddField(strings.RepMinMembershipField(guildId), $"{repConfig.MinServerMembershipHours} hours", true)
                .AddField(strings.RepMinMessagesField(guildId), repConfig.MinMessageCount.ToString(), true)
                .Build(),

            ConfigCategory.Notifications => embed
                .AddField(strings.RepNotificationChannel(guildId),
                    repConfig.NotificationChannel.HasValue ? $"<#{repConfig.NotificationChannel}>" : "None")
                .Build(),

            ConfigCategory.Advanced => embed
                .AddField(strings.RepEnableDecay(guildId), repConfig.EnableDecay ? "✅" : "❌", true)
                .AddField(strings.RepDecayType(guildId), repConfig.DecayType, true)
                .AddField(strings.RepDecayAmount(guildId), repConfig.DecayAmount.ToString(), true)
                .AddField(strings.RepDecayInactiveDays(guildId), $"{repConfig.DecayInactiveDays} days", true)
                .Build(),

            _ => embed.Build()
        };
    }

    /// <summary>
    ///     Builds the interactive components for configuration using Components V2.
    /// </summary>
    /// <param name="category">The current configuration category.</param>
    /// <param name="repConfig">The reputation configuration.</param>
    /// <returns>The message components.</returns>
    private ComponentBuilderV2 BuildConfigurationComponentsV2(ConfigCategory category, RepConfig repConfig)
    {
        var builder = new ComponentBuilderV2();

        // Add category-specific toggle components first
        switch (category)
        {
            case ConfigCategory.Basic:
                builder.WithContainer(
                    BuildToggleSection("System Enabled", "rep_toggle_enabled", repConfig.Enabled,
                        "Enable or disable the reputation system"),
                    BuildToggleSection("Anonymous Rep", "rep_toggle_anonymous", repConfig.EnableAnonymous,
                        "Allow giving reputation anonymously"),
                    BuildToggleSection("Negative Rep", "rep_toggle_negative", repConfig.EnableNegativeRep,
                        "Allow giving negative reputation")
                );
                break;

            case ConfigCategory.Cooldowns:
                builder.WithContainer(
                    new SectionBuilder()
                        .WithTextDisplay(
                            $"**Default Cooldown**\n{repConfig.DefaultCooldownMinutes} minutes between giving reputation")
                        .WithAccessory(new ButtonBuilder("Edit", "rep_edit_cooldown", ButtonStyle.Secondary,
                            emote: "⏱️".ToIEmote())),
                    new SectionBuilder()
                        .WithTextDisplay($"**Daily Limit**\n{repConfig.DailyLimit} reputation per day")
                        .WithAccessory(new ButtonBuilder("Edit", "rep_edit_daily_limit", ButtonStyle.Secondary,
                            emote: "📊".ToIEmote())),
                    new SectionBuilder()
                        .WithTextDisplay(
                            $"**Weekly Limit**\n{repConfig.WeeklyLimit?.ToString() ?? "No limit"} reputation per week")
                        .WithAccessory(new ButtonBuilder("Edit", "rep_edit_weekly_limit", ButtonStyle.Secondary,
                            emote: "📈".ToIEmote()))
                );
                break;

            case ConfigCategory.Requirements:
                builder.WithContainer(
                    new SectionBuilder()
                        .WithTextDisplay(
                            $"**Minimum Account Age**\n{repConfig.MinAccountAgeDays} days old to give reputation")
                        .WithAccessory(new ButtonBuilder("Edit", "rep_edit_account_age", ButtonStyle.Secondary,
                            emote: "👤".ToIEmote())),
                    new SectionBuilder()
                        .WithTextDisplay(
                            $"**Minimum Server Membership**\n{repConfig.MinServerMembershipHours} hours in server required")
                        .WithAccessory(new ButtonBuilder("Edit", "rep_edit_membership", ButtonStyle.Secondary,
                            emote: "🏠".ToIEmote())),
                    new SectionBuilder()
                        .WithTextDisplay($"**Minimum Messages**\n{repConfig.MinMessageCount} messages sent required")
                        .WithAccessory(new ButtonBuilder("Edit", "rep_edit_messages", ButtonStyle.Secondary,
                            emote: "💬".ToIEmote()))
                );
                break;

            case ConfigCategory.Notifications:
                var channelText = repConfig.NotificationChannel.HasValue
                    ? $"<#{repConfig.NotificationChannel}>"
                    : "None set";
                builder.WithContainer(
                        new SectionBuilder()
                            .WithTextDisplay(
                                $"**Notification Channel**\n{channelText}\nWhere reputation notifications are sent")
                            .WithAccessory(new ButtonBuilder("Clear", "rep_clear_notification_channel",
                                ButtonStyle.Secondary, emote: "🗑️".ToIEmote()))
                    )
                    .WithActionRow([
                        new SelectMenuBuilder()
                            .WithPlaceholder("Select notification channel...")
                            .WithCustomId("rep_select_notification_channel")
                            .WithChannelTypes(ChannelType.Text, ChannelType.News)
                    ]);
                break;

            case ConfigCategory.Advanced:
                builder.WithContainer(
                        BuildToggleSection("Reputation Decay", "rep_toggle_decay", repConfig.EnableDecay,
                            "Enable automatic reputation decay over time"),
                        new SectionBuilder()
                            .WithTextDisplay($"**Decay Amount**\n{repConfig.DecayAmount} reputation lost per decay")
                            .WithAccessory(new ButtonBuilder("Edit", "rep_edit_decay_amount", ButtonStyle.Secondary,
                                emote: "📉".ToIEmote())),
                        new SectionBuilder()
                            .WithTextDisplay(
                                $"**Inactive Days**\n{repConfig.DecayInactiveDays} days before decay starts")
                            .WithAccessory(new ButtonBuilder("Edit", "rep_edit_inactive_days", ButtonStyle.Secondary,
                                emote: "📅".ToIEmote()))
                    )
                    .WithActionRow([
                        new SelectMenuBuilder()
                            .WithPlaceholder($"Decay Schedule: {repConfig.DecayType}")
                            .WithCustomId("rep_select_decay_type")
                            .AddOption("Daily", "daily", "Decay happens every day",
                                repConfig.DecayType == "daily" ? "✅".ToIEmote() : null)
                            .AddOption("Weekly", "weekly", "Decay happens every week",
                                repConfig.DecayType == "weekly" ? "✅".ToIEmote() : null)
                            .AddOption("Monthly", "monthly", "Decay happens every month",
                                repConfig.DecayType == "monthly" ? "✅".ToIEmote() : null)
                            .AddOption("Fixed", "fixed", "Decay after fixed period",
                                repConfig.DecayType == "fixed" ? "✅".ToIEmote() : null)
                            .AddOption("Percentage", "percentage", "Percentage-based decay",
                                repConfig.DecayType == "percentage" ? "✅".ToIEmote() : null)
                    ]);
                break;
        }

        // Action buttons
        builder.WithActionRow([
            new ButtonBuilder("Save Changes", "rep_config_save", ButtonStyle.Success, emote: "💾".ToIEmote()),
            new ButtonBuilder("Reset to Defaults", "rep_config_reset", ButtonStyle.Danger, emote: "🔄".ToIEmote()),
            new ButtonBuilder("Export Config", "rep_config_export", ButtonStyle.Secondary, emote: "📤".ToIEmote()),
            new ButtonBuilder("Close", "rep_config_close", ButtonStyle.Secondary, emote: "❌".ToIEmote())
        ]);

        // Category selection dropdown at the bottom
        var categorySelect = new SelectMenuBuilder()
            .WithPlaceholder("Select configuration category...")
            .WithCustomId("rep_config_category")
            .AddOption("Basic Settings", "basic", "Enable/disable core features",
                category == ConfigCategory.Basic ? "✅".ToIEmote() : null)
            .AddOption("Cooldowns & Limits", "cooldowns", "Configure timing and limits",
                category == ConfigCategory.Cooldowns ? "✅".ToIEmote() : null)
            .AddOption("Requirements", "requirements", "User requirements to give rep",
                category == ConfigCategory.Requirements ? "✅".ToIEmote() : null)
            .AddOption("Notifications", "notifications", "Configure notification settings",
                category == ConfigCategory.Notifications ? "✅".ToIEmote() : null)
            .AddOption("Advanced", "advanced", "Advanced features like decay",
                category == ConfigCategory.Advanced ? "✅".ToIEmote() : null);

        builder.WithActionRow([categorySelect]);

        return builder;
    }

    /// <summary>
    ///     Builds a toggle section with a button indicator.
    /// </summary>
    /// <param name="title">The title of the setting.</param>
    /// <param name="customId">The custom ID for the toggle button.</param>
    /// <param name="isEnabled">Whether the setting is currently enabled.</param>
    /// <param name="description">The description of the setting.</param>
    /// <returns>A section builder with toggle functionality.</returns>
    private SectionBuilder BuildToggleSection(string title, string customId, bool isEnabled, string description)
    {
        var toggleButton = new ButtonBuilder()
            .WithCustomId(customId)
            .WithLabel(isEnabled ? "ON" : "OFF")
            .WithStyle(isEnabled ? ButtonStyle.Success : ButtonStyle.Secondary)
            .WithEmote(isEnabled ? "✅".ToIEmote() : "❌".ToIEmote());

        return new SectionBuilder()
            .WithTextDisplay($"**{title}**\n{description}")
            .WithAccessory(toggleButton);
    }

    /// <summary>
    ///     Gets the description for a configuration category.
    /// </summary>
    /// <param name="category">The configuration category.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <returns>The category description.</returns>
    private string GetCategoryDescription(ConfigCategory category, ulong guildId)
    {
        return category switch
        {
            ConfigCategory.Basic => strings.RepConfigBasicDesc(guildId),
            ConfigCategory.Cooldowns => strings.RepConfigCooldownsDesc(guildId),
            ConfigCategory.Requirements => strings.RepConfigRequirementsDesc(guildId),
            ConfigCategory.Notifications => strings.RepConfigNotificationsDesc(guildId),
            ConfigCategory.Advanced => strings.RepConfigAdvancedDesc(guildId),
            _ => "Configuration options"
        };
    }

    /// <summary>
    ///     Gets or creates a reputation configuration for a guild.
    /// </summary>
    /// <param name="db">The database connection.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The reputation configuration.</returns>
    private static async Task<RepConfig> GetOrCreateConfigAsync(MewdekoDb db, ulong guildId)
    {
        var repConfig = await db.RepConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (repConfig != null) return repConfig;
        repConfig = new RepConfig
        {
            GuildId = guildId, DateAdded = DateTime.UtcNow
        };
        await db.InsertAsync(repConfig);

        return repConfig;
    }

    /// <summary>
    ///     Builds the basic configuration text display.
    /// </summary>
    /// <param name="repConfig">The reputation configuration.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <returns>The formatted text.</returns>
    private string BuildBasicConfigText(RepConfig repConfig, ulong guildId)
    {
        return $"**{strings.RepEnabled(guildId)}:** {(repConfig.Enabled ? "✅ Enabled" : "❌ Disabled")}\n" +
               $"**{strings.RepEnableAnonymous(guildId)}:** {(repConfig.EnableAnonymous ? "✅ Enabled" : "❌ Disabled")}\n" +
               $"**{strings.RepEnableNegative(guildId)}:** {(repConfig.EnableNegativeRep ? "✅ Enabled" : "❌ Disabled")}";
    }

    /// <summary>
    ///     Builds the cooldown configuration text display.
    /// </summary>
    /// <param name="repConfig">The reputation configuration.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <returns>The formatted text.</returns>
    private string BuildCooldownConfigText(RepConfig repConfig, ulong guildId)
    {
        return $"**{strings.RepDefaultCooldown(guildId)}:** {repConfig.DefaultCooldownMinutes} minutes\n" +
               $"**{strings.RepDailyLimitField(guildId)}:** {repConfig.DailyLimit}\n" +
               $"**{strings.RepWeeklyLimitField(guildId)}:** {repConfig.WeeklyLimit?.ToString() ?? "None"}";
    }

    /// <summary>
    ///     Builds the requirements configuration text display.
    /// </summary>
    /// <param name="repConfig">The reputation configuration.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <returns>The formatted text.</returns>
    private string BuildRequirementsConfigText(RepConfig repConfig, ulong guildId)
    {
        return $"**{strings.RepMinAccountAgeField(guildId)}:** {repConfig.MinAccountAgeDays} days\n" +
               $"**{strings.RepMinMembershipField(guildId)}:** {repConfig.MinServerMembershipHours} hours\n" +
               $"**{strings.RepMinMessagesField(guildId)}:** {repConfig.MinMessageCount}";
    }

    /// <summary>
    ///     Builds the notifications configuration text display.
    /// </summary>
    /// <param name="repConfig">The reputation configuration.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <returns>The formatted text.</returns>
    private string BuildNotificationsConfigText(RepConfig repConfig, ulong guildId)
    {
        return $"**{strings.RepNotificationChannel(guildId)}:** " +
               (repConfig.NotificationChannel.HasValue ? $"<#{repConfig.NotificationChannel}>" : "None");
    }

    /// <summary>
    ///     Builds the advanced configuration text display.
    /// </summary>
    /// <param name="repConfig">The reputation configuration.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <returns>The formatted text.</returns>
    private string BuildAdvancedConfigText(RepConfig repConfig, ulong guildId)
    {
        return $"**{strings.RepEnableDecay(guildId)}:** {(repConfig.EnableDecay ? "✅ Enabled" : "❌ Disabled")}\n" +
               $"**{strings.RepDecayType(guildId)}:** {repConfig.DecayType}\n" +
               $"**{strings.RepDecayAmount(guildId)}:** {repConfig.DecayAmount}\n" +
               $"**{strings.RepDecayInactiveDays(guildId)}:** {repConfig.DecayInactiveDays} days";
    }

    #region Component Interaction Handlers

    /// <summary>
    ///     Handles category selection from the configuration menu.
    /// </summary>
    /// <param name="ctx">The interaction context.</param>
    /// <param name="category">The selected category.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task HandleCategorySelectionAsync(IInteractionContext ctx, string category)
    {
        if (!Enum.TryParse<ConfigCategory>(category, true, out var configCategory))
        {
            await ctx.Interaction.RespondAsync(strings.RepConfigInvalidCategory(ctx.Guild.Id), ephemeral: true);
            return;
        }

        await ShowConfigurationMenuAsync(ctx, configCategory);
    }

    /// <summary>
    ///     Handles saving configuration changes.
    /// </summary>
    /// <param name="ctx">The interaction context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task HandleSaveAsync(IInteractionContext ctx)
    {
        await ctx.Interaction.RespondAsync(strings.RepConfigSaved(ctx.Guild.Id), ephemeral: true);
    }

    /// <summary>
    ///     Handles resetting configuration to defaults.
    /// </summary>
    /// <param name="ctx">The interaction context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task HandleResetAsync(IInteractionContext ctx)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var repConfig = await GetOrCreateConfigAsync(db, ctx.Guild.Id);

        // Reset to default values
        repConfig.Enabled = true;
        repConfig.EnableAnonymous = false;
        repConfig.EnableNegativeRep = false;
        repConfig.DefaultCooldownMinutes = 60;
        repConfig.DailyLimit = 5;
        repConfig.WeeklyLimit = null;
        repConfig.MinAccountAgeDays = 0;
        repConfig.MinServerMembershipHours = 0;
        repConfig.MinMessageCount = 0;
        repConfig.NotificationChannel = null;
        repConfig.EnableDecay = false;
        repConfig.DecayType = "daily";
        repConfig.DecayAmount = 1;
        repConfig.DecayInactiveDays = 30;

        await db.UpdateAsync(repConfig);

        await ctx.Interaction.RespondAsync(strings.RepConfigReset(ctx.Guild.Id), ephemeral: true);
    }

    /// <summary>
    ///     Handles exporting configuration.
    /// </summary>
    /// <param name="ctx">The interaction context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task HandleExportAsync(IInteractionContext ctx)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var repConfig = await GetOrCreateConfigAsync(db, ctx.Guild.Id);

        var exportData = new
        {
            GuildId = ctx.Guild.Id,
            GuildName = ctx.Guild.Name,
            ExportedAt = DateTime.UtcNow,
            Configuration = new
            {
                repConfig.Enabled,
                repConfig.EnableAnonymous,
                repConfig.EnableNegativeRep,
                repConfig.DefaultCooldownMinutes,
                repConfig.DailyLimit,
                repConfig.WeeklyLimit,
                repConfig.MinAccountAgeDays,
                repConfig.MinServerMembershipHours,
                repConfig.MinMessageCount,
                repConfig.NotificationChannel,
                repConfig.EnableDecay,
                repConfig.DecayType,
                repConfig.DecayAmount,
                repConfig.DecayInactiveDays
            }
        };

        var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
        var bytes = Encoding.UTF8.GetBytes(json);

        using var stream = new MemoryStream(bytes);
        var attachment = new FileAttachment(stream, $"reputation-config-{ctx.Guild.Id}.json");

        await ctx.Interaction.RespondWithFileAsync(attachment, "Reputation configuration exported!", ephemeral: true);
    }

    /// <summary>
    ///     Handles toggling configuration settings.
    /// </summary>
    /// <param name="ctx">The interaction context.</param>
    /// <param name="setting">The setting to toggle.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task HandleToggleAsync(IInteractionContext ctx, string setting)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var repConfig = await GetOrCreateConfigAsync(db, ctx.Guild.Id);

        var currentCategory = ConfigCategory.Basic; // Default to basic for toggles

        switch (setting.ToLower())
        {
            case "enabled":
                repConfig.Enabled = !repConfig.Enabled;
                break;
            case "anonymous":
                repConfig.EnableAnonymous = !repConfig.EnableAnonymous;
                break;
            case "negative":
                repConfig.EnableNegativeRep = !repConfig.EnableNegativeRep;
                break;
            case "decay":
                repConfig.EnableDecay = !repConfig.EnableDecay;
                currentCategory = ConfigCategory.Advanced;
                break;
            default:
                await ctx.Interaction.RespondAsync(strings.RepConfigUnknownSetting(ctx.Guild.Id), ephemeral: true);
                return;
        }

        await db.UpdateAsync(repConfig);

        // Rebuild and update the UI with the new state
        await ShowConfigurationMenuAsync(ctx, currentCategory);
    }

    /// <summary>
    ///     Handles editing configuration values via modals.
    /// </summary>
    /// <param name="ctx">The interaction context.</param>
    /// <param name="setting">The setting to edit.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task HandleEditAsync(IInteractionContext ctx, string setting)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var repConfig = await GetOrCreateConfigAsync(db, ctx.Guild.Id);

        var (title, label, currentValue, placeholder) = setting.ToLower() switch
        {
            "cooldown" => ("Edit Default Cooldown", "Minutes between giving reputation",
                repConfig.DefaultCooldownMinutes.ToString(), "60"),
            "daily_limit" => ("Edit Daily Limit", "Reputation per day", repConfig.DailyLimit.ToString(), "5"),
            "weekly_limit" => ("Edit Weekly Limit", "Reputation per week (0 = no limit)",
                repConfig.WeeklyLimit?.ToString() ?? "0", "35"),
            "account_age" => ("Edit Account Age Requirement", "Minimum days old",
                repConfig.MinAccountAgeDays.ToString(), "0"),
            "membership" => ("Edit Membership Requirement", "Minimum hours in server",
                repConfig.MinServerMembershipHours.ToString(), "0"),
            "messages" => ("Edit Message Requirement", "Minimum messages sent", repConfig.MinMessageCount.ToString(),
                "0"),
            "decay_amount" => ("Edit Decay Amount", "Reputation lost per decay", repConfig.DecayAmount.ToString(), "1"),
            "inactive_days" => ("Edit Inactive Days", "Days before decay starts",
                repConfig.DecayInactiveDays.ToString(), "30"),
            _ => ("Edit Setting", "Value", "0", "0")
        };

        var modal = new ModalBuilder()
            .WithTitle(title)
            .WithCustomId($"rep_modal_{setting}")
            .AddTextInput(label, "rep_input", TextInputStyle.Short, placeholder, 1, 10, value: currentValue);

        await ctx.Interaction.RespondWithModalAsync(modal.Build());
    }

    /// <summary>
    ///     Handles channel selection for notifications.
    /// </summary>
    /// <param name="ctx">The interaction context.</param>
    /// <param name="channelId">The selected channel ID.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task HandleChannelSelectAsync(IInteractionContext ctx, ulong? channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var repConfig = await GetOrCreateConfigAsync(db, ctx.Guild.Id);

        repConfig.NotificationChannel = channelId;
        await db.UpdateAsync(repConfig);

        await ShowConfigurationMenuAsync(ctx, ConfigCategory.Notifications);
    }

    /// <summary>
    ///     Handles decay type selection.
    /// </summary>
    /// <param name="ctx">The interaction context.</param>
    /// <param name="decayType">The selected decay type.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task HandleDecayTypeSelectAsync(IInteractionContext ctx, string decayType)
    {
        if (string.IsNullOrEmpty(decayType)) return;

        await using var db = await dbFactory.CreateConnectionAsync();
        var repConfig = await GetOrCreateConfigAsync(db, ctx.Guild.Id);

        repConfig.DecayType = decayType;
        await db.UpdateAsync(repConfig);

        await ShowConfigurationMenuAsync(ctx, ConfigCategory.Advanced);
    }

    /// <summary>
    ///     Handles modal submission for editing configuration values.
    /// </summary>
    /// <param name="ctx">The interaction context.</param>
    /// <param name="setting">The setting being edited.</param>
    /// <param name="value">The new value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task HandleModalSubmitAsync(IInteractionContext ctx, string setting, string value)
    {
        if (!int.TryParse(value, out var intValue) || intValue < 0)
        {
            await ctx.Interaction.RespondAsync(strings.RepConfigInvalidPositiveNumber(ctx.Guild.Id), ephemeral: true);
            return;
        }

        await using var db = await dbFactory.CreateConnectionAsync();
        var repConfig = await GetOrCreateConfigAsync(db, ctx.Guild.Id);

        var category = setting.ToLower() switch
        {
            "cooldown" => ConfigCategory.Cooldowns,
            "daily_limit" => ConfigCategory.Cooldowns,
            "weekly_limit" => ConfigCategory.Cooldowns,
            "account_age" => ConfigCategory.Requirements,
            "membership" => ConfigCategory.Requirements,
            "messages" => ConfigCategory.Requirements,
            "decay_amount" => ConfigCategory.Advanced,
            "inactive_days" => ConfigCategory.Advanced,
            _ => ConfigCategory.Basic
        };

        switch (setting.ToLower())
        {
            case "cooldown":
                repConfig.DefaultCooldownMinutes = intValue;
                break;
            case "daily_limit":
                repConfig.DailyLimit = intValue;
                break;
            case "weekly_limit":
                repConfig.WeeklyLimit = intValue == 0 ? null : intValue;
                break;
            case "account_age":
                repConfig.MinAccountAgeDays = intValue;
                break;
            case "membership":
                repConfig.MinServerMembershipHours = intValue;
                break;
            case "messages":
                repConfig.MinMessageCount = intValue;
                break;
            case "decay_amount":
                repConfig.DecayAmount = intValue;
                break;
            case "inactive_days":
                repConfig.DecayInactiveDays = intValue;
                break;
        }

        await db.UpdateAsync(repConfig);

        // Update the original message instead of responding
        try
        {
            await UpdateConfigurationMenuAsync(ctx, category);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    ///     Updates the configuration menu message with the specified category.
    /// </summary>
    /// <param name="ctx">The interaction context.</param>
    /// <param name="category">The configuration category to display.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task UpdateConfigurationMenuAsync(IInteractionContext ctx, ConfigCategory category)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var repConfig = await GetOrCreateConfigAsync(db, ctx.Guild.Id);

        var componentsV2 = BuildConfigurationComponentsV2(category, repConfig)
            .WithTextDisplay($"# {strings.RepConfigTitle(ctx.Guild.Id)}")
            .WithTextDisplay($"*{GetCategoryDescription(category, ctx.Guild.Id)}*");

        await ctx.Interaction.RespondAsync(components: componentsV2.Build());
    }

    /// <summary>
    ///     Shows the configuration menu with the specified category.
    /// </summary>
    /// <param name="ctx">The interaction context.</param>
    /// <param name="category">The configuration category to display.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ShowConfigurationMenuAsync(IInteractionContext ctx, ConfigCategory category)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var repConfig = await GetOrCreateConfigAsync(db, ctx.Guild.Id);

        var componentsV2 = BuildConfigurationComponentsV2(category, repConfig)
            .WithTextDisplay($"# {strings.RepConfigTitle(ctx.Guild.Id)}")
            .WithTextDisplay($"*{GetCategoryDescription(category, ctx.Guild.Id)}*");

        await ((SocketMessageComponent)ctx.Interaction).UpdateAsync(x =>
        {
            x.Components = componentsV2.Build();
            x.Flags = MessageFlags.ComponentsV2;
            x.Embed = null; // Clear embed since we're using Components V2
        });
    }

    #endregion
}