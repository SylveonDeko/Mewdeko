using System.ComponentModel.DataAnnotations.Schema;
using Discord;

namespace Mewdeko.Database.Models;

[Table("RotatingStatus")]
public class RotatingPlayingStatus : DbEntity
{
    public string Status { get; set; }
    public ActivityType Type { get; set; }
}