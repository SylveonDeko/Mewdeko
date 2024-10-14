using Discord.Net;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Utility.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.MultiGreets.Services;

/// <summary>
/// Service for handling multiple greeting messages for users joining a guild.
/// </summary>
public class MultiGreetService : INService
{
    private readonly DiscordShardedClient client;
    private readonly DbContextProvider dbProvider;
    private readonly GuildSettingsService guildSettingsService;
    private readonly InviteCountService inviteCountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiGreetService"/> class.
    /// </summary>
    /// <param name="dbProvider">The database context provider.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="guildSettingsService">The guild settings service.</param>
    /// <param name="eventHandler">The event handler for user join events.</param>
    /// <param name="inviteCountService">The invite count service.</param>
    public MultiGreetService(DbContextProvider dbProvider, DiscordShardedClient client,
        GuildSettingsService guildSettingsService, EventHandler eventHandler, InviteCountService inviteCountService)
    {
        this.client = client;
        this.guildSettingsService = guildSettingsService;
        this.inviteCountService = inviteCountService;
        this.dbProvider = dbProvider;
        eventHandler.UserJoined += DoMultiGreet;
    }

    /// <summary>
    /// Gets all greet messages for a specific guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>An array of MultiGreet objects for the specified guild.</returns>
    public async Task<MultiGreet?[]> GetGreets(ulong guildId) =>
        await WithMewdekoContext(db => Task.FromResult(db.MultiGreets.GetAllGreets(guildId)));

    private async Task<MultiGreet?[]> GetForChannel(ulong channelId) =>
        await WithMewdekoContext(db => Task.FromResult(db.MultiGreets.GetForChannel(channelId)));

