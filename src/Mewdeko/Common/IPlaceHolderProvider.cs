using System.Collections.Generic;

namespace Mewdeko.Common;

public interface IPlaceholderProvider
{
    public IEnumerable<(string Name, Func<string> Func)> GetPlaceholders();
}