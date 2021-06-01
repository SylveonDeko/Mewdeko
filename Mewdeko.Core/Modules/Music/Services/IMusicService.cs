using System.Threading.Tasks;
using Discord;
using Mewdeko.Core.Modules.Music;

namespace Mewdeko.Modules.Music.Services
{
    public interface IMusicService
    {
        /// <summary>
        /// Leave voice channel in the specified guild if it's connected to one
        /// </summary>
        /// <param name="guildId">Id of the guild</param>
        public Task LeaveVoiceChannelAsync(ulong guildId);

        /// <summary>
        /// Joins the voice channel with the specified id
        /// </summary>
        /// <param name="guildId">Id of the guild where the voice channel is</param>
        /// <param name="voiceChannelId">Id of the voice channel</param>
        public Task JoinVoiceChannelAsync(ulong guildId, ulong voiceChannelId);
        
        IMusicPlayer GetOrCreateMusicPlayer(ITextChannel contextChannel);
        bool TryGetMusicPlayer(ulong guildId, out IMusicPlayer musicPlayer);
        void SetDefaultVolume(ulong guildId, int val);
        Task<int> EnqueueYoutubePlaylistAsync(IMusicPlayer mp, string playlistId, string queuer);
        Task EnqueueDirectoryAsync(IMusicPlayer mp, string dirPath, string queuer);
        Task<int> EnqueueSoundcloudPlaylistAsync(IMusicPlayer mp, string playlist, string queuer);
        Task<IUserMessage> SendToOutputAsync(ulong guildId, EmbedBuilder embed);
        bool SetMusicChannel(ulong guildId, ulong channelId);
        void UnsetMusicChannel(ulong guildId);
        Task<bool> PlayAsync(ulong guildId, ulong voiceChannelId);
    }
}