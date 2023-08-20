using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using Humanizer;
using Mewdeko.Common.Collections;
using Mewdeko.Modules.Xp.Common;
using Mewdeko.Services.Impl;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using SkiaSharp;
using StackExchange.Redis;

namespace Mewdeko.Modules.Xp.Services;

public class XpService : INService, IUnloadableService
{
    public const int XpRequiredLvl1 = 36;

    private readonly ConcurrentQueue<UserCacheItem> addMessageXp = new();

    private readonly IDataCache cache;
    private readonly DiscordSocketClient client;
    private readonly CommandHandler cmd;
    private readonly IBotCredentials creds;
    private readonly EventHandler eventHandler;

    private readonly DbService db;

    private readonly NonBlocking.ConcurrentDictionary<ulong, ConcurrentHashSet<ulong>> excludedChannels;

    private readonly NonBlocking.ConcurrentDictionary<ulong, ConcurrentHashSet<ulong>> excludedRoles;

    private readonly ConcurrentHashSet<ulong> excludedServers;
    private readonly FontProvider fonts;
    private readonly IHttpClientFactory httpFactory;
    private readonly IImageCache images;
    private readonly IBotStrings strings;
    private readonly XpConfigService xpConfig;
    private readonly Mewdeko bot;
    private readonly IMemoryCache memoryCache;


    public XpService(
        DiscordSocketClient client,
        CommandHandler cmd,
        DbService db,
        IBotStrings strings,
        IDataCache cache,
        FontProvider fonts,
        IBotCredentials creds,
        IHttpClientFactory http,
        XpConfigService xpConfig,
        Mewdeko bot,
        IMemoryCache memoryCache, EventHandler eventHandler)
    {
        this.db = db;
        this.cmd = cmd;
        images = cache.LocalImages;
        this.strings = strings;
        this.cache = cache;
        this.fonts = fonts;
        this.creds = creds;
        httpFactory = http;
        this.xpConfig = xpConfig;
        this.bot = bot;
        this.memoryCache = memoryCache;
        this.eventHandler = eventHandler;
        excludedServers = new ConcurrentHashSet<ulong>();
        excludedChannels = new NonBlocking.ConcurrentDictionary<ulong, ConcurrentHashSet<ulong>>();
        this.client = client;


        //load settings
        var allGuildConfigs = bot.AllGuildConfigs;
        XpTxtRates = allGuildConfigs.ToDictionary(x => x.GuildId, x => x.XpTxtRate).ToConcurrent();
        XpTxtTimeouts = allGuildConfigs.ToDictionary(x => x.GuildId, x => x.XpTxtTimeout).ToConcurrent();
        XpVoiceRates = allGuildConfigs.ToDictionary(x => x.GuildId, x => x.XpVoiceRate).ToConcurrent();
        XpVoiceTimeouts = allGuildConfigs.ToDictionary(x => x.GuildId, x => x.XpVoiceTimeout).ToConcurrent();
        excludedChannels = allGuildConfigs.Where(x => x.XpSettings?.ExclusionList.Count > 0).ToDictionary(x => x.GuildId,
            x => new ConcurrentHashSet<ulong>(x.XpSettings?.ExclusionList
                .Where(ex => ex.ItemType == ExcludedItemType.Channel)
                .Select(ex => ex.ItemId).Distinct())).ToConcurrent();

        excludedRoles = allGuildConfigs.Where(x => x.XpSettings?.ExclusionList.Count > 0).ToDictionary(x => x.GuildId,
            x => new ConcurrentHashSet<ulong>(x.XpSettings?.ExclusionList
                .Where(ex => ex.ItemType == ExcludedItemType.Role)
                .Select(ex => ex.ItemId).Distinct())).ToConcurrent();

        excludedServers = new ConcurrentHashSet<ulong>(
            allGuildConfigs.Where(x => x.XpSettings?.ServerExcluded == 1).Select(x => x.GuildId));

        this.cmd.OnMessageNoTrigger += Cmd_OnMessageNoTrigger;

#if !GLOBAL_Mewdeko
        eventHandler.UserVoiceStateUpdated += Client_OnUserVoiceStateUpdated;

        // Scan guilds on startup.
        this.client.GuildAvailable += Client_OnGuildAvailable;
        foreach (var guild in this.client.Guilds) Client_OnGuildAvailable(guild);
#endif
        _ = Task.Run(UpdateLoop);
    }

