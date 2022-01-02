using System.Collections.Generic;
using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories;

public interface IMusicPlaylistRepository : IRepository<MusicPlaylist>
{
    List<MusicPlaylist> GetPlaylistsOnPage(int num);
    MusicPlaylist GetWithSongs(int id);
}