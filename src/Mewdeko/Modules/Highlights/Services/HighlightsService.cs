using Discord;
using Discord.WebSocket;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;

namespace Mewdeko.Modules.Highlights.Services;

public class HighlightsService : INService
{
    private readonly DiscordSocketClient _client;
    private readonly IDataCache _cache;
    private readonly DbService _db;

    public HighlightsService(DiscordSocketClient client, IDataCache cache, DbService db)
    {
        _client = client;
        _cache = cache;
        _db = db;
        _ = CacheToRedis();
        _client.MessageReceived += CheckHighlights;
    }

    private async Task CheckHighlights(SocketMessage message)
    {
        if (message.Channel is not ITextChannel channel)
            return;
        
        if (string.IsNullOrWhiteSpace(message.Content))
            return;

        var splitwords = message.Content.Split("");
        var highlightWords = GetForGuild(channel.Guild.Id);
        var toSend = (from i in splitwords from j in highlightWords where String.Equals(j.Word, i, StringComparison.CurrentCultureIgnoreCase) select j).ToList();
        
    }
    
    public async Task CacheToRedis()
    {
        var uow = _db.GetDbContext();
        var allHighlights = uow.Highlights.AllHighlights();
        var allHighlightSettings = uow.HighlightSettings.AllHighlightSettings();
        var guilds = await _client.Rest.GetGuildsAsync();
        foreach (var i in guilds)
        {
            await _cache.CacheHighlights(i.Id, allHighlights.Where(x => x.GuildId == i.Id).ToList());
            await _cache.CacheHighlightSettings(i.Id, allHighlightSettings.Where(x => x.GuildId == i.Id).ToList());
        }
    }

    public async Task AddHighlight(ulong guildId, ulong userId, string word)
    {
        var toadd = new Database.Models.Highlights { UserId = userId, GuildId = guildId, Word = word };
        var uow = _db.GetDbContext();
        uow.Highlights.Add(toadd);
        var current = _cache.GetHighlightsForGuild(guildId);
        current.Add(toadd);
        await _cache.AddHighlightToCache(guildId, current);
    }

    public List<Database.Models.Highlights> GetForGuild(ulong guildId) 
        => _cache.GetHighlightsForGuild(guildId);


}