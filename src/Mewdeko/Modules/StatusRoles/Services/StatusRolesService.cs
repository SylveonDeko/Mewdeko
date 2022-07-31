namespace Mewdeko.Modules.StatusRoles.Services;

public class StatusRolesService : INService
{
    private readonly DiscordSocketClient _client;
    public readonly DbService _db;

    public StatusRolesService(DiscordSocketClient client, DbService db)
    {
        _client = client;
        _db = db;
    }
}