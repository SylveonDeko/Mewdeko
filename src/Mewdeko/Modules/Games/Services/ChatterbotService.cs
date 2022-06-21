using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Games.Common.ChatterBot;
using Mewdeko.Modules.Permissions.Services;
using Serilog;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Games.Services;

public class ChatterBotService : INService
{
    private readonly DiscordSocketClient _client;
    private readonly BlacklistService _blacklistService;
    private readonly IBotCredentials _creds;
    private readonly DbService _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly GuildSettingsService _guildSettings;
    public List<ulong> LimitUser = new();

    public ChatterBotService(DiscordSocketClient client, IHttpClientFactory factory,
        IBotCredentials creds, DbService db,
        BlacklistService blacklistService,
        GuildSettingsService guildSettings)
    {
        _db = db;
        _blacklistService = blacklistService;
        _guildSettings = guildSettings;
        _client = client;
        _creds = creds;
        _httpFactory = factory;
        _client.MessageReceived += MessageRecieved;
    }

    public static int Priority => -1;
    public static ModuleBehaviorType BehaviorType => ModuleBehaviorType.Executor;

    public ConcurrentDictionary<ulong, Lazy<IChatterBotSession>> CleverbotUsers = new();

    public async Task SetCleverbotChannel(IGuild guild, ulong id)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.CleverbotChannel = id;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public ulong GetCleverbotChannel(ulong id) => _guildSettings.GetGuildConfig(id).CleverbotChannel;

    public Task MessageRecieved(SocketMessage msg)
    {
        _ = Task.Factory.StartNew(async () =>
        {
            if (msg.Author.IsBot)
                return;
            if (msg.Channel is not ITextChannel chan)
                return;
            try
            {
                if (msg is not IUserMessage usrMsg)
                    return;

                IChatterBotSession cbs;
                string message;
                try
                {
                    message = PrepareMessage(usrMsg, out cbs);
                }
                catch
                {
                    return;
                }

                if (string.IsNullOrEmpty(message) || cbs.Equals(default))
                    return;
                var cleverbotExecuted = await TryAsk(cbs, (ITextChannel)usrMsg.Channel, message, usrMsg).ConfigureAwait(false);
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
        }, TaskCreationOptions.LongRunning);
        return Task.CompletedTask;
    }

    public IChatterBotSession CreateSession()
    {
        if (!string.IsNullOrWhiteSpace(_creds.CleverbotApiKey))
            return new OfficialCleverbotSession(_creds.CleverbotApiKey, _httpFactory);
        return new CleverbotIoSession("GAh3wUfzDCpDpdpT", "RStKgqn7tcO9blbrv4KbXM8NDlb7H37C", _httpFactory);
    }

    private string PrepareMessage(IMessage? msg, out IChatterBotSession cleverbot)
    {
        cleverbot = null;
        if (msg?.Channel is not ITextChannel channel)
            return null;
        if (GetCleverbotChannel(channel.Guild.Id) == 0)
            return null;
        if (GetCleverbotChannel(channel.Guild.Id) != channel.Id)
            return null;

        if (_blacklistService.BlacklistEntries.Select(x => x.ItemId).Contains(channel.Guild.Id))
        {
            channel.SendErrorAsync("This server is blacklisted. Please join using the button below for an explanation or to appeal.");
            return null;
        }

        if (_blacklistService.BlacklistEntries.Select(x => x.ItemId).Contains(msg.Author.Id))
        {
            (msg as IUserMessage).ReplyError("You are blacklisted from Mewdeko, join using the button below to get more info or appeal.");
            return null;
        }
        if (!CleverbotUsers.TryGetValue(msg.Author.Id, out var lazyCleverbot))
        {
            CleverbotUsers.TryAdd(msg.Author.Id, new Lazy<IChatterBotSession>(CreateSession, true));
            CleverbotUsers.TryGetValue(msg.Author.Id, out lazyCleverbot);
        }

        cleverbot = lazyCleverbot.Value;

        var mewdekoId = _client.CurrentUser.Id;
        var normalMention = $"<@{mewdekoId}> ";
        var nickMention = $"<@!{mewdekoId}> ";
        string message;

        if (msg.Content.StartsWith(normalMention, StringComparison.InvariantCulture))
            message = msg.Content[normalMention.Length..].Trim();
        else if (msg.Content.StartsWith(nickMention, StringComparison.InvariantCulture))
            message = msg.Content[nickMention.Length..].Trim();
        else if (msg.Content.StartsWith(_guildSettings.GetPrefix(channel.Guild)))
            return null;
        else
            message = msg.Content;
        return message;
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
                "Cleverbot is paid and I cannot pay for it right now! If you want to support Mewdeko and reenable this please donate so it'll be available!\nhttps://ko-fi.com/mewdeko\nThis is not a premium feature and never will be!");
            return false;
        }

        await msg.ReplyAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(response.SanitizeMentions(true)).Build());

        return true;
    }
}