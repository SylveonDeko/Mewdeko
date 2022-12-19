using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Mewdeko.Extensions;

public static class Rgba32Extensions
{
    public static Image<Rgba32> Merge(this IEnumerable<Image<Rgba32>> images) => images.Merge(out _);

    public static Image<Rgba32> Merge(this IEnumerable<Image<Rgba32>> images, out IImageFormat format)
    {
        format = PngFormat.Instance;

        void DrawFrame(IEnumerable<Image<Rgba32>> imgArray, Image<Rgba32> imgFrame, int frameNumber)
        {
            var xOffset = 0;
            foreach (var t in imgArray)
            {
                var frame = t.Frames.CloneFrame(frameNumber % t.Frames.Count);
                var offset = xOffset;
                imgFrame.Mutate(x => x.DrawImage(frame, new Point(offset, 0), new GraphicsOptions()));
                xOffset += t.Bounds().Width;
            }
        }

        var enumerable = images as Image<Rgba32>[] ?? images.ToArray();
        var imgs = enumerable.ToArray();
        var frames = enumerable.Max(x => x.Frames.Count);

        var width = imgs.Sum(img => img.Width);
        var height = imgs.Max(img => img.Height);
        var canvas = new Image<Rgba32>(width, height);
        if (frames == 1)
        {
            DrawFrame(imgs, canvas, 0);
            return canvas;
        }

        format = GifFormat.Instance;
        for (var j = 0; j < frames; j++)
        {
            using var imgFrame = new Image<Rgba32>(width, height);
            DrawFrame(imgs, imgFrame, j);

            var frameToAdd = imgFrame.Frames[0];
            frameToAdd.Metadata.GetGifMetadata().DisposalMethod = GifDisposalMethod.RestoreToBackground;
            canvas.Frames.AddFrame(frameToAdd);
        }

        canvas.Frames.RemoveFrame(0);
        return canvas;
    }
}