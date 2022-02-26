using System.Collections.Concurrent;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

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
        if (TryGetOverrides(context.Guild?.Id ?? 0, command.MethodName(), out var perm) && perm is not null)
        {
            var result = await new Discord.Commands.RequireUserPermissionAttribute((GuildPermission) perm)
                .CheckPermissionsAsync(context, command, _services);
            return !result.IsSuccess;
        }

        return false;
    }
    public async Task<bool> TryBlockLate(DiscordSocketClient client, IInteractionContext context,
        ICommandInfo command)
    {
        if (TryGetOverrides(context.Guild?.Id ?? 0, command.MethodName, out var perm) && perm is not null)
        {
            var result = await new Discord.Interactions.RequireUserPermissionAttribute((GuildPermission) perm)
                .CheckRequirementsAsync(context, command, _services);
            if (!result.IsSuccess)
                await context.Interaction.SendEphemeralErrorAsync($"You need `{perm}` to use this command.");
            return !result.IsSuccess;
        }

        return false;
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

    public static Task<Discord.Commands.PreconditionResult> ExecuteOverrides(ICommandContext ctx, CommandInfo command,
        GuildPermission perms, IServiceProvider services)
    {
        var rupa = new Discord.Commands.RequireUserPermissionAttribute(perms);
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
            .FirstOrDefaultAsync(x => x.GuildId == guildId && commandName == x.Command);

        if (over is null)
            uow.DiscordPermOverrides
                     .Add(over = new DiscordPermOverride
                     {
                         Command = commandName,
                         Perm = perm,
                         GuildId = guildId
                     });
        else
            over.Perm = perm;

        _overrides[(guildId, commandName)] = over;

        await uow.SaveChangesAsync();
    }

    public async Task ClearAllOverrides(ulong guildId)
    {
        await using var uow = _db.GetDbContext();
        var overrides = await uow.DiscordPermOverrides
                                 .AsQueryable()
                                 .AsNoTracking()
                                 .Where(x => x.GuildId == guildId)
                                 .ToListAsync();

        uow.DiscordPermOverrides.RemoveRange(overrides);
        await uow.SaveChangesAsync();

        foreach (var over in overrides) _overrides.TryRemove((guildId, over.Command), out _);
    }

    public async Task RemoveOverride(ulong guildId, string commandName)
    {
        commandName = commandName.ToLowerInvariant();

        await using var uow = _db.GetDbContext();
        var over = await uow.DiscordPermOverrides
                            .AsQueryable()
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Command == commandName);

        if (over is null)
            return;

       uow.DiscordPermOverrides .Remove(over);
        await uow.SaveChangesAsync();

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