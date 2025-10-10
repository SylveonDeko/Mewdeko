using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     Represents a user's submission to a form
/// </summary>
[Table("form_responses")]
public class FormResponse
{
    [Column("id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; }

    [Column("form_id", CanBeNull = false)]
    public int FormId { get; set; }

    [Column("user_id")]
    public ulong? UserId { get; set; }

    [Column("username")]
    public string? Username { get; set; }

    [Column("submitted_at", CanBeNull = false)]
    public DateTime SubmittedAt { get; set; }

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("message_id")]
    public ulong? MessageId { get; set; }
}