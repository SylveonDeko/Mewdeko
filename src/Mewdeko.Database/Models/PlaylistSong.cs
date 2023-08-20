using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models;

public class PlaylistSong : DbEntity
{
    [ForeignKey("MusicPlaylistId")]
    public int MusicPlaylistId { get; set; }

    public string Provider { get; set; }
    public Platform ProviderType { get; set; }
    public string Title { get; set; }
    public string Uri { get; set; }
    public string Query { get; set; }
}

public enum Platform
{
    Youtube,
    Spotify,
    Soundcloud,
    Url,
    File,
    Twitch
}