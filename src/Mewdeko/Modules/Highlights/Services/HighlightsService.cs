using System.Text.RegularExpressions;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Serilog;
using ZiggyCreatures.Caching.Fusion;

namespace Mewdeko.Modules.Highlights.Services;

/// <summary>
/// The service for handling highlights.
/// </summary>
public class HighlightsService : INService, IReadyExecutor
{
    private readonly IFusionCache cache;
    private readonly DiscordShardedClient client;
    private readonly DbContextProvider dbProvider;

    private readonly Channel<(SocketMessage, TaskCompletionSource<bool>)> highlightQueue =
        Channel.CreateBounded<(SocketMessage, TaskCompletionSource<bool>)>(new BoundedChannelOptions(60)
        {
            FullMode = BoundedChannelFullMode.DropNewest
        });

    /// <summary>
    /// Initializes a new instance of the <see cref="HighlightsService"/> class.
    /// </summary>
    /// <param name="client">The discord client</param>
    /// <param name="cache">Fusion cache</param>
    /// <param name="db">The database provider</param>
    public HighlightsService(DiscordShardedClient client, IFusionCache cache, DbContextProvider dbProvider)
    {
        this.client = client;
        this.cache = cache;
        this.dbProvider = dbProvider;
        this.client.MessageReceived += StaggerHighlights;
        this.client.UserIsTyping += AddHighlightTimer;
        _ = HighlightLoop();
    }

    /// <summary>
    /// Caches highlights and settings on bot ready.
    /// </summary>
    /// <returns></returns>
    public async Task OnReadyAsync()
    {
        Log.Information($"Starting {this.GetType()} Cache");
        await using var dbContext = await dbProvider.GetContextAsync();

        var allHighlights = await dbContext.Highlights.AllHighlights();
        var allHighlightSettings = await dbContext.HighlightSettings.AllHighlightSettings();
        foreach (var i in client.Guilds)
        {
            var highlights = allHighlights.Where(x => x.GuildId == i.Id).ToList();
            var hlSettings = allHighlightSettings.Where(x => x.GuildId == i.Id).ToList();
            if (highlights.Any())
            {
                await cache.SetAsync($"highlights_{i.Id}", highlights);
            }

            if (hlSettings.Any())
            {
                await cache.SetAsync($"highlightSettings_{i.Id}", hlSettings);
            }
        }

        Log.Information("Highlights Cached");
    }

    private async Task HighlightLoop()
    {
        while (true)
        {
            var (msg, compl) = await highlightQueue.Reader.ReadAsync().ConfigureAwait(false);
            try
            {
                var res = await ExecuteHighlights(msg).ConfigureAwait(false);
                compl.TrySetResult(res);
            }
            catch
            {
                compl.TrySetResult(false);
            }

            await Task.Delay(2000).ConfigureAwait(false);
        }
    }

    private async Task AddHighlightTimer(Cacheable<IUser, ulong> arg1, Cacheable<IMessageChannel, ulong> _)
    {
        if (arg1.Value is not IGuildUser user)
            return;

        await TryAddHighlightStaggerUser(user.GuildId, user.Id).ConfigureAwait(false);
    }

    private Task StaggerHighlights(SocketMessage message)
    {
        _ = Task.Run(async () =>
        {
            var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await highlightQueue.Writer.WriteAsync((message, completionSource)).ConfigureAwait(false);
            return await completionSource.Task.ConfigureAwait(false);
        });
        return Task.CompletedTask;
    }

