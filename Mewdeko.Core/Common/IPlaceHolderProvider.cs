using System;
using System.Collections.Generic;

namespace Mewdeko.Core.Common
{
    public interface IPlaceholderProvider
    {
        public IEnumerable<(string Name, Func<string> Func)> GetPlaceholders();
    }
}