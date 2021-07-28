using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Moderation.Services;

namespace Mewdeko.Modules.Moderation
{
    public partial class Moderation
    {
        [Group]
        public class PurgeCommands : MewdekoSubmodule<PurgeService>
        {
            private static readonly TimeSpan twoWeeks = TimeSpan.FromDays(14);


            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireUserPermission(GuildPermission.ManageMessages)]
            [RequireContext(ContextType.Guild)]
            public async Task Purge(string parameter = null)
            {
                var user = await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false);

                if (parameter == "-s" || parameter == "--safe")
                    await _service
                        .PurgeWhere((ITextChannel) ctx.Channel, 100, x => x.Author.Id == user.Id && !x.IsPinned)
                        .ConfigureAwait(false);
                else
                    await _service.PurgeWhere((ITextChannel) ctx.Channel, 100, x => x.Author.Id == user.Id)
                        .ConfigureAwait(false);
                ctx.Message.DeleteAfter(3);
            }

            // Purge x
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(ChannelPerm.ManageMessages)]
            [BotPerm(ChannelPerm.ManageMessages)]
            [Priority(1)]
            public async Task Purge(int count, string parameter = null)
            {
                count++;
                if (count < 1)
                    return;
                if (count > 1000)
                    count = 1000;

                if (parameter == "-s" || parameter == "--safe")
                {
                    await _service.PurgeWhere((ITextChannel) ctx.Channel, count, x => !x.IsPinned)
                        .ConfigureAwait(false);
                    return;
                }

                if (parameter == "-nb" || parameter == "--nobots")
                {
                    await _service.PurgeWhere((ITextChannel) ctx.Channel, count, x => !x.Author.IsBot)
                        .ConfigureAwait(false);
                    return;
                }

                if (parameter == "-ob" || parameter == "--onlybots")
                    await _service.PurgeWhere((ITextChannel) ctx.Channel, count, x => x.Author.IsBot)
                        .ConfigureAwait(false);
                else
                    await _service.PurgeWhere((ITextChannel) ctx.Channel, count, x => true).ConfigureAwait(false);
            }

            //Purge @user [x]
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(ChannelPerm.ManageMessages)]
            [BotPerm(ChannelPerm.ManageMessages)]
            [Priority(0)]
            public Task Purge(IGuildUser user, int count = 100, string parameter = null)
            {
                return Purge(user.Id, count, parameter);
            }

            //Purge userid [x]
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(ChannelPerm.ManageMessages)]
            [BotPerm(ChannelPerm.ManageMessages)]
            [Priority(0)]
            public async Task Purge(ulong userId, int count = 100, string parameter = null)
            {
                if (userId == ctx.User.Id)
                    count++;

                if (count < 1)
                    return;

                if (count > 1000)
                    count = 1000;

                if (parameter == "-s" || parameter == "--safe")
                    await _service.PurgeWhere((ITextChannel) ctx.Channel, count,
                            m => m.Author.Id == userId && DateTime.UtcNow - m.CreatedAt < twoWeeks && !m.IsPinned)
                        .ConfigureAwait(false);
                else
                    await _service.PurgeWhere((ITextChannel) ctx.Channel, count,
                        m => m.Author.Id == userId && DateTime.UtcNow - m.CreatedAt < twoWeeks).ConfigureAwait(false);
            }
        }
    }
}