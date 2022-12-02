using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class ChatTriggersExtensions
{
    public static List<ulong> GetGrantedRoles(this ChatTriggers trigger)
        => ParseUlongs(trigger.GrantedRoles) ?? new List<ulong>();

    public static List<ulong> GetRemovedRoles(this ChatTriggers trigger)
        => ParseUlongs(trigger.RemovedRoles) ?? new List<ulong>();

    private static List<ulong> ParseUlongs(string inpt)
        => inpt?.Split("@@@")
            .Select(x => ulong.TryParse(x, out var v) ? v : 0)
            .Where(x => x != 0)
            .Distinct()
            .ToList();

    public static bool IsRemoved(this ChatTriggers trigger, ulong roleId) =>
        trigger.RemovedRoles?.Contains(roleId.ToString()) ?? false;

    public static bool IsGranted(this ChatTriggers trigger, ulong roleId) =>
        trigger.GrantedRoles?.Contains(roleId.ToString()) ?? false;

    public static bool IsToggled(this ChatTriggers trigger, ulong roleId) =>
        trigger.IsGranted(roleId) && trigger.IsRemoved(roleId);

    public static int ClearFromGuild(this DbSet<ChatTriggers> crs, ulong guildId)
        => crs.Delete(x => x.GuildId == guildId);

    public static async Task<IEnumerable<ChatTriggers>> ForId(this DbSet<ChatTriggers> crs, ulong id) =>
        await crs
            .AsNoTracking()
            .AsQueryable()
            .Where(x => x.GuildId == id)
            .ToArrayAsyncEF();

    public static async Task<ChatTriggers> GetByGuildIdAndInput(this DbSet<ChatTriggers> crs, ulong? guildId, string input) =>
        await AsyncExtensions.FirstOrDefaultAsync(crs, x => x.GuildId == guildId && x.Trigger.ToUpper() == input).ConfigureAwait(false);
}