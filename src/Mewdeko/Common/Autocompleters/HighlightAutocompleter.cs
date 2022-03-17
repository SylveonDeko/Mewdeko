using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Mewdeko.Modules.Highlights.Services;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;

namespace Mewdeko.Common.Autocompleters;

public class HighlightAutocompleter : AutocompleteHandler
{
    public DiscordSocketClient Client { get; set; }
    public IDataCache Cache { get; set; }
    public DbService Db { get; set; }

    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction interaction, IParameterInfo parameter, IServiceProvider services)
    {
        await using var uow = Db.GetDbContext();

        var content = (string)interaction.Data.Current.Value;
        var highlightsService = new HighlightsService(Client, Cache, Db);

        return AutocompletionResult.FromSuccess(uow.Highlights.ForUser(context.Guild.Id, context.User.Id)
            .Where(x => x.UserId == context.User.Id && x.GuildId == context.Guild.Id)
            .Select(x => x.Word = x.Word.ToLower().Replace("\n", " | "))
            .Where(x => x.Contains(content.ToLower().Replace("\n", " | ")))
            .Take(20)
            .Select(x => new AutocompleteResult(x, x.Replace(" | ", "\n"))));
    }
}
