using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Mewdeko._Extensions
{
    public static class Rgba32Extensions
    {
        public static Image<Rgba32> Merge(this IEnumerable<Image<Rgba32>> images)
        {
            return images.Merge(out _);
        }

        public static Image<Rgba32> Merge(this IEnumerable<Image<Rgba32>> images, out IImageFormat format)
        {
            format = PngFormat.Instance;

            void DrawFrame(Image<Rgba32>[] imgArray, Image<Rgba32> imgFrame, int frameNumber)
            {
                var xOffset = 0;
                for (var i = 0; i < imgArray.Length; i++)
                {
                    var frame = imgArray[i].Frames.CloneFrame(frameNumber % imgArray[i].Frames.Count);
                    imgFrame.Mutate(x => x.DrawImage(frame, new Point(xOffset, 0), new GraphicsOptions()));
                    xOffset += imgArray[i].Bounds().Width;
                }
            }

            var imgs = images.ToArray();
            var frames = images.Max(x => x.Frames.Count);

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
}