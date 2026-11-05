using Discord.Commands;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Commands for backing up a server and restoring the backup into the same or another server.
    /// </summary>
    [Group]
    public class ServerBackupCommands : MewdekoSubmodule<ServerBackupService>
    {
        /// <summary>
        ///     Makes a backup of this server.
        /// </summary>
        /// <param name="options">
        ///     How many recent messages to save from each channel, then the channels to save them from.
        /// </param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task BackupCreate([Remainder] string? options = null)
        {
            var guildId = ctx.Guild.Id;
            if (!ServerBackupService.IsOwnerOrAdmin(ctx.Guild, (IGuildUser)ctx.User))
            {
                await ReplyErrorAsync(Strings.BackupNotAdmin(guildId));
                return;
            }

            var messageCount = 0;
            var rest = options?.Trim();
            if (!string.IsNullOrEmpty(rest))
            {
                var first = rest.Split(' ', 2)[0];
                if (int.TryParse(first, out var parsed) && first.Length < 6)
                {
                    messageCount = Math.Clamp(parsed, 0, ServerBackupService.MaxMessagesPerChannel);
                    rest = rest.Length > first.Length ? rest[first.Length..] : null;
                }
            }

            var channelIds = ServerBackupService.ParseIds(rest);
            if (messageCount > 0 && channelIds.Count == 0)
                channelIds.Add(ctx.Channel.Id);

            var status = await ReplyConfirmAsync(Strings.BackupCreating(guildId));
            var (error, backup, wait) = await Service.CreateAsync(ctx.Guild, ctx.User.Id, messageCount, channelIds);
            if (error != BackupError.None || backup is null)
            {
                await status.DeleteAsync();
                await ReplyErrorAsync(Service.ErrorText(guildId, error, wait: wait));
                return;
            }

            await status.DeleteAsync();
            await ReplyConfirmAsync(Strings.BackupCreated(guildId, backup.Id, backup.RoleCount, backup.ChannelCount,
                backup.EmojiCount, backup.MessageCount));
        }

        /// <summary>
        ///     Lists your backups.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task BackupList()
        {
            var guildId = ctx.Guild.Id;
            var backups = await Service.ListAsync(ctx.User.Id);
            if (backups.Count == 0)
            {
                await ReplyErrorAsync(Strings.BackupListEmpty(guildId));
                return;
            }

            var lines = backups.Select(x => Strings.BackupListLine(guildId, x.Id, x.SourceGuildName, x.RoleCount,
                x.ChannelCount, Timestamp(x.DateAdded)));
            await ctx.Channel.SendConfirmAsync(string.Join("\n", lines), Strings.BackupListTitle(guildId));
        }

        /// <summary>
        ///     Shows what a backup holds.
        /// </summary>
        /// <param name="id">The backup ID.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task BackupInfo(int id)
        {
            var guildId = ctx.Guild.Id;
            var backup = (await Service.ListAsync(ctx.User.Id)).FirstOrDefault(x => x.Id == id);
            if (backup is null)
            {
                await ReplyErrorAsync(Strings.BackupNotFound(guildId));
                return;
            }

            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.BackupInfoTitle(guildId, backup.Id))
                .AddField(Strings.BackupInfoServer(guildId), backup.SourceGuildName, true)
                .AddField(Strings.BackupInfoCreated(guildId), Timestamp(backup.DateAdded), true)
                .AddField(Strings.BackupInfoSize(guildId), backup.SizeBytes.Bytes().Humanize(), true)
                .AddField(Strings.BackupInfoContents(guildId), Strings.BackupInfoContentsValue(guildId,
                    backup.RoleCount, backup.ChannelCount, backup.EmojiCount, backup.MessageCount));
            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Deletes one of your backups.
        /// </summary>
        /// <param name="id">The backup ID.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task BackupDelete(int id)
        {
            var guildId = ctx.Guild.Id;
            if (!await Service.DeleteAsync(ctx.User.Id, id))
            {
                await ReplyErrorAsync(Strings.BackupNotFound(guildId));
                return;
            }

            await ReplyConfirmAsync(Strings.BackupDeleted(guildId, id));
        }

        /// <summary>
        ///     Restores one of your backups into this server.
        /// </summary>
        /// <param name="id">The backup ID.</param>
        /// <param name="mode">replace to replace everything, or add to only add what is missing.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task BackupRestore(int id, string mode = "replace")
        {
            var guildId = ctx.Guild.Id;
            BackupRestoreMode restoreMode;
            switch (mode.ToLowerInvariant())
            {
                case "replace":
                    restoreMode = BackupRestoreMode.Replace;
                    break;
                case "add":
                case "missing":
                    restoreMode = BackupRestoreMode.AddMissing;
                    break;
                default:
                    await ReplyErrorAsync(Strings.BackupUnknownMode(guildId));
                    return;
            }

            var backup = await Service.GetAsync(ctx.User.Id, id);
            if (backup is null)
            {
                await ReplyErrorAsync(Strings.BackupNotFound(guildId));
                return;
            }

            var data = ServerBackupService.ReadData(backup);
            if (data is null)
            {
                await ReplyErrorAsync(Service.ErrorText(guildId, BackupError.Corrupt));
                return;
            }

            var (error, detail) = await Service.CheckRestoreAsync(ctx.Guild, (IGuildUser)ctx.User, backup,
                data.Messages.Count > 0);
            if (error != BackupError.None)
            {
                await ReplyErrorAsync(Service.ErrorText(guildId, error, detail));
                return;
            }

            var prompt = restoreMode == BackupRestoreMode.Replace
                ? Strings.BackupConfirmReplace(guildId, id, backup.SourceGuildName)
                : Strings.BackupConfirmAdd(guildId, id, backup.SourceGuildName);
            if (!await PromptUserConfirmAsync(prompt, ctx.User.Id))
            {
                await ReplyErrorAsync(Strings.BackupCancelled(guildId));
                return;
            }

            await Service.RunRestoreAsync(ctx.Guild, ctx.User, ctx.Channel, data, restoreMode);
        }

        private static string Timestamp(DateTime utc)
        {
            return $"<t:{new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds()}:R>";
        }
    }
}
