using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility
{
    public partial class Utility
    {
        [Group]
        public class SuggestCommands : MewdekoSubmodule<SuggestService>
        {
            public DiscordSocketClient _client;

            public SuggestCommands(DiscordSocketClient client)
            {
                _client = client;
                _client.MessageReceived += MessageRecieved;
            }

            public async Task MessageRecieved(SocketMessage msg)
            {
                if (msg.Channel is SocketDMChannel) return;
                var Guild = (msg.Channel as IGuildChannel).Guild;
                var Prefix = CmdHandler.GetPrefix(Guild);
                if (msg.Channel.Id == _service.GetSuggestionChannel(Guild.Id) && msg.Author.IsBot == false &&
                    !msg.Content.StartsWith(Prefix))
                {
                    await msg.DeleteAsync();
                    string n;
                    if (_service.GetSuggestionRole(Guild.Id) != 0)
                    {
                        var role = Guild.GetRole(_service.GetSuggestionRole(Guild.Id));
                        n = role.Mention;
                    }
                    else
                    {
                        n = "_ _";
                    }

                    var sugnum = _service.GetSNum(Guild.Id);
                    await _service.sugnum(Guild, sugnum + 1);
                    var t = await (await Guild.GetTextChannelAsync(_service.GetSuggestionChannel(Guild.Id))).EmbedAsync(
                        new EmbedBuilder()
                            .WithAuthor(msg.Author)
                            .WithTitle($"Suggestion #{_service.GetSNum(Guild.Id)}")
                            .WithDescription(msg.Content)
                            .WithOkColor(), n);
                    var tup = new Emoji("\uD83D\uDC4D");
                    var tdown = new Emoji("\uD83D\uDC4E");
                    IEmote[] reacts = {tup, tdown};
                    foreach (var i in reacts) await t.AddReactionAsync(i);
                    await _service.Suggest(Guild, sugnum + 1, t.Id, msg.Author.Id);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            public async Task SuggestRole(IRole name = null)
            {
                if (name is null)
                {
                    var em = new EmbedBuilder
                    {
                        Description = "Ping on suggest has been disabled.",
                        Color = Mewdeko.OkColor
                    };
                    await _service.SetSuggestionRole(ctx.Guild, 0);
                    await ctx.Message.ReplyAsync(embed: em.Build());
                    return;
                }

                if (_service.GetSuggestionRole(ctx.Guild.Id) == name.Id)
                {
                    var em = new EmbedBuilder
                    {
                        Description = "This is already your suggestion ping!",
                        Color = Mewdeko.OkColor
                    };
                    await ctx.Message.ReplyAsync(embed: em.Build());
                    return;
                }

                if (!(_service.GetSuggestionRole(ctx.Guild.Id) == name.Id) &&
                    !(_service.GetSuggestionRole(ctx.Guild.Id) == 0))
                {
                    var oldrole = ctx.Guild.GetRole(_service.GetSuggestionRole(ctx.Guild.Id));
                    var em = new EmbedBuilder
                    {
                        Description = $"Switched the suggestion role from {oldrole.Mention} to {name.Mention}.",
                        Color = Mewdeko.OkColor
                    };
                    await ctx.Message.ReplyAsync(embed: em.Build());
                    return;
                }

                if (!(name == null))
                {
                    var em = new EmbedBuilder
                    {
                        Description = $"Ping on suggest has been set to ping {name.Mention}.",
                        Color = Mewdeko.OkColor
                    };
                    await _service.SetSuggestionRole(ctx.Guild, name.Id);
                    await ctx.Message.ReplyAsync(embed: em.Build());
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            public async Task SetSuggestChannel(ITextChannel channel = null)
            {
                if (channel == null)
                {
                    await _service.SetSuggestionChannelId(ctx.Guild, 0);
                    await ctx.Channel.SendConfirmAsync("Suggestions Disabled!");
                }
                else
                {
                    await _service.SetSuggestionChannelId(ctx.Guild, channel.Id);
                    var chn2 = await ctx.Guild.GetTextChannelAsync(SuggestChannel);
                    await ctx.Channel.SendConfirmAsync($"Your Suggestion channel has been set to {chn2.Mention}");
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Suggest([Remainder] string suggestion)
            {
                var media = ctx.Message.Attachments.FirstOrDefault();
                await ctx.Message.DeleteAsync();
                if (SuggestChannel == 0)
                {
                    await ctx.Channel.SendErrorAsync(
                        $"A suggestion channel has not been set! Please have someone with the manage messages or Administration perm set one using {CmdHandler.GetPrefix(ctx.Guild)}setsuggestchannel #channel.");
                    return;
                }

                string n;
                if (_service.GetSuggestionRole(ctx.Guild.Id) != 0)
                {
                    var role = ctx.Guild.GetRole(_service.GetSuggestionRole(ctx.Guild.Id));
                    n = role.Mention;
                }
                else
                {
                    n = "_ _";
                }

                var sugnum = this.sugnum;
                await _service.sugnum(ctx.Guild, sugnum + 1);
                var eb = new EmbedBuilder()
                    .WithAuthor(ctx.User)
                    .WithTitle($"Suggestion #{this.sugnum}")
                    .WithDescription(suggestion)
                    .WithOkColor();
                var t = await (await ctx.Guild.GetTextChannelAsync(SuggestChannel))
                    .SendMessageAsync(n, embed: eb.Build());
                var tup = new Emoji("\uD83D\uDC4D");
                var tdown = new Emoji("\uD83D\uDC4E");
                IEmote[] reacts = {tup, tdown};
                foreach (var i in reacts) await t.AddReactionAsync(i);
                await _service.Suggest(ctx.Guild, sugnum + 1, t.Id, ctx.User.Id);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            public async Task Deny(ulong sid, [Remainder] string reason = "None")
            {
                var done = await ctx.Channel.SendConfirmAsync($"Suggestion {sid} set as Denied!");
                done.DeleteAfter(5);
                var chn = await ctx.Guild.GetTextChannelAsync(SuggestChannel);
                var id = _service.Suggestions(ctx.Guild.Id, sid).ToArray();
                foreach (var i in id)
                {
                    var mid = i.MessageID;
                    var message = await chn.GetMessageAsync(mid) as IUserMessage;
                    var eb = message.Embeds.First().ToEmbedBuilder();
                    var user = await ctx.Guild.GetUserAsync(i.UserID);
                    var chan = await user.GetOrCreateDMChannelAsync();
                    await chan.EmbedAsync(new EmbedBuilder()
                        .WithTitle(Format.Bold($" Your Suggestion #{sid} has been Denied."))
                        .AddField(Format.Bold("Suggestion"), eb.Description)
                        .AddField(Format.Bold("Reason"), reason)
                        .WithErrorColor());
                    if (eb.Title.Contains("Denied"))
                    {
                        var eb3 = new EmbedBuilder()
                            .WithAuthor(eb.Author)
                            .WithTitle(eb.Title)
                            .WithDescription(eb.Description)
                            .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                            .WithErrorColor().Build();
                        await message.ModifyAsync(x => { x.Embed = eb3; });
                        return;
                    }

                    if (eb.Title.Contains("Accepted"))
                    {
                        var eb3 = new EmbedBuilder()
                            .WithAuthor(eb.Author)
                            .WithTitle(Format.Bold($"Suggestion #{sid} Denied"))
                            .WithDescription(eb.Description)
                            .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                            .WithErrorColor().Build();
                        await message.ModifyAsync(x => { x.Embed = eb3; });
                        return;
                    }

                    if (eb.Title.Contains("Implemented"))
                    {
                        var eb3 = new EmbedBuilder()
                            .WithAuthor(eb.Author)
                            .WithTitle(Format.Bold($"Suggestion #{sid} Denied"))
                            .WithDescription(eb.Description)
                            .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                            .WithErrorColor().Build();
                        await message.ModifyAsync(x => { x.Embed = eb3; });
                        return;
                    }

                    if (eb.Title.Contains("Considering"))
                    {
                        var eb3 = new EmbedBuilder()
                            .WithAuthor(eb.Author)
                            .WithTitle(Format.Bold($"Suggestion #{sid} Denied"))
                            .WithDescription(eb.Description)
                            .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                            .WithErrorColor().Build();
                        await message.ModifyAsync(x => { x.Embed = eb3; });
                        return;
                    }

                    var eb2 = eb
                        .WithTitle(eb.Title + Format.Bold(" Denied"))
                        .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                        .WithErrorColor().Build();
                    await message.ModifyAsync(x => { x.Embed = eb2; });
                    await message.RemoveAllReactionsAsync();
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            public async Task Accept(ulong sid, [Remainder] string reason = "None")
            {
                var done = await ctx.Channel.SendConfirmAsync($"Suggestion {sid} set as Accepted!");
                done.DeleteAfter(5);
                var chn = await ctx.Guild.GetTextChannelAsync(SuggestChannel);
                var id = _service.Suggestions(ctx.Guild.Id, sid).ToArray();
                foreach (var i in id)
                {
                    var mid = i.MessageID;
                    var message = await chn.GetMessageAsync(mid) as IUserMessage;
                    var eb = message.Embeds.First().ToEmbedBuilder();
                    var user = await ctx.Guild.GetUserAsync(i.UserID);
                    var chan = await user.GetOrCreateDMChannelAsync();
                    await chan.EmbedAsync(new EmbedBuilder()
                        .WithTitle(Format.Bold($" Your Suggestion #{sid} has been Accepted."))
                        .AddField(Format.Bold("Suggestion"), eb.Description)
                        .AddField(Format.Bold("Reason"), reason)
                        .WithColor(0, 255, 0));

                    if (eb.Title.Contains("Accepted"))
                    {
                        var eb3 = new EmbedBuilder()
                            .WithAuthor(eb.Author)
                            .WithTitle(eb.Title)
                            .WithDescription(eb.Description)
                            .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                            .WithColor(0, 255, 0).Build();
                        await message.ModifyAsync(x => { x.Embed = eb3; });
                        return;
                    }

                    if (eb.Title.Contains("Denied"))
                    {
                        var eb3 = new EmbedBuilder()
                            .WithAuthor(eb.Author)
                            .WithTitle(Format.Bold($"Suggestion #{sid} Accepted"))
                            .WithDescription(eb.Description)
                            .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                            .WithColor(0, 255, 0).Build();
                        await message.ModifyAsync(x => { x.Embed = eb3; });
                        return;
                    }

                    if (eb.Title.Contains("Considering"))
                    {
                        var eb3 = new EmbedBuilder()
                            .WithAuthor(eb.Author)
                            .WithTitle(Format.Bold($"Suggestion #{sid} Accepted"))
                            .WithDescription(eb.Description)
                            .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                            .WithColor(0, 255, 0).Build();
                        await message.ModifyAsync(x => { x.Embed = eb3; });
                        return;
                    }

                    if (eb.Title.Contains("Implemented"))
                    {
                        var eb3 = new EmbedBuilder()
                            .WithAuthor(eb.Author)
                            .WithTitle(Format.Bold($"Suggestion #{sid} Accepted"))
                            .WithDescription(eb.Description)
                            .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                            .WithColor(0, 255, 0).Build();
                        await message.ModifyAsync(x => { x.Embed = eb3; });
                        return;
                    }

                    var eb2 = eb
                        .WithTitle(eb.Title + Format.Bold(" Accepted"))
                        .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                        .WithColor(0, 255, 0).Build();
                    await message.ModifyAsync(x => { x.Embed = eb2; });
                    await message.RemoveAllReactionsAsync();
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            public async Task Implemented(ulong sid, [Remainder] string reason = "None")
            {
                var done = await ctx.Channel.SendConfirmAsync($"Suggestion {sid} set as Implemented!");
                done.DeleteAfter(5);
                var chn = await ctx.Guild.GetTextChannelAsync(SuggestChannel);
                var id = _service.Suggestions(ctx.Guild.Id, sid).ToArray();
                foreach (var i in id)
                {
                    var mid = i.MessageID;
                    var message = await chn.GetMessageAsync(mid) as IUserMessage;
                    var eb = message.Embeds.First().ToEmbedBuilder();
                    var user = await ctx.Guild.GetUserAsync(i.UserID);
                    var chan = await user.GetOrCreateDMChannelAsync();
                    await chan.EmbedAsync(new EmbedBuilder()
                        .WithTitle(Format.Bold($" Your Suggestion #{sid} has been Implemented."))
                        .AddField(Format.Bold("Suggestion"), eb.Description)
                        .AddField(Format.Bold("Reason"), reason)
                        .WithColor(0, 255, 0));

                    if (eb.Title.Contains("Accepted"))
                    {
                        var eb3 = new EmbedBuilder()
                            .WithAuthor(eb.Author)
                            .WithTitle(Format.Bold($"Suggestion #{sid} Implemented"))
                            .WithDescription(eb.Description)
                            .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                            .WithColor(0, 255, 0).Build();
                        await message.ModifyAsync(x => { x.Embed = eb3; });
                        return;
                    }

                    if (eb.Title.Contains("Considering"))
                    {
                        var eb3 = new EmbedBuilder()
                            .WithAuthor(eb.Author)
                            .WithTitle(Format.Bold($"Suggestion #{sid} Implemented"))
                            .WithDescription(eb.Description)
                            .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                            .WithColor(0, 255, 0).Build();
                        await message.ModifyAsync(x => { x.Embed = eb3; });
                        return;
                    }

                    if (eb.Title.Contains("Denied"))
                    {
                        var eb3 = new EmbedBuilder()
                            .WithAuthor(eb.Author)
                            .WithTitle(Format.Bold($"Suggestion #{sid} Implemented"))
                            .WithDescription(eb.Description)
                            .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                            .WithColor(0, 255, 0).Build();
                        await message.ModifyAsync(x => { x.Embed = eb3; });
                        return;
                    }

                    if (eb.Title.Contains("Implemented"))
                    {
                        var eb3 = new EmbedBuilder()
                            .WithAuthor(eb.Author)
                            .WithTitle(eb.Title)
                            .WithDescription(eb.Description)
                            .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                            .WithOkColor().Build();
                        await message.ModifyAsync(x => { x.Embed = eb3; });
                        return;
                    }

                    var eb2 = eb
                        .WithTitle(eb.Title + Format.Bold(" Implemented"))
                        .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                        .WithOkColor().Build();
                    await message.ModifyAsync(x => { x.Embed = eb2; });
                    await message.RemoveAllReactionsAsync();
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            public async Task Consider(ulong sid, [Remainder] string reason = "None")
            {
                var done = await ctx.Channel.SendConfirmAsync($"Suggestion {sid} set as Considering!");
                done.DeleteAfter(5);
                var chn = await ctx.Guild.GetTextChannelAsync(SuggestChannel);
                var id = _service.Suggestions(ctx.Guild.Id, sid).ToArray();
                foreach (var i in id)
                {
                    var mid = i.MessageID;
                    var message = await chn.GetMessageAsync(mid) as IUserMessage;
                    var eb = message.Embeds.First().ToEmbedBuilder();
                    var user = await ctx.Guild.GetUserAsync(i.UserID);
                    var chan = await user.GetOrCreateDMChannelAsync();
                    await chan.EmbedAsync(new EmbedBuilder()
                        .WithTitle(Format.Bold($" Your Suggestion #{sid} is being considered."))
                        .AddField(Format.Bold("Suggestion"), eb.Description)
                        .AddField(Format.Bold("Reason"), reason)
                        .WithColor(0, 255, 0));

                    if (eb.Title.Contains("Accepted"))
                    {
                        var eb3 = new EmbedBuilder()
                            .WithAuthor(eb.Author)
                            .WithTitle(Format.Bold($"Suggestion #{sid} Considering"))
                            .WithDescription(eb.Description)
                            .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                            .WithColor(0, 255, 0).Build();
                        await message.ModifyAsync(x => { x.Embed = eb3; });
                        return;
                    }

                    if (eb.Title.Contains("Denied"))
                    {
                        var eb3 = new EmbedBuilder()
                            .WithAuthor(eb.Author)
                            .WithTitle(Format.Bold($"Suggestion #{sid} Considering"))
                            .WithDescription(eb.Description)
                            .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                            .WithColor(0, 255, 0).Build();
                        await message.ModifyAsync(x => { x.Embed = eb3; });
                        return;
                    }

                    if (eb.Title.Contains("Implemented"))
                    {
                        var eb3 = new EmbedBuilder()
                            .WithAuthor(eb.Author)
                            .WithTitle(Format.Bold($"Suggestion #{sid} Considering"))
                            .WithDescription(eb.Description)
                            .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                            .WithOkColor().Build();
                        await message.ModifyAsync(x => { x.Embed = eb3; });
                        return;
                    }

                    if (eb.Title.Contains("Considering"))
                    {
                        var eb3 = new EmbedBuilder()
                            .WithAuthor(eb.Author)
                            .WithTitle(eb.Title)
                            .WithDescription(eb.Description)
                            .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                            .WithOkColor().Build();
                        await message.ModifyAsync(x => { x.Embed = eb3; });
                        return;
                    }

                    var eb2 = eb
                        .WithTitle(eb.Title + Format.Bold(" Considering"))
                        .AddField(Format.Bold($"Reason from {ctx.User}"), reason)
                        .WithOkColor().Build();
                    await message.ModifyAsync(x => { x.Embed = eb2; });
                    await message.RemoveAllReactionsAsync();
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ReSuggest(ulong num, [Remainder] string suggest)
            {
                await ctx.Message.DeleteAsync();
                var sug = _service.Suggestions(ctx.Guild.Id, num).ToArray();
                foreach (var i in sug)
                    if (i.UserID != ctx.User.Id)
                    {
                        await ctx.Channel.SendErrorAsync("This isnt your suggestion!");
                        return;
                    }
                    else
                    {
                        var chn = await ctx.Guild.GetTextChannelAsync(SuggestChannel);
                        var message = await chn.GetMessageAsync(i.MessageID) as IUserMessage;
                        var eb = message.Embeds.First().ToEmbedBuilder();
                        if (eb.Title.Contains("Implemented") || eb.Title.Contains("Considering") ||
                            eb.Title.Contains("Accepted") || eb.Title.Contains("Denied"))
                        {
                            await ctx.Channel.SendErrorAsync("This suggestion has already been reviewed!");
                            return;
                        }

                        if (eb.Description.Contains("Resuggest:"))
                        {
                            var e = eb.Description.IndexOf("**Resuggest:**");
                            var str = eb.Description.Substring(0, e);
                            var eb2 = new EmbedBuilder()
                                .WithAuthor(eb.Author)
                                .WithTitle(eb.Title)
                                .WithDescription($"{str}{Format.Bold("Resuggest:")}\n{suggest}")
                                .WithOkColor();
                            await message.ModifyAsync(x => { x.Embed = eb2.Build(); });
                            return;
                        }

                        var eb3 = new EmbedBuilder()
                            .WithAuthor(eb.Author)
                            .WithTitle(eb.Title)
                            .WithDescription($"{eb.Description}\n\n{Format.Bold("Resuggest:")}\n{suggest}")
                            .WithOkColor();
                        await message.ModifyAsync(x => { x.Embed = eb3.Build(); });
                    }
            }
        }
    }
}