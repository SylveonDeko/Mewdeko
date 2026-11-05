using Discord.Interactions;
using Discord.Net;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Server_Management.Services;

namespace Mewdeko.Modules.Server_Management;

public partial class SlashChannel
{
    /// <summary>
    ///     Slash commands that change the settings of any channel, sync its permissions or clone it.
    /// </summary>
    public class SlashChannelEdit : MewdekoSlashModuleBase<ChannelEditService>
    {
        /// <summary>
        ///     Changes several settings of a channel at once.
        /// </summary>
        /// <param name="channel">The channel to edit.</param>
        /// <param name="name">The new name.</param>
        /// <param name="topic">The new topic. Use a single dash to clear it.</param>
        /// <param name="nsfw">Whether the channel is age restricted.</param>
        /// <param name="slowmode">The slowmode in seconds, up to 21600.</param>
        /// <param name="bitrate">The voice bitrate in kbps.</param>
        /// <param name="userLimit">The voice user limit. Zero removes the limit.</param>
        /// <param name="category">The category to move the channel to.</param>
        /// <param name="removeCategory">Take the channel out of its category.</param>
        /// <param name="position">The position in the channel list, counting from 1.</param>
        [SlashCommand("edit", "Change a channel's name, topic, nsfw flag, slowmode, bitrate, limit, category or position")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task Edit([Summary("channel", "The channel to edit")] IGuildChannel channel,
            [Summary("name", "The new name")] string? name = null,
            [Summary("topic", "The new topic, or a dash to clear it")]
            string? topic = null,
            [Summary("nsfw", "Whether the channel is age restricted")]
            bool? nsfw = null,
            [Summary("slowmode", "Slowmode in seconds")] [MinValue(0)] [MaxValue(ChannelEditService.MaxSlowMode)]
            int? slowmode = null,
            [Summary("bitrate", "Voice bitrate in kbps")] [MinValue(8)]
            int? bitrate = null,
            [Summary("user-limit", "Voice user limit, zero for none")] [MinValue(0)] [MaxValue(ChannelEditService.MaxUserLimit)]
            int? userLimit = null,
            [Summary("category", "The category to move it to")] [ChannelTypes(ChannelType.Category)]
            IGuildChannel? category = null,
            [Summary("remove-category", "Take it out of its category")]
            bool removeCategory = false,
            [Summary("position", "Position in the channel list")] [MinValue(1)]
            int? position = null)
        {
            var guildId = ctx.Guild.Id;
            var request = new ChannelEditRequest(name, topic is "-" ? "" : topic, nsfw, slowmode, bitrate, userLimit,
                category?.Id, removeCategory && category is null, position - 1);
            if (request == new ChannelEditRequest(RemoveCategory: false))
            {
                await ErrorAsync(Strings.ChanEditNothing(guildId));
                return;
            }

            try
            {
                var error = await Service.EditAsync(channel, request);
                var mention = $"<#{channel.Id}>";
                var text = error switch
                {
                    ChannelEditError.None => null,
                    ChannelEditError.BadName => Strings.ChanEditBadName(guildId),
                    ChannelEditError.BadBitrate => Strings.ChanEditBadBitrate(guildId, ctx.Guild.MaxBitrate / 1000),
                    ChannelEditError.BadUserLimit => Strings.ChanEditBadUserLimit(guildId,
                        ChannelEditService.MaxUserLimit),
                    ChannelEditError.BadSlowmode => Strings.SlowmodeMax(guildId),
                    ChannelEditError.BadPosition => Strings.ChanEditBadPosition(guildId),
                    _ => Strings.ChanEditUnsupported(guildId, mention)
                };

                if (text is null)
                    await ConfirmAsync(Strings.ChanEditUpdated(guildId, mention));
                else
                    await ErrorAsync(text);
            }
            catch (HttpException ex)
            {
                await ErrorAsync(Strings.ChanEditFailed(guildId, ex.Reason ?? ex.Message));
            }
        }

        /// <summary>
        ///     Makes a channel's permissions match its category.
        /// </summary>
        /// <param name="channel">The channel.</param>
        [SlashCommand("sync-permissions", "Make a channel's permissions match its category")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task SyncPermissions([Summary("channel", "The channel to sync")] IGuildChannel channel)
        {
            var guildId = ctx.Guild.Id;
            try
            {
                if (await Service.SyncPermissionsAsync(channel))
                    await ConfirmAsync(Strings.ChanSyncDone(guildId, $"<#{channel.Id}>"));
                else
                    await ErrorAsync(Strings.ChanSyncNoCategory(guildId, $"<#{channel.Id}>"));
            }
            catch (HttpException ex)
            {
                await ErrorAsync(Strings.ChanEditFailed(guildId, ex.Reason ?? ex.Message));
            }
        }

        /// <summary>
        ///     Creates a copy of a channel with the same settings and permissions.
        /// </summary>
        /// <param name="channel">The channel to copy.</param>
        /// <param name="name">The name of the copy. Defaults to the same name.</param>
        [SlashCommand("clone", "Copy a channel with its settings and permissions")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageChannels)]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        public async Task Clone([Summary("channel", "The channel to copy")] IGuildChannel channel,
            [Summary("name", "The name of the copy")]
            string? name = null)
        {
            var guildId = ctx.Guild.Id;
            try
            {
                var copy = await Service.CloneAsync(channel, name);
                if (copy is null)
                    await ErrorAsync(Strings.ChanEditUnsupported(guildId, $"<#{channel.Id}>"));
                else
                    await ConfirmAsync(Strings.ChanCloneDone(guildId, $"<#{channel.Id}>", $"<#{copy.Id}>"));
            }
            catch (HttpException ex)
            {
                await ErrorAsync(Strings.ChanEditFailed(guildId, ex.Reason ?? ex.Message));
            }
        }
    }
}
