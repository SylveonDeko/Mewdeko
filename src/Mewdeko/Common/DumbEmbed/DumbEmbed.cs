using System.Text.Json.Serialization;
using SixLabors.ImageSharp.PixelFormats;

namespace Mewdeko.Common.DumbEmbed;

public class DumbEmbed
{
    [JsonPropertyName("embeds")]
    public List<Embed?> Embeds { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    public class Author
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("icon_url")]
        public string IconUrl { get; set; }
    }

    public class Embed
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; }

        [JsonPropertyName("author")]
        public Author Author { get; set; }

        [JsonPropertyName("footer")]
        public Footer Footer { get; set; }

        [JsonPropertyName("thumbnail")]
        public string Thumbnail { get; set; }

        [JsonPropertyName("image")]
        public string Image { get; set; }

        [JsonPropertyName("fields")]
        public List<Field> Fields { get; set; }
    }

    public class Field
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("inline")]
        public bool Inline { get; set; }
    }

    public class Footer
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("icon_url")]
        public string IconUrl { get; set; }
    }

    public bool IsValid
    {
        get
        {
            return Content != null || Embeds.Any();
        }
    }

    // Nadekos stupid proprietary embed format doesnt get special treatment, it just gets a copypaste from the better format.
    public static Discord.Embed[] ToEmbedArray(IEnumerable<Embed> embeds)
    {
        var toReturn = new List<Discord.Embed>();
        foreach (var i in embeds)
        {
            var embed = new EmbedBuilder();

            if (!string.IsNullOrWhiteSpace(i.Title))
                embed.WithTitle(i.Title);
            if (!string.IsNullOrWhiteSpace(i.Description))
                embed.WithDescription(i.Description);
            if (i != null && Uri.IsWellFormedUriString(i.Url, UriKind.Absolute))
                embed.WithUrl(i.Url);
            if (!string.IsNullOrWhiteSpace(i.Color) && Rgba32.TryParseHex(i.Color, out var color))
            {
                embed.WithColor(new Color(color.R, color.G, color.B));
            }

            if (i.Footer != null)
            {
                embed.WithFooter(efb =>
                {
                    efb.WithText(i.Footer.Text);
                    if (Uri.IsWellFormedUriString(i.Footer.IconUrl, UriKind.Absolute))
                        efb.WithIconUrl(i.Footer.IconUrl);
                });
            }

            if (i.Thumbnail != null && Uri.IsWellFormedUriString(i.Thumbnail, UriKind.Absolute))
                embed.WithThumbnailUrl(i.Thumbnail);
            if (i.Image != null && Uri.IsWellFormedUriString(i.Image, UriKind.Absolute))
                embed.WithImageUrl(i.Image);
            if (i.Author != null && !string.IsNullOrWhiteSpace(i.Author.Name))
            {
                if (!Uri.IsWellFormedUriString(i.Author.IconUrl, UriKind.Absolute))
                    i.Author.IconUrl = null;


                embed.WithAuthor(i.Author.Name, i.Author.IconUrl);
            }

            if (i.Fields != null)
            {
                foreach (var f in i.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Name) && !string.IsNullOrWhiteSpace(f.Value)))
                    embed.AddField(efb => efb.WithName(f.Name).WithValue(f.Value).WithIsInline(f.Inline));
            }

            toReturn.Add(embed.Build());
        }

        return toReturn.ToArray();
    }
}