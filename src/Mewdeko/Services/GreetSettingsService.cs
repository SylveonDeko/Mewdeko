using Mewdeko.Services.Common;
using Mewdeko.Services.Settings;
using Serilog;
using System.Collections.Concurrent;
using Embed = Discord.Embed;

namespace Mewdeko.Services;

public class GreetSettingsService : INService
{
    private readonly BotConfigService _bss;
    private readonly DiscordSocketClient _client;
    private readonly DbService _db;
    private readonly Mewdeko _bot;

    private readonly GreetGrouper<IGuildUser> _greets = new();

    public GreetSettingsService(DiscordSocketClient client, Mewdeko bot, DbService db,
        BotConfigService bss)
    {
        _db = db;
        _client = client;
        _bot = bot;
        _bss = bss;
        using var uow = db.GetDbContext();
        var gc = uow.GuildConfigs.All().Where(x => bot.GetCurrentGuildIds().Contains(x.GuildId));
        GuildConfigsCache = new ConcurrentDictionary<ulong, GreetSettings>(
            gc
                .ToDictionary(g => g.GuildId, GreetSettings.Create));

        _client.UserJoined += UserJoined;
        _client.UserLeft += UserLeft;

        bot.JoinedGuild += Bot_JoinedGuild;
        _client.LeftGuild += Client_LeftGuild;

        _client.GuildMemberUpdated += ClientOnGuildMemberUpdated;
    }

    public ConcurrentDictionary<ulong, GreetSettings> GuildConfigsCache { get; }
    public bool GroupGreets => _bss.Data.GroupGreets;

    private async Task TriggerBoostMessage(GreetSettings conf, SocketGuildUser user)
    {
        var chan = user.Guild.GetTextChannel(conf.BoostMessageChannelId);

        if (chan is null)
            return;

        if (string.IsNullOrWhiteSpace(conf.BoostMessage))
            return;
        var rep = new ReplacementBuilder()
                  .WithDefault(user, chan, user.Guild, _client)
                  .Build();
        if (SmartEmbed.TryParse(rep.Replace(conf.BoostMessage), user.Guild?.Id, out var embed, out var plainText, out var components))
        {
            try
            {
                var toDelete = await chan.SendMessageAsync(plainText, embed: embed?.Build(), components:components?.Build());
                if (conf.BoostMessageDeleteAfter > 0) toDelete.DeleteAfter(conf.BoostMessageDeleteAfter);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending boost message.");
            }
        }
        else
        {
            var msg = rep.Replace(conf.BoostMessage);
            try
            {
                var toDelete = await chan.SendMessageAsync(msg);

                if (conf.BoostMessageDeleteAfter > 0) toDelete.DeleteAfter(conf.BoostMessageDeleteAfter);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending boost message.");
            }
        }
    }

