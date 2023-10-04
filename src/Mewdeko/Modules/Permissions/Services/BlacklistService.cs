using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Permissions.Services;

public sealed class BlacklistService : IEarlyBehavior, INService
{
    private readonly DbService db;
    private readonly IPubSub pubSub;
    private readonly DiscordSocketClient client;

    private readonly TypedKey<BlacklistEntry[]> blPubKey = new("blacklist.reload");
    public IList<BlacklistEntry> BlacklistEntries;
    private readonly TypedKey<bool> blPrivKey = new("blacklist.reload.priv");

    public BlacklistService(DbService db, IPubSub pubSub, EventHandler handler, DiscordSocketClient client)
    {
        this.db = db;
        this.pubSub = pubSub;
        this.client = client;
        Reload(false);
        this.pubSub.Sub(blPubKey, OnReload);
        this.pubSub.Sub(blPrivKey, ManualCheck);
        handler.JoinedGuild += CheckBlacklist;
        client.Ready += CheckAllGuilds;
        _ = CheckAllGuilds();
        BlacklistEntries.Add(new BlacklistEntry
        {
            DateAdded = DateTime.Now, ItemId = 967780813741625344, Type = BlacklistType.User
        });
        BlacklistEntries.Add(new BlacklistEntry
        {
            DateAdded = DateTime.UtcNow, ItemId = 930096051900280882, Type = BlacklistType.User
        });
        BlacklistEntries.Add(new BlacklistEntry
        {
            DateAdded = DateTime.UtcNow, ItemId = 767459211373314118, Type = BlacklistType.User
        });
    }

    private ValueTask ManualCheck(bool _)
    {
        CheckAllGuilds();
        return default;
    }

    private Task CheckAllGuilds()
    {
        _ = Task.Run(async () =>
        {
            var guilds = client.Guilds;
            foreach (var guild in guilds)
            {
                if (BlacklistEntries.Select(x => x.ItemId).Contains(guild.Id))
                {
                    await guild.LeaveAsync().ConfigureAwait(false);
                }

                if (!guild.Name.ToLower().Contains("nigger")) continue;
                Blacklist(BlacklistType.Server, guild.Id, "Inappropriate Name");
                await guild.LeaveAsync().ConfigureAwait(false);
            }
        });
        return Task.CompletedTask;
    }

    public Task SendManualCheck()
    {
        this.pubSub.Pub(blPrivKey, true);
        return Task.CompletedTask;
    }

    private async Task CheckBlacklist(IGuild arg)
    {
        var channels = await arg.GetTextChannelsAsync();
        var channel = channels.FirstOrDefault(x => x is not IVoiceChannel);
        if (arg.Name.ToLower().Contains("nigger"))
        {
            Blacklist(BlacklistType.Server, arg.Id, "Inappropriate Name");
            try
            {
                await channel.SendErrorAsync("This server has been blacklisted. Please click the button below to potentially appeal your server ban.").ConfigureAwait(false);
            }
            catch
            {
                Log.Error($"Unable to send blacklist message to {arg.Name}");
            }
            finally
            {
                await arg.LeaveAsync().ConfigureAwait(false);
            }

            await arg.LeaveAsync();
        }

        if (BlacklistEntries.Select(x => x.ItemId).Contains(arg.Id))
        {
            if (channel is null)
            {
                await arg.LeaveAsync().ConfigureAwait(false);
                return;
            }

            try
            {
                await channel.SendErrorAsync("This server has been blacklisted. Please click the button below to potentially appeal your server ban.").ConfigureAwait(false);
            }
            catch
            {
                Log.Error($"Unable to send blacklist message to {arg.Name}");
            }
            finally
            {
                await arg.LeaveAsync().ConfigureAwait(false);
            }

            await arg.LeaveAsync().ConfigureAwait(false);
        }
    }

    public int Priority => -100;

    public ModuleBehaviorType BehaviorType => ModuleBehaviorType.Blocker;

    public Task<bool> RunBehavior(DiscordSocketClient socketClient, IGuild guild, IUserMessage usrMsg)
    {
        foreach (var bl in BlacklistEntries)
        {
            if (guild != null && bl.Type == BlacklistType.Server && bl.ItemId == guild.Id)
                return Task.FromResult(true);

            switch (bl.Type)
            {
                case BlacklistType.Channel when bl.ItemId == usrMsg.Channel.Id:
                    return Task.FromResult(true);
                case BlacklistType.User when bl.ItemId == usrMsg.Author.Id:
                    return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    private ValueTask OnReload(BlacklistEntry[] blacklist)
    {
        BlacklistEntries = blacklist;
        return default;
    }

    public void Reload(bool publish = true)
    {
        using var uow = db.GetDbContext();
        var toPublish = uow.Blacklist.AsNoTracking().ToArray();
        BlacklistEntries = toPublish.ToList();
        if (publish) pubSub.Pub(blPubKey, toPublish);
    }

    public void Blacklist(BlacklistType type, ulong id, string? reason)
    {
        using var uow = db.GetDbContext();
        var item = new BlacklistEntry
        {
            ItemId = id, Type = type, Reason = reason ?? "No reason provided."
        };
        uow.Blacklist.Add(item);
        uow.SaveChanges();

        Reload();
    }

    public void UnBlacklist(BlacklistType type, ulong id)
    {
        using var uow = db.GetDbContext();
        var toRemove = uow.Blacklist
            .FirstOrDefault(bi => bi.ItemId == id && bi.Type == type);

        if (toRemove is not null)
            uow.Blacklist.Remove(toRemove);

        uow.SaveChanges();

        Reload();
    }

    public async void BlacklistUsers(IEnumerable<ulong> toBlacklist)
    {
        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var bc = uow.Blacklist;
            //blacklist the users
            bc.AddRange(toBlacklist.Select(x =>
                new BlacklistEntry
                {
                    ItemId = x, Type = BlacklistType.User
                }));

            //clear their currencies
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        Reload();
    }
}