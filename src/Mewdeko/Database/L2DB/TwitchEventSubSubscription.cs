using LinqToDB.Mapping;

#pragma warning disable 1591
#nullable enable

namespace DataModel;

[Table("TwitchEventSubSubscriptions")]
public class TwitchEventSubSubscription
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("GuildId")]
    public ulong GuildId { get; set; }

    [Column("TwitchSubscriptionId")]
    public string TwitchSubscriptionId { get; set; } = "";

    [Column("SubscriptionType")]
    public string SubscriptionType { get; set; } = "";

    [Column("Version")]
    public string Version { get; set; } = "";

    [Column("Status")]
    public string Status { get; set; } = "";

    [Column("TransportMethod")]
    public string TransportMethod { get; set; } = "";

    [Column("SessionId")]
    public string? SessionId { get; set; }

    /// <summary>Gets or sets the webhook callback URL used by this subscription.</summary>
    [Column("CallbackUrl")]
    public string? CallbackUrl { get; set; }

    [Column("Cost")]
    public int Cost { get; set; }

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; }

    [Column("LastUpdatedAt")]
    public DateTime? LastUpdatedAt { get; set; }
}