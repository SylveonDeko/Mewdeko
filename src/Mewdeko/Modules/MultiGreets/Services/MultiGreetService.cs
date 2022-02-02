using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Replacements;
using Mewdeko.Services.Database.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Mewdeko.Modules.MultiGreets.Services;

public class MultiGreetService : INService
{
    private readonly DbService _db;
    private DiscordSocketClient client;
    private ConcurrentDictionary<ulong, int> multiGreetTypes;


    public MultiGreetService(DbService db, DiscordSocketClient client, Mewdeko.Services.Mewdeko bot)
    {
        this.client = client;
        _db = db;
        this.client.UserJoined += DoMultiGreet;
        multiGreetTypes = bot.AllGuildConfigs
                   .ToDictionary(x => x.GuildId, x => x.MultiGreetType)
                   .ToConcurrent();
    }
    
    public MultiGreet[] GetGreets(ulong guildId) => _db.GetDbContext().MultiGreets.GetAllGreets(guildId);
    private MultiGreet[] GetForChannel(ulong channelId) => _db.GetDbContext().MultiGreets.GetForChannel(channelId);

    private Task DoMultiGreet(SocketGuildUser user)
    {
        _ = Task.Run(async () =>
        {
            var greets = GetGreets(user.Guild.Id);
            if (!greets.Any()) return;
            if (GetMultiGreetType(user.Guild.Id) == 3)
                return;
            if (GetMultiGreetType(user.Guild.Id) == 1)
            {
                var random = new Random();
                var index = random.Next(greets.Length);
                await HandleRandomGreet(greets[index], user);
                return;
            }
            var webhooks = greets.Where(x => x.WebhookUrl is not null)
                                 .Select(x => new DiscordWebhookClient(x.WebhookUrl));
            if (greets.Any())
                await HandleChannelGreets(greets, user);
            if (webhooks.Any())
                await HandleWebhookGreets(greets, user);
        });
        return Task.CompletedTask;

    }
    
