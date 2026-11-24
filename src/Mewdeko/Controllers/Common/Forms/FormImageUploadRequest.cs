namespace Mewdeko.Controllers.Common.Forms;

/// <summary>
///     Request model for uploading a picture to answer a picture question.
/// </summary>
public class FormImageUploadRequest
{
    /// <summary>
    ///     The picture, either raw base64 or a <c>data:image/...;base64,</c> URI.
    /// </summary>
    public string Data { get; set; } = null!;
}

/// <summary>
///     Response model for a stored picture answer.
/// </summary>
public class FormImageUploadResponse
{
    /// <summary>
    ///     The address of the stored copy, which is what the answer carries.
    /// </summary>
    public string Url { get; set; } = null!;
}
