using System.Text.Json.Serialization;
using Mewdeko.Common.JsonConverters;
using static Mewdeko.Extensions.StringExtensions;

// ReSharper disable NotNullOrRequiredMemberIsNotInitialized

namespace Mewdeko.Common;

/// <summary>
///     Represents an author of an embed.
/// </summary>
public class Author
{
    /// <summary>
    ///     Gets or sets the name of the author.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    ///     Gets or sets the URL associated with the author.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    ///     Gets or sets the icon URL associated with the author.
    /// </summary>
    [JsonPropertyName("icon_url")]
    public string IconUrl { get; set; }
}

/// <summary>
///     Represents a thumbnail image for an embed.
/// </summary>
public class Thumbnail
{
    /// <summary>
    ///     Gets or sets the URL of the thumbnail image.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; }
}

/// <summary>
///     Represents an image for an embed.
/// </summary>
public class Image
{
    /// <summary>
    ///     Gets or sets the URL of the image.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; }
}

/// <summary>
///     Represents the footer of an embed.
/// </summary>
public class Footer
{
    /// <summary>
    ///     Gets or sets the text of the footer.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; }

    /// <summary>
    ///     Gets or sets the icon URL of the footer.
    /// </summary>
    [JsonPropertyName("icon_url")]
    public string IconUrl { get; set; }
}

/// <summary>
///     Represents a field in an embed.
/// </summary>
public class Field
{
    /// <summary>
    ///     Gets or sets the name of the field.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    ///     Gets or sets the value of the field.
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; set; }

    /// <summary>
    ///     Gets or sets whether the field is displayed inline.
    /// </summary>
    [JsonPropertyName("inline")]
    public bool Inline { get; set; }
}

/// <summary>
///     Represents an embed message.
/// </summary>
public class Embed
{
    /// <summary>
    ///     Gets or sets the title of the embed.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    ///     Gets or sets the description of the embed.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    ///     Gets or sets the color of the embed.
    /// </summary>
    [JsonPropertyName("color")]
    [JsonConverter(typeof(DiscordColorConverter))]
    public Color? Color { get; set; }

    /// <summary>
    ///     Gets or sets the timestamp of the embed.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }

    /// <summary>
    ///     Gets or sets the URL of the embed.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    ///     Gets or sets the author of the embed.
    /// </summary>
    [JsonPropertyName("author")]
    public Author? Author { get; set; }

    /// <summary>
    ///     Gets or sets the thumbnail image of the embed.
    /// </summary>
    [JsonPropertyName("thumbnail")]
    public Thumbnail? Thumbnail { get; set; }

    /// <summary>
    ///     Gets or sets the image of the embed.
    /// </summary>
    [JsonPropertyName("image")]
    public Image? Image { get; set; }

    /// <summary>
    ///     Gets or sets the footer of the embed.
    /// </summary>
    [JsonPropertyName("footer")]
    public Footer? Footer { get; set; }

    /// <summary>
    ///     Gets or sets the fields of the embed.
    /// </summary>
    [JsonPropertyName("fields")]
    public List<Field>? Fields { get; set; }
}

/// <summary>
///     Represents a new embed message.
/// </summary>
public class NewEmbed
{
    /// <summary>
    ///     Gets or sets the content of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    ///     Gets or sets the embed of the message.
    /// </summary>
    [JsonPropertyName("embed")]
    public Embed? Embed { get; set; }

    /// <summary>
    ///     Gets or sets the list of embeds of the message.
    /// </summary>
    [JsonPropertyName("embeds")]
    public List<Embed>? Embeds { get; set; }

    /// <summary>
    ///     Gets or sets the list of components of the message.
    /// </summary>
    [JsonPropertyName("components")]
    public List<NewEmbedComponent>? Components { get; set; }

    /// <summary>
    ///     Gets a value indicating whether the message is valid.
    /// </summary>
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

