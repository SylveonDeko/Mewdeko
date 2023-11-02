namespace Mewdeko.Database.Models;

public class ServerRecoveryStore : DbEntity
{
    public ulong GuildId { get; set; }
    public string RecoveryKey { get; set; }
    public string TwoFactorKey { get; set; }
}