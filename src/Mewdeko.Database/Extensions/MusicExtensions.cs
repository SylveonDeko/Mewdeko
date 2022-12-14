using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class MusicExtensions
{
    public static IEnumerable<MusicPlaylist> GetPlaylistsByUser(this DbSet<MusicPlaylist> set, ulong userId)
        => set.AsQueryable().Where(x => x.AuthorId == userId)
            .Include(x => x.Songs);

    public static Task<MusicPlaylist> GetDefaultPlaylist(this DbSet<MusicPlaylist> set, ulong userId) =>
        set.AsQueryable().Where(x => x.AuthorId == userId && x.IsDefault).Include(x => x.Songs).FirstOrDefaultAsyncEF();
}