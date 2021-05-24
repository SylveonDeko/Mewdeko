using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class TimeZoneCommands : MewdekoSubmodule<GuildTimezoneService>
        {
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Timezones(int page = 1)
            {
                page--;

                if (page < 0 || page > 20)
                    return;

                var timezones = TimeZoneInfo.GetSystemTimeZones()
                    .OrderBy(x => x.BaseUtcOffset)
                    .ToArray();
                var timezonesPerPage = 20;

                await ctx.SendPaginatedConfirmAsync(page,
                    curPage => new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle(GetText("timezones_available"))
                        .WithDescription(string.Join("\n",
                            timezones.Skip(curPage * timezonesPerPage).Take(timezonesPerPage).Select(x =>
                                $"`{x.Id,-25}` {(x.BaseUtcOffset < TimeSpan.Zero ? "-" : "+")}{x.BaseUtcOffset:hhmm}"))),
                    timezones.Length, timezonesPerPage).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Timezone()
            {
                await ReplyConfirmLocalizedAsync("timezone_guild", _service.GetTimeZoneOrUtc(ctx.Guild.Id))
                    .ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
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

                _service.SetTimeZone(ctx.Guild.Id, tz);

                await ctx.Channel.SendConfirmAsync(tz.ToString()).ConfigureAwait(false);
            }
        }
    }
}