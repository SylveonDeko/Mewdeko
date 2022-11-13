using System.Threading.Tasks;
using Serilog;
using SQLitePCL;

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
        //this.eventHandler.PresenceUpdated += EventHandlerOnPresenceUpdated;
    }

    // private async Task EventHandlerOnPresenceUpdated(SocketUser args, SocketPresence args2, SocketPresence args3)
    // {
    //     var status = args3.Activities.FirstOrDefault() as CustomStatusGame;
    //     if (!await cache.SetUserStatusCache(args.Id, status.State.GetHashCode()))
    //         return;
    //     Log.Information($"{status.State} | {status.Details}");
    //     await using var uow = db.GetDbContext();
    //     var statusRolesConfigs = uow.StatusRoles.ToList();
    //     foreach (var i in statusRolesConfigs)
    //     {
    //         var guild = client.GetGuild(i.GuildId);
    //         var users = guild.Users;
    //         var curUser = users.FirstOrDefault(x => x.Id == args.Id);
    //         List<SocketRole> toAdd;
    //         if (curUser is null)
    //             continue;
    //
    //     }
    // }
}