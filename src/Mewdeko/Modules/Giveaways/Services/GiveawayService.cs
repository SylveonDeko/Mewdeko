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

namespace Mewdeko.Modules.Giveaways.Services;

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
            var toupdate = new Mewdeko.Services.Database.Models.Giveaways
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
                Winners = i.Winners
            };
            uow.Giveaways.Remove(i);
            uow.Giveaways.Add(toupdate);
            await uow.SaveChangesAsync();
        }
    }

    private Task<List<Mewdeko.Services.Database.Models.Giveaways>> GetGiveawaysBeforeAsync(DateTime now)
    {
        using var uow = _db.GetDbContext();
        return uow._context.Giveaways
            .FromSqlInterpolated(
                $"select * from giveaways where ((serverid >> 22) % {_creds.TotalShards}) == {_client.ShardId} and \"when\" < {now} and \"Ended\" == 0;")
            .ToListAsync();
    }

    public async Task GiveawaysInternal(ITextChannel chan, TimeSpan ts, string item, int winners, ulong host,
        ulong ServerId, ITextChannel CurrentChannel, IGuild guild, string reqroles = null, string blacklistusers = null,
        string blacklistroles = null)
    {
        var eb = new EmbedBuilder
        {
            Color = Mewdeko.Services.Mewdeko.OkColor,
            Description =
                "<:HaneBomb:914307912044802059> Mewdeko Giveaway!\n" +
                "<:testingpurposes:915634289847201812><:testingpurposes:915634289847201812><:testingpurposes:915634289847201812><:testingpurposes:915634289847201812><:testingpurposes:915634289847201812><:testingpurposes:915634289847201812><:testingpurposes:915634289847201812><:testingpurposes:915634289847201812><:testingpurposes:915634289847201812>\n" +
                $"Host: {guild.GetUserAsync(host).Result}\n" +
                $"🎁 Prize: {item} 🎁\n" +
                $"🏅 Winners: {winners} 🏅\n" +
                $"🗯️Required Roles: {reqroles ?? "None"}\n" +
                "<:testingpurposes:915634289847201812><:testingpurposes:915634289847201812><:testingpurposes:915634289847201812><:testingpurposes:915634289847201812><:testingpurposes:915634289847201812><:testingpurposes:915634289847201812><:testingpurposes:915634289847201812><:testingpurposes:915634289847201812><:testingpurposes:915634289847201812>\n" +
                $"End Time: {ts.Humanize(maxUnit: TimeUnit.Year)}",
            ImageUrl =
                "https://media.discordapp.net/attachments/915770282579484693/915770338825097226/AmbitiousFaroffDiscus-size_restricted.gif"
        };
        var msg = await chan.SendMessageAsync(embed: eb.Build());

        var emote = Emote.Parse("<a:HaneMeow:914307922287276052>");
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
            try
            {
                uow.Giveaways.Add(rem);
                var e = await uow.SaveChangesAsync();
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
        if (await _client.GetGuild(r.ServerId)?.GetTextChannel(r.ChannelId).GetMessageAsync(r.MessageId)! is not
            IUserMessage ch)
            return;
        var emote = Emote.Parse("<a:HaneMeow:914307922287276052>");
        var reacts = await ch.GetReactionUsersAsync(emote, 999999).FlattenAsync();
        if (reacts.Count() - 1 <= r.Winners)
        {
            var eb = new EmbedBuilder
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
                var eb = new EmbedBuilder
                {
                    Color = Mewdeko.Services.Mewdeko.OkColor,
                    Description = $"{user.Mention} won the giveaway for {r.Item}!"
                };
                await ch.ModifyAsync(x => x.Embed = eb.Build());
                await ch.Channel.SendMessageAsync($"{user.Mention} won the giveaway for {r.Item}!",
                    embed: new EmbedBuilder().WithOkColor().WithDescription($"[Jump To Giveaway]({ch.GetJumpUrl()})")
                        .Build());
            }
            else
            {
                var rand = new Random();
                var users = reacts.Where(x => !x.IsBot);
                var winners = users.ToList().OrderBy(x => rand.Next()).Take(r.Winners);
                var eb = new EmbedBuilder
                {
                    Color = Mewdeko.Services.Mewdeko.OkColor,
                    Description = $"{string.Join("", winners.Select(x => x.Mention))} won the giveaway for {r.Item}!"
                };
                await ch.ModifyAsync(x => x.Embed = eb.Build());
                await ch.Channel.SendMessageAsync(
                    $"{string.Join("", winners.Select(x => x.Mention))} won the giveaway for {r.Item}!",
                    embed: new EmbedBuilder().WithOkColor().WithDescription($"[Jump To Giveaway]({ch.GetJumpUrl()})")
                        .Build());
            }
        }
    }
}