    private Task ClientOnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> optOldUser, SocketGuildUser newUser)
    {
        _ = Task.Factory.StartNew(async () =>
        {
            // if user is a new booster
            // or boosted again the same server
            if ((optOldUser.Value is not { PremiumSince: null } || newUser is not { PremiumSince: not null })
                && (optOldUser.Value?.PremiumSince is not { } oldDate || newUser.PremiumSince is not { } newDate || newDate <= oldDate))
            {
                return;
            }

            var conf = GetOrAddSettingsForGuild(newUser.Guild.Id);
            if (!conf.SendBoostMessage)
                return;

            await TriggerBoostMessage(conf, newUser);
        }, TaskCreationOptions.LongRunning);
        return Task.CompletedTask;
    }

    private Task Client_LeftGuild(SocketGuild arg)
    {
        GuildConfigsCache.TryRemove(arg.Id, out _);
        return Task.CompletedTask;
    }

    private Task Bot_JoinedGuild(GuildConfig gc)
    {
        GuildConfigsCache.AddOrUpdate(gc.GuildId,
            GreetSettings.Create(gc),
            delegate { return GreetSettings.Create(gc); });
        return Task.CompletedTask;
    }

    private Task UserLeft(SocketGuild guild, SocketUser usr)
    {
        var _ = Task.Factory.StartNew(async () =>
        {
            try
            {
                var user = usr as SocketGuildUser;
                var conf = GetOrAddSettingsForGuild(guild.Id);

                if (!conf.SendChannelByeMessage) return;

                if (guild.Channels.SingleOrDefault(c =>
                        c.Id == conf.ByeMessageChannelId) is not ITextChannel
                    channel) //maybe warn the server owner that the channel is missing
                {
                    return;
                }

                // if group is newly created, greet that user right away,
                // but any user which joins in the next 5 seconds will
                // be greeted in a group greet
                await ByeUsers(conf, channel, new[] { user });
            }
            catch
            {
                // ignored
            }
        }, TaskCreationOptions.LongRunning);
        return Task.CompletedTask;
    }

    public bool SetBoostMessage(ulong guildId, string message)
    {
        message = message?.SanitizeMentions();

        using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set);
        conf.BoostMessage = message;

        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);
        _bot.UpdateGuildConfig(guildId, conf);
        uow.SaveChanges();
        return conf.SendBoostMessage;
    }

    public async Task SetBoostDel(ulong guildId, int timer)
    {
        if (timer is < 0 or > 600)
            throw new ArgumentOutOfRangeException(nameof(timer));

        await using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set);
        conf.BoostMessageDeleteAfter = timer;
        _bot.UpdateGuildConfig(guildId, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public string GetBoostMessage(ulong gid)
        => _bot.GetGuildConfig(gid).BoostMessage;

    public async Task<bool> SetBoost(ulong guildId, ulong channelId, bool? value = null)
    {
        await using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set);
        var enabled = conf.SendBoostMessage = value ?? !conf.SendBoostMessage;
        conf.BoostMessageChannelId = channelId;
        _bot.UpdateGuildConfig(guildId, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return enabled;
    }

    public async Task SetWebGreetUrl(IGuild guild, string url)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.GreetHook = url;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetWebLeaveUrl(IGuild guild, string url)
    {
        await using var uow = _db.GetDbContext();
        var gc = uow.ForGuildId(guild.Id, set => set);
        gc.LeaveHook = url;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _bot.UpdateGuildConfig(guild.Id, gc);
    }

    public string GetDmGreetMsg(ulong id)
    {
        using var uow = _db.GetDbContext();
        return uow.ForGuildId(id, set => set)?.DmGreetMessageText;
    }

    public string GetGreetMsg(ulong gid)
    {
        using var uow = _db.GetDbContext();
        return uow.ForGuildId(gid, set => set).ChannelGreetMessageText;
    }

    public string GetGreetHook(ulong? gid)
        => _bot.GetGuildConfig(gid.Value).GreetHook;

    public string GetLeaveHook(ulong? gid)
        => _bot.GetGuildConfig(gid.Value).LeaveHook;

    private Task ByeUsers(GreetSettings conf, ITextChannel channel, IUser user) => ByeUsers(conf, channel, new[] { user });

    private async Task ByeUsers(GreetSettings conf, ITextChannel channel, IEnumerable<IUser> users)
    {
        if (!users.Any())
            return;

        var rep = new ReplacementBuilder()
                  .WithChannel(channel)
                  .WithClient(_client)
                  .WithServer(_client, (SocketGuild)channel.Guild)
                  .WithManyUsers(users)
                  .Build();
        var lh = GetLeaveHook(channel.GuildId);

        if (SmartEmbed.TryParse(rep.Replace(conf.ChannelByeMessageText), channel.GuildId, out var embed, out var plainText, out var components))
        {
            try
            {
                if (string.IsNullOrEmpty(lh) || lh == 0.ToString())
                {
                    var toDelete = await channel.SendMessageAsync(plainText, embed: embed?.Build(), components:components?.Build()).ConfigureAwait(false);
                    if (conf.AutoDeleteByeMessagesTimer > 0) toDelete.DeleteAfter(conf.AutoDeleteByeMessagesTimer);
                }
                else
                {
                    var webhook = new DiscordWebhookClient(GetLeaveHook(channel.GuildId));
                    var embeds = new List<Embed> { embed?.Build() };
                    var toDelete = await webhook.SendMessageAsync(plainText, embeds: embeds, components:components?.Build())
                                                .ConfigureAwait(false);
                    if (conf.AutoDeleteByeMessagesTimer > 0)
                    {
                        var msg = await channel.GetMessageAsync(toDelete) as IUserMessage;
                        msg.DeleteAfter(conf.AutoDeleteByeMessagesTimer);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error embeding bye message");
            }
        }
        else
        {
            var msg = rep.Replace(conf.ChannelByeMessageText);
            if (string.IsNullOrWhiteSpace(msg))
                return;
            try
            {
                if (string.IsNullOrEmpty(lh) || lh == 0.ToString())
                {
                    var toDelete = await channel.SendMessageAsync(msg.SanitizeMentions()).ConfigureAwait(false);
                    if (conf.AutoDeleteByeMessagesTimer > 0) toDelete.DeleteAfter(conf.AutoDeleteByeMessagesTimer);
                }
                else
                {
                    var webhook = new DiscordWebhookClient(GetLeaveHook(channel.GuildId));
                    var toDel = await webhook.SendMessageAsync(msg.SanitizeMentions());
                    if (conf.AutoDeleteByeMessagesTimer > 0)
                    {
                        var msg2 = await channel.GetMessageAsync(toDel) as IUserMessage;
                        msg2.DeleteAfter(conf.AutoDeleteByeMessagesTimer);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error sending bye message");
            }
        }
    }

    private Task GreetUsers(GreetSettings conf, ITextChannel channel, IGuildUser user) => GreetUsers(conf, channel, new[] { user });

    private async Task GreetUsers(GreetSettings conf, ITextChannel channel, IEnumerable<IGuildUser> users)
    {
        if (!users.Any())
            return;

        var rep = new ReplacementBuilder()
                  .WithChannel(channel)
                  .WithClient(_client)
                  .WithServer(_client, (SocketGuild)channel.Guild)
                  .WithManyUsers(users)
                  .Build();
        var gh = GetGreetHook(channel.GuildId);
        if (SmartEmbed.TryParse(rep.Replace(conf.ChannelGreetMessageText), channel.GuildId, out var embed, out var plainText, out var components))
        {
            try
            {
                if (string.IsNullOrEmpty(gh) || gh == 0.ToString())
                {
                    var toDelete = await channel
                                         .SendMessageAsync(plainText, embed: embed?.Build(),
                                             components: components?.Build()).ConfigureAwait(false);
                    if (conf.AutoDeleteGreetMessagesTimer > 0)
                        toDelete.DeleteAfter(conf.AutoDeleteGreetMessagesTimer);
                }
                else
                {
                    var webhook = new DiscordWebhookClient(GetGreetHook(channel.GuildId));
                    var embeds = new List<Embed> { embed?.Build() };
                    var toDelete = await webhook
                                         .SendMessageAsync(plainText, embeds: embeds, components: components?.Build())
                                         .ConfigureAwait(false);
                    if (conf.AutoDeleteGreetMessagesTimer > 0)
                    {
                        var msg = await channel.GetMessageAsync(toDelete) as IUserMessage;
                        msg.DeleteAfter(conf.AutoDeleteGreetMessagesTimer);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error embeding greet message");
            }
        }
        else
        {
            var msg = rep.Replace(conf.ChannelGreetMessageText);
            if (!string.IsNullOrWhiteSpace(msg))
            {
                try
                {
                    if (string.IsNullOrEmpty(gh) || gh == 0.ToString())
                    {
                        var toDelete = await channel.SendMessageAsync(msg.SanitizeMentions()).ConfigureAwait(false);
                        if (conf.AutoDeleteGreetMessagesTimer > 0)
                            toDelete.DeleteAfter(conf.AutoDeleteGreetMessagesTimer);
                    }
                    else
                    {
                        var webhook = new DiscordWebhookClient(GetGreetHook(channel.GuildId));
                        var toDel = await webhook.SendMessageAsync(msg.SanitizeMentions());
                        if (conf.AutoDeleteGreetMessagesTimer > 0)
                        {
                            var msg2 = await channel.GetMessageAsync(toDel) as IUserMessage;
                            msg2.DeleteAfter(conf.AutoDeleteGreetMessagesTimer);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error sending greet message");
                }
            }
        }
    }

    private async Task<bool> GreetDmUser(GreetSettings conf, IDMChannel channel, IGuildUser user)
    {
        var rep = new ReplacementBuilder()
                  .WithDefault(user, channel, (SocketGuild)user.Guild, _client)
                  .Build();

        if (SmartEmbed.TryParse(rep.Replace(conf.DmGreetMessageText), user.GuildId, out var embed, out var plainText, out var components))
        {
            try
            {
                await channel.SendMessageAsync(plainText, embed: embed?.Build(), components: components?.Build()).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }
        else
        {
            var msg = rep.Replace(conf.DmGreetMessageText);
            if (string.IsNullOrWhiteSpace(msg)) return true;
            try
            {
                await channel.SendConfirmAsync(msg).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    private Task UserJoined(IGuildUser user)
    {
        var _ = Task.Factory.StartNew(async () =>
        {
            try
            {
                var conf = GetOrAddSettingsForGuild(user.GuildId);

                if (conf.SendChannelGreetMessage)
                {
                    var channel = await user.Guild.GetTextChannelAsync(conf.GreetMessageChannelId);
                    if (channel != null)
                    {
                        if (GroupGreets)
                        {
                            // if group is newly created, greet that user right away,
                            // but any user which joins in the next 5 seconds will
                            // be greeted in a group greet
                            if (_greets.CreateOrAdd(user.GuildId, user))
                            {
                                // greet single user
                                await GreetUsers(conf, channel, new[] { user });
                                var groupClear = false;
                                while (!groupClear)
                                {
                                    await Task.Delay(5000).ConfigureAwait(false);
                                    groupClear = _greets.ClearGroup(user.GuildId, 5, out var toGreet);
                                    await GreetUsers(conf, channel, toGreet);
                                }
                            }
                        }
                        else
                        {
                            await GreetUsers(conf, channel, new[] { user });
                        }
                    }
                }

                if (conf.SendDmGreetMessage)
                {
                    var channel = await user.CreateDMChannelAsync().ConfigureAwait(false);

                    if (channel != null) await GreetDmUser(conf, channel, user);
                }
            }
            catch
            {
                // ignored
            }
        }, TaskCreationOptions.LongRunning);
        return Task.CompletedTask;
    }

    public string GetByeMessage(ulong gid)
    {
        using var uow = _db.GetDbContext();
        return uow.ForGuildId(gid, set => set).ChannelByeMessageText;
    }

    public GreetSettings GetOrAddSettingsForGuild(ulong guildId)
    {
        if (GuildConfigsCache.TryGetValue(guildId, out var settings) &&
            settings != null)
        {
            return settings;
        }

        using (var uow = _db.GetDbContext())
        {
            var gc = uow.ForGuildId(guildId, set => set);
            settings = GreetSettings.Create(gc);
        }

        GuildConfigsCache.TryAdd(guildId, settings);
        return settings;
    }

    public async Task<bool> SetSettings(ulong guildId, GreetSettings settings)
    {
        if (settings.AutoDeleteByeMessagesTimer is > 600 or < 0 || settings.AutoDeleteGreetMessagesTimer is > 600 or < 0)
            return false;

        await using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set);
        conf.DmGreetMessageText = settings.DmGreetMessageText?.SanitizeMentions();
        conf.ChannelGreetMessageText = settings.ChannelGreetMessageText?.SanitizeMentions();
        conf.ChannelByeMessageText = settings.ChannelByeMessageText?.SanitizeMentions();

        conf.AutoDeleteGreetMessagesTimer = settings.AutoDeleteGreetMessagesTimer;
        conf.AutoDeleteGreetMessages = settings.AutoDeleteGreetMessagesTimer > 0;

        conf.AutoDeleteByeMessagesTimer = settings.AutoDeleteByeMessagesTimer;
        conf.AutoDeleteByeMessages = settings.AutoDeleteByeMessagesTimer > 0;

        conf.GreetMessageChannelId = settings.GreetMessageChannelId;
        conf.ByeMessageChannelId = settings.ByeMessageChannelId;

        conf.SendChannelGreetMessage = settings.SendChannelGreetMessage;
        conf.SendChannelByeMessage = settings.SendChannelByeMessage;

        await uow.SaveChangesAsync().ConfigureAwait(false);

        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        return true;
    }

    public async Task<bool> SetGreet(ulong guildId, ulong channelId, bool? value = null)
    {
        bool enabled;
        await using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set);
        enabled = conf.SendChannelGreetMessage = value ?? !conf.SendChannelGreetMessage;
        conf.GreetMessageChannelId = channelId;

        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return enabled;
    }

    public bool SetGreetMessage(ulong guildId, ref string message)
    {
        message = message?.SanitizeMentions();

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentNullException(nameof(message));

        bool greetMsgEnabled;
        using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set);
        conf.ChannelGreetMessageText = message;
        greetMsgEnabled = conf.SendChannelGreetMessage;
        _bot.UpdateGuildConfig(guildId, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        uow.SaveChanges();

        return greetMsgEnabled;
    }

    public async Task<bool> SetGreetDm(ulong guildId, bool? value = null)
    {
        bool enabled;
        await using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set);
        enabled = conf.SendDmGreetMessage = value ?? !conf.SendDmGreetMessage;
        _bot.UpdateGuildConfig(guildId, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return enabled;
    }

    public bool SetGreetDmMessage(ulong guildId, ref string message)
    {
        message = message?.SanitizeMentions();

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentNullException(nameof(message));

        bool greetMsgEnabled;
        using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set);
        conf.DmGreetMessageText = message;
        greetMsgEnabled = conf.SendDmGreetMessage;
        _bot.UpdateGuildConfig(guildId, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        uow.SaveChanges();

        return greetMsgEnabled;
    }

    public async Task<bool> SetBye(ulong guildId, ulong channelId, bool? value = null)
    {
        bool enabled;
        await using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set);
        enabled = conf.SendChannelByeMessage = value ?? !conf.SendChannelByeMessage;
        conf.ByeMessageChannelId = channelId;
        _bot.UpdateGuildConfig(guildId, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return enabled;
    }

    public bool SetByeMessage(ulong guildId, ref string message)
    {
        message = message?.SanitizeMentions();

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentNullException(nameof(message));

        bool byeMsgEnabled;
        using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set);
        conf.ChannelByeMessageText = message;
        byeMsgEnabled = conf.SendChannelByeMessage;
        _bot.UpdateGuildConfig(guildId, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        uow.SaveChanges();

        return byeMsgEnabled;
    }

    public async Task SetByeDel(ulong guildId, int timer)
    {
        if (timer is < 0 or > 600)
            return;

        await using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set);
        conf.AutoDeleteByeMessagesTimer = timer;
        _bot.UpdateGuildConfig(guildId, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task SetGreetDel(ulong id, int timer)
    {
        if (timer is < 0 or > 600)
            return;

        await using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(id, set => set);
        conf.AutoDeleteGreetMessagesTimer = timer;
        _bot.UpdateGuildConfig(id, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(id, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    #region Get Enabled Status

    public bool GetGreetDmEnabled(ulong guildId)
    {
        using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set);
        return conf.SendDmGreetMessage;
    }

    public bool GetGreetEnabled(ulong guildId)
    {
        using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set);
        return conf.SendChannelGreetMessage;
    }
    public bool GetBoostEnabled(ulong guildId)
    {
        using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set);
        return conf.SendBoostMessage;
    }

    public bool GetByeEnabled(ulong guildId)
    {
        using var uow = _db.GetDbContext();
        var conf = uow.ForGuildId(guildId, set => set);
        return conf.SendChannelByeMessage;
    }

    #endregion

    #region Test Messages

    public Task ByeTest(ITextChannel channel, IGuildUser user)
    {
        var conf = GetOrAddSettingsForGuild(user.GuildId);
        return ByeUsers(conf, channel, user);
    }

    public Task GreetTest(ITextChannel channel, IGuildUser user)
    {
        var conf = GetOrAddSettingsForGuild(user.GuildId);
        return GreetUsers(conf, channel, user);
    }
    public async Task BoostTest(ITextChannel channel, IGuildUser user)
    {
        var conf = GetOrAddSettingsForGuild(user.GuildId);
        conf.BoostMessageChannelId = channel.Id;
        await TriggerBoostMessage(conf, user as SocketGuildUser);
    }

    public Task<bool> GreetDmTest(IDMChannel channel, IGuildUser user)
    {
        var conf = GetOrAddSettingsForGuild(user.GuildId);
        return GreetDmUser(conf, channel, user);
    }

    #endregion
}

public class GreetSettings
{
    public bool SendBoostMessage { get; set; }
    public string BoostMessage { get; set; }
    public int BoostMessageDeleteAfter { get; set; }
    public ulong BoostMessageChannelId { get; set; }

    public int AutoDeleteGreetMessagesTimer { get; set; }
    public int AutoDeleteByeMessagesTimer { get; set; }

    public ulong GreetMessageChannelId { get; set; }
    public ulong ByeMessageChannelId { get; set; }

    public bool SendDmGreetMessage { get; set; }
    public string DmGreetMessageText { get; set; }

    public bool SendChannelGreetMessage { get; set; }
    public string ChannelGreetMessageText { get; set; }

    public bool SendChannelByeMessage { get; set; }
    public string ChannelByeMessageText { get; set; }

    public static GreetSettings Create(GuildConfig g) =>
        new()
        {
            AutoDeleteByeMessagesTimer = g.AutoDeleteByeMessagesTimer,
            AutoDeleteGreetMessagesTimer = g.AutoDeleteGreetMessagesTimer,
            GreetMessageChannelId = g.GreetMessageChannelId,
            ByeMessageChannelId = g.ByeMessageChannelId,
            SendDmGreetMessage = g.SendDmGreetMessage,
            DmGreetMessageText = g.DmGreetMessageText,
            SendChannelGreetMessage = g.SendChannelGreetMessage,
            ChannelGreetMessageText = g.ChannelGreetMessageText,
            SendChannelByeMessage = g.SendChannelByeMessage,
            ChannelByeMessageText = g.ChannelByeMessageText,
            BoostMessage = g.BoostMessage,
            BoostMessageChannelId = g.BoostMessageChannelId,
            BoostMessageDeleteAfter = g.BoostMessageDeleteAfter,
            SendBoostMessage = g.SendBoostMessage
        };
}