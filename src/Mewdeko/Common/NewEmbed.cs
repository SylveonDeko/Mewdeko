using Newtonsoft.Json;

namespace Mewdeko.Common;

public class Author
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("icon_url")]
    public string IconUrl { get; set; }
}

public class Thumbnail
{
    [JsonProperty("url")]
    public string Url { get; set; }
}

public class Image
{
    [JsonProperty("url")]
    public string Url { get; set; }
}

public class Footer
{
    [JsonProperty("text")]
    public string Text { get; set; }

    [JsonProperty("icon_url")]
    public string IconUrl { get; set; }
}

public class Field
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("value")]
    public string Value { get; set; }

    [JsonProperty("inline")]
    public bool Inline { get; set; }
}

public class Embed
{
    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("color")]
    public string Color { get; set; }

    [JsonProperty("timestamp")]
    public DateTime? Timestamp { get; set; }

    [JsonProperty("url")]
    public string? Url { get; set; }

    [JsonProperty("author")]
    public Author? Author { get; set; }

    [JsonProperty("thumbnail")]
    public Thumbnail? Thumbnail { get; set; }

    [JsonProperty("image")]
    public Image? Image { get; set; }

    [JsonProperty("footer")]
    public Footer? Footer { get; set; }

    [JsonProperty("fields")]
    public List<Field>? Fields { get; set; }
}

public class NewEmbed
{
    [JsonProperty("content")]
    public string? Content { get; set; }

    [JsonProperty("embed")]
    public Embed? Embed { get; set; }

    [JsonProperty("embeds")]
    public Embed[]? Embeds { get; set; }

    [JsonProperty("components")]
    public NewEmbedComponent[]? Components { get; set; }

    public bool IsValid
    {
        get
        {
            if (Content != null)
                return true;
            if (Embed != null)
                return true;
            if (Embeds != null)
                return true;
            return Components != null;
        }
    }

    public bool IsEmbedValid =>
        !string.IsNullOrWhiteSpace(Embed?.Description) ||
        !string.IsNullOrWhiteSpace(Embed?.Url) ||
        Embed?.Thumbnail != null ||
        Embed?.Image != null ||
        (Embed?.Footer != null && (!string.IsNullOrWhiteSpace(Embed?.Footer.Text) || !string.IsNullOrWhiteSpace(Embed?.Footer.IconUrl))) ||
        Embed?.Fields is { Count: > 0 };

    public class NewEmbedComponent
    {
        public string? DisplayName { get; set; }
        public int Id { get; set; } = 0;
        public ButtonStyle Style { get; set; } = ButtonStyle.Primary;
        public string? Url { get; set; }
        public string? Emoji { get; set; }

        public bool IsSelect { get; set; } = false;
        public int MaxOptions { get; set; } = 1;
        public int MinOptions { get; set; } = 1;
        public List<NewEmbedSelectOption>? Options { get; set; }
    }

