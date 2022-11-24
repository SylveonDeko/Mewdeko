using System.Threading.Tasks;
using Discord.Net;
using Serilog;

namespace Mewdeko.Modules.MultiGreets.Services;

public class MultiGreetService : INService
{
    private readonly DbService db;
    private readonly DiscordSocketClient client;
    private readonly GuildSettingsService guildSettingsService;


    public MultiGreetService(DbService db, DiscordSocketClient client,
        GuildSettingsService guildSettingsService, EventHandler eventHandler)
    {
        this.client = client;
        this.guildSettingsService = guildSettingsService;
        this.db = db;
        eventHandler.UserJoined += DoMultiGreet;
    }

    public MultiGreet?[] GetGreets(ulong guildId) => db.GetDbContext().MultiGreets.GetAllGreets(guildId);
    private MultiGreet?[] GetForChannel(ulong channelId) => db.GetDbContext().MultiGreets.GetForChannel(channelId);

    private async Task DoMultiGreet(IGuildUser user)
    {
        var greets = GetGreets(user.Guild.Id);
        if (!greets.Any()) return;
        if (await GetMultiGreetType(user.Guild.Id) == 3)
            return;
        if (await GetMultiGreetType(user.Guild.Id) == 1)
        {
            var random = new Random();
            var index = random.Next(greets.Length);
            await HandleRandomGreet(greets[index], user).ConfigureAwait(false);
            return;
        }

        var webhooks = greets.Where(x => x.WebhookUrl is not null).Select(x => new DiscordWebhookClient(x.WebhookUrl));
        if (greets.Any())
            await HandleChannelGreets(greets, user).ConfigureAwait(false);
        if (webhooks.Any())
            await HandleWebhookGreets(greets, user).ConfigureAwait(false);

    }

    public async Task HandleRandomGreet(MultiGreet greet, IGuildUser user)
    {
        var replacer = new ReplacementBuilder().WithUser(user).WithClient(client).WithServer(client, user.Guild as SocketGuild).Build();
        if (greet.WebhookUrl is not null)
        {
            if (user.IsBot && !greet.GreetBots)
                return;
            var webhook = new DiscordWebhookClient(greet.WebhookUrl);
            var content = replacer.Replace(greet.Message);
            try
            {
                if (SmartEmbed.TryParse(content, user.Guild.Id, out var embedData, out var plainText, out var components2))
                {

                    var msg = await webhook.SendMessageAsync(plainText, embeds: embedData, components: components2.Build()).ConfigureAwait(false);
                    if (greet.DeleteTime > 0)
                        (await (await user.Guild.GetTextChannelAsync(greet.ChannelId)).GetMessageAsync(msg).ConfigureAwait(false)).DeleteAfter(
                            int.Parse(greet.DeleteTime.ToString()));
                }
                else
                {
                    var msg = await webhook.SendMessageAsync(content).ConfigureAwait(false);
                    if (greet.DeleteTime > 0)
                        (await (await user.Guild.GetTextChannelAsync(greet.ChannelId)).GetMessageAsync(msg).ConfigureAwait(false)).DeleteAfter(
                            int.Parse(greet.DeleteTime.ToString()));
                }
            }
            catch (HttpException ex)
            {
                if (ex.DiscordCode is DiscordErrorCode.UnknownWebhook or DiscordErrorCode.InvalidWebhookToken)
                {
                    await MultiGreetDisable(greet, true);
                    Log.Information($"MultiGreet disabled in {user.Guild} due to missing permissions.");
                }
            }
        }
        else
        {
            if (user.IsBot && !greet.GreetBots)
                return;
            var channel = await user.Guild.GetTextChannelAsync(greet.ChannelId);
            var content = replacer.Replace(greet.Message);
            if (channel is null)
            {
                await RemoveMultiGreetInternal(greet);
                return;
            }

            try
            {
                if (SmartEmbed.TryParse(content, user.Guild.Id, out var embedData, out var plainText, out var components2))
                {
                    if (embedData is not null && plainText is not "")
                    {
                        var msg = await channel.SendMessageAsync(plainText, embeds: embedData, components: components2?.Build(), options: new RequestOptions
                        {
                            RetryMode = RetryMode.RetryRatelimit
                        }).ConfigureAwait(false);
                        if (greet.DeleteTime > 0)
                            msg.DeleteAfter(greet.DeleteTime);

                    }
                }
                else
                {
                    var msg = await channel.SendMessageAsync(content, options: new RequestOptions
                    {
                        RetryMode = RetryMode.RetryRatelimit
                    }).ConfigureAwait(false);
                    if (greet.DeleteTime > 0)
                        msg.DeleteAfter(greet.DeleteTime);
                }
            }
            catch (HttpException ex)
            {
                if (ex.DiscordCode == DiscordErrorCode.MissingPermissions)
                {
                    await MultiGreetDisable(greet, true);
                    Log.Information($"MultiGreet disabled in {user.Guild} due to missing permissions.");
                }
            }
        }
    }
    private async Task HandleChannelGreets(IEnumerable<MultiGreet> multiGreets, IGuildUser user)
    {

        var replacer = new ReplacementBuilder().WithUser(user).WithClient(client).WithServer(client, user.Guild as SocketGuild).Build();
        foreach (var i in multiGreets.Where(x => x.WebhookUrl == null))
        {
            if (user.IsBot && !i.GreetBots)
                continue;
            if (i.WebhookUrl is not null) continue;
            var channel = await user.Guild.GetTextChannelAsync(i.ChannelId);
            if (channel is null)
            {
                await RemoveMultiGreetInternal(i).ConfigureAwait(false);
                continue;
            }
            var content = replacer.Replace(i.Message);
            if (SmartEmbed.TryParse(content , user.Guild.Id, out var embedData, out var plainText, out var components2))
            {
                var msg = await channel.SendMessageAsync(plainText, embeds: embedData, components: components2?.Build()).ConfigureAwait(false);
                    if (i.DeleteTime > 0)
                        msg.DeleteAfter(i.DeleteTime);
            }
            else
            {
                var msg = await channel.SendMessageAsync(content).ConfigureAwait(false);
                if (i.DeleteTime > 0)
                    msg.DeleteAfter(i.DeleteTime);
            }
        }
    }
    private async Task HandleWebhookGreets(IEnumerable<MultiGreet> multiGreets, IGuildUser user)
    {
        var replacer = new ReplacementBuilder().WithUser(user).WithClient(client).WithServer(client, user.Guild as SocketGuild).Build();
        foreach (var i in multiGreets)
        {
            if (user.IsBot && !i.GreetBots)
                continue;
            if (i.WebhookUrl is null) continue;
            var webhook = new DiscordWebhookClient(i.WebhookUrl);
            var content = replacer.Replace(i.Message);
            var channel = await user.Guild.GetTextChannelAsync(i.ChannelId);
            if (channel is null)
            {
                await RemoveMultiGreetInternal(i).ConfigureAwait(false);
                continue;
            }
            if (SmartEmbed.TryParse(content , user.Guild.Id, out var embedData, out var plainText, out var components2))
            {
                var msg = await webhook.SendMessageAsync(plainText, embeds: embedData, components: components2?.Build()).ConfigureAwait(false);
                    if (i.DeleteTime > 0)
                        (await ( await user.Guild.GetTextChannelAsync(i.ChannelId)).GetMessageAsync(msg).ConfigureAwait(false)).DeleteAfter(int.Parse(i.DeleteTime.ToString()));
            }
            else
            {
                var msg = await webhook.SendMessageAsync(content).ConfigureAwait(false);
                if (i.DeleteTime > 0)
                    (await (await user.Guild.GetTextChannelAsync(i.ChannelId)).GetMessageAsync(msg).ConfigureAwait(false)).DeleteAfter(int.Parse(i.DeleteTime.ToString()));
            }
        }
    }