    private async Task<bool> ExecuteHighlights(SocketMessage message)
    {
        if (message.Channel is not ITextChannel channel)
            return true;

        if (string.IsNullOrWhiteSpace(message.Content))
            return true;

        var usersDMd = new List<ulong>();
        var content = message.Content;
        var highlightWords = await GetForGuild(channel.Guild.Id);
        if (highlightWords.Count==0)
            return true;

        foreach (var i in highlightWords.Where(h => Regex.IsMatch(content, @$"\b{Regex.Escape(h.Word)}\b")).ToList())
        {
            var cacheKey = $"highlightStagger_{channel.Guild.Id}_{i.UserId}";
            if (await cache.TryGetAsync<bool>(cacheKey).ConfigureAwait(false))
                continue;

            if (usersDMd.Contains(i.UserId))
                continue;

            var settings = (await GetSettingsForGuild(channel.GuildId)).FirstOrDefault(x => x.UserId == i.UserId && x.GuildId == channel.GuildId);
            if (settings is not null)
            {
                if (!settings.HighlightsOn)
                    continue;
                if (settings.IgnoredChannels.Split(" ").Contains(channel.Id.ToString()))
                    continue;
                if (settings.IgnoredUsers.Split(" ").Contains(message.Author.Id.ToString()))
                    continue;
            }

            if (!await TryAddHighlightStaggerUser(channel.Guild.Id, i.UserId).ConfigureAwait(false))
                continue;

            var user = await channel.Guild.GetUserAsync(i.UserId).ConfigureAwait(false);
            var permissions = user.GetPermissions(channel);
            if (!permissions.ViewChannel)
                continue;

            var messages = (await channel.GetMessagesAsync(message.Id, Direction.Before, 5).FlattenAsync().ConfigureAwait(false)).Append(message);
            var eb = new EmbedBuilder().WithOkColor().WithTitle(i.Word.TrimTo(100)).WithDescription(string.Join("\n",
                messages.OrderBy(x => x.Timestamp)
                    .Select(x =>
                        $"<t:{x.Timestamp.ToUnixTimeSeconds()}:T>: {Format.Bold(x.Author.ToString())}: {(x == message ? "***" : "")}" +
                        $"[{x.Content?.TrimTo(100)}]({message.GetJumpLink()}){(x == message ? "***" : "")}" +
                        $" {(x.Embeds?.Count >= 1 || x.Attachments?.Count >= 1 ? " [has embed]" : "")}")));

            var cb = new ComponentBuilder()
                .WithButton("Jump to message", style: ButtonStyle.Link,
                    emote: Emote.Parse("<:MessageLink:778925231506587668>"), url: message.GetJumpLink());

            try
            {
                await user.SendMessageAsync(
                    $"In {Format.Bold(channel.Guild.Name)} {channel.Mention} you were mentioned with highlight word {i.Word}",
                    embed: eb.Build(), components: cb.Build()).ConfigureAwait(false);
                usersDMd.Add(user.Id);
            }
            catch
            {
                // ignored in case a user has dms off
            }
        }

        return true;
    }

    /// <summary>
    /// Adds a highlight word to the database.
    /// </summary>
    /// <param name="guildId">The guild to watch the highlight in</param>
    /// <param name="userId">The user that added the highlight</param>
    /// <param name="word">The word or regex to watch for</param>
    public async Task AddHighlight(ulong guildId, ulong userId, string word)
    {
        var toadd = new Database.Models.Highlights
        {
            UserId = userId, GuildId = guildId, Word = word
        };
        await using var dbContext = await dbProvider.GetContextAsync();

        dbContext.Highlights.Add(toadd);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        var current = await cache.GetOrSetAsync($"highlights_{guildId}", async _ => new List<Database.Models.Highlights>());
        current.Add(toadd);
        await cache.SetAsync($"highlights_{guildId}", current).ConfigureAwait(false);
    }

