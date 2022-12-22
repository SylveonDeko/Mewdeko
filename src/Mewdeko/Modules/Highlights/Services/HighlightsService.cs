using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mewdeko.Common.ModuleBehaviors;
using Serilog;

namespace Mewdeko.Modules.Highlights.Services;

public class HighlightsService : INService, IReadyExecutor
{
    private readonly DiscordSocketClient client;
    private readonly IDataCache cache;
    private readonly DbService db;

    public HighlightsService(DiscordSocketClient client, IDataCache cache, DbService db)
    {
        this.client = client;
        this.cache = cache;
        this.db = db;
        this.client.MessageReceived += StaggerHighlights;
        this.client.UserIsTyping += AddHighlightTimer;
        _ = HighlightLoop();
    }

    public async Task HighlightLoop()
    {
        while (true)
        {
            bool res;
            var (msg, compl) = await highlightQueue.Reader.ReadAsync().ConfigureAwait(false);
            try
            {
                res = await ExecuteHighlights(msg).ConfigureAwait(false);
            }
            catch
            {
                continue;
            }

            compl.TrySetResult(res);
            await Task.Delay(2000).ConfigureAwait(false);
        }
    }

    private async Task AddHighlightTimer(Cacheable<IUser, ulong> arg1, Cacheable<IMessageChannel, ulong> _)
    {
        if (arg1.Value is not IGuildUser user)
            return;

        await cache.TryAddHighlightStagger(user.GuildId, user.Id).ConfigureAwait(false);
    }

