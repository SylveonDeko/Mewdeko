using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;

namespace Mewdeko.Services;

/// <summary>
///     Holds each guild's own colour for the bot's ok and info embeds. The colour a command uses is picked by the guild
///     the running command or interaction belongs to, so the same embed code works in every guild.
/// </summary>
public class GuildEmbedColorService(IDataConnectionFactory dbFactory, ILogger<GuildEmbedColorService> logger)
    : INService, IReadyExecutor
{
    private static readonly ConcurrentDictionary<ulong, Color> Overrides = new();
    private static readonly AsyncLocal<ulong?> CurrentGuild = new();

    /// <summary>
    ///     Gets the ok colour for the guild the running command or interaction belongs to.
    /// </summary>
    public static Color Ok
    {
        get
        {
            return OkFor(CurrentGuild.Value);
        }
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            foreach (var row in await db.GuildEmbedColors.ToListAsync())
                Overrides[row.GuildId] = new Color((uint)row.OkColor);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed loading guild embed colours");
        }
    }

    /// <summary>
    ///     Makes the given guild the one whose colour <see cref="Ok" /> returns for the rest of the current async flow.
    /// </summary>
    /// <param name="guildId">The guild the running command belongs to, or null outside a guild.</param>
    public static void Enter(ulong? guildId)
    {
        CurrentGuild.Value = guildId;
    }

    /// <summary>
    ///     Gets the ok colour for a guild.
    /// </summary>
    /// <param name="guildId">The guild, or null for the bot's default.</param>
    /// <returns>The guild's own colour, or the bot's default when it has none.</returns>
    public static Color OkFor(ulong? guildId)
    {
        return guildId is { } id && Overrides.TryGetValue(id, out var color) ? color : Mewdeko.DefaultOkColor;
    }

    /// <summary>
    ///     Gets a guild's own ok colour.
    /// </summary>
    /// <param name="guildId">The guild.</param>
    /// <returns>The colour, or null when the guild uses the bot's default.</returns>
    public Color? Get(ulong guildId)
    {
        return Overrides.TryGetValue(guildId, out var color) ? color : null;
    }

    /// <summary>
    ///     Sets or clears a guild's own ok colour.
    /// </summary>
    /// <param name="guildId">The guild.</param>
    /// <param name="color">The colour, or null to use the bot's default again.</param>
    public async Task SetAsync(ulong guildId, Color? color)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        if (color is null)
        {
            await db.GuildEmbedColors.Where(x => x.GuildId == guildId).DeleteAsync();
            Overrides.TryRemove(guildId, out _);
            return;
        }

        var value = (int)color.Value.RawValue;
        var updated = await db.GuildEmbedColors.Where(x => x.GuildId == guildId)
            .Set(x => x.OkColor, value)
            .UpdateAsync();

        if (updated == 0)
        {
            await db.InsertAsync(new GuildEmbedColor
            {
                GuildId = guildId, OkColor = value, DateAdded = DateTime.UtcNow
            });
        }

        Overrides[guildId] = color.Value;
    }
}
