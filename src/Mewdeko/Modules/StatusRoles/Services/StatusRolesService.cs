using System.Threading.Tasks;
using Serilog;

namespace Mewdeko.Modules.StatusRoles.Services;

public class StatusRolesService : INService
{
    private readonly DiscordSocketClient client;
    private readonly DbService db;
    private readonly IDataCache cache;
    private readonly EventHandler eventHandler;

    public StatusRolesService(DiscordSocketClient client, DbService db, EventHandler eventHandler, IDataCache cache)
    {
        this.client = client;
        this.db = db;
        this.eventHandler = eventHandler;
        this.cache = cache;
        this.eventHandler.PresenceUpdated += EventHandlerOnPresenceUpdated;
    }

    private async Task EventHandlerOnPresenceUpdated(SocketUser args, SocketPresence args2, SocketPresence args3)
    {
        var status = args3.Activities.FirstOrDefault() as CustomStatusGame;
        if (!await cache.SetUserStatusCache(args.Id, status.State.GetHashCode()))
            return;
        await using var uow = db.GetDbContext();
        var statusRolesConfigs = uow.StatusRoles.ToList();
        foreach (var i in statusRolesConfigs)
        {
            if (!status.State.Contains(i.Status))
                continue;
            var guild = client.GetGuild(i.GuildId);
            var addedCount = 0;
            var users = guild.Users;
            var curUser = users.FirstOrDefault(x => x.Id == args.Id);
            var toAdd = new List<SocketRole>();
            var toRemove = new List<SocketRole>();
            if (!string.IsNullOrWhiteSpace(i.ToAdd))
            {
                var newRoleCollection = i.ToAdd.Split(" ").Select(ulong.Parse);
                toAdd = newRoleCollection.Select(x => guild.GetRole(x)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(i.ToRemove))
            {
                var newRoleCollection = i.ToRemove.Split(" ").Select(ulong.Parse);
                toRemove = newRoleCollection.Select(x => guild.GetRole(x)).ToList();
            }

            if (toAdd.Any())
            {
                try
                {
                    await curUser.AddRolesAsync(toAdd);
                }
                catch
                {
                    Log.Error($"Invalid permissions or not high enough to add status roles in {guild}.");
                }
            }

            if (toRemove.Any())
            {
                try
                {
                    await curUser.RemoveRolesAsync(toRemove);
                }
                catch
                {
                    Log.Error($"Invalid permissions or not high enough to remove status roles in {guild}.");
                }
            }

            if (SmartEmbed.TryParse(i.StatusEmbed, guild.Id, out var embeds, out var plainText, out var components))
            {
                var rep = new ReplacementBuilder().Build();
            }
        }
    }
}