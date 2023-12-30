namespace Mewdeko.Database.Models;

public class KarutaButtonOptions : DbEntity
{
    public ulong GuildId { get; set; }
    public string Button1Text { get; set; }
    public string Button2Text { get; set; }
    public string Button3Text { get; set; }
    public string Button4Text { get; set; }
    public string Button5Text { get; set; }
    public string Button6Text { get; set; }
}