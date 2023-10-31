namespace Mewdeko.Services.TwitchChat.Entities
{
    public class BotConnection
    {
        public Guid Id { get; set; }
        public string? ChannelId { get; set; }
        public string? Login { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool? Active { get; set; }
        public string ProfileImageUrl { get; set; }
        public string Email { get; set; }
        public UserAccessLevelEnum AccessLevel { get; set; } = UserAccessLevelEnum.Default;
    }
}
