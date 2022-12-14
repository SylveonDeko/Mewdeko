using System.Threading.Tasks;
using Discord.Commands;
using Discord.Interactions;
using Mewdeko.Common.ModuleBehaviors;
using Microsoft.EntityFrameworkCore;
using PreconditionResult = Discord.Commands.PreconditionResult;
using RequireUserPermissionAttribute = Discord.Commands.RequireUserPermissionAttribute;

namespace Mewdeko.Modules.Administration.Services;

public class DiscordPermOverrideService : INService, ILateBlocker
{
    private readonly DbService db;

    private readonly ConcurrentDictionary<(ulong, string), DiscordPermOverride> overrides;
    private readonly IServiceProvider services;

    public DiscordPermOverrideService(DbService db, IServiceProvider services)
    {
        this.db = db;
        this.services = services;
        using var uow = this.db.GetDbContext();
        overrides = uow.DiscordPermOverrides
            .AsNoTracking()
            .AsEnumerable()
            .ToDictionary(o => (o.GuildId ?? 0, o.Command), o => o)
            .ToConcurrent();
    }

    public int Priority { get; } = int.MaxValue;

    public async Task<bool> TryBlockLate(DiscordSocketClient client, ICommandContext context, string moduleName,
        CommandInfo command)
    {
        if (!TryGetOverrides(context.Guild?.Id ?? 0, command.MethodName(), out var perm)) return false;
        var result = await new RequireUserPermissionAttribute(perm)
            .CheckPermissionsAsync(context, command, services).ConfigureAwait(false);
        return !result.IsSuccess;
    }

    public async Task<bool> TryBlockLate(DiscordSocketClient client, IInteractionContext context,
        ICommandInfo command)
    {
        if (!TryGetOverrides(context.Guild?.Id ?? 0, command.MethodName, out var perm)) return false;
        var result = await new Discord.Interactions.RequireUserPermissionAttribute(perm)
            .CheckRequirementsAsync(context, command, services).ConfigureAwait(false);
        if (!result.IsSuccess)
            await context.Interaction.SendEphemeralErrorAsync($"You need `{perm}` to use this command.").ConfigureAwait(false);
        return !result.IsSuccess;
    }

    public bool TryGetOverrides(ulong guildId, string commandName, out GuildPermission perm)
    {
        commandName = commandName.ToLowerInvariant();
        if (overrides.TryGetValue((guildId, commandName), out var dpo))
        {
            perm = dpo.Perm;
            return true;
        }

        perm = default;
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
        await using var uow = db.GetDbContext();
        var over = await uow.DiscordPermOverrides
            .AsQueryable()
            .FirstOrDefaultAsync(x => x.GuildId == guildId && commandName == x.Command).ConfigureAwait(false);

        if (over is null)
        {
            uow.DiscordPermOverrides
                .Add(over = new DiscordPermOverride
                {
                    Command = commandName, Perm = perm, GuildId = guildId
                });
        }
        else
        {
            over.Perm = perm;
        }

        overrides[(guildId, commandName)] = over;

        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task ClearAllOverrides(ulong guildId)
    {
        await using var uow = db.GetDbContext();
        var discordPermOverrides = await uow.DiscordPermOverrides
            .AsQueryable()
            .AsNoTracking()
            .Where(x => x.GuildId == guildId)
            .ToListAsync().ConfigureAwait(false);

        uow.DiscordPermOverrides.RemoveRange(discordPermOverrides);
        await uow.SaveChangesAsync().ConfigureAwait(false);

        foreach (var over in discordPermOverrides) this.overrides.TryRemove((guildId, over.Command), out _);
    }

    public async Task RemoveOverride(ulong guildId, string commandName)
    {
        commandName = commandName.ToLowerInvariant();

        await using var uow = db.GetDbContext();
        var over = await uow.DiscordPermOverrides
            .AsQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.Command == commandName).ConfigureAwait(false);

        if (over is null)
            return;

        uow.DiscordPermOverrides.Remove(over);
        await uow.SaveChangesAsync().ConfigureAwait(false);

        overrides.TryRemove((guildId, commandName), out _);
    }

    public Task<List<DiscordPermOverride>> GetAllOverrides(ulong guildId)
    {
        using var uow = db.GetDbContext();
        return
            uow.DiscordPermOverrides
                .AsQueryable()
                .AsNoTracking()
                .Where(x => x.GuildId == guildId)
                .ToListAsync();
    }
}