    public class NewEmbedSelectOption
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Emoji { get; set; }
        public string Description { get; set; }
    }


    public ComponentBuilder GetComponents(ulong? guildId)
    {
        var cb = new ComponentBuilder();

        var activeRowId = 0;
        var rowLength = 0;
        if (Components is null) return null;
        foreach (var comp in Components)
        {
            if (activeRowId == 5)
                break;

            if (rowLength == 5)
            {
                ++activeRowId;
                rowLength = 0;
            }

            if (comp.IsSelect is true)
            {
                if (rowLength != 0)
                    ++activeRowId;
                rowLength = 0;
                if (activeRowId == 5)
                    break;
                cb.WithSelectMenu(GetSelectMenu(comp, (activeRowId * 10 + rowLength), guildId ?? 0));
            }
            else
            {
                ++rowLength;
                cb.WithButton(GetButton(comp, (activeRowId * 10 + rowLength), guildId ?? 0));
            }
        }

        return cb;
    }


    public static ButtonBuilder GetButton(NewEmbedComponent btn, int pos, ulong guildId)
    {
        var bb = new ButtonBuilder();
        if (btn.Url.IsNullOrWhiteSpace() && btn.Id == 0)
            bb.WithDisabled(true).WithLabel("Buttons must have a url or id").WithStyle(ButtonStyle.Danger).WithCustomId(pos.ToString());
        else if (!btn.Url.IsNullOrWhiteSpace() && btn.Id != 0)
            bb.WithDisabled(true).WithLabel("Buttons cannot have both a url and id").WithStyle(ButtonStyle.Danger).WithCustomId(pos.ToString());
        else if (btn.Url.IsNullOrWhiteSpace() && btn.Style == ButtonStyle.Link)
            bb.WithDisabled(true).WithLabel("Button styles must be 1, 2, 3, or 4").WithStyle(ButtonStyle.Danger).WithCustomId(pos.ToString());
        else if (btn.DisplayName.IsNullOrWhiteSpace())
            bb.WithDisabled(true).WithLabel("Buttons must have a display name").WithStyle(ButtonStyle.Danger).WithCustomId(pos.ToString());
        else if (!btn.Url.IsNullOrWhiteSpace() && !btn.Url.StartsWith("https://") && !btn.Url.StartsWith("http://") && !btn.Url.StartsWith("discord://"))
            bb.WithDisabled(true).WithLabel("Buttons with a url must have a https://, https://, or discord:// link").WithStyle(ButtonStyle.Danger).WithCustomId(pos.ToString());
        else if (!btn.Url.IsNullOrWhiteSpace())
        {
            bb.WithLabel(btn.DisplayName).WithStyle(ButtonStyle.Link).WithUrl(btn.Url);
            if (btn.Emoji is not null)
            {
                bb.WithEmote(btn.Emoji.ToIEmote());
            }
        }
        else
        {
            bb.WithLabel(btn.DisplayName).WithStyle(btn.Style).WithCustomId($"trigger.{btn.Id}.runin.{guildId}${pos}");
            if (btn.Emoji is not null)
            {
                bb.WithEmote(btn.Emoji.ToIEmote());
            }
        }

        return bb;
    }

    public static SelectMenuBuilder GetSelectMenu(NewEmbedComponent sel, int pos, ulong guildId)
    {
        var sb = new SelectMenuBuilder();

        var error = new SelectMenuBuilder()
            .WithDisabled(true)
            .WithOptions(new()
            {
                new("a", "a")
            })
            .WithCustomId(pos.ToString());

        if ((sel.MaxOptions, sel.MinOptions) is ((> 25) or (< 0), (> 25) or (< 0)))
            sb = error.WithPlaceholder("MinOptions and MaxOptions must be less than 25 and more than 0");
        else if (sel.MaxOptions < sel.MinOptions)
            sb = error.WithPlaceholder("MinOptions must be larger than or equal to MaxOptions");
        else if (sel.MaxOptions > (sel.Options?.Count ?? 0))
            sb = error.WithPlaceholder("MaxOptions cannot be greater than total options");
        else if ((sel.Options?.Count ?? 0) == 0)
            sb = error.WithPlaceholder("Options must not be empty");
        else if (sel.Options.Count > 25)
            sb = error.WithPlaceholder("More than 25 options cannot be specified");
        else if (sel.DisplayName.Length > 80)
            sb = error.WithPlaceholder("displayName.length cannot be greater than 80");
        else if (sel.Options.Any(x => x.Name.Length > 100))
            sb = error.WithPlaceholder("select option names length cannot be greater than 100");
        else if (sel.Options.Any(x => x.Description.Length > 100))
            sb = error.WithPlaceholder("select option description length cannot be greater than 100");
        else
            sb
                .WithPlaceholder(sel.DisplayName)
                .WithCustomId($"multitrigger.runin.{guildId}${pos}")
                .WithMaxValues(sel.MaxOptions)
                .WithMinValues(sel.MinOptions)
                .WithOptions(sel.Options
                    .Select(x => new SelectMenuOptionBuilder(x.Name, x.Id.ToString(), x.Description, x.Emoji?.ToIEmote()))
                    .ToList());

        return sb;
    }

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
            if (i.Url != null && Uri.IsWellFormedUriString(i.Url, UriKind.Absolute))
                embed.WithUrl(i.Url);
            if (i.Color.StartsWith("#"))
                embed.WithColor(new Color(Convert.ToUInt32(i.Color.Replace("#", ""), 16)));
            if (i.Color.StartsWith("0x") && i.Color.Length == 8)
                embed.WithColor(new Color(Convert.ToUInt32(i.Color.Replace("0x", ""), 16)));
            if (uint.TryParse(i.Color, out var colorNumber))
                embed.WithColor(new Color(colorNumber));
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

            toReturn.Add(embed.Build());
        }

        return toReturn.ToArray();
    }
}