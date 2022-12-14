using System.Threading.Tasks;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Gambling.Common;
using Mewdeko.Modules.Gambling.Services;

namespace Mewdeko.Modules.Games;

public partial class Games
{
    [Group]
    public class PlantPickCommands : GamblingSubmodule<PlantPickService>
    {
        private readonly InteractiveService interactivity;
        private readonly LogCommandService logService;

        public PlantPickCommands(LogCommandService logService, GamblingConfigService gss,
            InteractiveService serv) : base(gss)
        {
            interactivity = serv;
            this.logService = logService;
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Pick(string? pass = null)
        {
            if (!string.IsNullOrWhiteSpace(pass) && !pass.IsAlphaNumeric()) return;

            var picked = await Service.PickAsync(ctx.Guild.Id, (ITextChannel)ctx.Channel, ctx.User.Id, pass);

            if (picked > 0)
            {
                var msg = await ReplyConfirmLocalizedAsync("picked", picked + CurrencySign)
                    .ConfigureAwait(false);
                msg.DeleteAfter(10);
            }

            if (((SocketGuild)ctx.Guild).CurrentUser.GuildPermissions.ManageMessages)
            {
                try
                {
                    logService.AddDeleteIgnore(ctx.Message.Id);
                    await ctx.Message.DeleteAsync().ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Plant(int amount = 1, string? pass = null)
        {
            if (amount < 1)
                return;

            if (!string.IsNullOrWhiteSpace(pass) && !pass.IsAlphaNumeric()) return;

            var success = await Service.PlantAsync(ctx.Guild.Id, ctx.Channel, ctx.User.Id, ctx.User.ToString(),
                amount, pass);
            if (!success)
            {
                await ReplyErrorLocalizedAsync("not_enough", CurrencySign).ConfigureAwait(false);
                return;
            }

            if (((SocketGuild)ctx.Guild).CurrentUser.GuildPermissions.ManageMessages)
            {
                logService.AddDeleteIgnore(ctx.Message.Id);
                await ctx.Message.DeleteAsync().ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
#if GLOBAL_Mewdeko
            [OwnerOnly]
#endif
        public async Task GenCurrency()
        {
            var enabled = await Service.ToggleCurrencyGeneration(ctx.Guild.Id, ctx.Channel.Id);
            if (enabled)
                await ReplyConfirmLocalizedAsync("curgen_enabled").ConfigureAwait(false);
            else
                await ReplyConfirmLocalizedAsync("curgen_disabled").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageMessages), OwnerOnly]
        public async Task GenCurList()
        {
            var enabledIn = Service.GetAllGeneratingChannels();

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(enabledIn.Count() / 9)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask;
                var items = enabledIn.Skip(page * 9).Take(9);
                if (!items.Any())
                {
                    return new PageBuilder().WithErrorColor()
                        .WithDescription("-");
                }

                return items.Aggregate(new PageBuilder().WithOkColor(),
                    (eb, i) => eb.AddField(i.GuildId.ToString(), i.ChannelId));
            }
        }
    }
}