using Newtonsoft.Json;

namespace Mewdeko.Common;

public class CrEmbed
{
    public CrEmbedAuthor? Author { get; set; }
    public string? PlainText { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public CrEmbedFooter? Footer { get; set; }
    public string? Thumbnail { get; set; }
    public string? Image { get; set; }
    public CrEmbedField[]? Fields { get; set; }
    public uint Color { get; set; } = 7458112;
    public CrEmbedComponent[]? Components { get; set; }

    public bool IsValid =>
        IsEmbedValid || !string.IsNullOrWhiteSpace(PlainText);

    public bool IsEmbedValid =>
        !string.IsNullOrWhiteSpace(Title) ||
        !string.IsNullOrWhiteSpace(Description) ||
        !string.IsNullOrWhiteSpace(Url) ||
        !string.IsNullOrWhiteSpace(Thumbnail) ||
        !string.IsNullOrWhiteSpace(Image) ||
        (Footer is not null &&
         (!string.IsNullOrWhiteSpace(Footer.Text) || !string.IsNullOrWhiteSpace(Footer.IconUrl))) ||
        Fields is { Length: > 0 };

    public EmbedBuilder ToEmbed()
    {
        var embed = new EmbedBuilder();

        if (!string.IsNullOrWhiteSpace(Title))
            embed.WithTitle(Title);
        if (!string.IsNullOrWhiteSpace(Description))
            embed.WithDescription(Description);
        if (Url != null && Uri.IsWellFormedUriString(Url, UriKind.Absolute))
            embed.WithUrl(Url);
        embed.WithColor(new Color(Color));
        if (Footer != null)
        {
            embed.WithFooter(efb =>
            {
                efb.WithText(Footer.Text);
                if (Uri.IsWellFormedUriString(Footer.IconUrl, UriKind.Absolute))
                    efb.WithIconUrl(Footer.IconUrl);
            });
        }

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

        if (Fields == null)
            return embed;
        {
            foreach (var f in Fields)
            {
                if (!string.IsNullOrWhiteSpace(f.Name) && !string.IsNullOrWhiteSpace(f.Value))
                    embed.AddField(efb => efb.WithName(f.Name).WithValue(f.Value).WithIsInline(f.Inline));
            }
        }

        return embed;
    }

    public ComponentBuilder GetComponents(ulong? guildId)
    {
        var cb = new ComponentBuilder();

        var activeRowId = 0;
        var rowLength = 0;
        if (Components is null)
            return cb;
        foreach (var comp in Components)
        {
            if (activeRowId == 5)
                break;

            if (rowLength == 5)
            {
                ++activeRowId;
                rowLength = 0;
            }

            if (comp.IsSelect)
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

    public static ButtonBuilder GetButton(CrEmbedComponent btn, int pos, ulong guildId)
    {
        var bb = new ButtonBuilder();
        if (btn.Url.IsNullOrWhiteSpace() && btn.Id is not null)
            bb.WithDisabled(true).WithLabel("Buttons must have a url or id").WithStyle(ButtonStyle.Danger)
                .WithCustomId(pos.ToString());
        else if (!btn.Url.IsNullOrWhiteSpace() && btn.Id is not null)
            bb.WithDisabled(true).WithLabel("Buttons cannot have both a url and id").WithStyle(ButtonStyle.Danger)
                .WithCustomId(pos.ToString());
        else if (btn.Url.IsNullOrWhiteSpace() && btn.Style == ButtonStyle.Link)
            bb.WithDisabled(true).WithLabel("Button styles must be 1, 2, 3, or 4").WithStyle(ButtonStyle.Danger)
                .WithCustomId(pos.ToString());
        else if (btn.DisplayName.IsNullOrWhiteSpace())
            bb.WithDisabled(true).WithLabel("Buttons must have a display name").WithStyle(ButtonStyle.Danger)
                .WithCustomId(pos.ToString());
        else if (!btn.Url.IsNullOrWhiteSpace() && !btn.Url.StartsWith("https://") && !btn.Url.StartsWith("http://") &&
                 !btn.Url.StartsWith("discord://"))
            bb.WithDisabled(true).WithLabel("Buttons with a url must have a https://, https://, or discord:// link")
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
            bb.WithLabel(btn.DisplayName).WithStyle(btn.Style).WithCustomId($"trigger.{btn.Id}.runin.{guildId}${pos}");
            if (btn.Emoji is not null)
            {
                bb.WithEmote(btn.Emoji.ToIEmote());
            }
        }

        return bb;
    }

    public static SelectMenuBuilder GetSelectMenu(CrEmbedComponent sel, int pos, ulong guildId)
    {
        var sb = new SelectMenuBuilder();

        var error = new SelectMenuBuilder()
            .WithDisabled(true)
            .WithOptions(new()
            {
                new("a", "a")
            });

        if ((sel.MaxOptions, sel.MinOptions) is ((> 25) or (< 0), (> 25) or (< 0)))
            sb = error.WithPlaceholder("MinOptions and MaxOptions must be less than 25 and more than 0");
        else if (sel.MaxOptions < sel.MinOptions)
            sb = error.WithPlaceholder("MinOptions must be larger than or equal to MaxOptions");
        else if (sel.MaxOptions < (sel.Options?.Count ?? 0))
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
                    .Select(x =>
                        new SelectMenuOptionBuilder(x.Name, x.Id.ToString(), x.Description, x.Emoji?.ToIEmote()))
                    .ToList());

        return sb;
    }
}

public class CrEmbedField
{
    public string? Name { get; set; }
    public string? Value { get; set; }
    public bool Inline { get; set; }
}

public class CrEmbedFooter
{
    public string? Text { get; set; }
    public string? IconUrl { get; set; }

    [JsonProperty("icon_url")]
    private string IconUrl1
    {
        set => IconUrl = value;
    }
}

public class CrEmbedAuthor
{
    public string? Name { get; set; }
    public string? IconUrl { get; set; }

    [JsonProperty("icon_url")]
    private string? IconUrl1
    {
        set => IconUrl = value;
    }

    public string? Url { get; set; }
}

public class CrEmbedComponent
{
    public string? DisplayName { get; set; }

    public string Id { get; set; } = null;
    public ButtonStyle Style { get; set; } = ButtonStyle.Primary;
    public string? Url { get; set; }
    public string? Emoji { get; set; }

    public bool IsSelect { get; set; } = false;
    public int MaxOptions { get; set; } = 1;
    public int MinOptions { get; set; } = 1;
    public List<CrEmbedSelectOption>? Options { get; set; }
}

public class CrEmbedSelectOption
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Emoji { get; set; }
    public string Description { get; set; }
}