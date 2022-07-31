using Humanizer;
using Mewdeko.Common.ModuleBehaviors;
using Serilog;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Afk.Services;

public class AfkService : INService, IReadyExecutor
{
    private readonly DbService _db;
    private readonly DiscordSocketClient _client;
    private readonly Mewdeko _bot;
    private readonly IDataCache _cache;
    private readonly GuildSettingsService _guildSettings;

    public AfkService(
        DbService db,
        DiscordSocketClient client,
        Mewdeko bot,
        IDataCache cache,
        GuildSettingsService guildSettings)
    {
        _bot = bot;
        _cache = cache;
        _guildSettings = guildSettings;
        _db = db;
        _client = client;
        _client.MessageReceived += MessageReceived;
        _client.MessageUpdated += MessageUpdated;
        _client.UserIsTyping += UserTyping;
    }

    public async Task OnReadyAsync()
    {
        await using var uow = _db.GetDbContext();
        var allafk = uow.Afk.Where(x => _client.Guilds.Select(x => x.Id).Contains(x.GuildId));
        foreach (var i in _client.Guilds.Select(x => x.Id))
        {
            await _cache.CacheAfk(i, allafk.Where(x => x.GuildId == i).ToList()).ConfigureAwait(false);
        }

        Environment.SetEnvironmentVariable($"AFK_CACHED_{_client.ShardId}", "1");
        Log.Information("AFK Cached.");
    }

    private Task UserTyping(Cacheable<IUser, ulong> user, Cacheable<IMessageChannel, ulong> chan)
    {
        _ = Task.Factory.StartNew(async () =>
        {
            if (user.Value is IGuildUser use)
            {
                if (await GetAfkType(use.GuildId) is 2 or 4)
                    if (IsAfk(use.Guild, use))
                    {
                        var t = GetAfkMessage(use.Guild.Id, user.Id).Last();
                        if (t.DateAdded != null && t.DateAdded.Value.ToLocalTime() < DateTime.Now.AddSeconds(-await GetAfkTimeout(use.GuildId)) && t.WasTimed == 0)
                        {
                            await AfkSet(use.Guild, use, "", 0).ConfigureAwait(false);
                            var msg = await chan.Value.SendMessageAsync($"Welcome back {user.Value.Mention}! I noticed you typing so I disabled your afk.").ConfigureAwait(false);
                            try
                            {
                                await use.ModifyAsync(x => x.Nickname = use.Nickname.Replace("[AFK]", "")).ConfigureAwait(false);
                            }
                            catch
                            {
                                //ignored
                            }

                            msg.DeleteAfter(5);
                        }
                    }
            }
        }, TaskCreationOptions.LongRunning);
        return Task.CompletedTask;
    }

