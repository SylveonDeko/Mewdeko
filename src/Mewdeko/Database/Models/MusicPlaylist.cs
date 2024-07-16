namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a music playlist.
    /// </summary>
    public class MusicPlaylist : DbEntity
    {
        /// <summary>
        /// Gets or sets the name of the playlist.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the author of the playlist.
        /// </summary>
        public string? Author { get; set; }

        /// <summary>
        /// Gets or sets the author ID of the playlist.
        /// </summary>
        public ulong AuthorId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the playlist is default.
        /// </summary>
        public bool IsDefault { get; set; } = false;

        /// <summary>
        /// Gets or sets the songs in the playlist.
        /// </summary>
        public IEnumerable<PlaylistSong> Songs { get; set; } = new List<PlaylistSong>();
    }
}