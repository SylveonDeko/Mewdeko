namespace Mewdeko.Database.Models;

public class ChannelRestrictions
{
    public ulong ChannelId { get; set; }
    public int MaximumAttachments { get; set; }
    public int MinimumAttachments { get; set; }
    public bool AllowText { get; set; }
    public int PunishSeconds { get; set; } = 0;
    public ChannelActionEnum ChannelAction { get; set; } = ChannelActionEnum.Delete;
    public AllowedImageTypesEnum AllowedImageTypes { get; set; } = AllowedImageTypesEnum.All;

    public enum ChannelActionEnum
    {
        Delete,
        Warn,
        Timeout,
        Ba
    }

    public enum AllowedImageTypesEnum
    {
        All,
        Static,
        Gif
    }
}