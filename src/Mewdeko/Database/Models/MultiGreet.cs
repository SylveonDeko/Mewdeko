#nullable enable
namespace Mewdeko.Database.Models
{
    /// <summary>
    /// Represents a multi-greet configuration for a guild.
    /// </summary>
    public class MultiGreet : DbEntity
    {
        /// <summary>
        /// Gets or sets the guild ID.
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        /// Gets or sets the channel ID.
        /// </summary>
        public ulong ChannelId { get; set; }

        /// <summary>
        /// Gets or sets the greeting message.
        /// </summary>
        public string Message { get; set; } = "Welcome %user%";

        /// <summary>
        /// Gets or sets a value indicating whether to greet bots.
        /// </summary>
        public bool GreetBots { get; set; } = false;

        /// <summary>
        /// Gets or sets the time in minutes after which the message is deleted.
        /// </summary>
        public int DeleteTime { get; set; } = 1;

        /// <summary>
        /// Gets or sets the webhook URL for sending the greeting message.
        /// </summary>
        public string? WebhookUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the greeting is disabled.
        /// </summary>
        public bool Disabled { get; set; } = false;
    }
}