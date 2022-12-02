using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Games.Common;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

public partial class Games
{
    [Group]
    public class SpeedTypingCommands : MewdekoSubmodule<GamesService>
    {
        private readonly DiscordSocketClient client;
        private readonly GamesService games;
        private readonly GuildSettingsService guildSettings;

        public SpeedTypingCommands(DiscordSocketClient client, GamesService games, GuildSettingsService guildSettings)
        {
            this.games = games;
            this.guildSettings = guildSettings;
            this.client = client;
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         MewdekoOptions(typeof(TypingGame.Options))]
        public async Task TypeStart(params string[] args)
        {
            var (options, _) = OptionsParser.ParseFrom(new TypingGame.Options(), args);
            var channel = (ITextChannel)ctx.Channel;

            var game = Service.RunningContests.GetOrAdd(channel.Guild.Id,
                _ => new TypingGame(games, client, channel, guildSettings.GetPrefix(ctx.Guild).GetAwaiter().GetResult(), options));

            if (game.IsActive)
            {
                await channel.SendErrorAsync($"Contest already running in {game.Channel.Mention} channel.")
                    .ConfigureAwait(false);
            }
            else
            {
                await game.Start().ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task TypeStop()
        {
            var channel = (ITextChannel)ctx.Channel;
            if (Service.RunningContests.TryRemove(channel.Guild.Id, out var game))
            {
                await game.Stop().ConfigureAwait(false);
                return;
            }

            await channel.SendErrorAsync("No contest to stop on this channel.").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), OwnerOnly]
        public async Task Typeadd([Remainder] string text)
        {
            var channel = (ITextChannel)ctx.Channel;
            if (string.IsNullOrWhiteSpace(text))
                return;

            games.AddTypingArticle(ctx.User, text);

            await channel.SendConfirmAsync("Added new article for typing game.").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Typelist(int page = 1)
        {
            var channel = (ITextChannel)ctx.Channel;

            if (page < 1)
                return;

            var articles = games.TypingArticles.Skip((page - 1) * 15).Take(15).ToArray();

            if (articles.Length == 0)
            {
                await channel.SendErrorAsync($"{ctx.User.Mention} `No articles found on that page.`")
                    .ConfigureAwait(false);
                return;
            }

            var i = (page - 1) * 15;
            await channel.SendConfirmAsync("List of articles for Type Race",
                    string.Join("\n", articles.Select(a => $"`#{++i}` - {a.Text.TrimTo(50)}")))
                .ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), OwnerOnly]
        public async Task Typedel(int index)
        {
            var removed = Service.RemoveTypingArticle(--index);

            if (removed is null) return;

            var embed = new EmbedBuilder()
                .WithTitle($"Removed typing article #{index + 1}")
                .WithDescription(removed.Text.TrimTo(50))
                .WithOkColor();

            await Context.Channel.EmbedAsync(embed);
        }
    }
}