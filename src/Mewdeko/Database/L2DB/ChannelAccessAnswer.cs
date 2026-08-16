using LinqToDB.Mapping;

#pragma warning disable 1573, 1591
#nullable enable

namespace DataModel;

[Table("ChannelAccessAnswers")]
public class ChannelAccessAnswer
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, SkipOnInsert = true, SkipOnUpdate = true)]
    public int Id { get; set; } // integer

    [Column("ApplicationId")]
    public int ApplicationId { get; set; } // integer

    [Column("QuestionId")]
    public int? QuestionId { get; set; } // integer

    [Column("Position")]
    public int Position { get; set; } // integer

    [Column("Question")]
    public string Question { get; set; } = null!; // text

    [Column("Answer")]
    public string Answer { get; set; } = null!; // text
}