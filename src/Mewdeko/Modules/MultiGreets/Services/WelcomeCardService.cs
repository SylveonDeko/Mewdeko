using System.IO;
using System.Net.Http;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Cards;
using Mewdeko.Common.Palette;
using Mewdeko.Modules.Achievements.Common;
using Mewdeko.Modules.MultiGreets.Common;
using SkiaSharp;

namespace Mewdeko.Modules.MultiGreets.Services;

/// <summary>
///     Draws welcome cards for greet and bye messages from each server's card design, and keeps the designs and the
///     images they use.
/// </summary>
public class WelcomeCardService : INService
{
    /// <summary>
    ///     The name of the attached image file.
    /// </summary>
    public const string FileName = "welcome.png";

    /// <summary>
    ///     Largest image accepted for upload or download.
    /// </summary>
    public const int MaxImageBytes = 8 * 1024 * 1024;

    /// <summary>
    ///     Most images a server keeps.
    /// </summary>
    public const int MaxImages = 30;

    private const int ImageSize = 1600;
    private const int ThumbnailSize = 192;

    private readonly CardRenderer cards;
    private readonly CardPresetService presets;
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<WelcomeCardService> logger;
    private readonly GuildPaletteService palettes;
    private readonly SemaphoreSlim saveLock = new(1, 1);
    private readonly ConcurrentDictionary<(ulong, WelcomeCardTarget), WelcomeCardSetting> settings = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="WelcomeCardService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="cards">The card renderer.</param>
    /// <param name="presets">The shared background presets.</param>
    /// <param name="palettes">The server palettes.</param>
    /// <param name="logger">The logger instance.</param>
    public WelcomeCardService(IDataConnectionFactory dbFactory, DiscordShardedClient client,
        IHttpClientFactory httpClientFactory, CardRenderer cards, CardPresetService presets,
        GuildPaletteService palettes,
        ILogger<WelcomeCardService> logger)
    {
        this.dbFactory = dbFactory;
        this.client = client;
        this.httpClientFactory = httpClientFactory;
        this.cards = cards;
        this.presets = presets;
        this.palettes = palettes;
        this.logger = logger;
    }

    /// <summary>
    ///     Gets a guild's card settings, or the defaults when it has none.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Which message the card is for.</param>
    public async Task<WelcomeCardSetting> GetSettingsAsync(ulong guildId, WelcomeCardTarget target)
    {
        if (settings.TryGetValue((guildId, target), out var cached))
            return cached;

        await using var db = await dbFactory.CreateConnectionAsync();
        var row = await db.WelcomeCardSettings.FirstOrDefaultAsync(x => x.GuildId == guildId && x.Target == target)
                  ?? new WelcomeCardSetting
                  {
                      GuildId = guildId, Target = target, DateAdded = DateTime.UtcNow
                  };
        settings[(guildId, target)] = row;
        return row;
    }

    /// <summary>
    ///     Gets a card's design. A card saved before designs existed is drawn from its original settings, with its
    ///     background picture moved into the server's images the first time it is read.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Which message the card is for.</param>
    /// <returns>The design.</returns>
    public async Task<CardTemplate> GetTemplateAsync(ulong guildId, WelcomeCardTarget target)
    {
        var row = await GetSettingsAsync(guildId, target);
        if (!string.IsNullOrWhiteSpace(row.Template))
            return WelcomeCardTemplates.Parse(row.Template);
        if (row.Id == 0 || row.BackgroundData is not { Length: > 0 } legacyImage)
            return WelcomeCardTemplates.FromLegacy(row, null);

        await saveLock.WaitAsync();
        try
        {
            row = await GetSettingsAsync(guildId, target);
            if (!string.IsNullOrWhiteSpace(row.Template))
                return WelcomeCardTemplates.Parse(row.Template);

            var image = Normalize(legacyImage);
            if (image is null)
                return WelcomeCardTemplates.FromLegacy(row, null);

            await using var db = await dbFactory.CreateConnectionAsync();
            var id = await db.InsertWithInt32IdentityAsync(new WelcomeCardImage
            {
                GuildId = guildId, Data = image.Value.Image, Thumbnail = image.Value.Thumbnail,
                DateAdded = DateTime.UtcNow
            });
            var template = WelcomeCardTemplates.FromLegacy(row, id);
            var copy = Clone(row);
            copy.Template = template.Serialize();
            await db.UpdateAsync(copy);
            settings[(guildId, target)] = copy;
            return template;
        }
        finally
        {
            saveLock.Release();
        }
    }

