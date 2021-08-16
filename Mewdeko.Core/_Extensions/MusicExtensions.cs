using Discord;

namespace Mewdeko.Extensions
{
    public static class MusicExtensions
    {
        public static EmbedAuthorBuilder WithMusicIcon(this EmbedAuthorBuilder eab)
        {
            return eab.WithIconUrl("http://i.imgur.com/nhKS3PT.png");
        }
    }
}