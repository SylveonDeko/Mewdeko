namespace Mewdeko.Database.Models;

public class RoleConnectionAuthStorage : DbEntity
{
    public ulong UserId { get; set; }
    public string Scopes { get; set; }
    public string Token { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
}