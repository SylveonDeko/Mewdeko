#nullable enable
using System.Threading.Tasks;

namespace Mewdeko.Core.Modules.Music
{
    public interface IPlatformQueryResolver
    {
        Task<ITrackInfo?> ResolveByQueryAsync(string query);
    }
}