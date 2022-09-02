using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using StackExchange.Redis;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Image = Discord.Image;

namespace Mewdeko.Modules.OwnerOnly.Services;

public class OwnerOnlyService : ILateExecutor, IReadyExecutor, INService
{
    private readonly Mewdeko _bot;
    private readonly BotConfigService _bss;

    private readonly IDataCache _cache;
    private readonly DiscordSocketClient _client;
    private readonly CommandHandler _cmdHandler;
    private readonly IBotCredentials _creds;
    private readonly DbService _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly Replacer _rep;
    private readonly IBotStrings _strings;
    private readonly GuildSettingsService _guildSettings;

#pragma warning disable CS8714
    private ConcurrentDictionary<ulong?, ConcurrentDictionary<int, Timer>> autoCommands =
#pragma warning restore CS8714
        new();

    private ImmutableDictionary<ulong, IDMChannel> ownerChannels =
        new Dictionary<ulong, IDMChannel>().ToImmutableDictionary();

    public OwnerOnlyService(DiscordSocketClient client, CommandHandler cmdHandler, DbService db,
        IBotStrings strings, IBotCredentials creds, IDataCache cache, IHttpClientFactory factory,
        BotConfigService bss, IEnumerable<IPlaceholderProvider> phProviders, Mewdeko bot,
        GuildSettingsService guildSettings)
    {
        var redis = cache.Redis;
        _cmdHandler = cmdHandler;
        _db = db;
        _strings = strings;
        _client = client;
        _creds = creds;
        _cache = cache;
        _bot = bot;
        _guildSettings = guildSettings;
        var imgs = cache.LocalImages;
        _httpFactory = factory;
        _bss = bss;
        if (client.ShardId == 0)
        {
            _rep = new ReplacementBuilder()
                .WithClient(client)
                .WithProviders(phProviders)
                .Build();

            _ = new Timer(RotatingStatuses, new TimerState(), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        var sub = redis.GetSubscriber();
        if (_client.ShardId == 0)
        {
            sub.Subscribe($"{_creds.RedisKey()}_reload_images",
                delegate { imgs.Reload(); }, CommandFlags.FireAndForget);
        }

        sub.Subscribe($"{_creds.RedisKey()}_leave_guild", async (_, v) =>
        {
            try
            {
                var guildStr = v.ToString()?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(guildStr))
                    return;
                var server = _client.Guilds.FirstOrDefault(g => g.Id.ToString() == guildStr) ??
                             _client.Guilds.FirstOrDefault(g => g.Name.Trim().ToUpperInvariant() == guildStr);

                if (server == null) return;

                if (server.OwnerId != _client.CurrentUser.Id)
                {
                    await server.LeaveAsync().ConfigureAwait(false);
                    Log.Information($"Left server {server.Name} [{server.Id}]");
                }
                else
                {
                    await server.DeleteAsync().ConfigureAwait(false);
                    Log.Information($"Deleted server {server.Name} [{server.Id}]");
                }
            }
            catch
            {
                // ignored
            }
        }, CommandFlags.FireAndForget);
    }

    // forwards dms
    public async Task LateExecute(DiscordSocketClient client, IGuild guild, IUserMessage msg)
    {
        var bs = _bss.Data;
        if (msg.Channel is IDMChannel && _bss.Data.ForwardMessages && ownerChannels.Count > 0)
        {
            var title = $"{_strings.GetText("dm_from")} [{msg.Author}]({msg.Author.Id})";

            var attachamentsTxt = _strings.GetText("attachments");

            var toSend = msg.Content;

            if (msg.Attachments.Count > 0)
            {
                toSend +=
                    $"\n\n{Format.Code(attachamentsTxt)}:\n{string.Join("\n", msg.Attachments.Select(a => a.ProxyUrl))}";
            }

            if (bs.ForwardToAllOwners)
            {
                var allOwnerChannels = ownerChannels.Values;

                foreach (var ownerCh in allOwnerChannels.Where(ch => ch.Recipient.Id != msg.Author.Id))
                {
                    try
                    {
                        await ownerCh.SendConfirmAsync(title, toSend).ConfigureAwait(false);
                    }
                    catch
                    {
                        Log.Warning("Can't contact owner with id {0}", ownerCh.Recipient.Id);
                    }
                }
            }
            else
            {
                var firstOwnerChannel = ownerChannels.Values.First();
                if (firstOwnerChannel.Recipient.Id != msg.Author.Id)
                {
                    try
                    {
                        await firstOwnerChannel.SendConfirmAsync(title, toSend).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }
    }

    public async Task OnReadyAsync()
    {
        await using var uow = _db.GetDbContext();

        autoCommands =
            uow.AutoCommands
            .AsNoTracking()
            .Where(x => x.Interval >= 5)
            .AsEnumerable()
            .GroupBy(x => x.GuildId)
            .ToDictionary(x => x.Key,
                y => y.ToDictionary(x => x.Id, TimerFromAutoCommand)
                    .ToConcurrent())
            .ToConcurrent();

        foreach (var cmd in uow.AutoCommands.AsNoTracking().Where(x => x.Interval == 0))
        {
            try
            {
                await ExecuteCommand(cmd).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }

        if (_client.ShardId == 0)
        {
            var channels = await Task.WhenAll(_creds.OwnerIds.Select(id =>
            {
                var user = _client.GetUser(id);
                return user == null ? Task.FromResult<IDMChannel?>(null) : user.CreateDMChannelAsync();
            })).ConfigureAwait(false);
            
            ownerChannels = channels.Where(x => x is not null)
                                    .ToDictionary(x => x.Recipient.Id, x => x)
                                    .ToImmutableDictionary();

            if (ownerChannels.Count == 0)
            {
                Log.Warning(
                                "No owner channels created! Make sure you've specified the correct OwnerId in the credentials.json file and invited the bot to a Discord server.");
            }
            else
            {
                Log.Information(
                                $"Created {ownerChannels.Count} out of {_creds.OwnerIds.Length} owner message channels.");
            }
        }
    }

    private async void RotatingStatuses(object objState)
    {
        try
        {
            var state = (TimerState)objState;

            if (!_bss.Data.RotateStatuses) return;

            IReadOnlyList<RotatingPlayingStatus> rotatingStatuses;
            var uow = _db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                rotatingStatuses = uow.RotatingStatus
                                      .AsNoTracking()
                                      .OrderBy(x => x.Id)
                                      .ToList();
            }

            if (rotatingStatuses.Count == 0)
                return;

            var playingStatus = state.Index >= rotatingStatuses.Count
                ? rotatingStatuses[state.Index = 0]
                : rotatingStatuses[state.Index++];

            var statusText = _rep.Replace(playingStatus.Status);
            await _bot.SetGameAsync(statusText, playingStatus.Type).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Rotating playing status errored: {ErrorMessage}", ex.Message);
        }
    }

    public async Task<string?> RemovePlayingAsync(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        await using var uow = _db.GetDbContext();
        var toRemove = await uow.RotatingStatus
                                .AsQueryable()
                                .AsNoTracking()
                                .Skip(index)
                                .FirstOrDefaultAsync().ConfigureAwait(false);

        if (toRemove is null)
            return null;

        uow.Remove(toRemove);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        return toRemove.Status;
    }

    public async Task AddPlaying(ActivityType t, string status)
    {
        await using var uow = _db.GetDbContext();
        var toAdd = new RotatingPlayingStatus { Status = status, Type = t };
        uow.Add(toAdd);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public bool ToggleRotatePlaying()
    {
        var enabled = false;
        _bss.ModifyConfig(bs => enabled = bs.RotateStatuses = !bs.RotateStatuses);
        return enabled;
    }

    public IReadOnlyList<RotatingPlayingStatus> GetRotatingStatuses()
    {
        using var uow = _db.GetDbContext();
        return uow.RotatingStatus.AsNoTracking().ToList();
    }

    private Timer TimerFromAutoCommand(AutoCommand x) =>
        new(async obj => await ExecuteCommand((AutoCommand)obj).ConfigureAwait(false),
            x,
            x.Interval * 1000,
            x.Interval * 1000);

    private async Task ExecuteCommand(AutoCommand cmd)
    {
        try
        {
            if (cmd.GuildId is null)
                return;

            var guildShard = (int)((cmd.GuildId.Value >> 22) % (ulong)_creds.TotalShards);
            if (guildShard != _client.ShardId)
                return;
            var prefix = await _guildSettings.GetPrefix(cmd.GuildId.Value);
            //if someone already has .die as their startup command, ignore it
            if (cmd.CommandText.StartsWith($"{prefix}die", StringComparison.InvariantCulture))
                return;
            await _cmdHandler.ExecuteExternal(cmd.GuildId, cmd.ChannelId, cmd.CommandText).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in SelfService ExecuteCommand");
        }
    }

    public void AddNewAutoCommand(AutoCommand cmd)
    {
        using (var uow = _db.GetDbContext())
        {
            uow.AutoCommands.Add(cmd);
            uow.SaveChanges();
        }

        if (cmd.Interval >= 5)
        {
            var autos = autoCommands.GetOrAdd(cmd.GuildId, new ConcurrentDictionary<int, Timer>());
            autos.AddOrUpdate(cmd.Id, _ => TimerFromAutoCommand(cmd), (_, old) =>
            {
                old.Change(Timeout.Infinite, Timeout.Infinite);
                return TimerFromAutoCommand(cmd);
            });
        }
    }
    public string SetDefaultPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentNullException(nameof(prefix));

        _bss.ModifyConfig(bs => bs.Prefix = prefix);

        return prefix;
    }

    public IEnumerable<AutoCommand> GetStartupCommands()
    {
        using var uow = _db.GetDbContext();
        return
            uow.AutoCommands
            .AsNoTracking()
            .Where(x => x.Interval == 0)
            .OrderBy(x => x.Id)
            .ToList();
    }

    public IEnumerable<AutoCommand> GetAutoCommands()
    {
        using var uow = _db.GetDbContext();
        return
            uow.AutoCommands
            .AsNoTracking()
            .Where(x => x.Interval >= 5)
            .OrderBy(x => x.Id)
            .ToList();
    }

    public Task LeaveGuild(string guildStr)
    {
        var sub = _cache.Redis.GetSubscriber();
        return sub.PublishAsync($"{_creds.RedisKey()}_leave_guild", guildStr);
    }

    public bool RestartBot()
    {
        var cmd = _creds.RestartCommand;
        if (string.IsNullOrWhiteSpace(cmd.Cmd)) return false;

        Restart();
        return true;
    }

    public bool RemoveStartupCommand(int index, out AutoCommand cmd)
    {
        using var uow = _db.GetDbContext();
        cmd = uow.AutoCommands
            .AsNoTracking()
            .Where(x => x.Interval == 0)
            .Skip(index)
            .FirstOrDefault();

        if (cmd != null)
        {
            uow.Remove(cmd);
            uow.SaveChanges();
            return true;
        }

        return false;
    }

    public bool RemoveAutoCommand(int index, out AutoCommand cmd)
    {
        using var uow = _db.GetDbContext();
        cmd = uow.AutoCommands
            .AsNoTracking()
            .Where(x => x.Interval >= 5)
            .Skip(index)
            .FirstOrDefault();

        if (cmd == null) return false;
        uow.Remove(cmd);
        if (autoCommands.TryGetValue(cmd.GuildId, out var autos))
        {
            if (autos.TryRemove(cmd.Id, out var timer))
                timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        uow.SaveChanges();
        return true;
    }

    public async Task<bool> SetAvatar(string img)
    {
        if (string.IsNullOrWhiteSpace(img))
            return false;

        if (!Uri.IsWellFormedUriString(img, UriKind.Absolute))
            return false;

        var uri = new Uri(img);

        using var http = _httpFactory.CreateClient();
        using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        if (!sr.IsImage())
            return false;

        // i can't just do ReadAsStreamAsync because dicord.net's image poops itself
        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var imgStream = imgData.ToStream();
        await using var _ = imgStream.ConfigureAwait(false);
        await _client.CurrentUser.ModifyAsync(u => u.Avatar = new Image(imgStream)).ConfigureAwait(false);

        return true;
    }

    public void ClearStartupCommands()
    {
        using var uow = _db.GetDbContext();
        var toRemove =
            uow.AutoCommands
            .AsNoTracking()
            .Where(x => x.Interval == 0);

        uow.AutoCommands.RemoveRange(toRemove);
        uow.SaveChanges();
    }

    public void ReloadImages()
    {
        var sub = _cache.Redis.GetSubscriber();
        sub.Publish($"{_creds.RedisKey()}_reload_images", "");
    }

    public void Die()
    {
        var sub = _cache.Redis.GetSubscriber();
        sub.Publish($"{_creds.RedisKey()}_die", "", CommandFlags.FireAndForget);
    }

    public void Restart()
    {
        Process.Start(_creds.RestartCommand.Cmd, _creds.RestartCommand.Args);
        var sub = _cache.Redis.GetSubscriber();
        sub.Publish($"{_creds.RedisKey()}_die", "", CommandFlags.FireAndForget);
    }

    public bool RestartShard(int shardId)
    {
        if (shardId < 0 || shardId >= _creds.TotalShards)
            return false;

        var pub = _cache.Redis.GetSubscriber();
        pub.Publish($"{_creds.RedisKey()}_shardcoord_stop",
            JsonConvert.SerializeObject(shardId),
            CommandFlags.FireAndForget);

        return true;
    }

    public bool ForwardMessages()
    {
        var isForwarding = false;
        _bss.ModifyConfig(config => isForwarding = config.ForwardMessages = !config.ForwardMessages);

        return isForwarding;
    }

    public bool ForwardToAll()
    {
        var isToAll = false;
        _bss.ModifyConfig(config => isToAll = config.ForwardToAllOwners = !config.ForwardToAllOwners);
        return isToAll;
    }

    private class TimerState
    {
        public int Index { get; set; }
    }
}