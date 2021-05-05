using Mewdeko.Core.Services.Database.Models;
using System.Collections.Generic;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface IMusicPlaylistRepository : IRepository<MusicPlaylist>
    {
        List<MusicPlaylist> GetPlaylistsOnPage(int num);
        MusicPlaylist GetWithSongs(int id);
    }
}
