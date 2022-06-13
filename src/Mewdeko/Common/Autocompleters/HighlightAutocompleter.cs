using Discord.Interactions;
using System.Threading.Tasks;

namespace Mewdeko.Common.Autocompleters;

public class HighlightAutocompleter : AutocompleteHandler
{
    public HighlightAutocompleter(DbService db)
        => Db = db;

    public DiscordSocketClient Client { get; set; }
    public readonly DbService Db;

    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction interaction, IParameterInfo parameter, IServiceProvider services)
    {
        await using var uow = Db.GetDbContext();

        var content = (string)interaction.Data.Current.Value;

        return AutocompletionResult.FromSuccess(uow.Highlights.ForUser(context.Guild.Id, context.User.Id)
                                                   .Where(x => x.UserId == context.User.Id && x.GuildId == context.Guild.Id)
                                                   .Select(x => x.Word = x.Word.ToLower().Replace("\n", " | "))
                                                   .Where(x => x.Contains(content.ToLower().Replace("\n", " | ")))
                                                   .Take(20)
                                                   .Select(x => new AutocompleteResult(x, x.Replace(" | ", "\n"))));
    }
}
