namespace Mewdeko.Database.Models;

public class Usernames : DbEntity
{
    public string Username { get; set; }
    public ulong UserId { get; set; }
}