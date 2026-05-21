using System.Text.Json;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Tickets.Common;
using Mewdeko.Modules.Tickets.Services;

namespace Mewdeko.Modules.Tickets;

public partial class TicketsSlash
{
    /// <summary>
    ///     Wizard step enumeration
    /// </summary>
    public enum WizardStep
    {
        /// <summary>
        ///     Selecting setup method
        /// </summary>
        SelectMethod,

        /// <summary>
        ///     Template selection
        /// </summary>
        TemplateSelection,

        /// <summary>
        ///     Custom panel design
        /// </summary>
        PanelDesign,

        /// <summary>
        ///     Component setup
        /// </summary>
        ComponentSetup,

        /// <summary>
        ///     Permission configuration
        /// </summary>
        PermissionSetup,

        /// <summary>
        ///     Final review
        /// </summary>
        FinalReview
    }

    /// <summary>
    ///     Improved ticket setup commands with wizard-style interface
    /// </summary>
    [Group("setup", "Setup your ticket system with guided wizards")]
    public class TicketSetupWizard : MewdekoSlashModuleBase<TicketService>
    {
        private readonly IDataCache cache;
        private readonly InteractiveService interactivity;
        private readonly ILogger<TicketSetupWizard> logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TicketSetupWizard" /> class
        /// </summary>
        /// <param name="interactivity">The interactive service</param>
        /// <param name="cache">The cache service</param>
        /// <param name="logger">The logger instance for structured logging.</param>
        public TicketSetupWizard(InteractiveService interactivity, IDataCache cache, ILogger<TicketSetupWizard> logger)
        {
            this.interactivity = interactivity;
            this.cache = cache;
            this.logger = logger;
        }