    private NonBlocking.ConcurrentDictionary<ulong, int> XpTxtRates { get; }
    private NonBlocking.ConcurrentDictionary<ulong, int> XpVoiceRates { get; }
    private NonBlocking.ConcurrentDictionary<ulong, int> XpTxtTimeouts { get; }
    private NonBlocking.ConcurrentDictionary<ulong, int> XpVoiceTimeouts { get; }

    public Task Unload()
    {
        cmd.OnMessageNoTrigger -= Cmd_OnMessageNoTrigger;
        eventHandler.UserVoiceStateUpdated -= Client_OnUserVoiceStateUpdated;
        client.GuildAvailable -= Client_OnGuildAvailable;
        return Task.CompletedTask;
    }

    private async Task UpdateLoop()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            try
            {
                var toNotify =
                    new List<(IGuild Guild, IMessageChannel? MessageChannel, IUser User, int Level,
                        XpNotificationLocation NotifyType, NotifOf NotifOf)>();
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

                        //1. Mass query discord users and userxpstats and get them from local dict
                        //2. (better but much harder) Move everything to the database, and get old and new xp
                        // amounts for every user (in order to give rewards)

                        var usr = await uow.UserXpStats.GetOrCreateUser(item.Key.GuildId, item.Key.User.Id);
                        var du = await uow.GetOrCreateUser(item.Key.User).ConfigureAwait(false);

                        var globalXp = du.TotalXp;
                        var oldGlobalLevelData = new LevelStats(globalXp);
                        var newGlobalLevelData = new LevelStats(globalXp + xp);

                        var oldGuildLevelData = new LevelStats(usr.Xp + usr.AwardedXp);
                        usr.Xp += xp;
                        du.TotalXp += xp;
                        if (du.Club != null) du.Club.Xp += xp;
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
                            //send level up notification
                            var first = item.First();
                            if (usr.NotifyOnLevelUp != XpNotificationLocation.None)
                            {
                                toNotify.Add((first.Guild, first.Channel, first.User, newGuildLevelData.Level,
                                    usr.NotifyOnLevelUp, NotifOf.Server));
                            }

                            //give role
                            if (!roleRewards.TryGetValue(usr.GuildId, out var rrews))
                            {
                                rrews = (await uow.XpSettingsFor(usr.GuildId)).RoleRewards.ToList();
                                roleRewards.Add(usr.GuildId, rrews);
                            }

                            if (!curRewards.TryGetValue(usr.GuildId, out var crews))
                            {
                                crews = (await uow.XpSettingsFor(usr.GuildId)).CurrencyRewards.ToList();
                                curRewards.Add(usr.GuildId, crews);
                            }

                            for (var i = oldGuildLevelData.Level + 1; i <= newGuildLevelData.Level; i++)
                            {
                                var rrew = rrews.Find(x => x.Level == i);
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
                                    Format.Bold(x.Level.ToString()), Format.Bold(x.Guild.ToString() ?? "-"))).ConfigureAwait(false);
                            }
                        }
                        else if (x.MessageChannel != null) // channel
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
                        else // channel
                            chan = x.MessageChannel;

                        await chan.SendConfirmAsync(strings.GetText("level_up_global", x.Guild.Id, x.User.Mention,
                            Format.Bold(x.Level.ToString()))).ConfigureAwait(false);
                    }
                })).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error In the XP update loop");
            }
        }
    }


    public async void SetCurrencyReward(ulong guildId, int level, int amount)
    {
        await using var uow = db.GetDbContext();
        var settings = await uow.XpSettingsFor(guildId);

        if (amount <= 0)
        {
            var toRemove = settings.CurrencyRewards.FirstOrDefault(x => x.Level == level);
            if (toRemove != null)
            {
                uow.Remove(toRemove);
                settings.CurrencyRewards.Remove(toRemove);
            }
        }
        else
        {
            var rew = settings.CurrencyRewards.FirstOrDefault(x => x.Level == level);

            if (rew != null)
                rew.Amount = amount;
            else
                settings.CurrencyRewards.Add(new XpCurrencyReward
                {
                    Level = level, Amount = amount
                });
        }

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<IEnumerable<XpCurrencyReward>> GetCurrencyRewards(ulong id)
    {
        await using var uow = db.GetDbContext();
        return (await uow.XpSettingsFor(id)).CurrencyRewards.ToArray();
    }

    public async Task<IEnumerable<XpRoleReward>> GetRoleRewards(ulong id)
    {
        await using var uow = db.GetDbContext();
        return (await uow.XpSettingsFor(id)).RoleRewards.ToArray();
    }

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
    }

    public async Task<List<UserXpStats>> GetUserXps(ulong guildId, int page)
    {
        await using var uow = db.GetDbContext();
        return await uow.UserXpStats.GetUsersFor(guildId, page);
    }

    public async Task<List<UserXpStats>> GetTopUserXps(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        return await uow.UserXpStats.GetTopUserXps(guildId);
    }

    public DiscordUser[] GetUserXps(int page)
    {
        using var uow = db.GetDbContext();
        return uow.DiscordUser.GetUsersXpLeaderboardFor(page);
    }

    public async Task ChangeNotificationType(ulong userId, ulong guildId, XpNotificationLocation type)
    {
        await using var uow = db.GetDbContext();
        var user = await uow.UserXpStats.GetOrCreateUser(guildId, userId);
        user.NotifyOnLevelUp = type;
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<XpNotificationLocation> GetNotificationType(ulong userId, ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var user = await uow.UserXpStats.GetOrCreateUser(guildId, userId);
        return user.NotifyOnLevelUp;
    }

    public async Task<XpNotificationLocation> GetNotificationType(IUser user)
    {
        await using var uow = db.GetDbContext();
        return (await uow.GetOrCreateUser(user).ConfigureAwait(false)).NotifyOnLevelUp;
    }

    public async Task ChangeNotificationType(IUser user, XpNotificationLocation type)
    {
        await using var uow = db.GetDbContext();
        var du = await uow.GetOrCreateUser(user).ConfigureAwait(false);
        du.NotifyOnLevelUp = type;
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    private Task Client_OnGuildAvailable(SocketGuild guild)
    {
        Task.Run(() =>
        {
            foreach (var channel in guild.VoiceChannels) ScanChannelForVoiceXp(channel);
        });

        return Task.CompletedTask;
    }

    private Task Client_OnUserVoiceStateUpdated(SocketUser socketUser, SocketVoiceState before, SocketVoiceState after)
    {
        _ = Task.Run(() =>
        {
            if (socketUser is not SocketGuildUser user || user.IsBot)
                return;
            var vcxp = GetVoiceXpRate(user.Guild.Id);
            var vctime = GetVoiceXpTimeout(user.Guild.Id);
            if (vctime is 0)
                return;
            if (vcxp is 0)
                return;
            if (!bot.Ready.Task.IsCompleted)
                return;
            if (before.VoiceChannel != null) ScanChannelForVoiceXp(before.VoiceChannel);

            if (after.VoiceChannel != null && after.VoiceChannel != before.VoiceChannel)
            {
                ScanChannelForVoiceXp(after.VoiceChannel);
            }
            else if (after.VoiceChannel == null)
            {
                // In this case, the user left the channel and the previous for loops didn't catch
                // it because it wasn't in any new channel. So we need to get rid of it.
                UserLeftVoiceChannel(user, before.VoiceChannel);
            }
        });

        return Task.CompletedTask;
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

    /// <summary>
    ///     Assumes that the channel itself is valid and adding xp.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="channel"></param>
    private void ScanUserForVoiceXp(SocketGuildUser user, SocketGuildChannel channel)
    {
        if (UserParticipatingInVoiceChannel(user) && ShouldTrackXp(user, channel.Id))
            UserJoinedVoiceChannel(user);
        else
            UserLeftVoiceChannel(user, channel);
    }

    private static bool ShouldTrackVoiceChannel(SocketGuildChannel channel) =>
        channel.Users.Where(x => !x.IsBot && UserParticipatingInVoiceChannel(x)).Take(2).Count() >= 2;

    private static bool UserParticipatingInVoiceChannel(IVoiceState user) =>
        !user.IsDeafened && !user.IsMuted && !user.IsSelfDeafened && !user.IsSelfMuted;

    private void UserJoinedVoiceChannel(SocketGuildUser user)
    {
        var key = $"{creds.RedisKey()}_user_xp_vc_join_{user.Id}";
        var value = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var e = GetVoiceXpTimeout(user.Guild.Id) == 0 ? xpConfig.Data.VoiceMaxMinutes : GetVoiceXpTimeout(user.Guild.Id);
        if (memoryCache.Get(key) is not null)
            return;
        memoryCache.Set(key, value, TimeSpan.FromMinutes(e));
    }

    public int GetXpTimeout(ulong id)
    {
        XpTxtTimeouts.TryGetValue(id, out var snum);
        return snum;
    }

    public int GetTxtXpRate(ulong id)
    {
        XpTxtRates.TryGetValue(id, out var snum);
        return snum;
    }

    public double GetVoiceXpRate(ulong id)
    {
        XpVoiceRates.TryGetValue(id, out var snum);
        return snum;
    }

    public int GetVoiceXpTimeout(ulong id)
    {
        XpVoiceTimeouts.TryGetValue(id, out var snum);
        return snum;
    }

    private void UserLeftVoiceChannel(SocketGuildUser user, SocketGuildChannel channel)
    {
        var key = $"{creds.RedisKey()}_user_xp_vc_join_{user.Id}";
        var value = memoryCache.Get(key);
        memoryCache.Remove(key);

        // Allow for if this function gets called multiple times when a user leaves a channel.
        if (value is null) return;

        if (value is not long startUnixTime)
            return;

        var dateStart = DateTimeOffset.FromUnixTimeSeconds(startUnixTime);
        var dateEnd = DateTimeOffset.UtcNow;
        var minutes = (dateEnd - dateStart).TotalMinutes;
        var ten = GetVoiceXpRate(user.Guild.Id) == 0 ? xpConfig.Data.VoiceXpPerMinute : GetVoiceXpRate(user.Guild.Id);
        var xp = ten * minutes;
        var actualXp = (int)Math.Floor(xp);

        if (actualXp > 0)
            addMessageXp.Enqueue(new UserCacheItem
            {
                Guild = channel.Guild, User = user, XpAmount = actualXp
            });
    }

    private bool ShouldTrackXp(SocketGuildUser user, ulong channelId)
    {
        if (excludedChannels.TryGetValue(user.Guild.Id, out var chans) && chans.Contains(channelId)) return false;

        if (excludedServers.Contains(user.Guild.Id)) return false;

        return !excludedRoles.TryGetValue(user.Guild.Id, out var roles) || !user.Roles.Any(x => roles.Contains(x.Id));
    }

    private Task Cmd_OnMessageNoTrigger(IUserMessage arg)
    {
        if (arg.Author is not SocketGuildUser user || user.IsBot)
            return Task.CompletedTask;

        _ = Task.Run(() =>
        {
            if (!ShouldTrackXp(user, arg.Channel.Id))
                return;

            if (!arg.Content.Contains(' ') && arg.Content.Length < 5)
                return;

            if (!SetUserRewarded(user))
                return;
            var e = GetTxtXpRate(user.Guild.Id) == 0 ? xpConfig.Data.XpPerMessage : GetTxtXpRate(user.Guild.Id);
            addMessageXp.Enqueue(new UserCacheItem
            {
                Guild = user.Guild, Channel = arg.Channel, User = user, XpAmount = e
            });
        });
        return Task.CompletedTask;
    }

    public void AddXpDirectly(IGuildUser user, IMessageChannel channel, int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

        addMessageXp.Enqueue(new UserCacheItem
        {
            Guild = user.Guild, Channel = channel, User = user, XpAmount = amount
        });
    }

    public async Task AddXp(ulong userId, ulong guildId, int amount)
    {
        await using var uow = db.GetDbContext();
        var usr = await uow.UserXpStats.GetOrCreateUser(guildId, userId);

        usr.AwardedXp += amount;

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task XpTxtRateSet(IGuild guild, int num)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.XpTxtRate = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        XpTxtRates.AddOrUpdate(guild.Id, num, (_, _) => num);
    }

    public async Task XpTxtTimeoutSet(IGuild guild, int num)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.XpTxtTimeout = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        XpTxtTimeouts.AddOrUpdate(guild.Id, num, (_, _) => num);
    }

    public async Task XpVoiceRateSet(IGuild guild, int num)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.XpVoiceRate = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        XpVoiceRates.AddOrUpdate(guild.Id, num, (_, _) => num);
    }

    public async Task XpVoiceTimeoutSet(IGuild guild, int num)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.XpVoiceTimeout = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);

        XpVoiceTimeouts.AddOrUpdate(guild.Id, num, (_, _) => num);
    }

    public bool IsServerExcluded(ulong id) => excludedServers.Contains(id);

    public IEnumerable<ulong> GetExcludedRoles(ulong id) =>
        excludedRoles.TryGetValue(id, out var val) ? val.ToArray() : Enumerable.Empty<ulong>();

    public IEnumerable<ulong> GetExcludedChannels(ulong id) => excludedChannels.TryGetValue(id, out var val)
        ? val.ToArray()
        : Enumerable.Empty<ulong>();

    private bool SetUserRewarded(SocketGuildUser userId)
    {
        var r = cache.Redis.GetDatabase();
        var key = $"{creds.RedisKey()}_user_xp_gain_{userId.Id}";
        var e = GetXpTimeout(userId.Guild.Id) == 0 ? xpConfig.Data.MessageXpCooldown : GetXpTimeout(userId.Guild.Id);
        return r.StringSet(key, true, TimeSpan.FromMinutes(e), when: When.NotExists);
    }

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

        return new FullUserStats(du, stats, new LevelStats(stats.Xp + stats.AwardedXp),
            guildRank);
    }

    public static (int Level, int LevelXp, int LevelRequiredXp) GetLevelData(UserXpStats stats)
    {
        int required;
        var totalXp = 0;
        var lvl = 1;
        while (true)
        {
            required = (int)(XpRequiredLvl1 + (XpRequiredLvl1 / 4.0 * (lvl - 1)));

            if (required + totalXp > stats.Xp)
                break;

            totalXp += required;
            lvl++;
        }

        return (lvl - 1, stats.Xp - totalXp, required);
    }

    public async Task<bool> ToggleExcludeServer(ulong id)
    {
        await using var uow = db.GetDbContext();
        var xpSetting = await uow.XpSettingsFor(id);
        if (excludedServers.Add(id))
        {
            xpSetting.ServerExcluded = 1;
            await uow.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        excludedServers.TryRemove(id);
        xpSetting.ServerExcluded = 0;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        return false;
    }

    public async Task<bool> ToggleExcludeRole(ulong guildId, ulong rId)
    {
        var roles = excludedRoles.GetOrAdd(guildId, _ => new ConcurrentHashSet<ulong>());
        await using var uow = db.GetDbContext();
        var xpSetting = await uow.XpSettingsFor(guildId);
        var excludeObj = new ExcludedItem
        {
            ItemId = rId, ItemType = ExcludedItemType.Role
        };

        if (roles.Add(rId))
        {
            if (xpSetting.ExclusionList.Add(excludeObj)) await uow.SaveChangesAsync().ConfigureAwait(false);

            return true;
        }

        roles.TryRemove(rId);

        var toDelete = xpSetting.ExclusionList.FirstOrDefault(x => x.Equals(excludeObj));
        if (toDelete == null) return false;
        uow.Remove(toDelete);
        await uow.SaveChangesAsync().ConfigureAwait(false);

        return false;
    }

    public async Task<bool> ToggleExcludeChannel(ulong guildId, ulong chId)
    {
        var channels = excludedChannels.GetOrAdd(guildId, _ => new ConcurrentHashSet<ulong>());
        await using var uow = db.GetDbContext();
        var xpSetting = await uow.XpSettingsFor(guildId);
        var excludeObj = new ExcludedItem
        {
            ItemId = chId, ItemType = ExcludedItemType.Channel
        };

        if (channels.Add(chId))
        {
            if (xpSetting.ExclusionList.Add(excludeObj)) await uow.SaveChangesAsync().ConfigureAwait(false);

            return true;
        }

        channels.TryRemove(chId);

        if (xpSetting.ExclusionList.Remove(excludeObj)) await uow.SaveChangesAsync().ConfigureAwait(false);

        return false;
    }

    public async Task<Stream> GenerateXpImageAsync(IGuildUser user)
    {
        var stats = await GetUserStatsAsync(user).ConfigureAwait(false);
        var template = await GetTemplate(user.Guild.Id).ConfigureAwait(false);
        return await GenerateXpImageAsync(stats, template).ConfigureAwait(false);
    }

    private async Task<Stream> GenerateXpImageAsync(FullUserStats stats, Template template)
    {
        // Load the background image
        await using var xpstream = new MemoryStream(images.XpBackground);
        var imgData = SKData.Create(xpstream);
        var img = SKBitmap.Decode(imgData);
        var canvas = new SKCanvas(img);

        var textPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Fill,
        };

        // Draw the username
        if (template.TemplateUser.ShowText)
        {
            var color = SKColor.Parse(template.TemplateUser.TextColor);
            textPaint.Color = color;
            textPaint.TextSize = template.TemplateUser.FontSize;
            textPaint.Typeface = SKTypeface.FromFamilyName("NotoSans", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
            var username = stats.User.Username;
            canvas.DrawText(username, template.TemplateUser.TextX, template.TemplateUser.TextY, textPaint);
        }

        // Draw the guild level
        if (template.TemplateGuild.ShowGuildLevel)
        {
            textPaint.TextSize = template.TemplateGuild.GuildLevelFontSize;
            var color = SKColor.Parse(template.TemplateGuild.GuildLevelColor);
            textPaint.Color = color;
            canvas.DrawText(stats.Guild.Level.ToString(), template.TemplateGuild.GuildLevelX, template.TemplateGuild.GuildLevelY, textPaint);
        }

        var guild = stats.Guild;

        // Draw the XP bar
        if (template.TemplateBar.ShowBar)
        {
            var xpPercent = guild.LevelXp / (float)guild.RequiredXp;
            DrawXpBar(xpPercent, template.TemplateBar, canvas);
        }

        // Draw awarded XP
        if (stats.FullGuildStats.AwardedXp != 0 && template.ShowAwarded)
        {
            var sign = stats.FullGuildStats.AwardedXp > 0 ? "+ " : "";
            textPaint.TextSize = template.AwardedFontSize;
            var color = SKColor.Parse(template.AwardedColor);
            textPaint.Color = color;
            var text = $"({sign}{stats.FullGuildStats.AwardedXp})";
            canvas.DrawText(text, template.AwardedX, template.AwardedY, textPaint);
        }

        // Draw guild rank
        if (template.TemplateGuild.ShowGuildRank)
        {
            textPaint.TextSize = template.TemplateGuild.GuildRankFontSize;
            var color = SKColor.Parse(template.TemplateGuild.GuildRankColor);
            textPaint.Color = color;
            canvas.DrawText(stats.GuildRanking.ToString(), template.TemplateGuild.GuildRankX, template.TemplateGuild.GuildRankY, textPaint);
        }

        // Draw time on level
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

                    // resize the avatar
                    var resizedAvatar = avatarImg.Resize(new SKImageInfo(template.TemplateUser.IconSizeX, template.TemplateUser.IconSizeY), SKFilterQuality.High);

                    // apply rounded corners
                    var roundedAvatar = ApplyRoundedCorners(resizedAvatar, template.TemplateUser.IconSizeX / 2);

                    // draw the avatar onto the main image canvas
                    canvas.DrawImage(roundedAvatar, template.TemplateUser.IconX, template.TemplateUser.IconY);
                }
            }
            catch (Exception ex)
            {
                // Log error or handle it appropriately
                Console.WriteLine($"Error drawing avatar image: {ex.Message}");
            }
        }

        // Convert to Stream and return
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

        // Clip the canvas to the round rectangle
        canvas.ClipRoundRect(rrect, antialias: true);

        // Now draw the bitmap, it will only be drawn where the canvas is not clipped
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

    public void XpReset(ulong guildId, ulong userId)
    {
        using var uow = db.GetDbContext();
        uow.UserXpStats.ResetGuildUserXp(userId, guildId);
        uow.SaveChanges();
    }

    public void XpReset(ulong guildId)
    {
        using var uow = db.GetDbContext();
        uow.UserXpStats.ResetGuildXp(guildId);
        uow.SaveChanges();
    }

    private enum NotifOf
    {
        Server,
        Global
    } // is it a server level-up or global level-up notification
}