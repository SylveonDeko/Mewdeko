namespace Mewdeko.Database.Models;

public class RoleGreet : DbEntity
{
    public ulong GuildId { get; set; }
    public ulong RoleId { get; set; }
    public ulong ChannelId { get; set; }
    public bool GreetBots { get; set; }
    public string Message { get; set; } = "Welcome %user%";
    public int DeleteTime { get; set; } = 0;
    public string WebhookUrl { get; set; }
    public bool Disabled { get; set; }
}