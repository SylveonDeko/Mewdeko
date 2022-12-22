using System.Threading.Tasks;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Common;
using Mewdeko.Services.Settings;
using Serilog;

namespace Mewdeko.Services;

public class GreetSettingsService : INService, IReadyExecutor
{
    private readonly BotConfigService bss;
    private readonly DiscordSocketClient client;
    private readonly DbService db;
    private readonly GuildSettingsService gss;

    private readonly GreetGrouper<IGuildUser> greets = new();

    public GreetSettingsService(DiscordSocketClient client, GuildSettingsService gss, DbService db,
        BotConfigService bss, EventHandler eventHandler)
    {
        this.db = db;
        this.client = client;
        this.gss = gss;
        this.bss = bss;
        using var uow = db.GetDbContext();
        var gc = uow.GuildConfigs.Where(x => this.client.Guilds.Select(socketGuild => socketGuild.Id).Contains(x.GuildId));
        GuildConfigsCache = new ConcurrentDictionary<ulong, GreetSettings>(
            gc
                .ToDictionary(g => g.GuildId, GreetSettings.Create));

        eventHandler.UserJoined += UserJoined;
        eventHandler.UserLeft += UserLeft;

        client.JoinedGuild += Bot_JoinedGuild;
        this.client.LeftGuild += Client_LeftGuild;

        eventHandler.GuildMemberUpdated += ClientOnGuildMemberUpdated;
    }

    private readonly Channel<(GreetSettings, IGuildUser, TaskCompletionSource<bool>)> greetDmQueue =
        Channel.CreateBounded<(GreetSettings, IGuildUser, TaskCompletionSource<bool>)>(new BoundedChannelOptions(60)
        {
            // The limit of 60 users should be only hit when there's a raid. In that case
            // probably the best thing to do is to drop newest (raiding) users
            FullMode = BoundedChannelFullMode.DropNewest
        });

    private async Task<bool> GreetDmUser(GreetSettings conf, IGuildUser user)
    {
        var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await greetDmQueue.Writer.WriteAsync((conf, user, completionSource));
        return await completionSource.Task;
    }

    public async Task OnReadyAsync()
    {
        while (true)
        {
            try
            {
                var (conf, user, compl) = await greetDmQueue.Reader.ReadAsync();
                var res = await GreetDmUserInternal(conf, user);
                compl.TrySetResult(res);
                await Task.Delay(5000);
            }
            catch
            {
                // ignored
            }
        }
    }

    public ConcurrentDictionary<ulong, GreetSettings?> GuildConfigsCache { get; }
    public bool GroupGreets => bss.Data.GroupGreets;

    private async Task TriggerBoostMessage(GreetSettings conf, SocketGuildUser user)
    {
        var chan = user.Guild.GetTextChannel(conf.BoostMessageChannelId);

        if (chan is null)
            return;

        if (string.IsNullOrWhiteSpace(conf.BoostMessage))
            return;
        var rep = new ReplacementBuilder()
            .WithDefault(user, chan, user.Guild, client)
            .Build();
        if (SmartEmbed.TryParse(rep.Replace(conf.BoostMessage), user.Guild?.Id, out var embed, out var plainText, out var components))
        {
            try
            {
                var toDelete = await chan.SendMessageAsync(plainText, embeds: embed, components: components?.Build()).ConfigureAwait(false);
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
                var toDelete = await chan.SendMessageAsync(msg).ConfigureAwait(false);

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
        _ = Task.Run(async () =>
        {
            // if user is a new booster
            // or boosted again the same server
            if ((optOldUser.Value is not { PremiumSince: null } || newUser is not { PremiumSince: not null })
                && (optOldUser.Value?.PremiumSince is not { } oldDate || newUser.PremiumSince is not { } newDate || newDate <= oldDate))
            {
                return;
            }

            var conf = await GetOrAddSettingsForGuild(newUser.Guild.Id);
            if (!conf.SendBoostMessage)
                return;

            await TriggerBoostMessage(conf, newUser).ConfigureAwait(false);
        });
        return Task.CompletedTask;
    }

    private Task Client_LeftGuild(SocketGuild arg)
    {
        GuildConfigsCache.TryRemove(arg.Id, out _);
        return Task.CompletedTask;
    }

    private Task Bot_JoinedGuild(IGuild guild)
    {
        _ = Task.Run(async () =>
        {
            GuildConfigsCache.AddOrUpdate(guild.Id, GreetSettings.Create(await gss.GetGuildConfig(guild.Id)),
                delegate { return GreetSettings.Create(gss.GetGuildConfig(guild.Id).GetAwaiter().GetResult()); });
        });

        return Task.CompletedTask;
    }

    private async Task UserLeft(IGuild guild, IUser usr)
    {
        try
        {
            var user = usr as SocketGuildUser;
            var conf = await GetOrAddSettingsForGuild(guild.Id);

            if (!conf.SendChannelByeMessage) return;

            if ((await guild.GetTextChannelsAsync()).SingleOrDefault(c =>
                    c.Id == conf.ByeMessageChannelId) is not { } channel) //maybe warn the server owner that the channel is missing
            {
                return;
            }

            // if group is newly created, greet that user right away,
            // but any user which joins in the next 5 seconds will
            // be greeted in a group greet
            await ByeUsers(conf, channel, new[]
            {
                user
            }).ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }
    }

    public async Task<bool> SetBoostMessage(ulong guildId, string? message)
    {
        message = message?.SanitizeMentions();

        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set);
        conf.BoostMessage = message;

        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);
        gss.UpdateGuildConfig(guildId, conf);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        return conf.SendBoostMessage;
    }

