using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Humanizer;
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
                        var executedReminders = group.ToList();
                        await Task.WhenAll(executedReminders.Select(GiveawayTimerAction));
                        await RemoveReminders(executedReminders);
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

        private async Task RemoveReminders(List<Mewdeko.Services.Database.Models.Giveaways> reminders)
        {
            using (var uow = _db.GetDbContext())
            {
                uow._context.Set<global::Mewdeko.Services.Database.Models.Giveaways>()
                    .RemoveRange(reminders);

                await uow.SaveChangesAsync();
            }
        }

        private Task<List<global::Mewdeko.Services.Database.Models.Giveaways>> GetGiveawaysBeforeAsync(DateTime now)
        {
            using (var uow = _db.GetDbContext())
            {
                return uow._context.Giveaways
                    .FromSqlInterpolated(
                        $"select * from giveaways where ((serverid >> 22) % {_creds.TotalShards}) == {_client.ShardId} and \"when\" < {now};")
                    .ToListAsync();
            }
        }
        public async Task GiveawaysInternal(ITextChannel chan, TimeSpan ts, string item, ulong host, ulong ServerId, ITextChannel CurrentChannel, IGuild guild)
        {
            var eb = new EmbedBuilder()
            {
                Color = Mewdeko.Services.Mewdeko.OkColor,
                Title = "Mewdeko Giveaway!",
                Description =
                    $"Prize: {item}\nWinners: 1\nEnd Time: {ts.Humanize()}\nHost: {await guild.GetUserAsync(host)}\n\n\nReact to <:Nekoha_nom:866616296291172353> to enter!",
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
                When = time,
                Item = item,
                MessageId = msg.Id
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
            if (await _client.GetGuild(r.ServerId)?.GetTextChannel(r.ChannelId).GetMessageAsync(r.MessageId) is not IUserMessage ch)
                    return;
                var emote = Emote.Parse("<:Nekoha_nom:866616296291172353>");
                var reacts = await ch.GetReactionUsersAsync(emote, 999999).FlattenAsync();
                if (reacts.Count()-1 <= 1)
                {
                    var eb = new EmbedBuilder()
                    {
                        Color = Mewdeko.Services.Mewdeko.ErrorColor,
                        Description = "Nobody won because nobody else reacted!"
                    };
                    await ch.ModifyAsync(x => x.Embed = eb.Build());
                }
                else
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
        }
    }
}