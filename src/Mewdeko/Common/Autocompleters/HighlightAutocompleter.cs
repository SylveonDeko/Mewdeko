using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Mewdeko.Database;
using Mewdeko.Modules.Highlights;
using Mewdeko.Modules.Highlights.Services;

namespace Mewdeko.Common.Autocompleters;

public class HighlightAutocompleter : AutocompleteHandler
{
    public DiscordSocketClient Client { get; set; }
    public IDataCache Cache { get; set; }
    public DbService DB { get; set; }

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction interaction, IParameterInfo parameter, IServiceProvider services)
    {
        var content = (string)interaction.Data.Current.Value;
        var highlightsService = new HighlightsService(Client, Cache, DB);

        return Task.FromResult(AutocompletionResult.FromSuccess(highlightsService.GetForGuild(context.Guild.Id)
            .Where(x => x.UserId == context.User.Id && x.GuildId == context.Guild.Id)
            .Select(x => x.Word = x.Word.ToLower().Replace("\n", " | "))
            .Where(x => x.Contains(content.ToLower().Replace("\n", " | ")))
            .Take(20)
            .Select(x => new AutocompleteResult(x, x.Replace(" | ", "\n")))));
    }
}
