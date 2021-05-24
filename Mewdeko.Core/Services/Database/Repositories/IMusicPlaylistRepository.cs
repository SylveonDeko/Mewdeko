using System.Collections.Generic;
using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Core.Services.Database.Repositories
{
    public interface IMusicPlaylistRepository : IRepository<MusicPlaylist>
    {
        List<MusicPlaylist> GetPlaylistsOnPage(int num);
        MusicPlaylist GetWithSongs(int id);
    }
}