namespace Mewdeko.Common.Yml;

/// <summary>
///     Attribute used to add comments to properties when serializing to YAML.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class CommentAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CommentAttribute" /> class with the specified comment.
    /// </summary>
    /// <param name="comment">The comment text to be associated with the property.</param>
    public CommentAttribute(string? comment)
    {
        Comment = comment;
    }

    /// <summary>
    ///     Gets the comment associated with the property.
    /// </summary>
    public string? Comment { get; }
}