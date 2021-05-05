using Discord;
using NadekoBot.Extensions;
using NadekoBot.Core.Services.Database.Models;
using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Music.Common
{
    public class SongInfo
    {
        public string Provider { get; set; }
        public MusicType ProviderType { get; set; }
        public string Query { get; set; }
        public string Title { get; set; }
        public Func<Task<string>> Uri { get; set; }
        public string Thumbnail { get; set; }
        public string QueuerName { get; set; }
        public TimeSpan TotalTime { get; set; } = TimeSpan.Zero;

        public string PrettyProvider => (Provider ?? "???");
        //public string PrettyFullTime => PrettyCurrentTime + " / " + PrettyTotalTime;
        public string PrettyName => $"**[{Title.TrimTo(65)}]({SongUrl})**";
        public string PrettyInfo => $"{PrettyTotalTime} | {PrettyProvider} | {QueuerName}";
        public string PrettyFullName => $"{PrettyName}\n\t\t`{PrettyTotalTime} | {PrettyProvider} | {Format.Sanitize(QueuerName.TrimTo(15))}`";
        public string PrettyTotalTime
        {
            get
            {
                if (TotalTime == TimeSpan.Zero)
                    return "(?)";
                if (TotalTime == TimeSpan.MaxValue)
                    return "∞";
                var time = TotalTime.ToString(@"mm\:ss");
                var hrs = (int)TotalTime.TotalHours;

                if (hrs > 0)
                    return hrs + ":" + time;
                return time;
            }
        }

        public string SongUrl
        {
            get
            {
                switch (ProviderType)
                {
                    case MusicType.YouTube:
                        return Query;
                    case MusicType.Soundcloud:
                        return Query;
                    case MusicType.Local:
                        return $"https://google.com/search?q={ WebUtility.UrlEncode(Title).Replace(' ', '+') }";
                    case MusicType.Radio:
                        return $"https://google.com/search?q={Title}";
                    default:
                        return "";
                }
            }
        }
        private string _videoId = null;
        public string VideoId
        {
            get
            {
                if (ProviderType == MusicType.YouTube)
                    return _videoId = _videoId ?? videoIdRegex.Match(Query)?.ToString();

                return _videoId ?? "";
            }

            set => _videoId = value;
        }

        private readonly Regex videoIdRegex = new Regex("<=v=[a-zA-Z0-9-]+(?=&)|(?<=[0-9])[^&\n]+|(?<=v=)[^&\n]+", RegexOptions.Compiled);
    }
}
