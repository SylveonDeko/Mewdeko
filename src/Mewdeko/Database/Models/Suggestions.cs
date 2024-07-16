namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a suggestion in a guild.
    /// </summary>
    public class SuggestionsModel : DbEntity
    {
        /// <summary>
        /// Gets or sets the guild ID.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        /// Gets or sets the suggestion ID.
        /// </summary>
        public ulong SuggestionId { get; set; }

        /// <summary>
        /// Gets or sets the suggestion text.
        /// </summary>
        public string? Suggestion { get; set; }

        /// <summary>
        /// Gets or sets the message ID.
        /// </summary>
        public ulong MessageId { get; set; }

        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        /// Gets or sets the count of emote 1.
        /// </summary>
        public int EmoteCount1 { get; set; } = 0;

        /// <summary>
        /// Gets or sets the count of emote 2.
        /// </summary>
        public int EmoteCount2 { get; set; } = 0;

        /// <summary>
        /// Gets or sets the count of emote 3.
        /// </summary>
        public int EmoteCount3 { get; set; } = 0;

        /// <summary>
        /// Gets or sets the count of emote 4.
        /// </summary>
        public int EmoteCount4 { get; set; } = 0;

        /// <summary>
        /// Gets or sets the count of emote 5.
        /// </summary>
        public int EmoteCount5 { get; set; } = 0;

        /// <summary>
        /// Gets or sets the state change user ID.
        /// </summary>
        public ulong StateChangeUser { get; set; } = 0;

        /// <summary>
        /// Gets or sets the state change count.
        /// </summary>
        public ulong StateChangeCount { get; set; } = 0;

        /// <summary>
        /// Gets or sets the state change message ID.
        /// </summary>
        public ulong StateChangeMessageId { get; set; } = 0;

        /// <summary>
        /// Gets or sets the current state.
        /// </summary>
        public int CurrentState { get; set; } = 0;
    }
}
