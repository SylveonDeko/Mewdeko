using System.Collections.Generic;
using Mewdeko.Services.Database.Models;

namespace Mewdeko.Services.Database.Repositories;

public interface IMusicPlaylistRepository : IRepository<MusicPlaylist>
{
    IEnumerable<MusicPlaylist> GetPlaylistsByUser(ulong userId);
    MusicPlaylist GetDefaultPlaylist(ulong userId);
}