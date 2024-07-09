namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a giveaway in the database.
    /// </summary>
    public class Giveaways : DbEntity
    {
        /// <summary>
        /// Gets or sets the date and time when the giveaway was created.
        /// </summary>
        public DateTime When { get; set; }

        /// <summary>
        /// Gets or sets the channel ID where the giveaway is hosted.
        /// </summary>
        public ulong ChannelId { get; set; }

        /// <summary>
        /// Gets or sets the server ID where the giveaway is hosted.
        /// </summary>
        public ulong ServerId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the giveaway has ended.
        /// </summary>
        public int Ended { get; set; }

        /// <summary>
        /// Gets or sets the message ID of the giveaway announcement.
        /// </summary>
        public ulong MessageId { get; set; }

        /// <summary>
        /// Gets or sets the number of winners for the giveaway.
        /// </summary>
        public int Winners { get; set; }

        /// <summary>
        /// Gets or sets the user ID of the giveaway creator.
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        /// Gets or sets the item being given away.
        /// </summary>
        public string Item { get; set; }

        /// <summary>
        /// Gets or sets the roles restricted to participate in the giveaway.
        /// </summary>
        public string RestrictTo { get; set; }

        /// <summary>
        /// Gets or sets the list of users blacklisted from the giveaway.
        /// </summary>
        public string BlacklistUsers { get; set; }

        /// <summary>
        /// Gets or sets the list of roles blacklisted from the giveaway.
        /// </summary>
        public string BlacklistRoles { get; set; }

        /// <summary>
        /// Gets or sets the emote used for the giveaway.
        /// </summary>
        public string Emote { get; set; } = "<a:HaneMeow:914307922287276052>";
    }
}