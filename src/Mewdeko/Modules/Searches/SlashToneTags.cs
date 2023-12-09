using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Modules.Searches;

public class SlashToneTags : MewdekoSlashModuleBase<ToneTagService>
{
    [SlashCommand("tone-tags", "Search for a specified tone tag."), CheckPermissions]
    public Task Search(
        [Summary("query", "the tone tag to search for.")] [Autocomplete(typeof(ToneTagAutocompleter))]
        string query) =>
        RespondAsync(embed: Service.GetEmbed(Service.ParseTags(query)).Build(), ephemeral: true);

    [MessageCommand("Tone Tags")]
    public Task ToneTags(SocketMessage message) => Search(message.Content);
}