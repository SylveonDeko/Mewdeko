using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.Webhook;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Replacements;
using Mewdeko.Services.Common;
using Mewdeko.Services.Database.Models;
using Mewdeko.Services.Settings;
using Serilog;

namespace Mewdeko.Services;

public class GreetSettingsService : INService
{
    private readonly BotConfigService _bss;
    private readonly DiscordSocketClient _client;
    private readonly DbService _db;
    private readonly GreetGrouper<IGuildUser> byes = new();

    private readonly GreetGrouper<IGuildUser> greets = new();

    public GreetSettingsService(DiscordSocketClient client, Mewdeko bot, DbService db,
        BotConfigService bss)
    {
        _db = db;
        _client = client;
        _bss = bss;

        GuildConfigsCache = new ConcurrentDictionary<ulong, GreetSettings>(
            bot.AllGuildConfigs
                .ToDictionary(g => g.GuildId, GreetSettings.Create));

        _client.UserJoined += UserJoined;
        _client.UserLeft += UserLeft;

        bot.JoinedGuild += Bot_JoinedGuild;
        _client.LeftGuild += _client_LeftGuild;
        _greethooks = bot.AllGuildConfigs
            .ToDictionary(x => x.GuildId, x => x.GreetHook)
            .ToConcurrent();
        _leavehooks = bot.AllGuildConfigs
            .ToDictionary(x => x.GuildId, x => x.LeaveHook)
            .ToConcurrent();

        _client.MessageReceived += ClientOnGuildMemberUpdated;
    }


    public ConcurrentDictionary<ulong, GreetSettings> GuildConfigsCache { get; }
    private ConcurrentDictionary<ulong, string> _greethooks { get; } = new();
    private ConcurrentDictionary<ulong, string> _leavehooks { get; } = new();
    public bool GroupGreets => _bss.Data.GroupGreets;

    private Func<Task> TriggerBoostMessage(GuildConfig conf, SocketGuildUser user)
    {
        return async () =>
        {
            var channel = user.Guild.GetTextChannel(conf.BoostMessageChannelId);
            if (channel is null)
                return;

            if (string.IsNullOrWhiteSpace(conf.BoostMessage))
                return;
            if (CREmbed.TryParse(conf.BoostMessage, out var embedData))
            {
                var rep = new ReplacementBuilder()
                    .WithDefault(user, channel, user.Guild, _client)
                    .Build();
                rep.Replace(embedData);
                try
                {
                    RestUserMessage toDelete = null;
                    if (embedData.IsEmbedValid)
                        toDelete = await channel.SendMessageAsync(embedData.PlainText ?? "",
                            embed: embedData.ToEmbed().Build());
                    else
                        toDelete = await channel.SendMessageAsync(embedData.PlainText);
                    if (conf.BoostMessageDeleteAfter > 0) toDelete.DeleteAfter(conf.BoostMessageDeleteAfter);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error sending boost message.");
                }
            }
            else
            {
                var rep = new ReplacementBuilder()
                    .WithDefault(user, channel, user.Guild, _client)
                    .Build();
                var msg = rep.Replace(conf.BoostMessage);
                try
                {
                    var toDelete = await channel.SendMessageAsync(msg);

                    if (conf.BoostMessageDeleteAfter > 0) toDelete.DeleteAfter(conf.BoostMessageDeleteAfter);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error sending boost message.");
                }
            }
        };
    }

    private Task ClientOnGuildMemberUpdated(SocketMessage msg)
    {
        if (msg.Channel is not SocketGuildChannel chan) return Task.CompletedTask;
        // if user is a new booster
        // or boosted again the same server
        if (msg.Type != MessageType.UserPremiumGuildSubscription &&
            msg.Type != MessageType.UserPremiumGuildSubscriptionTier1 &&
            msg.Type != MessageType.UserPremiumGuildSubscriptionTier2 &&
            msg.Type != MessageType.UserPremiumGuildSubscriptionTier3) return Task.CompletedTask;
        var uow = _db.GetDbContext();
        var conf = uow.GuildConfigs.ForId(chan.Guild.Id, set => set);
        if (!conf.SendBoostMessage) return Task.CompletedTask;

        _ = Task.Run(TriggerBoostMessage(conf, msg.Author as SocketGuildUser));

        return Task.CompletedTask;
    }

