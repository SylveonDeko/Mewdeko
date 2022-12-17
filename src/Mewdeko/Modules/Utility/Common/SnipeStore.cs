namespace Mewdeko.Modules.Utility.Common;

public class SnipeStore
{
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public ulong ChannelId { get; set; }
    public string Message { get; set; }
    public string ReferenceMessage { get; set; } = null;
    public bool Edited { get; set; }
    public DateTime DateAdded { get; set; }
}