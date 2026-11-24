using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

/// <summary>
///     The first time a person began filling in a form. One row per form and person, kept so the
///     owner can compare people who started with people who submitted.
/// </summary>
[Table("form_starts")]
public class FormStart
{
    [Column("form_id", IsPrimaryKey = true, PrimaryKeyOrder = 0, CanBeNull = false)]
    public int FormId { get; set; }

    [Column("user_id", IsPrimaryKey = true, PrimaryKeyOrder = 1, CanBeNull = false)]
    public ulong UserId { get; set; }

    [Column("started_at", CanBeNull = false)]
    public DateTime StartedAt { get; set; }
}
