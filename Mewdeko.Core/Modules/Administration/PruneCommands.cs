using Discord;
using Discord.Commands;
using Mewdeko.Extensions;
using System;
using System.Threading.Tasks;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.Administration.Services;
using ITextChannel = Discord.ITextChannel;

namespace Mewdeko.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class PruneCommands : MewdekoSubmodule<PruneService>
        {
            private static readonly TimeSpan twoWeeks = TimeSpan.FromDays(14);

            
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireUserPermission(GuildPermission.ManageMessages)]
            [RequireContext(ContextType.Guild)]
            public async Task Prune(string parameter = null)
            {
                var user = await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false);

                if (parameter == "-s" || parameter == "--safe")
                    await _service.PruneWhere((ITextChannel)ctx.Channel, 100, (x) => x.Author.Id == user.Id && !x.IsPinned).ConfigureAwait(false);
                else
                    await _service.PruneWhere((ITextChannel)ctx.Channel, 100, (x) => x.Author.Id == user.Id).ConfigureAwait(false);
                ctx.Message.DeleteAfter(3);
            }
            // prune x
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(ChannelPerm.ManageMessages)]
            [BotPerm(ChannelPerm.ManageMessages)]
            [Priority(1)]
            public async Task Prune(int count, string parameter = null)
            {
                count++;
                if (count < 1)
                    return;
                if (count > 1000)
                    count = 1000;

                if (parameter == "-s" || parameter == "--safe")
                    await _service.PruneWhere((ITextChannel)ctx.Channel, count, (x) => !x.IsPinned).ConfigureAwait(false);
                if (parameter == "-nb" || parameter == "--nobots")
                    await _service.PruneWhere((ITextChannel)ctx.Channel, count, (x) => !x.Author.IsBot).ConfigureAwait(false);
                if (parameter == "-ob" || parameter == "--onlybots")
                    await _service.PruneWhere((ITextChannel)ctx.Channel, count, (x) => x.Author.IsBot).ConfigureAwait(false);
                else
                    await _service.PruneWhere((ITextChannel)ctx.Channel, count, x => true).ConfigureAwait(false);
            }

            //prune @user [x]
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(ChannelPerm.ManageMessages)]
            [BotPerm(ChannelPerm.ManageMessages)]
            [Priority(0)]
            public Task Prune(IGuildUser user, int count = 100, string parameter = null)
                => Prune(user.Id, count, parameter);

            //prune userid [x]
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(ChannelPerm.ManageMessages)]
            [BotPerm(ChannelPerm.ManageMessages)]
            [Priority(0)]
            public async Task Prune(ulong userId, int count = 100, string parameter = null)
            {
                if (userId == ctx.User.Id)
                    count++;

                if (count < 1)
                    return;

                if (count > 1000)
                    count = 1000;

                if (parameter == "-s" || parameter == "--safe")
                    await _service.PruneWhere((ITextChannel)ctx.Channel, count, m => m.Author.Id == userId && DateTime.UtcNow - m.CreatedAt < twoWeeks && !m.IsPinned).ConfigureAwait(false);
                else
                    await _service.PruneWhere((ITextChannel)ctx.Channel, count, m => m.Author.Id == userId && DateTime.UtcNow - m.CreatedAt < twoWeeks).ConfigureAwait(false);
            }
        }
    }
}
