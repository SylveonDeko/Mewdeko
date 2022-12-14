using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

public partial class Games
{
    [Group]
    public class PollCommands : MewdekoSubmodule<PollService>
    {
        [Cmd, Aliases, UserPerm(GuildPermission.ManageMessages),
         RequireContext(ContextType.Guild)]
        public async Task Poll([Remainder] string input)
            => await Poll(PollType.SingleAnswer, input);

        [Cmd, Aliases, UserPerm(GuildPermission.ManageMessages),
         RequireContext(ContextType.Guild)]
        public async Task Poll(PollType type, [Remainder] string arg)
        {
            if (type == PollType.PollEnded)
                return;

            if (string.IsNullOrWhiteSpace(arg))
                return;

            var poll = PollService.CreatePoll(ctx.Guild.Id,
                ctx.Channel.Id, arg, type);
            if (poll.Answers.Count > 25)
            {
                await ctx.Channel.SendErrorAsync("You can only have up to 25 options!");
                return;
            }

            if (poll == null)
            {
                await ReplyErrorLocalizedAsync("poll_invalid_input").ConfigureAwait(false);
                return;
            }

            if (Service.StartPoll(poll))
            {
                var eb = new EmbedBuilder().WithOkColor().WithTitle(GetText("poll_created", ctx.User.ToString()))
                    .WithDescription(
                        $"{Format.Bold(poll.Question)}\n\n{string.Join("\n", poll.Answers.Select(x => $"`{x.Index + 1}.` {Format.Bold(x.Text)}"))}");
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
                await ReplyErrorLocalizedAsync("poll_already_running").ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, UserPerm(GuildPermission.ManageMessages),
         RequireContext(ContextType.Guild)]
        public async Task PollStats()
        {
            if (!Service.ActivePolls.TryGetValue(ctx.Guild.Id, out var pr))
                return;

            await ctx.Channel.EmbedAsync(GetStats(pr.Poll, GetText("current_poll_results"))).ConfigureAwait(false);
        }

        [Cmd, Aliases, UserPerm(GuildPermission.ManageMessages),
         RequireContext(ContextType.Guild)]
        public async Task Pollend()
        {
            Poll p;
            if ((p = await Service.StopPoll(ctx.Guild.Id)) == null)
                return;

            var embed = GetStats(p, GetText("poll_closed"));
            await ctx.Channel.EmbedAsync(embed)
                .ConfigureAwait(false);
        }

        public EmbedBuilder GetStats(Poll poll, string? title)
        {
            var results = poll.Votes.GroupBy(kvp => kvp.VoteIndex)
                .ToDictionary(x => x.Key, x => x.Sum(_ => 1));

            var totalVotesCast = results.Sum(x => x.Value);

            var eb = new EmbedBuilder().WithTitle(title);

            var sb = new StringBuilder()
                .AppendLine(Format.Bold(poll.Question))
                .AppendLine();

            var stats = poll.Answers
                .Select(x =>
                {
                    results.TryGetValue(x.Index, out var votes);

                    return (x.Index, votes, x.Text);
                })
                .OrderByDescending(x => x.votes)
                .ToArray();

            for (var i = 0; i < stats.Length; i++)
            {
                var (index, votes, text) = stats[i];
                sb.AppendLine(GetText("poll_result",
                    index + 1,
                    Format.Bold(text),
                    Format.Bold(votes.ToString())));
            }

            return eb.WithDescription(sb.ToString())
                .WithFooter(efb => efb.WithText(GetText("x_votes_cast", totalVotesCast)))
                .WithOkColor();
        }
    }
}