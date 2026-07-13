namespace Mewdeko.Modules.Xp.Models;

/// <summary>
///     A user-created layer rendered on an XP card.
/// </summary>
internal sealed class XpCardElement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = "rectangle";
    public string Label { get; set; } = "Shape";
    public bool Visible { get; set; } = true;
    public int ZIndex { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; } = 120;
    public float Height { get; set; } = 60;
    public float Rotation { get; set; }
    public float Opacity { get; set; } = 1;
    public float CornerRadius { get; set; }
    public string Fill { get; set; } = "#5865F2";
    public string? GradientEnd { get; set; }
    public float GradientAngle { get; set; }
    public string Stroke { get; set; } = "#00000000";
    public float StrokeWidth { get; set; }
    public string ShadowColor { get; set; } = "#00000080";
    public float ShadowBlur { get; set; }
    public float ShadowX { get; set; }
    public float ShadowY { get; set; }
    public string Text { get; set; } = "Custom text";
    public float FontSize { get; set; } = 24;
    public string TextAlign { get; set; } = "left";
    public string Url { get; set; } = "";
    public string ProgressStyle { get; set; } = "rounded";
    public string TrackFill { get; set; } = "#FFFFFF30";
    public int Segments { get; set; } = 10;
}