        /// <summary>
        ///     Starts a complete wizard for setting up a ticket panel
        /// </summary>
        /// <param name="channel">The channel to create the panel in</param>
        [SlashCommand("wizard", "Start an interactive wizard to set up your ticket system")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task StartWizard(
            [Summary("channel", "Channel to create the panel in")]
            ITextChannel channel)
        {
            var wizardId = Guid.NewGuid().ToString();
            var wizardState = new WizardState
            {
                UserId = ctx.User.Id, GuildId = ctx.Guild.Id, ChannelId = channel.Id, Step = WizardStep.SelectMethod
            };

            await cache.Redis.GetDatabase().StringSetAsync($"wizard:{wizardId}",
                JsonSerializer.Serialize(wizardState), TimeSpan.FromMinutes(30));

            var embed = new EmbedBuilder()
                .WithTitle(Strings.TicketWizardTitle(ctx.Guild.Id))
                .WithDescription(Strings.TicketWizardDescription(ctx.Guild.Id, channel.Mention))
                .AddField("📋 Templates", "Use pre-made templates for common use cases")
                .AddField("🎨 Custom Setup", "Create a completely custom panel")
                .AddField("⚡ Quick Setup", "Fast setup with basic options")
                .WithColor(Color.Blue);

            var components = new ComponentBuilder()
                .WithButton("📋 Browse Templates", $"wizard_templates:{wizardId}", ButtonStyle.Success)
                .WithButton("🎨 Custom Setup", $"wizard_custom:{wizardId}")
                .WithButton("⚡ Quick Setup", $"wizard_quick:{wizardId}", ButtonStyle.Secondary)
                .Build();

            await RespondAsync(embed: embed.Build(), components: components, ephemeral: true);
        }

        /// <summary>
        ///     Quick button creation for existing panels
        /// </summary>
        /// <param name="panelId">The panel to add the button to</param>
        [SlashCommand("quick-button", "Quickly add a button to an existing panel")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task QuickButton(
            [Summary("panel-id", "Panel message ID to add button to")]
            ulong panelId)
        {
            var panel = await Service.GetPanelAsync(panelId);
            if (panel == null || panel.GuildId != ctx.Guild.Id)
            {
                await RespondAsync($"{Config.ErrorEmote} Panel not found in this server!", ephemeral: true);
                return;
            }

            await RespondWithModalAsync<QuickButtonSetupModal>($"quick_button:{panelId}");
        }

        /// <summary>
        ///     Quick select menu creation for existing panels
        /// </summary>
        /// <param name="panelId">The panel to add the select menu to</param>
        [SlashCommand("quick-menu", "Quickly add a select menu to an existing panel")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task QuickSelectMenu(
            [Summary("panel-id", "Panel message ID to add select menu to")]
            ulong panelId)
        {
            var panel = await Service.GetPanelAsync(panelId);
            if (panel == null || panel.GuildId != ctx.Guild.Id)
            {
                await RespondAsync($"{Config.ErrorEmote} Panel not found in this server!", ephemeral: true);
                return;
            }

            await RespondWithModalAsync<QuickSelectMenuSetupModal>($"quick_menu:{panelId}");
        }

        /// <summary>
        ///     Create a complete panel from a template
        /// </summary>
        /// <param name="templateId">The template ID to use</param>
        /// <param name="channel">The channel to create the panel in</param>
        [SlashCommand("from-template", "Create a panel from a pre-made template")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task CreateFromTemplate(
            [Summary("template", "Template to use")]
            [Choice("Basic Support", "basic_support")]
            [Choice("Department Support", "department")]
            [Choice("Gaming Server", "gaming")]
            [Choice("Business", "business")]
            [Choice("Mod Applications", "mod_application")]
            string templateId,
            [Summary("channel", "Channel to create the panel in")]
            ITextChannel channel)
        {
            await DeferAsync(true);

            try
            {
                var template = TicketTemplates.GetAllTemplates().FirstOrDefault(t => t.Id == templateId);
                if (template == null)
                {
                    await FollowupAsync($"{Config.ErrorEmote} Template not found!", ephemeral: true);
                    return;
                }

                // Create the panel
                var embedJson = ConfigurationParser.CreateEmbedJson(template.EmbedConfig);
                var panel = await Service.CreatePanelAsync(channel, embedJson);

                var createdComponents = new List<string>();

                // Add buttons from template
                foreach (var buttonTemplate in template.Buttons)
                {
                    string modalJson = null;
                    if (!string.IsNullOrEmpty(buttonTemplate.ModalConfig))
                    {
                        modalJson = ConfigurationParser.CreateModalJson(buttonTemplate.ModalConfig);
                    }


                    var settings = ConfigurationParser.ParseKeyValuePairs(buttonTemplate.Settings);

                    TimeSpan? autoCloseTime = null;
                    if (settings.ContainsKey("auto_close_hours") &&
                        int.TryParse(settings["auto_close_hours"], out var hours))
                        autoCloseTime = TimeSpan.FromHours(hours);

                    TimeSpan? responseTime = null;
                    if (settings.ContainsKey("response_time_minutes") &&
                        int.TryParse(settings["response_time_minutes"], out var minutes))
                        responseTime = TimeSpan.FromMinutes(minutes);

                    var saveTranscripts = settings.ContainsKey("save_transcripts") &&
                                          settings["save_transcripts"].ToLower() is "true" or "yes";

                    await Service.AddButtonAsync(
                        panel,
                        buttonTemplate.Label,
                        buttonTemplate.Emoji,
                        buttonTemplate.Style,
                        modalJson: modalJson ?? string.Empty,
                        autoCloseTime: autoCloseTime,
                        requiredResponseTime: responseTime
                    );

                    createdComponents.Add($"🔘 {buttonTemplate.Label}");
                }

                // Add select menus from template
                foreach (var menuTemplate in template.SelectMenus)
                {
                    var menu = await Service.AddSelectMenuAsync(panel, menuTemplate.Placeholder, "",
                        updateComponents: false);

                    var optionLines = menuTemplate.Options.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var optionLine in optionLines)
                    {
                        var parts = optionLine.Split('|', StringSplitOptions.TrimEntries);
                        if (parts.Length >= 2)
                        {
                            var label = parts[0];
                            var emoji = parts.Length > 1 ? parts[1] : null;
                            var description = parts.Length > 2 ? parts[2] : null;
                            logger.LogInformation(label);
                            await Service.AddSelectOptionAsync(menu, label, $"option_{Guid.NewGuid():N}", description,
                                emoji, updateComponents: false);
                        }
                    }

                    createdComponents.Add($"📜 {menuTemplate.Placeholder}");
                }

                await Service.UpdatePanelComponentsAsync(panel);

                var successEmbed = new EmbedBuilder()
                    .WithTitle(Strings.TemplateAppliedSuccess(ctx.Guild.Id, Config.SuccessEmote))
                    .WithDescription(
                        Strings.TicketPanelCreated(ctx.Guild.Id, channel.Mention))
                    .AddField("📋 Panel ID", panel.MessageId.ToString())
                    .AddField("🧩 Components Created", string.Join("\n", createdComponents))
                    .WithColor(Color.Green);

                await FollowupAsync(embed: successEmbed.Build(), ephemeral: true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating panel from template {TemplateId}", templateId);
                await FollowupAsync($"{Config.ErrorEmote} An error occurred while creating the panel from template.",
                    ephemeral: true);
            }
        }

        /// <summary>
        ///     Shows available templates
        /// </summary>
        [SlashCommand("templates", "Browse available ticket panel templates")]
        [RequireContext(ContextType.Guild)]
        public async Task ShowTemplates()
        {
            var templates = TicketTemplates.GetAllTemplates();

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(templates.Count - 1)
                .WithDefaultEmotes()
                .Build();

            await interactivity.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(10));

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask;
                var template = templates[page];

                var pageBuilder = new PageBuilder()
                    .WithTitle(Strings.TemplateTitle(ctx.Guild.Id, template.Name))
                    .WithDescription(template.Description)
                    .WithColor(Color.Blue);

                // Parse embed config for preview
                var embedSettings = ConfigurationParser.ParseKeyValuePairs(template.EmbedConfig);
                pageBuilder.AddField("📝 Panel Embed",
                    $"**Title:** {embedSettings.GetValueOrDefault("title", "Support Tickets")}\n" +
                    $"**Description:** {embedSettings.GetValueOrDefault("description", "Click a button below...")}\n" +
                    $"**Color:** {embedSettings.GetValueOrDefault("color", "blue")}");

                if (template.Buttons.Any())
                {
                    var buttonPreview = string.Join("\n", template.Buttons.Select(b =>
                        $"{b.Emoji} {b.Label} ({b.Style})"));
                    pageBuilder.AddField("🔘 Buttons", buttonPreview);
                }

                if (template.SelectMenus.Any())
                {
                    var menuPreview = string.Join("\n", template.SelectMenus.Select(m =>
                        $"📜 {m.Placeholder} ({m.Options.Split('\n').Length} options)"));
                    pageBuilder.AddField("📜 Select Menus", menuPreview);
                }

                pageBuilder.AddField("🚀 Usage",
                    $"`/ticket-setup from-template template:{template.Id} channel:#your-channel`");

                return pageBuilder;
            }
        }

        #region Component Interaction Handlers

        /// <summary>
        ///     Handles template browsing from wizard
        /// </summary>
        /// <param name="wizardId">The wizard session ID</param>
        [ComponentInteraction("wizard_templates:*", true)]
        public async Task HandleTemplateWizard(string wizardId)
        {
            var wizardData = await cache.Redis.GetDatabase().StringGetAsync($"wizard:{wizardId}");
            if (!wizardData.HasValue)
            {
                await RespondAsync($"{Config.ErrorEmote} Wizard session expired. Please start over.", ephemeral: true);
                return;
            }

            var templates = TicketTemplates.GetAllTemplates();
            var selectMenu = new SelectMenuBuilder()
                .WithCustomId($"wizard_select_template:{wizardId}")
                .WithPlaceholder("Choose a template...");

            foreach (var template in templates)
            {
                selectMenu.AddOption(template.Name, template.Id, template.Description.Length > 100
                    ? template.Description[..97] + "..."
                    : template.Description);
            }

            var embed = new EmbedBuilder()
                .WithTitle(Strings.ChooseTemplate(ctx.Guild.Id))
                .WithDescription(Strings.SelectTemplateDescription(ctx.Guild.Id))
                .WithColor(Color.Green);

            var components = new ComponentBuilder()
                .WithSelectMenu(selectMenu)
                .WithButton("⬅️ Back", $"wizard_back:{wizardId}", ButtonStyle.Secondary)
                .Build();

            await RespondAsync(embed: embed.Build(), components: components, ephemeral: true);
        }

        /// <summary>
        ///     Handles template selection in wizard
        /// </summary>
        /// <param name="wizardId">The wizard session ID</param>
        /// <param name="values">Selected template IDs</param>
        [ComponentInteraction("wizard_select_template:*", true)]
        public async Task HandleTemplateSelection(string wizardId, string[] values)
        {
            var wizardData = await cache.Redis.GetDatabase().StringGetAsync($"wizard:{wizardId}");
            if (!wizardData.HasValue)
            {
                await RespondAsync($"{Config.ErrorEmote} Wizard session expired. Please start over.", ephemeral: true);
                return;
            }

            var state = JsonSerializer.Deserialize<WizardState>((string)wizardData);
            var selectedTemplate = values.FirstOrDefault();
            var template = TicketTemplates.GetAllTemplates().FirstOrDefault(t => t.Id == selectedTemplate);

            if (template == null)
            {
                await RespondAsync($"{Config.ErrorEmote} Invalid template selected!", ephemeral: true);
                return;
            }

            // Store template selection
            state.Data["selectedTemplate"] = selectedTemplate;
            await cache.Redis.GetDatabase().StringSetAsync($"wizard:{wizardId}",
                JsonSerializer.Serialize(state), TimeSpan.FromMinutes(30));

            // Show template preview and confirm
            var embedSettings = ConfigurationParser.ParseKeyValuePairs(template.EmbedConfig);
            var embed = new EmbedBuilder()
                .WithTitle(Strings.TemplatePreview(ctx.Guild.Id, template.Name))
                .WithDescription(template.Description)
                .AddField("📝 Panel Settings",
                    $"**Title:** {embedSettings.GetValueOrDefault("title")}\n" +
                    $"**Description:** {embedSettings.GetValueOrDefault("description")}\n" +
                    $"**Color:** {embedSettings.GetValueOrDefault("color")}")
                .WithColor(Color.Blue);

            if (template.Buttons.Any())
            {
                var buttonList = string.Join("\n", template.Buttons.Take(5).Select(b =>
                    $"{b.Emoji} {b.Label} ({b.Style})"));
                if (template.Buttons.Count > 5)
                    buttonList += $"\n... and {template.Buttons.Count - 5} more";
                embed.AddField("🔘 Buttons", buttonList, true);
            }

            if (template.SelectMenus.Any())
            {
                var menuList = string.Join("\n", template.SelectMenus.Select(m =>
                    $"📜 {m.Placeholder}"));
                embed.AddField("📜 Select Menus", menuList, true);
            }

            var channel = await ctx.Guild.GetTextChannelAsync(state.ChannelId);
            embed.AddField("📍 Target Channel", channel?.Mention ?? "Unknown");

            var components = new ComponentBuilder()
                .WithButton($"{Config.SuccessEmote} Create Panel", $"wizard_confirm_template:{wizardId}",
                    ButtonStyle.Success)
                .WithButton("🔄 Customize First", $"wizard_customize_template:{wizardId}")
                .WithButton("⬅️ Back", $"wizard_templates:{wizardId}", ButtonStyle.Secondary)
                .Build();

            await RespondAsync(embed: embed.Build(), components: components, ephemeral: true);
        }

        /// <summary>
        ///     Handles template confirmation and creation with proper channel format handling
        /// </summary>
        /// <param name="wizardId">The wizard session ID</param>
        [ComponentInteraction("wizard_confirm_template:*", true)]
        public async Task HandleTemplateConfirmation(string wizardId)
        {
            await DeferAsync(true);

            var wizardData = await cache.Redis.GetDatabase().StringGetAsync($"wizard:{wizardId}");
            if (!wizardData.HasValue)
            {
                await FollowupAsync($"{Config.ErrorEmote} Wizard session expired. Please start over.", ephemeral: true);
                return;
            }

            var state = JsonSerializer.Deserialize<WizardState>((string)wizardData);
            var templateId = state.Data.GetValueOrDefault("selectedTemplate")?.ToString();

            if (string.IsNullOrEmpty(templateId))
            {
                await FollowupAsync($"{Config.ErrorEmote} No template selected!", ephemeral: true);
                return;
            }

            try
            {
                var channel = await ctx.Guild.GetTextChannelAsync(state.ChannelId);
                if (channel == null)
                {
                    await FollowupAsync($"{Config.ErrorEmote} Target channel not found!", ephemeral: true);
                    return;
                }

                var template = TicketTemplates.GetAllTemplates().FirstOrDefault(t => t.Id == templateId);
                if (template == null)
                {
                    await FollowupAsync($"{Config.ErrorEmote} Template not found!", ephemeral: true);
                    return;
                }

                // Create the panel and ensure it's properly saved
                var embedJson = ConfigurationParser.CreateEmbedJson(template.EmbedConfig);
                var panel = await Service.CreatePanelAsync(channel, embedJson);

                // CRITICAL: Verify the panel has a valid ID before proceeding
                if (panel == null || panel.Id == 0)
                {
                    await FollowupAsync($"{Config.ErrorEmote} Failed to create panel - invalid panel ID",
                        ephemeral: true);
                    return;
                }

                // Refresh panel from database to ensure we have the correct ID
                panel = await Service.GetPanelAsync(panel.MessageId);
                if (panel == null)
                {
                    await FollowupAsync($"{Config.ErrorEmote} Failed to retrieve created panel from database",
                        ephemeral: true);
                    return;
                }

                var createdComponents = new List<string>();

                // Add buttons from template
                foreach (var buttonTemplate in template.Buttons)
                {
                    try
                    {
                        string modalJson = null;
                        if (!string.IsNullOrEmpty(buttonTemplate.ModalConfig))
                        {
                            modalJson = ConfigurationParser.CreateModalJson(buttonTemplate.ModalConfig);
                        }


                        var settings = ConfigurationParser.ParseKeyValuePairs(buttonTemplate.Settings);

                        TimeSpan? autoCloseTime = null;
                        if (settings.ContainsKey("auto_close_hours") &&
                            int.TryParse(settings["auto_close_hours"], out var hours))
                            autoCloseTime = TimeSpan.FromHours(hours);

                        TimeSpan? responseTime = null;
                        if (settings.ContainsKey("response_time_minutes") &&
                            int.TryParse(settings["response_time_minutes"], out var minutes))
                            responseTime = TimeSpan.FromMinutes(minutes);

                        var saveTranscripts = settings.ContainsKey("save_transcripts") &&
                                              settings["save_transcripts"].ToLower() is "true" or "yes";

                        // Get channel format from settings or use null (now allowed)
                        var channelFormat = settings.GetValueOrDefault("channel_format"); // This can be null

                        // Pass all required parameters with proper defaults
                        await Service.AddButtonAsync(
                            panel,
                            buttonTemplate.Label,
                            buttonTemplate.Emoji,
                            buttonTemplate.Style,
                            null, // Use service default
                            modalJson,
                            channelFormat, // Can be null after migration
                            null, // Can be set later via commands
                            null, // Can be set later via commands
                            null, // Can be set later via commands
                            null, // Can be set later via commands
                            autoCloseTime,
                            responseTime // Can be set later via commands
                        );

                        createdComponents.Add($"🔘 {buttonTemplate.Label}");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error adding button {Label} to panel {PanelId}", buttonTemplate.Label,
                            panel.Id);
                        createdComponents.Add($"{Config.ErrorEmote} {buttonTemplate.Label} (failed)");
                    }
                }

                // Add select menus from template
                foreach (var menuTemplate in template.SelectMenus)
                {
                    try
                    {
                        var menu = await Service.AddSelectMenuAsync(panel, menuTemplate.Placeholder, "");

                        var optionLines = menuTemplate.Options.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var optionLine in optionLines)
                        {
                            var parts = optionLine.Split('|', StringSplitOptions.TrimEntries);
                            if (parts.Length >= 2)
                            {
                                var label = parts[0];
                                var emoji = parts.Length > 1 ? parts[1] : null;
                                var description = parts.Length > 2 ? parts[2] : null;
                                logger.LogInformation(label);
                                await Service.AddSelectOptionAsync(menu, label, $"option_{Guid.NewGuid():N}",
                                    description, emoji);
                            }
                        }

                        createdComponents.Add($"📜 {menuTemplate.Placeholder}");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error adding select menu {Placeholder} to panel {PanelId}",
                            menuTemplate.Placeholder, panel.Id);
                        createdComponents.Add($"{Config.ErrorEmote} {menuTemplate.Placeholder} (failed)");
                    }
                }

                var successEmbed = new EmbedBuilder()
                    .WithTitle(Strings.TicketPanelCreated(ctx.Guild.Id, Config.SuccessEmote))
                    .WithDescription(
                        Strings.SuccessfullyCreatedTicketPanelTemplate(ctx.Guild.Id, channel.Mention, template.Name))
                    .AddField("📋 Panel ID", panel.MessageId.ToString())
                    .AddField("🧩 Components", string.Join("\n", createdComponents))
                    .AddField("🎉 Next Steps",
                        "• Test your ticket system\n" +
                        "• Configure categories and roles if needed\n" +
                        "• Add more buttons or menus as required\n" +
                        "• Use `/ticket-setup set-category` to configure categories\n" +
                        "• Use `/ticket-setup set-channel-format` to customize channel names")
                    .WithColor(Color.Green);

                await FollowupAsync(embed: successEmbed.Build(), ephemeral: true);

                // Clean up wizard data
                await cache.Redis.GetDatabase().KeyDeleteAsync($"wizard:{wizardId}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating panel from template in wizard");
                await FollowupAsync($"{Config.ErrorEmote} An error occurred while creating the panel.",
                    ephemeral: true);
            }
        }

