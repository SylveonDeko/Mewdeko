using Mewdeko.Modules.Music.Common.SongResolver.Strategies;
using Mewdeko.Core.Services.Database.Models;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Music.Common.SongResolver
{
    public interface ISongResolverFactory
    {
        Task<IResolveStrategy> GetResolveStrategy(string query, MusicType? musicType);
    }
}
