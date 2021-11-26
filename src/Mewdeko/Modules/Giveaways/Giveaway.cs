using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Giveaways.Services;
using Mewdeko.Services;

namespace Mewdeko.Modules.Giveaways
{
    public class GiveawayCommands : MewdekoModuleBase<GiveawayService>
    {
        private DbService _db;
        private readonly IServiceProvider _servs;

        public GiveawayCommands(DbService db, IServiceProvider servs)
        {
            _db = db;
            _servs = servs;
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task GStats()
        {
            var eb = new EmbedBuilder().WithOkColor();
            var gways = _db.GetDbContext().Giveaways.GiveawaysForGuild(ctx.Guild.Id);
            if (!gways.Any())
            {
                await ctx.Channel.SendErrorAsync("There have been no giveaways here, so no stats!");
            }
            else
            {
                List<ITextChannel> gchans = new();
                foreach (var i in gways)
                {
                    var chan = await ctx.Guild.GetTextChannelAsync(i.ChannelId);
                    if (!gchans.Contains(chan))
                        gchans.Add(chan);
                }
                var amount = gways.Distinct(x => x.UserId).Count();
                eb.WithTitle("Giveaway Statistics!");
                eb.AddField("Amount of users that started giveaways", amount, true);
                eb.AddField("Total amount of giveaways", gways.Count, true);
                eb.AddField("Active Giveaways", gways.Count(x => x.Ended == 0), true);
                eb.AddField("Ended Giveaways", gways.Count(x => x.Ended == 1), true);
                eb.AddField("Giveaway Channels: Uses",
                    string.Join("\n", gchans.Select(x => $"{x.Mention}: {gways.Count(s => s.ChannelId == x.Id)}")),
                    true);

                await ctx.Channel.SendMessageAsync(embed: eb.Build());
            }
        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task GStart(ITextChannel chan, StoopidTime time, int winners, [Remainder] string what)
            => await Service.GiveawaysInternal(chan, time.Time, what, winners, ctx.User.Id, ctx.Guild.Id, ctx.Channel as ITextChannel, ctx.Guild);

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task GStart()
        {
            ITextChannel chan = null;
            int winners = 0;
            string prize;
            string blacklistroles;
            string blacklistusers;
            string reqroles;
            IUser Host;
            TimeSpan time;
            var erorrembed = new EmbedBuilder()
                .WithErrorColor()
                .WithDescription("Either something went wrong or you input a value incorrectly! Please start over.").Build();
            var win0embed = new EmbedBuilder()
                .WithErrorColor()
                .WithDescription("You can't have 0 winners!").Build();
            int tries = 0;
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithDescription(
                    "Please say, mention or put the ID of the channel where you want to start a giveaway. (Keep in mind you can cancel this by just leaving this to sit)");
            var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build());
            var next = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            var reader = new ChannelTypeReader<ITextChannel>(); 
            var e = await reader.ReadAsync(ctx, next, _servs);
            if (!e.IsSuccess)
            {
                await msg.ModifyAsync(x => x.Embed = erorrembed);
                return;
            }
            chan = (ITextChannel) e.BestMatch;
            await msg.ModifyAsync(x => x.Embed = eb.WithDescription("How many winners will there be?").Build());
            next = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            try
            {
                tries = int.Parse(next);
            }
            catch
            {
                await msg.ModifyAsync(x => x.Embed = erorrembed);
                return;
            }

            while (tries == 0)
            {
                await msg.ModifyAsync(x => x.Embed = win0embed);
                next = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
                try
                {
                    tries = int.Parse(next);
                }
                catch
                {
                    await msg.ModifyAsync(x => x.Embed = erorrembed);
                    return;
                }
            }

            await msg.ModifyAsync(x => x.Embed = eb.WithDescription("What is the prize/item?").Build());
            prize = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            await msg.ModifyAsync(x => x.Embed = eb.WithDescription("How long will this giveaway last? Use the format 1mo,2d,3m,4s").Build());
            next = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            {
                try
                {
                    var t = StoopidTime.FromInput(next);
                    time = t.Time;
                }
                catch
                {
                    await msg.ModifyAsync(x => x.Embed = erorrembed);
                    return;
                }

            }
            await msg.ModifyAsync(x => x.Embed = eb.WithDescription("Who is the giveaway host? You can mention them or provide an ID, say none/skip to set yourself as the host.").Build());
            next = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            if (next.ToLower() == "none" || next.ToLower() == "skip")
            {
                Host = ctx.User;
            }
            else
            {
                var reader1 = new UserTypeReader<IUser>();
                try
                {
                    var result = await reader1.ReadAsync(ctx, next, _servs);
                    Host = (IUser)result.BestMatch;
                }
                catch
                {
                    await msg.ModifyAsync(x => x.Embed = erorrembed);
                    return;
                }

            }
            await msg.ModifyAsync(x => x.Embed = eb.WithDescription("Would you like to setup role requirements or user blacklists? Say no/none to just start the giveaway.").Build());
            next = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            if (next.ToLower() == "no" || next.ToLower() == "none")
            {
                await Service.GiveawaysInternal(chan, time, prize, winners, Host.Id, ctx.Guild.Id, chan, ctx.Guild);
            }

        }
        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [RequireUserPermission(GuildPermission.ManageChannels)]
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
        [RequireUserPermission(GuildPermission.ManageChannels)]
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
