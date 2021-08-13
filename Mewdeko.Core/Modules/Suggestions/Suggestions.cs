using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Interactivity;
using Interactivity.Confirmation;
using Interactivity.Pagination;
using Interactivity.Selection;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Suggestions.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Suggestions
{
    public partial class Suggestions
    {
        [Group]
        public class SuggestCommands : MewdekoModule<SuggestService>
        {
            public DiscordSocketClient _client;

            public SuggestCommands(DiscordSocketClient client)
            {
                _client = client;
            }
            
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task SuggestMessage([Remainder] string embed)
            {

                CREmbed crEmbed;
                CREmbed.TryParse(embed, out crEmbed);
                if (embed == "-")
                {
                    await _service.SetSuggestionMessage(ctx.Guild, embed);
                    await ctx.Channel.SendConfirmAsync("Suggestions will now have the default look.");
                    return;
                }
                else
                {
                    if (crEmbed is not null && !crEmbed.IsValid || !embed.Contains("%suggest"))
                    {
                        await ctx.Channel.SendErrorAsync("The embed code you provided cannot be used for suggestion messages!");
                        return;
                    }
                    else
                    {
                        await _service.SetSuggestionMessage(ctx.Guild, embed);
                        var ebe = CREmbed.TryParse(_service.GetSuggestionMessage(ctx.Guild), out crEmbed);
                        if (ebe is false)
                        {
                            await _service.SetSuggestionMessage(ctx.Guild, "-");
                            await ctx.Channel.SendErrorAsync("There was an error checking the embed, it may be invalid, so I set the suggest message back to default. Please dont hesitate to ask for embed help in the support server at https://discord.gg/6n3aa9Xapf.");
                            return;
                        }
                        await ctx.Channel.SendConfirmAsync("Sucessfully updated suggestion message!");
                    }
                }

            }
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task AcceptMessage([Remainder] string embed)
            {

                CREmbed crEmbed;
                CREmbed.TryParse(embed, out crEmbed);
                if (embed == "-")
                {
                    await _service.SetAcceptMessage(ctx.Guild, embed);
                    await ctx.Channel.SendConfirmAsync("Accepted Suggestions will now have the default look.");
                    return;
                }
                else
                {
                    if (crEmbed is not null && !crEmbed.IsValid || !embed.Contains("%suggest"))
                    {
                        await ctx.Channel.SendErrorAsync("The embed code you provided cannot be used for accepted suggestion messages!");
                        return;
                    }
                    else
                    {
                        await _service.SetAcceptMessage(ctx.Guild, embed);
                        var ebe = CREmbed.TryParse(_service.GetAcceptMessage(ctx.Guild), out crEmbed);
                        if (ebe is false)
                        {
                            await _service.SetAcceptMessage(ctx.Guild, "-");
                            await ctx.Channel.SendErrorAsync("There was an error checking the embed, it may be invalid, so I set the accept message back to default. Please dont hesitate to ask for embed help in the support server at https://discord.gg/6n3aa9Xapf.");
                            return;
                        }
                        await ctx.Channel.SendConfirmAsync("Sucessfully updated accepted suggestion message!");
                    }
                }

            }
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task ImplementMessage([Remainder] string embed)
            {

                CREmbed crEmbed;
                CREmbed.TryParse(embed, out crEmbed);
                if (embed == "-")
                {
                    await _service.SetImplementMessage(ctx.Guild, embed);
                    await ctx.Channel.SendConfirmAsync("Implemented Suggestions will now have the default look.");
                    return;
                }
                else
                {
                    if (crEmbed is not null && !crEmbed.IsValid || !embed.Contains("%suggest"))
                    {
                        await ctx.Channel.SendErrorAsync("The embed code you provided cannot be used for implemented suggestion messages!");
                        return;
                    }
                    else
                    {
                        await _service.SetImplementMessage(ctx.Guild, embed);
                        var ebe = CREmbed.TryParse(_service.GetImplementMessage(ctx.Guild), out crEmbed);
                        if (ebe is false)
                        {
                            await _service.SetImplementMessage(ctx.Guild, "-");
                            await ctx.Channel.SendErrorAsync("There was an error checking the embed, it may be invalid, so I set the implemented message back to default. Please dont hesitate to ask for embed help in the support server at https://discord.gg/6n3aa9Xapf.");
                            return;
                        }
                        await ctx.Channel.SendConfirmAsync("Sucessfully updated implemented suggestion message!");
                    }
                }

            }
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task DenyMessage([Remainder] string embed)
            {

                CREmbed crEmbed;
                CREmbed.TryParse(embed, out crEmbed);
                if (embed == "-")
                {
                    await _service.SetDenyMessage(ctx.Guild, embed);
                    await ctx.Channel.SendConfirmAsync("Denied Suggestions will now have the default look.");
                    return;
                }
                else
                {
                    if (crEmbed is not null && !crEmbed.IsValid || !embed.Contains("%suggest"))
                    {
                        await ctx.Channel.SendErrorAsync("The embed code you provided cannot be used for denied suggestion messages!");
                        return;
                    }
                    else
                    {
                        await _service.SetDenyMessage(ctx.Guild, embed);
                        var ebe = CREmbed.TryParse(_service.GetDenyMessage(ctx.Guild), out crEmbed);
                        if (ebe is false)
                        {
                            await _service.SetDenyMessage(ctx.Guild, "-");
                            await ctx.Channel.SendErrorAsync("There was an error checking the embed, it may be invalid, so I set the deny message back to default. Please dont hesitate to ask for embed help in the support server at https://discord.gg/6n3aa9Xapf.");
                            return;
                        }
                        await ctx.Channel.SendConfirmAsync("Sucessfully updated denied suggestion message!");
                    }
                }

            }
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task ConsiderMessage([Remainder] string embed)
            {

                CREmbed crEmbed;
                CREmbed.TryParse(embed, out crEmbed);
                if (embed == "-")
                {
                    await _service.SetConsiderMessage(ctx.Guild, embed);
                    await ctx.Channel.SendConfirmAsync("Considered Suggestions will now have the default look.");
                    return;
                }
                else
                {
                    if (crEmbed is not null && !crEmbed.IsValid || !embed.Contains("%suggest"))
                    {
                        await ctx.Channel.SendErrorAsync("The embed code you provided cannot be used for considered suggestion messages!");
                        return;
                    }
                    else
                    {
                        await _service.SetConsiderMessage(ctx.Guild, embed);
                        var ebe = CREmbed.TryParse(_service.GetConsiderMessage(ctx.Guild), out crEmbed);
                        if(ebe is false)
                        {
                            await _service.SetConsiderMessage(ctx.Guild, "-");
                            await ctx.Channel.SendErrorAsync("There was an error checking the embed, it may be invalid, so I set the Consider message back to default. Please dont hesitate to ask for embed help in the support server at https://discord.gg/6n3aa9Xapf.");
                            return;
                        }
                        await ctx.Channel.SendConfirmAsync("Sucessfully updated considered suggestion message!");
                    }
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
                    await _service.SetSuggestionRole(ctx.Guild, name.Id);
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

                await _service.SendSuggestion(ctx.Guild, ctx.User as IGuildUser, ctx.Client as DiscordSocketClient, suggestion, ctx.Channel as ITextChannel);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            public async Task Deny(ulong sid, [Remainder] string reason = null)
            {
                await _service.SendDenyEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid, ctx.Channel as ITextChannel, reason);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            public async Task Accept(ulong sid, [Remainder] string reason = null)
            {
                await _service.SendAcceptEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid, ctx.Channel as ITextChannel, reason);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            public async Task Implemented(ulong sid, [Remainder] string reason = null)
            {
                await _service.SendImplementEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid, ctx.Channel as ITextChannel, reason);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            public async Task Consider(ulong sid, [Remainder] string reason = null)
            {
                await _service.SendConsiderEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid, ctx.Channel as ITextChannel, reason);
            }
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task SuggestMotes([Remainder] string _ = null)
            {
                if (_ == null)
                {
                    await ctx.Channel.SendErrorAsync("You need to either provide emojis or say disable for this to work!");
                    return;
                }
                if (_ != null && _.Contains("disable"))
                {
                    await _service.SetSuggestionEmotes(ctx.Guild, "disable");
                    await ctx.Channel.SendConfirmAsync("Disabled Custom Emotes for Suggestions");
                    return;
                }
                if (_ != null && !_.Contains("disable") && ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote)x.Value).Count() > 5)
                {
                    await ctx.Channel.SendErrorAsync("You may only have up to 5 emotes for suggestions!");
                    return;
                }
                if (!_.Contains("disable") && ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote)x.Value).Count() < 1)
                {
                    await ctx.Channel.SendErrorAsync("You need to specify up to 5 emotes for this command to work!");
                    return;
                }
                else
                {
                    var emotes = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(x => (Emote)x.Value);
                    foreach (var emoji in emotes)
                    {
                        if (!ctx.Guild.GetEmotesAsync().Result.Contains(emoji))
                        {
                            await ctx.Channel.SendMessageAsync(emoji.ToString());
                            await ctx.Channel.SendErrorAsync("One or more emotes you provided is not in this server, please use only emotes in the server.");
                            return;
                        }
                    }
                    var list = new List<string>();
                    foreach (var emote in emotes)
                    {
                        list.Add(emote.ToString());
                    }
                    await _service.SetSuggestionEmotes(ctx.Guild, string.Join(",", list));
                    await ctx.Channel.SendConfirmAsync($"Suggestions will now be reacted with {string.Join(",", list)}");
                }
            }
            //[MewdekoCommand]
            //[Usage]
            //[Description]
            //[Aliases]
            //[RequireContext(ContextType.Guild)]
            //public async Task ReSuggest(ulong num, [Remainder] string suggest)
            //{
            //    await ctx.Message.DeleteAsync();
            //    var sug = _service.Suggestionse(ctx.Guild.Id, num).ToArray();
            //    foreach (var i in sug)
            //        if (i.UserID != ctx.User.Id)
            //        {
            //            await ctx.Channel.SendErrorAsync("This isnt your suggestion!");
            //            return;
            //        }
            //        else
            //        {
            //            var chn = await ctx.Guild.GetTextChannelAsync(SuggestChannel);
            //            var message = await chn.GetMessageAsync(i.MessageID) as IUserMessage;
            //            var eb = message.Embed.First().ToEmbedBuilder();
            //            if (eb.Title.Contains("Implemented") || eb.Title.Contains("Considering") ||
            //                eb.Title.Contains("Accepted") || eb.Title.Contains("Denied"))
            //            {
            //                await ctx.Channel.SendErrorAsync("This suggestion has already been reviewed!");
            //                return;
            //            }

            //            if (eb.Description.Contains("Resuggest:"))
            //            {
            //                var e = eb.Description.IndexOf("**Resuggest:**");
            //                var str = eb.Description.Substring(0, e);
            //                var eb2 = new EmbedBuilder()
            //                    .WithAuthor(eb.Author)
            //                    .WithTitle(eb.Title)
            //                    .WithDescription($"{str}{Format.Bold("Resuggest:")}\n{suggest}")
            //                    .WithOkColor();
            //                await message.ModifyAsync(x => { x.Embed = eb2.Build(); });
            //                return;
            //            }

            //            var eb3 = new EmbedBuilder()
            //                .WithAuthor(eb.Author)
            //                .WithTitle(eb.Title)
            //                .WithDescription($"{eb.Description}\n\n{Format.Bold("Resuggest:")}\n{suggest}")
            //                .WithOkColor();
            //            await message.ModifyAsync(x => { x.Embed = eb3.Build(); });
            //        }
            //}
        }
    }
}