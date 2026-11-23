namespace Mewdeko.Controllers.Common.Embeds;

/// <summary>
///     Request model for uploading a picture to use in a built message.
/// </summary>
public class UploadEmbedImageRequest
{
    /// <summary>
    ///     The image, either raw base64 or a <c>data:image/...;base64,</c> URI.
    /// </summary>
    public string Data { get; set; } = null!;
}

/// <summary>
///     Response model for an uploaded picture.
/// </summary>
public class EmbedImageResponse
{
    /// <summary>
    ///     The public address the picture can be reached at, ready to paste into an image field.
    /// </summary>
    public string Url { get; set; } = null!;
}