    private Task MessageReceived(SocketMessage msg)
    {
        _ = Task.Factory.StartNew(async () =>
        {
            if (msg.Author.IsBot)
                return;

            if (msg.Author is IGuildUser user)
            {
                if (await GetAfkType(user.Guild.Id) is 3 or 4)
                {
                    if (IsAfk(user.Guild, user))
                    {
                        var t = GetAfkMessage(user.Guild.Id, user.Id).Last();
                        if (t.DateAdded != null && t.DateAdded.Value.ToLocalTime() < DateTime.Now.AddSeconds(-await GetAfkTimeout(user.GuildId)) && t.WasTimed == 0)
                        {
                            await AfkSet(user.Guild, user, "", 0).ConfigureAwait(false);
                            var ms = await msg.Channel.SendMessageAsync($"Welcome back {user.Mention}, I have disabled your AFK for you.").ConfigureAwait(false);
                            ms.DeleteAfter(5);
                            try
                            {
                                await user.ModifyAsync(x => x.Nickname = user.Nickname.Replace("[AFK]", "")).ConfigureAwait(false);
                            }
                            catch
                            {
                                //ignored
                            }

                            return;
                        }
                    }
                }

                if (msg.MentionedUsers.Count > 0 && !msg.Author.IsBot)
                {
                    var prefix = await _guildSettings.GetPrefix(user.Guild);
                    if (msg.Content.Contains($"{prefix}afkremove") || msg.Content.Contains($"{prefix}afkrm") || msg.Content.Contains($"{prefix}afk"))
                    {
                        return;
                    }

                    if (await GetDisabledAfkChannels(user.GuildId) is not "0" and not null)
                    {
                        var chans = await GetDisabledAfkChannels(user.GuildId);
                        var e = chans.Split(",");
                        if (e.Contains(msg.Channel.Id.ToString())) return;
                    }

                    if (msg.MentionedUsers.FirstOrDefault() is not IGuildUser mentuser) return;
                    if (IsAfk(user.Guild, mentuser))
                    {
                        try
                        {
                            await user.ModifyAsync(x => x.Nickname = user.Nickname.Replace("[AFK]", "")).ConfigureAwait(false);
                        }
                        catch
                        {
                            //ignored
                        }

                        var afkmessage = GetAfkMessage(user.GuildId, user.Id);
                        var customafkmessage = await GetCustomAfkMessage(user.Guild.Id);
                        var afkdel = await GetAfkDel(((ITextChannel)msg.Channel).GuildId);
                        if (customafkmessage is null or "-")
                        {
                            var a = await msg.Channel.EmbedAsync(new EmbedBuilder()
                                                                 .WithAuthor(eab => eab.WithName($"{mentuser} is currently away").WithIconUrl(mentuser.GetAvatarUrl()))
                                                                 .WithDescription(GetAfkMessage(user.GuildId, mentuser.Id).Last().Message.Truncate(await GetAfkLength(user.Guild.Id)))
                                                                 .WithFooter(new EmbedFooterBuilder
                                                                 {
                                                                     Text =
                                                                         $"AFK for {(DateTime.UtcNow - GetAfkMessage(user.GuildId, mentuser.Id).Last().DateAdded.Value).Humanize()}"
                                                                 }).WithOkColor()).ConfigureAwait(false);
                            if (afkdel > 0)
                                a.DeleteAfter(afkdel);
                            return;
                        }

                        var replacer = new ReplacementBuilder()
                                       .WithOverride("%afk.message%", () => afkmessage.Last().Message.SanitizeMentions(true).Truncate(GetAfkLength(user.GuildId).GetAwaiter().GetResult()))
                                       .WithOverride("%afk.user%", () => mentuser.ToString()).WithOverride("%afk.user.mention%", () => mentuser.Mention)
                                       .WithOverride("%afk.user.avatar%", () => mentuser.GetAvatarUrl(size: 2048)).WithOverride("%afk.user.id%", () => mentuser.Id.ToString())
                                       .WithOverride("%afk.triggeruser%", () => msg.Author.ToString())
                                       .WithOverride("%afk.triggeruser.avatar%", () => msg.Author.RealAvatarUrl().ToString())
                                       .WithOverride("%afk.triggeruser.id%", () => msg.Author.Id.ToString()).WithOverride("%afk.triggeruser.mention%", () => msg.Author.Mention)
                                       .WithOverride("%afk.time%", () =>
                                           // ReSharper disable once PossibleInvalidOperationException
                                           $"{(DateTime.UtcNow - GetAfkMessage(user.GuildId, user.Id).Last().DateAdded.Value).Humanize()}").Build();
                        var ebe = SmartEmbed.TryParse(replacer.Replace(customafkmessage), (msg.Channel as ITextChannel)?.GuildId, out var embed, out var plainText,
                            out var components);
                        if (!ebe)
                        {
                            var a = await msg.Channel.SendMessageAsync(replacer.Replace(customafkmessage).SanitizeMentions(true)).ConfigureAwait(false);
                            if (afkdel != 0)
                                a.DeleteAfter(afkdel);
                            return;
                        }

                        var b = await msg.Channel.SendMessageAsync(plainText, embeds: embed, components: components.Build()).ConfigureAwait(false);
                        if (afkdel > 0)
                            b.DeleteAfter(afkdel);
                    }
                }
            }
        }, TaskCreationOptions.LongRunning);
        return Task.CompletedTask;
    }

