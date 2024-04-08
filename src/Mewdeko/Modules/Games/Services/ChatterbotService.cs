using System.Net.Http;
using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Games.Common.ChatterBot;
using Mewdeko.Modules.Permissions.Services;
using Serilog;

namespace Mewdeko.Modules.Games.Services;

/// <summary>
/// Represents a service for interacting with the Cleverbot API.
/// </summary>
public class ChatterBotService : INService
{
    private readonly DiscordSocketClient client;
    private readonly BlacklistService blacklistService;
    private readonly IBotCredentials creds;
    private readonly DbService db;
    private readonly IHttpClientFactory httpFactory;
    private readonly GuildSettingsService guildSettings;
    private readonly BotConfig config;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatterBotService"/> class.
    /// </summary>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="factory">The HTTP client factory.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="db">The database service.</param>
    /// <param name="blacklistService">The blacklist service.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    /// <param name="eventHandler">The event handler.</param>
    public ChatterBotService(DiscordSocketClient client, IHttpClientFactory factory,
        IBotCredentials creds, DbService db,
        BlacklistService blacklistService,
        GuildSettingsService guildSettings, EventHandler eventHandler, BotConfig config)
    {
        this.db = db;
        this.blacklistService = blacklistService;
        this.guildSettings = guildSettings;
        this.config = config;
        this.client = client;
        this.creds = creds;
        httpFactory = factory;
        eventHandler.MessageReceived += MessageReceived;
    }


    /// <summary>
    /// Gets the priority of the chatterbot service.
    /// </summary>
    public static int Priority => -1;

    /// <summary>
    /// Gets the behavior type of the chatterbot service.
    /// </summary>
    public static ModuleBehaviorType BehaviorType => ModuleBehaviorType.Executor;

    /// <summary>
    /// Dictionary to store the Cleverbot sessions for users.
    /// </summary>
    public readonly ConcurrentDictionary<ulong, Lazy<IChatterBotSession>> CleverbotUsers = new();

    /// <summary>
    /// Sets the Cleverbot channel for a guild.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="id">The ID of the channel.</param>
    public async Task SetCleverbotChannel(IGuild guild, ulong id)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.CleverbotChannel = id;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    /// <summary>
    /// Gets the Cleverbot channel ID for a guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The Cleverbot channel ID.</returns>
    public async Task<ulong> GetCleverbotChannel(ulong id) => (await guildSettings.GetGuildConfig(id)).CleverbotChannel;

    /// <summary>
    /// Handles received messages for Cleverbot interactions.
    /// </summary>
    /// <param name="msg">The received message.</param>
    public async Task MessageReceived(SocketMessage msg)
    {
        if (msg.Author.IsBot)
            return;
        if (msg.Channel is not ITextChannel chan)
            return;
        try
        {
            if (msg is not IUserMessage usrMsg)
                return;
            (string, IChatterBotSession) message;
            try
            {
                message = await PrepareMessage(usrMsg);
            }
            catch
            {
                return;
            }

            if (string.IsNullOrEmpty(message.Item1) || message.Item2.Equals(default))
                return;
            var cleverbotExecuted = await TryAsk(message.Item2, (ITextChannel)usrMsg.Channel, message.Item1, usrMsg)
                .ConfigureAwait(false);
            if (cleverbotExecuted)
            {
                Log.Information(
                    $@"CleverBot Executed
                Server: {chan.Guild.Name} {chan.Guild.Name}]
                Channel: {usrMsg.Channel?.Name} [{usrMsg.Channel?.Id}]
                UserId: {usrMsg.Author} [{usrMsg.Author.Id}]
                Message: {usrMsg.Content}");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in Cleverbot");
        }
    }

    /// <summary>
    /// Creates a new session for Cleverbot interactions.
    /// </summary>
    /// <returns>A new Cleverbot session.</returns>
    public IChatterBotSession CreateSession()
    {
        if (!string.IsNullOrWhiteSpace(creds.CleverbotApiKey))
            return new OfficialCleverbotSession(creds.CleverbotApiKey, httpFactory);
        return new CleverbotIoSession("GAh3wUfzDCpDpdpT", "RStKgqn7tcO9blbrv4KbXM8NDlb7H37C", httpFactory);
    }


    /// <summary>
    /// Prepares the message for Cleverbot interaction.
    /// </summary>
    /// <param name="msg">The received message.</param>
    /// <returns>A tuple containing the message content and the Cleverbot session.</returns>
    private async Task<(string, IChatterBotSession)> PrepareMessage(IMessage? msg)
    {
        if (msg?.Channel is not ITextChannel channel)
            return (null, null);
        if (await GetCleverbotChannel(channel.Guild.Id) == 0)
            return (null, null);
        if (await GetCleverbotChannel(channel.Guild.Id) != channel.Id)
            return (null, null);

        if (blacklistService.BlacklistEntries.Select(x => x.ItemId).Contains(channel.Guild.Id))
        {
            await channel.SendErrorAsync(
                "This server is blacklisted. Please join using the button below for an explanation or to appeal.",
                config);
            return (null, null);
        }

        if (blacklistService.BlacklistEntries.Select(x => x.ItemId).Contains(msg.Author.Id))
        {
            (msg as IUserMessage).ReplyError(
                "You are blacklisted from Mewdeko, join using the button below to get more info or appeal.");
            return (null, null);
        }

        if (!CleverbotUsers.TryGetValue(msg.Author.Id, out var lazyCleverbot))
        {
            CleverbotUsers.TryAdd(msg.Author.Id, new Lazy<IChatterBotSession>(CreateSession, true));
            CleverbotUsers.TryGetValue(msg.Author.Id, out lazyCleverbot);
        }


        var mewdekoId = client.CurrentUser.Id;
        var normalMention = $"<@{mewdekoId}> ";
        var nickMention = $"<@!{mewdekoId}> ";
        string message;

        if (msg.Content.StartsWith(normalMention, StringComparison.InvariantCulture))
            message = msg.Content[normalMention.Length..].Trim();
        else if (msg.Content.StartsWith(nickMention, StringComparison.InvariantCulture))
            message = msg.Content[nickMention.Length..].Trim();
        else if (msg.Content.StartsWith(await guildSettings.GetPrefix(channel.Guild)))
            return (null, null);
        else
            message = msg.Content;
        return (message, lazyCleverbot.Value);
    }

    /// <summary>
    /// Tries to ask Cleverbot a question and sends the response.
    /// </summary>
    /// <param name="cleverbot">The Cleverbot session.</param>
    /// <param name="channel">The text channel to send the response to.</param>
    /// <param name="message">The message to ask Cleverbot.</param>
    /// <param name="msg">The original user message.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task<bool> TryAsk(IChatterBotSession cleverbot, ITextChannel channel, string message,
        IUserMessage msg)
    {
        await channel.TriggerTypingAsync().ConfigureAwait(false);
        string response;
        try
        {
            response = await cleverbot.Think(message).ConfigureAwait(false);
        }
        catch
        {
            await channel.SendErrorAsync(
                    "Cleverbot is paid and I cannot pay for it right now! If you want to support Mewdeko and reenable this please donate so it'll be available!\nhttps://ko-fi.com/mewdeko\nThis is not a premium feature and never will be!",
                    config)
                .ConfigureAwait(false);
            return false;
        }

        await msg.ReplyAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(response.SanitizeMentions(true))
            .Build()).ConfigureAwait(false);

        return true;
    }
}