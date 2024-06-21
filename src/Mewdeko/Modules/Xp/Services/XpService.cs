using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Humanizer;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Xp.Common;
using Mewdeko.Services.Impl;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SkiaSharp;
using ZiggyCreatures.Caching.Fusion;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
/// Represents a service that handles experience points (XP) in the bot.
/// </summary>
public class XpService : INService, IUnloadableService, IReadyExecutor
{
    /// <summary>
    /// Represents the constant experience points required to reach level 1.
    /// </summary>
    public const int XpRequiredLvl1 = 36;

    private readonly ConcurrentQueue<UserCacheItem> addMessageXp = new();
    private readonly Mewdeko bot;
    private readonly DiscordShardedClient client;
    private readonly CommandHandler cmd;
    private readonly IBotCredentials creds;
    private readonly DbService db;
    private readonly EventHandler eventHandler;
    private readonly GuildSettingsService guildSettings;
    private readonly IImageCache images;
    private readonly IBotStrings strings;
    private readonly XpConfigService xpConfig;

    private readonly IFusionCache cache;

    /// <summary>
    /// Represents a service that handles experience points (XP) in the bot
    /// </summary>
    /// <param name="client">The Discord client instance.</param>
    /// <param name="cmd">The command handler instance.</param>
    /// <param name="db">The database service instance.</param>
    /// <param name="strings">The bot strings service instance.</param>
    /// <param name="creds">The bot credentials instance.</param>
    /// <param name="xpConfig">The XP configuration service instance.</param>
    /// <param name="bot">The bot instance.</param>
    /// <param name="eventHandler">The event handler instance.</param>
    /// <param name="guildSettings">The guild settings service instance.</param>
    public XpService(
        DiscordShardedClient client,
        CommandHandler cmd,
        DbService db,
        IBotStrings strings,
        IBotCredentials creds,
        XpConfigService xpConfig,
        Mewdeko bot,
        EventHandler eventHandler,
        GuildSettingsService guildSettings,
        IFusionCache cache)
    {
        this.db = db;
        this.cmd = cmd;
        this.images = null;
        this.strings = strings;
        this.creds = creds;
        this.xpConfig = xpConfig;
        this.bot = bot;
        this.eventHandler = eventHandler;
        this.guildSettings = guildSettings;
        this.client = client;
        this.cache = cache;

        this.cmd.OnMessageNoTrigger += Cmd_OnMessageNoTrigger;
        eventHandler.UserVoiceStateUpdated += Client_OnUserVoiceStateUpdated;

        this.client.GuildAvailable += Client_OnGuildAvailable;
        foreach (var guild in this.client.Guilds) Client_OnGuildAvailable(guild);
        _ = Task.Run(UpdateLoop);
    }


