using System.Threading.Tasks;
using Mewdeko.Common.ModuleBehaviors;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Utility.Services;

public class CommandMapService : IInputTransformer, INService
{
    private readonly DbService db;

    //commandmap
    public CommandMapService(DiscordSocketClient client, DbService db)
    {
        using var uow = db.GetDbContext();
        var guildIds = client.Guilds.Select(x => x.Id).ToList();
        var configs = uow.GuildConfigs
            .Include(gc => gc.CommandAliases)
            .Where(x => guildIds.Contains(x.GuildId))
            .ToList();

        AliasMaps = new ConcurrentDictionary<ulong, ConcurrentDictionary<string, string>>(configs
            .ToDictionary(
                x => x.GuildId,
                x => new ConcurrentDictionary<string, string>(x.CommandAliases
                    .Distinct(new CommandAliasEqualityComparer())
                    .ToDictionary(ca => ca.Trigger, ca => ca.Mapping))));

        this.db = db;
    }

    public ConcurrentDictionary<ulong, ConcurrentDictionary<string, string>> AliasMaps { get; }

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

public class CommandAliasEqualityComparer : IEqualityComparer<CommandAlias>
{
    public bool Equals(CommandAlias? x, CommandAlias? y) => x?.Trigger == y?.Trigger;

    public int GetHashCode(CommandAlias obj) => obj.Trigger.GetHashCode(StringComparison.InvariantCulture);
}