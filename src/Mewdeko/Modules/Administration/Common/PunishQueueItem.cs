namespace Mewdeko.Modules.Administration.Common;

public class PunishQueueItem
{
    public PunishmentAction Action { get; set; }
    public ProtectionType Type { get; set; }
    public int MuteTime { get; set; }
    public ulong? RoleId { get; set; }
    public IGuildUser User { get; set; }
}