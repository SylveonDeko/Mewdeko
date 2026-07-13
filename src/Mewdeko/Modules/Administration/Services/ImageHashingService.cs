using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Numerics;
using System.Threading;
using PdqHash.Hashing;
using SkiaSharp;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     The perceptual hash of a blocked image: the full frame hash plus the variant hashes that let a mirrored or
///     bordered re-upload still match.
/// </summary>
/// <param name="Hash">The full frame hash, used for display and de-duplication.</param>
/// <param name="Quality">The PDQ quality score, from 0 to 100.</param>
/// <param name="Variants">The mirrored and border-stripped hashes of the same image.</param>
public record ImageHashSet(string Hash, int Quality, IReadOnlyList<string> Variants);

/// <summary>
///     The hashes computed for a posted image that is being checked against the blocklist.
/// </summary>
/// <param name="Quality">The PDQ quality score of the full frame, from 0 to 100.</param>
/// <param name="Hashes">The full frame hash, plus the border-stripped hash when the image has a solid border.</param>
public record ImageMatchHashes(int Quality, IReadOnlyList<string> Hashes);

/// <summary>
///     Computes and compares perceptual image hashes using PDQ, Meta's 256 bit DCT based image hash.
/// </summary>
/// <remarks>
///     PDQ is robust to what a re-uploader normally does to an image (re-encoding, rescaling, recompression, brightness
///     shifts, changing overlaid text) but, like every global DCT hash, it is extremely sensitive to framing: trimming
///     even 1% off an image moves its hash by around 30 of 256 bits, which is the entire match budget. Measured against a
///     corpus of real scam images, that means:
///     <list type="bullet">
///         <item>Resizing, recompressing, and brightening a blocked image are all caught, every time.</item>
///         <item>Mirroring is caught, because each blocked image also stores the hash of its mirror.</item>
///         <item>
///             Wrapping a blocked image in a border is caught, because a solid border can be measured and stripped back
///             off rather than guessed at.
///         </item>
///         <item>
///             Arbitrary cropping is <b>not</b> caught, and cannot be by any variant of this approach: guessing a fixed
///             set of crops only catches the crops you guessed. Nor is a fresh photo or screenshot of the same scam,
///             which is a different image as far as any perceptual hash is concerned. Catching those needs image
///             embeddings, not hashing.
///         </item>
///     </list>
/// </remarks>
public class ImageHashingService : INService
{
    /// <summary>
    ///     The number of hex characters in a PDQ hash.
    /// </summary>
    public const int HashHexLength = 64;

    /// <summary>
    ///     The maximum possible hamming distance between two PDQ hashes.
    /// </summary>
    public const int MaxDistance = 256;

    /// <summary>
    ///     PDQ's own guidance: below this quality score the image is too flat or too low in detail for its hash to
    ///     discriminate, and matching it would produce false positives.
    /// </summary>
    public const int MinReliableQuality = 50;

    /// <summary>
    ///     The longest edge an image is scaled to before hashing. Scaling is aspect preserving so that an image and a
    ///     resized copy of it land on the same hash.
    /// </summary>
    private const int MaxWorkingDimension = 1024;

    /// <summary>
    ///     How far a pixel may sit from the edge colour and still count as part of a border, out of 255.
    /// </summary>
    private const int BorderColorTolerance = 14;

    /// <summary>
    ///     A detected border is only trimmed if it takes up at least this fraction of the edge, so ordinary dark image
    ///     content is not mistaken for padding.
    /// </summary>
    private const float MinBorderFraction = 0.02f;

    private readonly IHttpClientFactory httpFactory;
    private readonly ILogger<ImageHashingService> logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageHashingService" /> class.
    /// </summary>
    /// <param name="httpFactory">The http client factory used to download images.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public ImageHashingService(IHttpClientFactory httpFactory, ILogger<ImageHashingService> logger)
    {
        this.httpFactory = httpFactory;
        this.logger = logger;
    }