    /// <summary>
    ///     Saves a card's design, its on and off state, or both.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Which message the card is for.</param>
    /// <param name="enabled">Whether the card is attached, or null to keep it.</param>
    /// <param name="template">The design, or null to keep it. It is brought within limits first.</param>
    /// <returns>The state and design as saved.</returns>
    public async Task<(bool Enabled, CardTemplate Template)> SaveAsync(ulong guildId, WelcomeCardTarget target,
        bool? enabled, CardTemplate? template)
    {
        var owned = template is null ? null : await OwnedAsync(guildId, template);

        await saveLock.WaitAsync();
        try
        {
            var copy = Clone(await GetSettingsAsync(guildId, target));
            if (enabled is { } on)
                copy.Enabled = on;
            if (owned is not null)
                copy.Template = owned.Serialize();

            await using var db = await dbFactory.CreateConnectionAsync();
            if (copy.Id == 0)
                copy.Id = await db.InsertWithInt32IdentityAsync(copy);
            else
                await db.UpdateAsync(copy);

            settings[(guildId, target)] = copy;
            return (copy.Enabled,
                owned ?? (string.IsNullOrWhiteSpace(copy.Template)
                    ? WelcomeCardTemplates.FromLegacy(copy, null)
                    : WelcomeCardTemplates.Parse(copy.Template)));
        }
        finally
        {
            saveLock.Release();
        }
    }

    /// <summary>
    ///     Changes a card's design and saves it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Which message the card is for.</param>
    /// <param name="change">Applies the change to the design and reports why it can't be made.</param>
    /// <returns>The error, or none.</returns>
    public async Task<WelcomeCardError> EditAsync(ulong guildId, WelcomeCardTarget target,
        Func<CardTemplate, WelcomeCardError> change)
    {
        var template = (await GetTemplateAsync(guildId, target)).Clone();
        var error = change(template);
        if (error != WelcomeCardError.None)
            return error;

        await SaveAsync(guildId, target, null, template);
        return WelcomeCardError.None;
    }

    /// <summary>
    ///     Makes the basic changes the commands offer.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Which message the card is for.</param>
    /// <param name="basics">The changes.</param>
    /// <returns>The error, or none.</returns>
    public Task<WelcomeCardError> ApplyAsync(ulong guildId, WelcomeCardTarget target, WelcomeCardBasics basics)
    {
        return EditAsync(guildId, target, template => WelcomeCardTemplates.Apply(template, basics));
    }

    /// <summary>
    ///     Turns a card on or off.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Which message the card is for.</param>
    /// <param name="enabled">Whether the card is attached.</param>
    public Task SetEnabledAsync(ulong guildId, WelcomeCardTarget target, bool enabled)
    {
        return SaveAsync(guildId, target, enabled, null);
    }

    /// <summary>
    ///     Replaces a card's design with a starter layout.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Which message the card is for.</param>
    /// <param name="starterId">The layout's short name.</param>
    /// <returns>The error, or none.</returns>
    public async Task<WelcomeCardError> ResetToStarterAsync(ulong guildId, WelcomeCardTarget target, string starterId)
    {
        var starter = WelcomeCardTemplates.Starters.FirstOrDefault(s =>
            string.Equals(s.Id, starterId, StringComparison.OrdinalIgnoreCase));
        if (starter is null)
            return WelcomeCardError.UnknownStarter;

        await SaveAsync(guildId, target, null, starter.Build());
        return WelcomeCardError.None;
    }

