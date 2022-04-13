// ReSharper disable InconsistentNaming

using System.ComponentModel.DataAnnotations;

namespace Mewdeko.Database.Models;

public class SuggestionsModel
{
    public ulong GuildId { get; set; }
    public ulong SuggestID { get; set; }
    public string Suggestion { get; set; }
    [Key]
    public ulong MessageID { get; set; }
    public ulong UserID { get; set; }
    public int EmoteCount1 { get; set; } = 0;
    public int EmoteCount2 { get; set; } = 0;
    public int EmoteCount3 { get; set; } = 0;
    public int EmoteCount4 { get; set; } = 0;
    public int EmoteCount5 { get; set; } = 0;
    public ulong StateChangeUser { get; set; } = 0;
    public ulong StateChangeCount { get; set; } = 0;
    public ulong StateChangeMessageId { get; set; } = 0;
    public int CurrentState = 0;
    public DateTime DateAdded = DateTime.UtcNow;


}