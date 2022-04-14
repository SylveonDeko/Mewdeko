using Mewdeko.Database.Models;

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
}
