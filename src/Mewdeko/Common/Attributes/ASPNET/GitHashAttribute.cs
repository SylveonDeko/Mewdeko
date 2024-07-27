namespace Mewdeko.Common.Attributes.ASPNET;

/// <inheritdoc />
[AttributeUsage(AttributeTargets.Assembly)]
public class GitHashAttribute : Attribute
{
    /// <summary>
    /// Ze Hash
    /// </summary>
    public string Hash { get; }

    /// <inheritdoc />
    public GitHashAttribute(string hash)
    {
        Hash = hash;
    }
}