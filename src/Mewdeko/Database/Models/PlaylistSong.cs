using Mewdeko.Modules.Music.Extensions;

namespace Mewdeko.Database.Models;

public class PlaylistSong : DbEntity
{
    public string Provider { get; set; }
    public AdvancedLavaTrack.Platform ProviderType { get; set; }
    public string Title { get; set; }
    public string Uri { get; set; }
    public string Query { get; set; }
}