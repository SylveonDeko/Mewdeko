using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Common;
using Mewdeko.Core.Modules.Utility.Services;
using Mewdeko.Extensions;
using Mewdeko.Interactive;
using Mewdeko.Interactive.Pagination;

namespace Mewdeko.Modules.Utility
{
    public partial class Utility
    {
        [Group]
        public class InviteCommands : MewdekoSubmodule<InviteService>
        {
            private InteractiveService Interactivity;

            public InviteCommands(InteractiveService serv)
            {
                Interactivity = serv;
            }
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [BotPerm(ChannelPerm.CreateInstantInvite)]
            [UserPerm(ChannelPerm.CreateInstantInvite)]
            [MewdekoOptions(typeof(InviteService.Options))]
            public async Task InviteCreate(params string[] args)
            {
                var (opts, success) = OptionsParser.ParseFrom(new InviteService.Options(), args);
                if (!success)
                    return;

                var ch = (ITextChannel) ctx.Channel;
                var invite = await ch.CreateInviteAsync(opts.Expire, opts.MaxUses, opts.Temporary, opts.Unique)
                    .ConfigureAwait(false);

                await ctx.Channel.SendConfirmAsync($"{ctx.User.Mention} https://discord.gg/{invite.Code}")
                    .ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [BotPerm(ChannelPerm.ManageChannel)]
            public async Task InviteList(int page = 1)
            {
                if (--page < 0)
                    return;

                var invites = await ctx.Guild.GetInvitesAsync().ConfigureAwait(false);

                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(invites.Count() / 9)
                    .WithDefaultEmotes()
                    .Build();

                await Interactivity.SendPaginatorAsync(paginator, Context.Channel, System.TimeSpan.FromMinutes(60));

                Task<PageBuilder> PageFactory(int page)
                {
                    {
                        var i = 1;
                        var invs = invites.OrderByDescending(x => x.Uses).Skip(page * 9).Take(9);
                        if (!invs.Any())
                            return Task.FromResult(new PageBuilder()
                                .WithErrorColor()
                                .WithDescription(GetText("no_invites")));
                        return Task.FromResult(invs.Aggregate(new PageBuilder().WithOkColor(),
                            (acc, inv) => acc.AddField(
                                $"#{i++} {inv.Inviter.ToString().TrimTo(15)} " +
                                $"({inv.Uses} / {(inv.MaxUses == 0 ? "∞" : inv.MaxUses?.ToString())})",
                                inv.Url)));
                    }
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [BotPerm(ChannelPerm.ManageChannel)]
            [UserPerm(ChannelPerm.ManageChannel)]
            public async Task InviteDelete(int index)
            {
                if (--index < 0)
                    return;
                var ch = (ITextChannel) ctx.Channel;

                var invites = await ch.GetInvitesAsync().ConfigureAwait(false);

                if (invites.Count <= index)
                    return;
                var inv = invites.ElementAt(index);
                await inv.DeleteAsync().ConfigureAwait(false);

                await ReplyAsync(GetText("invite_deleted", Format.Bold(inv.Code))).ConfigureAwait(false);
            }
        }
    }
}