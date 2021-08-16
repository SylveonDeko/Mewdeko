using System;

namespace Mewdeko.Modules.Music.Services
{
    public sealed partial class YtLoader
    {
        public class InitRange
        {
            public string Start { get; set; }
            public string End { get; set; }
        }

        public class IndexRange
        {
            public string Start { get; set; }
            public string End { get; set; }
        }

        public class ColorInfo
        {
            public string Primaries { get; set; }
            public string TransferCharacteristics { get; set; }
            public string MatrixCoefficients { get; set; }
        }

        public class YtAdaptiveFormat
        {
            public int Itag { get; set; }
            public string MimeType { get; set; }
            public int Bitrate { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public InitRange InitRange { get; set; }
            public IndexRange IndexRange { get; set; }
            public string LastModified { get; set; }
            public string ContentLength { get; set; }
            public string Quality { get; set; }
            public int Fps { get; set; }
            public string QualityLabel { get; set; }
            public string ProjectionType { get; set; }
            public int AverageBitrate { get; set; }
            public ColorInfo ColorInfo { get; set; }
            public string ApproxDurationMs { get; set; }
            public string SignatureCipher { get; set; }
        }

        public abstract class TrackInfo
        {
            public abstract string Url { get; }
            public abstract string Title { get; }
            public abstract TimeSpan Duration { get; }
        }

        public sealed class YtTrackInfo : TrackInfo
        {
            private const string BaseYoutubeUrl = "https://youtube.com?v=";

            private readonly string _videoId;

            public YtTrackInfo(string title, string videoId, TimeSpan duration)
            {
                Title = title;
                Url = BaseYoutubeUrl + videoId;
                Duration = duration;

                _videoId = videoId;
            }

            public override string Url { get; }
            public override string Title { get; }
            public override TimeSpan Duration { get; }
        }
    }
}