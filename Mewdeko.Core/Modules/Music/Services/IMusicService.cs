#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Mewdeko.Core.Common;
using Mewdeko.Core.Modules.Music;
using Mewdeko.Core.Services.Database.Models;

namespace Mewdeko.Modules.Music.Services
{
    public interface IMusicService : IPlaceholderProvider
    {
        /// <summary>
        ///     Leave voice channel in the specified guild if it's connected to one
        /// </summary>
        /// <param name="guildId">Id of the guild</param>
        public Task LeaveVoiceChannelAsync(ulong guildId);

        /// <summary>
        ///     Joins the voice channel with the specified id
        /// </summary>
        /// <param name="guildId">Id of the guild where the voice channel is</param>
        /// <param name="voiceChannelId">Id of the voice channel</param>
        public Task JoinVoiceChannelAsync(ulong guildId, ulong voiceChannelId);

        Task<IMusicPlayer?> GetOrCreateMusicPlayerAsync(ITextChannel contextChannel);
        bool TryGetMusicPlayer(ulong guildId, out IMusicPlayer musicPlayer);
        Task<int> EnqueueYoutubePlaylistAsync(IMusicPlayer mp, string playlistId, string queuer);
        Task EnqueueDirectoryAsync(IMusicPlayer mp, string dirPath, string queuer);
        Task<int> EnqueueSoundcloudPlaylistAsync(IMusicPlayer mp, string playlist, string queuer);
        Task<IUserMessage?> SendToOutputAsync(ulong guildId, EmbedBuilder embed);
        Task<QualityPreset> GetMusicQualityAsync(ulong guildId);
        Task SetMusicQualityAsync(ulong guildId, QualityPreset preset);

        Task<bool> PlayAsync(ulong guildId, ulong voiceChannelId);
        Task<IList<(string Title, string Url)>> SearchVideosAsync(string query);
        Task<bool> SetMusicChannelAsync(ulong guildId, ulong? channelId);
        Task SetRepeatAsync(ulong guildId, PlayerRepeatType repeatType);
        Task SetVolumeAsync(ulong guildId, int value);
        Task<bool> ToggleAutoDisconnectAsync(ulong guildId);
    }
}