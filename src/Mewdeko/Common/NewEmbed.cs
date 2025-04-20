using System.ComponentModel;
using System.Text.Json.Serialization;
using FParsec;
using Mewdeko.Common.JsonConverters;
using Mewdeko.Database.Migrations.SQLite;
using Microsoft.IdentityModel.Abstractions;
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
    ///     used for comp v2, should be the only prop or not be set.
    /// </summary>
    [JsonPropertyName("containers")]
    public List<NewEmbedContainer> Containers {get;set;}

    /// <summary>
    ///     Gets a value indicating whether the message is valid.
    /// </summary>
    public bool IsValid
    {
        get
        {
            // if any containers there must be no other content, otherwise there must be at least one content.
            if (Containers != null && Components == null && Embed == null && Embeds == null && Content == null)
                return true;
            if (Containers != null)
                return false;
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
/// get components for the guy
/// </summary>
/// <param name="guildId">the guy</param>
/// <returns>take a gander</returns>
    public ComponentBuilder? GetComponents(ulong? guildId) => GetComponents(guildId, Components);

    /// <summary>
    ///     Gets the components of the message.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="components">Components to be used .</param>
    /// <param name="posOffset">for deidentifying </param>
    /// <returns>A <see cref="ComponentBuilder" /> containing the components.</returns>
    public static ComponentBuilder? GetComponents(ulong? guildId, List<NewEmbedComponent> components, int posOffset = 0)
    {
        var cb = new ComponentBuilder();

        var activeRowId = posOffset;
        var rowLength = 0;
        if (components is null) return null;
        foreach (var comp in components)
        {

            if (comp.IsSelect)
            {

                if (rowLength != 0)
                {
                    ++activeRowId;
                    rowLength = 0;
                }

                cb.WithSelectMenu(GetSelectMenu(comp, activeRowId, guildId ?? 0));

                ++activeRowId;
            }
            else
            {

                if (rowLength != 0)
                {
                    ++activeRowId;
                    rowLength = 0;
                }


                cb.WithButton(GetButton(comp, activeRowId, guildId ?? 0));
                ++rowLength;
            }
        }

        return cb;
    }
/// <summary>
/// gets a button for the specified component
/// </summary>
/// <param name="btn">the component</param>
/// <param name="pos">offset for unqueification</param>
/// <param name="guildId">guildid for triggers</param>
/// <returns></returns>
    public static ButtonBuilder GetButton(NewEmbedComponent btn, int pos, ulong? guildId)
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
/// <summary>
/// gets a select for the specified component
/// </summary>
/// <param name="sel">the component</param>
/// <param name="pos">offset for unqueification</param>
/// <param name="guildId">guildid for triggers</param>
/// <returns></returns>
    public static SelectMenuBuilder GetSelectMenu(NewEmbedComponent sel, int pos, ulong guildId)
    {
        var sb = new SelectMenuBuilder();

        var error = new SelectMenuBuilder()
            .WithDisabled(true)
            .WithOptions([new SelectMenuOptionBuilder("a", "a")])
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
                        new SelectMenuOptionBuilder(x.Name, $"option.{x.Id}.{GenerateSecureString(10)}", x.Description ?? "None",
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
    /// <summary>
    ///     The containers (embed looking things)
    /// </summary>
    public class NewEmbedContainer
    {
        /// <summary>
        ///     matches background if null, otherwise thats the color on the side
        /// </summary>
        
        [JsonConverter(typeof(DiscordColorConverter))]
        public Color? Color {get;set;} = null;
        /// <summary>
        ///     only seems to effect images in galleries and the like.
        /// </summary>
        public bool IsSpoiler {get;set;} = false;
        /// <summary>
        ///     can contain a mix of up to 10 items. have fun.
        /// </summary>
        public List<NewEmbedContainerItem> Items {get;set;}
        
        /// <summary>
        ///     love this guy
        /// </summary>
        /// <param name="guildId">used for trigger buttons</param>
        /// <param name="pos">used for button diferentiation, should be incrimented for sequentail parsing</param>
        /// <returns>a <see cref="ContainerBuilder" /> that has all of Items in it.</returns>
        public ContainerBuilder GetBuilder(ulong? guildId, int pos)
        {
            var builder = new ContainerBuilder()
                .WithAccentColor(Color)
                .WithSpoiler(IsSpoiler);
            Items.ForEach(x => builder.AddComponents(x.GetComponents(pos++, guildId)));
            return builder;
        }
    }

    /// <summary>
    /// Items for a <see cref="NewEmbedContainer" /> different props are needed for different types.
    /// </summary>
    public class NewEmbedContainerItem 
    {
        /// <summary>
        ///     The tipe of item, the props you need depend on this
        /// </summary>
        public NewEmbedContainerItemType Type {get;set;}
        /// <summary>
        ///     The size of the space around a seperator, defaults to small
        /// </summary>
        public SeparatorSpacingSize SeperatorSize {get;set;} = SeparatorSpacingSize.Small;
        /// <summary>
        ///     Hides the seperator, for some reason. no idea why you would want this
        /// </summary>
        public bool SeperatorInvisable {get;set;} = false;

        /// <summary>
        ///  Up to 4096 characters of your own composition. pretty boring. supports full markup (so # TItle, lists, everyting) 
        /// </summary>
        public string TextContent {get;set;} = null;
        // component

        /// <summary>
        ///     Sub-components for the components type, supports buttons and selects with auto-rowing like normal  
        /// </summary>
        public List<NewEmbedComponent> Components {get;set;} = null;
        // section

        /// <summary>
        ///     Text with a thumb or button on the right. 
        /// </summary>
        public NewEmbedSection Section {get;set;} = null;
        // gallery

        /// <summary>
        ///     up to ten urls, can link to anywhere. 
        /// </summary>
        public List<string> GalleryImageURLs {get;set;} = null;
        /// <summary>
        ///     sets the IsSpoiler property on every iamge in the gallery to this value. defautls to false.
        /// </summary>
        public bool GalleryIsSpoiler {get;set;} = false;
        
        /// <summary>
        ///     Parses the components, returns an arrays to support multiple action rows cleanly 
        /// </summary>
        /// <param name="pos">Needed for component differentiation</param>
        /// <param name="guildId">Needed for trigger buttons</param>
        /// <returns>An array of generic component builders represnting </returns>
        public IMessageComponentBuilder[] GetComponents(int pos, ulong? guildId) 
        {
            if (Type == NewEmbedContainerItemType.Text)
                return (TextContent?.Length >= 4096 || TextContent is null)
                    ? [new TextDisplayBuilder("TextContent must be between 0 and 4096 chars.")]
                    : [new TextDisplayBuilder(TextContent)];
            else if (Type == NewEmbedContainerItemType.Seperator)
                return [new SeparatorBuilder().WithIsDivider(!SeperatorInvisable).WithSpacing(SeperatorSize)];
            else if (Type == NewEmbedContainerItemType.Components)
                return NewEmbed.GetComponents(guildId, Components).ActionRows.Select(x => x as IMessageComponentBuilder).ToArray();
            else if (Type == NewEmbedContainerItemType.Section)
                return [Section.GetBuilder(pos, guildId)];
            else if (Type == NewEmbedContainerItemType.Gallery)
                return (GalleryImageURLs is not null && GalleryImageURLs.Count <= 10 && GalleryImageURLs.Count >= 1)
                    ? [new MediaGalleryBuilder().WithItems(GalleryImageURLs.Select(x => new MediaGalleryItemProperties(new(x), null, GalleryIsSpoiler)))]
                    : [new TextDisplayBuilder("`GalleryImageURLs` must have between 1 and 10 entries")];
            else
                return [new TextDisplayBuilder("unknown type")];
        }
    }

    /// <summary>
    ///     A section with tect on the left and an accessory (button or iamge) on the right.
    /// </summary>
    public class NewEmbedSection 
    {
        /// <summary>
        ///     up to 4096 chars of text
        /// </summary>
        public string Text {get;set;}
        /// <summary>
        ///     a button, must be null if <see cref="ImageUrl" /> is specified
        /// </summary>
        public NewEmbedComponent Button {get;set;} = null;
        /// <summary>
        ///     a link to an image, must be null if <see cref="Button" /> is specified
        /// </summary>
        public string ImageUrl {get;set;} = null;
        /// <summary>
        ///     true if the image should be spoilered. defaults to false
        /// </summary>
        public bool ImageIsSpoiler {get;set;} = false;
        /// <summary>
        ///     gets a builder representing the section
        /// </summary>
        /// <param name="pos">position offset for un-matching components</param>
        /// <param name="guildId">the guildid for triggers</param>
        /// <returns>a builder repping. the section, or an error message</returns>
        public SectionBuilder GetBuilder(int pos, ulong? guildId) 
        {
            if (Button != null && ImageUrl != null)
                return new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder("A section can only have a ImageUrl or a Component, not both"))
                    .WithAccessory(new ButtonBuilder("error", $"{pos}", ButtonStyle.Danger, isDisabled: true));
            if (Button == null && ImageUrl == null)
                return new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder("A section must have either a Component or an ImageUrl"))
                    .WithAccessory(new ButtonBuilder("Error", $"{pos}", ButtonStyle.Danger, isDisabled: true));
            if (Text.Length >= 4096)
                return new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder("Text length must be less than 4096"))
                    .WithAccessory(new ButtonBuilder("Error", $"{pos}", ButtonStyle.Danger, isDisabled: true));
            var builder = new SectionBuilder()
                .AddComponent(new TextDisplayBuilder(Text));
            if (ImageUrl != null)
                return builder.WithAccessory(new ThumbnailBuilder(new() {Url=ImageUrl}, isSpoiler: ImageIsSpoiler));
            return builder.WithAccessory(GetButton(Button, pos, guildId));
        }
    }

/// <summary>
///     A type of item
/// </summary>
    public enum NewEmbedContainerItemType 
    {
        /// <summary>
        /// text
        /// </summary>
        Text,
        /// <summary>
        /// sueperator
        /// </summary>
        Seperator,
        /// <summary>
        /// list of buttons or selects
        /// </summary>
        Components,
        /// <summary>
        /// text with a button or image on the right
        /// </summary>
        Section,
        /// <summary>
        /// 1 to 10 images
        /// </summary>
        Gallery
    }
}