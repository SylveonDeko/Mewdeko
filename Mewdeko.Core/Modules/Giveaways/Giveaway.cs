using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Humanizer;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Common.TypeReaders.Models;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Giveaways.Services;

namespace Mewdeko.Core.Modules.Giveaways
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
        public async Task GStart(ITextChannel chan, StoopidTime time, [Leftover] string what)
            => await RemindInternal(chan, time.Time, what, ctx.User.Id);

        private async Task RemindInternal(ITextChannel chan, TimeSpan ts, string item, ulong host)
        {
            var eb = new EmbedBuilder()
            {
                Color = Mewdeko.OkColor,
                Title = "Mewdeko Giveaway!",
                Description =
                    $"Prize: {item}\nWinners: 1\nEnd Time: {ts.Humanize()}\nHost: {await ctx.Guild.GetUserAsync(host)}\n\n\nReact to <:Nekoha_nom:866616296291172353> to enter!",
                ImageUrl = "https://cdn.discordapp.com/attachments/866315387703394314/866321920822870026/80942286_p0.png?width=1246&height=701"
            };
            var msg = await chan.SendMessageAsync(embed: eb.Build());

            var emote = Emote.Parse("<:Nekoha_nom:866616296291172353>");
            await msg.AddReactionAsync(emote);
            var time = DateTime.UtcNow + ts;
            var rem = new Services.Database.Models.Giveaways
            {
                ChannelId = chan.Id,
                UserId = host,
                ServerId = ctx.Guild.Id,
                When = time,
                Item = item,
                MessageId = msg.Id
            };

            using (var uow = _db.GetDbContext())
            {
                uow.Giveaways.Add(rem);
                await uow.SaveChangesAsync();
            }

            await Task.Delay(500);
            await ctx.Channel.SendConfirmAsync($"Giveaway started in {chan.Mention}");
        }
    }
}
