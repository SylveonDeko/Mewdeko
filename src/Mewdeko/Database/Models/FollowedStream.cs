using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.Common;

namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a stream followed in a guild.
    /// </summary>
    public class FollowedStream : DbEntity
    {
        /// <summary>
        /// Specifies the type of the stream.
        /// </summary>
        public enum FType
        {
            /// <summary>
            /// Twitch stream.
            /// </summary>
            Twitch = 0,
            /// <summary>
            /// Picarto stream.
            /// </summary>
            Picarto = 3,
            /// <summary>
            /// YouTube stream.
            /// </summary>
            Youtube = 4,
            /// <summary>
            /// Facebook stream.
            /// </summary>
            Facebook = 5,
            /// <summary>
            /// Trovo stream.
            /// </summary>
            Trovo = 6
        }

        /// <summary>
        /// Gets or sets the guild ID.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        /// Gets or sets the guild configuration ID.
        /// </summary>
        [ForeignKey("GuildConfigId")]
        public int GuildConfigId { get; set; }

        /// <summary>
        /// Gets or sets the channel ID.
        /// </summary>
        public ulong ChannelId { get; set; }

        /// <summary>
        /// Gets or sets the username of the stream.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the type of the stream.
        /// </summary>
        public FType Type { get; set; }

        /// <summary>
        /// Gets or sets the message associated with the followed stream.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="other">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        protected bool Equals(FollowedStream other) =>
            ChannelId == other.ChannelId &&
            string.Equals(Username.Trim(), other.Username.Trim(), StringComparison.InvariantCultureIgnoreCase) &&
            Type == other.Type;

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode() =>
            HashCode.Combine(ChannelId, Username, (int)Type);

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj) =>
            obj is FollowedStream fs && Equals(fs);

        /// <summary>
        /// Creates a key for the stream data.
        /// </summary>
        /// <returns>A key for the stream data.</returns>
        public StreamDataKey CreateKey() =>
            new(Type, Username.ToLower());
    }
}