    /// <summary>
    ///     Gets a value indicating whether the embed is valid.
    /// </summary>
    public bool IsEmbedValid
    {
        get
        {
            return !string.IsNullOrWhiteSpace(Embed?.Description) ||
                   !string.IsNullOrWhiteSpace(Embed?.Url) ||
                   Embed?.Thumbnail != null ||
                   Embed?.Image != null ||
                   Embed?.Footer != null && (!string.IsNullOrWhiteSpace(Embed?.Footer.Text) ||
                                             !string.IsNullOrWhiteSpace(Embed?.Footer.IconUrl)) ||
                   Embed?.Fields is { Count: > 0 };
        }
    }

    /// <summary>
    ///     Gets the components of the message.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A <see cref="ComponentBuilder" /> containing the components.</returns>
    public ComponentBuilder? GetComponents(ulong? guildId)
    {
        if (Components is null) return null;

        var cb = new ComponentBuilder();

        // Group components by row and add them in order
        var groupedByRow = Components
            .GroupBy(c => c.Row)
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var rowGroup in groupedByRow)
        {
            var rowId = rowGroup.Key;
            var componentsInRow = rowGroup.ToList();

            // Add all components in this row
            foreach (var comp in componentsInRow)
            {
                if (comp.IsSelect)
                {
                    cb.WithSelectMenu(GetSelectMenu(comp, rowId, guildId ?? 0), rowId);
                }
                else
                {
                    cb.WithButton(GetButton(comp, rowId, guildId ?? 0), rowId);
                }
            }
        }

