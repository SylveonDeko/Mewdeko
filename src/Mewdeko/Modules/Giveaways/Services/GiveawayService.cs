using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Humanizer;
using Humanizer.Localisation;
using Mewdeko._Extensions;
using Mewdeko.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Giveaways.Services
{
    public class GiveawayService : INService
    {
        private readonly DiscordSocketClient _client;
        private readonly IBotCredentials _creds;
        private readonly DbService _db;

        public GiveawayService(DiscordSocketClient client, DbService db, IBotCredentials creds)
        {
            _client = client;
            _db = db;
            _creds = creds;
            _ = StartGiveawayLoop();
        }

        private async Task StartGiveawayLoop()
        {
            while (true)
            {
                await Task.Delay(2000);
                try
                {
                    var now = DateTime.UtcNow;
                    var reminders = await GetGiveawaysBeforeAsync(now);
                    if (reminders.Count == 0)
                        continue;

                    Log.Information($"Executing {reminders.Count} giveaways.");

                    // make groups of 5, with 1.5 second inbetween each one to ensure against ratelimits
                    var i = 0;
                    foreach (var group in reminders
                        .GroupBy(_ => ++i / (reminders.Count / 5 + 1)))
                    {
                        var executedGiveaways = group.ToList();
                        await Task.WhenAll(executedGiveaways.Select(GiveawayTimerAction));
                        await UpdateGiveaways(executedGiveaways);
                        await Task.Delay(1500);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"Error in Giveaway loop: {ex.Message}");
                    Log.Warning(ex.ToString());
                }
            }
        }

        private async Task UpdateGiveaways(List<Mewdeko.Services.Database.Models.Giveaways> g)
        {
            using var uow = _db.GetDbContext();
            foreach (var i in g)
            {
                var toupdate = new Mewdeko.Services.Database.Models.Giveaways()
                {
                    When = i.When,
                    BlacklistRoles = i.BlacklistRoles,
                    BlacklistUsers = i.BlacklistUsers,
                    ChannelId = i.ChannelId,
                    Ended = 1,
                    MessageId = i.MessageId,
                    RestrictTo = i.RestrictTo,
                    Item = i.Item,
                    ServerId = i.ServerId,
                    UserId = i.UserId,
                    Winners = i.Winners,
                };
                uow.Giveaways.Remove(i);
                uow.Giveaways.Add(toupdate);
                await uow.SaveChangesAsync();
            }
        }

        private Task<List<global::Mewdeko.Services.Database.Models.Giveaways>> GetGiveawaysBeforeAsync(DateTime now)
        {
            using (var uow = _db.GetDbContext())
            {
                return uow._context.Giveaways
                    .FromSqlInterpolated(
                        $"select * from giveaways where ((serverid >> 22) % {_creds.TotalShards}) == {_client.ShardId} and \"when\" < {now} and \"Ended\" == 0;")
                    .ToListAsync();
            }
        }
        public async Task GiveawaysInternal(ITextChannel chan, TimeSpan ts, string item, int winners, ulong host, ulong ServerId, ITextChannel CurrentChannel, IGuild guild, string reqroles = null, string blacklistusers = null, string blacklistroles = null)
        {
            var eb = new EmbedBuilder()
            {
                Color = Mewdeko.Services.Mewdeko.OkColor,
                Description =
                    "<:Nekohawave:866615191100588042> Mewdeko Giveaway!\n" +
                    "<:testingpurposes:912493798955819049><:testingpurposes:912493798955819049><:testingpurposes:912493798955819049><:testingpurposes:912493798955819049><:testingpurposes:912493798955819049><:testingpurposes:912493798955819049><:testingpurposes:912493798955819049><:testingpurposes:912493798955819049><:testingpurposes:912493798955819049>\n" + 
                    $"Host: {guild.GetUserAsync(host).Result}\n" +
                    $"🎁 Prize: {item} 🎁\n" + 
                    $"🏅 Winners: {winners} 🏅\n" + 
                    $"🗯️Required Roles: {reqroles ?? "None"}" + 
                    "<:testingpurposes:912493798955819049><:testingpurposes:912493798955819049><:testingpurposes:912493798955819049><:testingpurposes:912493798955819049><:testingpurposes:912493798955819049><:testingpurposes:912493798955819049><:testingpurposes:912493798955819049><:testingpurposes:912493798955819049><:testingpurposes:912493798955819049>\n" + 
                    $"End Time: {ts.Humanize(maxUnit: TimeUnit.Year)}" ,
                ImageUrl = "https://cdn.discordapp.com/attachments/866315387703394314/866321920822870026/80942286_p0.png?width=1246&height=701"
            };
            var msg = await chan.SendMessageAsync(embed: eb.Build());

            var emote = Emote.Parse("<:Nekoha_nom:866616296291172353>");
            await msg.AddReactionAsync(emote);
            var time = DateTime.UtcNow + ts;
            var rem = new Mewdeko.Services.Database.Models.Giveaways
            {
                ChannelId = chan.Id,
                UserId = host,
                ServerId = ServerId,
                Ended = 0,
                When = time,
                Item = item,
                MessageId = msg.Id,
                Winners = winners
            };

            using (var uow = _db.GetDbContext())
            {
                uow.Giveaways.Add(rem);
                try
                {
                    var e = uow.SaveChanges();
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
                
            }

            await CurrentChannel.SendConfirmAsync($"Giveaway started in {chan.Mention}");
        }
        public async Task GiveawayTimerAction(Mewdeko.Services.Database.Models.Giveaways r)
        {
            if (await _client.GetGuild(r.ServerId)?.GetTextChannel(r.ChannelId).GetMessageAsync(r.MessageId)! is not IUserMessage ch)
                    return;
            var emote = Emote.Parse("<:Nekoha_nom:866616296291172353>");
            var reacts = await ch.GetReactionUsersAsync(emote, 999999).FlattenAsync();
            if (reacts.Count()-1 <= r.Winners)
            {
                var eb = new EmbedBuilder()
                {
                    Color = Mewdeko.Services.Mewdeko.ErrorColor,
                    Description = "There were not enough participants!"
                };
                await ch.ModifyAsync(x => x.Embed = eb.Build());
            }
            else
            {
                if (r.Winners == 1)
                {
                    var users = reacts.Where(x => !x.IsBot);
                    var rand = new Random();
                    var index = rand.Next(users.Count());
                    var user = users.ToList()[index];
                    var eb = new EmbedBuilder()
                    {
                        Color = Mewdeko.Services.Mewdeko.OkColor,
                        Description = $"{user.Mention} won the giveaway for {r.Item}!"
                    };
                    await ch.ModifyAsync(x => x.Embed = eb.Build());
                    await ch.Channel.SendConfirmAsync($"Giveaway ended!\n{ch.GetJumpUrl()}");
                }
                else
                {
                    var rand = new Random();
                    var users = reacts.Where(x => !x.IsBot);
                    var winners = users.ToList().OrderBy(x => rand.Next()).Take(r.Winners);
                    var eb = new EmbedBuilder()
                    {
                        Color = Mewdeko.Services.Mewdeko.OkColor,
                        Description = $"{string.Join("", users.Select(x => x.Mention))} won the giveaway for {r.Item}!"
                    };
                    await ch.ModifyAsync(x => x.Embed = eb.Build());
                    await ch.Channel.SendConfirmAsync($"Giveaway ended!\n{ch.GetJumpUrl()}");
                }
            }
        }
    }
}