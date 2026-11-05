using System.IO;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

/// <summary>
///     Slash commands for editing messages the bot posted, by message link.
/// </summary>
[Group("bot-message", "Edit messages the bot posted")]
public class SlashBotMessage : MewdekoSlashModuleBase<BotMessageEditService>
{
    /// <summary>
    ///     Opens a form to replace the text and embeds of a message the bot posted.
    /// </summary>
    /// <param name="link">The link to the bot's message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("edit", "Edits a message the bot posted")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task Edit([Summary("link", "The link to the bot's message")] string link)
    {
        var (result, message) = await Service.FindAsync(ctx.Guild.Id, link).ConfigureAwait(false);
        if (message == null)
        {
            await EphemeralReplyErrorAsync(BotMessageEditService.Explain(Strings, ctx.Guild.Id, result))
                .ConfigureAwait(false);
            return;
        }

        if (!((IGuildUser)ctx.User).GetPermissions((IGuildChannel)message.Channel).ManageMessages)
        {
            await EphemeralReplyErrorAsync(Strings.InsufPermsU(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await RespondWithModalAsync<EditMessageModal>($"administration_edit_message:{message.Channel.Id}:{message.Id}")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows the text and embed json of a message the bot posted.
    /// </summary>
    /// <param name="link">The link to the bot's message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("json", "Shows the text and embed json of a message the bot posted")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task Json([Summary("link", "The link to the bot's message")] string link)
    {
        var (result, message) = await Service.FindAsync(ctx.Guild.Id, link).ConfigureAwait(false);
        if (message == null)
        {
            await EphemeralReplyErrorAsync(BotMessageEditService.Explain(Strings, ctx.Guild.Id, result))
                .ConfigureAwait(false);
            return;
        }

        if (!((IGuildUser)ctx.User).GetPermissions((IGuildChannel)message.Channel).ManageMessages)
        {
            await EphemeralReplyErrorAsync(Strings.InsufPermsU(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
            BotMessageEditService.ToEditableContent(message)));
        await RespondWithFileAsync(stream, "message.json", ephemeral: true).ConfigureAwait(false);
    }
}
