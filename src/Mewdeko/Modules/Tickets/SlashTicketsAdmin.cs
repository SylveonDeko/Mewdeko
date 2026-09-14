using System.Text.Json;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Tickets.Common;
using Mewdeko.Modules.Tickets.Services;

namespace Mewdeko.Modules.Tickets;

/// <summary>
///     Administrative slash commands for the ticket system, covering panel maintenance, button configuration,
///     tags, priorities, user blocking and bulk operations.
/// </summary>
public partial class TicketsSlash
{
    /// <summary>
    ///     Panel maintenance commands: inspection, recreation, duplication, moving, updating and select menu editing.
    /// </summary>
    public partial class PanelCommands
    {
        /// <summary>
        ///     Displays detailed information about a ticket panel.
        /// </summary>
        /// <param name="panelId">The message ID of the panel to view.</param>
        [SlashCommand("info", "Displays detailed information about a ticket panel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task PanelInfo([Summary("panel-id", "Message ID of the panel")] ulong panelId)
        {
            var panel = await Service.GetPanelAsync(panelId);
            if (panel == null || panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.PanelNotFound(ctx.Guild.Id));
                return;
            }

            var channel = await ctx.Guild.GetTextChannelAsync(panel.ChannelId);
            var embed = new EmbedBuilder()
                .WithTitle(Strings.TicketPanelInfo(ctx.Guild.Id))
                .WithDescription(Strings.ChannelDeletedOrExists(ctx.Guild.Id,
                    channel == null ? "Deleted" : channel.Mention))
                .AddField("Message ID", panel.MessageId, true)
                .AddField("Buttons", panel.PanelButtons?.Count() ?? 0, true)
                .AddField("Select Menus", panel.PanelSelectMenus?.Count() ?? 0, true)
                .WithOkColor();

            if (panel.PanelButtons?.Any() == true)
            {
                var buttonInfo = string.Join("\n", panel.PanelButtons.Select(b =>
                    $"{b.Label} (ID: {b.Id}), Style: {b.Style}"));
                embed.AddField("Button Details", buttonInfo);
            }

            if (panel.PanelSelectMenus?.Any() == true)
            {
                foreach (var menu in panel.PanelSelectMenus)
                {
                    var optionInfo = string.Join("\n", menu.SelectMenuOptions.Select(o =>
                        $"{o.Label} ({o.Value})"));
                    embed.AddField($"Select Menu {menu.Id}",
                        $"Placeholder: {menu.Placeholder}\nOptions:\n{optionInfo}");
                }
            }

            await RespondAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Recreates a deleted ticket panel in its original channel.
        /// </summary>
        /// <param name="panelId">The message ID of the panel to recreate.</param>
        [SlashCommand("recreate", "Recreates a deleted ticket panel in its original channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RecreatePanel([Summary("panel-id", "Message ID of the panel")] ulong panelId)
        {
            await DeferAsync();
            var (success, newMessageId, channelMention, error) =
                await Service.RecreatePanelAsync(ctx.Guild.Id, panelId);

            if (success)
            {
                await ConfirmAsync(Strings.PanelRecreated(ctx.Guild.Id, channelMention, panelId,
                    newMessageId.Value));
            }
            else
            {
                await ErrorAsync(error);
            }
        }

        /// <summary>
        ///     Recreates all panels with missing messages.
        /// </summary>
        [SlashCommand("recreate-all", "Recreates all ticket panels whose messages are missing")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RecreateAllPanels()
        {
            await DeferAsync();

            var (recreated, failed, errors) = await Service.RecreateAllMissingPanelsAsync(ctx.Guild.Id);

            if (recreated == 0 && failed == 0)
            {
                await ConfirmAsync(Strings.NoMissingPanels(ctx.Guild.Id));
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle(Strings.PanelRecreationComplete(ctx.Guild.Id))
                .AddField(Strings.Recreated(ctx.Guild.Id), recreated, true)
                .AddField(Strings.Failed(ctx.Guild.Id), failed, true)
                .WithOkColor();

            if (errors.Any())
            {
                embed.AddField(Strings.Errors(ctx.Guild.Id), string.Join("\n", errors));
            }

            await ctx.Interaction.FollowupAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Duplicates a ticket panel into another channel.
        /// </summary>
        /// <param name="panelId">The message ID of the panel to duplicate.</param>
        /// <param name="targetChannel">The channel to create the copy in.</param>
        [SlashCommand("duplicate", "Duplicates a ticket panel into another channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TicketDuplicatePanel(
            [Summary("panel-id", "Message ID of the panel")]
            ulong panelId,
            [Summary("channel", "Channel to create the copy in")]
            ITextChannel targetChannel)
        {
            await DeferAsync();
            var newPanel = await Service.DuplicatePanelAsync(ctx.Guild, panelId, targetChannel.Id);
            if (newPanel != null)
                await ConfirmAsync(Strings.PanelDuplicated(ctx.Guild.Id, panelId, targetChannel.Mention));
            else
                await ErrorAsync(Strings.PanelDuplicateFailed(ctx.Guild.Id));
        }

        /// <summary>
        ///     Moves a ticket panel to another channel.
        /// </summary>
        /// <param name="panelId">The message ID of the panel to move.</param>
        /// <param name="targetChannel">The channel to move the panel to.</param>
        [SlashCommand("move", "Moves a ticket panel to another channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task MovePanel(
            [Summary("panel-id", "Message ID of the panel")]
            ulong panelId,
            [Summary("channel", "Channel to move the panel to")]
            ITextChannel targetChannel)
        {
            await DeferAsync();
            var success = await Service.MovePanelAsync(ctx.Guild, panelId, targetChannel.Id);
            if (success)
                await ConfirmAsync(Strings.PanelMoved(ctx.Guild.Id, panelId, targetChannel.Mention));
            else
                await ErrorAsync(Strings.PanelMoveFailed(ctx.Guild.Id));
        }

        /// <summary>
        ///     Updates a ticket panel's embed using a modal for the embed json.
        /// </summary>
        /// <param name="panelId">The message ID of the panel to update.</param>
        [SlashCommand("update", "Updates a ticket panel's embed")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public Task UpdatePanel([Summary("panel-id", "Message ID of the panel")] ulong panelId)
        {
            return RespondWithModalAsync<PanelUpdateModal>($"ticket_panel_update:{panelId}");
        }

        /// <summary>
        ///     Handles the panel update modal submission.
        /// </summary>
        /// <param name="panelId">The message ID of the panel being updated.</param>
        /// <param name="modal">The modal containing the new embed json.</param>
        [ModalInteraction("ticket_panel_update:*", true)]
        public async Task UpdatePanelSubmitted(string panelId, PanelUpdateModal modal)
        {
            await DeferAsync(true);
            var success = await Service.UpdatePanelEmbedAsync(ctx.Guild, ulong.Parse(panelId), modal.EmbedJson);
            if (success)
                await ConfirmAsync(Strings.PanelUpdated(ctx.Guild.Id));
            else
                await ErrorAsync(Strings.PanelUpdateFailed(ctx.Guild.Id));
        }

        /// <summary>
        ///     Enables or disables transcript saving for every button on a panel.
        /// </summary>
        /// <param name="panelId">The message ID of the panel to modify.</param>
        /// <param name="enable">Whether to enable or disable transcript saving.</param>
        [SlashCommand("transcripts", "Enables or disables transcript saving for a panel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task SetPanelTranscripts(
            [Summary("panel-id", "Message ID of the panel")]
            ulong panelId,
            [Summary("enable", "Whether transcripts should be saved")]
            bool enable)
        {
            var panel = await Service.GetPanelAsync(panelId);
            if (panel == null || panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.PanelNotFound(ctx.Guild.Id));
                return;
            }

            await DeferAsync();

            if (panel.PanelButtons != null)
            {
                foreach (var button in panel.PanelButtons)
                {
                    await Service.UpdateButtonSettingsAsync(ctx.Guild, button.Id,
                        new Dictionary<string, object>
                        {
                            {
                                "SaveTranscript", enable
                            }
                        });
                }
            }

            await ConfirmAsync(
                Strings.TranscriptsStatus(ctx.Guild.Id, enable ? "enabled" : "disabled") +
                " for all buttons in this panel.");
        }

        /// <summary>
        ///     Deletes a select menu from a panel.
        /// </summary>
        /// <param name="menuId">The ID of the select menu to delete.</param>
        [SlashCommand("delete-menu", "Deletes a select menu from a panel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task DeleteSelectMenu([Summary("menu-id", "ID of the select menu")] int menuId)
        {
            var menu = await Service.GetSelectMenuAsync(menuId.ToString());
            if (menu == null || menu.Panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.SelectMenuNotFound(ctx.Guild.Id));
                return;
            }

            await DeferAsync();
            var success = await Service.DeleteSelectMenuAsync(ctx.Guild, menuId);
            if (success)
                await ConfirmAsync(Strings.SelectMenuDeletedSuccess(ctx.Guild.Id, menu.Placeholder));
            else
                await ErrorAsync(Strings.FailedDeleteSelectMenu(ctx.Guild.Id));
        }

        /// <summary>
        ///     Deletes a specific option from a select menu. At least one option must remain.
        /// </summary>
        /// <param name="optionId">The ID of the option to delete.</param>
        [SlashCommand("delete-option", "Deletes an option from a select menu")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task DeleteSelectOption([Summary("option-id", "ID of the option")] int optionId)
        {
            var option = await Service.GetSelectOptionAsync(optionId);
            if (option == null || option.SelectMenu.Panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.SelectMenuOptionNotFound(ctx.Guild.Id));
                return;
            }

            var optionCount = await Service.GetSelectMenuOptionCountAsync(option.SelectMenuId);
            if (optionCount <= 1)
            {
                await ReplyErrorAsync(Strings.CannotDeleteLastSelectOption(ctx.Guild.Id));
                return;
            }

            await DeferAsync();
            var success = await Service.DeleteSelectOptionAsync(ctx.Guild, optionId);
            if (success)
                await ConfirmAsync(Strings.SuccessfullyDeletedOption(ctx.Guild.Id, option.Label));
            else
                await ErrorAsync(Strings.FailedDeleteSelectMenuOption(ctx.Guild.Id));
        }

        /// <summary>
        ///     Adds an option to an existing select menu, collecting the option details through a modal.
        /// </summary>
        /// <param name="menuId">The ID of the menu to add the option to.</param>
        [SlashCommand("option-add", "Adds an option to a select menu")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AddOption([Summary("menu-id", "ID of the select menu")] string menuId)
        {
            var menu = await Service.GetSelectMenuAsync(menuId);
            if (menu == null || menu.Panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.SelectMenuNotFound(ctx.Guild.Id));
                return;
            }

            await RespondWithModalAsync<TicketOptionAddModal>($"ticket_option_add:{menuId}");
        }

        /// <summary>
        ///     Handles the select menu option modal submission.
        /// </summary>
        /// <param name="menuId">The ID of the menu the option is added to.</param>
        /// <param name="modal">The modal containing the option details.</param>
        [ModalInteraction("ticket_option_add:*", true)]
        public async Task AddOptionSubmitted(string menuId, TicketOptionAddModal modal)
        {
            var menu = await Service.GetSelectMenuAsync(menuId);
            if (menu == null || menu.Panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.SelectMenuNotFound(ctx.Guild.Id));
                return;
            }

            await DeferAsync();

            var description = string.IsNullOrWhiteSpace(modal.Description) ? null : modal.Description;
            var emoji = string.IsNullOrWhiteSpace(modal.Emoji) ? null : modal.Emoji;

            try
            {
                await Service.AddSelectOptionAsync(
                    menu,
                    modal.Label,
                    $"option_{Guid.NewGuid():N}",
                    description,
                    emoji);

                var embed = new EmbedBuilder()
                    .WithTitle(Strings.OptionAdded(ctx.Guild.Id))
                    .WithDescription(Strings.OptionAddedSuccess(ctx.Guild.Id, modal.Label))
                    .AddField("Details",
                        $"Label: {modal.Label}\n" +
                        $"Description: {description ?? "None"}\n" +
                        $"Emoji: {emoji ?? "None"}")
                    .WithOkColor();

                await ctx.Interaction.FollowupAsync(embed: embed.Build());
            }
            catch (Exception)
            {
                await ErrorAsync(Strings.OptionAddFailed(ctx.Guild.Id));
            }
        }

        /// <summary>
        ///     Lists all options in a select menu.
        /// </summary>
        /// <param name="menuId">The ID of the menu to view.</param>
        [SlashCommand("option-list", "Lists all options in a select menu")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task ListOptions([Summary("menu-id", "ID of the select menu")] string menuId)
        {
            var menu = await Service.GetSelectMenuAsync(menuId);
            if (menu == null || menu.Panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.SelectMenuNotFound(ctx.Guild.Id));
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle(Strings.SelectMenuOptions(ctx.Guild.Id))
                .WithDescription(Strings.SelectMenuPlaceholder(ctx.Guild.Id, menu.Placeholder))
                .WithOkColor();

            foreach (var option in menu.SelectMenuOptions)
            {
                embed.AddField(option.Label,
                    $"Value: {option.Value}\n" +
                    $"Description: {option.Description ?? "None"}\n" +
                    $"Emoji: {option.Emoji ?? "None"}");
            }

            await RespondAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Removes an option from a select menu by its value.
        /// </summary>
        /// <param name="menuId">The ID of the menu.</param>
        /// <param name="optionValue">The value of the option to remove.</param>
        [SlashCommand("option-remove", "Removes an option from a select menu")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RemoveOption(
            [Summary("menu-id", "ID of the select menu")]
            string menuId,
            [Summary("option-value", "Value of the option to remove")]
            string optionValue)
        {
            var menu = await Service.GetSelectMenuAsync(menuId);
            if (menu == null || menu.Panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.SelectMenuNotFound(ctx.Guild.Id));
                return;
            }

            var option = menu.SelectMenuOptions.FirstOrDefault(o => o.Value == optionValue);
            if (option == null)
            {
                await ReplyErrorAsync(Strings.OptionNotFound(ctx.Guild.Id));
                return;
            }

            if (menu.SelectMenuOptions.Count() <= 1)
            {
                await ReplyErrorAsync(Strings.CannotRemoveLastOption(ctx.Guild.Id));
                return;
            }

            await DeferAsync();
            var success = await Service.DeleteSelectOptionAsync(ctx.Guild, option.Id);
            if (success)
                await ConfirmAsync(Strings.OptionRemoved(ctx.Guild.Id, option.Label));
            else
                await ErrorAsync(Strings.FailedDeleteSelectMenuOption(ctx.Guild.Id));
        }

        /// <summary>
        ///     Updates the placeholder text for a select menu.
        /// </summary>
        /// <param name="menuId">The ID of the menu.</param>
        /// <param name="placeholder">The new placeholder text.</param>
        [SlashCommand("placeholder", "Updates the placeholder text of a select menu")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task SetPlaceholder(
            [Summary("menu-id", "ID of the select menu")]
            string menuId,
            [Summary("placeholder", "The new placeholder text")]
            string placeholder)
        {
            var menu = await Service.GetSelectMenuAsync(menuId);
            if (menu == null || menu.Panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.SelectMenuNotFound(ctx.Guild.Id));
                return;
            }

            await DeferAsync();
            await Service.UpdateSelectMenuAsync(menu, m => m.Placeholder = placeholder);
            await ConfirmAsync(Strings.PlaceholderUpdated(ctx.Guild.Id, placeholder));
        }
    }

    /// <summary>
    ///     Commands for configuring individual ticket buttons.
    /// </summary>
    [Group("button", "Configure ticket buttons")]
    public class ButtonCommands : MewdekoSlashSubmodule<TicketService>
    {
        /// <summary>
        ///     Sets the category where tickets created by a button will be placed.
        /// </summary>
        /// <param name="buttonId">The ID of the button to modify.</param>
        /// <param name="category">The category channel, or omit it to remove the category.</param>
        [SlashCommand("category", "Sets the category where tickets from a button are created")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TicketCategory(
            [Summary("button-id", "ID of the button")]
            int buttonId,
            [Summary("category", "Category for new tickets, omit to clear")]
            ICategoryChannel? category = null)
        {
            var panel = await Service.GetButtonAsync(buttonId);
            if (panel == null || panel.Panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.TicketPanelNotFound(ctx.Guild.Id));
                return;
            }

            await DeferAsync();
            await Service.UpdateButtonSettingsAsync(ctx.Guild, buttonId, new Dictionary<string, object>
            {
                {
                    "categoryId", category?.Id
                }
            });

            if (category == null)
                await ConfirmAsync(Strings.TicketCategoryRemoved(ctx.Guild.Id, buttonId));
            else
                await ConfirmAsync(Strings.TicketCategorySet(ctx.Guild.Id, buttonId, category.Name));
        }

        /// <summary>
        ///     Sets the category where closed tickets from a button will be archived.
        /// </summary>
        /// <param name="buttonId">The ID of the button to modify.</param>
        /// <param name="category">The archive category channel, or omit it to remove it.</param>
        [SlashCommand("archive-category", "Sets the category where closed tickets from a button are archived")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TicketArchiveCategory(
            [Summary("button-id", "ID of the button")]
            int buttonId,
            [Summary("category", "Archive category, omit to clear")]
            ICategoryChannel? category = null)
        {
            var panel = await Service.GetButtonAsync(buttonId);
            if (panel == null || panel.Panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.TicketPanelNotFound(ctx.Guild.Id));
                return;
            }

            await DeferAsync();
            await Service.UpdateButtonSettingsAsync(ctx.Guild, buttonId, new Dictionary<string, object>
            {
                {
                    "archiveCategoryId", category?.Id
                }
            });

            if (category == null)
                await ConfirmAsync(Strings.TicketArchiveCategoryRemoved(ctx.Guild.Id, buttonId));
            else
                await ConfirmAsync(Strings.TicketArchiveCategorySet(ctx.Guild.Id, buttonId, category.Name));
        }

        /// <summary>
        ///     Sets auto-archive behavior for a button.
        /// </summary>
        /// <param name="buttonId">The ID of the button to modify.</param>
        /// <param name="enabled">Whether tickets should be archived automatically on close.</param>
        [SlashCommand("auto-archive", "Sets whether tickets from a button are archived automatically on close")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TicketAutoArchive(
            [Summary("button-id", "ID of the button")]
            int buttonId,
            [Summary("enabled", "Whether auto archive is enabled")]
            bool enabled = true)
        {
            await DeferAsync();
            var success = await Service.UpdateButtonSettingsAsync(ctx.Guild, buttonId, new Dictionary<string, object>
            {
                {
                    "autoArchiveOnClose", enabled
                }
            });

            if (success)
            {
                if (enabled)
                    await ConfirmAsync(Strings.AutoArchiveEnabled(ctx.Guild.Id, buttonId));
                else
                    await ConfirmAsync(Strings.AutoArchiveDisabled(ctx.Guild.Id, buttonId));
            }
            else
            {
                await ErrorAsync(Strings.ButtonUpdateFailed(ctx.Guild.Id));
            }
        }

        /// <summary>
        ///     Sets multiple close behaviors for a button at once.
        /// </summary>
        /// <param name="buttonId">The ID of the button to modify.</param>
        /// <param name="autoArchive">Whether to archive automatically on close.</param>
        /// <param name="deleteOnClose">Whether to delete the channel on close.</param>
        /// <param name="lockOnClose">Whether to lock the channel on close.</param>
        /// <param name="renameOnClose">Whether to rename the channel on close.</param>
        /// <param name="deleteDelayMinutes">Minutes to wait before deleting a closed ticket.</param>
        [SlashCommand("close-behavior", "Sets multiple close behaviors for a button at once")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TicketCloseBehavior(
            [Summary("button-id", "ID of the button")]
            int buttonId,
            [Summary("auto-archive", "Archive automatically on close")]
            bool? autoArchive = null,
            [Summary("delete-on-close", "Delete the channel on close")]
            bool? deleteOnClose = null,
            [Summary("lock-on-close", "Lock the channel on close")]
            bool? lockOnClose = null,
            [Summary("rename-on-close", "Rename the channel on close")]
            bool? renameOnClose = null,
            [Summary("delete-delay", "Minutes to wait before deleting")]
            int? deleteDelayMinutes = null)
        {
            var settings = new Dictionary<string, object>();

            if (autoArchive.HasValue)
                settings["autoArchiveOnClose"] = autoArchive.Value;
            if (deleteOnClose.HasValue)
                settings["deleteOnClose"] = deleteOnClose.Value;
            if (lockOnClose.HasValue)
                settings["lockOnClose"] = lockOnClose.Value;
            if (renameOnClose.HasValue)
                settings["renameOnClose"] = renameOnClose.Value;
            if (deleteDelayMinutes.HasValue)
                settings["deleteDelay"] = TimeSpan.FromMinutes(deleteDelayMinutes.Value);

            if (!settings.Any())
            {
                await ReplyErrorAsync(Strings.NoSettingsToUpdate(ctx.Guild.Id));
                return;
            }

            await DeferAsync();
            var success = await Service.UpdateButtonSettingsAsync(ctx.Guild, buttonId, settings);
            if (success)
                await ConfirmAsync(Strings.CloseBehaviorUpdated(ctx.Guild.Id, settings.Count, buttonId));
            else
                await ErrorAsync(Strings.ButtonUpdateFailed(ctx.Guild.Id));
        }

        /// <summary>
        ///     Sets the required response time for tickets created by a button.
        /// </summary>
        /// <param name="buttonId">The ID of the button to modify.</param>
        /// <param name="minutes">The number of minutes, or omit it to disable the requirement.</param>
        [SlashCommand("response-time", "Sets the required staff response time for a button")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TicketResponseTime(
            [Summary("button-id", "ID of the button")]
            int buttonId,
            [Summary("minutes", "Response time in minutes, omit to disable")]
            int? minutes = null)
        {
            await DeferAsync();
            var success = await Service.UpdateRequiredResponseTimeAsync(ctx.Guild, buttonId,
                minutes.HasValue ? TimeSpan.FromMinutes(minutes.Value) : null);

            if (success)
            {
                if (minutes.HasValue)
                    await ConfirmAsync(Strings.ResponseTimeSet(ctx.Guild.Id, minutes.Value));
                else
                    await ConfirmAsync(Strings.ResponseTimeRemoved(ctx.Guild.Id));
            }
            else
            {
                await ErrorAsync(Strings.ResponseTimeUpdateFailed(ctx.Guild.Id));
            }
        }

        /// <summary>
        ///     Sets the auto-close time for tickets created by a button.
        /// </summary>
        /// <param name="buttonId">The ID of the button to modify.</param>
        /// <param name="hours">Hours of inactivity before auto-close, or omit it to disable.</param>
        [SlashCommand("auto-close", "Sets the inactivity auto-close time for a button")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TicketAutoClose(
            [Summary("button-id", "ID of the button")]
            int buttonId,
            [Summary("hours", "Hours of inactivity, omit to disable")]
            int? hours = null)
        {
            await DeferAsync();
            await Service.UpdateButtonSettingsAsync(ctx.Guild, buttonId, new Dictionary<string, object>
            {
                {
                    "autoCloseTime", hours.HasValue ? TimeSpan.FromHours(hours.Value) : null
                }
            });

            if (hours.HasValue)
                await ConfirmAsync(Strings.AutoCloseSet(ctx.Guild.Id, hours.Value));
            else
                await ConfirmAsync(Strings.AutoCloseDisabled(ctx.Guild.Id));
        }

        /// <summary>
        ///     Displays detailed information about a button.
        /// </summary>
        /// <param name="buttonId">The ID of the button to view.</param>
        [SlashCommand("info", "Displays detailed information about a ticket button")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task ButtonInfo([Summary("button-id", "ID of the button")] int buttonId)
        {
            var button = await Service.GetButtonAsync(buttonId);
            if (button == null || button.Panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.ButtonNotFound(ctx.Guild.Id));
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle(Strings.ButtonInformation(ctx.Guild.Id))
                .AddField("Label", button.Label, true)
                .AddField("Style", button.Style, true)
                .AddField("Save Transcripts", button.SaveTranscript, true)
                .AddField("Max Active Tickets", button.MaxActiveTickets, true)
                .AddField("Auto Close Time",
                    button.AutoCloseTime.HasValue ? $"{button.AutoCloseTime.Value.TotalHours} hours" : "Disabled",
                    true)
                .AddField("Required Response Time",
                    button.RequiredResponseTime.HasValue
                        ? $"{button.RequiredResponseTime.Value.TotalMinutes} minutes"
                        : "None", true)
                .WithOkColor();

            if (button.CategoryId.HasValue)
            {
                var category = await ctx.Guild.GetCategoryChannelAsync(button.CategoryId.Value);
                embed.AddField("Ticket Category", category?.Name ?? "Deleted", true);
            }

            if (button.ArchiveCategoryId.HasValue)
            {
                var archiveCategory = await ctx.Guild.GetCategoryChannelAsync(button.ArchiveCategoryId.Value);
                embed.AddField("Archive Category", archiveCategory?.Name ?? "Deleted", true);
            }

            if (button.SupportRoles?.Any() == true)
            {
                var roles = button.SupportRoles
                    .Select(id => ctx.Guild.GetRole(id))
                    .Where(r => r != null)
                    .Select(r => r.Mention);
                embed.AddField("Support Roles", string.Join(", ", roles));
            }

            if (button.ViewerRoles?.Any() == true)
            {
                var roles = button.ViewerRoles
                    .Select(id => ctx.Guild.GetRole(id))
                    .Where(r => r != null)
                    .Select(r => r.Mention);
                embed.AddField("Viewer Roles", string.Join(", ", roles));
            }

            await RespondAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Deletes a button from a panel.
        /// </summary>
        /// <param name="buttonId">The ID of the button to delete.</param>
        [SlashCommand("delete", "Deletes a button from a panel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task DeleteButton([Summary("button-id", "ID of the button")] int buttonId)
        {
            var button = await Service.GetButtonAsync(buttonId);
            if (button == null || button.Panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.ButtonNotFound(ctx.Guild.Id));
                return;
            }

            await DeferAsync();
            var success = await Service.DeleteButtonAsync(ctx.Guild, buttonId);
            if (success)
                await ConfirmAsync(Strings.ButtonDeletedSuccess(ctx.Guild.Id, button.Label));
            else
                await ErrorAsync(Strings.FailedDeleteButton(ctx.Guild.Id));
        }

        /// <summary>
        ///     Sets the title for a button's ticket creation modal.
        /// </summary>
        /// <param name="buttonId">The ID of the button to configure.</param>
        /// <param name="title">The title to display on the modal.</param>
        [SlashCommand("modal-title", "Sets the title of a button's ticket creation modal")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task SetModalTitle(
            [Summary("button-id", "ID of the button")]
            int buttonId,
            [Summary("title", "Title shown on the modal")]
            string title)
        {
            var button = await Service.GetButtonAsync(buttonId);
            if (button == null || button.Panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.ButtonNotFound(ctx.Guild.Id));
                return;
            }

            await DeferAsync();
            var modalConfig = LoadModalConfiguration(button.ModalJson);
            modalConfig.Title = title;

            await Service.UpdateButtonSettingsAsync(ctx.Guild, buttonId, new Dictionary<string, object>
            {
                {
                    "modalJson", JsonSerializer.Serialize(modalConfig)
                }
            });

            await ConfirmAsync(Strings.ModalTitleSet(ctx.Guild.Id, title));
        }

        /// <summary>
        ///     Adds a field to a button's ticket creation modal. The label and configuration are collected in a modal.
        ///     Configuration options are comma separated: paragraph, optional, min:X, max:X.
        /// </summary>
        /// <param name="buttonId">The ID of the button to configure.</param>
        /// <param name="label">The label for the field.</param>
        [SlashCommand("modal-field-add", "Adds a field to a button's ticket creation modal")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AddModalField(
            [Summary("button-id", "ID of the button")]
            int buttonId,
            [Summary("label", "Label for the field")]
            string label)
        {
            var button = await Service.GetButtonAsync(buttonId);
            if (button == null || button.Panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.ButtonNotFound(ctx.Guild.Id));
                return;
            }

            await ctx.Interaction.RespondWithModalAsync<TicketModalFieldModal>($"ticket_modal_field:{buttonId}", null,
                x => x.UpdateTextInput("label", input => input.Value = label));
        }

        /// <summary>
        ///     Handles the modal field submission and adds the field to the button's modal configuration.
        /// </summary>
        /// <param name="buttonIdStr">The ID of the button being configured.</param>
        /// <param name="modal">The modal containing the label and configuration.</param>
        [ModalInteraction("ticket_modal_field:*", true)]
        public async Task AddModalFieldSubmitted(string buttonIdStr, TicketModalFieldModal modal)
        {
            var buttonId = int.Parse(buttonIdStr);
            var button = await Service.GetButtonAsync(buttonId);
            if (button == null || button.Panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.ButtonNotFound(ctx.Guild.Id));
                return;
            }

            await DeferAsync();
            var modalConfig = LoadModalConfiguration(button.ModalJson);
            var label = modal.Label;

            var fieldSettings = new ModalFieldConfig
            {
                Label = label,
                Style = 1,
                Required = true,
                MinLength = 1,
                MaxLength = 1000
            };

            if (!string.IsNullOrWhiteSpace(modal.Config))
            {
                var options = modal.Config.Split(',')
                    .Select(o => o.Trim().ToLower())
                    .ToList();

                foreach (var option in options)
                {
                    switch (option)
                    {
                        case "paragraph":
                            fieldSettings.Style = 2;
                            break;
                        case "optional":
                            fieldSettings.Required = false;
                            break;
                        default:
                        {
                            if (option.StartsWith("min:") && int.TryParse(option[4..], out var min))
                                fieldSettings.MinLength = Math.Max(0, Math.Min(min, 4000));
                            else if (option.StartsWith("max:") && int.TryParse(option[4..], out var max))
                                fieldSettings.MaxLength = Math.Max(fieldSettings.MinLength ?? 0, Math.Min(max, 4000));
                            break;
                        }
                    }
                }
            }

            var fieldId = label.ToLower().Replace(" ", "_");
            modalConfig.Fields[fieldId] = fieldSettings;

            await Service.UpdateButtonSettingsAsync(ctx.Guild, buttonId, new Dictionary<string, object>
            {
                {
                    "modalJson", JsonSerializer.Serialize(modalConfig)
                }
            });

            var embed = new EmbedBuilder()
                .WithTitle(Strings.ModalFieldAdded(ctx.Guild.Id))
                .WithDescription(Strings.ModalFieldAddedSuccess(ctx.Guild.Id, label))
                .AddField("Field ID", fieldId, true)
                .AddField("Style", fieldSettings.Style == 2 ? "Paragraph" : "Short", true)
                .AddField("Required", fieldSettings.Required, true)
                .AddField("Length Limits",
                    $"Min: {fieldSettings.MinLength ?? 0}, Max: {fieldSettings.MaxLength ?? 4000}",
                    true)
                .WithOkColor();

            await ctx.Interaction.FollowupAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Removes a field from a button's ticket creation modal.
        /// </summary>
        /// <param name="buttonId">The ID of the button to configure.</param>
        /// <param name="fieldId">The ID of the field to remove.</param>
        [SlashCommand("modal-field-remove", "Removes a field from a button's ticket creation modal")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task RemoveModalField(
            [Summary("button-id", "ID of the button")]
            int buttonId,
            [Summary("field-id", "ID of the field to remove")]
            string fieldId)
        {
            var button = await Service.GetButtonAsync(buttonId);
            if (button == null || button.Panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.ButtonNotFound(ctx.Guild.Id));
                return;
            }

            if (string.IsNullOrWhiteSpace(button.ModalJson))
            {
                await ReplyErrorAsync(Strings.NoModalConfiguration(ctx.Guild.Id));
                return;
            }

            var modalFields = JsonSerializer.Deserialize<Dictionary<string, ModalFieldConfig>>(button.ModalJson);
            if (!modalFields.ContainsKey(fieldId))
            {
                await ReplyErrorAsync(Strings.FieldNotFoundInModal(ctx.Guild.Id));
                return;
            }

            modalFields.Remove(fieldId);

            await DeferAsync();
            await Service.UpdateButtonSettingsAsync(ctx.Guild, buttonId, new Dictionary<string, object>
            {
                {
                    "modalJson", modalFields.Any() ? JsonSerializer.Serialize(modalFields) : null
                }
            });

            await ConfirmAsync(Strings.ModalFieldRemoved(ctx.Guild.Id, fieldId));
        }

        /// <summary>
        ///     Lists all fields in a button's ticket creation modal.
        /// </summary>
        /// <param name="buttonId">The ID of the button to view.</param>
        [SlashCommand("modal-field-list", "Lists all fields in a button's ticket creation modal")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task ListModalFields([Summary("button-id", "ID of the button")] int buttonId)
        {
            var button = await Service.GetButtonAsync(buttonId);
            if (button == null || button.Panel.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.ButtonNotFound(ctx.Guild.Id));
                return;
            }

            if (string.IsNullOrWhiteSpace(button.ModalJson))
            {
                await ReplyErrorAsync(Strings.NoModalConfiguration(ctx.Guild.Id));
                return;
            }

            var modalFields = JsonSerializer.Deserialize<Dictionary<string, ModalFieldConfig>>(button.ModalJson);

            var embed = new EmbedBuilder()
                .WithTitle(Strings.ModalFieldsForButton(ctx.Guild.Id, button.Label))
                .WithOkColor();

            foreach (var (fieldId, config) in modalFields)
            {
                embed.AddField(fieldId,
                    $"Label: {config.Label}\n" +
                    $"Style: {(config.Style == 2 ? "Paragraph" : "Short")}\n" +
                    $"Required: {config.Required}\n" +
                    $"Length: {config.MinLength ?? 0}-{config.MaxLength ?? 4000}");
            }

            await RespondAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Loads a button's modal configuration, falling back to the legacy field dictionary format.
        /// </summary>
        /// <param name="modalJson">The stored modal json.</param>
        /// <returns>The parsed configuration, or an empty one when nothing is stored.</returns>
        private static ModalConfiguration LoadModalConfiguration(string? modalJson)
        {
            var modalConfig = new ModalConfiguration();
            if (string.IsNullOrWhiteSpace(modalJson))
                return modalConfig;

            try
            {
                modalConfig = JsonSerializer.Deserialize<ModalConfiguration>(modalJson);
            }
            catch
            {
                try
                {
                    modalConfig.Fields = JsonSerializer.Deserialize<Dictionary<string, ModalFieldConfig>>(modalJson);
                }
                catch
                {
                    modalConfig.Fields = new Dictionary<string, ModalFieldConfig>();
                }
            }

            return modalConfig;
        }
    }

    /// <summary>
    ///     Commands for managing ticket tags.
    /// </summary>
    [Group("tags", "Manage ticket tags")]
    public class TagCommands : MewdekoSlashSubmodule<TicketService>
    {
        /// <summary>
        ///     Creates a new tag for tickets.
        /// </summary>
        /// <param name="id">The unique identifier of the tag.</param>
        /// <param name="name">The display name of the tag.</param>
        /// <param name="description">The description of the tag.</param>
        /// <param name="color">The tag color as a name, hex code or rgb values.</param>
        [SlashCommand("add", "Creates a new ticket tag")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TicketAddTag(
            [Summary("id", "Unique identifier of the tag")]
            string id,
            [Summary("name", "Display name of the tag")]
            string name,
            [Summary("description", "Description of the tag")]
            string description,
            [Summary("color", "Color name, hex code or rgb values")]
            string color)
        {
            if (!ColorUtils.TryParseColor(color, out var parsedColor))
            {
                await ReplyErrorAsync(Strings.InvalidColorFormat(ctx.Guild.Id));
                return;
            }

            await DeferAsync();
            var success = await Service.CreateTag(ctx.Guild.Id, id, name, description, parsedColor);
            if (success)
                await ConfirmAsync(Strings.TicketTagCreated(ctx.Guild.Id, name));
            else
                await ErrorAsync(Strings.TicketTagCreateFailed(ctx.Guild.Id));
        }

        /// <summary>
        ///     Removes a tag.
        /// </summary>
        /// <param name="tagId">The identifier of the tag to remove.</param>
        [SlashCommand("remove", "Removes a ticket tag")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TicketRemoveTag([Summary("tag-id", "Identifier of the tag")] string tagId)
        {
            await DeferAsync();
            var success = await Service.DeleteTag(ctx.Guild.Id, tagId);
            if (success)
                await ConfirmAsync(Strings.TicketTagDeleted(ctx.Guild.Id, tagId));
            else
                await ErrorAsync(Strings.TicketTagDeleteFailed(ctx.Guild.Id));
        }

        /// <summary>
        ///     Adds tags to the current ticket.
        /// </summary>
        /// <param name="tags">Space separated tag identifiers to add.</param>
        [SlashCommand("add-many", "Adds tags to the current ticket")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task TicketAddTags([Summary("tags", "Space separated tag identifiers")] string tags)
        {
            await DeferAsync();
            var tagIds = tags.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var success = await Service.AddTicketTags(ctx.Guild, ctx.Channel.Id, tagIds, ctx.User as IGuildUser);
            if (success)
                await ConfirmAsync(Strings.TicketTagsAdded(ctx.Guild.Id));
            else
                await ErrorAsync(Strings.TicketTagsAddFailed(ctx.Guild.Id));
        }

        /// <summary>
        ///     Removes tags from the current ticket.
        /// </summary>
        /// <param name="tags">Space separated tag identifiers to remove.</param>
        [SlashCommand("remove-many", "Removes tags from the current ticket")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task TicketRemoveTags([Summary("tags", "Space separated tag identifiers")] string tags)
        {
            await DeferAsync();
            var tagIds = tags.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var success = await Service.RemoveTicketTags(ctx.Guild, ctx.Channel.Id, tagIds, ctx.User as IGuildUser);
            if (success)
                await ConfirmAsync(Strings.TicketTagsRemoved(ctx.Guild.Id));
            else
                await ErrorAsync(Strings.TicketTagsRemoveFailed(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Commands for managing ticket priority levels.
    /// </summary>
    [Group("priorities", "Manage ticket priority levels")]
    public class PriorityCommands : MewdekoSlashSubmodule<TicketService>
    {
        /// <summary>
        ///     Creates a new priority level for tickets.
        /// </summary>
        /// <param name="id">The unique identifier of the priority.</param>
        /// <param name="name">The display name of the priority.</param>
        /// <param name="emoji">The emoji shown for the priority.</param>
        /// <param name="level">The priority level from 1 to 5.</param>
        /// <param name="pingStaff">Whether staff are pinged for tickets with this priority.</param>
        /// <param name="responseTime">The expected response time, for example 1h30m.</param>
        /// <param name="color">The priority color as a name, hex code or rgb values.</param>
        [SlashCommand("add", "Creates a new ticket priority level")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TicketAddPriority(
            [Summary("id", "Unique identifier of the priority")]
            string id,
            [Summary("name", "Display name of the priority")]
            string name,
            [Summary("emoji", "Emoji shown for the priority")]
            string emoji,
            [Summary("level", "Priority level from 1 to 5")]
            int level,
            [Summary("ping-staff", "Whether staff are pinged")]
            bool pingStaff,
            [Summary("response-time", "Expected response time, for example 1h30m")]
            TimeSpan responseTime,
            [Summary("color", "Color name, hex code or rgb values")]
            string color)
        {
            if (!Emote.TryParse(emoji, out _) && !Emoji.TryParse(emoji, out _))
            {
                await ReplyErrorAsync(Strings.InvalidEmojis(ctx.Guild.Id));
                return;
            }

            if (!ColorUtils.TryParseColor(color, out var parsedColor))
            {
                await ReplyErrorAsync(Strings.InvalidColorFormat(ctx.Guild.Id));
                return;
            }

            await DeferAsync();
            var success = await Service.CreatePriority(ctx.Guild.Id, id, name, emoji, level, pingStaff, responseTime,
                parsedColor);
            if (success)
                await ConfirmAsync(Strings.TicketPriorityCreated(ctx.Guild.Id, name));
            else
                await ErrorAsync(Strings.TicketPriorityCreateFailed(ctx.Guild.Id));
        }

        /// <summary>
        ///     Deletes a priority level.
        /// </summary>
        /// <param name="priorityId">The identifier of the priority to delete.</param>
        [SlashCommand("remove", "Deletes a ticket priority level")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TicketRemovePriority([Summary("priority-id", "Identifier of the priority")] string priorityId)
        {
            await DeferAsync();
            var success = await Service.DeletePriority(ctx.Guild.Id, priorityId);
            if (success)
                await ConfirmAsync(Strings.TicketPriorityDeleted(ctx.Guild.Id, priorityId));
            else
                await ErrorAsync(Strings.TicketPriorityDeleteFailed(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Commands for managing users in the ticket system.
    /// </summary>
    [Group("users", "Manage ticket users")]
    public class UserCommands : MewdekoSlashSubmodule<TicketService>
    {
        /// <summary>
        ///     Blocks a user from creating tickets.
        /// </summary>
        /// <param name="user">The user to block.</param>
        /// <param name="reason">The reason for the block.</param>
        [SlashCommand("block", "Blocks a user from creating tickets")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TicketBlock(
            [Summary("user", "User to block")] IGuildUser user,
            [Summary("reason", "Reason for the block")]
            string? reason = null)
        {
            await DeferAsync();
            var success = await Service.BlacklistUser(ctx.Guild, user.Id, reason);
            if (success)
                await ConfirmAsync(Strings.TicketUserBlocked(ctx.Guild.Id, user.Mention));
            else
                await ErrorAsync(Strings.TicketUserAlreadyBlocked(ctx.Guild.Id));
        }

        /// <summary>
        ///     Unblocks a user from creating tickets.
        /// </summary>
        /// <param name="user">The user to unblock.</param>
        [SlashCommand("unblock", "Unblocks a user from creating tickets")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TicketUnblock([Summary("user", "User to unblock")] IGuildUser user)
        {
            await DeferAsync();
            var success = await Service.UnblacklistUser(ctx.Guild, user.Id);
            if (success)
                await ConfirmAsync(Strings.TicketUserUnblocked(ctx.Guild.Id, user.Mention));
            else
                await ErrorAsync(Strings.TicketUserNotBlocked(ctx.Guild.Id));
        }

        /// <summary>
        ///     Shows ticket statistics for a user.
        /// </summary>
        /// <param name="user">The user to show statistics for, defaults to the caller.</param>
        [SlashCommand("stats", "Shows ticket statistics for a user")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task TicketUserStats(
            [Summary("user", "User to show statistics for")]
            IGuildUser? user = null)
        {
            await DeferAsync();
            user ??= ctx.User as IGuildUser;
            var stats = await Service.GetUserStatistics(ctx.Guild.Id, user.Id);

            var eb = new EmbedBuilder()
                .WithTitle(Strings.TicketUserStats(ctx.Guild.Id, user.Username))
                .AddField(Strings.TotalTickets(ctx.Guild.Id), stats.TotalTickets, true)
                .AddField(Strings.OpenTickets(ctx.Guild.Id), stats.OpenTickets, true)
                .AddField(Strings.ClosedTickets(ctx.Guild.Id), stats.ClosedTickets, true)
                .WithOkColor();

            if (stats.TicketsByType.Any())
            {
                var typeStats = string.Join("\n", stats.TicketsByType.Select(t =>
                    $"{t.Key}: {t.Value}"));
                eb.AddField(Strings.TicketsByType(ctx.Guild.Id), typeStats);
            }

            if (stats.RecentTickets.Any())
            {
                var recentTickets = string.Join("\n", stats.RecentTickets.Select(t =>
                    $"#{t.TicketId} - {t.Type} - {(t.ClosedAt.HasValue ? Strings.Closed(ctx.Guild.Id) : Strings.Open(ctx.Guild.Id))}"));
                eb.AddField(Strings.RecentTickets(ctx.Guild.Id), recentTickets);
            }

            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Administrative bulk operations and statistics for the ticket system.
    /// </summary>
    [Group("admin", "Ticket system administration")]
    public class AdminCommands : MewdekoSlashSubmodule<TicketService>
    {
        /// <summary>
        ///     Shows ticket statistics for the guild.
        /// </summary>
        [SlashCommand("stats", "Shows ticket statistics for the server")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task TicketStats()
        {
            await DeferAsync();
            var stats = await Service.GetGuildStatistics(ctx.Guild.Id);

            var eb = new EmbedBuilder()
                .WithTitle(Strings.TicketStatsTitle(ctx.Guild.Id))
                .AddField(Strings.TotalTickets(ctx.Guild.Id), stats.TotalTickets, true)
                .AddField(Strings.OpenTickets(ctx.Guild.Id), stats.OpenTickets, true)
                .AddField(Strings.ClosedTickets(ctx.Guild.Id), stats.ClosedTickets, true)
                .AddField(Strings.AverageResponseTime(ctx.Guild.Id),
                    $"{stats.AverageResponseTime:F1} " + Strings.Minutes(ctx.Guild.Id), true)
                .AddField(Strings.AverageResolutionTime(ctx.Guild.Id),
                    $"{stats.AverageResolutionTime:F1} " + Strings.Hours(ctx.Guild.Id), true)
                .WithOkColor();

            if (stats.TicketsByType.Any())
            {
                var typeStats = string.Join("\n", stats.TicketsByType.Select(t =>
                    $"{t.Key}: {t.Value}"));
                eb.AddField(Strings.TicketsByType(ctx.Guild.Id), typeStats);
            }

            if (stats.TicketsByPriority.Any())
            {
                var priorityStats = string.Join("\n", stats.TicketsByPriority.Select(p =>
                    $"{p.Key}: {p.Value}"));
                eb.AddField(Strings.TicketsByPriority(ctx.Guild.Id), priorityStats);
            }

            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Moves all tickets from one category to another.
        /// </summary>
        /// <param name="sourceCategory">The category to move tickets out of.</param>
        /// <param name="targetCategory">The category to move tickets into.</param>
        [SlashCommand("move-all", "Moves all tickets from one category to another")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TicketMoveAll(
            [Summary("source", "Category to move tickets out of")]
            ICategoryChannel sourceCategory,
            [Summary("target", "Category to move tickets into")]
            ICategoryChannel targetCategory)
        {
            await DeferAsync();
            var (moved, failed) = await Service.BatchMoveTickets(ctx.Guild, sourceCategory.Id, targetCategory.Id);
            await ConfirmAsync(
                Strings.TicketsMoved(ctx.Guild.Id, moved, failed, sourceCategory.Name, targetCategory.Name));
        }

        /// <summary>
        ///     Closes tickets that have been inactive for the given number of hours.
        /// </summary>
        /// <param name="hours">The number of hours of inactivity.</param>
        [SlashCommand("close-inactive", "Closes tickets inactive for the given number of hours")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TicketCloseInactive([Summary("hours", "Hours of inactivity")] int hours)
        {
            await DeferAsync();
            var (closed, failed) = await Service.BatchCloseInactiveTickets(ctx.Guild, TimeSpan.FromHours(hours));
            await ConfirmAsync(Strings.InactiveTicketsClosed(ctx.Guild.Id, closed, failed, hours));
        }
    }
}