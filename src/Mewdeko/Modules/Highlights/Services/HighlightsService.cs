using System.Text.RegularExpressions;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Analytics;
using ZiggyCreatures.Caching.Fusion;

namespace Mewdeko.Modules.Highlights.Services;

/// <summary>
///     The service for handling highlights.
/// </summary>
public class HighlightsService : INService, IReadyExecutor, IUnloadableService
{
    private readonly IFusionCache cache;
    private readonly DiscordShardedClient client;
    private readonly IAnalyticsCollector collector;
    private readonly IDataConnectionFactory dbFactory;
    private readonly EventHandler eventHandler;


    private readonly Channel<(SocketMessage, TaskCompletionSource<bool>)> highlightQueue =
        Channel.CreateBounded<(SocketMessage, TaskCompletionSource<bool>)>(new BoundedChannelOptions(60)
        {
            FullMode = BoundedChannelFullMode.DropNewest
        });

    private readonly ILogger<HighlightsService> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HighlightsService" /> class.
    /// </summary>
    /// <param name="client">The discord client</param>
    /// <param name="cache">Fusion cache</param>
    /// <param name="dbFactory">The database provider</param>
    /// <param name="eventHandler">Async event handler because discord stoopid</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="collector">The analytics collector.</param>
    public HighlightsService(DiscordShardedClient client, IFusionCache cache, IDataConnectionFactory dbFactory,
        EventHandler eventHandler, ILogger<HighlightsService> logger, IAnalyticsCollector collector)
    {
        this.collector = collector;
        this.client = client;
        this.cache = cache;
        this.dbFactory = dbFactory;
        this.eventHandler = eventHandler;
        this.logger = logger;
        eventHandler.Subscribe("MessageReceived", "HighlightsService", StaggerHighlights);
        eventHandler.Subscribe("UserIsTyping", "HighlightsService", AddHighlightTimer);

        // Clean up cache when leaving guilds
        eventHandler.Subscribe("LeftGuild", "HighlightsService", OnLeftGuild);

        _ = HighlightLoop();
    }

