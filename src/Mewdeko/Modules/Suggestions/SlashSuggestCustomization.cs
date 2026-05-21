using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Suggestions.Services;

namespace Mewdeko.Modules.Suggestions;

public partial class SlashSuggestions
{
    /// <summary>
    ///     Manages the customization options for the suggestions system through slash commands.
    /// </summary>
    /// <remarks>
    ///     This class provides a set of slash commands designed for guild administrators to customize the suggestions system.
    ///     It includes commands for setting custom messages for new, accepted, denied, considered, and implemented
    ///     suggestions, configuring the suggestion length limits, defining the channels for posting suggestions based on their
    ///     status, adjusting emote modes, and managing threads and archiving settings related to suggestions.
    /// </remarks>
    [Group("customize", "Manage suggestions!")]
    public class SlashSuggestionsCustomization : MewdekoSlashSubmodule<SuggestionsService>
    {
        /// <summary>
        ///     Sets a custom message template for new suggestions.
        /// </summary>
        /// <param name="embed">The message template. Use "-" to reset to the default message.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("suggestmessage", "Allows to set a custom embed when suggesting.")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task SuggestMessage(string embed)
        {
            if (embed == "-")
            {
                await Service.SetSuggestionMessage(ctx.Guild, embed).ConfigureAwait(false);
                await ctx.Interaction.SendConfirmAsync(Strings.SuggestionsDefaultLook(ctx.Guild.Id))
                    .ConfigureAwait(false);
                return;
            }

            await Service.SetSuggestionMessage(ctx.Guild, embed).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.SuggestionMessageUpdated(ctx.Guild.Id));
        }

