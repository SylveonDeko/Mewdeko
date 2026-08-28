using DataModel;
using LinqToDB;
using LinqToDB.Async;

namespace Mewdeko.Modules.Chat_Triggers.Services;

/// <summary>
///     Stores the named counters chat trigger responses read and update.
/// </summary>
/// <remarks>
///     Counters are the small piece of persistent state triggers were missing. A counter is either guild-scoped, shared
///     by everyone, or user-scoped, tracked per member, which covers tallies, streaks and "you have done this N times"
///     responses without needing a trigger per value.
/// </remarks>
public sealed class TriggerCounterService : INService
{
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<TriggerCounterService> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TriggerCounterService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="logger">The logger.</param>
    public TriggerCounterService(IDataConnectionFactory dbFactory, ILogger<TriggerCounterService> logger)
    {
        this.dbFactory = dbFactory;
        this.logger = logger;
    }

    /// <summary>
    ///     Gets a counter's current value without changing it.
    /// </summary>
    /// <param name="guildId">The guild the counter belongs to.</param>
    /// <param name="name">The counter's name.</param>
    /// <param name="userId">The user the counter tracks, or 0 for a guild-wide counter.</param>
    /// <returns>The counter's value, or zero if it does not exist yet.</returns>
    public async Task<long> GetAsync(ulong guildId, string name, ulong userId = 0)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var counter = await db.ChatTriggerCounters
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Name == name && x.UserId == userId)
            .ConfigureAwait(false);

        return counter?.Value ?? 0;
    }

    /// <summary>
    ///     Adds to a counter and returns its new value, creating it if it does not exist.
    /// </summary>
    /// <param name="guildId">The guild the counter belongs to.</param>
    /// <param name="name">The counter's name.</param>
    /// <param name="amount">The amount to add, which may be negative.</param>
    /// <param name="userId">The user the counter tracks, or 0 for a guild-wide counter.</param>
    /// <returns>The counter's value after the change.</returns>
    /// <remarks>
    ///     The update is done in the database rather than read-modify-write, so two triggers firing at once cannot lose
    ///     an increment.
    /// </remarks>
    public async Task<long> AddAsync(ulong guildId, string name, long amount, ulong userId = 0)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var updated = await db.ChatTriggerCounters
            .Where(x => x.GuildId == guildId && x.Name == name && x.UserId == userId)
            .Set(x => x.Value, x => x.Value + amount)
            .UpdateAsync()
            .ConfigureAwait(false);

        if (updated == 0)
        {
            try
            {
                await db.InsertAsync(new ChatTriggerCounter
                {
                    GuildId = guildId,
                    Name = name,
                    UserId = userId,
                    Value = amount,
                    DateAdded = DateTime.UtcNow
                }).ConfigureAwait(false);

                return amount;
            }
            catch (Exception ex)
            {
                // Another fire may have created the row between the update and the insert; fall through to re-read
                logger.LogDebug(ex, "Counter {Name} was created concurrently in {GuildId}", name, guildId);

                await db.ChatTriggerCounters
                    .Where(x => x.GuildId == guildId && x.Name == name && x.UserId == userId)
                    .Set(x => x.Value, x => x.Value + amount)
                    .UpdateAsync()
                    .ConfigureAwait(false);
            }
        }

        return await GetAsync(guildId, name, userId).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets a counter to an exact value, creating it if it does not exist.
    /// </summary>
    /// <param name="guildId">The guild the counter belongs to.</param>
    /// <param name="name">The counter's name.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="userId">The user the counter tracks, or 0 for a guild-wide counter.</param>
    public async Task SetAsync(ulong guildId, string name, long value, ulong userId = 0)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var updated = await db.ChatTriggerCounters
            .Where(x => x.GuildId == guildId && x.Name == name && x.UserId == userId)
            .Set(x => x.Value, value)
            .UpdateAsync()
            .ConfigureAwait(false);

        if (updated == 0)
        {
            await db.InsertAsync(new ChatTriggerCounter
            {
                GuildId = guildId,
                Name = name,
                UserId = userId,
                Value = value,
                DateAdded = DateTime.UtcNow
            }).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Deletes a counter and every per-user value stored under its name.
    /// </summary>
    /// <param name="guildId">The guild the counter belongs to.</param>
    /// <param name="name">The counter's name.</param>
    /// <returns>The number of rows removed.</returns>
    public async Task<int> DeleteAsync(ulong guildId, string name)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.ChatTriggerCounters
            .Where(x => x.GuildId == guildId && x.Name == name)
            .DeleteAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists the guild-wide counters defined in a guild.
    /// </summary>
    /// <param name="guildId">The guild to list counters for.</param>
    /// <returns>The guild's counters, ordered by name.</returns>
    public async Task<List<ChatTriggerCounter>> ListAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        return await db.ChatTriggerCounters
            .Where(x => x.GuildId == guildId && x.UserId == 0)
            .OrderBy(x => x.Name)
            .ToListAsync()
            .ConfigureAwait(false);
    }
}