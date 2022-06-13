namespace Mewdeko.Modules.MultiGreets.Services;

public class MultiGreetService : INService
{
    private readonly DbService _db;
    private readonly Mewdeko _bot;

    public MultiGreetService(DbService db, DiscordSocketClient client, Mewdeko bot)
    {
        var client1 = client;
        _bot = bot;
        _db = db;
        client1.UserJoined += DoMultiGreet;
        _ = Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                var (multiGreets, user) = await _multiGreetQueue.Reader.ReadAsync();
                var multiGreetType = GetMultiGreetType(user.Guild.Id);
                var replacer = new ReplacementBuilder().WithUser(user).WithClient(client1).WithServer(client1, user.Guild).Build();
                switch (multiGreetType)
                {
                    case 2:
                        continue;
                    case 1:
                        var multiGreet = multiGreets.GetRandomElement();
                        var done = false;
                        do
                        {
                            var content = replacer.Replace(multiGreet.Message);
                            if (!string.IsNullOrWhiteSpace(multiGreet.WebhookUrl))
                            {
                                var webhook = new DiscordWebhookClient(multiGreet.WebhookUrl);
                                if (webhook is null)
                                {
                                    await SetMultiGreetDisabledState(multiGreet, true);
                                    multiGreet = multiGreets.GetRandomElement();
                                    continue;
                                }

                                if (SmartEmbed.TryParse(content, user.Guild?.Id, out var embedData, out var plainText, out var components))
                                {
                                    if (embedData is not null && plainText is not "")
                                    {
                                        var msg = await webhook.SendMessageAsync(plainText, embeds: new[] { embedData.Build() }, components: components?.Build());
                                        if (multiGreet.DeleteTime > 0)
                                            (await user.Guild.GetTextChannel(multiGreet.ChannelId).GetMessageAsync(msg)).DeleteAfter(int.Parse(multiGreet.DeleteTime.ToString()));
                                        done = true;
                                    }

                                    else if (embedData is null && plainText is not null)
                                    {
                                        var msg = await webhook.SendMessageAsync(plainText, components: components?.Build());
                                        if (multiGreet.DeleteTime > 0)
                                            (await user.Guild.GetTextChannel(multiGreet.ChannelId).GetMessageAsync(msg)).DeleteAfter(int.Parse(multiGreet.DeleteTime.ToString()));
                                        done = true;
                                    }

                                    else if (embedData is null || plainText is not "") continue;

                                    {
                                        var msg = await webhook.SendMessageAsync(embeds: new[] { embedData.Build() }, components: components?.Build());
                                        if (multiGreet.DeleteTime > 0)
                                            (await user.Guild.GetTextChannel(multiGreet.ChannelId).GetMessageAsync(msg)).DeleteAfter(int.Parse(multiGreet.DeleteTime.ToString()));
                                        done = true;
                                    }
                                }
                                else
                                {
                                    var msg = await webhook.SendMessageAsync(content, components: components?.Build());
                                    if (multiGreet.DeleteTime > 0)
                                        (await user.Guild.GetTextChannel(multiGreet.ChannelId).GetMessageAsync(msg)).DeleteAfter(int.Parse(multiGreet.DeleteTime.ToString()));
                                    done = true;
                                }
                            }
                            else
                            {
                                if (user.IsBot && !multiGreet.GreetBots)
                                    continue;
                                var channel = user.Guild.GetTextChannel(multiGreet.ChannelId);
                                if (SmartEmbed.TryParse(content, user.Guild?.Id, out var embedData, out var plainText, out var components))
                                {
                                    if (embedData is not null && plainText is not "")
                                    {
                                        var msg = await channel.SendMessageAsync(plainText, embed: embedData.Build(), components: components?.Build(),
                                            options: new RequestOptions { RetryMode = RetryMode.RetryRatelimit });
                                        if (multiGreet.DeleteTime > 0)
                                            msg.DeleteAfter(multiGreet.DeleteTime);
                                        done = true;
                                    }

                                    else if (embedData is null && plainText is not null)
                                    {
                                        var msg = await channel.SendMessageAsync(plainText, components: components?.Build(),
                                            options: new RequestOptions { RetryMode = RetryMode.RetryRatelimit });
                                        if (multiGreet.DeleteTime > 0)
                                            msg.DeleteAfter(multiGreet.DeleteTime);
                                        done = true;
                                    }

                                    else if (embedData is not null && plainText is "")
                                    {
                                        var msg = await channel.SendMessageAsync(embed: embedData.Build(), components: components?.Build(),
                                            options: new RequestOptions { RetryMode = RetryMode.RetryRatelimit });
                                        if (multiGreet.DeleteTime > 0)
                                            msg.DeleteAfter(multiGreet.DeleteTime);
                                        done = true;
                                    }
                                }
                                else
                                {
                                    var msg = await channel.SendMessageAsync(content, components: components?.Build(),
                                        options: new RequestOptions { RetryMode = RetryMode.RetryRatelimit });
                                    if (multiGreet.DeleteTime > 0)
                                        msg.DeleteAfter(multiGreet.DeleteTime);
                                    done = true;
                                }
                            }
                        } while (done);

                        break;
                    case 0:
                        {
                            foreach (var i in multiGreets)
                            {
                                if (user.IsBot && !i.GreetBots)
                                    continue;
                                var channel = user.Guild.GetTextChannel(i.ChannelId);
                                if (channel is null)
                                {
                                    await SetMultiGreetDisabledState(i, true);
                                    continue;
                                }

                                var content = replacer.Replace(i.Message);
                                if (SmartEmbed.TryParse(content, user.Guild.Id, out var embedData, out var plainText, out var components))
                                {
                                    if (embedData is not null && plainText is not "")
                                    {
                                        var msg = await channel.SendMessageAsync(plainText, embed: embedData.Build(), components: components?.Build());
                                        if (i.DeleteTime > 0)
                                            msg.DeleteAfter(i.DeleteTime);
                                    }

                                    if (embedData is null && plainText is not null)
                                    {
                                        var msg = await channel.SendMessageAsync(plainText, components: components?.Build());
                                        if (i.DeleteTime > 0)
                                            msg.DeleteAfter(i.DeleteTime);
                                    }

                                    if (embedData is not null && plainText is "")
                                    {
                                        var msg = await channel.SendMessageAsync(embed: embedData.Build(), components: components?.Build());
                                        if (i.DeleteTime > 0)
                                            msg.DeleteAfter(i.DeleteTime);
                                    }
                                }
                                else
                                {
                                    var msg = await channel.SendMessageAsync(content, components: components?.Build());
                                    if (i.DeleteTime > 0)
                                        msg.DeleteAfter(i.DeleteTime);
                                }
                            }

                            break;
                        }
                }
            }
        }, TaskCreationOptions.LongRunning);
    }

    private readonly Channel<(MultiGreet[], SocketGuildUser)> _multiGreetQueue = Channel.CreateBounded<(MultiGreet[], SocketGuildUser)>(
        new BoundedChannelOptions(int.MaxValue) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false });

    public MultiGreet?[] GetGreets(ulong guildId) => _db.GetDbContext().MultiGreets.GetAllGreets(guildId);
    private MultiGreet?[] GetForChannel(ulong channelId) => _db.GetDbContext().MultiGreets.GetForChannel(channelId);

    private Task DoMultiGreet(SocketGuildUser user)
    {
        _ = Task.Factory.StartNew(async () =>
        {
            var greets = GetGreets(user.Guild.Id);
            if (greets.Length == 0) return;
            await _multiGreetQueue.Writer.WriteAsync((greets, user));
        }, TaskCreationOptions.LongRunning);
        return Task.CompletedTask;
    }

    public async Task SetMultiGreetType(IGuild guild, int type)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.MultiGreetType = type;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public int GetMultiGreetType(ulong? id) => _bot.GetGuildConfig(id.Value).MultiGreetType;

    public bool AddMultiGreet(ulong guildId, ulong channelId)
    {
        if (GetForChannel(channelId).Length == 5)
            return false;
        if (GetGreets(guildId).Length == 30)
            return false;
        var toadd = new MultiGreet { ChannelId = channelId, GuildId = guildId };
        using var uow = _db.GetDbContext();
        uow.MultiGreets.Add(toadd);
        uow.SaveChangesAsync();
        return true;
    }

    public async Task ChangeMgMessage(MultiGreet greet, string code)
    {
        await using var uow = _db.GetDbContext();
        greet.Message = code;
        uow.MultiGreets.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task ChangeMgDelete(MultiGreet greet, int howlong)
    {
        await using var uow = _db.GetDbContext();
        greet.DeleteTime = howlong;
        uow.MultiGreets.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task ChangeMgGb(MultiGreet greet, bool enabled)
    {
        await using var uow = _db.GetDbContext();
        greet.GreetBots = enabled;
        uow.MultiGreets.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task ChangeMgWebhook(MultiGreet greet, string webhookurl)
    {
        await using var uow = _db.GetDbContext();
        greet.WebhookUrl = webhookurl;
        uow.MultiGreets.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task SetMultiGreetDisabledState(MultiGreet greet, bool disabled)
    {
        await using var uow = _db.GetDbContext();
        greet.Disabled = disabled;
        uow.MultiGreets.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task RemoveMultiGreetInternal(MultiGreet greet)
    {
        await using var uow = _db.GetDbContext();
        uow.MultiGreets.Remove(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task MultiRemoveMultiGreetInternal(MultiGreet[] greet)
    {
        await using var uow = _db.GetDbContext();
        uow.MultiGreets.RemoveRange(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }
}