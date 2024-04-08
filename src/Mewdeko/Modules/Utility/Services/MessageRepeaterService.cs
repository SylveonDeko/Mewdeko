using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.Utility.Common;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
/// Manages the scheduling and execution of repeating messages across guilds.
/// </summary>
public class MessageRepeaterService(DiscordSocketClient client, DbService db, Mewdeko bot) : INService, IReadyExecutor
{
    /// <summary>
    /// A collection of repeaters organized by guild ID and then by repeater ID.
    /// </summary>
    public ConcurrentDictionary<ulong, ConcurrentDictionary<int, RepeatRunner>> Repeaters { get; set; }

    /// <summary>
    /// Indicates whether the repeater service has finished initializing and loading all repeaters.
    /// </summary>
    public bool RepeaterReady { get; private set; }


    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        await bot.Ready.Task.ConfigureAwait(false);
        Log.Information("Loading message repeaters on shard {ShardId}", client.ShardId);
        await using var uow = db.GetDbContext();
        var allgc = bot.AllGuildConfigs;
        var repeaters = new Dictionary<ulong, ConcurrentDictionary<int, RepeatRunner>>();
        foreach (var gc in allgc)
        {
            try
            {
                var guild = client.GetGuild(gc.GuildId);
                if (guild is null)
                {
                    Log.Information("Unable to find guild {GuildId} for message repeaters", gc.GuildId);
                    continue;
                }

                var idToRepeater = gc.GuildRepeaters
                    .Where(gr => gr.DateAdded is not null)
                    .Select(gr =>
                        new KeyValuePair<int, RepeatRunner>(gr.Id, new RepeatRunner(client, guild, gr, this)))
                    .ToDictionary(x => x.Key, y => y.Value)
                    .ToConcurrent();

                repeaters.TryAdd(gc.GuildId, idToRepeater);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load repeaters on Guild {0}", gc.GuildId);
            }
        }

        Repeaters = repeaters.ToConcurrent();
        RepeaterReady = true;
    }

    /// <summary>
    /// Removes a specific repeater from the database.
    /// </summary>
    /// <param name="r">The repeater configuration to remove.</param>
    public async Task RemoveRepeater(Repeater r)
    {
        await using var uow = db.GetDbContext();
        var gr = (await uow.ForGuildId(r.GuildId, x => x.Include(y => y.GuildRepeaters))).GuildRepeaters;
        var toDelete = gr.Find(x => x.Id == r.Id);
        if (toDelete != null)
            uow.Set<Repeater>().Remove(toDelete);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the ID of the last message sent by a repeater, updating the database with this new value.
    /// </summary>
    /// <param name="repeaterId">The ID of the repeater.</param>
    /// <param name="lastMsgId">The ID of the last message sent by the repeater.</param>
    public void SetRepeaterLastMessage(int repeaterId, ulong lastMsgId)
    {
        using var uow = db.GetDbContext();
        uow.Database.ExecuteSqlInterpolated($@"UPDATE GuildRepeater SET
                    LastMessageId={lastMsgId} WHERE Id={repeaterId}");
    }
}