    public async Task SetBoostDel(ulong guildId, int timer)
    {
        if (timer is < 0 or > 600)
            throw new ArgumentOutOfRangeException(nameof(timer));

        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set);
        conf.BoostMessageDeleteAfter = timer;
        gss.UpdateGuildConfig(guildId, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<string> GetBoostMessage(ulong gid)
        => (await gss.GetGuildConfig(gid)).BoostMessage;

    public async Task<bool> SetBoost(ulong guildId, ulong channelId, bool? value = null)
    {
        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set);
        var enabled = conf.SendBoostMessage = value ?? !conf.SendBoostMessage;
        conf.BoostMessageChannelId = channelId;
        gss.UpdateGuildConfig(guildId, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return enabled;
    }

    public async Task SetWebGreetUrl(IGuild guild, string url)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.GreetHook = url;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        gss.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task SetWebLeaveUrl(IGuild guild, string url)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.LeaveHook = url;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        gss.UpdateGuildConfig(guild.Id, gc);
    }

    public async Task<string> GetDmGreetMsg(ulong id)
    {
        await using var uow = db.GetDbContext();
        return (await uow.ForGuildId(id, set => set)).DmGreetMessageText;
    }

    public async Task<string> GetGreetMsg(ulong gid)
    {
        await using var uow = db.GetDbContext();
        return (await uow.ForGuildId(gid, set => set)).ChannelGreetMessageText;
    }

    public async Task<string> GetGreetHook(ulong? gid)
        => (await gss.GetGuildConfig(gid.Value)).GreetHook;

    public async Task<string> GetLeaveHook(ulong? gid)
        => (await gss.GetGuildConfig(gid.Value)).LeaveHook;

    private Task ByeUsers(GreetSettings conf, ITextChannel channel, IUser user) => ByeUsers(conf, channel, new[]
    {
        user
    });

