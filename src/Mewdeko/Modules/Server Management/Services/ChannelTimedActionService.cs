using System.Text.Json;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Server_Management.Services;

/// <summary>
///     What an overwrite of a role looked like before a channel was locked or hidden.
/// </summary>
/// <param name="RoleId">The role ID. The @everyone role has the guild's ID.</param>
/// <param name="Existed">Whether the role had an overwrite on the channel.</param>
/// <param name="Value">The value the changed permission had, as a <see cref="PermValue" />.</param>
public record ChannelTimedTarget(ulong RoleId, bool Existed, int Value);

/// <summary>
///     Locks or hides channels, optionally for a set time, and puts the overwrites back afterwards.
/// </summary>
public class ChannelTimedActionService : INService, IReadyExecutor
{
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<ChannelTimedActionService> logger;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly GuildOwnershipService ownership;
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     The shortest time a lock or hide can last.
    /// </summary>
    public static readonly TimeSpan MinDuration = TimeSpan.FromMinutes(1);

    /// <summary>
    ///     The longest time a lock or hide can last.
    /// </summary>
    public static readonly TimeSpan MaxDuration = TimeSpan.FromDays(365);

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChannelTimedActionService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="eventHandler">The event handler.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="strings">The generated bot strings.</param>
    /// <param name="ownership">The guild ownership service.</param>
    public ChannelTimedActionService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        EventHandler eventHandler, ILogger<ChannelTimedActionService> logger, GeneratedBotStrings strings,
        GuildOwnershipService ownership)
    {
        this.ownership = ownership;
        this.strings = strings;
        this.client = client;
        this.dbFactory = dbFactory;
        this.logger = logger;
        eventHandler.Subscribe("ChannelDestroyed", "ChannelTimedActionService", OnChannelDestroyed);
    }

    /// <summary>
    ///     Starts the loop that undoes locks and hides when their time is up.
    /// </summary>
    public Task OnReadyAsync()
    {
        _ = Task.Run(RunLoop);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Locks or hides a channel for the given roles, or for @everyone when none are given. A channel that is already
    ///     locked or hidden keeps its original overwrites and only gets a new end time.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="kind">Whether to lock or hide.</param>
    /// <param name="roles">The roles to deny, or none for @everyone.</param>
    /// <param name="duration">How long it lasts, or null to last until undone.</param>
    /// <param name="userId">The user who did it.</param>
    /// <returns>True if the channel was already locked or hidden and only the end time changed.</returns>
    public async Task<bool> ApplyAsync(SocketGuildChannel channel, ChannelTimedActionKind kind,
        IReadOnlyCollection<IRole> roles, TimeSpan? duration, ulong userId)
    {
        await gate.WaitAsync();
        try
        {
            var endsAt = duration is { } span ? DateTime.UtcNow.Add(span) : (DateTime?)null;
            await using var db = await dbFactory.CreateConnectionAsync();
            var existing = await db.ChannelTimedActions
                .FirstOrDefaultAsync(x => x.ChannelId == channel.Id && x.Kind == kind);
            if (existing is not null)
            {
                existing.EndsAt = endsAt;
                await db.UpdateAsync(existing);
                return true;
            }

            var targets = roles.Count == 0 ? [channel.Guild.EveryoneRole] : roles.ToList();
            var previous = new List<ChannelTimedTarget>();
            foreach (var role in targets)
            {
                var current = channel.GetPermissionOverwrite(role);
                previous.Add(new ChannelTimedTarget(role.Id, current is not null,
                    (int)(current is { } c ? GetValue(c, kind) : PermValue.Inherit)));
            }

            await db.InsertAsync(new ChannelTimedAction
            {
                GuildId = channel.Guild.Id,
                ChannelId = channel.Id,
                Kind = kind,
                Previous = JsonSerializer.Serialize(previous),
                EndsAt = endsAt,
                CreatedBy = userId,
                DateAdded = DateTime.UtcNow
            });

            try
            {
                foreach (var role in targets)
                {
                    var current = channel.GetPermissionOverwrite(role) ?? new OverwritePermissions();
                    await channel.AddPermissionOverwriteAsync(role, SetValue(current, kind, PermValue.Deny));
                }
            }
            catch
            {
                await db.ChannelTimedActions.Where(x => x.ChannelId == channel.Id && x.Kind == kind).DeleteAsync();
                await RestoreOverwritesAsync(channel, previous, kind);
                throw;
            }

            return false;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    ///     Undoes a lock or hide now and puts the overwrites back.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="kind">Whether to unlock or unhide.</param>
    /// <returns>False if the channel was not locked or hidden by this tool.</returns>
    public async Task<bool> UndoAsync(ulong channelId, ChannelTimedActionKind kind)
    {
        await gate.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var action = await db.ChannelTimedActions.FirstOrDefaultAsync(x => x.ChannelId == channelId && x.Kind == kind);
            if (action is null)
                return false;

            await RestoreAsync(db, action);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    ///     Describes a locked or hidden channel for a list.
    /// </summary>
    /// <param name="action">The action.</param>
    public string Line(ChannelTimedAction action)
    {
        var kind = action.Kind == ChannelTimedActionKind.Lock
            ? strings.ChantoolsKindLock(action.GuildId)
            : strings.ChantoolsKindHide(action.GuildId);
        var channel = $"<#{action.ChannelId}>";
        return action.EndsAt is { } ends
            ? strings.ChantoolsListUntil(action.GuildId, channel, kind,
                TimestampTag.FromDateTime(DateTime.SpecifyKind(ends, DateTimeKind.Utc), TimestampTagStyles.Relative))
            : strings.ChantoolsListForever(action.GuildId, channel, kind);
    }

    /// <summary>
    ///     Lists the channels in a guild that are locked or hidden by this tool.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public async Task<List<ChannelTimedAction>> ListAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ChannelTimedActions.Where(x => x.GuildId == guildId).OrderBy(x => x.EndsAt).ToListAsync();
    }

    private static PermValue GetValue(OverwritePermissions perms, ChannelTimedActionKind kind) =>
        kind == ChannelTimedActionKind.Lock ? perms.SendMessages : perms.ViewChannel;

    private static OverwritePermissions SetValue(OverwritePermissions perms, ChannelTimedActionKind kind,
        PermValue value) =>
        kind == ChannelTimedActionKind.Lock
            ? perms.Modify(sendMessages: value)
            : perms.Modify(viewChannel: value);

    private async Task RestoreOverwritesAsync(SocketGuildChannel channel, IEnumerable<ChannelTimedTarget> previous,
        ChannelTimedActionKind kind)
    {
        foreach (var target in previous)
        {
            var role = channel.Guild.GetRole(target.RoleId);
            if (role is null)
                continue;

            try
            {
                var current = channel.GetPermissionOverwrite(role) ?? new OverwritePermissions();
                var restored = SetValue(current, kind, (PermValue)target.Value);
                if (!target.Existed && restored.AllowValue == 0 && restored.DenyValue == 0)
                    await channel.RemovePermissionOverwriteAsync(role);
                else
                    await channel.AddPermissionOverwriteAsync(role, restored);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not restore the overwrite of {RoleId} on {ChannelId}", target.RoleId,
                    channel.Id);
            }
        }
    }

    private async Task RestoreAsync(MewdekoDb db, ChannelTimedAction action)
    {
        var channel = client.GetGuild(action.GuildId)?.GetChannel(action.ChannelId);
        if (channel is not null)
        {
            var previous = JsonSerializer.Deserialize<List<ChannelTimedTarget>>(action.Previous) ?? [];
            await RestoreOverwritesAsync(channel, previous, action.Kind);
        }

        await db.ChannelTimedActions.Where(x => x.Id == action.Id).DeleteAsync();
    }

    private async Task OnChannelDestroyed(SocketChannel channel)
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            await db.ChannelTimedActions.Where(x => x.ChannelId == channel.Id).DeleteAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not clear the timed actions of {ChannelId}", channel.Id);
        }
    }

    private async Task RunLoop()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                await ProcessAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Timed channel action pass failed");
            }
        } while (await timer.WaitForNextTickAsync());
    }

    private async Task ProcessAsync()
    {
        await gate.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var now = DateTime.UtcNow;
            var due = await db.ChannelTimedActions.Where(x => x.EndsAt != null && x.EndsAt <= now).ToListAsync();
            foreach (var action in due)
            {
                if (!ownership.IsMine(action.GuildId) || client.GetGuild(action.GuildId) is null)
                    continue;

                try
                {
                    await RestoreAsync(db, action);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not undo the timed action on {ChannelId}", action.ChannelId);
                }
            }
        }
        finally
        {
            gate.Release();
        }
    }
}