    /// <summary>
    ///     Computes the full set of hashes stored for a blocked image: the full frame, its mirror, and, when the image has a
    ///     solid border, the same two with the border stripped off.
    /// </summary>
    /// <param name="data">The raw bytes of a png, jpeg, webp, gif, or bmp image.</param>
    /// <returns>The hash set, or null if the data could not be decoded as an image.</returns>
    public ImageHashSet? ComputeHashSet(byte[] data)
    {
        try
        {
            using var working = DecodeAndNormalize(data);
            if (working is null)
                return null;

            using var hasher = new PdqHasher();

            var full = HashRegion(hasher, working, false);
            if (full is null)
                return null;

            var variants = new List<string>();

            var mirrored = HashRegion(hasher, working, true);
            if (mirrored is not null)
                variants.Add(mirrored.Value.Hash);

            // If the image itself carries a border, also store it without one, so a copy posted without the border, or
            // with a different one, still matches.
            using var trimmed = TryTrimBorder(working);
            if (trimmed is not null)
            {
                var trimmedHash = HashRegion(hasher, trimmed, false);
                if (trimmedHash is not null)
                    variants.Add(trimmedHash.Value.Hash);

                var trimmedMirror = HashRegion(hasher, trimmed, true);
                if (trimmedMirror is not null)
                    variants.Add(trimmedMirror.Value.Hash);
            }

            return new ImageHashSet(full.Value.Hash, full.Value.Quality,
                variants.Distinct().Where(v => v != full.Value.Hash).ToList());
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to compute image hash set");
            return null;
        }
    }

    /// <summary>
    ///     Computes the hashes a posted image is checked with: the full frame, plus the image with any solid border stripped
    ///     off.
    /// </summary>
    /// <param name="data">The raw bytes of the posted image.</param>
    /// <param name="stripBorders">Whether to also hash the image with any solid border stripped off.</param>
    /// <returns>The hashes and the full frame quality, or null if the data could not be decoded as an image.</returns>
    public ImageMatchHashes? ComputeMatchHashes(byte[] data, bool stripBorders)
    {
        try
        {
            using var working = DecodeAndNormalize(data);
            if (working is null)
                return null;

            using var hasher = new PdqHasher();

            var full = HashRegion(hasher, working, false);
            if (full is null)
                return null;

            var hashes = new List<string>
            {
                full.Value.Hash
            };

            // Someone wrapping a blocked image in a border to disguise it produces a different hash. Stripping the
            // border back off recovers the original, which is the one case of "cropping" that can be undone reliably.
            if (stripBorders)
            {
                using var trimmed = TryTrimBorder(working);
                if (trimmed is not null)
                {
                    var trimmedHash = HashRegion(hasher, trimmed, false);
                    if (trimmedHash is not null)
                        hashes.Add(trimmedHash.Value.Hash);
                }
            }

            return new ImageMatchHashes(full.Value.Quality, hashes.Distinct().ToList());
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to compute image match hashes");
            return null;
        }
    }

    /// <summary>
    ///     Downloads an image and computes the hash set stored for a blocked image.
    /// </summary>
    /// <param name="url">The image URL.</param>
    /// <param name="maxSizeMb">The maximum size to download, in megabytes. Larger images are skipped.</param>
    /// <param name="cancellationToken">A token used to cancel the download.</param>
    /// <returns>The hash set, or null if the image could not be fetched or decoded.</returns>
    public async Task<ImageHashSet?> ComputeHashSetFromUrlAsync(string url, int maxSizeMb = 8,
        CancellationToken cancellationToken = default)
    {
        var data = await DownloadAsync(url, maxSizeMb, cancellationToken).ConfigureAwait(false);
        return data is null ? null : ComputeHashSet(data);
    }