    public async Task<IGuildUser[]> GetAfkUsers(IGuild guild) =>
        _cache.GetAfkForGuild(guild.Id) == null
            ? Array.Empty<IGuildUser>()
            : await _cache.GetAfkForGuild(guild.Id).GroupBy(m => m.UserId).Where(m => !string.IsNullOrEmpty(m.Last().Message)).Select(async m => await guild.GetUserAsync(m.Key).ConfigureAwait(false))
                          .WhenAll().ConfigureAwait(false);

    public async Task SetCustomAfkMessage(IGuild guild, string afkMessage)
    {
        await using var uow = _db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.AfkMessage = afkMessage;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task TimedAfk(
        IGuild guild,
        IUser user,
        string message,
        TimeSpan time)
    {
        await AfkSet(guild, user as IGuildUser, message, 1).ConfigureAwait(false);
        await Task.Delay(time.Milliseconds).ConfigureAwait(false);
        await AfkSet(guild, user as IGuildUser, "", 0).ConfigureAwait(false);
    }

    public bool IsAfk(IGuild guild, IGuildUser user)
    {
        var afkmsg = GetAfkMessage(guild.Id, user.Id);
        if (!afkmsg.Any())
            return false;
        var result = afkmsg.LastOrDefault();
        if (result is null)
            return false;
        return !string.IsNullOrEmpty(result.Message);
    }

    private Task MessageUpdated(Cacheable<IMessage, ulong> msg, SocketMessage msg2, ISocketMessageChannel t)
    {
        _ = Task.Factory.StartNew(async () =>
        {
            var message = await msg.GetOrDownloadAsync().ConfigureAwait(false);
            if (message is null)
                return;
            var origDateUnspecified = message.Timestamp.ToUniversalTime();
            var origDate = new DateTime(origDateUnspecified.Ticks, DateTimeKind.Unspecified);
            if (DateTime.UtcNow > origDate.Add(TimeSpan.FromMinutes(30)))
                return;

            await MessageReceived(msg2).ConfigureAwait(false);
        }, TaskCreationOptions.LongRunning);
        return Task.CompletedTask;
    }

    public async Task AfkTypeSet(IGuild guild, int num)
    {
        await using var uow = _db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.AfkType = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task AfkDelSet(IGuild guild, int num)
    {
        await using var uow = _db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.AfkDel = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task AfkLengthSet(IGuild guild, int num)
    {
        await using var uow = _db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.AfkLength = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task AfkTimeoutSet(IGuild guild, int num)
    {
        await using var uow = _db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.AfkTimeout = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task AfkDisabledSet(IGuild guild, string num)
    {
        await using var uow = _db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.AfkDisabledChannels = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task<string> GetCustomAfkMessage(ulong id) => (await _guildSettings.GetGuildConfig(id)).AfkMessage;

    public async Task<int> GetAfkDel(ulong id) => (await _guildSettings.GetGuildConfig(id)).AfkDel;

    private async Task<int> GetAfkType(ulong id) => (await _guildSettings.GetGuildConfig(id)).AfkType;

    public async Task<int> GetAfkLength(ulong id) => (await _guildSettings.GetGuildConfig(id)).AfkLength;

    public async Task<string> GetDisabledAfkChannels(ulong id) => (await _guildSettings.GetGuildConfig(id)).AfkDisabledChannels;

    private async Task<int> GetAfkTimeout(ulong id) => (await _guildSettings.GetGuildConfig(id)).AfkTimeout;

    public async Task AfkSet(
        IGuild guild,
        IGuildUser user,
        string message,
        int timed)
    {
        var afk = new Database.Models.Afk { GuildId = guild.Id, UserId = user.Id, Message = message, WasTimed = timed };
        await using var uow = _db.GetDbContext();
        uow.Afk.Update(afk);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        var current = _cache.GetAfkForGuild(guild.Id) ?? new List<Database.Models.Afk?>();
        current.Add(afk);
        await _cache.AddAfkToCache(guild.Id, current).ConfigureAwait(false);
    }

    public IEnumerable<Database.Models.Afk> GetAfkMessage(ulong gid, ulong uid)
    {
        var e = _cache.GetAfkForGuild(gid);
        return e is null ? new List<Database.Models.Afk>() : e.Where(x => x.UserId == uid).ToList();
    }
}