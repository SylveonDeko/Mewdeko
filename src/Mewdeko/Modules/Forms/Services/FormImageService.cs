using SkiaSharp;
using Mewdeko.Services.Impl;

namespace Mewdeko.Modules.Forms.Services;

/// <summary>
///     Takes a picture a submitter attached to a form answer and keeps a safe copy of it on the CDN.
/// </summary>
/// <remarks>
///     The type is decided from the file's own bytes and the picture is decoded and written out again
///     as webp, so nothing the submitter sent is ever served back as it arrived.
/// </remarks>
public class FormImageService(CdnStorageService cdn, IBotCredentials creds, ILogger<FormImageService> logger)
    : INService
{
    /// <summary>
    ///     The largest upload accepted before decoding, the same ceiling the embed builder upload uses.
    /// </summary>
    public const int MaxUploadBytes = 8 * 1024 * 1024;

    /// <summary>
    ///     The largest picture accepted once decoded, so a small file that expands into hundreds of
    ///     megapixels is refused before anything is allocated for it.
    /// </summary>
    private const long MaxPixels = 40_000_000;

    /// <summary>
    ///     The longest edge of the stored copy. Anything larger is scaled down to fit.
    /// </summary>
    private const int MaxDimension = 2048;

    private const int WebpQuality = 82;

    private const string Folder = "forms/images";

    private const int UploadsPerWindow = 30;

    private static readonly TimeSpan UploadWindow = TimeSpan.FromHours(1);

    private readonly Dictionary<(ulong UserId, int FormId), Queue<DateTime>> recentUploads = new();

    /// <summary>
    ///     Counts an upload against the person's allowance for a form, which keeps one account from
    ///     filling the CDN.
    /// </summary>
    /// <param name="userId">Who is uploading.</param>
    /// <param name="formId">The form the picture is for.</param>
    /// <returns>False when they have used their allowance for the hour.</returns>
    public bool TryReserveUpload(ulong userId, int formId)
    {
        var now = DateTime.UtcNow;

        lock (recentUploads)
        {
            foreach (var key in recentUploads.Where(e => e.Value.Count == 0 || now - e.Value.Peek() > UploadWindow)
                         .Select(e => e.Key).ToList())
                recentUploads.Remove(key);

            if (!recentUploads.TryGetValue((userId, formId), out var times))
                recentUploads[(userId, formId)] = times = new Queue<DateTime>();

            while (times.Count > 0 && now - times.Peek() > UploadWindow)
                times.Dequeue();

            if (times.Count >= UploadsPerWindow)
                return false;

            times.Enqueue(now);
            return true;
        }
    }

    /// <summary>
    ///     Whether uploads can be stored on this instance.
    /// </summary>
    public bool IsConfigured => cdn.IsConfigured;

    /// <summary>
    ///     Whether an address is a picture this service stored for a form. An image answer that is not
    ///     is refused, so a typed address cannot stand in for a file that was never re-encoded.
    /// </summary>
    /// <param name="url">The address to check.</param>
    /// <param name="formId">The form the answer belongs to.</param>
    /// <returns>True when the address points at a stored picture of that form.</returns>
    public bool IsStoredImage(string? url, int formId)
    {
        return IsStoredImageAddress(url, creds.CdnUrl, formId);
    }

    /// <summary>
    ///     Whether an address is a picture stored for a form under a given CDN address.
    /// </summary>
    /// <param name="url">The address to check.</param>
    /// <param name="cdnUrl">The public address of the CDN.</param>
    /// <param name="formId">The form the answer belongs to.</param>
    /// <returns>True when the address points at a stored picture of that form.</returns>
    public static bool IsStoredImageAddress(string? url, string? cdnUrl, int formId)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(cdnUrl))
            return false;

        var prefix = $"{cdnUrl.TrimEnd('/')}/{Folder}/{formId}/";

        if (!url.StartsWith(prefix, StringComparison.Ordinal) || !url.EndsWith(".webp", StringComparison.Ordinal))
            return false;

        var name = url[prefix.Length..^".webp".Length];

        return name.Length == 32 && name.All(char.IsAsciiHexDigitLower);
    }

    /// <summary>
    ///     Decodes an uploaded payload from raw base64 or a <c>data:</c> URI.
    /// </summary>
    /// <param name="data">The submitted payload.</param>
    /// <param name="bytes">The decoded bytes, when decoding succeeds.</param>
    /// <param name="problem">Why the payload was refused, when it was.</param>
    /// <returns>True when the payload decoded to a non-empty file within the size limit.</returns>
    public static bool TryDecode(string? data, out byte[]? bytes, out string? problem)
    {
        bytes = null;
        problem = null;

        if (string.IsNullOrWhiteSpace(data))
        {
            problem = "Choose a picture to upload.";
            return false;
        }

        var payload = data.Trim();

        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = payload.IndexOf(',');

            if (comma < 0)
            {
                problem = "The picture could not be read.";
                return false;
            }

            payload = payload[(comma + 1)..];
        }

        try
        {
            bytes = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            problem = "The picture could not be read.";
            return false;
        }

        if (bytes.Length == 0)
        {
            problem = "The picture is empty.";
            return false;
        }

        if (bytes.Length > MaxUploadBytes)
        {
            problem = "The picture must be 8 MB or smaller.";
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Identifies png, jpg, gif and webp by their signature bytes.
    /// </summary>
    /// <param name="bytes">The file.</param>
    /// <returns>True when the file starts like one of those formats.</returns>
    public static bool HasAllowedSignature(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return true;

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return true;

        if (bytes.Length >= 6 && bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == '8' &&
            (bytes[4] == '7' || bytes[4] == '9') && bytes[5] == 'a')
            return true;

        return bytes.Length >= 12 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F' &&
               bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P';
    }

    /// <summary>
    ///     Re-encodes an uploaded picture and stores it for a form.
    /// </summary>
    /// <param name="content">The bytes as uploaded.</param>
    /// <param name="formId">The form the answer belongs to.</param>
    /// <returns>The address of the stored copy, or the reason the picture was refused.</returns>
    public async Task<(string? Url, string? Error)> StoreAsync(byte[] content, int formId)
    {
        if (!cdn.IsConfigured)
            return (null, "This bot cannot store uploaded pictures.");

        if (content.Length == 0 || content.Length > MaxUploadBytes)
            return (null, "The picture must be 8 MB or smaller.");

        if (!HasAllowedSignature(content))
            return (null, "Upload a png, jpg, gif or webp picture.");

        var encoded = await Task.Run(() => Reencode(content));

        if (encoded.Error != null)
            return (null, encoded.Error);

        var fileName = $"{Guid.NewGuid():N}.webp";
        var url = await cdn.SaveAsync($"{Folder}/{formId}", fileName, encoded.Bytes!);

        return url == null
            ? (null, "The picture could not be saved. Try again in a moment.")
            : (url, null);
    }

    /// <summary>
    ///     Removes every picture stored for a form, used when the form is deleted.
    /// </summary>
    /// <param name="formId">The form.</param>
    public Task DeleteFormImagesAsync(int formId)
    {
        return cdn.DeleteByPrefixAsync($"{Folder}/{formId}", string.Empty);
    }

    private (byte[]? Bytes, string? Error) Reencode(byte[] content)
    {
        try
        {
            using var codec = SKCodec.Create(new SKMemoryStream(content));

            if (codec == null || codec.Info.Width <= 0 || codec.Info.Height <= 0)
                return (null, "The picture could not be read.");

            if ((long)codec.Info.Width * codec.Info.Height > MaxPixels)
                return (null, "The picture has too many pixels.");

            using var decoded = SKBitmap.Decode(codec);

            if (decoded == null)
                return (null, "The picture could not be read.");

            var longest = Math.Max(decoded.Width, decoded.Height);
            var scale = longest > MaxDimension ? (double)MaxDimension / longest : 1d;
            var width = Math.Max(1, (int)Math.Round(decoded.Width * scale));
            var height = Math.Max(1, (int)Math.Round(decoded.Height * scale));

            using var resized = scale < 1d
                ? decoded.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul),
                    new SKSamplingOptions(SKCubicResampler.Mitchell))
                : null;

            using var image = SKImage.FromBitmap(resized ?? decoded);
            using var data = image.Encode(SKEncodedImageFormat.Webp, WebpQuality);

            return data == null ? (null, "The picture could not be read.") : (data.ToArray(), null);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Refused a form picture that could not be decoded");
            return (null, "The picture could not be read.");
        }
    }
}