    public async Task SetMultiGreetType(IGuild guild, int type)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.MultiGreetType = type;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        guildSettingsService.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task<int> GetMultiGreetType(ulong id) => (await guildSettingsService.GetGuildConfig(id)).MultiGreetType;
    public bool AddMultiGreet(ulong guildId, ulong channelId)
    {
        if (GetForChannel(channelId).Length == 5)
            return false;
        if (GetGreets(guildId).Length == 30)
            return false;
        var toadd = new MultiGreet { ChannelId = channelId, GuildId = guildId };
        using var uow = db.GetDbContext();
        uow.MultiGreets.Add(toadd);
        uow.SaveChangesAsync();
        return true;
    }

    public async Task ChangeMgMessage(MultiGreet greet, string code)
    {
        await using var uow = db.GetDbContext();
        greet.Message = code;
        uow.MultiGreets.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task ChangeMgDelete(MultiGreet greet, int howlong)
    {
        await using var uow = db.GetDbContext();
        greet.DeleteTime = howlong;
        uow.MultiGreets.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task ChangeMgGb(MultiGreet greet, bool enabled)
    {
        await using var uow = db.GetDbContext();
        greet.GreetBots = enabled;
        uow.MultiGreets.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task ChangeMgWebhook(MultiGreet greet, string webhookurl)
    {
        await using var uow = db.GetDbContext();
        greet.WebhookUrl = webhookurl;
        uow.MultiGreets.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task RemoveMultiGreetInternal(MultiGreet greet)
    {
        var uow =  db.GetDbContext();
        await using var _ = uow.ConfigureAwait(false);
        uow.MultiGreets.Remove(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }
    public async Task MultiRemoveMultiGreetInternal(MultiGreet[] greet)
    {
        var uow =  db.GetDbContext();
        await using var _ = uow.ConfigureAwait(false);
        uow.MultiGreets.RemoveRange(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task MultiGreetDisable(MultiGreet greet, bool disabled)
    {
        var uow = db.GetDbContext();
        greet.Disabled = disabled;
        uow.MultiGreets.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

}