    private async Task DoMultiGreet(IGuildUser user)
    {
        var greets = await GetGreets(user.Guild.Id);
        if (greets.Length == 0) return;

        var greetType = await GetMultiGreetType(user.Guild.Id);
        if (greetType == 3) return;

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
    /// Sets the multi-greet type for a guild.
    /// </summary>
    /// <param name="guild">The guild to set the multi-greet type for.</param>
    /// <param name="type">The type of multi-greet to set.</param>
    public async Task SetMultiGreetType(IGuild guild, int type)
    {
        await WithMewdekoContextNoReturn(async db =>
        {
            var gc = await db.ForGuildId(guild.Id, set => set);
            gc.MultiGreetType = type;
            await guildSettingsService.UpdateGuildConfig(guild.Id, gc);
        });
    }

    /// <summary>
    /// Gets the multi-greet type for a guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The multi-greet type for the specified guild.</returns>
    public async Task<int> GetMultiGreetType(ulong id) =>
        (await guildSettingsService.GetGuildConfig(id)).MultiGreetType;

    /// <summary>
    /// Adds a new multi-greet for a guild and channel.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="channelId">The ID of the channel.</param>
    /// <returns>True if the multi-greet was added successfully, false otherwise.</returns>
    public async Task<bool> AddMultiGreet(ulong guildId, ulong channelId)
    {
        if ((await GetForChannel(channelId)).Length == 5 || (await GetGreets(guildId)).Length == 30)
            return false;

        await WithMewdekoContextNoReturn(db =>
        {
            db.MultiGreets.Add(new MultiGreet { ChannelId = channelId, GuildId = guildId });
            return db.SaveChangesAsync();
        });
        return true;
    }

    /// <summary>
    /// Changes the message for a specific multi-greet.
    /// </summary>
    /// <param name="greet">The multi-greet to update.</param>
    /// <param name="code">The new message code.</param>
    public async Task ChangeMgMessage(MultiGreet greet, string code) =>
        await UpdateMultiGreet(greet, mg => mg.Message = code);

    /// <summary>
    /// Changes the delete time for a specific multi-greet.
    /// </summary>
    /// <param name="greet">The multi-greet to update.</param>
    /// <param name="howlong">The new delete time in seconds.</param>
    public async Task ChangeMgDelete(MultiGreet greet, int howlong) =>
        await UpdateMultiGreet(greet, mg => mg.DeleteTime = howlong);

    /// <summary>
    /// Changes whether a specific multi-greet should greet bots.
    /// </summary>
    /// <param name="greet">The multi-greet to update.</param>
    /// <param name="enabled">True to greet bots, false otherwise.</param>
    public async Task ChangeMgGb(MultiGreet greet, bool enabled) =>
        await UpdateMultiGreet(greet, mg => mg.GreetBots = enabled);

    /// <summary>
    /// Changes the webhook URL for a specific multi-greet.
    /// </summary>
    /// <param name="greet">The multi-greet to update.</param>
    /// <param name="webhookurl">The new webhook URL.</param>
    public async Task ChangeMgWebhook(MultiGreet greet, string webhookurl) =>
        await UpdateMultiGreet(greet, mg => mg.WebhookUrl = webhookurl);

    /// <summary>
    /// Removes a specific multi-greet.
    /// </summary>
    /// <param name="greet">The multi-greet to remove.</param>
    public async Task RemoveMultiGreetInternal(MultiGreet greet) =>
        await WithMewdekoContextNoReturn(db =>
        {
            db.MultiGreets.Remove(greet);
            return db.SaveChangesAsync();
        });

    /// <summary>
    /// Removes multiple multi-greets.
    /// </summary>
    /// <param name="greets">An array of multi-greets to remove.</param>
    public async Task MultiRemoveMultiGreetInternal(MultiGreet[] greets) =>
        await WithMewdekoContextNoReturn(db =>
        {
            db.MultiGreets.RemoveRange(greets);
            return db.SaveChangesAsync();
        });

    /// <summary>
    /// Enables or disables a specific multi-greet.
    /// </summary>
    /// <param name="greet">The multi-greet to update.</param>
    /// <param name="disabled">True to disable the multi-greet, false to enable it.</param>
    public async Task MultiGreetDisable(MultiGreet greet, bool disabled) =>
        await UpdateMultiGreet(greet, mg => mg.Disabled = disabled);

    private async Task UpdateMultiGreet(MultiGreet greet, Action<MultiGreet> updateAction) =>
        await WithMewdekoContextNoReturn(db =>
        {
            updateAction(greet);
            db.MultiGreets.Update(greet);
            return db.SaveChangesAsync();
        });

    private static async Task SendSmartEmbedMessage(IMessageChannel channel, string content, ulong guildId, int deleteTime = 0)
    {
        if (SmartEmbed.TryParse(content, guildId, out var embedData, out var plainText, out var components))
        {
            var msg = await channel.SendMessageAsync(plainText, embeds: embedData, components: components?.Build());
            if (deleteTime > 0)
                msg.DeleteAfter(deleteTime);
        }
        else
        {
            var msg = await channel.SendMessageAsync(content);
            if (deleteTime > 0)
                msg.DeleteAfter(deleteTime);
        }
    }

    private async Task<ulong> SendSmartEmbedWebhookMessage(DiscordWebhookClient webhook, string content, ulong guildId)
    {
        if (SmartEmbed.TryParse(content, guildId, out var embedData, out var plainText, out var components))
        {
            return await webhook.SendMessageAsync(plainText, embeds: embedData, components: components?.Build());
        }
        else
        {
            return await webhook.SendMessageAsync(content);
        }
    }

    private async Task HandleGreet(MultiGreet greet, IGuildUser user, ReplacementBuilder replacer)
    {
        if (user.IsBot && !greet.GreetBots)
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
            if (ex.DiscordCode is DiscordErrorCode.UnknownWebhook or DiscordErrorCode.InvalidWebhookToken or DiscordErrorCode.MissingPermissions)
            {
                await MultiGreetDisable(greet, true);
                Log.Information($"MultiGreet disabled in {user.Guild} due to {ex.DiscordCode}.");
            }
        }
    }

    private async Task<T> WithMewdekoContext<T>(Func<MewdekoContext, Task<T>> action)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        return await action(dbContext);
    }

    private async Task WithMewdekoContextNoReturn(Func<MewdekoContext, Task> action)
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        await action(dbContext);
    }
}