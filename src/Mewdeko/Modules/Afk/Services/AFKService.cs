using System.Threading;
using Humanizer;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Settings;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Afk.Services;

public class AfkService : INService, IReadyExecutor
{
    private readonly DbService db;
    private readonly DiscordSocketClient client;
    private readonly IDataCache cache;
    private readonly GuildSettingsService guildSettings;
    private readonly IBotCredentials creds;
    private readonly BotConfigService config;

    public AfkService(
        DbService db,
        DiscordSocketClient client,
        IDataCache cache,
        GuildSettingsService guildSettings, EventHandler eventHandler,
        IBotCredentials creds,
        BotConfigService config)
    {
        this.cache = cache;
        this.guildSettings = guildSettings;
        this.creds = creds;
        this.config = config;
        this.db = db;
        this.client = client;
        eventHandler.MessageReceived += MessageReceived;
        eventHandler.MessageUpdated += MessageUpdated;
        eventHandler.UserIsTyping += UserTyping;
        _ = Task.Run(StartTimedAfkLoop);
    }

    private async Task StartTimedAfkLoop()
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync())
        {
            await Task.Delay(1000).ConfigureAwait(false);
            try
            {
                var now = DateTime.UtcNow;
                var afks = GetAfkBeforeAsync(now);
                if (!afks.Any())
                    continue;

                Log.Information($"Executing {afks.Count()} timed AFKs.");
                await Task.WhenAll(afks.Select(TimedAfkFinished)).ConfigureAwait(false);
                await Task.Delay(1500).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning($"Error in Timed AFK loop: {ex.Message}");
                Log.Warning(ex.ToString());
            }
        }
    }

    private IEnumerable<Database.Models.Afk> GetAfkBeforeAsync(DateTime now)
    {
        using var uow = db.GetDbContext();
        IEnumerable<Database.Models.Afk> afks;

        if (uow.Database.IsNpgsql())
        {
            afks = uow.Afk
                .ToLinqToDB().Where(x =>
                    (int)(x.GuildId / (ulong)Math.Pow(2, 22) % (ulong)creds.TotalShards) == client.ShardId &&
                    x.When < now && x.WasTimed == 1).ToList();
        }

        else
        {
            afks = uow.Afk
                .FromSqlInterpolated(
                    $"select * from AFK where ((GuildId >> 22) % {creds.TotalShards}) = {client.ShardId} and \"WasTimed\" = 1 and \"when\" < {now};")
                .ToList();
        }

        return afks;
    }

    private async Task TimedAfkFinished(Database.Models.Afk afk)
    {
        if (!await IsAfk(afk.GuildId, afk.UserId))
        {
            await RemoveAfk(afk);
            return;
        }

        await AfkSet(afk.GuildId, afk.UserId, "", 0);
        var guild = client.GetGuild(afk.GuildId);
        var user = guild.GetUser(afk.UserId);
        try
        {
            await user.ModifyAsync(x => x.Nickname = user.Nickname.Replace("[AFK]", "")).ConfigureAwait(false);
        }
        catch
        {
            //ignored
        }

        await RemoveAfk(afk);
    }

    public async Task OnReadyAsync()
    {
        await using var uow = db.GetDbContext();
        var guilds = client.Guilds.Select(x => x.Id).ToList();
        var allafk = await uow.Afk.ToListAsyncEF();

        var latestAfkPerUserPerGuild =
            new ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, Database.Models.Afk>>();

        Parallel.ForEach(guilds, guildId =>
        {
            var latestAfkPerUser = new ConcurrentDictionary<ulong, Database.Models.Afk>();

            foreach (var afk in allafk.Where(afk => afk.GuildId == guildId))
            {
                latestAfkPerUser.AddOrUpdate(afk.UserId, afk,
                    (key, existingVal) => existingVal.When < afk.When ? afk : existingVal);
            }

            latestAfkPerUserPerGuild[guildId] = latestAfkPerUser;
        });

        CacheLatestAfks(latestAfkPerUserPerGuild);

        Environment.SetEnvironmentVariable($"AFK_CACHED_{client.ShardId}", "1");
        Log.Information("AFK Cached");
    }

    private async void CacheLatestAfks(
        ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, Database.Models.Afk>> latestAfks)
    {
        foreach (var guild in latestAfks)
        {
            foreach (var userAfk in guild.Value)
            {
                if (string.IsNullOrEmpty(userAfk.Value.Message))
                    await cache.ClearAfk(guild.Key, userAfk.Key);
                await cache.CacheAfk(guild.Key, userAfk.Key, userAfk.Value);
            }
        }
    }


    private async Task UserTyping(Cacheable<IUser, ulong> user, Cacheable<IMessageChannel, ulong> chan)
    {
        if (user.Value is IGuildUser use)
        {
            if (await GetAfkType(use.GuildId) is 2 or 4)
                if (await IsAfk(use.Guild.Id, use.Id))
                {
                    var t = await GetAfk(use.Guild.Id, user.Id);
                    if (t.DateAdded != null &&
                        t.DateAdded.Value.ToLocalTime() < DateTime.Now.AddSeconds(-await GetAfkTimeout(use.GuildId)) &&
                        t.WasTimed == 0)
                    {
                        await AfkSet(use.Guild.Id, use.Id, "", 0).ConfigureAwait(false);
                        var msg = await chan.Value
                            .SendMessageAsync(
                                $"Welcome back {user.Value.Mention}! I noticed you typing so I disabled your afk.")
                            .ConfigureAwait(false);
                        try
                        {
                            await use.ModifyAsync(x => x.Nickname = use.Nickname.Replace("[AFK]", ""))
                                .ConfigureAwait(false);
                        }
                        catch
                        {
                            //ignored
                        }

                        msg.DeleteAfter(5);
                    }
                }
        }
    }

    private async Task MessageReceived(SocketMessage msg)
    {
        try
        {
            if (msg.Author.IsBot)
                return;

            if (msg.Author is IGuildUser user)
            {
                var afk = await GetAfk(user.GuildId, user.Id);
                if (await GetAfkType(user.Guild.Id) is 3 or 4)
                {
                    if (await IsAfk(user.Guild.Id, user.Id))
                    {
                        if (afk.DateAdded != null &&
                            afk.DateAdded.Value.ToLocalTime() <
                            DateTime.Now.AddSeconds(-await GetAfkTimeout(user.GuildId)) && afk.WasTimed == 0)
                        {
                            await AfkSet(user.Guild.Id, user.Id, "", 0).ConfigureAwait(false);
                            var ms = await msg.Channel
                                .SendMessageAsync($"Welcome back {user.Mention}, I have disabled your AFK for you.")
                                .ConfigureAwait(false);
                            ms.DeleteAfter(5);
                            try
                            {
                                await user.ModifyAsync(x => x.Nickname = user.Nickname.Replace("[AFK]", ""))
                                    .ConfigureAwait(false);
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
                    var prefix = await guildSettings.GetPrefix(user.Guild);
                    if (msg.Content.Contains($"{prefix}afkremove") || msg.Content.Contains($"{prefix}afkrm") ||
                        msg.Content.Contains($"{prefix}afk"))
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
                    if (await IsAfk(user.Guild.Id, mentuser.Id))
                    {
                        try
                        {
                            await user.ModifyAsync(x => x.Nickname = user.Nickname.Replace("[AFK]", ""))
                                .ConfigureAwait(false);
                        }
                        catch
                        {
                            //ignored
                        }

                        var customafkmessage = await GetCustomAfkMessage(user.Guild.Id);
                        var afkdel = await GetAfkDel(((ITextChannel)msg.Channel).GuildId);
                        if (customafkmessage is null or "-")
                        {
                            var a = await msg.Channel.SendMessageAsync(embed: new EmbedBuilder()
                                    .WithAuthor(eab =>
                                        eab.WithName($"{mentuser} is currently away")
                                            .WithIconUrl(mentuser.GetAvatarUrl()))
                                    .WithDescription(afk.Message
                                        .Truncate(await GetAfkLength(user.Guild.Id)))
                                    .WithFooter(new EmbedFooterBuilder
                                    {
                                        Text =
                                            // ReSharper disable once PossibleInvalidOperationException
                                            $"AFK for {(DateTime.UtcNow - afk.DateAdded.Value).Humanize()}"
                                    }).WithOkColor().Build(),
                                components: config.Data.ShowInviteButton
                                    ? new ComponentBuilder()
                                        .WithButton(style: ButtonStyle.Link,
                                            url:
                                            "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                                            label: "Invite Me!",
                                            emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build()
                                    : null).ConfigureAwait(false);
                            if (afkdel > 0)
                                a.DeleteAfter(afkdel);
                            return;
                        }

                        var replacer = new ReplacementBuilder()
                            .WithOverride("%afk.message%",
                                () => afk.Message.SanitizeMentions(true)
                                    .Truncate(GetAfkLength(user.GuildId).GetAwaiter().GetResult()))
                            .WithOverride("%afk.user%", () => mentuser.ToString())
                            .WithOverride("%afk.user.mention%", () => mentuser.Mention)
                            .WithOverride("%afk.user.avatar%", () => mentuser.GetAvatarUrl(size: 2048))
                            .WithOverride("%afk.user.id%", () => mentuser.Id.ToString())
                            .WithOverride("%afk.triggeruser%", () => msg.Author.ToString().EscapeWeirdStuff())
                            .WithOverride("%afk.triggeruser.avatar%", () => msg.Author.RealAvatarUrl().ToString())
                            .WithOverride("%afk.triggeruser.id%", () => msg.Author.Id.ToString())
                            .WithOverride("%afk.triggeruser.mention%", () => msg.Author.Mention)
                            .WithOverride("%afk.time%", () =>
                                // ReSharper disable once PossibleInvalidOperationException
                                $"{(DateTime.UtcNow - afk.DateAdded.Value).Humanize()}")
                            .Build();
                        var ebe = SmartEmbed.TryParse(replacer.Replace(customafkmessage),
                            ((ITextChannel)msg.Channel)?.GuildId, out var embed, out var plainText,
                            out var components);
                        if (!ebe)
                        {
                            var a = await msg.Channel
                                .SendMessageAsync(replacer.Replace(customafkmessage).SanitizeMentions(true))
                                .ConfigureAwait(false);
                            if (afkdel != 0)
                                a.DeleteAfter(afkdel);
                            return;
                        }

                        var b = await msg.Channel
                            .SendMessageAsync(plainText, embeds: embed, components: components?.Build())
                            .ConfigureAwait(false);
                        if (afkdel > 0)
                            b.DeleteAfter(afkdel);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("Error in AfkHandler: " + e);
        }
    }

    public async Task<Database.Models.Afk?> GetAfk(ulong guildId, ulong userId)
    {
        return await cache.RetrieveAfk(guildId, userId);
    }

    public async Task<List<IGuildUser>> GetAfkUsers(IGuild guild)
    {
        var afkUsers = new List<IGuildUser>();
        var users = await guild.GetUsersAsync();

        foreach (var i in users)
        {
            if (await IsAfk(guild.Id, i.Id).ConfigureAwait(false))
                afkUsers.Add(i);
        }

        return afkUsers;
    }

    public async Task SetCustomAfkMessage(IGuild guild, string afkMessage)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.AfkMessage = afkMessage;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task<bool> IsAfk(ulong guildId, ulong userId)
    {
        var afkmsg = await cache.RetrieveAfk(guildId, userId);
        return afkmsg is not null;
    }

    private async Task MessageUpdated(Cacheable<IMessage, ulong> msg, SocketMessage msg2, ISocketMessageChannel t)
    {
        var message = await msg.GetOrDownloadAsync().ConfigureAwait(false);
        if (message is null)
            return;
        var origDateUnspecified = message.Timestamp.ToUniversalTime();
        var origDate = new DateTime(origDateUnspecified.Ticks, DateTimeKind.Unspecified);
        if (DateTime.UtcNow > origDate.Add(TimeSpan.FromMinutes(30)))
            return;

        await MessageReceived(msg2).ConfigureAwait(false);
    }

    public async Task AfkTypeSet(IGuild guild, int num)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.AfkType = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task AfkDelSet(IGuild guild, int inputNum)
    {
        var num = inputNum.ToString();
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.AfkDel = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task AfkLengthSet(IGuild guild, int num)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.AfkLength = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task AfkTimeoutSet(IGuild guild, int num)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.AfkTimeout = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task AfkDisabledSet(IGuild guild, string num)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.AfkDisabledChannels = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task<string> GetCustomAfkMessage(ulong id) => (await guildSettings.GetGuildConfig(id)).AfkMessage;

    public async Task<int> GetAfkDel(ulong id) => int.Parse((await guildSettings.GetGuildConfig(id)).AfkDel);

    private async Task<int> GetAfkType(ulong id) => (await guildSettings.GetGuildConfig(id)).AfkType;

    public async Task<int> GetAfkLength(ulong id) => (await guildSettings.GetGuildConfig(id)).AfkLength;

    public async Task<string?> GetDisabledAfkChannels(ulong id) =>
        (await guildSettings.GetGuildConfig(id)).AfkDisabledChannels;

    private async Task<int> GetAfkTimeout(ulong id) => (await guildSettings.GetGuildConfig(id)).AfkTimeout;

    public async Task AfkSet(
        ulong guildId,
        ulong userId,
        string message,
        int timed,
        DateTime when = new())
    {
        var afk = new Database.Models.Afk
        {
            GuildId = guildId,
            UserId = userId,
            Message = message,
            WasTimed = timed,
            When = when
        };
        await using var uow = db.GetDbContext();
        uow.Afk.Update(afk);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        if (string.IsNullOrEmpty(message))
            await cache.ClearAfk(guildId, userId);
        else
            await cache.CacheAfk(guildId, userId, afk);
    }

    private async Task RemoveAfk(Database.Models.Afk afk)
    {
        await cache.ClearAfk(afk.GuildId, afk.UserId);

        await using var uow = db.GetDbContext();
        uow.Afk.Remove(afk);
        await uow.SaveChangesAsync();
    }
}