    public async Task HandleRandomGreet(MultiGreet greet, SocketGuildUser user)
    {
        var replacer = new ReplacementBuilder().WithUser(user).WithClient(client).WithServer(client, user.Guild).Build();
        if (greet.WebhookUrl is not null)
        {
            var webhook = new DiscordWebhookClient(greet.WebhookUrl);
            var content = replacer.Replace(greet.Message);
            if (CrEmbed.TryParse(content, out var embedData))
            {
                if (embedData.IsEmbedValid && embedData.PlainText is not null)
                {
                    var msg = await webhook.SendMessageAsync(embedData.PlainText, embeds: new[] { embedData.ToEmbed().Build() });
                    if (greet.DeleteTime > 0)
                        user.Guild.GetTextChannel(greet.ChannelId).GetMessageAsync(msg).Result.DeleteAfter(int.Parse(greet.DeleteTime.ToString()));
                }

                if (!embedData.IsEmbedValid && embedData.PlainText is not null)
                {
                    var msg = await webhook.SendMessageAsync(embedData.PlainText);
                    if (greet.DeleteTime > 0)
                        user.Guild.GetTextChannel(greet.ChannelId).GetMessageAsync(msg).Result.DeleteAfter(int.Parse(greet.DeleteTime.ToString()));
                }

                if (embedData.IsEmbedValid && embedData.PlainText is null)
                {
                    var msg = await webhook.SendMessageAsync(embeds: new[] { embedData.ToEmbed().Build() });
                    if (greet.DeleteTime > 0)
                        user.Guild.GetTextChannel(greet.ChannelId).GetMessageAsync(msg).Result.DeleteAfter(int.Parse(greet.DeleteTime.ToString()));
                }
            }
            else
            {
                var msg = await webhook.SendMessageAsync(content);
                if (greet.DeleteTime > 0)
                    user.Guild.GetTextChannel(greet.ChannelId).GetMessageAsync(msg).Result.DeleteAfter(int.Parse(greet.DeleteTime.ToString()));
            }
        }
        else
        {
            var channel = user.Guild.GetTextChannel(greet.ChannelId);
            var content = replacer.Replace(greet.Message);
            if (CrEmbed.TryParse(content, out var embedData))
            {
                if (embedData.IsEmbedValid && embedData.PlainText is not null)
                {
                    var msg = await channel.SendMessageAsync(embedData.PlainText, embed: embedData.ToEmbed().Build(), options: new RequestOptions()
                    {
                        RetryMode = RetryMode.RetryRatelimit
                    });
                    if (greet.DeleteTime > 0)
                        msg.DeleteAfter(int.Parse(greet.DeleteTime.ToString()));

                }

                if (!embedData.IsEmbedValid && embedData.PlainText is not null)
                {
                    var msg = await channel.SendMessageAsync(embedData.PlainText, options: new RequestOptions()
                    {
                        RetryMode = RetryMode.RetryRatelimit
                    });;
                    if (greet.DeleteTime > 0)
                        msg.DeleteAfter(int.Parse(greet.DeleteTime.ToString()));
                }

                if (embedData.IsEmbedValid && embedData.PlainText is null)
                {
                    var msg = await channel.SendMessageAsync(embed: embedData.ToEmbed().Build(), options: new RequestOptions()
                    {
                        RetryMode = RetryMode.RetryRatelimit
                    });;
                    if (greet.DeleteTime > 0)
                        msg.DeleteAfter(int.Parse(greet.DeleteTime.ToString()));
                }
            }
            else
            {
                var msg = await channel.SendMessageAsync(content, options: new RequestOptions()
                {
                    RetryMode = RetryMode.RetryRatelimit
                });;
                if (greet.DeleteTime > 0)
                    msg.DeleteAfter(int.Parse(greet.DeleteTime.ToString()));
            }
        }
    }
    private async Task HandleChannelGreets(IEnumerable<MultiGreet> multiGreets, SocketGuildUser user)
    {
        var replacer = new ReplacementBuilder().WithUser(user).WithClient(client).WithServer(client, user.Guild).Build();
        foreach (var i in multiGreets.Where(x => x.WebhookUrl == null))
        {
            if (i.WebhookUrl is not null) continue;
            var channel = user.Guild.GetTextChannel(i.ChannelId);
            var content = replacer.Replace(i.Message);
            if (CrEmbed.TryParse(content, out var embedData))
            {
                if (embedData.IsEmbedValid && embedData.PlainText is not null)
                {
                    var msg = await channel.SendMessageAsync(embedData.PlainText, embed: embedData.ToEmbed().Build());
                    if (i.DeleteTime > 0)
                        msg.DeleteAfter(int.Parse(i.DeleteTime.ToString()));

                }

                if (!embedData.IsEmbedValid && embedData.PlainText is not null)
                {
                    var msg = await channel.SendMessageAsync(embedData.PlainText);
                    if (i.DeleteTime > 0)
                        msg.DeleteAfter(int.Parse(i.DeleteTime.ToString()));
                }

                if (embedData.IsEmbedValid && embedData.PlainText is null)
                {
                    var msg = await channel.SendMessageAsync(embed: embedData.ToEmbed().Build());
                    if (i.DeleteTime > 0)
                        msg.DeleteAfter(int.Parse(i.DeleteTime.ToString()));
                }
            }
            else
            {
                var msg = await channel.SendMessageAsync(content);
                if (i.DeleteTime > 0)
                    msg.DeleteAfter(int.Parse(i.DeleteTime.ToString()));
            }
        }
    }
    private async Task HandleWebhookGreets(IEnumerable<MultiGreet> multiGreets, SocketGuildUser user)
    {
        var replacer = new ReplacementBuilder().WithUser(user).WithClient(client).WithServer(client, user.Guild).Build();
        foreach (var i in multiGreets)
        {
            
            if (i.WebhookUrl is null) continue;
            var webhook = new DiscordWebhookClient(i.WebhookUrl);
            var content = replacer.Replace(i.Message);
            if (CrEmbed.TryParse(content, out var embedData))
            {
                if (embedData.IsEmbedValid && embedData.PlainText is not null)
                {
                    var msg = await webhook.SendMessageAsync(embedData.PlainText, embeds: new[] { embedData.ToEmbed().Build() });
                    if (i.DeleteTime > 0)
                        user.Guild.GetTextChannel(i.ChannelId).GetMessageAsync(msg).Result.DeleteAfter(int.Parse(i.DeleteTime.ToString()));
                }

                if (!embedData.IsEmbedValid && embedData.PlainText is not null)
                {
                    var msg = await webhook.SendMessageAsync(embedData.PlainText);
                    if (i.DeleteTime > 0)
                        user.Guild.GetTextChannel(i.ChannelId).GetMessageAsync(msg).Result.DeleteAfter(int.Parse(i.DeleteTime.ToString()));
                }

                if (embedData.IsEmbedValid && embedData.PlainText is null)
                {
                    var msg = await webhook.SendMessageAsync(embeds: new[] { embedData.ToEmbed().Build() });
                    if (i.DeleteTime > 0)
                        user.Guild.GetTextChannel(i.ChannelId).GetMessageAsync(msg).Result.DeleteAfter(int.Parse(i.DeleteTime.ToString()));
                }
            }
            else
            {
                var msg = await webhook.SendMessageAsync(content);
                if (i.DeleteTime > 0)
                    user.Guild.GetTextChannel(i.ChannelId).GetMessageAsync(msg).Result.DeleteAfter(int.Parse(i.DeleteTime.ToString()));
            }
        }
    }