        return cb;
    }

    private static ButtonBuilder GetButton(NewEmbedComponent btn, int pos, ulong guildId)
    {
        var bb = new ButtonBuilder();
        if (btn.Url.IsNullOrWhiteSpace() && btn.Id.IsNullOrWhiteSpace())
            bb.WithDisabled(true).WithLabel("Buttons must have a url or id").WithStyle(ButtonStyle.Danger)
                .WithCustomId(pos.ToString());
        else if (!btn.Url.IsNullOrWhiteSpace() && !btn.Id.IsNullOrWhiteSpace())
            bb.WithDisabled(true).WithLabel("Buttons cannot have both a url and id").WithStyle(ButtonStyle.Danger)
                .WithCustomId(pos.ToString());
        else if (btn.Url.IsNullOrWhiteSpace() && btn.Style == ButtonStyle.Link)
            bb.WithDisabled(true).WithLabel("Button styles must be 1, 2, 3, or 4").WithStyle(ButtonStyle.Danger)
                .WithCustomId(pos.ToString());
        else if (btn.DisplayName.IsNullOrWhiteSpace())
            bb.WithDisabled(true).WithLabel("Buttons must have a display name").WithStyle(ButtonStyle.Danger)
                .WithCustomId(pos.ToString());
        else if (!btn.Url.IsNullOrWhiteSpace() && !btn.Url.StartsWith("https://") &&
                 !btn.Url.StartsWith("discord://"))
            bb.WithDisabled(true).WithLabel("Buttons with a url must have a https:// or discord:// link")
                .WithStyle(ButtonStyle.Danger).WithCustomId(pos.ToString());
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
            bb.WithLabel(btn.DisplayName).WithStyle(btn.Style)
                .WithCustomId($"trigger.{btn.Id}.runin.{guildId}${pos}");
            if (btn.Emoji is not null)
            {
                bb.WithEmote(btn.Emoji.ToIEmote());
            }
        }

        return bb;
    }

    private SelectMenuBuilder GetSelectMenu(NewEmbedComponent sel, int pos, ulong guildId)
    {
        var sb = new SelectMenuBuilder();

        var error = new SelectMenuBuilder()
            .WithDisabled(true)
            .WithOptions([new SelectMenuOptionBuilder("a", "a")])
            .WithCustomId(pos.ToString());

        if ((sel.MaxOptions, sel.MinOptions) is (> 25 or < 0, > 25 or < 0))
            sb = error.WithPlaceholder("MinOptions and MaxOptions must be less than 25 and more than 0");
        else if (sel.MaxOptions < sel.MinOptions)
            sb = error.WithPlaceholder("MinOptions must be larger than or equal to MaxOptions");
        else if (sel.MaxOptions > (sel.Options?.Count ?? 0))
            sb = error.WithPlaceholder("MaxOptions cannot be greater than total options");
        else if ((sel.Options?.Count ?? 0) == 0)
            sb = error.WithPlaceholder("Options must not be empty");
        else if (sel.Options.Count > 25)
            sb = error.WithPlaceholder("More than 25 options cannot be specified");
        else if (sel.DisplayName?.Length > 80)
            sb = error.WithPlaceholder("displayName.length cannot be greater than 80");
        else if (sel.Options.Any(x => x.Name.Length > 100))
            sb = error.WithPlaceholder("select option names length cannot be greater than 100");
        else if (sel.Options.Any(x => x.Description?.Length > 100))
            sb = error.WithPlaceholder("select option description length cannot be greater than 100");
        else
            sb
                .WithPlaceholder(sel.DisplayName)
                .WithCustomId($"multitrigger.runin.{guildId}${pos}")
                .WithMaxValues(sel.MaxOptions)
                .WithMinValues(sel.MinOptions)
                .WithOptions(sel.Options
                    .Select(x =>
                        new SelectMenuOptionBuilder(x.Name, $"option.{x.Id}.{GenerateSecureString(10)}",
                            x.Description ?? "None",
                            x.Emoji?.ToIEmote()))
                    .ToList());

        return sb;
    }

    /// <summary>
    ///     Converts a collection of <see cref="Embed" /> objects to a collection of Discord.NET <see cref="Embed" /> objects.
    /// </summary>
    /// <param name="embeds">The collection of <see cref="Embed" /> objects to convert.</param>
    /// <returns>An array of <see cref="Discord.Embed" /> objects.</returns>
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
            if (i.Color is not null)
                embed.WithColor(i.Color.Value);
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
                foreach (var f in i.Fields.Where(f =>
                             !string.IsNullOrWhiteSpace(f.Name) && !string.IsNullOrWhiteSpace(f.Value)))
                    embed.AddField(efb => efb.WithName(f.Name).WithValue(f.Value).WithIsInline(f.Inline));
            }

            toReturn.Add(embed.Build());
        }

        return toReturn.ToArray();
    }

    /// <summary>
    ///     Represents a component in a new embed message.
    /// </summary>
    public class NewEmbedComponent
    {
        /// <summary>
        ///     Gets or sets the row number (0-4) for this component.
        /// </summary>
        [JsonPropertyName("row")]
        public int Row { get; set; } = 0;

        /// <summary>
        ///     Gets or sets the display name of the component.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        ///     Gets or sets the ID of the component.
        /// </summary>
        public string Id { get; set; } = null!;

        /// <summary>
        ///     Gets or sets the style of the component.
        /// </summary>
        public ButtonStyle Style { get; set; } = ButtonStyle.Primary;

        /// <summary>
        ///     Gets or sets the URL of the component.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        ///     Gets or sets the emoji of the component.
        /// </summary>
        public string? Emoji { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the component is a select menu.
        /// </summary>
        public bool IsSelect { get; set; } = false;

        /// <summary>
        ///     Gets or sets the maximum number of options in the select menu.
        /// </summary>
        public int MaxOptions { get; set; } = 1;

        /// <summary>
        ///     Gets or sets the minimum number of options in the select menu.
        /// </summary>
        public int MinOptions { get; set; } = 1;

        /// <summary>
        ///     Gets or sets the list of options for the select menu.
        /// </summary>
        public List<NewEmbedSelectOption>? Options { get; set; }
    }

    /// <summary>
    ///     Represents an option in a select menu of a new embed message.
    /// </summary>
    public class NewEmbedSelectOption
    {
        /// <summary>
        ///     Gets or sets the ID of the option.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        ///     Gets or sets the name of the option.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Gets or sets the emoji of the option.
        /// </summary>
        public string? Emoji { get; set; }

        /// <summary>
        ///     Gets or sets the description of the option.
        /// </summary>
        public string? Description { get; set; }
    }
}