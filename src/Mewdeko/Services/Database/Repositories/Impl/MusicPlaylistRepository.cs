using System.Collections.Generic;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class MusicPlaylistRepository : Repository<MusicPlaylist>, IMusicPlaylistRepository
{
    public MusicPlaylistRepository(DbContext context) : base(context)
    {
    }

    public IEnumerable<MusicPlaylist> GetPlaylistsByUser(ulong userId) 
        => Set.AsQueryable().Where(x => x.AuthorId == userId)
               .Include(x => x.Songs);

    public MusicPlaylist GetDefaultPlaylist(ulong userId) =>
        Set.AsQueryable().Where(x => x.AuthorId == userId && x.IsDefault).Include(x => x.Songs).FirstOrDefault();
}