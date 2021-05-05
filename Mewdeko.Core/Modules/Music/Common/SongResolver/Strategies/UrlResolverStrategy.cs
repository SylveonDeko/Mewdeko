using Mewdeko.Core.Services.Database.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Music.Common.SongResolver.Strategies
{
    public class UrlResolverStrategy : IResolveStrategy
    {
        public Task<SongInfo> ResolveSong(string query)
        {
            return Task.FromResult(new SongInfo
            {
                Uri = () => Task.FromResult(new Uri(query).AbsoluteUri),
                Title = Path.GetFileNameWithoutExtension(query),
                Provider = "Direct Url / File",
                ProviderType = MusicType.Url,
                Query = query,
                Thumbnail = "https://cdn.discordapp.com/attachments/155726317222887425/261850914783100928/1482522077_music.png",
            });
        }
    }
}
