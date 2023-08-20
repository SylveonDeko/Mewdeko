using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

public class IgnoredLogChannel : DbEntity
{
    [ForeignKey("LogSettingId")]
    public int LogSettingId { get; set; }

    public LogSetting LogSetting { get; set; }
    public ulong ChannelId { get; set; }
}