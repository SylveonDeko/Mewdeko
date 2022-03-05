using System.Text;
using Discord;
using Discord.Commands;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Database.Models;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

public partial class Games
{
    [Group]
    public class PollCommands : MewdekoSubmodule<PollService>
    {
        [MewdekoCommand, Usage, Description, Aliases, UserPerm(GuildPermission.ManageMessages),
         RequireContext(ContextType.Guild)]
        public async Task Test()
        {
            var builder = new ComponentBuilder().WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "1")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "2")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "3")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "4")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "5")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "6")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "7")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "8")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "9")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "10")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "11")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "12")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "13")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "14")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "15")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "16")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "17")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "18")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "19")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "20")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "21")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "22")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "23")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "24")
                                                .WithButton("12345678911234567892123456789312345678941234567895123456789612345678971234567898", "25");
            await ctx.Channel.SendMessageAsync("Button test", components: builder.Build());
        }
        [MewdekoCommand, Usage, Description, Aliases, UserPerm(GuildPermission.ManageMessages),
         RequireContext(ContextType.Guild)]
        public async Task Poll([Remainder] string input) 
            => await Poll(PollType.SingleAnswer, input);
        
        [MewdekoCommand, Usage, Description, Aliases, UserPerm(GuildPermission.ManageMessages),
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
                                           .WithDescription(Format.Bold(poll.Question)
                                                            + "\n\n"
                                                            + string.Join("\n",
                                                                poll.Answers.Select(x =>
                                                                    $"`{x.Index + 1}.` {Format.Bold(x.Text)}")));
                int count = 1;
                var builder = new ComponentBuilder();
                foreach (var i in poll.Answers)
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