    public Task OnReadyAsync()
    {
        using var uow = db.GetDbContext();
        var allHighlights = uow.Highlights.AllHighlights();
        var allHighlightSettings = uow.HighlightSettings.AllHighlightSettings();
        foreach (var i in client.Guilds)
        {
            var highlights = allHighlights.FirstOrDefault(x => x.GuildId == i.Id);
            var hlSettings = allHighlightSettings.FirstOrDefault(x => x.GuildId == i.Id);
            if (highlights is not null)
            {
                _ = Task.Run(async () => await cache.CacheHighlights(i.Id, allHighlights.Where(x => x.GuildId == i.Id).ToList()).ConfigureAwait(false));
            }

            if (hlSettings is not null)
            {
                _ = Task.Run(async () => await cache.CacheHighlightSettings(i.Id, allHighlightSettings.Where(x => x.GuildId == i.Id).ToList()).ConfigureAwait(false));
            }
        }

        Log.Information("Highlights Cached.");
        return Task.CompletedTask;
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

    private readonly Channel<(SocketMessage, TaskCompletionSource<bool>)> highlightQueue =
        Channel.CreateBounded<(SocketMessage, TaskCompletionSource<bool>)>(new BoundedChannelOptions(60)
        {
            FullMode = BoundedChannelFullMode.DropNewest
        });

    private async Task<bool> ExecuteHighlights(SocketMessage message)
    {
        if (message.Channel is not ITextChannel channel)
            return true;

        if (string.IsNullOrWhiteSpace(message.Content))
            return true;
        var usersDMd = new List<ulong>();
        var content = message.Content;
        var highlightWords = GetForGuild(channel.Guild.Id);
        if (highlightWords.Count == 0)
            return true;
        foreach (var i in (List<Database.Models.Highlights>)(from h in highlightWords where Regex.IsMatch(h.Word, @$"\b{Regex.Escape(content)}\b") select h).ToList())
        {
            if (await cache.GetHighlightStagger(channel.Guild.Id, i.UserId).ConfigureAwait(false))
                continue;
            if (usersDMd.Contains(i.UserId))
                continue;
            if (GetSettingsForGuild(channel.GuildId).Any())
            {
                var settings = GetSettingsForGuild(channel.GuildId)
                    .FirstOrDefault(x => x.UserId == i.UserId && x.GuildId == channel.GuildId);
                if (settings is not null)
                {
                    if (!settings.HighlightsOn)
                        continue;
                    if (settings.IgnoredChannels.Split(" ").Contains(channel.Id.ToString()))
                        continue;
                    if (settings.IgnoredUsers.Split(" ").Contains(message.Author.Id.ToString()))
                        continue;
                }
            }

            if (!await cache.TryAddHighlightStaggerUser(i.UserId).ConfigureAwait(false))
                continue;
            var user = await channel.Guild.GetUserAsync(i.UserId).ConfigureAwait(false);
            var permissions = user.GetPermissions(channel);
            IEnumerable<IMessage> messages;
            if (!permissions.ViewChannel)
                continue;
            try
            {
                messages = (await channel.GetMessagesAsync(message.Id, Direction.Before, 5).FlattenAsync().ConfigureAwait(false)).Append(message);
            }
            catch
            {
                // dont get messages if it doesnt have message history access
                continue;
            }

            var eb = new EmbedBuilder().WithOkColor().WithTitle(i.Word.TrimTo(100)).WithDescription(string.Join("\n", messages.OrderBy(x => x.Timestamp)
                .Select(x => $"<t:{x.Timestamp.ToUnixTimeSeconds()}:T>: {Format.Bold(x.Author.ToString())}: {(x == message ? "***" : "")}" +
                             $"[{x.Content?.TrimTo(100)}]({message.GetJumpLink()}){(x == message ? "***" : "")}" +
                             $" {(x.Embeds?.Count >= 1 || x.Attachments?.Count >= 1 ? " [has embed]" : "")}")));

            var cb = new ComponentBuilder()
                .WithButton("Jump to message", style: ButtonStyle.Link, emote: Emote.Parse("<:MessageLink:778925231506587668>"), url: message.GetJumpLink());

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

    public async Task AddHighlight(ulong guildId, ulong userId, string word)
    {
        var toadd = new Database.Models.Highlights
        {
            UserId = userId, GuildId = guildId, Word = word
        };
        await using var uow = db.GetDbContext();
        uow.Highlights.Add(toadd);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        var current = cache.GetHighlightsForGuild(guildId) ?? new List<Database.Models.Highlights?>();
        current.Add(toadd);
        await cache.AddHighlightToCache(guildId, current).ConfigureAwait(false);
    }

    public async Task ToggleHighlights(ulong guildId, ulong userId, bool enabled)
    {
        await using var uow = db.GetDbContext();
        var toupdate = uow.HighlightSettings.FirstOrDefault(x => x.UserId == userId && x.GuildId == guildId);
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
            uow.HighlightSettings.Add(toadd);
            await uow.SaveChangesAsync().ConfigureAwait(false);
            var current1 = cache.GetHighlightSettingsForGuild(guildId) ?? new List<HighlightSettings?>();
            current1.Add(toadd);
            await cache.AddHighlightSettingToCache(guildId, current1).ConfigureAwait(false);
        }

        toupdate.HighlightsOn = enabled;
        uow.HighlightSettings.Update(toupdate);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        var current = cache.GetHighlightSettingsForGuild(guildId) ?? new List<HighlightSettings?>();
        current.Add(toupdate);
        await cache.AddHighlightSettingToCache(guildId, current).ConfigureAwait(false);
    }

    public async Task<bool> ToggleIgnoredChannel(ulong guildId, ulong userId, string channelId)
    {
        var ignored = true;
        await using var uow = db.GetDbContext();
        var toupdate = uow.HighlightSettings.FirstOrDefault(x => x.UserId == userId && x.GuildId == guildId);
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
            toedit1.Add(channelId);
            toadd.IgnoredChannels = string.Join(" ", toedit1);
            uow.HighlightSettings.Add(toadd);
            await uow.SaveChangesAsync().ConfigureAwait(false);
            var current1 = cache.GetHighlightSettingsForGuild(guildId) ?? new List<HighlightSettings?>();
            current1.Add(toadd);
            await cache.AddHighlightSettingToCache(guildId, current1).ConfigureAwait(false);
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
        uow.HighlightSettings.Update(toupdate);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        var current = cache.GetHighlightSettingsForGuild(guildId) ?? new List<HighlightSettings?>();
        current.Add(toupdate);
        await cache.AddHighlightSettingToCache(guildId, current).ConfigureAwait(false);
        return ignored;
    }

    public async Task<bool> ToggleIgnoredUser(ulong guildId, ulong userId, string userToIgnore)
    {
        var ignored = true;
        await using var uow = db.GetDbContext();
        var toupdate = uow.HighlightSettings.FirstOrDefault(x => x.UserId == userId && x.GuildId == guildId);
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
            uow.HighlightSettings.Add(toadd);
            await uow.SaveChangesAsync().ConfigureAwait(false);
            var current1 = cache.GetHighlightSettingsForGuild(guildId) ?? new List<HighlightSettings?>();
            current1.Add(toadd);
            await cache.AddHighlightSettingToCache(guildId, current1).ConfigureAwait(false);
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
        uow.HighlightSettings.Update(toupdate);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        var current = cache.GetHighlightSettingsForGuild(guildId) ?? new List<HighlightSettings?>();
        current.Add(toupdate);
        await cache.AddHighlightSettingToCache(guildId, current).ConfigureAwait(false);
        return ignored;
    }

    public async Task RemoveHighlight(Database.Models.Highlights? toremove)
    {
        await using var uow = db.GetDbContext();
        uow.Highlights.Remove(toremove);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        var current = cache.GetHighlightsForGuild(toremove.GuildId) ?? new List<Database.Models.Highlights?>();
        if (current.Count > 0)
        {
            toremove.Id = 0;
            current.Remove(toremove);
        }

        await cache.RemoveHighlightFromCache(toremove.GuildId, current).ConfigureAwait(false);
    }

    public List<Database.Models.Highlights?> GetForGuild(ulong guildId)
    {
        var highlightsForGuild = this.cache.GetHighlightsForGuild(guildId);
        if (highlightsForGuild is not null) return highlightsForGuild;
        using var uow = db.GetDbContext();
        var highlights = uow.Highlights.Where(x => x.GuildId == guildId).ToList();
        if (highlights.Count == 0) return new List<Database.Models.Highlights?>();
        this.cache.AddHighlightToCache(guildId, highlights);
        return highlights;
    }

    public IEnumerable<HighlightSettings?> GetSettingsForGuild(ulong guildId)
    {
        var highlightSettingsForGuild = this.cache.GetHighlightSettingsForGuild(guildId);
        if (highlightSettingsForGuild is not null) return highlightSettingsForGuild;
        using var uow = db.GetDbContext();
        var highlightSettings = uow.HighlightSettings.Where(x => x.GuildId == guildId).ToList();
        if (highlightSettings.Count == 0) return new List<HighlightSettings?>();
        this.cache.AddHighlightSettingToCache(guildId, highlightSettings);
        return highlightSettings;
    }
}