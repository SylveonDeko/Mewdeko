using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a template in a guild.
    /// </summary>
    [Table("Template")]
    public class Template : DbEntity
    {
        /// <summary>
        /// Gets or sets the guild ID.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        /// Gets or sets the output size X.
        /// </summary>
        public int OutputSizeX { get; set; } = 797;

        /// <summary>
        /// Gets or sets the output size Y.
        /// </summary>
        public int OutputSizeY { get; set; } = 279;

        /// <summary>
        /// Gets or sets the time on level format.
        /// </summary>
        public string? TimeOnLevelFormat { get; set; } = "{0}d{1}h{2}m";

        /// <summary>
        /// Gets or sets the time on level X.
        /// </summary>
        public int TimeOnLevelX { get; set; } = 50;

        /// <summary>
        /// Gets or sets the time on level Y.
        /// </summary>
        public int TimeOnLevelY { get; set; } = 204;

        /// <summary>
        /// Gets or sets the time on level font size.
        /// </summary>
        public int TimeOnLevelFontSize { get; set; } = 20;

        /// <summary>
        /// Gets or sets the time on level color.
        /// </summary>
        public string? TimeOnLevelColor { get; set; } = "FF000000";

        /// <summary>
        /// Gets or sets a value indicating whether to show time on level.
        /// </summary>
        public bool ShowTimeOnLevel { get; set; } = true;

        /// <summary>
        /// Gets or sets the awarded X.
        /// </summary>
        public int AwardedX { get; set; } = 445;

        /// <summary>
        /// Gets or sets the awarded Y.
        /// </summary>
        public int AwardedY { get; set; } = 347;

        /// <summary>
        /// Gets or sets the awarded font size.
        /// </summary>
        public int AwardedFontSize { get; set; } = 25;

        /// <summary>
        /// Gets or sets the awarded color.
        /// </summary>
        public string? AwardedColor { get; set; } = "ffffffff";

        /// <summary>
        /// Gets or sets a value indicating whether to show awarded.
        /// </summary>
        public bool ShowAwarded { get; set; } = false;

        /// <summary>
        /// Gets or sets the template user.
        /// </summary>
        [ForeignKey("TemplateUserId")]
        public TemplateUser TemplateUser { get; set; }

        /// <summary>
        /// Gets or sets the template guild.
        /// </summary>
        [ForeignKey("TemplateGuildId")]
        public TemplateGuild TemplateGuild { get; set; }

        /// <summary>
        /// Gets or sets the template club.
        /// </summary>
        [ForeignKey("TemplateClubId")]
        public TemplateClub TemplateClub { get; set; }

        /// <summary>
        /// Gets or sets the template bar.
        /// </summary>
        [ForeignKey("TemplateBarId")]
        public TemplateBar TemplateBar { get; set; }
    }

    /// <summary>
    /// Represents a template user.
    /// </summary>
    public class TemplateUser : DbEntity
    {
        /// <summary>
        /// Gets or sets the text color.
        /// </summary>
        public string? TextColor { get; set; } = "FF000000";

        /// <summary>
        /// Gets or sets the font size.
        /// </summary>
        public int FontSize { get; set; } = 50;

        /// <summary>
        /// Gets or sets the text X.
        /// </summary>
        public int TextX { get; set; } = 120;

        /// <summary>
        /// Gets or sets the text Y.
        /// </summary>
        public int TextY { get; set; } = 70;

        /// <summary>
        /// Gets or sets a value indicating whether to show text.
        /// </summary>
        public bool ShowText { get; set; } = true;

        /// <summary>
        /// Gets or sets the icon X.
        /// </summary>
        public int IconX { get; set; } = 27;

        /// <summary>
        /// Gets or sets the icon Y.
        /// </summary>
        public int IconY { get; set; } = 24;

        /// <summary>
        /// Gets or sets the icon size X.
        /// </summary>
        public int IconSizeX { get; set; } = 73;

        /// <summary>
        /// Gets or sets the icon size Y.
        /// </summary>
        public int IconSizeY { get; set; } = 74;

        /// <summary>
        /// Gets or sets a value indicating whether to show icon.
        /// </summary>
        public bool ShowIcon { get; set; } = true;
    }

    /// <summary>
    /// Represents a template guild.
    /// </summary>
    public class TemplateGuild : DbEntity
    {
        /// <summary>
        /// Gets or sets the guild level color.
        /// </summary>
        public string? GuildLevelColor { get; set; } = "FF000000";

        /// <summary>
        /// Gets or sets the guild level font size.
        /// </summary>
        public int GuildLevelFontSize { get; set; } = 27;

        /// <summary>
        /// Gets or sets the guild level X.
        /// </summary>
        public int GuildLevelX { get; set; } = 42;

        /// <summary>
        /// Gets or sets the guild level Y.
        /// </summary>
        public int GuildLevelY { get; set; } = 206;

        /// <summary>
        /// Gets or sets a value indicating whether to show guild level.
        /// </summary>
        public bool ShowGuildLevel { get; set; } = true;

        /// <summary>
        /// Gets or sets the guild rank color.
        /// </summary>
        public string? GuildRankColor { get; set; } = "FF000000";

        /// <summary>
        /// Gets or sets the guild rank font size.
        /// </summary>
        public int GuildRankFontSize { get; set; } = 25;

        /// <summary>
        /// Gets or sets the guild rank X.
        /// </summary>
        public int GuildRankX { get; set; } = 148;

        /// <summary>
        /// Gets or sets the guild rank Y.
        /// </summary>
        public int GuildRankY { get; set; } = 211;

        /// <summary>
        /// Gets or sets a value indicating whether to show guild rank.
        /// </summary>
        public bool ShowGuildRank { get; set; } = true;
    }

    /// <summary>
    /// Represents a template club.
    /// </summary>
    public class TemplateClub : DbEntity
    {
        /// <summary>
        /// Gets or sets the club icon X.
        /// </summary>
        public int ClubIconX { get; set; } = 717;

        /// <summary>
        /// Gets or sets the club icon Y.
        /// </summary>
        public int ClubIconY { get; set; } = 37;

        /// <summary>
        /// Gets or sets the club icon size X.
        /// </summary>
        public int ClubIconSizeX { get; set; } = 49;

        /// <summary>
        /// Gets or sets the club icon size Y.
        /// </summary>
        public int ClubIconSizeY { get; set; } = 49;

        /// <summary>
        /// Gets or sets a value indicating whether to show club icon.
        /// </summary>
        public bool ShowClubIcon { get; set; } = true;

        /// <summary>
        /// Gets or sets the club name color.
        /// </summary>
        public string? ClubNameColor { get; set; } = "FF000000";

        /// <summary>
        /// Gets or sets the club name font size.
        /// </summary>
        public int ClubNameFontSize { get; set; } = 32;

        /// <summary>
        /// Gets or sets the club name X.
        /// </summary>
        public int ClubNameX { get; set; } = 649;

        /// <summary>
        /// Gets or sets the club name Y.
        /// </summary>
        public int ClubNameY { get; set; } = 50;

        /// <summary>
        /// Gets or sets a value indicating whether to show club name.
        /// </summary>
        public bool ShowClubName { get; set; } = true;
    }

    /// <summary>
    /// Represents a template bar.
    /// </summary>
    public class TemplateBar : DbEntity
    {
        /// <summary>
        /// Gets or sets the bar color.
        /// </summary>
        public string? BarColor { get; set; } = "FF000000";

        /// <summary>
        /// Gets or sets the bar point A X.
        /// </summary>
        public int BarPointAx { get; set; } = 319;

        /// <summary>
        /// Gets or sets the bar point A Y.
        /// </summary>
        public int BarPointAy { get; set; } = 119;

        /// <summary>
        /// Gets or sets the bar point B X.
        /// </summary>
        public int BarPointBx { get; set; } = 284;

        /// <summary>
        /// Gets or sets the bar point B Y.
        /// </summary>
        public int BarPointBy { get; set; } = 250;

        /// <summary>
        /// Gets or sets the bar length.
        /// </summary>
        public int BarLength { get; set; } = 452;

        /// <summary>
        /// Gets or sets the bar transparency.
        /// </summary>
        public byte BarTransparency { get; set; } = 90;

        /// <summary>
        /// Gets or sets the bar direction.
        /// </summary>
        public XpTemplateDirection BarDirection { get; set; } = XpTemplateDirection.Right;

        /// <summary>
        /// Gets or sets a value indicating whether to show the bar.
        /// </summary>
        public bool ShowBar { get; set; } = true;
    }

    /// <summary>
    /// Specifies the direction of the XP template bar.
    /// </summary>
    public enum XpTemplateDirection
    {
        /// <summary>
        /// Up
        /// </summary>
        Up,
        /// <summary>
        /// Down
        /// </summary>
        Down,
        /// <summary>
        /// Left
        /// </summary>
        Left,
        /// <summary>
        /// Right
        /// </summary>
        Right
    }
}
