using Discord.Commands;
using Discord.Net;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Server_Management.Services;

namespace Mewdeko.Modules.Server_Management;

public partial class ServerManagement
{
    /// <summary>
    ///     Commands that change the settings of any channel, sync its permissions or clone it.
    /// </summary>
    [Group]
    public class ChannelEditCommands : MewdekoSubmodule<ChannelEditService>
    {
        /// <summary>
        ///     Renames a channel.
        /// </summary>
        /// <param name="channel">The channel to rename.</param>
        /// <param name="name">The new name.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageChannels)]
        public Task ChanName(IGuildChannel channel, [Remainder] string name) =>
            EditAsync(channel, new ChannelEditRequest(Name: name));

        /// <summary>
        ///     Sets the topic of a text or forum channel. Leave the topic out to clear it.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="topic">The new topic.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageChannels)]
        public Task ChanTopic(IGuildChannel channel, [Remainder] string? topic = null) =>
            EditAsync(channel, new ChannelEditRequest(Topic: topic ?? ""));

        /// <summary>
        ///     Turns the age restriction of a text or forum channel on or off.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="enabled">on or off. Leave out to switch it.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageChannels)]
        public Task ChanNsfw(IGuildChannel channel, bool? enabled = null) =>
            EditAsync(channel, new ChannelEditRequest(Nsfw: enabled ?? channel switch
            {
                ITextChannel text => !text.IsNsfw,
                IForumChannel forum => !forum.IsNsfw,
                _ => true
            }));

        /// <summary>
        ///     Sets the bitrate of a voice channel in kbps.
        /// </summary>
        /// <param name="channel">The voice channel.</param>
        /// <param name="kbps">The bitrate in kbps.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageChannels)]
        public Task ChanBitrate(IVoiceChannel channel, int kbps) =>
            EditAsync(channel, new ChannelEditRequest(BitrateKbps: kbps));

        /// <summary>
        ///     Sets how many members can be in a voice channel. Zero removes the limit.
        /// </summary>
        /// <param name="channel">The voice channel.</param>
        /// <param name="limit">The user limit.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageChannels)]
        public Task ChanUserLimit(IVoiceChannel channel, int limit) =>
            EditAsync(channel, new ChannelEditRequest(UserLimit: limit));

        /// <summary>
        ///     Moves a channel into a category, or out of its category when none is given.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="category">The category.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageChannels)]
        public Task ChanCategory(IGuildChannel channel, ICategoryChannel? category = null) =>
            EditAsync(channel, new ChannelEditRequest(CategoryId: category?.Id, RemoveCategory: category is null));

        /// <summary>
        ///     Moves a channel to a position in the channel list, counting from 1.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="position">The new position.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageChannels)]
        public Task ChanPosition(IGuildChannel channel, int position) =>
            EditAsync(channel, new ChannelEditRequest(Position: position - 1));

        /// <summary>
        ///     Makes a channel's permissions match its category.
        /// </summary>
        /// <param name="channel">The channel.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageChannels)]
        public async Task ChanSync(IGuildChannel channel)
        {
            var guildId = ctx.Guild.Id;
            try
            {
                if (await Service.SyncPermissionsAsync(channel))
                    await ReplyConfirmAsync(Strings.ChanSyncDone(guildId, $"<#{channel.Id}>"));
                else
                    await ReplyErrorAsync(Strings.ChanSyncNoCategory(guildId, $"<#{channel.Id}>"));
            }
            catch (HttpException ex)
            {
                await ReplyErrorAsync(Strings.ChanEditFailed(guildId, ex.Reason ?? ex.Message));
            }
        }

        /// <summary>
        ///     Creates a copy of a channel with the same settings and permissions.
        /// </summary>
        /// <param name="channel">The channel to copy.</param>
        /// <param name="name">The name of the copy. Defaults to the same name.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        [BotPerm(GuildPermission.ManageChannels)]
        public async Task ChanClone(IGuildChannel channel, [Remainder] string? name = null)
        {
            var guildId = ctx.Guild.Id;
            try
            {
                var copy = await Service.CloneAsync(channel, name);
                if (copy is null)
                    await ReplyErrorAsync(Strings.ChanEditUnsupported(guildId, $"<#{channel.Id}>"));
                else
                    await ReplyConfirmAsync(Strings.ChanCloneDone(guildId, $"<#{channel.Id}>", $"<#{copy.Id}>"));
            }
            catch (HttpException ex)
            {
                await ReplyErrorAsync(Strings.ChanEditFailed(guildId, ex.Reason ?? ex.Message));
            }
        }

        private async Task EditAsync(IGuildChannel channel, ChannelEditRequest request)
        {
            var guildId = ctx.Guild.Id;
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
                    await ReplyConfirmAsync(Strings.ChanEditUpdated(guildId, mention));
                else
                    await ReplyErrorAsync(text);
            }
            catch (HttpException ex)
            {
                await ReplyErrorAsync(Strings.ChanEditFailed(guildId, ex.Reason ?? ex.Message));
            }
        }
    }
}
