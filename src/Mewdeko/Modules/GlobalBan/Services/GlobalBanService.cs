using Discord.WebSocket;

namespace Mewdeko.Modules.GlobalBan.Services;

public class GlobalBanService : INService
{
    private readonly DiscordSocketClient _client;
    private readonly DbService _db;

    public GlobalBanService(DiscordSocketClient client, DbService db)
    {
        _client = client;
        _db = db;
    }   

    public async Task AddGlobalBan(ulong toBan, string reason, ulong addedby, string type, string proof)
    {
        var toadd = new global::Mewdeko.Services.Database.Models.GlobalBans
        {
            UserId = toBan,
            Reason = reason,
            AddedBy = addedby,
            Type = type,
            Proof = proof
        };
        var uow = _db.GetDbContext();
        uow.GlobalBans.Add(toadd);
        await uow.SaveChangesAsync();
    }

    public global::Mewdeko.Services.Database.Models.GlobalBans[] GetAllGlobals()
    {
        var uow = _db.GetDbContext();
        return uow.GlobalBans.AllGlobalBans();
    }
}