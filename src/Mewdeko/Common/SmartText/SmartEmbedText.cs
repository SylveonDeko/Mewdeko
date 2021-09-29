using System;
using Discord;
using Mewdeko.Extensions;

namespace Mewdeko
{
    public sealed record SmartEmbedText : SmartText
    {
        public string PlainText { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Url { get; set; }
        public string Thumbnail { get; set; }
        public string Image { get; set; }

        public SmartTextEmbedAuthor Author { get; set; }
        public SmartTextEmbedFooter Footer { get; set; }
        public SmartTextEmbedField[] Fields { get; set; }

        public uint Color { get; set; } = 7458112;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(Title) ||
            !string.IsNullOrWhiteSpace(Description) ||
            !string.IsNullOrWhiteSpace(Url) ||
            !string.IsNullOrWhiteSpace(Thumbnail) ||
            !string.IsNullOrWhiteSpace(Image) ||
            Footer != null && (!string.IsNullOrWhiteSpace(Footer.Text) || !string.IsNullOrWhiteSpace(Footer.IconUrl)) ||
            Fields != null && Fields.Length > 0;

        public EmbedBuilder GetEmbed()
        {
            var embed = new EmbedBuilder()
                .WithColor(Color);

            if (!string.IsNullOrWhiteSpace(Title))
                embed.WithTitle(Title);

            if (!string.IsNullOrWhiteSpace(Description))
                embed.WithDescription(Description);

            if (Url != null && Uri.IsWellFormedUriString(Url, UriKind.Absolute))
                embed.WithUrl(Url);

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
                    if (!string.IsNullOrWhiteSpace(f.Name) && !string.IsNullOrWhiteSpace(f.Value))
                        embed.AddField(f.Name, f.Value, f.Inline);

            return embed;
        }

        public void NormalizeFields()
        {
            if (Fields != null && Fields.Length > 0)
                foreach (var f in Fields)
                {
                    f.Name = f.Name.TrimTo(256);
                    f.Value = f.Value.TrimTo(1024);
                }
        }
    }
}