        /// <summary>
        ///     Handles quick setup option from wizard
        /// </summary>
        /// <param name="wizardId">The wizard session ID</param>
        [ComponentInteraction("wizard_quick:*", true)]
        public async Task HandleQuickSetup(string wizardId)
        {
            var wizardData = await cache.Redis.GetDatabase().StringGetAsync($"wizard:{wizardId}");
            if (!wizardData.HasValue)
            {
                await RespondAsync($"{Config.ErrorEmote} Wizard session expired. Please start over.", ephemeral: true);
                return;
            }

            await RespondWithModalAsync<PanelCreationWizardModal>($"wizard_quick_modal:{wizardId}");
        }

        /// <summary>
        ///     Handles custom setup option from wizard
        /// </summary>
        /// <param name="wizardId">The wizard session ID</param>
        [ComponentInteraction("wizard_custom:*", true)]
        public async Task HandleCustomSetup(string wizardId)
        {
            await RespondAsync("🚧 Custom setup wizard coming soon! For now, use the quick setup or templates.",
                ephemeral: true);
        }

        #endregion

        #region Modal Handlers

        /// <summary>
        ///     Handles quick button setup modal submission
        /// </summary>
        /// <param name="panelId">The panel ID to add the button to</param>
        /// <param name="modal">The submitted modal data</param>
        [ModalInteraction("quick_button:*", true)]
        public async Task HandleQuickButtonModal(string panelId, QuickButtonSetupModal modal)
        {
            await DeferAsync(true);

            try
            {
                if (!ulong.TryParse(panelId, out var parsedPanelId))
                {
                    await FollowupAsync($"{Config.ErrorEmote} Invalid panel ID!", ephemeral: true);
                    return;
                }

                var panel = await Service.GetPanelAsync(parsedPanelId);
                if (panel == null || panel.GuildId != ctx.Guild.Id)
                {
                    await FollowupAsync($"{Config.ErrorEmote} Panel not found!", ephemeral: true);
                    return;
                }

                // Parse configuration
                var basicSettings = ConfigurationParser.ParseKeyValuePairs(modal.ButtonBasic);
                var behaviorSettings = ConfigurationParser.ParseKeyValuePairs(modal.BehaviorSettings);
                var permissionSettings = ConfigurationParser.ParseKeyValuePairs(modal.PermissionSettings);

                // Validate configuration
                var issues =
                    ConfigurationValidator.ValidateButtonConfiguration(basicSettings, modal.ModalConfig,
                        behaviorSettings);
                if (issues.Any())
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithTitle(Strings.ConfigurationIssues(ctx.Guild.Id, Config.ErrorEmote))
                        .WithDescription(Strings.PleaseFixIssues(ctx.Guild.Id))
                        .WithColor(Color.Red);

                    foreach (var issue in issues.Take(10))
                    {
                        errorEmbed.Description += $"\n{Config.ErrorEmote} {issue}";
                    }

                    await FollowupAsync(embed: errorEmbed.Build(), ephemeral: true);
                    return;
                }

                // Show preview and confirmation
                var preview = ConfigurationPreview.GenerateButtonPreview(basicSettings, modal.ModalConfig,
                    behaviorSettings,
                    permissionSettings, Strings.ButtonPreview(ctx.Guild.Id), Strings.ReviewButtonConfig(ctx.Guild.Id));

                var confirmId = Guid.NewGuid().ToString();
                await cache.Redis.GetDatabase().StringSetAsync($"button_confirm:{confirmId}",
                    JsonSerializer.Serialize(modal), TimeSpan.FromMinutes(10));
                await cache.Redis.GetDatabase().StringSetAsync($"button_panel:{confirmId}",
                    panelId, TimeSpan.FromMinutes(10));

                var components = new ComponentBuilder()
                    .WithButton($"{Config.SuccessEmote} Create Button", $"confirm_button_creation:{confirmId}",
                        ButtonStyle.Success)
                    .WithButton($"{Config.ErrorEmote} Cancel", $"cancel_button_creation:{confirmId}",
                        ButtonStyle.Danger)
                    .Build();

                await FollowupAsync(embed: preview.Build(), components: components, ephemeral: true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing quick button modal for panel {PanelId}", panelId);
                await FollowupAsync(
                    $"{Config.ErrorEmote} An error occurred while processing your button configuration.",
                    ephemeral: true);
            }
        }

