using NadekoBot.Core.Services.Database.Models;
using System.IO;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Music.Common.SongResolver.Strategies
{
    public class LocalSongResolveStrategy : IResolveStrategy
    {
        public Task<SongInfo> ResolveSong(string query)
        {
            return Task.FromResult(new SongInfo
            {
                Uri = () => Task.FromResult("\"" + Path.GetFullPath(query) + "\""),
                Title = Path.GetFileNameWithoutExtension(query),
                Provider = "Local File",
                ProviderType = MusicType.Local,
                Query = query,
                Thumbnail = "https://cdn.discordapp.com/attachments/155726317222887425/261850914783100928/1482522077_music.png",
            });
        }
    }
}
