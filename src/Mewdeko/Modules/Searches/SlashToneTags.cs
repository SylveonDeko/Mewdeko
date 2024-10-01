using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Modules.Searches;

/// <summary>
///     Provides slash and message command interactions for searching and explaining tone tags.
/// </summary>
public class SlashToneTags : MewdekoSlashModuleBase<ToneTagService>
{
    /// <summary>
    ///     Executes a slash command to search for explanations of specified tone tags.
    /// </summary>
    /// <param name="query">The tone tag to search for.</param>
    /// <remarks>
    ///     Tone tags are used to express the tone of a message more clearly in text-based communication.
    ///     This command allows users to input a tone tag and receive an explanation for it,
    ///     helping to improve understanding and communication clarity.
    ///     The response is shown only to the user who issued the command (ephemeral).
    /// </remarks>
    /// <returns>A task that represents the asynchronous operation of sending an embed response with the tone tag explanation.</returns>
    [SlashCommand("tone-tags", "Search for a specified tone tag.")]
    [CheckPermissions]
    public Task Search(
        [Summary("query", "the tone tag to search for.")] [Autocomplete(typeof(ToneTagAutocompleter))]
        string query)
    {
        return RespondAsync(embed: Service.GetEmbed(Service.ParseTags(query)).Build(), ephemeral: true);
    }

    /// <summary>
    ///     Executes a message command that interprets the entire content of a message as a tone tag query.
    /// </summary>
    /// <param name="message">The message whose content is to be treated as a tone tag query.</param>
    /// <remarks>
    ///     This command provides a convenient way for users to get explanations for tone tags directly from any message
    ///     content.
    ///     It's particularly useful for quickly clarifying or educating about tone tags in ongoing conversations.
    /// </remarks>
    /// <returns>A task that represents the asynchronous operation of searching for the tone tag contained in the message.</returns>
    [MessageCommand("Tone Tags")]
    public Task ToneTags(SocketMessage message)
    {
        return Search(message.Content);
    }
}