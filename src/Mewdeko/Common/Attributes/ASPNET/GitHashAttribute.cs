namespace Mewdeko.Common.Attributes.ASPNET;

/// <inheritdoc />
[AttributeUsage(AttributeTargets.Assembly)]
public class GitHashAttribute : Attribute
{
    /// <inheritdoc />
    public GitHashAttribute(string hash)
    {
        Hash = hash;
    }

    /// <summary>
    ///     Ze Hash
    /// </summary>
    public string Hash { get; }
}