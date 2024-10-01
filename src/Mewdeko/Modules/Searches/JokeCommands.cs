using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Modules.Searches;

public partial class Searches
{
    /// <summary>
    ///     Module for retrieving various types of jokes.
    /// </summary>
    [Group]
    public class JokeCommands : MewdekoSubmodule<SearchesService>
    {
        /// <summary>
        ///     Retrieves a Yo Mama joke.
        /// </summary>
        /// <remarks>
        ///     This command retrieves a Yo Mama joke and sends it to the channel.
        /// </remarks>
        /// <example>
        ///     <code>.yomama</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task Yomama()
        {
            await ctx.Channel.SendConfirmAsync(await Service.GetYomamaJoke().ConfigureAwait(false))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Retrieves a random joke.
        /// </summary>
        /// <remarks>
        ///     This command retrieves a random joke and sends its setup and punchline to the channel.
        /// </remarks>
        /// <example>
        ///     <code>.randjoke</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task Randjoke()
        {
            var (setup, punchline) = await Service.GetRandomJoke().ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(setup, punchline).ConfigureAwait(false);
        }

        /// <summary>
        ///     Retrieves a Chuck Norris joke.
        /// </summary>
        /// <remarks>
        ///     This command retrieves a Chuck Norris joke and sends it to the channel.
        /// </remarks>
        /// <example>
        ///     <code>.chucknorris</code>
        /// </example>
        [Cmd]
        [Aliases]
        public async Task ChuckNorris()
        {
            await ctx.Channel.SendConfirmAsync(await Service.GetChuckNorrisJoke().ConfigureAwait(false))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Retrieves a joke related to World of Warcraft.
        /// </summary>
        /// <remarks>
        ///     This command retrieves a joke related to World of Warcraft and sends it to the channel.
        /// </remarks>
        /// <example>
        ///     <code>.wowjoke</code>
        /// </example>
        [Cmd]
        [Aliases]
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

        /// <summary>
        ///     Retrieves a magic item description related to World of Warcraft.
        /// </summary>
        /// <remarks>
        ///     This command retrieves a magic item description related to World of Warcraft and sends it to the channel.
        /// </remarks>
        /// <example>
        ///     <code>.magicitem</code>
        /// </example>
        [Cmd]
        [Aliases]
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