using System.Text;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Games.Services;
using Mewdeko.Services.Database.Models;

namespace Mewdeko.Modules.Games;

public partial class Games
{
    [Group]
    public class PollCommands : MewdekoSubmodule<PollService>
    {
        private readonly DiscordSocketClient _client;

        public PollCommands(DiscordSocketClient client) => _client = client;

        [MewdekoCommand, Usage, Description, Aliases, UserPerm(GuildPermission.ManageMessages),
         RequireContext(ContextType.Guild)]
        public async Task Poll([Remainder] string arg)
        {
            if (string.IsNullOrWhiteSpace(arg))
                return;

            var poll = PollService.CreatePoll(ctx.Guild.Id,
                ctx.Channel.Id, arg);
            if (poll == null)
            {
                await ReplyErrorLocalizedAsync("poll_invalid_input").ConfigureAwait(false);
                return;
            }

            if (Service.StartPoll(poll))
                await ctx.Channel
                    .EmbedAsync(new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle(GetText("poll_created", ctx.User.ToString()))
                        .WithDescription(
                            Format.Bold(poll.Question) + "\n\n" +
                            string.Join("\n", poll.Answers
                                .Select(x => $"`{x.Index + 1}.` {Format.Bold(x.Text)}"))))
                    .ConfigureAwait(false);
            else
                await ReplyErrorLocalizedAsync("poll_already_running").ConfigureAwait(false);
        }

        [MewdekoCommand, Usage, Description, Aliases, UserPerm(GuildPermission.ManageMessages),
         RequireContext(ContextType.Guild)]
        public async Task PollStats()
        {
            if (!Service.ActivePolls.TryGetValue(ctx.Guild.Id, out var pr))
                return;

            await ctx.Channel.EmbedAsync(GetStats(pr.Poll, GetText("current_poll_results"))).ConfigureAwait(false);
        }

        [MewdekoCommand, Usage, Description, Aliases, UserPerm(GuildPermission.ManageMessages),
         RequireContext(ContextType.Guild)]
        public async Task Pollend()
        {
            Poll p;
            if ((p = Service.StopPoll(ctx.Guild.Id)) == null)
                return;

            var embed = GetStats(p, GetText("poll_closed"));
            await ctx.Channel.EmbedAsync(embed)
                .ConfigureAwait(false);
        }

        public EmbedBuilder GetStats(Poll poll, string title)
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