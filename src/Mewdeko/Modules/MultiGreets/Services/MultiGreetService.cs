using Discord.Net;
using Mewdeko.Database.DbContextStuff;
using Serilog;

namespace Mewdeko.Modules.MultiGreets.Services;

/// <summary>
///     Service for handling multi greets.
/// </summary>
public class MultiGreetService : INService
{
    private readonly DiscordShardedClient client;
    private readonly DbContextProvider dbProvider;
    private readonly GuildSettingsService guildSettingsService;


    /// <summary>
    ///     Service for handling multi greets.
    /// </summary>
    /// <param name="db">The database provider</param>
    /// <param name="client">The discord client</param>
    /// <param name="guildSettingsService">The guild settings service</param>
    /// <param name="eventHandler">The event handler that i had to make because dnet has never heard of multithreading events</param>
    public MultiGreetService(DbContextProvider dbProvider, DiscordShardedClient client,
        GuildSettingsService guildSettingsService, EventHandler eventHandler)
    {
        this.client = client;
        this.guildSettingsService = guildSettingsService;
        this.dbProvider = dbProvider;
        eventHandler.UserJoined += DoMultiGreet;
    }

    /// <summary>
    ///     Gets all greets for a guild.
    /// </summary>
    /// <param name="guildId">The guild id</param>
    /// <returns>An array of <see cref="MultiGreet" /></returns>
    public async Task<MultiGreet?[]> GetGreets(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        return dbContext.MultiGreets.GetAllGreets(guildId);
    }

    private async Task<MultiGreet?[]> GetForChannel(ulong channelId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        return dbContext.MultiGreets.GetForChannel(channelId);
    }

