using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Permissions.Services;

public sealed class BlacklistService : IEarlyBehavior, INService
{
    private readonly DbService _db;
    private readonly IPubSub _pubSub;

    private readonly TypedKey<BlacklistEntry[]> _blPubKey = new("blacklist.reload");
    public IList<BlacklistEntry> BlacklistEntries;

    public BlacklistService(DbService db, IPubSub pubSub, DiscordSocketClient client)
    {
        _db = db;
        _pubSub = pubSub;

        Reload(false);
        _pubSub.Sub(_blPubKey, OnReload);
        client.JoinedGuild += CheckBlacklist;
        BlacklistEntries.Add(new BlacklistEntry
        {
            DateAdded = DateTime.Now,
            ItemId = 967780813741625344,
            Type = BlacklistType.User
        });
        BlacklistEntries.Add(new BlacklistEntry
        {
            DateAdded = DateTime.UtcNow,
            ItemId = 930096051900280882,
            Type = BlacklistType.User
        });
        BlacklistEntries.Add(new BlacklistEntry
        {
            DateAdded = DateTime.UtcNow,
            ItemId = 767459211373314118,
            Type = BlacklistType.User
        });
    }

    private Task CheckBlacklist(SocketGuild arg)
    {
        _ = Task.Run(async () =>
        {
            if (BlacklistEntries.Select(x => x.ItemId).Contains(arg.Id))
            {
                var channel = arg.DefaultChannel;
                if (channel is null)
                {
                    await arg.LeaveAsync().ConfigureAwait(false);
                    return;
                }

                await channel.SendErrorAsync("This server has been blacklisted. Please click the button below to potentially appeal your server ban.").ConfigureAwait(false);
                await arg.LeaveAsync().ConfigureAwait(false);
            }
        });
        return Task.CompletedTask;
    }

    public int Priority => -100;

    public ModuleBehaviorType BehaviorType => ModuleBehaviorType.Blocker;

    public Task<bool> RunBehavior(DiscordSocketClient _, IGuild guild, IUserMessage usrMsg)
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
        using var uow = _db.GetDbContext();
        var toPublish = uow.Blacklist.AsNoTracking().ToArray();
        BlacklistEntries = toPublish.ToList();
        if (publish) _pubSub.Pub(_blPubKey, toPublish);
    }

    public void Blacklist(BlacklistType type, ulong id, string? reason)
    {
        using var uow = _db.GetDbContext();
        var item = new BlacklistEntry { ItemId = id, Type = type, Reason = reason ?? "No reason provided." };
        uow.Blacklist.Add(item);
        uow.SaveChanges();

        Reload();
    }

    public void UnBlacklist(BlacklistType type, ulong id)
    {
        using var uow = _db.GetDbContext();
        var toRemove = uow.Blacklist
            .FirstOrDefault(bi => bi.ItemId == id && bi.Type == type);

        if (toRemove is not null)
            uow.Blacklist.Remove(toRemove);

        uow.SaveChanges();

        Reload();
    }

    public async void BlacklistUsers(IEnumerable<ulong> toBlacklist)
    {
        var uow = _db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var bc = uow.Blacklist;
            //blacklist the users
            bc.AddRange(toBlacklist.Select(x =>
                new BlacklistEntry
                {
                    ItemId = x,
                    Type = BlacklistType.User
                }));

            //clear their currencies
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        Reload();
    }
}