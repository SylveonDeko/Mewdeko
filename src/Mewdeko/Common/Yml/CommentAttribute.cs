namespace Mewdeko.Common.Yml;

public class CommentAttribute : Attribute
{
    public CommentAttribute(string? comment) => Comment = comment;

    public string? Comment { get; }
}