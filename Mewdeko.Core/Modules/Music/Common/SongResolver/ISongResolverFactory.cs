using System.Threading.Tasks;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Modules.Music.Common.SongResolver.Strategies;

namespace Mewdeko.Modules.Music.Common.SongResolver
{
    public interface ISongResolverFactory
    {
        Task<IResolveStrategy> GetResolveStrategy(string query, MusicType? musicType);
    }
}