    private async Task DoMultiGreet(IGuildUser user)
    {
        var greets = await GetGreets(user.Guild.Id);
        if (greets.Length == 0) return;
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

    /// <summary>
    ///     Handles randomly selected greet.
    /// </summary>
    /// <param name="greet">The greet to handle</param>
    /// <param name="user">The user to greet</param>
    private async Task HandleRandomGreet(MultiGreet greet, IGuildUser user)
    {
        var replacer = new ReplacementBuilder().WithUser(user).WithClient(client)
            .WithServer(client, user.Guild as SocketGuild).Build();
        if (greet.WebhookUrl is not null)
        {
            if (user.IsBot && !greet.GreetBots)
                return;
            var webhook = new DiscordWebhookClient(greet.WebhookUrl);
            var content = replacer.Replace(greet.Message);
            try
            {
                if (SmartEmbed.TryParse(content, user.Guild.Id, out var embedData, out var plainText,
                        out var components2))
                {
                    var msg = await webhook
                        .SendMessageAsync(plainText, embeds: embedData, components: components2.Build())
                        .ConfigureAwait(false);
                    if (greet.DeleteTime > 0)
                        (await (await user.Guild.GetTextChannelAsync(greet.ChannelId)).GetMessageAsync(msg)
                            .ConfigureAwait(false)).DeleteAfter(
                            int.Parse(greet.DeleteTime.ToString()));
                }
                else
                {
                    var msg = await webhook.SendMessageAsync(content).ConfigureAwait(false);
                    if (greet.DeleteTime > 0)
                        (await (await user.Guild.GetTextChannelAsync(greet.ChannelId)).GetMessageAsync(msg)
                            .ConfigureAwait(false)).DeleteAfter(
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
                if (SmartEmbed.TryParse(content, user.Guild.Id, out var embedData, out var plainText,
                        out var components2))
                {
                    if (embedData is not null && plainText is not "")
                    {
                        var msg = await channel.SendMessageAsync(plainText, embeds: embedData,
                            components: components2?.Build(), options: new RequestOptions
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

    /// <summary>
    ///     Handles channel greets.
    /// </summary>
    /// <param name="multiGreets">The greets to handle</param>
    /// <param name="user">The user to greet</param>
    private async Task HandleChannelGreets(IEnumerable<MultiGreet> multiGreets, IGuildUser user)
    {
        var replacer = new ReplacementBuilder().WithUser(user).WithClient(client)
            .WithServer(client, user.Guild as SocketGuild).Build();
        foreach (var i in multiGreets.Where(x => x.WebhookUrl == null))
        {
            if (i.Disabled)
                continue;
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
            if (SmartEmbed.TryParse(content, user.Guild.Id, out var embedData, out var plainText, out var components2))
            {
                var msg = await channel.SendMessageAsync(plainText, embeds: embedData, components: components2?.Build())
                    .ConfigureAwait(false);
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

    /// <summary>
    ///     Handles webhook greets.
    /// </summary>
    /// <param name="multiGreets">The greets to handle</param>
    /// <param name="user">The user to greet</param>
    private async Task HandleWebhookGreets(IEnumerable<MultiGreet> multiGreets, IGuildUser user)
    {
        var replacer = new ReplacementBuilder().WithUser(user).WithClient(client)
            .WithServer(client, user.Guild as SocketGuild).Build();
        foreach (var i in multiGreets)
        {
            if (i.Disabled)
                continue;
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

            if (SmartEmbed.TryParse(content, user.Guild.Id, out var embedData, out var plainText, out var components2))
            {
                var msg = await webhook.SendMessageAsync(plainText, embeds: embedData, components: components2?.Build())
                    .ConfigureAwait(false);
                if (i.DeleteTime > 0)
                    (await (await user.Guild.GetTextChannelAsync(i.ChannelId)).GetMessageAsync(msg)
                        .ConfigureAwait(false)).DeleteAfter(int.Parse(i.DeleteTime.ToString()));
            }
            else
            {
                var msg = await webhook.SendMessageAsync(content).ConfigureAwait(false);
                if (i.DeleteTime > 0)
                    (await (await user.Guild.GetTextChannelAsync(i.ChannelId)).GetMessageAsync(msg)
                        .ConfigureAwait(false)).DeleteAfter(int.Parse(i.DeleteTime.ToString()));
            }
        }
    }

    /// <summary>
    ///     Sets the multi greet type for a guild.
    /// </summary>
    /// <param name="guild">The guild</param>
    /// <param name="type">The type to set</param>
    public async Task SetMultiGreetType(IGuild guild, int type)
    {
        await using var db = await dbProvider.GetContextAsync();
        var gc = await db.ForGuildId(guild.Id, set => set);
        gc.MultiGreetType = type;
        await guildSettingsService.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    ///     Gets the multi greet type for a guild.
    /// </summary>
    /// <param name="id">The guild id</param>
    /// <returns></returns>
    public async Task<int> GetMultiGreetType(ulong id)
    {
        return (await guildSettingsService.GetGuildConfig(id)).MultiGreetType;
    }

    /// <summary>
    ///     Adds a multi greet to a guild.
    /// </summary>
    /// <param name="guildId">The guild id</param>
    /// <param name="channelId">The channel id</param>
    /// <returns>Whether the greet was added</returns>
    public async Task<bool> AddMultiGreet(ulong guildId, ulong channelId)
    {
        if ((await GetForChannel(channelId)).Length == 5)
            return false;
        if ((await GetGreets(guildId)).Length == 30)
            return false;
        var toadd = new MultiGreet
        {
            ChannelId = channelId, GuildId = guildId
        };
        await using var dbContext = await dbProvider.GetContextAsync();

        dbContext.MultiGreets.Add(toadd);
        await dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Sets the message for a multi greet.
    /// </summary>
    /// <param name="greet">The greet to change</param>
    /// <param name="code">The new message</param>
    public async Task ChangeMgMessage(MultiGreet greet, string code)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        greet.Message = code;
        dbContext.MultiGreets.Update(greet);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the delete time for a multi greet message.
    /// </summary>
    /// <param name="greet">The greet to change</param>
    /// <param name="howlong">The new delete time</param>
    public async Task ChangeMgDelete(MultiGreet greet, int howlong)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        greet.DeleteTime = howlong;
        dbContext.MultiGreets.Update(greet);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Changes whether a multi greet greets bots.
    /// </summary>
    /// <param name="greet">The greet to change</param>
    /// <param name="enabled">Whether to greet bots</param>
    public async Task ChangeMgGb(MultiGreet greet, bool enabled)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        greet.GreetBots = enabled;
        dbContext.MultiGreets.Update(greet);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Changes the webhook url for a multi greet.
    /// </summary>
    /// <param name="greet">The greet to change</param>
    /// <param name="webhookurl">The new webhook url</param>
    public async Task ChangeMgWebhook(MultiGreet greet, string webhookurl)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        greet.WebhookUrl = webhookurl;
        dbContext.MultiGreets.Update(greet);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a multi greet.
    /// </summary>
    /// <param name="greet">The greet to remove</param>
    public async Task RemoveMultiGreetInternal(MultiGreet greet)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        dbContext.MultiGreets.Remove(greet);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes multiple multi greets.
    /// </summary>
    /// <param name="greet">The greets to remove</param>
    public async Task MultiRemoveMultiGreetInternal(MultiGreet[] greet)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        await using var _ = dbContext.ConfigureAwait(false);
        dbContext.MultiGreets.RemoveRange(greet);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Disables or enables a multi greet.
    /// </summary>
    /// <param name="greet">The greet to disable</param>
    /// <param name="disabled">Whether to disable the greet</param>
    public async Task MultiGreetDisable(MultiGreet greet, bool disabled)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        greet.Disabled = disabled;
        dbContext.MultiGreets.Update(greet);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }
}