    private async Task ByeUsers(GreetSettings conf, ITextChannel channel, IEnumerable<IUser> users)
    {
        if (!users.Any())
            return;

        var rep = new ReplacementBuilder()
            .WithChannel(channel)
            .WithClient(client)
            .WithServer(client, (SocketGuild)channel.Guild)
            .WithManyUsers(users)
            .Build();
        var lh = await GetLeaveHook(channel.GuildId);

        if (SmartEmbed.TryParse(rep.Replace(conf.ChannelByeMessageText), channel.GuildId, out var embed, out var plainText, out var components))
        {
            try
            {
                if (string.IsNullOrEmpty(lh) || lh == 0.ToString())
                {
                    var toDelete = await channel.SendMessageAsync(plainText, embeds: embed, components: components?.Build()).ConfigureAwait(false);
                    if (conf.AutoDeleteByeMessagesTimer > 0) toDelete.DeleteAfter(conf.AutoDeleteByeMessagesTimer);
                }
                else
                {
                    var webhook = new DiscordWebhookClient(await GetLeaveHook(channel.GuildId));
                    var toDelete = await webhook.SendMessageAsync(plainText, embeds: embed, components: components?.Build())
                        .ConfigureAwait(false);
                    if (conf.AutoDeleteByeMessagesTimer > 0)
                    {
                        var msg = await channel.GetMessageAsync(toDelete).ConfigureAwait(false) as IUserMessage;
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
                    var webhook = new DiscordWebhookClient(await GetLeaveHook(channel.GuildId));
                    var toDel = await webhook.SendMessageAsync(msg.SanitizeMentions()).ConfigureAwait(false);
                    if (conf.AutoDeleteByeMessagesTimer > 0)
                    {
                        var msg2 = await channel.GetMessageAsync(toDel).ConfigureAwait(false) as IUserMessage;
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

    private Task GreetUsers(GreetSettings conf, ITextChannel channel, IGuildUser user) => GreetUsers(conf, channel, new[]
    {
        user
    });

    private async Task GreetUsers(GreetSettings conf, ITextChannel channel, IEnumerable<IGuildUser> users)
    {
        if (!users.Any())
            return;

        var rep = new ReplacementBuilder()
            .WithChannel(channel)
            .WithClient(client)
            .WithServer(client, (SocketGuild)channel.Guild)
            .WithManyUsers(users)
            .Build();
        var gh = await GetGreetHook(channel.GuildId);
        if (SmartEmbed.TryParse(rep.Replace(conf.ChannelGreetMessageText), channel.GuildId, out var embed, out var plainText, out var components))
        {
            try
            {
                if (string.IsNullOrEmpty(gh) || gh == 0.ToString())
                {
                    var toDelete = await channel
                        .SendMessageAsync(plainText, embeds: embed,
                            components: components?.Build()).ConfigureAwait(false);
                    if (conf.AutoDeleteGreetMessagesTimer > 0)
                        toDelete.DeleteAfter(conf.AutoDeleteGreetMessagesTimer);
                }
                else
                {
                    var webhook = new DiscordWebhookClient(await GetGreetHook(channel.GuildId));
                    var toDelete = await webhook
                        .SendMessageAsync(plainText, embeds: embed, components: components?.Build())
                        .ConfigureAwait(false);
                    if (conf.AutoDeleteGreetMessagesTimer > 0)
                    {
                        var msg = await channel.GetMessageAsync(toDelete).ConfigureAwait(false) as IUserMessage;
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
                        var webhook = new DiscordWebhookClient(await GetGreetHook(channel.GuildId));
                        var toDel = await webhook.SendMessageAsync(msg.SanitizeMentions()).ConfigureAwait(false);
                        if (conf.AutoDeleteGreetMessagesTimer > 0)
                        {
                            var msg2 = await channel.GetMessageAsync(toDel).ConfigureAwait(false) as IUserMessage;
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

    private async Task<bool> GreetDmUserInternal(GreetSettings conf, IGuildUser user)
    {
        if (!conf.SendDmGreetMessage)
            return false;

        var channel = await user.CreateDMChannelAsync();

        var rep = new ReplacementBuilder()
            .WithDefault(user, channel, (SocketGuild)user.Guild, client)
            .Build();

        if (SmartEmbed.TryParse(rep.Replace(conf.DmGreetMessageText), user.GuildId, out var embed, out var plainText, out var components))
        {
            try
            {
                await channel.SendMessageAsync(plainText, embeds: embed, components: components?.Build()).ConfigureAwait(false);
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
        _ = Task.Run(async () =>
        {
            try
            {
                var conf = await GetOrAddSettingsForGuild(user.GuildId);

                if (conf.SendChannelGreetMessage)
                {
                    var channel = await user.Guild.GetTextChannelAsync(conf.GreetMessageChannelId).ConfigureAwait(false);
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
                                await GreetUsers(conf, channel, new[]
                                {
                                    user
                                }).ConfigureAwait(false);
                                var groupClear = false;
                                while (!groupClear)
                                {
                                    await Task.Delay(5000).ConfigureAwait(false);
                                    groupClear = greets.ClearGroup(user.GuildId, 5, out var toGreet);
                                    await GreetUsers(conf, channel, toGreet).ConfigureAwait(false);
                                }
                            }
                        }
                        else
                        {
                            await GreetUsers(conf, channel, new[]
                            {
                                user
                            }).ConfigureAwait(false);
                        }
                    }
                }

                if (conf.SendDmGreetMessage)
                {
                    var channel = await user.CreateDMChannelAsync().ConfigureAwait(false);

                    if (channel != null) await GreetDmUser(conf, user).ConfigureAwait(false);
                }
            }
            catch
            {
                // ignored
            }
        });
        return Task.CompletedTask;
    }

    public async Task<string> GetByeMessage(ulong gid)
    {
        await using var uow = db.GetDbContext();
        return (await uow.ForGuildId(gid, set => set)).ChannelByeMessageText;
    }

    public async Task<GreetSettings> GetOrAddSettingsForGuild(ulong guildId)
    {
        if (GuildConfigsCache.TryGetValue(guildId, out var settings) &&
            settings != null)
        {
            return settings;
        }

        await using (var uow = db.GetDbContext())
        {
            var gc = await uow.ForGuildId(guildId, set => set);
            settings = GreetSettings.Create(gc);
        }

        GuildConfigsCache.TryAdd(guildId, settings);
        return settings;
    }

    public async Task<bool> SetSettings(ulong guildId, GreetSettings settings)
    {
        if (settings.AutoDeleteByeMessagesTimer is > 600 or < 0 || settings.AutoDeleteGreetMessagesTimer is > 600 or < 0)
            return false;

        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set);
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
        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set);
        var enabled = conf.SendChannelGreetMessage = value ?? !conf.SendChannelGreetMessage;
        conf.GreetMessageChannelId = channelId;

        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return enabled;
    }

    public async Task<bool> SetGreetMessage(ulong guildId, string? message)
    {
        message = message?.SanitizeMentions();

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentNullException(nameof(message));

        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set);
        conf.ChannelGreetMessageText = message;
        var greetMsgEnabled = conf.SendChannelGreetMessage;
        gss.UpdateGuildConfig(guildId, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return greetMsgEnabled;
    }

    public async Task<bool> SetGreetDm(ulong guildId, bool? value = null)
    {
        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set);
        var enabled = conf.SendDmGreetMessage = value ?? !conf.SendDmGreetMessage;
        gss.UpdateGuildConfig(guildId, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return enabled;
    }

    public async Task<bool> SetGreetDmMessage(ulong guildId, string? message)
    {
        message = message?.SanitizeMentions();

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentNullException(nameof(message));

        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set);
        conf.DmGreetMessageText = message;
        var greetMsgEnabled = conf.SendDmGreetMessage;
        gss.UpdateGuildConfig(guildId, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return greetMsgEnabled;
    }

    public async Task<bool> SetBye(ulong guildId, ulong channelId, bool? value = null)
    {
        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set);
        var enabled = conf.SendChannelByeMessage = value ?? !conf.SendChannelByeMessage;
        conf.ByeMessageChannelId = channelId;
        gss.UpdateGuildConfig(guildId, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return enabled;
    }

    public async Task<bool> SetByeMessage(ulong guildId, string? message)
    {
        message = message?.SanitizeMentions();

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentNullException(nameof(message));

        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set);
        conf.ChannelByeMessageText = message;
        var byeMsgEnabled = conf.SendChannelByeMessage;
        gss.UpdateGuildConfig(guildId, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync().ConfigureAwait(false);

        return byeMsgEnabled;
    }

    public async Task SetByeDel(ulong guildId, int timer)
    {
        if (timer is < 0 or > 600)
            return;

        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set);
        conf.AutoDeleteByeMessagesTimer = timer;
        gss.UpdateGuildConfig(guildId, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(guildId, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task SetGreetDel(ulong id, int timer)
    {
        if (timer is < 0 or > 600)
            return;

        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(id, set => set);
        conf.AutoDeleteGreetMessagesTimer = timer;
        gss.UpdateGuildConfig(id, conf);
        var toAdd = GreetSettings.Create(conf);
        GuildConfigsCache.AddOrUpdate(id, toAdd, (_, _) => toAdd);

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    #region Get Enabled Status

    public async Task<bool> GetGreetDmEnabled(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set);
        return conf.SendDmGreetMessage;
    }

    public async Task<bool> GetGreetEnabled(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set);
        return conf.SendChannelGreetMessage;
    }

    public async Task<bool> GetBoostEnabled(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set);
        return conf.SendBoostMessage;
    }

    public async Task<bool> GetByeEnabled(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var conf = await uow.ForGuildId(guildId, set => set);
        return conf.SendChannelByeMessage;
    }

    #endregion

    #region Test Messages

    public async Task ByeTest(ITextChannel channel, IGuildUser user)
    {
        var conf = await GetOrAddSettingsForGuild(user.GuildId);
        await ByeUsers(conf, channel, user);
    }

    public async Task GreetTest(ITextChannel channel, IGuildUser user)
    {
        var conf = await GetOrAddSettingsForGuild(user.GuildId);
        await GreetUsers(conf, channel, user);
    }

    public async Task BoostTest(ITextChannel channel, IGuildUser user)
    {
        var conf = await GetOrAddSettingsForGuild(user.GuildId);
        conf.BoostMessageChannelId = channel.Id;
        await TriggerBoostMessage(conf, user as SocketGuildUser).ConfigureAwait(false);
    }

    public async Task<bool> GreetDmTest(IDMChannel channel, IGuildUser user)
    {
        var conf = await GetOrAddSettingsForGuild(user.GuildId);
        return await GreetDmUser(conf, user);
    }

    #endregion
}

public class GreetSettings
{
    public bool SendBoostMessage { get; set; }
    public string? BoostMessage { get; set; }
    public int BoostMessageDeleteAfter { get; set; }
    public ulong BoostMessageChannelId { get; set; }

    public int AutoDeleteGreetMessagesTimer { get; set; }
    public int AutoDeleteByeMessagesTimer { get; set; }

    public ulong GreetMessageChannelId { get; set; }
    public ulong ByeMessageChannelId { get; set; }

    public bool SendDmGreetMessage { get; set; }
    public string? DmGreetMessageText { get; set; }

    public bool SendChannelGreetMessage { get; set; }
    public string? ChannelGreetMessageText { get; set; }

    public bool SendChannelByeMessage { get; set; }
    public string? ChannelByeMessageText { get; set; }

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