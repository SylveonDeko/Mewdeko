using Discord;
using NadekoBot.Extensions;
using Newtonsoft.Json;
using System;

namespace NadekoBot.Common
{
    public class CREmbed
    {
        public CREmbedAuthor Author { get; set; }
        public string PlainText { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Url { get; set; }
        public CREmbedFooter Footer { get; set; }
        public string Thumbnail { get; set; }
        public string Image { get; set; }
        public CREmbedField[] Fields { get; set; }
        public uint Color { get; set; } = 7458112;

        public bool IsValid =>
            IsEmbedValid || !string.IsNullOrWhiteSpace(PlainText);
        public bool IsEmbedValid =>
            !string.IsNullOrWhiteSpace(Title) ||
            !string.IsNullOrWhiteSpace(Description) ||
            !string.IsNullOrWhiteSpace(Url) ||
            !string.IsNullOrWhiteSpace(Thumbnail) ||
            !string.IsNullOrWhiteSpace(Image) ||
            (Footer != null && (!string.IsNullOrWhiteSpace(Footer.Text) || !string.IsNullOrWhiteSpace(Footer.IconUrl))) ||
            (Fields != null && Fields.Length > 0);

        public EmbedBuilder ToEmbed()
        {
            var embed = new EmbedBuilder();

            if (!string.IsNullOrWhiteSpace(Title))
                embed.WithTitle(Title);
            if (!string.IsNullOrWhiteSpace(Description))
                embed.WithDescription(Description);
            if (Url != null && Uri.IsWellFormedUriString(Url, UriKind.Absolute))
                embed.WithUrl(Url);
            embed.WithColor(new Discord.Color(Color));
            if (Footer != null)
                embed.WithFooter(efb =>
                {
                    efb.WithText(Footer.Text);
                    if (Uri.IsWellFormedUriString(Footer.IconUrl, UriKind.Absolute))
                        efb.WithIconUrl(Footer.IconUrl);
                });

            if (Thumbnail != null && Uri.IsWellFormedUriString(Thumbnail, UriKind.Absolute))
                embed.WithThumbnailUrl(Thumbnail);
            if (Image != null && Uri.IsWellFormedUriString(Image, UriKind.Absolute))
                embed.WithImageUrl(Image);
            if (Author != null && !string.IsNullOrWhiteSpace(Author.Name))
            {
                if (!Uri.IsWellFormedUriString(Author.IconUrl, UriKind.Absolute))
                    Author.IconUrl = null;
                if (!Uri.IsWellFormedUriString(Author.Url, UriKind.Absolute))
                    Author.Url = null;

                embed.WithAuthor(Author.Name, Author.IconUrl, Author.Url);
            }

            if (Fields != null)
                foreach (var f in Fields)
                {
                    if (!string.IsNullOrWhiteSpace(f.Name) && !string.IsNullOrWhiteSpace(f.Value))
                        embed.AddField(efb => efb.WithName(f.Name).WithValue(f.Value).WithIsInline(f.Inline));
                }

            return embed;
        }

        public static bool TryParse(string input, out CREmbed embed)
        {
            embed = null;
            if (string.IsNullOrWhiteSpace(input) || !input.Trim().StartsWith('{'))
                return false;

            try
            {
                var crembed = JsonConvert.DeserializeObject<CREmbed>(input);

                if (crembed.Fields != null && crembed.Fields.Length > 0)
                    foreach (var f in crembed.Fields)
                    {
                        f.Name = f.Name.TrimTo(256);
                        f.Value = f.Value.TrimTo(1024);
                    }
                if (!crembed.IsValid)
                    return false;

                embed = crembed;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class CREmbedField
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public bool Inline { get; set; }
    }

    public class CREmbedFooter
    {
        public string Text { get; set; }
        public string IconUrl { get; set; }
        [JsonProperty("icon_url")]
        private string Icon_Url { set => IconUrl = value; }
    }
    public class CREmbedAuthor
    {
        public string Name { get; set; }
        public string IconUrl { get; set; }
        [JsonProperty("icon_url")]
        private string Icon_Url { set => IconUrl = value; }
        public string Url { get; set; }
    }
}
