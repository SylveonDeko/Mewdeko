using System.Text;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

public partial class Games
{
    /// <summary>
    /// A module containing poll commands.
    /// </summary>
    [Group]
    public class PollCommands : MewdekoSubmodule<PollService>
    {
        /// <summary>
        /// Starts a poll with a single answer type.
        /// </summary>
        /// <param name="input">The input string for the poll.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <example>.poll "What is your favorite color?";Answer1;2;3;etc</example>
        [Cmd, Aliases, UserPerm(GuildPermission.ManageMessages),
         RequireContext(ContextType.Guild)]
        public Task Poll([Remainder] string input)
            => Poll(PollType.SingleAnswer, input);

        /// <summary>
        /// Starts a poll with the specified type and input.
        /// </summary>
        /// <param name="type">The type of the poll.</param>
        /// <param name="arg">The input string for the poll.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <example>.poll MultiAnswer "What is your favorite color?";Answer1;2;3;etc</example>
        [Cmd, Aliases, UserPerm(GuildPermission.ManageMessages),
         RequireContext(ContextType.Guild)]
        public async Task Poll(PollType type, [Remainder] string arg)
        {
            // Checks if the poll type is set to 'PollEnded'
            if (type == PollType.PollEnded)
                return;

            // Checks if the input string is null, empty, or whitespace
            if (string.IsNullOrWhiteSpace(arg))
                return;

            // Creates a new poll based on the provided parameters
            var poll = PollService.CreatePoll(ctx.Guild.Id,
                ctx.Channel.Id, arg, type);

            // Checks if the poll is null
            if (poll == null)
            {
                // Replies with an error message indicating invalid input
                await ReplyErrorLocalizedAsync("poll_invalid_input").ConfigureAwait(false);
                return;
            }

            // Checks if the number of poll answers exceeds the limit
            if (poll.Answers.Count > 25)
            {
                await ctx.Channel.SendErrorAsync("You can only have up to 25 options!", Config);
                return;
            }

            // Attempts to start the poll
            if (Service.StartPoll(poll))
            {
                // Constructs an embed for the poll
                var eb = new EmbedBuilder().WithOkColor().WithTitle(GetText("poll_created", ctx.User.ToString()))
                    .WithDescription(
                        $"{Format.Bold(poll.Question)}\n\n{string.Join("\n", poll.Answers.Select(x => $"`{x.Index + 1}.` {Format.Bold(x.Text)}"))}");

                // Constructs a component builder for the poll buttons
                var count = 1;
                var builder = new ComponentBuilder();
                foreach (var _ in poll.Answers)
                {
                    var component =
                        new ButtonBuilder(customId: $"pollbutton:{count}", label: count.ToString());
                    count++;
                    try
                    {
                        builder.WithButton(component);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
                }

                // Sends the poll message with the embed and components
                try
                {
                    await ctx.Channel.SendMessageAsync(embed: eb.Build(), components: builder.Build());
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
            else
            {
                // Replies with an error message indicating that a poll is already running
                await ReplyErrorLocalizedAsync("poll_already_running").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Displays the current statistics of the active poll in the guild.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <example>.pollstats</example>
        [Cmd, Aliases, UserPerm(GuildPermission.ManageMessages),
         RequireContext(ContextType.Guild)]
        public async Task PollStats()
        {
            // Tries to get the active poll in the guild
            if (!Service.ActivePolls.TryGetValue(ctx.Guild.Id, out var pr))
                return;

            // Sends an embed with the current poll statistics to the channel
            await ctx.Channel.EmbedAsync(GetStats(pr.Poll, GetText("current_poll_results"))).ConfigureAwait(false);
        }

        /// <summary>
        /// Ends the current poll in the guild and displays the final statistics.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <example>.pollend</example>
        [Cmd, Aliases, UserPerm(GuildPermission.ManageMessages),
         RequireContext(ContextType.Guild)]
        public async Task Pollend()
        {
            Poll p;
            // Stops the current poll in the guild and retrieves its information
            if ((p = await Service.StopPoll(ctx.Guild.Id)) == null)
                return;

            // Constructs an embed with the final poll statistics
            var embed = GetStats(p, GetText("poll_closed"));
            // Sends the embed to the channel
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        /// <summary>
        /// Generates an embed containing the statistics of a poll.
        /// </summary>
        /// <param name="poll">The poll to generate statistics for.</param>
        /// <param name="title">The title of the embed.</param>
        /// <returns>The embed containing the poll statistics.</returns>
        public EmbedBuilder GetStats(Poll poll, string? title)
        {
            // Group the votes by their corresponding answer index and calculate the total votes cast for each answer
            var results = poll.Votes.GroupBy(kvp => kvp.VoteIndex)
                .ToDictionary(x => x.Key, x => x.Sum(_ => 1));

            var totalVotesCast = results.Sum(x => x.Value);

            // Create a new EmbedBuilder with the provided title
            var eb = new EmbedBuilder().WithTitle(title);

            var sb = new StringBuilder()
                .AppendLine(Format.Bold(poll.Question))
                .AppendLine();

            // Retrieve the statistics for each answer, ordered by the number of votes in descending order
            var stats = poll.Answers
                .Select(x =>
                {
                    results.TryGetValue(x.Index, out var votes);

                    return (x.Index, votes, x.Text);
                })
                .OrderByDescending(x => x.votes)
                .ToArray();

            // Append each answer's statistics to the StringBuilder
            foreach (var t in stats)
            {
                var (index, votes, text) = t;
                sb.AppendLine(GetText("poll_result",
                    index + 1,
                    Format.Bold(text),
                    Format.Bold(votes.ToString())));
            }

            // Configure the embed with the description, footer, and color
            return eb.WithDescription(sb.ToString())
                .WithFooter(efb => efb.WithText(GetText("x_votes_cast", totalVotesCast)))
                .WithOkColor();
        }
    }
}