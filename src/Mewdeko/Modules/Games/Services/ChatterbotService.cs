using System.Net.Http;
using System.Threading.Tasks;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Games.Common.ChatterBot;
using Mewdeko.Modules.Permissions.Services;
using Serilog;

namespace Mewdeko.Modules.Games.Services;

public class ChatterBotService : INService
{
    private readonly DiscordSocketClient client;
    private readonly BlacklistService blacklistService;
    private readonly IBotCredentials creds;
    private readonly DbService db;
    private readonly IHttpClientFactory httpFactory;
    private readonly GuildSettingsService guildSettings;
    public List<ulong> LimitUser = new();

    public ChatterBotService(DiscordSocketClient client, IHttpClientFactory factory,
        IBotCredentials creds, DbService db,
        BlacklistService blacklistService,
        GuildSettingsService guildSettings, EventHandler eventHandler)
    {
        this.db = db;
        this.blacklistService = blacklistService;
        this.guildSettings = guildSettings;
        this.client = client;
        this.creds = creds;
        httpFactory = factory;
        eventHandler.MessageReceived += MessageRecieved;
    }

    public static int Priority => -1;
    public static ModuleBehaviorType BehaviorType => ModuleBehaviorType.Executor;

    public readonly ConcurrentDictionary<ulong, Lazy<IChatterBotSession>> CleverbotUsers = new();

    public async Task SetCleverbotChannel(IGuild guild, ulong id)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.CleverbotChannel = id;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task<ulong> GetCleverbotChannel(ulong id) => (await guildSettings.GetGuildConfig(id)).CleverbotChannel;

    public async Task MessageRecieved(SocketMessage msg)
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
            var cleverbotExecuted = await TryAsk(message.Item2, (ITextChannel)usrMsg.Channel, message.Item1, usrMsg).ConfigureAwait(false);
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
            Log.Warning(ex, "Error in cleverbot");
        }
    }

    public IChatterBotSession CreateSession()
    {
        if (!string.IsNullOrWhiteSpace(creds.CleverbotApiKey))
            return new OfficialCleverbotSession(creds.CleverbotApiKey, httpFactory);
        return new CleverbotIoSession("GAh3wUfzDCpDpdpT", "RStKgqn7tcO9blbrv4KbXM8NDlb7H37C", httpFactory);
    }

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
            await channel.SendErrorAsync("This server is blacklisted. Please join using the button below for an explanation or to appeal.");
            return (null, null);
        }

        if (blacklistService.BlacklistEntries.Select(x => x.ItemId).Contains(msg.Author.Id))
        {
            (msg as IUserMessage).ReplyError("You are blacklisted from Mewdeko, join using the button below to get more info or appeal.");
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

    private static async Task<bool> TryAsk(IChatterBotSession cleverbot, ITextChannel channel, string message, IUserMessage msg)
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
                    "Cleverbot is paid and I cannot pay for it right now! If you want to support Mewdeko and reenable this please donate so it'll be available!\nhttps://ko-fi.com/mewdeko\nThis is not a premium feature and never will be!")
                .ConfigureAwait(false);
            return false;
        }

        await msg.ReplyAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(response.SanitizeMentions(true)).Build()).ConfigureAwait(false);

        return true;
    }
}