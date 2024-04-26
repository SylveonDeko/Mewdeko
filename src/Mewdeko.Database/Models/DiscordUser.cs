#nullable enable
namespace Mewdeko.Database.Models;

public class DiscordUser : DbEntity
{
    public enum BirthdayDisplayModeEnum
    {
        Default,
        MonthOnly,
        YearOnly,
        MonthAndDate,
        Disabled
    }

    public enum ProfilePrivacyEnum
    {
        Public = 0,
        Private = 1
    }

    public ulong UserId { get; set; }
    public string? Username { get; set; }
    public string? Discriminator { get; set; }
    public string? AvatarId { get; set; }
    public bool IsClubAdmin { get; set; } = false;

    public int TotalXp { get; set; }
    public DateTime? LastLevelUp { get; set; } = DateTime.UtcNow;
    public XpNotificationLocation NotifyOnLevelUp { get; set; }

    public bool IsDragon { get; set; } = false;
    public string? Pronouns { get; set; }
    public string? PronounsClearedReason { get; set; }

    public bool PronounsDisabled { get; set; } = false;

    // public string PndbCache { get; set; }
    public string? Bio { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? ZodiacSign { get; set; }
    public ProfilePrivacyEnum ProfilePrivacy { get; set; } = ProfilePrivacyEnum.Public;
    public uint? ProfileColor { get; set; } = 0;
    public DateTime? Birthday { get; set; }
    public string? SwitchFriendCode { get; set; } = null;
    public BirthdayDisplayModeEnum BirthdayDisplayMode { get; set; } = BirthdayDisplayModeEnum.Default;
    public bool StatsOptOut { get; set; } = false;

    public override int GetHashCode() => UserId.GetHashCode();

    public override string ToString() => $"{Username}#{Discriminator}";
}