    /// <summary>
    /// Toggles highlights on or off for a user.
    /// </summary>
    /// <param name="guildId">The guild to toggle highlights in</param>
    /// <param name="userId">The user that wants to toggle highlights</param>
    /// <param name="enabled">Whether its enabled or disabled</param>
    public async Task ToggleHighlights(ulong guildId, ulong userId, bool enabled)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var toupdate = dbContext.HighlightSettings.FirstOrDefault(x => x.UserId == userId && x.GuildId == guildId);
        if (toupdate is null)
        {
            var toadd = new HighlightSettings
            {
                GuildId = guildId,
                UserId = userId,
                HighlightsOn = enabled,
                IgnoredChannels = "0",
                IgnoredUsers = "0"
            };
            dbContext.HighlightSettings.Add(toadd);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
            var current1 = await cache.GetOrSetAsync($"highlightSettings_{guildId}", async _ => new List<HighlightSettings>());
            current1.Add(toadd);
            await cache.SetAsync($"highlightSettings_{guildId}", current1).ConfigureAwait(false);
        }
        else
        {
            toupdate.HighlightsOn = enabled;
            dbContext.HighlightSettings.Update(toupdate);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
            var current = await cache.GetOrSetAsync($"highlightSettings_{guildId}", async _ => new List<HighlightSettings>());
            current.Add(toupdate);
            await cache.SetAsync($"highlightSettings_{guildId}", current).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Toggles a channel to be ignored or not.
    /// </summary>
    /// <param name="guildId">The guild to toggle highlights in</param>
    /// <param name="userId">The user that wants to toggle highlights</param>
    /// <param name="channelId">The channel to toggle</param>
    /// <returns></returns>
    public async Task<bool> ToggleIgnoredChannel(ulong guildId, ulong userId, string channelId)
    {
        var ignored = true;
        await using var dbContext = await dbProvider.GetContextAsync();

        var toupdate = dbContext.HighlightSettings.FirstOrDefault(x => x.UserId == userId && x.GuildId == guildId);
        if (toupdate is null)
        {
            var toadd = new HighlightSettings
            {
                GuildId = guildId,
                UserId = userId,
                HighlightsOn = true,
                IgnoredChannels = "0",
                IgnoredUsers = "0"
            };
            var toedit1 = toadd.IgnoredChannels.Split(" ").ToList();
            toedit1.Add(channelId);
            toadd.IgnoredChannels = string.Join(" ", toedit1);
            dbContext.HighlightSettings.Add(toadd);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
            var current1 = await cache.GetOrSetAsync($"highlightSettings_{guildId}", async _ => new List<HighlightSettings>());
            current1.Add(toadd);
            await cache.SetAsync($"highlightSettings_{guildId}", current1).ConfigureAwait(false);
            return ignored;
        }

        var toedit = toupdate.IgnoredChannels.Split(" ").ToList();
        if (toedit.Contains(channelId))
        {
            toedit.Remove(channelId);
            ignored = false;
        }
        else
        {
            toedit.Add(channelId);
        }

        toupdate.IgnoredChannels = string.Join(" ", toedit);
        dbContext.HighlightSettings.Update(toupdate);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        var current = await cache.GetOrSetAsync($"highlightSettings_{guildId}", async _ => new List<HighlightSettings>());
        current.Add(toupdate);
        await cache.SetAsync($"highlightSettings_{guildId}", current).ConfigureAwait(false);
        return ignored;
    }

    /// <summary>
    /// Toggles a user to be ignored or not.
    /// </summary>
    /// <param name="guildId">The guild to toggle highlights in</param>
    /// <param name="userId">The user that wants to toggle highlights</param>
    /// <param name="userToIgnore">The user to toggle</param>
    /// <returns></returns>
    public async Task<bool> ToggleIgnoredUser(ulong guildId, ulong userId, string userToIgnore)
    {
        var ignored = true;
        await using var dbContext = await dbProvider.GetContextAsync();

        var toupdate = dbContext.HighlightSettings.FirstOrDefault(x => x.UserId == userId && x.GuildId == guildId);
        if (toupdate is null)
        {
            var toadd = new HighlightSettings
            {
                GuildId = guildId,
                UserId = userId,
                HighlightsOn = true,
                IgnoredChannels = "0",
                IgnoredUsers = "0"
            };
            var toedit1 = toadd.IgnoredUsers.Split(" ").ToList();
            toedit1.Add(userToIgnore);
            toadd.IgnoredUsers = string.Join(" ", toedit1);
            dbContext.HighlightSettings.Add(toadd);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
            var current1 = await cache.GetOrSetAsync($"highlightSettings_{guildId}", async _ => new List<HighlightSettings>());
            current1.Add(toadd);
            await cache.SetAsync($"highlightSettings_{guildId}", current1).ConfigureAwait(false);
            return ignored;
        }

        var toedit = toupdate.IgnoredUsers.Split(" ").ToList();
        if (toedit.Contains(userToIgnore))
        {
            toedit.Remove(userToIgnore);
            ignored = false;
        }
        else
        {
            toedit.Add(userToIgnore);
        }

        toupdate.IgnoredUsers = string.Join(" ", toedit);
        dbContext.HighlightSettings.Update(toupdate);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        var current = await cache.GetOrSetAsync($"highlightSettings_{guildId}", async _ => new List<HighlightSettings>());
        current.Add(toupdate);
        await cache.SetAsync($"highlightSettings_{guildId}", current).ConfigureAwait(false);
        return ignored;
    }

    /// <summary>
    /// Removes a highlight word from the database.
    /// </summary>
    /// <param name="toremove">The db record to remove</param>
    public async Task RemoveHighlight(Database.Models.Highlights? toremove)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        dbContext.Highlights.Remove(toremove);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        var current = await cache.GetOrSetAsync($"highlights_{toremove.GuildId}", async _ => new List<Database.Models.Highlights>());
        if (current.Count > 0)
        {
            current.Remove(toremove);
        }

        await cache.SetAsync($"highlights_{toremove.GuildId}", current).ConfigureAwait(false);
    }

    private async Task<List<Database.Models.Highlights?>> GetForGuild(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var highlightsForGuild = await cache.GetOrSetAsync($"highlights_{guildId}", async _ =>
        {

            return dbContext.Highlights.Where(x => x.GuildId == guildId).ToList();
        });
        return highlightsForGuild;
    }

    private async Task<IEnumerable<HighlightSettings?>> GetSettingsForGuild(ulong guildId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var highlightSettingsForGuild = await cache.GetOrSetAsync($"highlightSettings_{guildId}", async _ =>
        {

            return dbContext.HighlightSettings.Where(x => x.GuildId == guildId).ToList();
        });
        return highlightSettingsForGuild;
    }

    private async Task<bool> TryAddHighlightStaggerUser(ulong guildId, ulong userId)
    {
        var cacheKey = $"highlightStagger_{guildId}_{userId}";
        var result = await cache.TryGetAsync<bool>(cacheKey);
        if (result.HasValue) return false;
        await cache.SetAsync(cacheKey, true, TimeSpan.FromMinutes(2));
        return true;
    }
}