    /// <summary>
    /// Handles the XP update loop asynchronously.
    /// </summary>
   private async Task UpdateLoop()
{
    while (true)
    {
        await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        try
        {
            var toNotify = new List<(IGuild Guild, IMessageChannel? MessageChannel, IUser User, int Level, XpNotificationLocation NotifyType, NotifOf NotifOf)>();
            var roleRewards = new Dictionary<ulong, List<XpRoleReward>>();
            var curRewards = new Dictionary<ulong, List<XpCurrencyReward>>();

            var toAddTo = new List<UserCacheItem>();
            while (addMessageXp.TryDequeue(out var usr)) toAddTo.Add(usr);

            var group = toAddTo.GroupBy(x => (GuildId: x.Guild.Id, x.User));
            if (toAddTo.Count == 0) continue;

            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                foreach (var item in group)
                {
                    var xp = item.Sum(x => x.XpAmount);

                    var usr = await uow.UserXpStats.GetOrCreateUser(item.Key.GuildId, item.Key.User.Id);
                    var du = await uow.GetOrCreateUser(item.Key.User).ConfigureAwait(false);

                    var globalXp = du.TotalXp;
                    var oldGlobalLevelData = new LevelStats(globalXp);
                    var newGlobalLevelData = new LevelStats(globalXp + xp);

                    var oldGuildLevelData = new LevelStats(usr.Xp + usr.AwardedXp);
                    usr.Xp += xp;
                    du.TotalXp += xp;
                    var newGuildLevelData = new LevelStats(usr.Xp + usr.AwardedXp);

                    if (oldGlobalLevelData.Level < newGlobalLevelData.Level)
                    {
                        du.LastLevelUp = DateTime.UtcNow;
                        var first = item.First();
                        if (du.NotifyOnLevelUp != XpNotificationLocation.None)
                        {
                            toNotify.Add((first.Guild, first.Channel, first.User, newGlobalLevelData.Level,
                                du.NotifyOnLevelUp, NotifOf.Global));
                        }
                    }

                    if (oldGuildLevelData.Level >= newGuildLevelData.Level) continue;
                    {
                        usr.LastLevelUp = DateTime.UtcNow;
                        var first = item.First();
                        if (usr.NotifyOnLevelUp != XpNotificationLocation.None)
                        {
                            toNotify.Add((first.Guild, first.Channel, first.User, newGuildLevelData.Level,
                                usr.NotifyOnLevelUp, NotifOf.Server));
                        }

                        var guildSettingsKey = $"XpSettings_{usr.GuildId}";
                        var settings = await cache.TryGetAsync<XpSettings>(guildSettingsKey);

                        if (!settings.HasValue)
                        {
                            var config = await uow.GuildConfigs.AsNoTracking()
                                .Include(x => x.XpSettings)
                                    .ThenInclude(x => x.RoleRewards)
                                .Include(x => x.XpSettings)
                                    .ThenInclude(x => x.CurrencyRewards)
                                .Include(x => x.XpSettings)
                                    .ThenInclude(x => x.ExclusionList)
                                .FirstOrDefaultAsync(x => x.GuildId == usr.GuildId);

                            settings = config?.XpSettings;

                            if (settings != null)
                            {
                                await cache.SetAsync(guildSettingsKey, settings);
                            }
                        }

                        if (settings != null)
                        {
                            if (!roleRewards.ContainsKey(usr.GuildId))
                            {
                                roleRewards[usr.GuildId] = settings.Value.RoleRewards.ToList();
                            }

                            if (!curRewards.ContainsKey(usr.GuildId))
                            {
                                curRewards[usr.GuildId] = settings.Value.CurrencyRewards.ToList();
                            }
                        }

                        for (var i = oldGuildLevelData.Level + 1; i <= newGuildLevelData.Level; i++)
                        {
                            var rrew = roleRewards[usr.GuildId].Find(x => x.Level == i);
                            if (rrew == null) continue;
                            var role = first.User.Guild.GetRole(rrew.RoleId);
                            if (role is not null) _ = first.User.AddRoleAsync(role);
                        }
                    }
                }

                await uow.SaveChangesAsync().ConfigureAwait(false);
            }

            await Task.WhenAll(toNotify.Select(async x =>
            {
                if (x.NotifOf == NotifOf.Server)
                {
                    if (x.NotifyType == XpNotificationLocation.Dm)
                    {
                        var chan = await x.User.CreateDMChannelAsync().ConfigureAwait(false);
                        if (chan != null)
                        {
                            await chan.SendConfirmAsync(strings.GetText("level_up_dm", x.Guild.Id, x.User.Mention,
                                    Format.Bold(x.Level.ToString()), Format.Bold(x.Guild.ToString() ?? "-")))
                                .ConfigureAwait(false);
                        }
                    }
                    else if (x.MessageChannel != null)
                    {
                        await x.MessageChannel.SendConfirmAsync(strings.GetText("level_up_channel", x.Guild.Id,
                            x.User.Mention, Format.Bold(x.Level.ToString()))).ConfigureAwait(false);
                    }
                }
                else
                {
                    IMessageChannel chan;
                    if (x.NotifyType == XpNotificationLocation.Dm)
                        chan = await x.User.CreateDMChannelAsync().ConfigureAwait(false);
                    else
                        chan = x.MessageChannel;

                    await chan.SendConfirmAsync(strings.GetText("level_up_global", x.Guild.Id, x.User.Mention,
                        Format.Bold(x.Level.ToString()))).ConfigureAwait(false);
                }
            })).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in the XP update loop");
        }
    }
}



    /// <summary>
    /// Unloads the service by removing event handlers.
    /// </summary>
    public Task Unload()
    {
        cmd.OnMessageNoTrigger -= Cmd_OnMessageNoTrigger;
        eventHandler.UserVoiceStateUpdated -= Client_OnUserVoiceStateUpdated;
        client.GuildAvailable -= Client_OnGuildAvailable;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        var sw = new Stopwatch();
        sw.Start();
        await using var uow = db.GetDbContext();

        var guildConfigs = await uow.GuildConfigs.AsNoTracking()
            .Include(x => x.XpSettings)
                .ThenInclude(x => x.RoleRewards)
            .Include(x => x.XpSettings)
                .ThenInclude(x => x.CurrencyRewards)
            .Include(x => x.XpSettings)
                .ThenInclude(x => x.ExclusionList)
            .ToListAsync();

        sw.Stop();
        Log.Information($"Xp Settings cached in {sw.Elapsed}");

        foreach (var config in guildConfigs)
        {
            await cache.SetAsync($"XpSettings_{config.GuildId}", config.XpSettings);
        }
    }

    private async Task<T> GetOrAddCacheItem<T>(string key, Func<Task<T>> factory)
    {
        // var sw = new Stopwatch();
        // sw.Start();
        var fu = await cache.GetOrSetAsync(key, factory);
        // sw.Stop();
        // Log.Information($"Cache save/get completed for {key} in {sw.Elapsed}");
        return await fu.Invoke();
    }

    /// <summary>
    /// Retrieves the role rewards for a guild asynchronously.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    public async Task<IEnumerable<XpRoleReward>> GetRoleRewards(ulong id)
    {
        var settings = await GetOrAddCacheItem($"XpSettings_{id}", async () =>
        {
            await using var uow = db.GetDbContext();
            var config = await uow.GuildConfigs.AsNoTracking()
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.RoleRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.CurrencyRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.ExclusionList)
                .FirstOrDefaultAsync(x => x.GuildId == id);
            return config?.XpSettings;
        });

        return settings?.RoleRewards ?? Enumerable.Empty<XpRoleReward>();
    }

    /// <summary>
    /// Sets the role reward for a specific level in a guild asynchronously.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="level">The level for which to set the reward.</param>
    /// <param name="roleId">The role ID to assign as a reward, or null to remove the reward.</param>

    public async void SetRoleReward(ulong guildId, int level, ulong? roleId)
    {
        await using var uow = db.GetDbContext();
        var settings = await uow.XpSettingsFor(guildId);

        if (roleId == null)
        {
            var toRemove = settings.RoleRewards.FirstOrDefault(x => x.Level == level);
            if (toRemove != null)
            {
                uow.Remove(toRemove);
                settings.RoleRewards.Remove(toRemove);
            }
        }
        else
        {
            var rew = settings.RoleRewards.FirstOrDefault(x => x.Level == level);

            if (rew != null)
                rew.RoleId = roleId.Value;
            else
                settings.RoleRewards.Add(new XpRoleReward
                {
                    Level = level, RoleId = roleId.Value
                });
        }

        await uow.SaveChangesAsync().ConfigureAwait(false);
        await cache.SetAsync($"XpSettings_{guildId}", settings);
    }

    /// <summary>
    /// Retrieves the user XP stats for a guild and page number asynchronously.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="page">The page number.</param>
    public async Task<List<UserXpStats>> GetUserXps(ulong guildId, int page)
    {
        await using var uow = db.GetDbContext();
        return await uow.UserXpStats.GetUsersFor(guildId, page);
    }

    /// <summary>
    /// Retrieves the top user XP stats for a guild asynchronously.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public async Task<List<UserXpStats>> GetTopUserXps(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        return await uow.UserXpStats.GetTopUserXps(guildId);
    }

    /// <summary>
    /// Retrieves the user XP leaderboard for a specific page.
    /// </summary>
    /// <param name="page">The page number.</param>
    public DiscordUser[] GetUserXps(int page)
    {
        using var uow = db.GetDbContext();
        return uow.DiscordUser.GetUsersXpLeaderboardFor(page);
    }

    /// <summary>
    /// Changes the notification type for a user in a guild asynchronously.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="type">The notification type.</param>
    public async Task ChangeNotificationType(ulong userId, ulong guildId, XpNotificationLocation type)
    {
        await using var uow = db.GetDbContext();
        var user = await uow.UserXpStats.GetOrCreateUser(guildId, userId);
        user.NotifyOnLevelUp = type;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await cache.RemoveAsync($"XpSettings_{guildId}");
    }

    /// <summary>
    /// Retrieves the notification type for a user in a guild asynchronously.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The guild ID.</param>
    public async Task<XpNotificationLocation> GetNotificationType(ulong userId, ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var user = await uow.UserXpStats.GetOrCreateUser(guildId, userId);
        return user.NotifyOnLevelUp;
    }

    /// <summary>
    /// Retrieves the notification type for a user asynchronously.
    /// </summary>
    /// <param name="user">The user instance.</param>
    public async Task<XpNotificationLocation> GetNotificationType(IUser user)
    {
        await using var uow = db.GetDbContext();
        return (await uow.GetOrCreateUser(user).ConfigureAwait(false)).NotifyOnLevelUp;
    }

    /// <summary>
    /// Changes the notification type for a user asynchronously.
    /// </summary>
    /// <param name="user">The user instance.</param>
    /// <param name="type">The notification type.</param>
    public async Task ChangeNotificationType(IUser user, XpNotificationLocation type)
    {
        await using var uow = db.GetDbContext();
        var du = await uow.GetOrCreateUser(user).ConfigureAwait(false);
        du.NotifyOnLevelUp = type;
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    private Task Client_OnGuildAvailable(SocketGuild guild)
    {
        _ = Task.Run(() =>
        {
            foreach (var channel in guild.VoiceChannels) ScanChannelForVoiceXp(channel);
        });

        return Task.CompletedTask;
    }

    private async Task Client_OnUserVoiceStateUpdated(SocketUser socketUser, SocketVoiceState before, SocketVoiceState after)
    {
        var sw = new Stopwatch();
        sw.Start();
        if (socketUser is not SocketGuildUser user || user.IsBot)
            return;
        var vcxp = await GetVoiceXpRate(user.Guild.Id);
        var vctime = await GetVoiceXpTimeout(user.Guild.Id);
        if (vctime == 0 || vcxp == 0 || !bot.Ready.Task.IsCompleted)
        {
            sw.Stop();
            Log.Information($"VC Check in {user.Guild.Id} took {sw.Elapsed}");
            return;
        }
        if (before.VoiceChannel != null) ScanChannelForVoiceXp(before.VoiceChannel);

        if (after.VoiceChannel != null && after.VoiceChannel != before.VoiceChannel)
        {
            sw.Stop();
            Log.Information($"VC Check in {user.Guild.Id} took {sw.Elapsed}");
            ScanChannelForVoiceXp(after.VoiceChannel);
        }
        else if (after.VoiceChannel == null)
        {
            sw.Stop();
            Log.Information($"VC Check in {user.Guild.Id} took {sw.Elapsed}");
            UserLeftVoiceChannel(user, before.VoiceChannel);
        }
    }

    private void ScanChannelForVoiceXp(SocketVoiceChannel channel)
    {
        if (ShouldTrackVoiceChannel(channel))
        {
            foreach (var user in channel.Users)
                ScanUserForVoiceXp(user, channel);
        }
        else
        {
            foreach (var user in channel.Users)
                UserLeftVoiceChannel(user, channel);
        }
    }

    private async void ScanUserForVoiceXp(SocketGuildUser user, SocketGuildChannel channel)
    {
        if (UserParticipatingInVoiceChannel(user) && await ShouldTrackXp(user, channel.Id))
            UserJoinedVoiceChannel(user);
        else
            UserLeftVoiceChannel(user, channel);
    }

    private static bool ShouldTrackVoiceChannel(SocketGuildChannel channel) =>
        channel.Users.Where(x => !x.IsBot && UserParticipatingInVoiceChannel(x)).Take(2).Count() >= 2;

    private static bool UserParticipatingInVoiceChannel(IVoiceState user) =>
        !user.IsDeafened && !user.IsMuted && !user.IsSelfDeafened && !user.IsSelfMuted;

    private async void UserJoinedVoiceChannel(SocketGuildUser user)
    {
        var key = $"{creds.RedisKey()}_user_xp_vc_join_{user.Id}";
        var value = DateTimeOffset.UtcNow;
        var e = await GetVoiceXpTimeout(user.Guild.Id) == 0 ? xpConfig.Data.VoiceMaxMinutes : await GetVoiceXpTimeout(user.Guild.Id);

        var maybeValue = await cache.TryGetAsync<DateTimeOffset>(key);
        if (maybeValue.HasValue)
            return;

        await cache.SetAsync(key, value);
    }

    /// <summary>
    /// Retrieves the XP timeout for a guild asynchronously.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    public async Task<int> GetXpTimeout(ulong id)
    {
        var settings = await guildSettings.GetGuildConfig(id);

        return settings?.XpTxtTimeout ?? 0;
    }

    /// <summary>
    /// Retrieves the text XP rate for a guild asynchronously.
    /// </summary>
    /// <param name="id">The guild ID.</param>

    public async Task<int> GetTxtXpRate(ulong id)
    {
        var settings = await guildSettings.GetGuildConfig(id);

        return settings?.XpTxtRate ?? 0;
    }

    /// <summary>
    /// Retrieves the voice XP rate for a guild asynchronously.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    public async Task<double> GetVoiceXpRate(ulong id)
    {
        var settings = await guildSettings.GetGuildConfig(id);

        return settings?.XpVoiceRate ?? 0;
    }

    /// <summary>
    /// Retrieves the voice XP timeout for a guild asynchronously.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    public async Task<int> GetVoiceXpTimeout(ulong id)
    {
        var settings = await guildSettings.GetGuildConfig(id);

        return settings?.XpVoiceTimeout ?? 0;
    }

    private async void UserLeftVoiceChannel(SocketGuildUser user, SocketGuildChannel channel)
    {
        var key = $"{creds.RedisKey()}_user_xp_vc_join_{user.Id}";
        var cacheItem = await cache.TryGetAsync<DateTimeOffset>(key);
        if (!cacheItem.HasValue)
            return;

        await cache.RemoveAsync(key);
        var minutes = (DateTimeOffset.UtcNow - cacheItem.Value).TotalMinutes;
        var xpRate = await GetVoiceXpRate(user.Guild.Id) == 0 ? xpConfig.Data.VoiceXpPerMinute : await GetVoiceXpRate(user.Guild.Id);
        var xp = xpRate * minutes;
        var actualXp = (int)Math.Floor(xp);

        if (actualXp > 0)
            addMessageXp.Enqueue(new UserCacheItem
            {
                Guild = channel.Guild, User = user, XpAmount = actualXp
            });
    }

    private async Task<bool> IsChannelExcluded(ulong guildId, ulong itemId)
    {
        var settings = await GetOrAddCacheItem($"XpSettings_{guildId}", async () =>
        {
            await using var uow = db.GetDbContext();
            var config = await uow.GuildConfigs.AsNoTracking()
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.RoleRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.CurrencyRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.ExclusionList)
                .FirstOrDefaultAsync(x => x.GuildId == guildId);
            return config?.XpSettings;
        });

        return settings?.ExclusionList.Select(x => x.ItemId).Contains(itemId) ?? false;
    }

    private async Task<bool> ShouldTrackXp(SocketGuildUser user, ulong channelId)
    {
        var settings = await GetOrAddCacheItem($"XpSettings_{user.Guild.Id}", async () =>
        {
            await using var uow = db.GetDbContext();
            var config = await uow.GuildConfigs.AsNoTracking()
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.RoleRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.CurrencyRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.ExclusionList)
                .FirstOrDefaultAsync(x => x.GuildId == user.Guild.Id);
            return config?.XpSettings;
        });

        if (settings?.ExclusionList == null)
            return true;
        if (settings.ExclusionList.Select(x => x.ItemId).Contains(channelId))
            return false;
        if (settings.ExclusionList.Select(x => x.ItemId).Contains(user.Id))
            return false;

        return !user.Roles.Any(i => settings.ExclusionList.Select(x => x.ItemId).Contains(i.Id));
    }

    private async Task Cmd_OnMessageNoTrigger(IUserMessage arg)
    {
        if (arg.Author is not SocketGuildUser user || user.IsBot)
            return;
        if (!await ShouldTrackXp(user, arg.Channel.Id))
            return;

        if (!arg.Content.Contains(' ') && arg.Content.Length < 5)
            return;

        if (!SetUserRewarded(user))
            return;

        var e = await GetTxtXpRate(user.Guild.Id) == 0 ? xpConfig.Data.XpPerMessage : await GetTxtXpRate(user.Guild.Id);
        addMessageXp.Enqueue(new UserCacheItem
        {
            Guild = user.Guild, Channel = arg.Channel, User = user, XpAmount = e
        });
    }

    private bool SetUserRewarded(SocketGuildUser user)
    {
        var key = $"{creds.RedisKey()}_user_xp_gain_{user.Id}";
        var cooldown = TimeSpan.FromMinutes(GetXpTimeout(user.Guild.Id).Result == 0 ? xpConfig.Data.MessageXpCooldown : GetXpTimeout(user.Guild.Id).Result);

        var maybeValue = cache.TryGet<DateTimeOffset>(key);
        if (maybeValue.HasValue && (DateTimeOffset.UtcNow - maybeValue) < cooldown)
            return false;

        cache.Set(key, DateTimeOffset.UtcNow);
        return true;
    }

    /// <summary>
    /// Adds XP directly to a user.
    /// </summary>
    /// <param name="user">The guild user instance.</param>
    /// <param name="channel">The message channel instance.</param>
    /// <param name="amount">The amount of XP to add.</param>
    public void AddXpDirectly(IGuildUser user, IMessageChannel channel, int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

        addMessageXp.Enqueue(new UserCacheItem
        {
            Guild = user.Guild, Channel = channel, User = user, XpAmount = amount
        });
    }

    /// <summary>
    /// Adds XP to a user in a guild asynchronously.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="amount">The amount of XP to add.</param>
    public async Task AddXp(ulong userId, ulong guildId, int amount)
    {
        await using var uow = db.GetDbContext();
        var usr = await uow.UserXpStats.GetOrCreateUser(guildId, userId);

        usr.AwardedXp += amount;

        await uow.SaveChangesAsync().ConfigureAwait(false);
        await cache.RemoveAsync($"XpSettings_{guildId}");
    }

    /// <summary>
    /// Sets the text XP rate for a guild asynchronously.
    /// </summary>
    /// <param name="guild">The guild instance.</param>
    /// <param name="num">The XP rate value.</param>
    public async Task XpTxtRateSet(IGuild guild, int num)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.XpTxtRate = num;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
        await cache.RemoveAsync($"XpSettings_{guild.Id}");
    }

    /// <summary>
    /// Sets the text XP timeout for a guild asynchronously.
    /// </summary>
    /// <param name="guild">The guild instance.</param>
    /// <param name="num">The XP timeout value.</param>
    public async Task XpTxtTimeoutSet(IGuild guild, int num)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.XpTxtTimeout = num;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
        await cache.RemoveAsync($"XpSettings_{guild.Id}");
    }

    /// <summary>
    /// Sets the voice XP rate for a guild asynchronously.
    /// </summary>
    /// <param name="guild">The guild instance.</param>
    /// <param name="num">The XP rate value.</param>
    public async Task XpVoiceRateSet(IGuild guild, int num)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.XpVoiceRate = num;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
        await cache.RemoveAsync($"XpSettings_{guild.Id}");
    }

    /// <summary>
    /// Sets the voice XP timeout for a guild asynchronously.
    /// </summary>
    /// <param name="guild">The guild instance.</param>
    /// <param name="num">The XP timeout value.</param>
    public async Task XpVoiceTimeoutSet(IGuild guild, int num)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.XpVoiceTimeout = num;
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
        await cache.RemoveAsync($"XpSettings_{guild.Id}");
    }

    /// <summary>
    /// Determines whether a server is excluded from XP tracking.
    /// </summary>
    /// <param name="id">The server ID.</param>
    public async Task<bool> IsServerExcluded(ulong id)
    {
        var settings = await GetOrAddCacheItem($"XpSettings_{id}", async () =>
        {
            await using var uow = db.GetDbContext();
            var config = await uow.GuildConfigs.AsNoTracking()
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.RoleRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.CurrencyRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.ExclusionList)
                .FirstOrDefaultAsync(x => x.GuildId == id);
            return config?.XpSettings;
        });

        return settings?.ServerExcluded ?? false;
    }

    /// <summary>
    /// Retrieves the roles excluded from XP tracking for a guild.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    public async Task<IEnumerable<ulong>> GetExcludedRoles(ulong id)
    {
        var settings = await GetOrAddCacheItem($"XpSettings_{id}", async () =>
        {
            await using var uow = db.GetDbContext();
            var config = await uow.GuildConfigs.AsNoTracking()
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.RoleRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.CurrencyRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.ExclusionList)
                .FirstOrDefaultAsync(x => x.GuildId == id);
            return config?.XpSettings;
        });

        return settings?.ExclusionList.Where(x => x.ItemType == ExcludedItemType.Role).Select(x => x.ItemId) ?? Enumerable.Empty<ulong>();
    }

    /// <summary>
    /// Retrieves the channels excluded from XP tracking for a guild.
    /// </summary>
    /// <param name="id">The guild ID.</param>
    public async Task<IEnumerable<ulong>> GetExcludedChannels(ulong id)
    {
        var settings = await GetOrAddCacheItem($"XpSettings_{id}", async () =>
        {
            await using var uow = db.GetDbContext();
            var config = await uow.GuildConfigs.AsNoTracking()
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.RoleRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.CurrencyRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.ExclusionList)
                .FirstOrDefaultAsync(x => x.GuildId == id);
            return config?.XpSettings;
        });

        return settings?.ExclusionList.Where(x => x.ItemType == ExcludedItemType.Channel).Select(x => x.ItemId) ?? Enumerable.Empty<ulong>();
    }

    /// <summary>
    /// Retrieves the user stats for a guild user asynchronously.
    /// </summary>
    /// <param name="user">The guild user instance.</param>
    public async Task<FullUserStats> GetUserStatsAsync(IGuildUser user)
    {
        DiscordUser du;
        UserXpStats stats;
        int guildRank;
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            du = await uow.GetOrCreateUser(user).ConfigureAwait(false);
            guildRank = uow.UserXpStats.GetUserGuildRanking(user.Id, user.GuildId);
            stats = await uow.UserXpStats.GetOrCreateUser(user.GuildId, user.Id);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        return new FullUserStats(du, stats, new LevelStats(stats.Xp + stats.AwardedXp), guildRank);
    }

    /// <summary>
    /// Toggles the exclusion of a server from XP tracking.
    /// </summary>
    /// <param name="id">The server ID.</param>
    public async Task<bool> ToggleExcludeServer(ulong id)
    {
        var settings = await GetOrAddCacheItem($"XpSettings_{id}", async () =>
        {
            await using var uow = db.GetDbContext();
            var config = await uow.GuildConfigs.AsNoTracking()
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.RoleRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.CurrencyRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.ExclusionList)
                .FirstOrDefaultAsync(x => x.GuildId == id);
            return config?.XpSettings;
        });

        if (settings != null)
        {
            settings.ServerExcluded = !settings.ServerExcluded;
            await using var uow = db.GetDbContext();
            uow.Update(settings);
            await uow.SaveChangesAsync();
            await cache.SetAsync($"XpSettings_{id}", settings);
            return settings.ServerExcluded;
        }

        return false;
    }


    /// <summary>
    /// Toggles the exclusion of a role from XP tracking in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="rId">The role ID.</param>
    public async Task<bool> ToggleExcludeRole(ulong guildId, ulong rId)
    {
        var settings = await GetOrAddCacheItem($"XpSettings_{guildId}", async () =>
        {
            await using var uow = db.GetDbContext();
            var config = await uow.GuildConfigs.AsNoTracking()
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.RoleRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.CurrencyRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.ExclusionList)
                .FirstOrDefaultAsync(x => x.GuildId == guildId);
            return config?.XpSettings;
        });

        if (settings != null)
        {
            var excluded = settings.ExclusionList;
            if (excluded.Select(x => x.ItemId).Contains(rId))
            {
                excluded.Remove(excluded.FirstOrDefault(x => x.ItemId == rId));
            }
            else
            {
                excluded.Add(new ExcludedItem
                {
                    ItemId = rId, ItemType = ExcludedItemType.Role
                });
            }

            await using var uow = db.GetDbContext();
            uow.Update(settings);
            await uow.SaveChangesAsync();
            await cache.SetAsync($"XpSettings_{guildId}", settings);
            return excluded.Select(x => x.ItemId).Contains(rId);
        }

        return false;
    }

    /// <summary>
    /// Retrieves the XP image URL for a guild asynchronously.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public async Task<string?> GetXpImage(ulong guildId)
    {
        var settings = await guildSettings.GetGuildConfig(guildId);

        return settings?.XpImgUrl;
    }

    /// <summary>
    /// Toggles the exclusion of a channel from XP tracking in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="chId">The channel ID.</param>
    public async Task<bool> ToggleExcludeChannel(ulong guildId, ulong chId)
    {
        var settings = await GetOrAddCacheItem($"XpSettings_{guildId}", async () =>
        {
            await using var uow = db.GetDbContext();
            var config = await uow.GuildConfigs.AsNoTracking()
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.RoleRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.CurrencyRewards)
                .Include(x => x.XpSettings)
                .ThenInclude(x => x.ExclusionList)
                .FirstOrDefaultAsync(x => x.GuildId == guildId);
            return config?.XpSettings;
        });

        if (settings != null)
        {
            var excluded = settings.ExclusionList;
            if (excluded.Select(x => x.ItemId).Contains(chId))
            {
                excluded.Remove(excluded.FirstOrDefault(x => x.ItemId == chId));
            }
            else
            {
                excluded.Add(new ExcludedItem
                {
                    ItemId = chId, ItemType = ExcludedItemType.Channel
                });
            }

            await using var uow = db.GetDbContext();
            uow.Update(settings);
            await uow.SaveChangesAsync();
            await cache.SetAsync($"XpSettings_{guildId}", settings);
            return excluded.Select(x => x.ItemId).Contains(chId);
        }

        return false;
    }

    /// <summary>
    /// Generates an XP image for a user asynchronously.
    /// </summary>
    /// <param name="user">The guild user instance.</param
    public async Task<Stream> GenerateXpImageAsync(IGuildUser user)
    {
        var stats = await GetUserStatsAsync(user).ConfigureAwait(false);
        var template = await GetTemplate(user.Guild.Id).ConfigureAwait(false);
        return await GenerateXpImageAsync(stats, template).ConfigureAwait(false);
    }

    private async Task<Stream> GenerateXpImageAsync(FullUserStats stats, Template template)
    {
        await using var xpstream = new MemoryStream();
        var xpImage = await GetXpImage(stats.FullGuildStats.GuildId);
        if (xpImage is not null)
        {
            using var httpClient = new HttpClient();
            var httpResponse = await httpClient.GetAsync(xpImage);
            if (httpResponse.IsSuccessStatusCode)
            {
                await httpResponse.Content.CopyToAsync(xpstream);
                xpstream.Position = 0;
            }
        }
        else
        {
            await xpstream.WriteAsync(images.XpBackground.AsMemory(0, images.XpBackground.Length));
            xpstream.Position = 0;
        }

        var imgData = SKData.Create(xpstream);
        var img = SKBitmap.Decode(imgData);
        var canvas = new SKCanvas(img);

        var textPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Fill,
        };

        if (template.TemplateUser.ShowText)
        {
            var color = SKColor.Parse(template.TemplateUser.TextColor);
            textPaint.Color = color;
            textPaint.TextSize = template.TemplateUser.FontSize;
            textPaint.Typeface = SKTypeface.FromFamilyName("NotoSans", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
            var username = stats.User.Username;
            canvas.DrawText(username, template.TemplateUser.TextX, template.TemplateUser.TextY, textPaint);
        }

        if (template.TemplateGuild.ShowGuildLevel)
        {
            textPaint.TextSize = template.TemplateGuild.GuildLevelFontSize;
            var color = SKColor.Parse(template.TemplateGuild.GuildLevelColor);
            textPaint.Color = color;
            canvas.DrawText(stats.Guild.Level.ToString(), template.TemplateGuild.GuildLevelX, template.TemplateGuild.GuildLevelY, textPaint);
        }

        var guild = stats.Guild;

        if (template.TemplateBar.ShowBar)
        {
            var xpPercent = guild.LevelXp / (float)guild.RequiredXp;
            DrawXpBar(xpPercent, template.TemplateBar, canvas);
        }

        if (stats.FullGuildStats.AwardedXp != 0 && template.ShowAwarded)
        {
            var sign = stats.FullGuildStats.AwardedXp > 0 ? "+ " : "";
            textPaint.TextSize = template.AwardedFontSize;
            var color = SKColor.Parse(template.AwardedColor);
            textPaint.Color = color;
            var text = $"({sign}{stats.FullGuildStats.AwardedXp})";
            canvas.DrawText(text, template.AwardedX, template.AwardedY, textPaint);
        }

        if (template.TemplateGuild.ShowGuildRank)
        {
            textPaint.TextSize = template.TemplateGuild.GuildRankFontSize;
            var color = SKColor.Parse(template.TemplateGuild.GuildRankColor);
            textPaint.Color = color;
            canvas.DrawText(stats.GuildRanking.ToString(), template.TemplateGuild.GuildRankX, template.TemplateGuild.GuildRankY, textPaint);
        }

        if (template.ShowTimeOnLevel)
        {
            textPaint.TextSize = template.TimeOnLevelFontSize;
            var color = SKColor.Parse(template.TimeOnLevelColor);
            textPaint.Color = color;
            var text = GetTimeSpent(stats.FullGuildStats.LastLevelUp);
            canvas.DrawText(text, template.TimeOnLevelX, template.TimeOnLevelY, textPaint);
        }

        if (stats.User.AvatarId != null && template.TemplateUser.ShowIcon)
        {
            try
            {
                var avatarUrl = stats.User.RealAvatarUrl();

                using var httpClient = new HttpClient();
                var httpResponse = await httpClient.GetAsync(avatarUrl);
                if (httpResponse.IsSuccessStatusCode)
                {
                    var avatarData = await httpResponse.Content.ReadAsByteArrayAsync();
                    await using var avatarStream = new MemoryStream(avatarData);
                    var avatarImgData = SKData.Create(avatarStream);
                    var avatarImg = SKBitmap.Decode(avatarImgData);

                    var resizedAvatar = avatarImg.Resize(new SKImageInfo(template.TemplateUser.IconSizeX, template.TemplateUser.IconSizeY), SKFilterQuality.High);
                    var roundedAvatar = ApplyRoundedCorners(resizedAvatar, template.TemplateUser.IconSizeX / 2);
                    canvas.DrawImage(roundedAvatar, template.TemplateUser.IconX, template.TemplateUser.IconY);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error drawing avatar image: {ex.Message}");
            }
        }

        var image = SKImage.FromBitmap(img);
        var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var stream = data.AsStream();
        return stream;
    }

    private static SKImage ApplyRoundedCorners(SKBitmap src, float cornerRadius)
    {
        var width = src.Width;
        var height = src.Height;
        var info = new SKImageInfo(width, height);
        var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        var paint = new SKPaint
        {
            IsAntialias = true,
        };

        var rect = SKRect.Create(width, height);
        var rrect = new SKRoundRect(rect, cornerRadius, cornerRadius);

        canvas.Clear(SKColors.Transparent);
        canvas.ClipRoundRect(rrect, antialias: true);
        canvas.DrawBitmap(src, 0, 0, paint);

        return surface.Snapshot();
    }

    private void DrawXpBar(float percent, TemplateBar info, SKCanvas canvas)
    {
        var x1 = info.BarPointAx;
        var y1 = info.BarPointAy;

        var x2 = info.BarPointBx;
        var y2 = info.BarPointBy;

        var length = info.BarLength * percent;

        float x3, x4, y3, y4;

        switch (info.BarDirection)
        {
            case XpTemplateDirection.Down:
                x3 = x1;
                x4 = x2;
                y3 = y1 + length;
                y4 = y2 + length;
                break;
            case XpTemplateDirection.Up:
                x3 = x1;
                x4 = x2;
                y3 = y1 - length;
                y4 = y2 - length;
                break;
            case XpTemplateDirection.Left:
                x3 = x1 - length;
                x4 = x2 - length;
                y3 = y1;
                y4 = y2;
                break;
            default:
                x3 = x1 + length;
                x4 = x2 + length;
                y3 = y1;
                y4 = y2;
                break;
        }

        using var path = new SKPath();
        path.MoveTo(x1, y1);
        path.LineTo(x3, y3);
        path.LineTo(x4, y4);
        path.LineTo(x2, y2);
        path.Close();

        using var paint = new SKPaint();
        paint.Style = SKPaintStyle.Fill;
        var color = SKColor.Parse(info.BarColor);
        paint.Color = new SKColor(color.Red, color.Green, color.Green, info.BarTransparency);
        canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Retrieves the template for a guild asynchronously.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public async Task<Template> GetTemplate(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var template = uow.Templates
            .Include(x => x.TemplateUser)
            .Include(x => x.TemplateBar)
            .Include(x => x.TemplateClub)
            .Include(x => x.TemplateGuild)
            .FirstOrDefault(x => x.GuildId == guildId);

        if (template != null) return template;
        var toAdd = new Template
        {
            GuildId = guildId,
            TemplateBar = new TemplateBar(),
            TemplateClub = new TemplateClub(),
            TemplateGuild = new TemplateGuild(),
            TemplateUser = new TemplateUser()
        };
        uow.Templates.Add(toAdd);
        await uow.SaveChangesAsync();
        return uow.Templates.FirstOrDefault(x => x.GuildId == guildId);
    }

    private string GetTimeSpent(DateTime time)
    {
        var offset = DateTime.UtcNow - time;
        return $"{offset.Humanize()} ago";
    }

    /// <summary>
    /// Resets the XP for a user in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    public void XpReset(ulong guildId, ulong userId)
    {
        using var uow = db.GetDbContext();
        uow.UserXpStats.ResetGuildUserXp(userId, guildId);
        uow.SaveChanges();
    }

    /// <summary>
    /// Resets the XP for all users in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public void XpReset(ulong guildId)
    {
        using var uow = db.GetDbContext();
        uow.UserXpStats.ResetGuildXp(guildId);
        uow.SaveChanges();
    }

    /// <summary>
    /// Sets the XP image URL for a guild asynchronously.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="imageUrl">The image URL.</param>
    public async Task SetImageUrl(ulong guildId, string imageUrl)
    {
        await using var uow = db.GetDbContext();
        var set = await uow.ForGuildId(guildId);
        set.XpImgUrl = imageUrl;
        uow.GuildConfigs.Update(set);
        await uow.SaveChangesAsync();
        await guildSettings.UpdateGuildConfig(guildId, set);
    }

    /// <summary>
    /// Validates an image URL asynchronously.
    /// </summary>
    /// <param name="url">The image URL.</param>
    public static async Task<(string, bool)> ValidateImageUrl(string url)
    {
        if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            return ("Malformed URL", false);

        var formatAllowed = url.EndsWith(".png") || url.EndsWith(".jpg");
        if (!formatAllowed)
            return ("Must end with png or jpg", false);

        using var httpClient = new HttpClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Head, url);

        try
        {
            var response = await httpClient.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                return ("Url provided was unreachable", false);
            }

            var contentLength = response.Content.Headers.ContentLength;
            var contentLengthMb = contentLength / (1024 * 1024);
            return ("File is over 20MB", !(contentLengthMb > 20));
        }
        catch
        {
            return ("An unknown error occured while attempting to fetch the image", false);
        }
    }

    private enum NotifOf
    {
        Server,
        Global
    }
}