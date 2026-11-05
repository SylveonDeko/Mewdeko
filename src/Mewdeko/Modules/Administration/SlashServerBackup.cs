using Discord.Interactions;
using Humanizer;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class SlashServerTools
{
    /// <summary>
    ///     Slash commands for backing up a server and restoring the backup into the same or another server.
    /// </summary>
    [Group("backup", "Back up this server and restore it here or in another server")]
    public class SlashServerBackup : MewdekoSlashModuleBase<ServerBackupService>
    {
        /// <summary>
        ///     Makes a backup of this server.
        /// </summary>
        /// <param name="messages">How many recent messages to save from each channel, up to 100.</param>
        /// <param name="channels">The channels to save messages from. Defaults to this channel.</param>
        [SlashCommand("create", "Make a backup of this server")]
        [RequireContext(ContextType.Guild)]
        public async Task Create(
            [Summary("messages", "How many recent messages to save from each channel")] [MinValue(0)] [MaxValue(100)]
            int messages = 0,
            [Summary("channels", "Channels to save messages from, as mentions or IDs")]
            string? channels = null)
        {
            var guildId = ctx.Guild.Id;
            await DeferAsync(true);
            if (!ServerBackupService.IsOwnerOrAdmin(ctx.Guild, (IGuildUser)ctx.User))
            {
                await EphemeralReplyErrorAsync(Strings.BackupNotAdmin(guildId));
                return;
            }

            var channelIds = ServerBackupService.ParseIds(channels);
            if (messages > 0 && channelIds.Count == 0)
                channelIds.Add(ctx.Channel.Id);

            var (error, backup, wait) = await Service.CreateAsync(ctx.Guild, ctx.User.Id, messages, channelIds);
            if (error != BackupError.None || backup is null)
            {
                await EphemeralReplyErrorAsync(Service.ErrorText(guildId, error, wait: wait));
                return;
            }

            await EphemeralReplyConfirmAsync(Strings.BackupCreated(guildId, backup.Id, backup.RoleCount,
                backup.ChannelCount, backup.EmojiCount, backup.MessageCount));
        }

        /// <summary>
        ///     Lists your backups.
        /// </summary>
        [SlashCommand("list", "List your backups")]
        [RequireContext(ContextType.Guild)]
        public async Task List()
        {
            var guildId = ctx.Guild.Id;
            var backups = await Service.ListAsync(ctx.User.Id);
            if (backups.Count == 0)
            {
                await EphemeralReplyErrorAsync(Strings.BackupListEmpty(guildId));
                return;
            }

            var lines = backups.Select(x => Strings.BackupListLine(guildId, x.Id, x.SourceGuildName, x.RoleCount,
                x.ChannelCount, Timestamp(x.DateAdded)));
            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.BackupListTitle(guildId))
                .WithDescription(string.Join("\n", lines));
            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }

        /// <summary>
        ///     Shows what a backup holds.
        /// </summary>
        /// <param name="id">The backup ID.</param>
        [SlashCommand("info", "Show what a backup holds")]
        [RequireContext(ContextType.Guild)]
        public async Task Info([Summary("id", "The backup ID")] int id)
        {
            var guildId = ctx.Guild.Id;
            var backup = (await Service.ListAsync(ctx.User.Id)).FirstOrDefault(x => x.Id == id);
            if (backup is null)
            {
                await EphemeralReplyErrorAsync(Strings.BackupNotFound(guildId));
                return;
            }

            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.BackupInfoTitle(guildId, backup.Id))
                .AddField(Strings.BackupInfoServer(guildId), backup.SourceGuildName, true)
                .AddField(Strings.BackupInfoCreated(guildId), Timestamp(backup.DateAdded), true)
                .AddField(Strings.BackupInfoSize(guildId), backup.SizeBytes.Bytes().Humanize(), true)
                .AddField(Strings.BackupInfoContents(guildId), Strings.BackupInfoContentsValue(guildId,
                    backup.RoleCount, backup.ChannelCount, backup.EmojiCount, backup.MessageCount));
            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }

        /// <summary>
        ///     Deletes one of your backups.
        /// </summary>
        /// <param name="id">The backup ID.</param>
        [SlashCommand("delete", "Delete one of your backups")]
        [RequireContext(ContextType.Guild)]
        public async Task Delete([Summary("id", "The backup ID")] int id)
        {
            var guildId = ctx.Guild.Id;
            if (!await Service.DeleteAsync(ctx.User.Id, id))
            {
                await EphemeralReplyErrorAsync(Strings.BackupNotFound(guildId));
                return;
            }

            await EphemeralReplyConfirmAsync(Strings.BackupDeleted(guildId, id));
        }

        /// <summary>
        ///     Restores one of your backups into this server.
        /// </summary>
        /// <param name="id">The backup ID.</param>
        /// <param name="mode">Whether to replace everything or only add what is missing.</param>
        [SlashCommand("restore", "Restore one of your backups into this server")]
        [RequireContext(ContextType.Guild)]
        public async Task Restore([Summary("id", "The backup ID")] int id,
            [Summary("mode", "Replace everything, or only add what is missing")]
            BackupRestoreMode mode = BackupRestoreMode.Replace)
        {
            var guildId = ctx.Guild.Id;
            await DeferAsync(true);

            var backup = await Service.GetAsync(ctx.User.Id, id);
            if (backup is null)
            {
                await EphemeralReplyErrorAsync(Strings.BackupNotFound(guildId));
                return;
            }

            var data = ServerBackupService.ReadData(backup);
            if (data is null)
            {
                await EphemeralReplyErrorAsync(Service.ErrorText(guildId, BackupError.Corrupt));
                return;
            }

            var (error, detail) = await Service.CheckRestoreAsync(ctx.Guild, (IGuildUser)ctx.User, backup,
                data.Messages.Count > 0);
            if (error != BackupError.None)
            {
                await EphemeralReplyErrorAsync(Service.ErrorText(guildId, error, detail));
                return;
            }

            var prompt = mode == BackupRestoreMode.Replace
                ? Strings.BackupConfirmReplace(guildId, id, backup.SourceGuildName)
                : Strings.BackupConfirmAdd(guildId, id, backup.SourceGuildName);
            if (!await PromptUserConfirmAsync(prompt, ctx.User.Id, true))
            {
                await EphemeralReplyErrorAsync(Strings.BackupCancelled(guildId));
                return;
            }

            await Service.RunRestoreAsync(ctx.Guild, ctx.User, ctx.Channel, data, mode);
        }

        private static string Timestamp(DateTime utc)
        {
            return $"<t:{new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds()}:R>";
        }
    }
}