    public async Task SetMultiGreetType(IGuild guild, int type)
    {
        using (var uow = _db.GetDbContext())
        {
            var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
            gc.MultiGreetType = type;
            await uow.SaveChangesAsync();
        }

        multiGreetTypes.AddOrUpdate(guild.Id, type, (_, _) => type);
    }

    public int GetMultiGreetType(ulong? id)
    {
        multiGreetTypes.TryGetValue(id.Value, out var mgType);
        return mgType;
    }
    public bool AddMultiGreet(ulong guildId, ulong channelId)
    {
        if (GetForChannel(channelId).Length == 5)
            return false;
        if (GetGreets(guildId).Length == 30)
            return false;
        var toadd = new MultiGreet { ChannelId = channelId, GuildId = guildId };
        var uow = _db.GetDbContext();
        uow.MultiGreets.Add(toadd);
        uow.SaveChangesAsync();
        return true;
    }

    public async Task ChangeMgMessage(MultiGreet greet, string code)
    {
        var uow = _db.GetDbContext();
        var toadd = new MultiGreet
        {
            Id = greet.Id,
            GuildId = greet.GuildId,
            ChannelId = greet.ChannelId,
            DeleteTime = greet.DeleteTime,
            Message = code,
            WebhookUrl = greet.WebhookUrl
        };
        uow.MultiGreets.Update(toadd);
        await uow.SaveChangesAsync();
    }

    public async Task ChangeMgDelete(MultiGreet greet, ulong howlong)
    {
        var uow = _db.GetDbContext();
        var toadd = new MultiGreet
        {
            Id = greet.Id,
            GuildId = greet.GuildId,
            ChannelId = greet.ChannelId,
            DeleteTime = howlong,
            Message = greet.Message,
            WebhookUrl = greet.WebhookUrl
        };
        uow.MultiGreets.Update(toadd);
        await uow.SaveChangesAsync();
    }
    public async Task ChangeMgWebhook(MultiGreet greet, string webhookurl)
    {
        var uow = _db.GetDbContext();
        var toadd = new MultiGreet
        {
            Id = greet.Id,
            GuildId = greet.GuildId,
            ChannelId = greet.ChannelId,
            DeleteTime = greet.DeleteTime,
            Message = greet.Message,
            WebhookUrl = webhookurl
        };
        uow.MultiGreets.Update(toadd);
        await uow.SaveChangesAsync();
    }

    public async Task RemoveMultiGreetInternal(MultiGreet greet)
    {
        var uow =  _db.GetDbContext();
        uow.MultiGreets.Remove(greet);
        await uow.SaveChangesAsync();
    }
    public async Task MultiRemoveMultiGreetInternal(MultiGreet[] greet)
    {
        var uow =  _db.GetDbContext();
        uow.MultiGreets.RemoveRange(greet);
        await uow.SaveChangesAsync();
    }
    
}