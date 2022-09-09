using System.Threading.Tasks;

namespace Mewdeko.Modules.Karuta.Services;

public class ShibaKarutaService : INService
{
    private readonly ConcurrentDictionary<ulong, int> _messagesSent = new();
    private readonly ConcurrentDictionary<ulong, int> _messagesSent1 = new();
    private readonly DbService _db;
    private readonly GuildSettingsService _guildSettingsService;
    public ShibaKarutaService(EventHandler handler, DbService db, GuildSettingsService guildSettingsService)
    {
        _db = db;
        _guildSettingsService = guildSettingsService;
        handler.MessageReceived += GrantKarutaRole;
        handler.MessageReceived += GrantKarutaRole1;
    }
    
    private async Task GrantKarutaRole(IMessage arg)

    {
        if (arg.Channel is not ITextChannel channel)

            return;

        if (channel.Id != 940654772070019132 && channel.Id != 809636962599829574)

            return;

        var gUser = arg.Author as SocketGuildUser;

        if (gUser.Roles.Select(x => x.Id).Contains<ulong>(940669747282980954))

            return;

        if (!_messagesSent.TryGetValue(gUser.Id, out var amount) || amount < 2)

            _messagesSent.AddOrUpdate(gUser.Id, amount++, (_, _) => amount++);

        else

        {

            await gUser.AddRoleAsync(940669747282980954);

            _messagesSent.TryRemove(gUser.Id, out _);

        }

    }

    private async Task GrantKarutaRole1(IMessage arg)

    {

        if (arg.Channel is not ITextChannel channel)
            return;

        if (channel.Id is not 952697336570728498 or 954828857985351740 or 952698660179808297)
            return;

        var gUser = arg.Author as SocketGuildUser;

        if (gUser.Roles.Select(x => x.Id).Contains<ulong>(952773926730203146))
            return;

        if (!_messagesSent1.TryGetValue(gUser.Id, out var amount) || amount < 3)

            _messagesSent1.AddOrUpdate(gUser.Id, amount++, (_, _) => amount++);

        else

        {

            await gUser.AddRoleAsync(952773926730203146);

            _messagesSent1.TryRemove(gUser.Id, out _);

        }
    }

    public async Task SetEventChannel(ulong guildId, ulong channelId)
    {
        await using var uow = _db.GetDbContext();

        var gc = await uow.ForGuildId(guildId, set => set);
        gc.KarutaEventChannel = channelId;
        await uow.SaveChangesAsync().ConfigureAwait(false);
        _guildSettingsService.UpdateGuildConfig(guildId, gc);
    }

    public async Task<ulong> GetEventChannel(ulong guildId) 
        => (await _guildSettingsService.GetGuildConfig(guildId)).KarutaEventChannel;

    public async Task SetButtonText(ulong guildId, int buttonNumber, string text)
    {
        await using var uow = _db.GetDbContext();
        switch (buttonNumber)
        {
            case 1:
                var get = uow.KarutaButtonOptions.FirstOrDefault(x => x.GuildId == guildId);
                if (get is null)
                {
                    var toadd = new KarutaButtonOptions
                    {
                        GuildId = guildId,
                        Button1Text = text
                    }
                }
        }
    }
}