    /// <summary>
    ///     Brings a design within limits and drops links to images the server doesn't own, without saving it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="template">The design.</param>
    /// <returns>The corrected design.</returns>
    public async Task<CardTemplate> OwnedAsync(ulong guildId, CardTemplate template)
    {
        var card = WelcomeCardTemplates.Sanitize(template);
        var linked = CardRules.Uploads(card).Distinct().ToList();
        if (linked.Count == 0)
            return card;

        await using var db = await dbFactory.CreateConnectionAsync();
        var owned = (await db.WelcomeCardImages
            .Where(x => x.GuildId == guildId && linked.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync()).ToHashSet();

        bool Missing(string link)
        {
            return AchievementIcons.UploadId(AchievementIcons.Parse(link)) is { } id && !owned.Contains(id);
        }

        if (Missing(card.Background.Url))
            card.Background.Url = "";
        foreach (var element in card.Elements.Where(e => Missing(e.Url)))
            element.Url = "";
        return card;
    }

    /// <summary>
    ///     Stores an image for the server's designs, shrunk to fit 1600 pixels and saved as PNG.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="bytes">The image.</param>
    /// <returns>The error, or none, and the stored image.</returns>
    public async Task<(WelcomeCardError Error, WelcomeCardImage? Image)> UploadImageAsync(ulong guildId, byte[] bytes)
    {
        if (bytes.Length is 0 or > MaxImageBytes)
            return (WelcomeCardError.InvalidImage, null);

        var image = await Task.Run(() => Normalize(bytes));
        if (image is null)
            return (WelcomeCardError.InvalidImage, null);

        await using var db = await dbFactory.CreateConnectionAsync();
        if (await db.WelcomeCardImages.CountAsync(x => x.GuildId == guildId) >= MaxImages)
            return (WelcomeCardError.TooManyImages, null);

        var row = new WelcomeCardImage
        {
            GuildId = guildId, Data = image.Value.Image, Thumbnail = image.Value.Thumbnail,
            DateAdded = DateTime.UtcNow
        };
        row.Id = await db.InsertWithInt32IdentityAsync(row);
        return (WelcomeCardError.None, row);
    }

    /// <summary>
    ///     The server's stored images with their thumbnails, oldest first.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The images, without their full size data.</returns>
    public async Task<IReadOnlyList<WelcomeCardImage>> GetImagesAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.WelcomeCardImages
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.Id)
            .Select(x => new WelcomeCardImage
            {
                Id = x.Id, GuildId = x.GuildId, Thumbnail = x.Thumbnail, DateAdded = x.DateAdded
            })
            .ToListAsync();
    }

    /// <summary>
    ///     Deletes a stored image and removes it from every design of the server that uses it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="id">The image ID.</param>
    /// <returns>True when it existed.</returns>
    public async Task<bool> DeleteImageAsync(ulong guildId, int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        if (await db.WelcomeCardImages.Where(x => x.GuildId == guildId && x.Id == id).DeleteAsync() == 0)
            return false;

        var link = $"{AchievementIcons.UploadPrefix}{id}";
        await saveLock.WaitAsync();
        try
        {
            var rows = await db.WelcomeCardSettings.Where(x => x.GuildId == guildId && x.Template != null)
                .ToListAsync();
            foreach (var row in rows.Where(r => r.Template!.Contains(link)))
            {
                var template = WelcomeCardTemplates.Parse(row.Template);
                if (template.Background.Url == link)
                    template.Background.Url = "";
                foreach (var element in template.Elements.Where(e => e.Url == link))
                    element.Url = "";
                row.Template = template.Serialize();
                await db.UpdateAsync(row);
                settings[(guildId, row.Target)] = row;
            }
        }
        finally
        {
            saveLock.Release();
        }

        return true;
    }

    /// <summary>
    ///     Downloads an image from a link.
    /// </summary>
    /// <param name="url">The link.</param>
    /// <returns>The bytes, or null when the link is not an image that can be downloaded.</returns>
    public async Task<byte[]?> DownloadImageAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return null;

        try
        {
            using var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxImageBytes)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxImageBytes)
                    return null;
            }

            return buffer.ToArray();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Sets the background picture of a card's design. Null goes back to the plain colour.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Which message the card is for.</param>
    /// <param name="image">The image bytes, or null to remove the picture.</param>
    /// <returns>Whether the image could be used.</returns>
    public async Task<bool> SetBackgroundAsync(ulong guildId, WelcomeCardTarget target, byte[]? image)
    {
        if (image is null)
        {
            return await EditAsync(guildId, target, template =>
            {
                if (template.Background.Kind == "image")
                    template.Background.Kind = "solid";
                template.Background.Url = "";
                return WelcomeCardError.None;
            }) == WelcomeCardError.None;
        }

        var (error, stored) = await UploadImageAsync(guildId, image);
        if (error != WelcomeCardError.None || stored is null)
            return false;

        return await EditAsync(guildId, target, template =>
        {
            template.Background.Kind = "image";
            template.Background.Url = $"{AchievementIcons.UploadPrefix}{stored.Id}";
            template.Background.Fit = "cover";
            template.Background.Dim = 0;
            return WelcomeCardError.None;
        }) == WelcomeCardError.None;
    }

    /// <summary>
    ///     Sets the background of a card's design to a shared preset.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Which message the card is for.</param>
    /// <param name="reference">A <c>preset:id</c> reference.</param>
    /// <returns>Whether the preset exists.</returns>
    public async Task<bool> SetPresetBackgroundAsync(ulong guildId, WelcomeCardTarget target, string reference)
    {
        if (!await presets.ExistsAsync(reference) || !CardPresets.TryParse(reference, out var id))
            return false;

        return await EditAsync(guildId, target, template =>
        {
            template.Background.Kind = "image";
            template.Background.Url = CardPresets.Reference(id);
            template.Background.Fit = "cover";
            template.Background.Dim = 0;
            return WelcomeCardError.None;
        }) == WelcomeCardError.None;
    }

    /// <summary>
    ///     Draws the card for a member if cards are on for the message.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Which message the card is for.</param>
    /// <param name="user">The member.</param>
    /// <returns>The PNG, or null when cards are off or the card could not be drawn.</returns>
    public async Task<byte[]?> TryRenderAsync(ulong guildId, WelcomeCardTarget target, IUser user)
    {
        var setting = await GetSettingsAsync(guildId, target);
        if (!setting.Enabled)
            return null;

        try
        {
            return await RenderAsync(guildId, target, user);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not draw the welcome card for {UserId} in {GuildId}", user.Id, guildId);
            return null;
        }
    }

    /// <summary>
    ///     Draws a card for a member with the design saved for the message.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="target">Which message the card is for.</param>
    /// <param name="user">The member.</param>
    /// <returns>The PNG.</returns>
    public async Task<byte[]> RenderAsync(ulong guildId, WelcomeCardTarget target, IUser user)
    {
        var result = await RenderCardAsync(guildId, await GetTemplateAsync(guildId, target), user);
        await using var image = result.Image;
        return image.ToArray();
    }

    /// <summary>
    ///     Draws a design for a member and reports where each element landed, for previews.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="template">The design.</param>
    /// <param name="user">The member.</param>
    /// <param name="hiddenTextId">An element drawn without its text, as the designer shows one being typed over.</param>
    /// <returns>The image and layout.</returns>
    public async Task<CardRenderResult> RenderCardAsync(ulong guildId, CardTemplate template, IUser user,
        string? hiddenTextId = null)
    {
        var guild = client.GetGuild(guildId);
        var replacer = new ReplacementBuilder().WithUser(user).WithClient(client).WithServer(client, guild).Build();
        var palette = guild is null ? DashboardPalette.Default : await palettes.GetAsync(guild);
        var content = new WelcomeCardContent(text => replacer.Replace(text) ?? "",
            key => LoadOwnImageAsync(key, user, guild), link => LoadLinkAsync(guildId, link))
        {
            Palette = palette
        };
        return await cards.RenderAsync(content, template, hiddenTextId, MarginFor(template));
    }

    /// <summary>
    ///     The transparent space around a drawn card: room for the shadow when the design has one, otherwise none, so a
    ///     plain card is exactly its own size.
    /// </summary>
    /// <param name="template">The design.</param>
    /// <returns>The margin in pixels.</returns>
    public static int MarginFor(CardTemplate template)
    {
        return template.Shadow ? CardRenderer.Margin : 0;
    }

    private async Task<SKBitmap?> LoadOwnImageAsync(string key, IUser user, SocketGuild? guild)
    {
        var url = key switch
        {
            "avatar" => user.GetAvatarUrl(ImageFormat.Png, 256) ?? user.GetDefaultAvatarUrl(),
            "servericon" when guild?.IconUrl is { } icon => $"{icon.Split('?')[0]}?size=256",
            _ => null
        };
        return url is null ? null : await DecodeAsync(await DownloadImageAsync(url));
    }

    private async Task<SKBitmap?> LoadLinkAsync(ulong guildId, string link)
    {
        var reference = AchievementIcons.Parse(link);
        if (AchievementIcons.UploadId(reference) is { } id)
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var data = await db.WelcomeCardImages.Where(x => x.GuildId == guildId && x.Id == id)
                .Select(x => x.Data)
                .FirstOrDefaultAsync();
            return await DecodeAsync(data);
        }

        return reference.Kind == AchievementIconKind.Url ? await DecodeAsync(await DownloadImageAsync(link)) : null;
    }

    private static Task<SKBitmap?> DecodeAsync(byte[]? bytes)
    {
        return Task.FromResult(bytes is null ? null : SKBitmap.Decode(bytes));
    }

    private static (byte[] Image, byte[] Thumbnail)? Normalize(byte[] bytes)
    {
        using var source = SKBitmap.Decode(bytes);
        if (source is null || source.Width == 0 || source.Height == 0)
            return null;

        return (Encode(source, ImageSize), Encode(source, ThumbnailSize));
    }

    private static byte[] Encode(SKBitmap source, int maxSide)
    {
        var scale = Math.Min(1f, (float)maxSide / Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        using var resized = source.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul),
            new SKSamplingOptions(SKCubicResampler.Mitchell)) ?? source.Copy();
        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static WelcomeCardSetting Clone(WelcomeCardSetting source) => new()
    {
        Id = source.Id,
        GuildId = source.GuildId,
        Target = source.Target,
        Enabled = source.Enabled,
        BackgroundColor = source.BackgroundColor,
        BackgroundData = source.BackgroundData,
        OverlayOpacity = source.OverlayOpacity,
        Title = source.Title,
        Subtitle = source.Subtitle,
        FontName = source.FontName,
        TextColor = source.TextColor,
        AvatarShape = source.AvatarShape,
        Template = source.Template,
        DateAdded = source.DateAdded
    };
}
