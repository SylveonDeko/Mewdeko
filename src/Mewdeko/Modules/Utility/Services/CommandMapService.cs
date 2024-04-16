using Mewdeko.Common.ModuleBehaviors;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
/// Manages the transformation of input commands based on alias mappings, allowing customization of command triggers.
/// </summary>
public class CommandMapService : IInputTransformer, INService
{
    private readonly DbService db;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandMapService"/>.
    /// </summary>
    /// <param name="db">The database service for accessing command alias configurations.</param>
    /// <param name="bot">The bot instance to access global guild configurations.</param>
    public CommandMapService(DbService db, Mewdeko bot)
    {
        AliasMaps = new ConcurrentDictionary<ulong, ConcurrentDictionary<string, string>>(bot.AllGuildConfigs
            .ToDictionary(
                x => x.Key,
                x => new ConcurrentDictionary<string, string>(x.Value.CommandAliases
                    .Distinct(new CommandAliasEqualityComparer())
                    .ToDictionary(ca => ca.Trigger, ca => ca.Mapping))));

        this.db = db;
    }


    /// <summary>
    /// Gets the collection of alias mappings by guild.
    /// </summary>
    public ConcurrentDictionary<ulong, ConcurrentDictionary<string, string>> AliasMaps { get; }

    /// <summary>
    /// Transforms an input command based on alias mappings for the specific guild.
    /// </summary>
    /// <param name="guild">The guild where the command was issued.</param>
    /// <param name="channel">The channel where the command was issued.</param>
    /// <param name="user">The user who issued the command.</param>
    /// <param name="input">The original command input.</param>
    /// <returns>The transformed command input if an alias is matched; otherwise, the original input.</returns>
    public async Task<string> TransformInput(IGuild? guild, IMessageChannel channel, IUser user, string input)
    {
        await Task.Yield();

        if (guild == null || string.IsNullOrWhiteSpace(input))
            return input;

        // ReSharper disable once HeuristicUnreachableCode
        if (guild == null) return input;
        if (!AliasMaps.TryGetValue(guild.Id, out var maps)) return input;
        var keys = maps.Keys
            .OrderByDescending(x => x.Length);

        foreach (var k in keys)
        {
            string newInput;
            if (input.StartsWith($"{k} ", StringComparison.InvariantCultureIgnoreCase))
                newInput = string.Concat(maps[k], input.AsSpan(k.Length, input.Length - k.Length));
            else if (input.Equals(k, StringComparison.InvariantCultureIgnoreCase))
                newInput = maps[k];
            else
                continue;
            return newInput;
        }

        return input;
    }

    /// <summary>
    /// Clears all command aliases for a specified guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild for which to clear aliases.</param>
    /// <returns>The number of aliases cleared.</returns>
    public async Task<int> ClearAliases(ulong guildId)
    {
        AliasMaps.TryRemove(guildId, out _);

        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guildId, set => set.Include(x => x.CommandAliases));
        var count = gc.CommandAliases.Count;
        gc.CommandAliases.Clear();
        await uow.SaveChangesAsync();

        return count;
    }
}

/// <summary>
/// This class provides a way to compare two CommandAlias objects.
/// It implements the IEqualityComparer interface which defines methods to support the comparison of objects for equality.
/// </summary>
public class CommandAliasEqualityComparer : IEqualityComparer<CommandAlias>
{
    /// <summary>
    /// Determines whether the specified CommandAlias objects are equal.
    /// </summary>
    /// <param name="x">The first CommandAlias object to compare.</param>
    /// <param name="y">The second CommandAlias object to compare.</param>
    /// <returns>true if the specified CommandAlias objects are equal; otherwise, false.</returns>
    public bool Equals(CommandAlias? x, CommandAlias? y) => x?.Trigger == y?.Trigger;

    /// <summary>
    /// Returns a hash code for the specified CommandAlias object.
    /// </summary>
    /// <param name="obj">The CommandAlias object for which a hash code is to be returned.</param>
    /// <returns>A hash code for the specified CommandAlias object.</returns>
    public int GetHashCode(CommandAlias obj) => obj.Trigger.GetHashCode(StringComparison.InvariantCulture);
}