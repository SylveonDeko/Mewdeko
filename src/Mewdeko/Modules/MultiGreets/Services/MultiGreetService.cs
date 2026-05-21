using DataModel;
using Discord.Net;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.MultiGreets.Services;

/// <summary>
///     Service for handling multiple greeting messages for users joining a guild.
/// </summary>
public class MultiGreetService : INService
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;
    private readonly GuildSettingsService guildSettingsService;
    private readonly InviteCountService inviteCountService;
    private readonly ILogger<MultiGreetService> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MultiGreetService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="guildSettingsService">The guild settings service.</param>
    /// <param name="eventHandler">The event handler for user join events.</param>
    /// <param name="inviteCountService">The invite count service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public MultiGreetService(IDataConnectionFactory dbFactory, DiscordShardedClient client,
        GuildSettingsService guildSettingsService, EventHandler eventHandler, InviteCountService inviteCountService,
        ILogger<MultiGreetService> logger)
    {
        this.client = client;
        this.guildSettingsService = guildSettingsService;
        this.inviteCountService = inviteCountService;
        this.logger = logger;
        this.dbFactory = dbFactory;
        this.eventHandler = eventHandler;
        eventHandler.Subscribe("UserJoined", "MultiGreetService", DoMultiGreet);
    }

    /// <summary>
    ///     Gets all greet messages for a specific guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>An array of MultiGreet objects for the specified guild.</returns>
    public async Task<MultiGreet[]?> GetGreets(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.MultiGreets
            .Where(x => x.GuildId == guildId)
            .ToArrayAsync();
    }

    private async Task<MultiGreet[]?> GetForChannel(ulong channelId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.MultiGreets
            .Where(x => x.ChannelId == channelId)
            .ToArrayAsync();
    }

    private async Task DoMultiGreet(IGuildUser user)
    {
        var greets = await GetGreets(user.Guild.Id);
        if (greets.Length == 0) return;

        var greetType = await GetMultiGreetType(user.Guild.Id);
        if (greetType == 2) return;

        var replacer = new ReplacementBuilder().WithUser(user).WithClient(client)
            .WithServer(client, user.Guild as SocketGuild);

        if (greetType == 1)
        {
            var random = new Random();
            var index = random.Next(greets.Length);
            await HandleGreet(greets[index], user, replacer);
        }
        else
        {
            foreach (var greet in greets)
            {
                await HandleGreet(greet, user, replacer);
            }
        }
    }

    /// <summary>
    ///     Sets the multi-greet type for a guild.
    /// </summary>
    /// <param name="guild">The guild to set the multi-greet type for.</param>
    /// <param name="type">The type of multi-greet to set.</param>
    public async Task SetMultiGreetType(IGuild guild, int type)
    {
        var gc = await guildSettingsService.GetGuildConfig(guild.Id);
        gc.MultiGreetType = type;
        await guildSettingsService.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    ///     Gets the multi-greet type for a guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The multi-greet type for the specified guild.</returns>
    public async Task<int> GetMultiGreetType(ulong id) =>
        (await guildSettingsService.GetGuildConfig(id)).MultiGreetType;

    /// <summary>
    ///     Adds a new multi-greet for a guild and channel.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="channelId">The ID of the channel.</param>
    /// <returns>True if the multi-greet was added successfully, false otherwise.</returns>
    public async Task<bool> AddMultiGreet(ulong guildId, ulong channelId)
    {
        if ((await GetForChannel(channelId)).Length == 5 || (await GetGreets(guildId)).Length == 30)
            return false;

        await using var db = await dbFactory.CreateConnectionAsync();

        await db.InsertAsync(new MultiGreet
        {
            ChannelId = channelId, GuildId = guildId
        });

        return true;
    }

    /// <summary>
    ///     Changes the message for a specific multi-greet.
    /// </summary>
    /// <param name="greet">The multi-greet to update.</param>
    /// <param name="code">The new message code.</param>
    public async Task ChangeMgMessage(MultiGreet greet, string code) =>
        await UpdateMultiGreet(greet, mg => mg.Message = code);

    /// <summary>
    ///     Changes the delete time for a specific multi-greet.
    /// </summary>
    /// <param name="greet">The multi-greet to update.</param>
    /// <param name="howlong">The new delete time in seconds.</param>
    public async Task ChangeMgDelete(MultiGreet greet, int howlong) =>
        await UpdateMultiGreet(greet, mg => mg.DeleteTime = howlong);

    /// <summary>
    ///     Changes whether a specific multi-greet should greet bots.
    /// </summary>
    /// <param name="greet">The multi-greet to update.</param>
    /// <param name="enabled">True to greet bots, false otherwise.</param>
    public async Task ChangeMgGb(MultiGreet greet, bool enabled) =>
        await UpdateMultiGreet(greet, mg => mg.GreetBots = enabled);

    /// <summary>
    ///     Changes the webhook URL for a specific multi-greet.
    /// </summary>
    /// <param name="greet">The multi-greet to update.</param>
    /// <param name="webhookurl">The new webhook URL.</param>
    public async Task ChangeMgWebhook(MultiGreet greet, string webhookurl) =>
        await UpdateMultiGreet(greet, mg => mg.WebhookUrl = webhookurl);

    /// <summary>
    ///     Removes a specific multi-greet.
    /// </summary>
    /// <param name="greet">The multi-greet to remove.</param>
    public async Task RemoveMultiGreetInternal(MultiGreet greet)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.DeleteAsync(greet);
    }

    /// <summary>
    ///     Removes multiple multi-greets.
    /// </summary>
    /// <param name="greets">An array of multi-greets to remove.</param>
    public async Task MultiRemoveMultiGreetInternal(MultiGreet[] greets)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        foreach (var greet in greets)
        {
            await db.DeleteAsync(greet);
        }
    }

    /// <summary>
    ///     Enables or disables a specific multi-greet.
    /// </summary>
    /// <param name="greet">The multi-greet to update.</param>
    /// <param name="disabled">True to disable the multi-greet, false to enable it.</param>
    public async Task MultiGreetDisable(MultiGreet greet, bool disabled) =>
        await UpdateMultiGreet(greet, mg => mg.Disabled = disabled);

    private async Task UpdateMultiGreet(MultiGreet greet, Action<MultiGreet> updateAction)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        updateAction(greet);
        await db.UpdateAsync(greet);
    }

    private static async Task SendSmartEmbedMessage(IMessageChannel channel, string content, ulong guildId,
        int deleteTime = 0)
    {
        var built = SmartEmbed.TryParse(content, guildId, out var embedData, out var plainText, out var components)
            ? components?.Build()
            : null;
        if (!string.IsNullOrWhiteSpace(plainText) || embedData?.Length > 0 || built?.Components.Count > 0)
        {
            var msg = await channel.SendMessageAsync(plainText, embeds: embedData, components: built);
            if (deleteTime > 0)
                msg.DeleteAfter(deleteTime);
        }
        else if (!string.IsNullOrWhiteSpace(content))
        {
            var msg = await channel.SendMessageAsync(content);
            if (deleteTime > 0)
                msg.DeleteAfter(deleteTime);
        }
    }

    private async Task<ulong> SendSmartEmbedWebhookMessage(DiscordWebhookClient webhook, string content, ulong guildId)
    {
        var built = SmartEmbed.TryParse(content, guildId, out var embedData, out var plainText, out var components)
            ? components?.Build()
            : null;
        if (!string.IsNullOrWhiteSpace(plainText) || embedData?.Length > 0 || built?.Components.Count > 0)
        {
            return await webhook.SendMessageAsync(plainText, embeds: embedData, components: built);
        }

        return await webhook.SendMessageAsync(content);
    }

    private async Task HandleGreet(MultiGreet greet, IGuildUser user, ReplacementBuilder replacer)
    {
        if (user.IsBot && !greet.GreetBots)
            return;

        if (greet.Disabled)
            return;

        var inviteSettings = await inviteCountService.GetInviteCountSettingsAsync(user.Guild.Id);
        if (inviteSettings.IsEnabled)
        {
            await Task.Delay(500);
            var inviter = await inviteCountService.GetInviter(user.Id, user.Guild);
            if (inviter == null)
            {
                replacer.WithOverride("%inviter.username%", () => "Unknown");
                replacer.WithOverride("%inviter.avatar%", () => "Unknown");
                replacer.WithOverride("%inviter.id%", () => "Unknown");
                replacer.WithOverride("%inviter.mention%", () => "Unknown");
            }
            else
            {
                var invCount = await inviteCountService.GetInviteCount(inviter.Id, user.Guild.Id);
                replacer.WithOverride("%inviter.username%", () => inviter.Username);
                replacer.WithOverride("%inviter.avatar%", () => inviter.GetAvatarUrl());
                replacer.WithOverride("%inviter.id%", () => user.Id.ToString());
                replacer.WithOverride("%inviter.mention%", () => user.Mention);
                replacer.WithOverride("%inviter.count%", () => invCount.ToString());
            }
        }

        var rep = replacer.Build();
        var content = rep.Replace(greet.Message);
        var channel = await user.Guild.GetTextChannelAsync(greet.ChannelId);

        if (channel == null)
        {
            await RemoveMultiGreetInternal(greet);
            return;
        }

        try
        {
            if (greet.WebhookUrl != null)
            {
                var webhook = new DiscordWebhookClient(greet.WebhookUrl);
                var msgId = await SendSmartEmbedWebhookMessage(webhook, content, user.Guild.Id);
                if (greet.DeleteTime > 0)
                {
                    var msg = await channel.GetMessageAsync(msgId);
                    msg?.DeleteAfter(greet.DeleteTime);
                }
            }
            else
            {
                await SendSmartEmbedMessage(channel, content, user.Guild.Id, greet.DeleteTime);
            }
        }
        catch (HttpException ex)
        {
            if (ex.DiscordCode is DiscordErrorCode.UnknownWebhook or DiscordErrorCode.InvalidWebhookToken
                or DiscordErrorCode.MissingPermissions)
            {
                await MultiGreetDisable(greet, true);
                logger.LogInformation($"MultiGreet disabled in {user.Guild} due to {ex.DiscordCode}.");
            }
        }
    }

    /// <summary>
    ///     Unloads the service and unsubscribes from events.
    /// </summary>
    public Task Unload()
    {
        eventHandler.Unsubscribe("UserJoined", "MultiGreetService", DoMultiGreet);
        return Task.CompletedTask;
    }
}