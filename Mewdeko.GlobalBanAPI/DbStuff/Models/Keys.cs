namespace Mewdeko.GlobalBanAPI.DbStuff.Models;

public class Keys : DbEntity
{
    public ulong UserId { get; set; }
    public string Key { get; set; }
}