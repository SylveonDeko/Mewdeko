namespace Mewdeko.Modules.Nsfw.Common;

/// <summary>
///     Represents an interface for image data.
/// </summary>
public interface IImageData
{
    /// <summary>
    ///     Converts the image data to cached image data specific to the provided Booru type.
    /// </summary>
    /// <param name="type">The type of Booru the image belongs to.</param>
    /// <returns>The cached image data.</returns>
    ImageData ToCachedImageData(Booru type);
}