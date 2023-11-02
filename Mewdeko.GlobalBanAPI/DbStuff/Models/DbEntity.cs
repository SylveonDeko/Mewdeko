using System.ComponentModel.DataAnnotations;

namespace Mewdeko.GlobalBanAPI.DbStuff.Models;

public class DbEntity
{
    [Key]
    public int Id { get; set; }

    public DateTime? DateAdded { get; set; } = DateTime.UtcNow;
}