    /// <summary>
    ///     Downloads a posted image and computes the hashes it is matched with.
    /// </summary>
    /// <param name="url">The image URL.</param>
    /// <param name="stripBorders">Whether to also hash the image with any solid border stripped off.</param>
    /// <param name="maxSizeMb">The maximum size to download, in megabytes.</param>
    /// <param name="cancellationToken">A token used to cancel the download.</param>
    /// <returns>The hashes, or null if the image could not be fetched or decoded.</returns>
    public async Task<ImageMatchHashes?> ComputeMatchHashesFromUrlAsync(string url, bool stripBorders,
        int maxSizeMb = 8, CancellationToken cancellationToken = default)
    {
        var data = await DownloadAsync(url, maxSizeMb, cancellationToken).ConfigureAwait(false);
        return data is null ? null : ComputeMatchHashes(data, stripBorders);
    }

    /// <summary>
    ///     Parses a PDQ hash from its 64 character hex form into four words.
    /// </summary>
    /// <param name="hash">The hash as hex characters.</param>
    /// <param name="value">The parsed hash.</param>
    /// <returns>True if the hash was valid; otherwise false.</returns>
    public static bool TryParseHash(string? hash, out ulong[] value)
    {
        value = [];

        if (string.IsNullOrWhiteSpace(hash))
            return false;

        var trimmed = hash.Trim();
        if (trimmed.Length != HashHexLength)
            return false;

        var words = new ulong[4];

        for (var i = 0; i < 4; i++)
        {
            if (!ulong.TryParse(trimmed.AsSpan(i * 16, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                    out words[i]))
                return false;
        }

        value = words;
        return true;
    }

    /// <summary>
    ///     Gets the number of differing bits between two PDQ hashes, from 0 (identical) to 256 (opposite).
    /// </summary>
    /// <param name="left">The first hash.</param>
    /// <param name="right">The second hash.</param>
    /// <returns>The hamming distance between the hashes.</returns>
    public static int Distance(ulong[] left, ulong[] right)
    {
        if (left.Length != right.Length)
            return MaxDistance;

        var distance = 0;
        for (var i = 0; i < left.Length; i++)
            distance += BitOperations.PopCount(left[i] ^ right[i]);

        return distance;
    }

    /// <summary>
    ///     Decodes an image, flattens transparency onto white, and scales it so its longest edge is at most
    ///     <see cref="MaxWorkingDimension" />. The scale is aspect preserving so an image and a resized copy of it produce
    ///     the same hash.
    /// </summary>
    private static SKBitmap? DecodeAndNormalize(byte[] data)
    {
        using var decoded = SKBitmap.Decode(data);
        if (decoded is null || decoded.Width == 0 || decoded.Height == 0)
            return null;

        var scale = Math.Min(1f,
            MaxWorkingDimension / (float)Math.Max(decoded.Width, decoded.Height));

        var width = Math.Max(1, (int)MathF.Round(decoded.Width * scale));
        var height = Math.Max(1, (int)MathF.Round(decoded.Height * scale));

        var normalized = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));

