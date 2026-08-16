using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("ChannelAccessQuestions")]
public class ChannelAccessQuestion
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; } // integer

    [Column("ConfigId")]
    public int ConfigId { get; set; } // integer

    [Column("Position")]
    public int Position { get; set; } // integer

    [Column("Question")]
    public string Question { get; set; } = null!; // text

    [Column("Placeholder")]
    public string? Placeholder { get; set; } // text

    [Column("Required")]
    public bool Required { get; set; } // boolean

    [Column("Paragraph")]
    public bool Paragraph { get; set; } // boolean

    [Column("MinLength")]
    public int MinLength { get; set; } // integer

    [Column("MaxLength")]
    public int MaxLength { get; set; } // integer

    [Column("DateAdded")]
    public DateTime? DateAdded { get; set; } // timestamp (6) without time zone
}