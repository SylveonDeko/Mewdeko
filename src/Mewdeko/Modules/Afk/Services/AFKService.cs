using Discord;
using Discord.WebSocket;
using Humanizer;
using Mewdeko.Common;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.Replacements;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using Mewdeko.Extensions;
using Serilog;

namespace Mewdeko.Modules.Afk.Services;

public class AfkService : INService, IReadyExecutor
{
    private readonly DbService _db;
    private readonly CommandHandler _cmd;
    public readonly DiscordSocketClient Client;
    private readonly Mewdeko _bot;
    private readonly IDataCache _cache;

    public AfkService(
        DbService db,
        DiscordSocketClient client,
        CommandHandler handle,
        Mewdeko bot,
        IDataCache cache)
    {
        
        _bot = bot;
        _cache = cache;
        _db = db;
        Client = client;
        _cmd = handle;
        Client.MessageReceived += MessageReceived;
        Client.MessageUpdated += MessageUpdated;
        Client.UserIsTyping += UserTyping;
    }
    

    public async Task OnReadyAsync()
    {
        {
            await using var uow = _db.GetDbContext();
            var allafk = uow.Afk.GetAll();
            foreach (var i in _bot.GetCurrentGuildIds())
            {
                await _cache.CacheAfk(i, allafk.Where(x => x.GuildId == i).ToList()).ConfigureAwait(false);
            }
            Environment.SetEnvironmentVariable($"AFK_CACHED_{Client.ShardId}", "1");
            Log.Information("AFK Cached.");
        }
    }
    

