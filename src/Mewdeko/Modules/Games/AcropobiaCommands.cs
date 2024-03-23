using System.Collections.Immutable;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Games.Common.Acrophobia;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

/// <summary>
/// A module containing various games.
/// </summary>
public partial class Games
{
    /// <summary>
    /// A module containing Acrophobia commands.
    /// </summary>
    /// <param name="client">The discord client</param>
    [Group]
    public class AcropobiaCommands(DiscordSocketClient client) : MewdekoSubmodule<GamesService>
    {
        /// <summary>
        /// Command for starting an Acrophobia game.
        /// </summary>
        /// <param name="args">Arguments passed to the command.</param>
        /// <example>.acro</example>
        [Cmd, Aliases, RequireContext(ContextType.Guild), MewdekoOptions(typeof(AcrophobiaGame.Options))]
        public async Task Acrophobia(params string[] args)
        {
            var (options, _) = OptionsParser.ParseFrom(new AcrophobiaGame.Options(), args);
            var channel = (ITextChannel)ctx.Channel;

            var game = new AcrophobiaGame(options);
            if (Service.AcrophobiaGames.TryAdd(channel.Id, game))
            {
                try
                {
                    game.OnStarted += Game_OnStarted;
                    game.OnEnded += Game_OnEnded;
                    game.OnVotingStarted += Game_OnVotingStarted;
                    game.OnUserVoted += Game_OnUserVoted;
                    client.MessageReceived += ClientMessageReceived;
                    await game.Run().ConfigureAwait(false);
                }
                finally
                {
                    client.MessageReceived -= ClientMessageReceived;
                    Service.AcrophobiaGames.TryRemove(channel.Id, out game);
                    game.Dispose();
                }
            }
            else
            {
                await ReplyErrorLocalizedAsync("acro_running").ConfigureAwait(false);
            }

            Task ClientMessageReceived(SocketMessage msg)
            {
                if (msg.Channel.Id != ctx.Channel.Id)
                    return Task.CompletedTask;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var success = await game.UserInput(msg.Author.Id, msg.Author.ToString(), msg.Content)
                            .ConfigureAwait(false);
                        if (success)
                            await msg.DeleteAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                });

                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Event handler for when an Acrophobia game is started.
        /// </summary>
        /// <param name="game">The Acrophobia game that has started.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private Task Game_OnStarted(AcrophobiaGame game)
        {
            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(GetText("acrophobia"))
                .WithDescription(GetText("acro_started", Format.Bold(string.Join(".", game.StartingLetters))))
                .WithFooter(efb => efb.WithText(GetText("acro_started_footer", game.Opts.SubmissionTime)));

            return ctx.Channel.EmbedAsync(embed);
        }

        /// <summary>
        /// Event handler for when a user votes in an Acrophobia game.
        /// </summary>
        /// <param name="user">The user who voted.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private Task Game_OnUserVoted(string user) =>
            ctx.Channel.SendConfirmAsync(
                GetText("acrophobia"),
                GetText("acro_vote_cast", Format.Bold(user)));

        /// <summary>
        /// Event handler for when voting starts in an Acrophobia game.
        /// </summary>
        /// <param name="game">The Acrophobia game in which voting started.</param>
        /// <param name="submissions">The submissions made by the players.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task Game_OnVotingStarted(AcrophobiaGame game,
            ImmutableArray<KeyValuePair<AcrophobiaUser, int>> submissions)
        {
            switch (submissions.Length)
            {
                case 0:
                    await ctx.Channel.SendErrorAsync(GetText("acrophobia"), GetText("acro_ended_no_sub"))
                        .ConfigureAwait(false);
                    return;
                case 1:
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithDescription(
                                GetText("acro_winner_only",
                                    Format.Bold(submissions.First().Key.UserName)))
                            .WithFooter(efb => efb.WithText(submissions.First().Key.Input)))
                        .ConfigureAwait(false);
                    return;
            }

            var i = 0;
            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"{GetText("acrophobia")} - {GetText("submissions_closed")}")
                .WithDescription(GetText("acro_nym_was",
                    $"{Format.Bold(string.Join(".", game.StartingLetters))}\n--\n{submissions.Aggregate("", (agg, cur) => $"{agg}`{++i}.` **{cur.Key.Input}**\n")}\n--"))
                .WithFooter(efb => efb.WithText(GetText("acro_vote")));

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        /// <summary>
        /// Event handler for when an Acrophobia game ends.
        /// </summary>
        /// <param name="game">The Acrophobia game that has ended.</param>
        /// <param name="votes">The votes received by the players.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task Game_OnEnded(AcrophobiaGame game,
            ImmutableArray<KeyValuePair<AcrophobiaUser, int>> votes)
        {
            if (!votes.Any() || votes.All(x => x.Value == 0))
            {
                await ctx.Channel.SendErrorAsync(GetText("acrophobia"), GetText("acro_no_votes_cast"))
                    .ConfigureAwait(false);
                return;
            }

            var table = votes.OrderByDescending(v => v.Value);
            var winner = table.First();
            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(GetText("acrophobia"))
                .WithDescription(GetText("acro_winner", Format.Bold(winner.Key.UserName),
                    Format.Bold(winner.Value.ToString())))
                .WithFooter(efb => efb.WithText(winner.Key.Input));

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }
    }
}