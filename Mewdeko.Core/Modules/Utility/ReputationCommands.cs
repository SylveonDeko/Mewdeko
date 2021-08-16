using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Webhook;
using Discord.WebSocket;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility
{
    public partial class Utility

    {
        public class Reputation : MewdekoSubmodule<ReputationService>
        {
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task repplus(IUser user, [Remainder] string review = null)
            {
                if (!await CheckContext(ctx.User, user, ctx.Guild)) return;
                if (review is null)
                {
                    await _service.AddRep(ctx.Guild, user as IGuildUser, ctx.User as IGuildUser, "No Review Left!", 1);
                    await ctx.Channel.SendConfirmAsync(
                        $"Succesfully given {user.Mention} a positive review. Leave one with a comment next time!");
                    await PostWebhook(user, ctx.Guild, "Positive", "No review was left!", ctx.User);
                }
                else
                {
                    await _service.AddRep(ctx.Guild, user as IGuildUser, ctx.User as IGuildUser, review, 1);
                    await ctx.Channel.SendConfirmAsync($"Succesfully given {user.Mention} a positive review.");
                    await PostWebhook(user, ctx.Guild, "Positive", review, ctx.User);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task repnegative(IUser user, [Remainder] string review = null)
            {
                if (!await CheckContext(ctx.User, user, ctx.Guild)) return;
                if (review is null)
                {
                    await _service.AddRep(ctx.Guild, user as IGuildUser, ctx.User as IGuildUser, "No Review Left!", 0);
                    await ctx.Channel.SendConfirmAsync(
                        $"Succesfully given {user.Mention} a negative review. Leave one with a comment next time!");
                    await PostWebhook(user, ctx.Guild, "Negative", "No review was left!", ctx.User);
                }
                else
                {
                    await _service.AddRep(ctx.Guild, user as IGuildUser, ctx.User as IGuildUser, review, 0);
                    await ctx.Channel.SendConfirmAsync($"Succesfully given {user.Mention} a negative review.");
                    await PostWebhook(user, ctx.Guild, "Negative", review, ctx.User);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OfficialServerMod]
            public async Task RepBlacklist(ulong Id)
            {
                var user = await ((DiscordSocketClient) ctx.Client).Rest.GetUserAsync(Id);
                if (user is null)
                {
                    await ctx.Channel.SendErrorAsync("Are you sure you got the right ID? No user exists with this ID.");
                    return;
                }

                if (_service._blacklist.Select(x => x.ItemId).Contains(Id))
                {
                    await ctx.Channel.SendErrorAsync("This user is already blacklisted!");
                    return;
                }

                _service.Blacklist(Id);
                await ctx.Channel.SendMessageAsync($"Succesfully Blacklisted {user}");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [OfficialServerMod]
            public async Task RepUnBlacklist(ulong Id)
            {
                var user = await ((DiscordSocketClient) ctx.Client).Rest.GetUserAsync(Id);
                if (user is null)
                {
                    await ctx.Channel.SendErrorAsync("Are you sure you got the right ID? No user exists with this ID.");
                    return;
                }

                if (!_service._blacklist.Select(x => x.ItemId).Contains(Id))
                {
                    await ctx.Channel.SendErrorAsync("This user is not blacklisted!");
                    return;
                }

                _service.UnBlacklist(Id);
                await ctx.Channel.SendMessageAsync($"Succesfully Unblacklisted {user}");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task Reviews(string e)
            {
                await Reviews(null, e);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            public async Task Reviews(IUser user = null, string type = null)
            {
                IUser use = null;
                switch (user)
                {
                    case null:
                        use = ctx.User;
                        break;
                    default:
                        use = user;
                        break;
                }

                if (type is null)
                {
                    var reps = _service.Reputations(use.Id);
                    if (!reps.Any())
                    {
                        if (use == ctx.User)
                        {
                            await ctx.Channel.SendErrorAsync("You have no reviews!");
                            return;
                        }

                        await ctx.Channel.SendErrorAsync("This user has no reviews!");
                        return;
                    }

                    var eb = new EmbedBuilder();
                    eb.Title = $"{use}'s Reviews";
                    eb.AddField("Positive Reviews", _service.Reputations(use.Id).Where(x => x.ReviewType == 1).Count());
                    eb.AddField("Negative Reviews", _service.Reputations(use.Id).Where(x => x.ReviewType == 0).Count());
                    eb.AddField("Latest Review Type", $"{ReviewType(use.Id)}");
                    eb.AddField("Latest Review", GetLastReview(use.Id));
                    eb.ThumbnailUrl = use.RealAvatarUrl(2048).ToString();
                    eb.Color = Mewdeko.OkColor;
                    await ctx.Channel.SendMessageAsync(embed: eb.Build());
                }

                if (type is not null && type.ToLower() == "positive")
                {
                    var reps = _service.Reputations(use.Id).Where(x => x.ReviewType == 1);
                    if (!reps.Any())
                    {
                        if (use == ctx.User)
                        {
                            await ctx.Channel.SendErrorAsync("You have no positive reviews!");
                            return;
                        }

                        if (use != ctx.User)
                        {
                            await ctx.Channel.SendErrorAsync("This user has no positive reviews!");
                            return;
                        }
                    }
                    else
                    {
                        var revs = _service.Reputations(use.Id).Where(x => x.ReviewType == 1);
                        await ctx.SendPaginatedConfirmAsync(0, cur =>
                        {
                            return new EmbedBuilder().WithOkColor()
                                .WithAuthor(x =>
                                    x.WithIconUrl(revs.ToArray().Skip(cur).Take(1).FirstOrDefault().ReviewerAv)
                                        .WithName(revs.ToArray().Skip(cur).Take(1).FirstOrDefault().ReviewerUsername))
                                .WithDescription(string.Join("\n",
                                    revs.ToArray().Skip(cur).Take(1).FirstOrDefault().ReviewMessage))
                                .WithTimestamp(revs.ToArray().Skip(cur).Take(1).FirstOrDefault().DateAdded.Value);
                        }, revs.ToArray().Length, 1).ConfigureAwait(false);
                    }
                }

                if (type is not null && type.ToLower() == "negative")
                {
                    var reps2 = _service.Reputations(use.Id).Where(x => x.ReviewType == 0);
                    if (!reps2.Any())
                    {
                        if (use == ctx.User)
                        {
                            await ctx.Channel.SendErrorAsync("You have no negative reviews!");
                            return;
                        }

                        if (use != ctx.User) await ctx.Channel.SendErrorAsync("This user has no negative reviews!");
                    }
                    else
                    {
                        var revs = _service.Reputations(use.Id).Where(x => x.ReviewType == 0);
                        await ctx.SendPaginatedConfirmAsync(0, cur =>
                        {
                            return new EmbedBuilder().WithOkColor()
                                .WithAuthor(x =>
                                    x.WithIconUrl(revs.ToArray().Skip(cur).Take(1).FirstOrDefault().ReviewerAv)
                                        .WithName(revs.ToArray().Skip(cur).Take(1).FirstOrDefault().ReviewerUsername))
                                .WithDescription(string.Join("\n",
                                    revs.ToArray().Skip(cur).Take(1).FirstOrDefault().ReviewMessage))
                                .WithTimestamp(revs.ToArray().Skip(cur).Take(1).FirstOrDefault().DateAdded.Value);
                        }, revs.ToArray().Length, 1).ConfigureAwait(false);
                    }
                }
            }

            private string ReviewType(ulong user)
            {
                var t = _service.Reputations(user).Last();
                if (t.ReviewType == 1) return "Positive";
                if (t.ReviewType == 0) return "Negative";
                return "";
            }

            private string GetLastReview(ulong user)
            {
                var t = _service.Reputations(user).Last();
                if (t.ReviewMessage == null)
                    return "The reviewer didnt leave a comment.";
                return t.ReviewMessage;
            }

            private async Task<bool> CheckContext(IUser user, IUser user2, IGuild guild)
            {
                if (_service._blacklist.Select(x => x.ItemId).Contains(user.Id))
                {
                    await ctx.Channel.SendErrorAsync(
                        "You have been blacklisted from reviews. Please visit discord.gg/oflaeti and open a ticket using .ticket for more info.");
                    return false;
                }

                var responses = _service.ServerReputations(guild.Id);
                var revreps = _service.ReviewerReps(user.Id);
                var c = responses.OrderByDescending(x => x.DateAdded).Take(30).Count(x =>
                    DateTimeOffset.Now.Subtract(x.DateAdded.Value) <= TimeSpan.FromDays(1));
                var r = revreps.OrderByDescending(x => x.DateAdded).Take(2).Count(x =>
                    DateTimeOffset.Now.Subtract(x.DateAdded.Value) <= TimeSpan.FromSeconds(30));
                if (user == user2)
                {
                    await ctx.Channel.SendErrorAsync("You can't rep yourself!");
                    return false;
                }

                if (DateTimeOffset.Now.Subtract(((IGuildUser) user).CreatedAt) < TimeSpan.FromDays(30))
                {
                    await ctx.Channel.SendErrorAsync("Your account is too young to rep a user!");
                    return false;
                }

                if (responses.Any() && responses.Length >= 30 && c == 30)
                {
                    await ctx.Channel.SendErrorAsync(
                        "This server has reached the daily 30 rep limit! There is going to be a whitelist soon! Come to discord.gg/oflaeti for news on this!");
                    return false;
                }

                return true;
            }

            private async Task PostWebhook(IUser user, IGuild guild, string type, string review, IUser reviewer)
            {
                var s = string.Empty;
                var reps = _service.Reputations(user.Id);
                if (reps.Any())
                {
                    var pos = reps.Where(x => x.ReviewType == 1);
                    var neg = reps.Where(x => x.ReviewType == 0);
                    var poscount = 0;
                    var negcount = 0;
                    if (pos.Any()) poscount = pos.Count();
                    if (neg.Any()) negcount = neg.Count();
                    s = $"{poscount} Positive Reviews\n{negcount} Negative Reviews\nReview:\n{review}";
                }

                var webhook = new DiscordWebhookClient(
                    "https://discord.com/api/webhooks/856361973443854336/MzmEjAxO9ec3XIt3Q9RWQeoc7aOnlEPJQx2mIIvvOzz-19WiEDsY6ly7U8rmDzQ34Mfa");
                var eb = new EmbedBuilder();
                eb.Color = Mewdeko.OkColor;
                eb.Description = s;
                eb.Author = new EmbedAuthorBuilder
                    {IconUrl = user.RealAvatarUrl().ToString(), Name = $"{user.Username}|{user.Id}"};
                eb.Footer = new EmbedFooterBuilder
                {
                    IconUrl = reviewer.RealAvatarUrl().ToString(),
                    Text = $"Reviewed by: {reviewer.Username}|{reviewer.Id}"
                };
                var ebs = new List<Embed> {eb.WithCurrentTimestamp().Build()};
                await webhook.SendMessageAsync($"A {type} reputation was given!", embeds: ebs);
            }
        }
    }
}