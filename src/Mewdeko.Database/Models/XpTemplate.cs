using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

[Table("Template")]
public class Template : DbEntity
{
    //General Stuff
    public ulong GuildId { get; set; }
    public int OutputSizeX { get; set; } = 797;
    public int OutputSizeY { get; set; } = 279;

    // Time on Level
    public string TimeOnLevelFormat { get; set; } = "{0}d{1}h{2}m";
    public int TimeOnLevelX { get; set; } = 50;
    public int TimeOnLevelY { get; set; } = 204;
    public int TimeOnLevelFontSize { get; set; } = 20;
    public string TimeOnLevelColor { get; set; } = "FF000000";
    public bool ShowTimeOnLevel { get; set; } = true;

    // Awarded
    public int AwardedX { get; set; } = 445;
    public int AwardedY { get; set; } = 347;
    public int AwardedFontSize { get; set; } = 25;
    public string AwardedColor { get; set; } = "ffffffff";
    public bool ShowAwarded { get; set; }

    // Navigation Properties
    [ForeignKey("TemplateUserId")]
    public TemplateUser TemplateUser { get; set; }

    [ForeignKey("TemplateGuildId")]
    public TemplateGuild TemplateGuild { get; set; }

    [ForeignKey("TemplateClubId")]
    public TemplateClub TemplateClub { get; set; }

    [ForeignKey("TemplateBarId")]
    public TemplateBar TemplateBar { get; set; }
}

public class TemplateUser : DbEntity
{
    // Username Text
    public string TextColor { get; set; } = "FF000000";
    public int FontSize { get; set; } = 50;
    public int TextX { get; set; } = 120;
    public int TextY { get; set; } = 70;
    public bool ShowText { get; set; } = true;

    // Icon
    public int IconX { get; set; } = 27;
    public int IconY { get; set; } = 24;
    public int IconSizeX { get; set; } = 73;
    public int IconSizeY { get; set; } = 74;
    public bool ShowIcon { get; set; } = true;
}

public class TemplateGuild : DbEntity
{
    // Guild Level
    public string GuildLevelColor { get; set; } = "FF000000";
    public int GuildLevelFontSize { get; set; } = 27;
    public int GuildLevelX { get; set; } = 42;
    public int GuildLevelY { get; set; } = 206;
    public bool ShowGuildLevel { get; set; } = true;

    // Guild Rank
    public string GuildRankColor { get; set; } = "FF000000";
    public int GuildRankFontSize { get; set; } = 25;
    public int GuildRankX { get; set; } = 148;
    public int GuildRankY { get; set; } = 211;
    public bool ShowGuildRank { get; set; } = true;
}

public class TemplateClub : DbEntity
{
    // Club Icon
    public int ClubIconX { get; set; } = 717;
    public int ClubIconY { get; set; } = 37;
    public int ClubIconSizeX { get; set; } = 49;
    public int ClubIconSizeY { get; set; } = 49;
    public bool ShowClubIcon { get; set; } = true;

    // Club Name
    public string ClubNameColor { get; set; } = "FF000000";
    public int ClubNameFontSize { get; set; } = 32;
    public int ClubNameX { get; set; } = 649;
    public int ClubNameY { get; set; } = 50;
    public bool ShowClubName { get; set; } = true;
}

public class TemplateBar : DbEntity
{
    public string BarColor { get; set; } = "FF000000";
    public int BarPointAx { get; set; } = 319;
    public int BarPointAy { get; set; } = 119;
    public int BarPointBx { get; set; } = 284;
    public int BarPointBy { get; set; } = 250;
    public int BarLength { get; set; } = 452;
    public byte BarTransparency { get; set; } = 90;
    public XpTemplateDirection BarDirection { get; set; } = XpTemplateDirection.Right;
    public bool ShowBar { get; set; } = true;
}

public enum XpTemplateDirection
{
    Up,
    Down,
    Left,
    Right
}