namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents the role connection authentication storage.
    /// </summary>
    public class RoleConnectionAuthStorage : DbEntity
    {
        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        /// Gets or sets the scopes for the role connection.
        /// </summary>
        public string Scopes { get; set; }

        /// <summary>
        /// Gets or sets the token for the role connection.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Gets or sets the refresh token for the role connection.
        /// </summary>
        public string RefreshToken { get; set; }

        /// <summary>
        /// Gets or sets the expiration date and time of the token.
        /// </summary>
        public DateTime ExpiresAt { get; set; }
    }
}