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
    [JsonProperty("title")] public string? Title { get; set; }

    [JsonProperty("description")] public string? Description { get; set; }

    [JsonProperty("color")] public uint Color { get; set; }

    [JsonProperty("timestamp")] public DateTime? Timestamp { get; set; }

    [JsonProperty("url")] public string? Url { get; set; }

    [JsonProperty("author")] public Author? Author { get; set; }

    [JsonProperty("thumbnail")] public Thumbnail? Thumbnail { get; set; }

    [JsonProperty("image")] public Image? Image { get; set; }

    [JsonProperty("footer")] public Footer? Footer { get; set; }

    [JsonProperty("fields")] public List<Field>? Fields { get; set; }
}

public class NewEmbed
{
    [JsonProperty("content")] public string? Content { get; set; }

    [JsonProperty("embed")] public Embed? Embed { get; set; }
    [JsonProperty("embeds")] public Embed[]? Embeds { get; set; }

    public bool IsValid 
        => (Embeds is not null || IsEmbedValid || string.IsNullOrWhiteSpace(Content)) && (Embeds is not null || IsEmbedValid);

    public bool IsEmbedValid =>
        !string.IsNullOrWhiteSpace(Embed?.Description) ||
        !string.IsNullOrWhiteSpace(Embed?.Url) ||
        Embed?.Thumbnail != null ||
        Embed?.Image != null ||
        (Embed?.Footer != null && (!string.IsNullOrWhiteSpace(Embed?.Footer.Text) || !string.IsNullOrWhiteSpace(Embed?.Footer.IconUrl))) ||
        Embed?.Fields is { Count: > 0 };

    public Discord.Embed[] ToEmbedArray(Embed[] embeds)
    {
        var toReturn = new List<Discord.Embed>();
        foreach (var i in embeds)
        {
            var embed = new EmbedBuilder();

            if (!string.IsNullOrWhiteSpace(i.Title))
                embed.WithTitle(i.Title);
            if (!string.IsNullOrWhiteSpace(i.Description))
                embed.WithDescription(i.Description);
            if (i.Url != null && Uri.IsWellFormedUriString(i.Url, UriKind.Absolute))
                embed.WithUrl(i.Url);
            embed.WithColor(new Color(i.Color));
            if (i.Footer != null)
            {
                embed.WithFooter(efb =>
                {
                    efb.WithText(i.Footer.Text);
                    if (Uri.IsWellFormedUriString(i.Footer.IconUrl, UriKind.Absolute))
                        efb.WithIconUrl(i.Footer.IconUrl);
                });
            }

            if (i.Thumbnail != null && Uri.IsWellFormedUriString(i.Thumbnail.Url, UriKind.Absolute))
                embed.WithThumbnailUrl(i.Thumbnail.Url);
            if (i.Image != null && Uri.IsWellFormedUriString(i.Image.Url, UriKind.Absolute))
                embed.WithImageUrl(i.Image.Url);
            if (i.Author != null && !string.IsNullOrWhiteSpace(i.Author.Name))
            {
                if (!Uri.IsWellFormedUriString(i.Author.IconUrl, UriKind.Absolute))
                    i.Author.IconUrl = null;
                if (!Uri.IsWellFormedUriString(i.Author.Url, UriKind.Absolute))
                    i.Author.Url = null;

                embed.WithAuthor(i.Author.Name, i.Author.IconUrl, i.Author.Url);
            }

            if (i.Fields != null)
            {
                foreach (var f in i.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Name) && !string.IsNullOrWhiteSpace(f.Value)))
                    embed.AddField(efb => efb.WithName(f.Name).WithValue(f.Value).WithIsInline(f.Inline));
            }
            else
                toReturn.Add(embed.Build());
        }

        return toReturn.ToArray();
    }
}