using System.Threading.Tasks;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Modules.Searches;

public class SlashToneTags : MewdekoSlashModuleBase<ToneTagService>
{
    [SlashCommand("tone-tags", "Search for a specified tone tag."), CheckPermissions]
    public async Task Search(
        [Summary("query", "the tone tag to search for.")] [Autocomplete(typeof(ToneTagAutocompleter))]
        string query) =>
        await RespondAsync(embed: Service.GetEmbed(Service.ParseTags(query)).Build(), ephemeral: true).ConfigureAwait(false);

    [MessageCommand("Tone Tags")]
    public async Task ToneTags(SocketMessage message) => await Search(message.Content).ConfigureAwait(false);
}