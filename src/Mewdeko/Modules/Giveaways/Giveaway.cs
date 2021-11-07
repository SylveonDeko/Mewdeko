using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Humanizer;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Giveaways.Services;
using Mewdeko.Services;

namespace Mewdeko.Modules.Giveaways
{
    public class Giveaways : MewdekoModule<GiveawayService>
    {
        private DbService _db;
        private readonly GuildTimezoneService _tz;

        public Giveaways(DbService db, GuildTimezoneService tz)
        {
            _db = db;
            _tz = tz;
        }
        
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task GStart(ITextChannel chan, StoopidTime time, [Remainder] string what)
            => await Service.GiveawaysInternal(chan, time.Time, what, ctx.User.Id, ctx.Guild.Id, ctx.Channel as ITextChannel, ctx.Guild);
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task GList(int page = 1)
        {
            if (--page < 0)
                return;

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle("Current active giveaways");

            List<Mewdeko.Services.Database.Models.Giveaways> rems;
            using (var uow = _db.GetDbContext())
            {
                rems = uow.Giveaways.GiveawaysFor(ctx.Guild.Id, page).ToList();
            }

            if (rems.Any())
            {
                var i = 0;
                foreach (var rem in rems)
                {
                    var when = rem.When;
                    var diff = when - DateTime.UtcNow;
                    embed.AddField(
                        $"#{++i + page * 10} {rem.When:HH:mm yyyy-MM-dd} UTC (in {(int)diff.TotalHours}h {diff.Minutes}m)",
                        $@"`Where:` {ctx.Guild.GetTextChannelAsync(rem.ChannelId).Result.Mention} [Jump To Message]({ctx.Guild.GetTextChannelAsync(rem.ChannelId).Result.GetMessageAsync(rem.MessageId).Result.GetJumpUrl()})
`Item:` {rem.Item?.TrimTo(50)}");
                }
            }
            else
            {
                embed.WithDescription("No active giveaways!");
            }

            embed.AddPaginatedFooter(page + 1, null);
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }
        [MewdekoCommand]
        public async Task GEnd(int index)
        {
            Mewdeko.Services.Database.Models.Giveaways rem;
            using var uow = _db.GetDbContext();
            var rems = uow.Giveaways.GiveawaysFor(ctx.Guild.Id, index / 10)
                .ToList();
            var pageIndex = index - 1;
            if (rems.Count > pageIndex)
            {
                rem = rems[pageIndex];
                await Service.GiveawayTimerAction(rem);
                uow.Giveaways.Remove(rem);
                await uow.SaveChangesAsync();
            }
        }

        
    }
}