        using (var canvas = new SKCanvas(normalized))
        {
            canvas.Clear(SKColors.White);
            using var image = SKImage.FromBitmap(decoded);
            canvas.DrawImage(image,
                new SKRect(0, 0, decoded.Width, decoded.Height),
                new SKRect(0, 0, width, height),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Nearest));
        }

        return normalized;
    }

    /// <summary>
    ///     Hashes the image, optionally mirrored.
    /// </summary>
    /// <param name="hasher">The PDQ hasher.</param>
    /// <param name="source">The normalized working image.</param>
    /// <param name="mirror">Whether to mirror the image horizontally before hashing.</param>
    private static (string Hash, int Quality)? HashRegion(PdqHasher hasher, SKBitmap source, bool mirror)
    {
        if (source.Width < 16 || source.Height < 16)
            return null;

        if (!mirror)
        {
            var direct = hasher.FromBitmap(source, "mewdeko");
            return (direct.Hash.ToString(), direct.Quality);
        }

        using var flipped = new SKBitmap(new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888,
            SKAlphaType.Opaque));

        using (var canvas = new SKCanvas(flipped))
        {
            canvas.Clear(SKColors.White);
            canvas.Translate(source.Width, 0);
            canvas.Scale(-1, 1);

            using var image = SKImage.FromBitmap(source);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
        }

        var result = hasher.FromBitmap(flipped, "mewdeko");
        return (result.Hash.ToString(), result.Quality);
    }

    /// <summary>
    ///     Detects a solid border around the image and returns the image with it removed, or null if there is no border to
    ///     remove.
    /// </summary>
    /// <remarks>
    ///     PDQ is extremely sensitive to framing: trimming even 1% off an image moves its hash by about 30 of 256 bits, so
    ///     an image padded with a border no longer matches the original and no fixed set of guessed crops can reliably undo
    ///     it. A solid border can be measured instead of guessed, which makes it the one framing change that can be reversed
    ///     exactly. Arbitrary crops of image content remain out of reach.
    /// </remarks>
    private static SKBitmap? TryTrimBorder(SKBitmap source)
    {
        using var pixmap = source.PeekPixels();
        if (pixmap is null)
            return null;

        var top = 0;
        var bottom = source.Height - 1;
        var left = 0;
        var right = source.Width - 1;

        while (top < bottom && IsUniformRow(pixmap, top, left, right))
            top++;

        while (bottom > top && IsUniformRow(pixmap, bottom, left, right))
            bottom--;

        while (left < right && IsUniformColumn(pixmap, left, top, bottom))
            left++;

        while (right > left && IsUniformColumn(pixmap, right, top, bottom))
            right--;

        var width = right - left + 1;
        var height = bottom - top + 1;

        if (width < 16 || height < 16)
            return null;

        var trimmedX = source.Width - width;
        var trimmedY = source.Height - height;

        // Nothing meaningful was removed, so the extra hash would be a duplicate of the full frame.
        if (trimmedX < source.Width * MinBorderFraction && trimmedY < source.Height * MinBorderFraction)
            return null;

        var trimmed = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));

        using (var canvas = new SKCanvas(trimmed))
        {
            canvas.Clear(SKColors.White);
            using var image = SKImage.FromBitmap(source);
            canvas.DrawImage(image,
                new SKRect(left, top, left + width, top + height),
                new SKRect(0, 0, width, height),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Nearest));
        }

        return trimmed;
    }

    private static bool IsUniformRow(SKPixmap pixmap, int y, int left, int right)
    {
        var reference = pixmap.GetPixelColor(left, y);

        for (var x = left; x <= right; x++)
        {
            if (!IsCloseTo(pixmap.GetPixelColor(x, y), reference))
                return false;
        }

        return true;
    }

    private static bool IsUniformColumn(SKPixmap pixmap, int x, int top, int bottom)
    {
        var reference = pixmap.GetPixelColor(x, top);

        for (var y = top; y <= bottom; y++)
        {
            if (!IsCloseTo(pixmap.GetPixelColor(x, y), reference))
                return false;
        }

        return true;
    }

    private static bool IsCloseTo(SKColor color, SKColor reference)
    {
        return Math.Abs(color.Red - reference.Red) <= BorderColorTolerance &&
               Math.Abs(color.Green - reference.Green) <= BorderColorTolerance &&
               Math.Abs(color.Blue - reference.Blue) <= BorderColorTolerance;
    }

    private async Task<byte[]?> DownloadAsync(string url, int maxSizeMb, CancellationToken cancellationToken)
    {
        try
        {
            var maxBytes = Math.Clamp(maxSizeMb, 1, 32) * 1024L * 1024L;

            using var http = httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(15);

            using var response = await http
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            if (response.Content.Headers.ContentLength > maxBytes)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var buffer = new MemoryStream();

            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > maxBytes)
                    return null;

                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to download image for hashing from {Url}", url);
            return null;
        }
    }
}