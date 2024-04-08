using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games
{
    public partial class Games
    {
        /// <summary>
        /// Group containing commands for speed typing games.
        /// </summary>
        [Group]
        public class SpeedTypingCommands : MewdekoSubmodule<GamesService>
        {
            private readonly DiscordSocketClient _client;
            private readonly GamesService _games;
            private readonly GuildSettingsService _guildSettings;

            /// <summary>
            /// Initializes a new instance of <see cref="SpeedTypingCommands"/>.
            /// </summary>
            /// <param name="client">The discord client</param>
            /// <param name="games">The games service for fetching configs</param>
            /// <param name="guildSettings">The guild settings service</param>
            public SpeedTypingCommands(DiscordSocketClient client, GamesService games,
                GuildSettingsService guildSettings)
            {
                _client = client;
                _games = games;
                _guildSettings = guildSettings;
            }

            /// <summary>
            /// Starts a speed typing game.
            /// </summary>
            /// <param name="args">Arguments for configuring the game.</param>
            /// <example>.typestart</example>
            [Cmd, Aliases, RequireContext(ContextType.Guild),
             MewdekoOptions(typeof(TypingGame.Options))]
            public async Task TypeStart(params string[] args)
            {
                var (options, _) = OptionsParser.ParseFrom(new TypingGame.Options(), args);
                var channel = (ITextChannel)ctx.Channel;

                var game = Service.RunningContests.GetOrAdd(channel.Guild.Id,
                    _ => new TypingGame(_games, _client, channel,
                        _guildSettings.GetPrefix(ctx.Guild).GetAwaiter().GetResult(),
                        options));

                if (game.IsActive)
                {
                    await channel.SendErrorAsync($"Contest already running in {game.Channel.Mention} channel.", Config)
                        .ConfigureAwait(false);
                }
                else
                {
                    await game.Start().ConfigureAwait(false);
                }
            }

            /// <summary>
            /// Stops the current speed typing game.
            /// </summary>
            /// <example>.typestop</example>
            [Cmd, Aliases, RequireContext(ContextType.Guild)]
            public async Task TypeStop()
            {
                var channel = (ITextChannel)ctx.Channel;
                if (Service.RunningContests.TryRemove(channel.Guild.Id, out var game))
                {
                    await game.Stop().ConfigureAwait(false);
                    return;
                }

                await channel.SendErrorAsync("No contest to stop on this channel.", Config).ConfigureAwait(false);
            }

            /// <summary>
            /// Adds a new article for the typing game.
            /// </summary>
            /// <param name="text">The text of the article to add.</param>
            /// <example>.typeadd The quick brown fox jumps over the lazy dog.</example>
            [Cmd, Aliases, RequireContext(ContextType.Guild), OwnerOnly]
            public async Task Typeadd([Remainder] string text)
            {
                var channel = (ITextChannel)ctx.Channel;
                if (string.IsNullOrWhiteSpace(text))
                    return;

                _games.AddTypingArticle(ctx.User, text);

                await channel.SendConfirmAsync("Added new article for typing game.").ConfigureAwait(false);
            }

            /// <summary>
            /// Lists the articles available for the typing game.
            /// </summary>
            /// <param name="page">The page number to display.</param>
            /// <example>.typelist 2</example>
            [Cmd, Aliases, RequireContext(ContextType.Guild)]
            public async Task Typelist(int page = 1)
            {
                var channel = (ITextChannel)ctx.Channel;

                if (page < 1)
                    return;

                var articles = _games.TypingArticles.Skip((page - 1) * 15).Take(15).ToArray();

                if (articles.Length == 0)
                {
                    await channel.SendErrorAsync($"{ctx.User.Mention} `No articles found on that page.`", Config)
                        .ConfigureAwait(false);
                    return;
                }

                var i = (page - 1) * 15;
                await channel.SendConfirmAsync("List of articles for Type Race",
                        string.Join("\n", articles.Select(a => $"`#{++i}` - {a.Text.TrimTo(50)}")))
                    .ConfigureAwait(false);
            }

            /// <summary>
            /// Deletes a typing article by its index.
            /// </summary>
            /// <param name="index">The index of the article to delete.</param>
            /// <example>.typedel 2</example>
            [Cmd, Aliases, RequireContext(ContextType.Guild), OwnerOnly]
            public async Task Typedel(int index)
            {
                var removed = Service.RemoveTypingArticle(--index);

                if (removed is null) return;

                var embed = new EmbedBuilder()
                    .WithTitle($"Removed typing article #{index + 1}")
                    .WithDescription(removed.Text.TrimTo(50))
                    .WithOkColor();

                await ctx.Channel.EmbedAsync(embed);
            }
        }
    }
}