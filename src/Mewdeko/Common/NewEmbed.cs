using Discord;
using Newtonsoft.Json;

namespace Mewdeko.Common;

public class Author
{
    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("url")] public string Url { get; set; }

    [JsonProperty("icon_url")] public string IconUrl { get; set; }
}

public class Thumbnail
{
    [JsonProperty("url")] public string Url { get; set; }
}

public class Image
{
    [JsonProperty("url")] public string Url { get; set; }
}

public class Footer
{
    [JsonProperty("text")] public string Text { get; set; }

    [JsonProperty("icon_url")] public string IconUrl { get; set; }
}

public class Field
{
    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("value")] public string Value { get; set; }

    [JsonProperty("inline")] public bool Inline { get; set; }
}

public class Embed
{
    [JsonProperty("title")] public string Title { get; set; }

    [JsonProperty("description")] public string Description { get; set; }

    [JsonProperty("color")] public uint Color { get; set; }

    [JsonProperty("timestamp")] public DateTime Timestamp { get; set; }

    [JsonProperty("url")] public string Url { get; set; }

    [JsonProperty("author")] public Author Author { get; set; }

    [JsonProperty("thumbnail")] public Thumbnail Thumbnail { get; set; }

    [JsonProperty("image")] public Image Image { get; set; }

    [JsonProperty("footer")] public Footer Footer { get; set; }

    [JsonProperty("fields")] public List<Field> Fields { get; set; }
}

public class NewEmbed
{
    [JsonProperty("content")] public string Content { get; set; }

    [JsonProperty("embed")] public Embed Embed { get; set; }
    
    public bool IsValid =>
        IsEmbedValid || !string.IsNullOrWhiteSpace(Content);
    
    public bool IsEmbedValid =>
        !string.IsNullOrWhiteSpace(Embed.Description) ||
        !string.IsNullOrWhiteSpace(Embed.Url) ||
        Embed.Thumbnail != null ||
        Embed.Image != null ||
        (Embed.Footer != null && (!string.IsNullOrWhiteSpace(Embed.Footer.Text) || !string.IsNullOrWhiteSpace(Embed.Footer.IconUrl))) ||
        Embed.Fields is {Count: > 0};
    
    public EmbedBuilder ToEmbed()
    {
        var embed = new EmbedBuilder();

        if (!string.IsNullOrWhiteSpace(Embed.Title))
            embed.WithTitle(Embed.Title);
        if (!string.IsNullOrWhiteSpace(Embed.Description))
            embed.WithDescription(Embed.Description);
        if (Embed.Url != null && Uri.IsWellFormedUriString(Embed.Url, UriKind.Absolute))
            embed.WithUrl(Embed.Url);
        embed.WithColor(new Color(Embed.Color));
        if (Embed.Footer != null)
            embed.WithFooter(efb =>
            {
                efb.WithText(Embed.Footer.Text);
                if (Uri.IsWellFormedUriString(Embed.Footer.IconUrl, UriKind.Absolute))
                    efb.WithIconUrl(Embed.Footer.IconUrl);
            });

        if (Embed.Thumbnail != null && Uri.IsWellFormedUriString(Embed.Thumbnail.Url, UriKind.Absolute))
            embed.WithThumbnailUrl(Embed.Thumbnail.Url);
        if (Embed.Image != null && Uri.IsWellFormedUriString(Embed.Image.Url, UriKind.Absolute))
            embed.WithImageUrl(Embed.Image.Url);
        if (Embed.Author != null && !string.IsNullOrWhiteSpace(Embed.Author.Name))
        {
            if (!Uri.IsWellFormedUriString(Embed.Author.IconUrl, UriKind.Absolute))
                Embed.Author.IconUrl = null;
            if (!Uri.IsWellFormedUriString(Embed.Author.Url, UriKind.Absolute))
                Embed.Author.Url = null;

            embed.WithAuthor(Embed.Author.Name, Embed.Author.IconUrl, Embed.Author.Url);
        }

        if (Embed.Fields == null) return embed;
        {
            foreach (var f in Embed.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Name) && !string.IsNullOrWhiteSpace(f.Value)))
                embed.AddField(efb => efb.WithName(f.Name).WithValue(f.Value).WithIsInline(f.Inline));
        }

        return embed;
    }
}