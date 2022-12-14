namespace Mewdeko.Database.Models;

public class MutedUserId : DbEntity
{
    public ulong UserId { get; set; }

    // ReSharper disable once InconsistentNaming
    public string roles { get; set; }

    public override int GetHashCode() => UserId.GetHashCode();

    public override bool Equals(object obj) => obj is MutedUserId mui && mui.UserId == UserId;
}