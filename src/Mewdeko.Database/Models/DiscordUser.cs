namespace Mewdeko.Database.Models;

public class DiscordUser : DbEntity
{
    public ulong UserId { get; set; }
    public string Username { get; set; }
    public string Discriminator { get; set; }
    public string AvatarId { get; set; }

    public ClubInfo Club { get; set; }
    public bool IsClubAdmin { get; set; }

    public int TotalXp { get; set; }
    public DateTime LastLevelUp { get; set; } = DateTime.UtcNow;
    public DateTime LastXpGain { get; set; } = DateTime.MinValue;
    public XpNotificationLocation NotifyOnLevelUp { get; set; }

    public long CurrencyAmount { get; set; }

    public override bool Equals(object obj) =>
        obj is DiscordUser du
&& du.UserId == UserId;

    public override int GetHashCode() => UserId.GetHashCode();

    public override string ToString() => $"{Username}#{Discriminator}";

    public bool IsDragon { get; set; }
    public string Pronouns { get; set; }
    public string? PronounsClearedReason { get; set; }
    public bool PronounsDisabled { get; set; }
}
