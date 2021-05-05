using System.Threading.Tasks;

namespace Mewdeko.Modules.Music.Common.SongResolver.Strategies
{
    public interface IResolveStrategy
    {
        Task<SongInfo> ResolveSong(string query);
    }
}