    private Task _client_LeftGuild(SocketGuild arg)
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
        var _ = Task.Run(async () =>
        {
            try
            {
                var user = usr as SocketGuildUser;
                var conf = GetOrAddSettingsForGuild(guild.Id);

                if (!conf.SendChannelByeMessage) return;

                if (guild.Channels.SingleOrDefault(c =>
                        c.Id == conf.ByeMessageChannelId) is not ITextChannel
                    channel) //maybe warn the server owner that the channel is missing
                    return;


                // if group is newly created, greet that user right away,
                // but any user which joins in the next 5 seconds will
                // be greeted in a group greet
                await ByeUsers(conf, channel, new[] {user});
            }
            catch
            {
                // ignored
            }
        });
        return Task.CompletedTask;
    }

    public bool SetBoostMessage(ulong guildId, ref string message)
    {
        message = message?.SanitizeMentions();

        using var uow = _db.GetDbContext();
        var conf = uow.GuildConfigs.ForId(guildId, set => set);
        conf.BoostMessage = message;

        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        uow.SaveChanges();
        return conf.SendBoostMessage;
    }

    public async Task SetBoostDel(ulong guildId, int timer)
    {
        if (timer is < 0 or > 600)
            throw new ArgumentOutOfRangeException(nameof(timer));

        using var uow = _db.GetDbContext();
        var conf = uow.GuildConfigs.ForId(guildId, set => set);
        conf.BoostMessageDeleteAfter = timer;

        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync();
    }

    public string GetBoostMessage(ulong gid)
    {
        using var uow = _db.GetDbContext();
        return uow.GuildConfigs.ForId(gid, set => set).BoostMessage;
    }

    public async Task<bool> SetBoost(ulong guildId, ulong channelId, bool? value = null)
    {
        using var uow = _db.GetDbContext();
        var conf = uow.GuildConfigs.ForId(guildId, set => set);
        var enabled = conf.SendBoostMessage = value ?? !conf.SendBoostMessage;
        conf.BoostMessageChannelId = channelId;

        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (key, old) => toAdd);

        await uow.SaveChangesAsync();

        return enabled;
    }


    public async Task SetWebGreetUrl(IGuild guild, string url)
    {
        using (var uow = _db.GetDbContext())
        {
            var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
            gc.GreetHook = url;
            await uow.SaveChangesAsync();
        }

        _greethooks.AddOrUpdate(guild.Id, url, (key, old) => url);
    }

    public async Task SetWebLeaveUrl(IGuild guild, string url)
    {
        using (var uow = _db.GetDbContext())
        {
            var gc = uow.GuildConfigs.ForId(guild.Id, set => set);
            gc.LeaveHook = url;
            await uow.SaveChangesAsync();
        }

        _leavehooks.AddOrUpdate(guild.Id, url, (key, old) => url);
    }

    public string GetDmGreetMsg(ulong id)
    {
        using var uow = _db.GetDbContext();
        return uow.GuildConfigs.ForId(id, set => set)?.DmGreetMessageText;
    }

    public string GetGreetMsg(ulong gid)
    {
        using var uow = _db.GetDbContext();
        return uow.GuildConfigs.ForId(gid, set => set).ChannelGreetMessageText;
    }

    public string GetGreetHook(ulong? gid)
    {
        _greethooks.TryGetValue(gid.Value, out var snum);
        return snum;
    }

    public string GetLeaveHook(ulong? gid)
    {
        _leavehooks.TryGetValue(gid.Value, out var snum);
        return snum;
    }

    private Task ByeUsers(GreetSettings conf, ITextChannel channel, IUser user)
    {
        return ByeUsers(conf, channel, new[] {user});
    }

    private async Task ByeUsers(GreetSettings conf, ITextChannel channel, IEnumerable<IUser> users)
    {
        if (!users.Any())
            return;

        var rep = new ReplacementBuilder()
            .WithChannel(channel)
            .WithClient(_client)
            .WithServer(_client, (SocketGuild) channel.Guild)
            .WithManyUsers(users)
            .Build();
        var lh = GetLeaveHook(channel.GuildId);

        if (CREmbed.TryParse(conf.ChannelByeMessageText, out var embedData))
        {
            rep.Replace(embedData);
            try
            {
                if (string.IsNullOrEmpty(lh) || lh == 0.ToString())
                {
                    var toDelete = await channel.EmbedAsync(embedData).ConfigureAwait(false);
                    if (conf.AutoDeleteByeMessagesTimer > 0) toDelete.DeleteAfter(conf.AutoDeleteByeMessagesTimer);
                }
                else
                {
                    var webhook = new DiscordWebhookClient(GetLeaveHook(channel.GuildId));
                    var embeds = new List<Embed>();
                    embeds.Add(embedData.ToEmbed().Build());
                    var toDelete = await webhook.SendMessageAsync(embedData.PlainText, embeds: embeds)
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

    private Task GreetUsers(GreetSettings conf, ITextChannel channel, IGuildUser user)
    {
        return GreetUsers(conf, channel, new[] {user});
    }

    private async Task GreetUsers(GreetSettings conf, ITextChannel channel, IEnumerable<IGuildUser> users)
    {
        if (!users.Any())
            return;

        var rep = new ReplacementBuilder()
            .WithChannel(channel)
            .WithClient(_client)
            .WithServer(_client, (SocketGuild) channel.Guild)
            .WithManyUsers(users)
            .Build();
        var gh = GetGreetHook(channel.GuildId);
        if (CREmbed.TryParse(conf.ChannelGreetMessageText, out var embedData))
        {
            rep.Replace(embedData);
            try
            {
                if (string.IsNullOrEmpty(gh) || gh == 0.ToString())
                {
                    var toDelete = await channel.EmbedAsync(embedData).ConfigureAwait(false);
                    if (conf.AutoDeleteGreetMessagesTimer > 0)
                        toDelete.DeleteAfter(conf.AutoDeleteGreetMessagesTimer);
                }
                else
                {
                    var webhook = new DiscordWebhookClient(GetGreetHook(channel.GuildId));
                    var embeds = new List<Embed> {embedData.ToEmbed().Build()};
                    var toDelete = await webhook.SendMessageAsync(embedData.PlainText, embeds: embeds)
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

    private async Task<bool> GreetDmUser(GreetSettings conf, IDMChannel channel, IGuildUser user)
    {
        var rep = new ReplacementBuilder()
            .WithDefault(user, channel, (SocketGuild) user.Guild, _client)
            .Build();

        if (CREmbed.TryParse(conf.DmGreetMessageText, out var embedData))
        {
            rep.Replace(embedData);
            try
            {
                await channel.EmbedAsync(embedData).ConfigureAwait(false);
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
        var _ = Task.Run(async () =>
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
                            if (greets.CreateOrAdd(user.GuildId, user))
                            {
                                // greet single user
                                await GreetUsers(conf, channel, new[] {user});
                                var groupClear = false;
                                while (!groupClear)
                                {
                                    await Task.Delay(5000).ConfigureAwait(false);
                                    groupClear = greets.ClearGroup(user.GuildId, 5, out var toGreet);
                                    await GreetUsers(conf, channel, toGreet);
                                }
                            }
                        }
                        else
                        {
                            await GreetUsers(conf, channel, new[] {user});
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
        });
        return Task.CompletedTask;
    }

    public string GetByeMessage(ulong gid)
    {
        using var uow = _db.GetDbContext();
        return uow.GuildConfigs.ForId(gid, set => set).ChannelByeMessageText;
    }

    public GreetSettings GetOrAddSettingsForGuild(ulong guildId)
    {
        if (GuildConfigsCache.TryGetValue(guildId, out var settings) &&
            settings != null)
            return settings;

        using (var uow = _db.GetDbContext())
        {
            var gc = uow.GuildConfigs.ForId(guildId, set => set);
            settings = GreetSettings.Create(gc);
        }

        GuildConfigsCache.TryAdd(guildId, settings);
        return settings;
    }

    public async Task<bool> SetSettings(ulong guildId, GreetSettings settings)
    {
        if (settings.AutoDeleteByeMessagesTimer > 600 ||
            settings.AutoDeleteByeMessagesTimer < 0 ||
            settings.AutoDeleteGreetMessagesTimer > 600 ||
            settings.AutoDeleteGreetMessagesTimer < 0)
            return false;

        using var uow = _db.GetDbContext();
        var conf = uow.GuildConfigs.ForId(guildId, set => set);
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

        await uow.SaveChangesAsync();

        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (key, old) => toAdd);

        return true;
    }

    public async Task<bool> SetGreet(ulong guildId, ulong channelId, bool? value = null)
    {
        bool enabled;
        using var uow = _db.GetDbContext();
        var conf = uow.GuildConfigs.ForId(guildId, set => set);
        enabled = conf.SendChannelGreetMessage = value ?? !conf.SendChannelGreetMessage;
        conf.GreetMessageChannelId = channelId;

        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (key, old) => toAdd);

        await uow.SaveChangesAsync();

        return enabled;
    }

    public bool SetGreetMessage(ulong guildId, ref string message)
    {
        message = message?.SanitizeMentions();

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentNullException(nameof(message));

        bool greetMsgEnabled;
        using var uow = _db.GetDbContext();
        var conf = uow.GuildConfigs.ForId(guildId, set => set);
        conf.ChannelGreetMessageText = message;
        greetMsgEnabled = conf.SendChannelGreetMessage;

        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (key, old) => toAdd);

        uow.SaveChanges();

        return greetMsgEnabled;
    }

    public async Task<bool> SetGreetDm(ulong guildId, bool? value = null)
    {
        bool enabled;
        using var uow = _db.GetDbContext();
        var conf = uow.GuildConfigs.ForId(guildId, set => set);
        enabled = conf.SendDmGreetMessage = value ?? !conf.SendDmGreetMessage;

        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (key, old) => toAdd);

        await uow.SaveChangesAsync();

        return enabled;
    }

    public bool SetGreetDmMessage(ulong guildId, ref string message)
    {
        message = message?.SanitizeMentions();

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentNullException(nameof(message));

        bool greetMsgEnabled;
        using var uow = _db.GetDbContext();
        var conf = uow.GuildConfigs.ForId(guildId, set => set);
        conf.DmGreetMessageText = message;
        greetMsgEnabled = conf.SendDmGreetMessage;

        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (key, old) => toAdd);

        uow.SaveChanges();

        return greetMsgEnabled;
    }

    public async Task<bool> SetBye(ulong guildId, ulong channelId, bool? value = null)
    {
        bool enabled;
        using var uow = _db.GetDbContext();
        var conf = uow.GuildConfigs.ForId(guildId, set => set);
        enabled = conf.SendChannelByeMessage = value ?? !conf.SendChannelByeMessage;
        conf.ByeMessageChannelId = channelId;

        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (key, old) => toAdd);

        await uow.SaveChangesAsync();

        return enabled;
    }

    public bool SetByeMessage(ulong guildId, ref string message)
    {
        message = message?.SanitizeMentions();

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentNullException(nameof(message));

        bool byeMsgEnabled;
        using var uow = _db.GetDbContext();
        var conf = uow.GuildConfigs.ForId(guildId, set => set);
        conf.ChannelByeMessageText = message;
        byeMsgEnabled = conf.SendChannelByeMessage;

        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (key, old) => toAdd);

        uow.SaveChanges();

        return byeMsgEnabled;
    }

    public async Task SetByeDel(ulong guildId, int timer)
    {
        if (timer < 0 || timer > 600)
            return;

        using var uow = _db.GetDbContext();
        var conf = uow.GuildConfigs.ForId(guildId, set => set);
        conf.AutoDeleteByeMessagesTimer = timer;

        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (key, old) => toAdd);

        await uow.SaveChangesAsync();
    }

    public async Task SetGreetDel(ulong id, int timer)
    {
        if (timer < 0 || timer > 600)
            return;

        using var uow = _db.GetDbContext();
        var conf = uow.GuildConfigs.ForId(id, set => set);
        conf.AutoDeleteGreetMessagesTimer = timer;

        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(id, toAdd, (key, old) => toAdd);

        await uow.SaveChangesAsync();
    }

    #region Get Enabled Status

    public bool GetGreetDmEnabled(ulong guildId)
    {
        using var uow = _db.GetDbContext();
        var conf = uow.GuildConfigs.ForId(guildId, set => set);
        return conf.SendDmGreetMessage;
    }

    public bool GetGreetEnabled(ulong guildId)
    {
        using var uow = _db.GetDbContext();
        var conf = uow.GuildConfigs.ForId(guildId, set => set);
        return conf.SendChannelGreetMessage;
    }

    public bool GetByeEnabled(ulong guildId)
    {
        using var uow = _db.GetDbContext();
        var conf = uow.GuildConfigs.ForId(guildId, set => set);
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

    public static GreetSettings Create(GuildConfig g)
    {
        return new GreetSettings
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
            ChannelByeMessageText = g.ChannelByeMessageText
        };
    }
}