namespace Mewdeko.Modules.Nsfw.Common;

public interface IImageData
{
    ImageData ToCachedImageData(Booru type);
}