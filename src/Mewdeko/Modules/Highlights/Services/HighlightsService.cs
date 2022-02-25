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
    
        
}