        /// <summary>
        ///     Handles quick select menu setup modal submission
        /// </summary>
        /// <param name="panelId">The panel ID to add the select menu to</param>
        /// <param name="modal">The submitted modal data</param>
        [ModalInteraction("quick_menu:*", true)]
        public async Task HandleQuickSelectMenuModal(string panelId, QuickSelectMenuSetupModal modal)
        {
            await DeferAsync(true);

            try
            {
                if (!ulong.TryParse(panelId, out var parsedPanelId))
                {
                    await FollowupAsync($"{Config.ErrorEmote} Invalid panel ID!", ephemeral: true);
                    return;
                }

                var panel = await Service.GetPanelAsync(parsedPanelId);
                if (panel == null || panel.GuildId != ctx.Guild.Id)
                {
                    await FollowupAsync($"{Config.ErrorEmote} Panel not found!", ephemeral: true);
                    return;
                }

                // Validate configuration
                var issues = ConfigurationValidator.ValidateSelectMenuConfiguration(modal.Options);
                if (issues.Any())
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithTitle(Strings.ConfigurationIssues(ctx.Guild.Id, Config.ErrorEmote))
                        .WithDescription(Strings.PleaseFixIssues(ctx.Guild.Id))
                        .WithColor(Color.Red);

                    foreach (var issue in issues.Take(10))
                    {
                        errorEmbed.Description += $"\n{Config.ErrorEmote} {issue}";
                    }

                    await FollowupAsync(embed: errorEmbed.Build(), ephemeral: true);
                    return;
                }

                // Show preview and confirmation
                var menuSettings = ConfigurationParser.ParseKeyValuePairs(modal.MenuSettings);
                var placeholder = menuSettings.GetValueOrDefault("placeholder", "Select an option...");

                var preview =
                    ConfigurationPreview.GenerateSelectMenuPreview(Strings.SelectMenuPreview(ctx.Guild.Id), placeholder,
                        modal.Options, modal.SharedSettings, Strings.ReviewSelectMenuConfig(ctx.Guild.Id));

                var confirmId = Guid.NewGuid().ToString();
                await cache.Redis.GetDatabase().StringSetAsync($"menu_confirm:{confirmId}",
                    JsonSerializer.Serialize(modal), TimeSpan.FromMinutes(10));
                await cache.Redis.GetDatabase().StringSetAsync($"menu_panel:{confirmId}",
                    panelId, TimeSpan.FromMinutes(10));

                var components = new ComponentBuilder()
                    .WithButton($"{Config.SuccessEmote} Create Select Menu", $"confirm_menu_creation:{confirmId}",
                        ButtonStyle.Success)
                    .WithButton($"{Config.ErrorEmote} Cancel", $"cancel_menu_creation:{confirmId}", ButtonStyle.Danger)
                    .Build();

                await FollowupAsync(embed: preview.Build(), components: components, ephemeral: true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing quick select menu modal for panel {PanelId}", panelId);
                await FollowupAsync(
                    $"{Config.ErrorEmote} An error occurred while processing your select menu configuration.",
                    ephemeral: true);
            }
        }