    /// <summary>
    ///     Caches highlights and settings on bot ready.
    /// </summary>
    /// <returns></returns>
    public async Task OnReadyAsync()
    {
        await Task.CompletedTask; // Keep async signature
        logger.LogInformation("Starting {Type} Cache - Lazy loading enabled", GetType());

        // Optional: Background cleanup of old/unused data
        _ = Task.Run(async () =>
        {
            try
            {
                await using var dbContext = await dbFactory.CreateConnectionAsync();

                // lean up highlights for guilds the bot is no longer in
                var currentGuildIds = client.Guilds.Select(g => g.Id).ToHashSet();
                var orphanedHighlights = await dbContext.Highlights
                    .Where(h => !currentGuildIds.Contains(h.GuildId))
                    .CountAsync();

                if (orphanedHighlights > 0)
                {
                    await dbContext.Highlights
                        .Where(h => !currentGuildIds.Contains(h.GuildId))
                        .DeleteAsync();

                    logger.LogInformation("Cleaned up {Count} orphaned highlights during startup", orphanedHighlights);
                }

                // Clean up settings too
                var orphanedSettings = await dbContext.HighlightSettings
                    .Where(h => !currentGuildIds.Contains(h.GuildId))
                    .CountAsync();

                if (orphanedSettings > 0)
                {
                    await dbContext.HighlightSettings
                        .Where(h => !currentGuildIds.Contains(h.GuildId))
                        .DeleteAsync();

                    logger.LogInformation("Cleaned up {Count} orphaned highlight settings during startup",
                        orphanedSettings);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during startup highlights cleanup");
            }
        });

        logger.LogInformation("Highlights Service Ready - data loads on-demand per guild");
    }

    /// <summary>
    ///     Unloads the service and unsubscribes from events.
    /// </summary>
    public Task Unload()
    {
        eventHandler.Unsubscribe("MessageReceived", "HighlightsService", StaggerHighlights);
        eventHandler.Unsubscribe("UserIsTyping", "HighlightsService", AddHighlightTimer);
        eventHandler.Unsubscribe("LeftGuild", "HighlightsService", OnLeftGuild);
        return Task.CompletedTask;
    }

    private async Task OnLeftGuild(SocketGuild guild)
    {
        try
        {
            // Remove cached data for this guild
            await cache.RemoveAsync($"highlights_{guild.Id}");
            await cache.RemoveAsync($"highlightSettings_{guild.Id}");

            logger.LogInformation("Cleaned up highlight cache for left guild {GuildId}", guild.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cleaning up highlight cache for left guild {GuildId}", guild.Id);
        }
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
        if (highlightWords.Count == 0)
            return true;

        foreach (var i in highlightWords
                     .Where(h => h.Word != null && Regex.IsMatch(content, @$"\b{Regex.Escape(h.Word)}\b")).ToList())
        {
            var cacheKey = $"highlightStagger_{channel.Guild.Id}_{i.UserId}";
            if (await cache.TryGetAsync<bool>(cacheKey).ConfigureAwait(false))
                continue;

            if (usersDMd.Contains(i.UserId))
                continue;

            var settings =
                (await GetSettingsForGuild(channel.GuildId)).FirstOrDefault(x =>
                    x.UserId == i.UserId && x.GuildId == channel.GuildId);
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

            var messages =
                (await channel.GetMessagesAsync(message.Id, Direction.Before, 5).FlattenAsync().ConfigureAwait(false))
                .Append(message);
            if (i.Word != null)
            {
                var eb = new EmbedBuilder().WithOkColor().WithTitle(i.Word.TrimTo(100)).WithDescription(string.Join(
                    "\n",
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
                    collector.Feature("highlight", channel.GuildId);
                }
                catch
                {
                    collector.Feature("highlight", channel.GuildId, false, "dm_failed");
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Adds a highlight word to the database.
    /// </summary>
    /// <param name="guildId">The guild to watch the highlight in</param>
    /// <param name="userId">The user that added the highlight</param>
    /// <param name="word">The word or regex to watch for</param>
    public async Task AddHighlight(ulong guildId, ulong userId, string word)
    {
        var toadd = new Highlight
        {
            UserId = userId, GuildId = guildId, Word = word
        };
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        await dbContext.InsertAsync(toadd);

        // Invalidate cache so next access reloads from DB
        await cache.RemoveAsync($"highlights_{guildId}");
    }

    /// <summary>
    ///     Toggles highlights on or off for a user.
    /// </summary>
    /// <param name="guildId">The guild to toggle highlights in</param>
    /// <param name="userId">The user that wants to toggle highlights</param>
    /// <param name="enabled">Whether its enabled or disabled</param>
    public async Task ToggleHighlights(ulong guildId, ulong userId, bool enabled)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var toupdate = dbContext.HighlightSettings.FirstOrDefault(x => x.UserId == userId && x.GuildId == guildId);
        if (toupdate is null)
        {
            var toadd = new HighlightSetting
            {
                GuildId = guildId,
                UserId = userId,
                HighlightsOn = enabled,
                IgnoredChannels = "0",
                IgnoredUsers = "0"
            };
            await dbContext.InsertAsync(toadd);

            var current1 = await cache.GetOrSetAsync($"highlightSettings_{guildId}",
                _ => Task.FromResult(new List<HighlightSetting>()));
            current1.Add(toadd);
            await cache.SetAsync($"highlightSettings_{guildId}", current1,
                options => options
                    .SetDuration(TimeSpan.FromMinutes(30))
            ).ConfigureAwait(false);
        }
        else
        {
            toupdate.HighlightsOn = enabled;
            await dbContext.UpdateAsync(toupdate);

            var current = await cache.GetOrSetAsync($"highlightSettings_{guildId}",
                _ => Task.FromResult(new List<HighlightSetting>()));
            current.Add(toupdate);
            await cache.SetAsync($"highlightSettings_{guildId}", current,
                options => options
                    .SetDuration(TimeSpan.FromMinutes(30))
            ).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Toggles a channel to be ignored or not.
    /// </summary>
    /// <param name="guildId">The guild to toggle highlights in</param>
    /// <param name="userId">The user that wants to toggle highlights</param>
    /// <param name="channelId">The channel to toggle</param>
    /// <returns></returns>
    public async Task<bool> ToggleIgnoredChannel(ulong guildId, ulong userId, string channelId)
    {
        var ignored = true;
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var toupdate = dbContext.HighlightSettings.FirstOrDefault(x => x.UserId == userId && x.GuildId == guildId);
        if (toupdate is null)
        {
            var toadd = new HighlightSetting
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
            await dbContext.InsertAsync(toadd);

            var current1 = await cache.GetOrSetAsync($"highlightSettings_{guildId}",
                _ => Task.FromResult(new List<HighlightSetting>()));
            current1.Add(toadd);
            await cache.SetAsync($"highlightSettings_{guildId}", current1,
                options => options
                    .SetDuration(TimeSpan.FromMinutes(30))
            ).ConfigureAwait(false);
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
        await dbContext.UpdateAsync(toupdate);

        var current =
            await cache.GetOrSetAsync($"highlightSettings_{guildId}",
                _ => Task.FromResult(new List<HighlightSetting>()));
        current.Add(toupdate);
        await cache.SetAsync($"highlightSettings_{guildId}", current).ConfigureAwait(false);
        return ignored;
    }

    /// <summary>
    ///     Toggles a user to be ignored or not.
    /// </summary>
    /// <param name="guildId">The guild to toggle highlights in</param>
    /// <param name="userId">The user that wants to toggle highlights</param>
    /// <param name="userToIgnore">The user to toggle</param>
    /// <returns></returns>
    public async Task<bool> ToggleIgnoredUser(ulong guildId, ulong userId, string userToIgnore)
    {
        var ignored = true;
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        var toupdate = dbContext.HighlightSettings.FirstOrDefault(x => x.UserId == userId && x.GuildId == guildId);
        if (toupdate is null)
        {
            var toadd = new HighlightSetting
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
            await dbContext.InsertAsync(toadd);

            var current1 = await cache.GetOrSetAsync($"highlightSettings_{guildId}",
                _ => Task.FromResult(new List<HighlightSetting>()));
            current1.Add(toadd);
            await cache.SetAsync($"highlightSettings_{guildId}", current1,
                options => options
                    .SetDuration(TimeSpan.FromMinutes(30))
            ).ConfigureAwait(false);
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
        await dbContext.UpdateAsync(toupdate);

        var current =
            await cache.GetOrSetAsync($"highlightSettings_{guildId}",
                _ => Task.FromResult(new List<HighlightSetting>()));
        current.Add(toupdate);
        await cache.SetAsync($"highlightSettings_{guildId}", current).ConfigureAwait(false);
        return ignored;
    }

    /// <summary>
    ///     Removes a highlight word from the database.
    /// </summary>
    /// <param name="toremove">The db record to remove</param>
    public async Task RemoveHighlight(Highlight? toremove)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        await dbContext.Highlights.Select(x => toremove).DeleteAsync();

        var current = await cache.GetOrSetAsync($"highlights_{toremove.GuildId}",
            _ => Task.FromResult(new List<Highlight>()));
        if (current.Count > 0)
        {
            current.Remove(toremove);
        }

        await cache.SetAsync($"highlights_{toremove.GuildId}", current,
            options => options
                .SetDuration(TimeSpan.FromMinutes(30))
        ).ConfigureAwait(false);
    }

    private async Task<List<Highlight?>> GetForGuild(ulong guildId)
    {
        return await cache.GetOrSetAsync($"highlights_{guildId}", async _ =>
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            return await dbContext.Highlights
                .Where(x => x.GuildId == guildId)
                .ToListAsync();
        }, options => options.SetDuration(TimeSpan.FromMinutes(30)));
    }

    private async Task<IEnumerable<HighlightSetting?>> GetSettingsForGuild(ulong guildId)
    {
        return await cache.GetOrSetAsync($"highlightSettings_{guildId}", async _ =>
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            return await dbContext.HighlightSettings
                .Where(x => x.GuildId == guildId)
                .ToListAsync();
        }, options => options.SetDuration(TimeSpan.FromMinutes(30)));
    }

    private async Task<bool> TryAddHighlightStaggerUser(ulong guildId, ulong userId)
    {
        var cacheKey = $"highlightStagger_{guildId}_{userId}";
        var result = await cache.TryGetAsync<bool>(cacheKey);
        if (result.HasValue) return false;
        await cache.SetAsync(cacheKey, true,
            options => options
                .SetDuration(TimeSpan.FromMinutes(2))
        );
        return true;
    }
}