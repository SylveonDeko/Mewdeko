using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     A known scam image shipped with the bot, as its parsed hashes.
/// </summary>
/// <param name="Id">A stable identifier, for example crypto-casino-014.</param>
/// <param name="Name">The label shown when the image is caught.</param>
/// <param name="Hashes">The full frame hash plus the mirrored and border-stripped variants.</param>
public record PresetScamImage(string Id, string Name, IReadOnlyList<ulong[]> Hashes);

/// <summary>
///     Loads the list of known scam image hashes that ships with the bot, so a guild can block the images every server
///     sees without having to collect them first.
/// </summary>
/// <remarks>
///     Only hashes are shipped, never the images themselves. The list is regenerated from a folder of samples by the
///     ScamPresetGenerator test. Because these are hashes of specific image files, the list catches those files being
///     re-shared, including resized, recompressed, mirrored, and bordered copies. It cannot catch a fresh screenshot or
///     photo of the same scam, which is a different image.
/// </remarks>
public class ScamImagePresetService : INService
{
    private readonly ILogger<ScamImagePresetService> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScamImagePresetService" /> class, loading the shipped hash list.
    /// </summary>
    /// <param name="logger">The logger instance for structured logging.</param>
    public ScamImagePresetService(ILogger<ScamImagePresetService> logger)
    {
        this.logger = logger;
        Images = Load();
    }

    /// <summary>
    ///     Gets the known scam images. Empty if the shipped list is missing or unreadable, which disables the feature rather
    ///     than breaking protection.
    /// </summary>
    public IReadOnlyList<PresetScamImage> Images { get; }

    /// <summary>
    ///     Gets the campaign the shipped list covers, for display.
    /// </summary>
    public string Campaign { get; private set; } = "";

    private IReadOnlyList<PresetScamImage> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "scam_image_hashes.json");

        try
        {
            if (!File.Exists(path))
            {
                logger.LogWarning("Known scam image list not found at {Path}; the preset will be unavailable", path);
                return [];
            }

            var file = JsonSerializer.Deserialize<PresetFile>(File.ReadAllText(path), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (file?.Entries is null)
                return [];

            Campaign = file.Campaign ?? "";

            var images = new List<PresetScamImage>();

            foreach (var entry in file.Entries)
            {
                var hashes = new List<ulong[]>();

                if (ImageHashingService.TryParseHash(entry.Hash, out var full))
                    hashes.Add(full);

                foreach (var variant in entry.Variants ?? [])
                {
                    if (ImageHashingService.TryParseHash(variant, out var value))
                        hashes.Add(value);
                }

                if (hashes.Count > 0)
                    images.Add(new PresetScamImage(entry.Id ?? "", entry.Name ?? "Known scam image", hashes));
            }

            logger.LogInformation("Loaded {Count} known scam image hashes", images.Count);
            return images;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load the known scam image list from {Path}", path);
            return [];
        }
    }

    private class PresetFile
    {
        [JsonPropertyName("campaign")]
        public string? Campaign { get; set; }

        [JsonPropertyName("entries")]
        public List<PresetEntry>? Entries { get; set; }
    }

    private class PresetEntry
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("hash")]
        public string? Hash { get; set; }

        [JsonPropertyName("variants")]
        public List<string>? Variants { get; set; }
    }
}