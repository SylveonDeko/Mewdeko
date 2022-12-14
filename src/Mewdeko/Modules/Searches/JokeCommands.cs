using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    [Group]
    public class JokeCommands : MewdekoSubmodule<SearchesService>
    {
        [Cmd, Aliases]
        public async Task Yomama() =>
            await ctx.Channel.SendConfirmAsync(await Service.GetYomamaJoke().ConfigureAwait(false))
                .ConfigureAwait(false);

        [Cmd, Aliases]
        public async Task Randjoke()
        {
            var (setup, punchline) = await Service.GetRandomJoke().ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(setup, punchline).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task ChuckNorris() =>
            await ctx.Channel.SendConfirmAsync(await Service.GetChuckNorrisJoke().ConfigureAwait(false))
                .ConfigureAwait(false);

        [Cmd, Aliases]
        public async Task WowJoke()
        {
            if (Service.WowJokes.Count == 0)
            {
                await ReplyErrorLocalizedAsync("jokes_not_loaded").ConfigureAwait(false);
                return;
            }

            var joke = Service.WowJokes[new MewdekoRandom().Next(0, Service.WowJokes.Count)];
            await ctx.Channel.SendConfirmAsync(joke.Question, joke.Answer).ConfigureAwait(false);
        }

        [Cmd, Aliases]
        public async Task MagicItem()
        {
            if (Service.WowJokes.Count == 0)
            {
                await ReplyErrorLocalizedAsync("magicitems_not_loaded").ConfigureAwait(false);
                return;
            }

            var item = Service.MagicItems[new MewdekoRandom().Next(0, Service.MagicItems.Count)];

            await ctx.Channel.SendConfirmAsync($"✨{item.Name}", item.Description).ConfigureAwait(false);
        }
    }
}