using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Extensions.Interactive;
using Mewdeko.Common.Extensions.Interactive.Entities.Page;
using Mewdeko.Common.Extensions.Interactive.Pagination;
using Mewdeko.Common.Extensions.Interactive.Pagination.Lazy;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class DiscordPermOverrideCommands : MewdekoSubmodule<DiscordPermOverrideService>
        {
            private readonly InteractiveService Interactivity;

            public DiscordPermOverrideCommands(InteractiveService serv)
            {
                Interactivity = serv;
            }

            // override stats, it should require that the user has managessages guild permission
            // .po 'stats' add user guild managemessages
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPermission.Administrator)]
            public async Task DiscordPermOverride(CommandOrCrInfo cmd, params GuildPermission[] perms)
            {
                if (perms is null || perms.Length == 0)
                {
                    await Service.RemoveOverride(ctx.Guild.Id, cmd.Name);
                    await ReplyConfirmLocalizedAsync("perm_override_reset");
                    return;
                }

                var aggregatePerms = perms.Aggregate((acc, seed) => seed | acc);
                await Service.AddOverride(Context.Guild.Id, cmd.Name, aggregatePerms);

                await ReplyConfirmLocalizedAsync("perm_override",
                    Format.Bold(aggregatePerms.ToString()),
                    Format.Code(cmd.Name));
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPermission.Administrator)]
            public async Task DiscordPermOverrideReset()
            {
                var result = await PromptUserConfirmAsync(new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription(GetText("perm_override_all_confirm")), ctx.User.Id);

                if (!result)
                    return;
                await Service.ClearAllOverrides(Context.Guild.Id);

                await ReplyConfirmLocalizedAsync("perm_override_all");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPermission.Administrator)]
            public async Task DiscordPermOverrideList(int page = 1)
            {
                if (--page < 0)
                    return;

                var overrides = await Service.GetAllOverrides(Context.Guild.Id);
                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(overrides.Count)
                    .WithDefaultCanceledPage()
                    .WithDefaultEmotes()
                    .Build();
                await Interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

                Task<PageBuilder> PageFactory(int page)
                {
                    var thisPageOverrides = overrides
                        .Skip(9 * page)
                        .Take(9)
                        .ToList();
                    if (thisPageOverrides.Count == 0)
                        return Task.FromResult(new PageBuilder().WithDescription(GetText("perm_override_page_none"))
                            .WithColor(Mewdeko.Services.Mewdeko.ErrorColor));
                    return Task.FromResult(new PageBuilder()
                        .WithDescription(string.Join("\n",
                            thisPageOverrides.Select(ov => $"{ov.Command} => {ov.Perm.ToString()}")))
                        .WithColor(Mewdeko.Services.Mewdeko.OkColor));
                }
            }
        }
    }
}