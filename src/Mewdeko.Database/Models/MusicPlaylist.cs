namespace Mewdeko.Database.Models;

public class MusicPlaylist : DbEntity
{
    public string Name { get; set; }
    public string Author { get; set; }
    public ulong AuthorId { get; set; }
    public bool IsDefault { get; set; }
    public IEnumerable<PlaylistSong> Songs { get; set; } = new List<PlaylistSong>();
}