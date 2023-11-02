namespace Mewdeko.Database.Models;

public class UserRoleStates : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public string UserName { get; set; }
    public string SavedRoles { get; set; }
}