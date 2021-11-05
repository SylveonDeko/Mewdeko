using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Suggestions.Services;

namespace Mewdeko.Modules.Suggestions
{
    public class SuggestionsCommands : MewdekoModule<SuggestionsService>
        {
            public DiscordSocketClient _client;

            public SuggestionsCommands(DiscordSocketClient client)
            {
                _client = client;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPermission.ManageChannels)]
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
                await ctx.Message.DeleteAsync();

                await _service.SendSuggestion(ctx.Guild, ctx.User as IGuildUser, ctx.Client as DiscordSocketClient,
                    suggestion, ctx.Channel as ITextChannel);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPermission.ManageMessages)]
            public async Task Deny(ulong sid, [Remainder] string reason = null)
            {
                await _service.SendDenyEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid,
                    ctx.Channel as ITextChannel, reason);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPermission.ManageMessages)]
            public async Task Accept(ulong sid, [Remainder] string reason = null)
            {
                await _service.SendAcceptEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid,
                    ctx.Channel as ITextChannel, reason);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPermission.ManageMessages)]
            public async Task Implemented(ulong sid, [Remainder] string reason = null)
            {
                await _service.SendImplementEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid,
                    ctx.Channel as ITextChannel, reason);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPermission.ManageMessages)]
            public async Task Consider(ulong sid, [Remainder] string reason = null)
            {
                await _service.SendConsiderEmbed(ctx.Guild, ctx.Client as DiscordSocketClient, ctx.User, sid,
                    ctx.Channel as ITextChannel, reason);
            }
        }
    }