using System.ComponentModel.DataAnnotations.Schema;

namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a song in a music playlist.
    /// </summary>
    [Table("PlaylistSong")]
    public class PlaylistSong : DbEntity
    {
        /// <summary>
        /// Gets or sets the music playlist ID.
        /// </summary>
        [ForeignKey("MusicPlaylistId")]
        public int MusicPlaylistId { get; set; }

        /// <summary>
        /// Gets or sets the provider of the song.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Gets or sets the provider type of the song.
        /// </summary>
        public Platform ProviderType { get; set; }

        /// <summary>
        /// Gets or sets the title of the song.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the URI of the song.
        /// </summary>
        public string? Uri { get; set; }

        /// <summary>
        /// Gets or sets the query for the song.
        /// </summary>
        public string? Query { get; set; }
    }

    /// <summary>
    /// Specifies the platform type.
    /// </summary>
    public enum Platform
    {
        /// <summary>
        /// YouTube platform.
        /// </summary>
        Youtube,

        /// <summary>
        /// Spotify platform.
        /// </summary>
        Spotify,

        /// <summary>
        /// SoundCloud platform.
        /// </summary>
        Soundcloud,

        /// <summary>
        /// URL platform.
        /// </summary>
        Url,

        /// <summary>
        /// File platform.
        /// </summary>
        File,

        /// <summary>
        /// Twitch platform.
        /// </summary>
        Twitch
    }
}