        /// <summary>
        ///     Handles quick panel creation from wizard
        /// </summary>
        /// <param name="wizardId">The wizard session ID</param>
        /// <param name="modal">The submitted modal data</param>
        [ModalInteraction("wizard_quick_modal:*", true)]
        public async Task HandleQuickPanelModal(string wizardId, PanelCreationWizardModal modal)
        {
            await DeferAsync(true);

            var wizardData = await cache.Redis.GetDatabase().StringGetAsync($"wizard:{wizardId}");
            if (!wizardData.HasValue)
            {
                await FollowupAsync($"{Config.ErrorEmote} Wizard session expired. Please start over.", ephemeral: true);
                return;
            }

            var state = JsonSerializer.Deserialize<WizardState>((string)wizardData);

            try
            {
                var channel = await ctx.Guild.GetTextChannelAsync(state.ChannelId);
                if (channel == null)
                {
                    await FollowupAsync($"{Config.ErrorEmote} Target channel not found!", ephemeral: true);
                    return;
                }

                // Create the panel
                var embedJson = ConfigurationParser.CreateEmbedJson(modal.EmbedConfig);
                var panel = await Service.CreatePanelAsync(channel, embedJson);

                var createdComponents = new List<string>();

                // Add initial buttons if specified
                if (!string.IsNullOrEmpty(modal.InitialButtons))
                {
                    var buttonLines = modal.InitialButtons.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    var defaultSettings = ConfigurationParser.ParseKeyValuePairs(modal.DefaultSettings);

                    foreach (var buttonLine in buttonLines)
                    {
                        var parts = buttonLine.Split('|', StringSplitOptions.TrimEntries);
                        if (parts.Length >= 1)
                        {
                            var label = parts[0];
                            var emoji = parts.Length > 1 ? parts[1] : null;
                            var style = parts.Length > 2
                                ? ConfigurationParser.ParseButtonStyle(parts[2])
                                : ButtonStyle.Primary;

                            TimeSpan? autoCloseTime = null;
                            if (defaultSettings.ContainsKey("auto_close_hours") &&
                                int.TryParse(defaultSettings["auto_close_hours"], out var hours))
                                autoCloseTime = TimeSpan.FromHours(hours);

                            var saveTranscripts = defaultSettings.ContainsKey("save_transcripts") &&
                                                  defaultSettings["save_transcripts"].ToLower() is "true" or "yes";

                            await Service.AddButtonAsync(
                                panel,
                                label,
                                emoji,
                                style,
                                autoCloseTime: autoCloseTime
                            );

                            createdComponents.Add($"🔘 {label}");
                        }
                    }
                }

                var successEmbed = new EmbedBuilder()
                    .WithTitle(Strings.TicketPanelCreated(ctx.Guild.Id, Config.SuccessEmote))
                    .WithDescription(Strings.TicketPanelCreatedIn(ctx.Guild.Id, channel.Mention))
                    .AddField("📋 Panel ID", panel.MessageId.ToString())
                    .WithColor(Color.Green);

                if (createdComponents.Any())
                {
                    successEmbed.AddField("🧩 Components", string.Join("\n", createdComponents));
                }

                successEmbed.AddField("🎉 Next Steps",
                    "• Test your ticket system\n" +
                    "• Add more buttons or select menus\n" +
                    "• Configure categories and roles");

                await FollowupAsync(embed: successEmbed.Build(), ephemeral: true);

                // Clean up wizard data
                await cache.Redis.GetDatabase().KeyDeleteAsync($"wizard:{wizardId}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating panel from quick wizard");
                await FollowupAsync($"{Config.ErrorEmote} An error occurred while creating the panel.",
                    ephemeral: true);
            }
        }

