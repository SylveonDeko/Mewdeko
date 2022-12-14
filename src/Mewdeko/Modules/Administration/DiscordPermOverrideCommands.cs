using System.Threading.Tasks;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    [Group]
    public class DiscordPermOverrideCommands : MewdekoSubmodule<DiscordPermOverrideService>
    {
        private readonly InteractiveService interactivity;

        public DiscordPermOverrideCommands(InteractiveService serv) => interactivity = serv;

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task DiscordPermOverride(CommandOrCrInfo cmd, params GuildPermission[]? perms)
        {
            if (perms is null || perms.Length == 0)
            {
                await Service.RemoveOverride(ctx.Guild.Id, cmd.Name).ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("perm_override_reset").ConfigureAwait(false);
                return;
            }

            var aggregatePerms = perms.Aggregate((acc, seed) => seed | acc);
            await Service.AddOverride(Context.Guild.Id, cmd.Name, aggregatePerms).ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("perm_override",
                Format.Bold(aggregatePerms.ToString()),
                Format.Code(cmd.Name)).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task DiscordPermOverrideReset()
        {
            var result = await PromptUserConfirmAsync(new EmbedBuilder()
                .WithOkColor()
                .WithDescription(GetText("perm_override_all_confirm")), ctx.User.Id).ConfigureAwait(false);

            if (!result)
                return;
            await Service.ClearAllOverrides(Context.Guild.Id).ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("perm_override_all").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task DiscordPermOverrideList()
        {
            var overrides = await Service.GetAllOverrides(Context.Guild.Id).ConfigureAwait(false);
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(overrides.Count / 9)
                .WithDefaultCanceledPage()
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();
            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var thisPageOverrides = overrides
                    .Skip(9 * page)
                    .Take(9)
                    .ToList();
                if (thisPageOverrides.Count == 0)
                {
                    return new PageBuilder().WithDescription(GetText("perm_override_page_none"))
                        .WithColor(Mewdeko.ErrorColor);
                }

                return new PageBuilder()
                    .WithDescription(string.Join("\n",
                        thisPageOverrides.Select(ov => $"{ov.Command} => {ov.Perm.ToString()}")))
                    .WithColor(Mewdeko.OkColor);
            }
        }
    }
}