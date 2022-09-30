namespace Mewdeko.Modules.StatusRoles.Services;

public class StatusRolesService : INService
{
    private readonly DiscordSocketClient client;
    public readonly DbService Db;

    public StatusRolesService(DiscordSocketClient client, DbService db)
    {
        this.client = client;
        Db = db;
    }
}