using Mewdeko.Modules.Utility.Common;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Utility.Services;

public class MessageRepeaterService : INService
{
    private readonly DiscordSocketClient _client;
    private readonly IBotCredentials _creds;
    private readonly DbService _db;

    public MessageRepeaterService(DiscordSocketClient client, DbService db,
        IBotCredentials creds, Mewdeko bot)
    {
        _db = db;
        _creds = creds;
        _client = client;
        _ = OnReadyAsync(client, bot);
    }

    public ConcurrentDictionary<ulong, ConcurrentDictionary<int, RepeatRunner>> Repeaters { get; set; }
    public bool RepeaterReady { get; private set; }

    public async Task OnReadyAsync(DiscordSocketClient client, Mewdeko bot)
    {
        await bot.Ready.Task.ConfigureAwait(false);
        Log.Information("Loading message repeaters on shard {ShardId}.", _client.ShardId);
        await using var uow = _db.GetDbContext();
        var gcs = uow.GuildConfigs.All().Where(x => client.Guilds.Select(socketGuild => socketGuild.Id).Contains(x.GuildId));
        var repeaters = new Dictionary<ulong, ConcurrentDictionary<int, RepeatRunner>>();
        foreach (var gc in gcs.Where(gc => (gc.GuildId >> 22) % (ulong)_creds.TotalShards == (ulong)_client.ShardId))
        {
            try
            {
                var guild = _client.GetGuild(gc.GuildId);
                if (guild is null)
                {
                    Log.Information("Unable to find guild {GuildId} for message repeaters.", gc.GuildId);
                    continue;
                }

                var idToRepeater = gc.GuildRepeaters
                                                          .Where(gr => gr.DateAdded is not null)
                                                          .Select(gr =>
                                                              new KeyValuePair<int, RepeatRunner>(gr.Id, new RepeatRunner(_client, guild, gr, this)))
                                                          .ToDictionary(x => x.Key, y => y.Value)
                                                          .ToConcurrent();

                repeaters.TryAdd(gc.GuildId, idToRepeater);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load repeaters on Guild {0}.", gc.GuildId);
            }
        }

        Repeaters = repeaters.ToConcurrent();
        RepeaterReady = true;
    }

    public async Task RemoveRepeater(Repeater r)
    {
        await using var uow = _db.GetDbContext();
        var gr = (await uow.ForGuildId(r.GuildId, x => x.Include(y => y.GuildRepeaters))).GuildRepeaters;
        var toDelete = gr.Find(x => x.Id == r.Id);
        if (toDelete != null)
            uow.Set<Repeater>().Remove(toDelete);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public void SetRepeaterLastMessage(int repeaterId, ulong lastMsgId)
    {
        using var uow = _db.GetDbContext();
        uow.Database.ExecuteSqlInterpolated($@"UPDATE GuildRepeater SET 
                    LastMessageId={lastMsgId} WHERE Id={repeaterId}");
    }
}