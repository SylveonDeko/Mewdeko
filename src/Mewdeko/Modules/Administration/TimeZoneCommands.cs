using System.Threading.Tasks;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    [Group]
    public class TimeZoneCommands : MewdekoSubmodule<GuildTimezoneService>
    {
        private readonly InteractiveService interactivity;

        public TimeZoneCommands(InteractiveService serv) => interactivity = serv;

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Timezones()
        {
            var timezones = TimeZoneInfo.GetSystemTimeZones()
                .OrderBy(x => x.BaseUtcOffset)
                .ToArray();
            const int timezonesPerPage = 20;

            var curTime = DateTimeOffset.UtcNow;

            var i = 0;
            var timezoneStrings = timezones
                .Select(x => (x, ++i % 2 == 0))
                .Select(data =>
                {
                    var (tzInfo, flip) = data;
                    var nameStr = $"{tzInfo.Id,-30}";
                    var offset = curTime.ToOffset(tzInfo.GetUtcOffset(curTime)).ToString("zzz");
                    if (flip)
                        return $"{offset} {Format.Code(nameStr)}";
                    return $"{Format.Code(offset)} {nameStr}";
                });

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(timezones.Length / 20)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder().WithColor(Mewdeko.OkColor)
                    .WithTitle(GetText("timezones_available"))
                    .WithDescription(string.Join("\n",
                        timezoneStrings.Skip(page * timezonesPerPage)
                            .Take(timezonesPerPage)));
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task Timezone() =>
            await ReplyConfirmLocalizedAsync("timezone_guild", Service.GetTimeZoneOrUtc(ctx.Guild.Id))
                .ConfigureAwait(false);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task Timezone([Remainder] string id)
        {
            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch
            {
                tz = null;
            }

            if (tz == null)
            {
                await ReplyErrorLocalizedAsync("timezone_not_found").ConfigureAwait(false);
                return;
            }

            await Service.SetTimeZone(ctx.Guild.Id, tz);

            await ctx.Channel.SendConfirmAsync(tz.ToString()).ConfigureAwait(false);
        }
    }
}