    private Task UserTyping(Cacheable<IUser, ulong> user, Cacheable<IMessageChannel, ulong> chan)
    {
        _ = Task.Run(async () =>
        {
            if (user.Value is IGuildUser use)
                if (GetAfkType(use.GuildId) is 2 or 4)
                    if (IsAfk(use.Guild, use))
                    {
                        var t = GetAfkMessage(use.Guild.Id, user.Id).Last();
                        if (t.DateAdded != null
                            && t.DateAdded.Value.ToLocalTime() < DateTime.Now.AddSeconds(-GetAfkTimeout(use.GuildId))
                            && t.WasTimed == 0)
                        {
                            await AfkSet(use.Guild, use, "", 0).ConfigureAwait(false);
                            var msg = await chan.Value.SendMessageAsync(
                                $"Welcome back {user.Value.Mention}! I noticed you typing so I disabled your afk.").ConfigureAwait(false);
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
        });
        return Task.CompletedTask;
    }

    private Task MessageReceived(SocketMessage msg)
    {
        _ = Task.Run(async () =>
        {
            if (msg.Author.IsBot)
                return;

            if (msg.Author is IGuildUser user)
            {
                if (GetAfkType(user.Guild.Id) is 3 or 4)
                    if (IsAfk(user.Guild, user))
                    {
                        var t = GetAfkMessage(user.Guild.Id, user.Id).Last();
                        if (t.DateAdded != null
                            && t.DateAdded.Value.ToLocalTime() < DateTime.Now.AddSeconds(-GetAfkTimeout(user.GuildId))
                            && t.WasTimed == 0)
                        {
                            await AfkSet(user.Guild, user, "", 0).ConfigureAwait(false);
                            var ms = await msg.Channel.SendMessageAsync(
                                $"Welcome back {user.Mention}, I have disabled your AFK for you.").ConfigureAwait(false);
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

                if (msg.MentionedUsers.Count > 0 && !msg.Author.IsBot)
                {
                    if (msg.Content.Contains($"{_cmd.GetPrefix(user.Guild)}afkremove")
                        || msg.Content.Contains($"{_cmd.GetPrefix(user.Guild)}afkrm")
                        || msg.Content.Contains($"{_cmd.GetPrefix(user.Guild)}afk")) return;
                    if (GetDisabledAfkChannels(user.GuildId) is not "0" and not null)
                    {
                        var chans = GetDisabledAfkChannels(user.GuildId);
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
                        var customafkmessage = GetCustomAfkMessage(user.Guild.Id);
                        var afkdel = GetAfkDel(((ITextChannel)msg.Channel).GuildId);
                        if (customafkmessage is null or "-")
                        {
                            var a = await msg.Channel.EmbedAsync(new EmbedBuilder()
                                                                 .WithAuthor(eab => eab.WithName($"{mentuser} is currently away")
                                                                                       .WithIconUrl(mentuser.GetAvatarUrl()))
                                                                 .WithDescription(
                                                                     GetAfkMessage(user.GuildId, mentuser.Id).Last()
                                                                                                             .Message.Truncate(GetAfkLength(user.Guild.Id)))
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
                                       .WithOverride("%afk.message%",
                                           () => afkmessage.Last().Message.SanitizeMentions(true)
                                                                                         .Truncate(GetAfkLength(user.GuildId)))
                                       .WithOverride("%afk.user%", () => mentuser.ToString())
                                       .WithOverride("%afk.user.mention%", () => mentuser.Mention)
                                       .WithOverride("%afk.user.avatar%", () => mentuser.GetAvatarUrl(size: 2048))
                                       .WithOverride("%afk.user.id%", () => mentuser.Id.ToString())
                                       .WithOverride("%afk.triggeruser%", () => msg.Author.ToString())
                                       .WithOverride("%afk.triggeruser.avatar%",
                                           () => msg.Author.RealAvatarUrl().ToString())
                                       .WithOverride("%afk.triggeruser.id%", () => msg.Author.Id.ToString())
                                       .WithOverride("%afk.triggeruser.mention%", () => msg.Author.Mention)
                                       .WithOverride("%afk.time%", () =>
                                           // ReSharper disable once PossibleInvalidOperationException
                                           $"{(DateTime.UtcNow - GetAfkMessage(user.GuildId, user.Id).Last().DateAdded.Value).Humanize()}")
                                       .Build();
                        var ebe = SmartEmbed.TryParse(replacer.Replace(customafkmessage), out var embed, out var plainText);
                        if (!ebe)
                        {
                            var a = await msg.Channel.SendMessageAsync(replacer.Replace(customafkmessage).SanitizeMentions(true)).ConfigureAwait(false);
                            if (afkdel != 0)
                                a.DeleteAfter(afkdel);
                            return;
                        }
                        var b = await msg.Channel.SendMessageAsync(plainText,
                            embed: embed?.Build()).ConfigureAwait(false);
                        if (afkdel > 0)
                            b.DeleteAfter(afkdel);
                    }
                }
            }
        });
        return Task.CompletedTask;
    }

    public async Task<IGuildUser[]> GetAfkUsers(IGuild guild) =>
        _cache.GetAfkForGuild(guild.Id) == null
            ? Array.Empty<IGuildUser>()
            : await _cache.GetAfkForGuild(guild.Id).GroupBy(m => m.UserId)
                   .Where(m => !string.IsNullOrEmpty(m.Last().Message))
                   .Select(async m => await guild.GetUserAsync(m.Key)).WhenAll().ConfigureAwait(false);

    public async Task SetCustomAfkMessage(IGuild guild, string afkMessage)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.AfkMessage = afkMessage;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
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
        if (afkmsg is null)
            return false;
        var result = afkmsg.LastOrDefault();
        if (result is null)
            return false;
        return !string.IsNullOrEmpty(result.Message);
    }

    private Task MessageUpdated(Cacheable<IMessage, ulong> msg, SocketMessage msg2, ISocketMessageChannel t)
    {
        _ = Task.Run(async () =>
        {
            var message = await msg.GetOrDownloadAsync();
            if (message is null)
                return;
            if (message.Timestamp > new DateTimeOffset(DateTime.UtcNow, TimeSpan.FromMinutes(30)))
                return;

            await MessageReceived(msg2);
        });
        return Task.CompletedTask;
    }

    public async Task AfkTypeSet(IGuild guild, int num)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.AfkType = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task AfkDelSet(IGuild guild, int num)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.AfkDel = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task AfkLengthSet(IGuild guild, int num)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.AfkLength = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }


    public async Task AfkTimeoutSet(IGuild guild, int num)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.AfkTimeout = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task AfkDisabledSet(IGuild guild, string num)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.AfkDisabledChannels = num;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public string GetCustomAfkMessage(ulong? id) 
        => _bot.GetGuildConfig(id.Value).AfkMessage;

    public int GetAfkDel(ulong? id) 
        => _bot.GetGuildConfig(id.Value).AfkDel;

    private int GetAfkType(ulong? id) 
        => _bot.GetGuildConfig(id.Value).AfkType;

    public int GetAfkLength(ulong? id) 
        => _bot.GetGuildConfig(id.Value).AfkLength;

    public string GetDisabledAfkChannels(ulong? id) 
        => _bot.GetGuildConfig(id.Value).AfkDisabledChannels;

    private int GetAfkTimeout(ulong? id) 
        => _bot.GetGuildConfig(id.Value).AfkTimeout;

    public async Task AfkSet(
        IGuild guild,
        IGuildUser user,
        string message,
        int timed)
    {
        var afk = new Database.Models.Afk {GuildId = guild.Id, UserId = user.Id, Message = message, WasTimed = timed};
        await using var uow = _db.GetDbContext();
        uow.Afk.Update(afk);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        var current = _cache.GetAfkForGuild(guild.Id) ?? new List<Database.Models.Afk>(); 
        current.Add(afk);
        await _cache.AddAfkToCache(guild.Id, current).ConfigureAwait(false);
    }

    public IEnumerable<Database.Models.Afk> GetAfkMessage(ulong gid, ulong uid)
    {
        var e = _cache.GetAfkForGuild(gid);
        return e is null ? new List<Database.Models.Afk>() : e.Where(x => x.UserId == uid).ToList();
    }
    
}