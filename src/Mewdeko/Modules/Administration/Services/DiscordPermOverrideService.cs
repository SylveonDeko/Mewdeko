using Discord.Commands;
using Discord.Interactions;
using Mewdeko.Common.ModuleBehaviors;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using PreconditionResult = Discord.Commands.PreconditionResult;
using RequireUserPermissionAttribute = Discord.Commands.RequireUserPermissionAttribute;

namespace Mewdeko.Modules.Administration.Services;

public class DiscordPermOverrideService : INService, ILateBlocker
{
    private readonly DbService _db;

    private readonly ConcurrentDictionary<(ulong, string), DiscordPermOverride> _overrides;
    private readonly IServiceProvider _services;

    public DiscordPermOverrideService(DbService db, IServiceProvider services)
    {
        _db = db;
        _services = services;
        using var uow = _db.GetDbContext();
        _overrides = uow.DiscordPermOverrides
            .AsNoTracking()
            .AsEnumerable()
            .ToDictionary(o => (o.GuildId ?? 0, o.Command), o => o)
            .ToConcurrent();
    }

    public int Priority { get; } = int.MaxValue;

    public async Task<bool> TryBlockLate(DiscordSocketClient client, ICommandContext context, string moduleName,
        CommandInfo command)
    {
        if (!TryGetOverrides(context.Guild?.Id ?? 0, command.MethodName(), out var perm) || perm is null) return false;
        var result = await new RequireUserPermissionAttribute((GuildPermission)perm)
                           .CheckPermissionsAsync(context, command, _services).ConfigureAwait(false);
        return !result.IsSuccess;
    }
    public async Task<bool> TryBlockLate(DiscordSocketClient client, IInteractionContext context,
        ICommandInfo command)
    {
        if (!TryGetOverrides(context.Guild?.Id ?? 0, command.MethodName, out var perm) || perm is null) return false;
        var result = await new Discord.Interactions.RequireUserPermissionAttribute((GuildPermission)perm)
            .CheckRequirementsAsync(context, command, _services).ConfigureAwait(false);
        if (!result.IsSuccess)
            await context.Interaction.SendEphemeralErrorAsync($"You need `{perm}` to use this command.").ConfigureAwait(false);
        return !result.IsSuccess;
    }

    public bool TryGetOverrides(ulong guildId, string commandName, out GuildPermission? perm)
    {
        commandName = commandName.ToLowerInvariant();
        if (_overrides.TryGetValue((guildId, commandName), out var dpo))
        {
            perm = dpo.Perm;
            return true;
        }

        perm = null;
        return false;
    }

    public static Task<PreconditionResult> ExecuteOverrides(ICommandContext ctx, CommandInfo command,
        GuildPermission perms, IServiceProvider services)
    {
        var rupa = new RequireUserPermissionAttribute(perms);
        return rupa.CheckPermissionsAsync(ctx, command, services);
    }
    public static Task<Discord.Interactions.PreconditionResult> ExecuteOverrides(IInteractionContext ctx, ICommandInfo command,
        GuildPermission perms, IServiceProvider services)
    {
        var rupa = new Discord.Interactions.RequireUserPermissionAttribute(perms);
        return rupa.CheckRequirementsAsync(ctx, command, services);
    }

    public async Task AddOverride(ulong guildId, string commandName, GuildPermission perm)
    {
        commandName = commandName.ToLowerInvariant();
        await using var uow = _db.GetDbContext();
        var over = await uow.DiscordPermOverrides
                            .AsQueryable()
                            .FirstOrDefaultAsync(x => x.GuildId == guildId && commandName == x.Command).ConfigureAwait(false);

        if (over is null)
        {
            uow.DiscordPermOverrides
                             .Add(over = new DiscordPermOverride
                             {
                                 Command = commandName,
                                 Perm = perm,
                                 GuildId = guildId
                             });
        }
        else
        {
            over.Perm = perm;
        }

        _overrides[(guildId, commandName)] = over;

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task ClearAllOverrides(ulong guildId)
    {
        await using var uow = _db.GetDbContext();
        var overrides = await uow.DiscordPermOverrides
                                 .AsQueryable()
                                 .AsNoTracking()
                                 .Where(x => x.GuildId == guildId)
                                 .ToListAsync().ConfigureAwait(false);

        uow.DiscordPermOverrides.RemoveRange(overrides);
        await uow.SaveChangesAsync().ConfigureAwait(false);

        foreach (var over in overrides) _overrides.TryRemove((guildId, over.Command), out _);
    }

    public async Task RemoveOverride(ulong guildId, string commandName)
    {
        commandName = commandName.ToLowerInvariant();

        await using var uow = _db.GetDbContext();
        var over = await uow.DiscordPermOverrides
                            .AsQueryable()
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Command == commandName).ConfigureAwait(false);

        if (over is null)
            return;

        uow.DiscordPermOverrides.Remove(over);
        await uow.SaveChangesAsync().ConfigureAwait(false);

        _overrides.TryRemove((guildId, commandName), out _);
    }

    public Task<List<DiscordPermOverride>> GetAllOverrides(ulong guildId)
    {
        using var uow = _db.GetDbContext();
        return
            uow.DiscordPermOverrides
                     .AsQueryable()
                     .AsNoTracking()
                     .Where(x => x.GuildId == guildId)
                     .ToListAsync();
    }
}