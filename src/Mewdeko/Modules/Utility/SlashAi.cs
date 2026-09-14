using System.Net.Http;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class SlashUtility
{
    /// <summary>
    ///     Commands for configuring and managing AI functionality.
    /// </summary>
    [Group("ai", "Configure and manage AI settings")]
    public class AiSlashCommands : MewdekoSlashSubmodule<AiService>
    {
        /// <summary>
        ///     Handles the button interaction for setting an AI API key, displaying a modal for secure input.
        /// </summary>
        /// <returns>A task representing the modal response operation.</returns>
        [ComponentInteraction("setaikey", true)]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public Task AiKeyButton()
        {
            return RespondWithModalAsync<AiKeyModal>("aikeymodal");
        }

        /// <summary>
        ///     Processes the submitted AI API key from the modal and updates the configuration.
        /// </summary>
        /// <param name="modal">The modal containing the submitted API key.</param>
        /// <returns>A task representing the asynchronous configuration update operation.</returns>
        [ModalInteraction("aikeymodal", true)]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AiKeyModal(AiKeyModal modal)
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);
            config.ApiKey = modal.ApiKey;
            await Service.UpdateConfig(config);

            await ctx.Interaction.SendConfirmAsync(Strings.AiApiKeyUpdated(ctx.Guild.Id, config.Provider));
        }

        /// <summary>
        ///     Configures AI functionality for a specific channel.
        /// </summary>
        /// <param name="channel">The channel to configure AI for. Defaults to current channel if not specified.</param>
        /// <param name="enabled">Whether to enable or disable AI in the channel.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("channel", "Configure AI for a channel")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task AiChannel(ITextChannel? channel = null, bool enabled = true)
        {
            channel ??= (ITextChannel)ctx.Channel;
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);

            config.Enabled = enabled;
            config.ChannelId = channel.Id;
            await Service.UpdateConfig(config);

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription(Strings.AiConfigUpdated(ctx.Guild.Id, channel.Mention))
                .Build());
        }

        /// <summary>
        ///     Sets or lists available AI models for a provider.
        /// </summary>
        /// <param name="provider">The AI provider to use.</param>
        /// <param name="model">The model ID to set. If null, lists available models.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("model", "Set the AI provider and model")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task AiModel(
            AiService.AiProvider provider,
            [Autocomplete(typeof(AiModelAutoCompleter))]
            string? model = null)
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                await ctx.Interaction.SendErrorAsync(Strings.AiNoApiKey(ctx.Guild.Id, provider), Config);
                return;
            }

            var models = await Service.GetSupportedModels(provider, config.ApiKey);

            if (model == null)
            {
                var modelList = string.Join("\n", models.Select(m => $"• {m.Name} (`{m.Id}`)"));
                await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription(Strings.AiModelList(ctx.Guild.Id, provider.ToString(), modelList))
                    .Build());
                return;
            }

            if (!models.Any(m => m.Id.Equals(model, StringComparison.OrdinalIgnoreCase)))
            {
                await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                    .WithErrorColor()
                    .WithDescription(Strings.AiInvalidModel(ctx.Guild.Id, model, provider.ToString()))
                    .Build());
                return;
            }

            config.Provider = (int)provider;
            config.Model = model;
            await Service.UpdateConfig(config);

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription(Strings.AiModelChanged(ctx.Guild.Id, model))
                .Build());
        }

        /// <summary>
        ///     Sets the API key for the configured AI provider.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("key", "Set the API key for the AI service")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AiKey()
        {
            var component = new ComponentBuilder()
                .WithButton(Strings.AiKeyClickToSet(ctx.Guild.Id), "setaikey")
                .Build();
            await ctx.Interaction.RespondAsync(Strings.EmptyResponse(ctx.Guild.Id), components: component);
        }

        /// <summary>
        ///     Sets the system prompt used for AI conversations.
        /// </summary>
        /// <param name="prompt">The system prompt to set.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("prompt", "Set the system prompt for the AI")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task AiPrompt(string prompt)
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);
            config.SystemPrompt = prompt;
            await Service.UpdateConfig(config);

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithDescription(Strings.AiSystemPromptUpdated(ctx.Guild.Id))
                .Build());
        }

        /// <summary>
        ///     Sets the webhook for AI responses in this guild. Leaving the name empty disables the webhook.
        /// </summary>
        /// <param name="name">The name of the webhook. If null, disables the webhook.</param>
        /// <param name="avatar">Optional URL for the webhook's avatar.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("webhook", "Set or disable the webhook used for AI responses")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [RequireBotPermission(GuildPermission.ManageWebhooks)]
        public async Task AiWebhook(
            [Summary("name", "Webhook name. Leave empty to disable the webhook")]
            string? name = null,
            [Summary("avatar", "Url of the webhook avatar")]
            string? avatar = null)
        {
            await DeferAsync();
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);
            var channel = await ctx.Guild.GetTextChannelAsync(config.ChannelId);

            if (name is null)
            {
                await Service.SetWebhook(ctx.Guild.Id, null);
                await ConfirmAsync(Strings.AiWebhookDisabled(ctx.Guild.Id));
                return;
            }

            if (channel is null)
            {
                await ErrorAsync(Strings.AiNoChannelSet(ctx.Guild.Id, Config.Prefix));
                return;
            }

            if (avatar is not null)
            {
                if (!Uri.IsWellFormedUriString(avatar, UriKind.Absolute))
                {
                    await ErrorAsync(Strings.AiWebhookInvalidAvatar(ctx.Guild.Id));
                    return;
                }

                var http = new HttpClient();
                using var sr = await http.GetAsync(avatar, HttpCompletionOption.ResponseHeadersRead);
                var imgData = await sr.Content.ReadAsByteArrayAsync();
                var imgStream = imgData.ToStream();
                await using var _ = imgStream;
                var webhook = await channel.CreateWebhookAsync(name, imgStream);
                await Service.SetWebhook(ctx.Guild.Id,
                    $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}");
            }
            else
            {
                var webhook = await channel.CreateWebhookAsync(name);
                await Service.SetWebhook(ctx.Guild.Id,
                    $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}");
            }

            await ConfirmAsync(Strings.AiWebhookSet(ctx.Guild.Id));
        }

        /// <summary>
        ///     Enables or disables web search for AI (Claude only).
        /// </summary>
        /// <param name="enabled">Whether to enable or disable web search.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("web-search", "Enable or disable web search for AI responses (Claude only)")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task AiWebSearch(bool enabled)
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);

            if (config.Provider != (int)AiService.AiProvider.Claude)
            {
                await ErrorAsync(Strings.AiWebSearchClaudeOnly(ctx.Guild.Id));
                return;
            }

            config.WebSearchEnabled = enabled;
            await Service.UpdateConfig(config);

            if (enabled)
                await ConfirmAsync(Strings.AiWebSearchEnabled(ctx.Guild.Id));
            else
                await ConfirmAsync(Strings.AiWebSearchDisabled(ctx.Guild.Id));
        }

        /// <summary>
        ///     Sets or displays the custom embed template for AI responses.
        ///     Use %airesponse% to specify where the AI response should appear in the embed.
        /// </summary>
        /// <param name="view">When true, shows the current template instead of opening the editor.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("custom-embed", "Set or view the custom embed template for AI responses")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AiCustomEmbed(
            [Summary("view", "Show the current template instead of editing it")]
            bool view = false)
        {
            if (!view)
            {
                await RespondWithModalAsync<AiCustomEmbedModal>("utility_ai_custom_embed");
                return;
            }

            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);
            if (string.IsNullOrEmpty(config.CustomEmbed))
            {
                await ErrorAsync(Strings.AiNoCustomEmbed(ctx.Guild.Id));
                return;
            }

            await ConfirmAsync(config.CustomEmbed);
        }

        /// <summary>
        ///     Handles the custom embed modal and stores the submitted template.
        /// </summary>
        /// <param name="modal">The modal containing the embed template.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [ModalInteraction("utility_ai_custom_embed", true)]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AiCustomEmbedSubmitted(AiCustomEmbedModal modal)
        {
            await Service.SetCustomEmbed(ctx.Guild.Id, modal.EmbedTemplate);
            await ConfirmAsync(Strings.AiCustomEmbedSet(ctx.Guild.Id));
        }

        /// <summary>
        ///     Shows the current AI configuration for the guild.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("config", "Show current AI configuration")]
        [RequireContext(ContextType.Guild)]
        public async Task AiConfig()
        {
            var config = await Service.GetOrCreateConfig(ctx.Guild.Id);

            await ctx.Interaction.RespondAsync(embed: new EmbedBuilder()
                .WithTitle(Strings.AiConfigTitle(ctx.Guild.Id))
                .WithDescription(Strings.AiConfigDescription(
                    ctx.Guild.Id,
                    config.Enabled,
                    config.ChannelId,
                    config.Provider,
                    config.Model ?? "Not Set",
                    config.TokensUsed))
                .WithOkColor()
                .Build());
        }
    }
}