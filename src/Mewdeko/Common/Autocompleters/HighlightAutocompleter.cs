using System.Threading.Tasks;
using Discord.Interactions;

namespace Mewdeko.Common.Autocompleters;

public class HighlightAutocompleter : AutocompleteHandler
{
    public HighlightAutocompleter(DbService db, IDataCache cache)
    {
        Db = db;
        this.cache = cache;
    }

    public readonly DbService Db;
    private readonly IDataCache cache;

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction interaction, IParameterInfo parameter,
        IServiceProvider services)
    {
        var content = (string)interaction.Data.Current.Value;
        var highlights = cache.GetHighlightsForGuild(context.Guild.Id);

        return Task.FromResult(AutocompletionResult.FromSuccess(highlights
            .Where(x => x.UserId == context.User.Id && x.GuildId == context.Guild.Id)
            .Select(x => x.Word = x.Word.ToLower().Replace("\n", " | "))
            .Where(x => x.Contains(content.ToLower().Replace("\n", " | ")))
            .Take(20)
            .Select(x => new AutocompleteResult(x, x.Replace(" | ", "\n")))));
    }
}