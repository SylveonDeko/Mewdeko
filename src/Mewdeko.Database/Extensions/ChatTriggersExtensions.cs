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

    public static bool IsRemoved(this ChatTriggers trigger, ulong roleId) =>
        trigger.RemovedRoles?.Contains(roleId.ToString()) ?? false;
    public static bool IsGranted(this ChatTriggers trigger, ulong roleId) =>
        trigger.GrantedRoles?.Contains(roleId.ToString()) ?? false;

    public static bool IsToggled(this ChatTriggers trigger, ulong roleId) =>
        trigger.IsGranted(roleId) && trigger.IsRemoved(roleId);

}
