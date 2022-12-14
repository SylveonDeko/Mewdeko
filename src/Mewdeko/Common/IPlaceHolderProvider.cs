namespace Mewdeko.Common;

public interface IPlaceholderProvider
{
    public IEnumerable<(string Name, Func<string?> Func)> GetPlaceholders();
}