        /// <summary>
        ///     Sets the minimum length for suggestions.
        /// </summary>
        /// <param name="length">The minimum number of characters allowed in a suggestion.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("suggestminlength", "Set the minimum suggestion length.")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task MinSuggestionLength(int length)
        {
            if (length >= 2048)
            {
                await ctx.Interaction.SendErrorAsync(Strings.SuggestionLengthInvalid(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            await Service.SetMinLength(ctx.Guild, length).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.MinLengthSet(ctx.Guild.Id, length)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the maximum length for suggestions.
        /// </summary>
        /// <param name="length">The maximum number of characters allowed in a suggestion.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("suggestmaxlength", "Set the maximum suggestion length.")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task MaxSuggestionLength(int length)
        {
            if (length <= 0)
            {
                await ctx.Interaction.SendErrorAsync(Strings.SuggestionLengthInvalid(ctx.Guild.Id), Config)
                    .ConfigureAwait(false);
                return;
            }

            await Service.SetMaxLength(ctx.Guild, length).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.MaxLengthSet(ctx.Guild.Id, length)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets a custom message template for when a suggestion is accepted.
        /// </summary>
        /// <param name="embed">The message template. Use "-" to reset to the default message.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("acceptmessage", "Allows to set a custom embed when a suggestion is accepted.")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task AcceptMessage(string embed)
        {
            if (embed == "-")
            {
                await Service.SetAcceptMessage(ctx.Guild, embed).ConfigureAwait(false);
                await ctx.Interaction.SendConfirmAsync(Strings.AcceptedSuggestionsDefaultLook(ctx.Guild.Id))
                    .ConfigureAwait(false);
                return;
            }

            await Service.SetAcceptMessage(ctx.Guild, embed).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.AcceptedMessageUpdated(ctx.Guild.Id))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets a custom message template for when a suggestion is implemented.
        /// </summary>
        /// <param name="embed">The message template. Use "-" to reset to the default message.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("implementmessage", "Allows to set a custom embed when a suggestion is set implemented.")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task ImplementMessage(string embed)
        {
            if (embed == "-")
            {
                await Service.SetImplementMessage(ctx.Guild, embed).ConfigureAwait(false);
                await ctx.Interaction.SendConfirmAsync(Strings.ImplementedSuggestionsDefaultLook(ctx.Guild.Id))
                    .ConfigureAwait(false);
                return;
            }

            await Service.SetImplementMessage(ctx.Guild, embed).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.ImplementedMessageUpdated(ctx.Guild.Id))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets a custom message template for when a suggestion is denied.
        /// </summary>
        /// <param name="embed">The message template. Use "-" to reset to the default message.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("denymessage", "Allows to set a custom embed when a suggestion is denied.")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task DenyMessage(string embed)
        {
            if (embed == "-")
            {
                await Service.SetDenyMessage(ctx.Guild, embed).ConfigureAwait(false);
                await ctx.Interaction.SendConfirmAsync(Strings.DeniedSuggestionsDefaultLook(ctx.Guild.Id))
                    .ConfigureAwait(false);
                return;
            }

            await Service.SetDenyMessage(ctx.Guild, embed).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.DeniedMessageUpdated(ctx.Guild.Id))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets a custom message template for when a suggestion is considered.
        /// </summary>
        /// <param name="embed">The message template. Use "-" to reset to the default message.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("considermessage", "Allows to set a custom embed when a suggestion is considered.")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task ConsiderMessage(string embed)
        {
            if (embed == "-")
            {
                await Service.SetConsiderMessage(ctx.Guild, embed).ConfigureAwait(false);
                await ctx.Interaction.SendConfirmAsync(Strings.ConsideredSuggestionsDefaultLook(ctx.Guild.Id))
                    .ConfigureAwait(false);
                return;
            }

            await Service.SetConsiderMessage(ctx.Guild, embed).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.ConsideredMessageUpdated(ctx.Guild.Id))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the mode for suggestion emotes to either buttons or reactions.
        /// </summary>
        /// <param name="mode">The mode to set for suggestion emotes.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("emotesmode", "Set whether suggestmotes are buttons or reactions")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task SuggestMotesMode(Suggestions.SuggestEmoteModeEnum mode)
        {
            await Service.SetEmoteMode(ctx.Guild, (int)mode).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.EmoteModeSet(ctx.Guild.Id, mode)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Changes the color of the suggestion button.
        /// </summary>
        /// <param name="type">The color type for the suggestion button.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("buttoncolor", "Change the color of the suggestion button")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task SuggestButtonColor(Suggestions.ButtonType type)
        {
            await Service.SetSuggestButtonColor(ctx.Guild, (int)type).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.SuggestButtonColorSet(ctx.Guild.Id, type.ToString()))
                .ConfigureAwait(false);
            await Service.UpdateSuggestionButtonMessage(ctx.Guild, await Service.GetSuggestButtonMessage(ctx.Guild))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the color for specific emote buttons on suggestions.
        /// </summary>
        /// <param name="num">The button number to change.</param>
        /// <param name="type">The color type for the button.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("emotecolor", "Set the color of each button on a suggestion")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task SuggestMoteColor([Summary("number", "The number you want to change")] int num,
            Suggestions.ButtonType type)
        {
            await Service.SetButtonType(ctx.Guild, num, (int)type).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.SuggestButtonTypeSet(ctx.Guild.Id, num, type))
                .ConfigureAwait(false);
            await Service.UpdateSuggestionButtonMessage(ctx.Guild, await Service.GetSuggestButtonMessage(ctx.Guild))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the channel where accepted suggestions are posted.
        /// </summary>
        /// <param name="channel">The channel for accepted suggestions. Null to disable.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("acceptchannel", "Set the channel accepted suggestions get sent to.")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task AcceptChannel(ITextChannel? channel = null)
        {
            await Service.SetAcceptChannel(ctx.Guild, channel?.Id ?? 0).ConfigureAwait(false);
            if (channel is null)
                await ctx.Interaction.SendConfirmAsync(Strings.AcceptChannelDisabled(ctx.Guild.Id));
            else
                await ctx.Interaction.SendConfirmAsync(Strings.AcceptChannelSet(ctx.Guild.Id, channel.Mention))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the channel where denied suggestions are posted.
        /// </summary>
        /// <param name="channel">The channel for denied suggestions. Null to disable.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("denychannel", "Set the channel denied suggestions go to.")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task DenyChannel(ITextChannel? channel = null)
        {
            await Service.SetDenyChannel(ctx.Guild, channel?.Id ?? 0).ConfigureAwait(false);
            if (channel is null)
                await ctx.Interaction.SendConfirmAsync(Strings.DenyChannelDisabled(ctx.Guild.Id));
            else
                await ctx.Interaction.SendConfirmAsync(Strings.DenyChannelSet(ctx.Guild.Id, channel.Mention))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the channel where considered suggestions are posted.
        /// </summary>
        /// <param name="channel">The channel for considered suggestions. Null to disable.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("considerchannel", "Set the channel considered suggestions go to.")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task ConsiderChannel(ITextChannel? channel = null)
        {
            await Service.SetConsiderChannel(ctx.Guild, channel?.Id ?? 0).ConfigureAwait(false);
            if (channel is null)
                await ctx.Interaction.SendConfirmAsync(Strings.ConsiderChannelDisabled(ctx.Guild.Id));
            else
                await ctx.Interaction.SendConfirmAsync(Strings.StatusRoleChannelSet(ctx.Guild.Id, channel.Mention))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the channel where implemented suggestions are posted.
        /// </summary>
        /// <param name="channel">The channel for implemented suggestions. Null to disable.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("implementchannel", "Set the channel where implemented suggestions go")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task ImplementChannel(ITextChannel? channel = null)
        {
            await Service.SetImplementChannel(ctx.Guild, channel?.Id ?? 0).ConfigureAwait(false);
            if (channel is null)
                await ctx.Interaction.SendConfirmAsync(Strings.ImplementChannelDisabled(ctx.Guild.Id));
            else
                await ctx.Interaction.SendConfirmAsync(Strings.ImplementChannelSet(ctx.Guild.Id, channel.Mention))
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the type of threads used in suggestions.
        /// </summary>
        /// <param name="type">The thread type to be used for suggestions.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        ///     This command allows the administrator to choose between different types of threads for suggestions.
        /// </remarks>
        [SlashCommand("threadstype", "Set the type of threads used in suggestions.")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task SuggestThreadsType(Suggestions.SuggestThreadType type)
        {
            await Service.SetSuggestThreadsType(ctx.Guild, (int)type).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.SuggestionThreadsTypeSet(ctx.Guild.Id, type))
                .ConfigureAwait(false);
        }


        /// <summary>
        ///     Sets whether threads auto-archive on deny.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("archiveondeny", "Set whether threads auto archive on deny")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task ArchiveOnDeny()
        {
            var current = await Service.GetArchiveOnDeny(ctx.Guild);
            await Service.SetArchiveOnDeny(ctx.Guild, !current).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.ArchiveOnDenySet(ctx.Guild.Id, !current))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets whether threads auto-archive on accept.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("archiveonaccept", "Set whether threads auto archive on accept")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task ArchiveOnAccept()
        {
            var current = await Service.GetArchiveOnAccept(ctx.Guild);
            await Service.SetArchiveOnAccept(ctx.Guild, !current).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.ArchiveOnAcceptSet(ctx.Guild.Id, !current))
                .ConfigureAwait(false);
        }


        /// <summary>
        ///     Sets whether threads auto-archive on consider.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("archiveonconsider", "Set whether threads auto archive on consider")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task ArchiveOnConsider()
        {
            var current = await Service.GetArchiveOnConsider(ctx.Guild);
            await Service.SetArchiveOnConsider(ctx.Guild, !current).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.ArchiveOnConsiderSet(ctx.Guild.Id, !current))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets whether threads auto-archive on implement.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("archiveonimplement", "Set whether threads auto archive on implement")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task ArchiveOnImplement()
        {
            var current = await Service.GetArchiveOnImplement(ctx.Guild);
            await Service.SetArchiveOnImplement(ctx.Guild, !current).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync(Strings.ArchiveOnImplementSet(ctx.Guild.Id, !current))
                .ConfigureAwait(false);
        }
    }
}