        #endregion

        #region Confirmation Handlers

        /// <summary>
        ///     Confirms button creation from quick setup with proper channel format handling
        /// </summary>
        /// <param name="confirmId">The confirmation ID</param>
        [ComponentInteraction("confirm_button_creation:*", true)]
        public async Task ConfirmButtonCreation(string confirmId)
        {
            await DeferAsync(true);

            try
            {
                var modalData = await cache.Redis.GetDatabase().StringGetAsync($"button_confirm:{confirmId}");
                var panelData = await cache.Redis.GetDatabase().StringGetAsync($"button_panel:{confirmId}");

                if (!modalData.HasValue || !panelData.HasValue)
                {
                    await FollowupAsync($"{Config.ErrorEmote} Confirmation expired. Please try again.",
                        ephemeral: true);
                    return;
                }

                var modal = JsonSerializer.Deserialize<QuickButtonSetupModal>((string)modalData);
                var panelId = ulong.Parse(panelData);

                var panel = await Service.GetPanelAsync(panelId);
                if (panel == null)
                {
                    await FollowupAsync($"{Config.ErrorEmote} Panel not found!", ephemeral: true);
                    return;
                }

                // Parse and apply configuration
                var basicSettings = ConfigurationParser.ParseKeyValuePairs(modal.ButtonBasic);
                var behaviorSettings = ConfigurationParser.ParseKeyValuePairs(modal.BehaviorSettings);
                var permissionSettings = ConfigurationParser.ParseKeyValuePairs(modal.PermissionSettings);

                var label = basicSettings.GetValueOrDefault("label", "Button");
                var emoji = basicSettings.GetValueOrDefault("emoji");
                var style = ConfigurationParser.ParseButtonStyle(basicSettings.GetValueOrDefault("style"));

                string modalJson = null;
                if (!string.IsNullOrEmpty(modal.ModalConfig))
                {
                    modalJson = ConfigurationParser.CreateModalJson(modal.ModalConfig);
                }

                TimeSpan? autoCloseTime = null;
                if (behaviorSettings.ContainsKey("auto_close_hours") &&
                    int.TryParse(behaviorSettings["auto_close_hours"], out var hours))
                    autoCloseTime = TimeSpan.FromHours(hours);

                TimeSpan? responseTime = null;
                if (behaviorSettings.ContainsKey("response_time_minutes") &&
                    int.TryParse(behaviorSettings["response_time_minutes"], out var minutes))
                    responseTime = TimeSpan.FromMinutes(minutes);

                // Get channel format from behavior settings (can be null)
                var channelFormat = behaviorSettings.GetValueOrDefault("channel_format");

                // Parse roles
                List<ulong> supportRoles = null;
                List<ulong> viewerRoles = null;

                if (permissionSettings.TryGetValue("support_roles", out var setting))
                {
                    supportRoles = await ConfigurationParser.ParseRoles(setting, ctx.Guild);
                }

                if (permissionSettings.TryGetValue("viewer_roles", out var permissionSetting))
                {
                    viewerRoles = await ConfigurationParser.ParseRoles(permissionSetting, ctx.Guild);
                }

                // Find categories by name
                ulong? categoryId = null;
                ulong? archiveCategoryId = null;

                if (permissionSettings.TryGetValue("ticket_category", out var setting1))
                {
                    var categories = await ctx.Guild.GetCategoriesAsync();
                    var category = categories.FirstOrDefault(c =>
                        string.Equals(c.Name, setting1,
                            StringComparison.OrdinalIgnoreCase));
                    categoryId = category?.Id;
                }

                if (permissionSettings.TryGetValue("archive_category", out var permissionSetting1))
                {
                    var categories = await ctx.Guild.GetCategoriesAsync();
                    var category = categories.FirstOrDefault(c =>
                        string.Equals(c.Name, permissionSetting1,
                            StringComparison.OrdinalIgnoreCase));
                    archiveCategoryId = category?.Id;
                }

                await Service.AddButtonAsync(
                    panel,
                    label,
                    emoji,
                    style,
                    null, // Use service default
                    modalJson,
                    channelFormat, // Can be null after migration
                    categoryId,
                    archiveCategoryId,
                    supportRoles,
                    viewerRoles,
                    autoCloseTime,
                    responseTime // Can be set later
                );

                await FollowupAsync($"{Config.SuccessEmote} Button '{label}' added successfully to panel {panelId}!",
                    ephemeral: true);

                // Clean up
                await cache.Redis.GetDatabase().KeyDeleteAsync($"button_confirm:{confirmId}");
                await cache.Redis.GetDatabase().KeyDeleteAsync($"button_panel:{confirmId}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error confirming button creation");
                await FollowupAsync($"{Config.ErrorEmote} An error occurred while creating the button.",
                    ephemeral: true);
            }
        }

