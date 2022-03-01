using Amazon.S3.Model;
using Discord;
using Discord.WebSocket;
using Mewdeko._Extensions;
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
        var usersDMd = new List<ulong>();
        var splitwords = message.Content.Split(" ");
        var highlightWords = GetForGuild(channel.Guild.Id);
        var toSend = (from i in splitwords from j in highlightWords where String.Equals(j.Word, i, StringComparison.CurrentCultureIgnoreCase) select j).ToList();
        foreach (var i in toSend)
        {
            if (usersDMd.Contains(i.UserId))
                return;
            var user = await channel.Guild.GetUserAsync(i.UserId);
            var permissions = user.GetPermissions(channel);
            if (!permissions.ViewChannel)
                return;
            var messages = await channel.GetMessagesAsync(5, Direction.Before).FlattenAsync();
            var eb = new EmbedBuilder().WithOkColor().WithTitle(i.Word.TrimTo(100)).WithDescription(string.Join("\n",
                messages.Select(x => $"{x.Timestamp}: {Format.Bold(user.ToString())}: {x.Content?.TrimTo(100)}")));
            await user.SendMessageAsync(
                $"In {channel.Guild} {channel.Mention} you were mentioned with highlight word {i.Word}",
                embed: eb.Build());
        }
        
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