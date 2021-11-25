#nullable enable
using System.Threading.Tasks;

namespace Mewdeko.Modules.Music.Common
{
    public interface IPlatformQueryResolver
    {
        Task<ITrackInfo?> ResolveByQueryAsync(string query);
    }
}