        /// <summary>
        ///     Confirms select menu creation from quick setup
        /// </summary>
        /// <param name="confirmId">The confirmation ID</param>
        [ComponentInteraction("confirm_menu_creation:*", true)]
        public async Task ConfirmMenuCreation(string confirmId)
        {
            await DeferAsync(true);

            try
            {
                var modalData = await cache.Redis.GetDatabase().StringGetAsync($"menu_confirm:{confirmId}");
                var panelData = await cache.Redis.GetDatabase().StringGetAsync($"menu_panel:{confirmId}");

                if (!modalData.HasValue || !panelData.HasValue)
                {
                    await FollowupAsync($"{Config.ErrorEmote} Confirmation expired. Please try again.",
                        ephemeral: true);
                    return;
                }

                var modal = JsonSerializer.Deserialize<QuickSelectMenuSetupModal>((string)modalData);
                var panelId = ulong.Parse(panelData);

                var panel = await Service.GetPanelAsync(panelId);
                if (panel == null)
                {
                    await FollowupAsync($"{Config.ErrorEmote} Panel not found!", ephemeral: true);
                    return;
                }

                var menuSettings = ConfigurationParser.ParseKeyValuePairs(modal.MenuSettings);
                var placeholder = menuSettings.GetValueOrDefault("placeholder", "Select an option...");

                // Create the select menu
                var menu = await Service.AddSelectMenuAsync(panel, placeholder, "");

                // Add options
                var optionLines = modal.Options.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var optionLine in optionLines)
                {
                    var parts = optionLine.Split('|', StringSplitOptions.TrimEntries);
                    if (parts.Length >= 2)
                    {
                        var label = parts[0];
                        var emoji = parts[1];
                        var description = parts.Length > 2 ? parts[2] : null;

                        await Service.AddSelectOptionAsync(menu, label, $"option_{Guid.NewGuid():N}", description,
                            emoji);
                    }
                }

                await FollowupAsync(
                    $"{Config.SuccessEmote} Select menu '{placeholder}' added successfully to panel {panelId}!",
                    ephemeral: true);

                // Clean up
                await cache.Redis.GetDatabase().KeyDeleteAsync($"menu_confirm:{confirmId}");
                await cache.Redis.GetDatabase().KeyDeleteAsync($"menu_panel:{confirmId}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error confirming menu creation");
                await FollowupAsync($"{Config.ErrorEmote} An error occurred while creating the select menu.",
                    ephemeral: true);
            }
        }

        /// <summary>
        ///     Cancels button creation
        /// </summary>
        /// <param name="confirmId">The confirmation ID</param>
        [ComponentInteraction("cancel_button_creation:*", true)]
        public async Task CancelButtonCreation(string confirmId)
        {
            await cache.Redis.GetDatabase().KeyDeleteAsync($"button_confirm:{confirmId}");
            await cache.Redis.GetDatabase().KeyDeleteAsync($"button_panel:{confirmId}");
            await RespondAsync($"{Config.ErrorEmote} Button creation cancelled.", ephemeral: true);
        }

        /// <summary>
        ///     Cancels select menu creation
        /// </summary>
        /// <param name="confirmId">The confirmation ID</param>
        [ComponentInteraction("cancel_menu_creation:*", true)]
        public async Task CancelMenuCreation(string confirmId)
        {
            await cache.Redis.GetDatabase().KeyDeleteAsync($"menu_confirm:{confirmId}");
            await cache.Redis.GetDatabase().KeyDeleteAsync($"menu_panel:{confirmId}");
            await RespondAsync($"{Config.ErrorEmote} Select menu creation cancelled.", ephemeral: true);
        }

        #endregion
    }

    /// <summary>
    ///     Wizard state management
    /// </summary>
    public class WizardState
    {
        /// <summary>
        ///     Gets or sets the user ID running the wizard
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        ///     Gets or sets the guild ID
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        ///     Gets or sets the target channel ID
        /// </summary>
        public ulong ChannelId { get; set; }

        /// <summary>
        ///     Gets or sets the current wizard step
        /// </summary>
        public WizardStep Step { get; set; }

        /// <summary>
        ///     Gets or sets the wizard data
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();
    }
}