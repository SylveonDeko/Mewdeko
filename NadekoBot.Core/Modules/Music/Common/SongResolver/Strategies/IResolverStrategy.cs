using System.Threading.Tasks;

namespace NadekoBot.Modules.Music.Common.SongResolver.Strategies
{
    public interface IResolveStrategy
    {
        Task<SongInfo> ResolveSong(string query);
    }
}
