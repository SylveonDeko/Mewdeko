using System.ComponentModel.DataAnnotations;

namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a quote in a guild.
    /// </summary>
    public class Quote : DbEntity
    {
        /// <summary>
        /// Gets or sets the guild ID.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        /// Gets or sets the keyword for the quote.
        /// </summary>
        [Required]
        public string? Keyword { get; set; }

        /// <summary>
        /// Gets or sets the author name of the quote.
        /// </summary>
        [Required]
        public string? AuthorName { get; set; }

        /// <summary>
        /// Gets or sets the author ID of the quote.
        /// </summary>
        public ulong AuthorId { get; set; }

        /// <summary>
        /// Gets or sets the text of the quote.
        /// </summary>
        [Required]
        public string? Text { get; set; }

        /// <summary>
        /// Gets or sets the use count of the quote.
        /// </summary>
        public ulong UseCount { get; set; }
    }

    /// <summary>
    /// Specifies the order type for quotes.
    /// </summary>
    public enum OrderType
    {
        /// <summary>
        /// Order by ID.
        /// </summary>
        Id = -1,

        /// <summary>
        /// Order by keyword.
        /// </summary>
        Keyword = -2
    }
}