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

    public async Task AddGlobalBan(ulong ToBan, string Reason, ulong addedby, string Type, string proof)
    {
        var toadd = new Mewdeko.Services.Database.Models.GlobalBans
        {
            UserId = ToBan,
            Reason = Reason,
            AddedBy = addedby,
            Type = Type,
            Proof = proof
        };
        var uow = _db.GetDbContext();
        uow.GlobalBans.Add(toadd);
        await uow.SaveChangesAsync();
    }

    public Mewdeko.Services.Database.Models.GlobalBans[] GetAllGlobals()
    {
        var uow = _db.GetDbContext();
        return uow.GlobalBans.AllGlobalBans();
    }
}