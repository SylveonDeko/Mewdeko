using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using DataModel;
using Discord.Net;
using Discord.Webhook;
using Humanizer;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Creates, stores and restores backups of a server's settings, roles, channels, emojis and messages.
/// </summary>
public class ServerBackupService : INService
{
    /// <summary>
    ///     The most backups one user can keep.
    /// </summary>
    public const int MaxBackupsPerUser = 5;

    /// <summary>
    ///     The most messages saved from one channel.
    /// </summary>
    public const int MaxMessagesPerChannel = 100;

    /// <summary>
    ///     The most channels messages can be saved from.
    /// </summary>
    public const int MaxMessageChannels = 10;

    /// <summary>
    ///     How long a user has to wait between creating backups.
    /// </summary>
    public static readonly TimeSpan CreateCooldown = TimeSpan.FromMinutes(30);

    private const int MaxCompressedBytes = 25 * 1024 * 1024;
    private const int RequestDelayMs = 300;

    private static readonly Regex SnowflakePattern = new(@"\d{15,21}", RegexOptions.Compiled);

    private static readonly Regex WebhookName = new("discord|clyde", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly IHttpClientFactory httpFactory;
    private readonly ILogger<ServerBackupService> logger;
    private readonly ConcurrentDictionary<ulong, byte> running = new();
    private readonly GeneratedBotStrings strings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ServerBackupService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory.</param>
    /// <param name="httpFactory">The HTTP client factory, for downloading images.</param>
    /// <param name="strings">The generated bot strings.</param>
    /// <param name="logger">The logger instance.</param>
    public ServerBackupService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        IHttpClientFactory httpFactory, GeneratedBotStrings strings, ILogger<ServerBackupService> logger)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.httpFactory = httpFactory;
        this.strings = strings;
        this.logger = logger;
    }

    /// <summary>
    ///     Checks whether a member owns the server or has the administrator permission.
    /// </summary>
    /// <param name="guild">The server.</param>
    /// <param name="user">The member.</param>
    /// <returns>True if the member is the owner or an administrator.</returns>
    public static bool IsOwnerOrAdmin(IGuild guild, IGuildUser user)
    {
        return guild.OwnerId == user.Id || user.GuildPermissions.Administrator;
    }

    /// <summary>
    ///     Lists a user's backups without their data.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The backups, newest first.</returns>
    public async Task<List<ServerBackup>> ListAsync(ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ServerBackups
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.DateAdded)
            .Select(x => new ServerBackup
            {
                Id = x.Id,
                UserId = x.UserId,
                SourceGuildId = x.SourceGuildId,
                SourceGuildName = x.SourceGuildName,
                SizeBytes = x.SizeBytes,
                RoleCount = x.RoleCount,
                ChannelCount = x.ChannelCount,
                EmojiCount = x.EmojiCount,
                MessageCount = x.MessageCount,
                DateAdded = x.DateAdded
            })
            .ToListAsync();
    }

    /// <summary>
    ///     Gets one of a user's backups including its data.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="id">The backup ID.</param>
    /// <returns>The backup, or null if it does not exist or belongs to someone else.</returns>
    public async Task<ServerBackup?> GetAsync(ulong userId, int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ServerBackups.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
    }

    /// <summary>
    ///     Deletes one of a user's backups.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="id">The backup ID.</param>
    /// <returns>True if a backup was deleted.</returns>
    public async Task<bool> DeleteAsync(ulong userId, int id)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ServerBackups.Where(x => x.Id == id && x.UserId == userId).DeleteAsync() > 0;
    }

    /// <summary>
    ///     Reads the data out of a backup.
    /// </summary>
    /// <param name="backup">The backup row.</param>
    /// <returns>The data, or null if it could not be read.</returns>
    public static ServerBackupData? ReadData(ServerBackup backup)
    {
        return ReadData(backup.Data);
    }

    /// <summary>
    ///     Reads backup data from its compressed form.
    /// </summary>
    /// <param name="compressed">The gzip compressed JSON.</param>
    /// <returns>The data, or null if it could not be read.</returns>
    public static ServerBackupData? ReadData(byte[] compressed)
    {
        try
        {
            using var input = new MemoryStream(compressed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            return JsonSerializer.Deserialize<ServerBackupData>(gzip);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    ///     Takes a backup of a server.
    /// </summary>
    /// <param name="guild">The server to back up.</param>
    /// <param name="userId">The user the backup belongs to.</param>
    /// <param name="messageCount">How many recent messages to save from each chosen channel.</param>
    /// <param name="messageChannelIds">The channels to save messages from.</param>
    /// <returns>The error, if any, the new backup, and how long to wait when on cooldown.</returns>
    public async Task<(BackupError Error, ServerBackup? Backup, TimeSpan Wait)> CreateAsync(IGuild guild,
        ulong userId, int messageCount, IReadOnlyCollection<ulong> messageChannelIds)
    {
        var existing = await ListAsync(userId);
        if (existing.Count >= MaxBackupsPerUser)
            return (BackupError.LimitReached, null, TimeSpan.Zero);

        var last = existing.Count == 0 ? (DateTime?)null : existing.Max(x => x.DateAdded);
        if (last is { } lastAt && DateTime.UtcNow - lastAt < CreateCooldown)
            return (BackupError.Cooldown, null, CreateCooldown - (DateTime.UtcNow - lastAt));

        var data = new ServerBackupData();
        using var http = httpFactory.CreateClient();

        data.Settings = new BackupSettings
        {
            Name = guild.Name,
            Icon = await DownloadAsync(http, guild.IconUrl),
            VerificationLevel = (int)guild.VerificationLevel,
            DefaultMessageNotifications = (int)guild.DefaultMessageNotifications,
            ExplicitContentFilter = (int)guild.ExplicitContentFilter,
            AfkTimeout = guild.AFKTimeout,
            AfkChannelId = guild.AFKChannelId,
            SystemChannelId = guild.SystemChannelId,
            SystemChannelFlags = (int)guild.SystemChannelFlags
        };

        foreach (var role in guild.Roles.Where(x => !x.IsManaged).OrderBy(x => x.Position))
        {
            data.Roles.Add(CaptureRole(guild, role,
                role.Id == guild.Id ? null : await DownloadAsync(http, role.GetIconUrl())));
        }

        var channels = (await guild.GetChannelsAsync()).Where(x => x is not IThreadChannel)
            .OrderBy(x => x.Position).ToList();
        foreach (var channel in channels)
        {
            if (CaptureChannel(channel) is { } entry)
                data.Channels.Add(entry);
        }

        var gate = new SemaphoreSlim(6);
        var emojiTasks = guild.Emotes.Select(async emote =>
        {
            await gate.WaitAsync();
            try
            {
                var image = await DownloadAsync(http, emote.Url);
                return image is null
                    ? null
                    : new BackupEmoji
                    {
                        Name = emote.Name, Animated = emote.Animated, Image = image
                    };
            }
            finally
            {
                gate.Release();
            }
        }).ToList();
        data.Emojis = (await Task.WhenAll(emojiTasks)).Where(x => x is not null).Select(x => x!).ToList();

        if (messageCount > 0)
        {
            var wanted = messageChannelIds.Take(MaxMessageChannels).ToHashSet();
            foreach (var text in channels.OfType<ITextChannel>().Where(x => wanted.Contains(x.Id)))
            {
                try
                {
                    var fetched = await text.GetMessagesAsync(Math.Min(messageCount, MaxMessagesPerChannel))
                        .FlattenAsync();
                    var saved = new BackupChannelMessages
                    {
                        ChannelId = text.Id
                    };
                    foreach (var message in fetched.OrderBy(x => x.Timestamp))
                    {
                        if (message.Type is not (MessageType.Default or MessageType.Reply))
                            continue;

                        var content = message.Content;
                        foreach (var attachment in message.Attachments)
                            content += $"\n{attachment.Url}";
                        content = content.Trim();
                        if (content.Length == 0)
                            continue;

                        saved.Messages.Add(new BackupMessage
                        {
                            Author = (message.Author as IGuildUser)?.DisplayName ?? message.Author.GlobalName ??
                                message.Author.Username,
                            AvatarUrl = message.Author.GetAvatarUrl() ?? message.Author.GetDefaultAvatarUrl(),
                            Content = content.Length > 2000 ? content[..2000] : content
                        });
                    }

                    if (saved.Messages.Count > 0)
                        data.Messages.Add(saved);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not read messages from {Channel} for a backup", text.Id);
                }
            }
        }

        var compressed = await CompressAsync(data);

        if (compressed.Length > MaxCompressedBytes)
            return (BackupError.TooLarge, null, TimeSpan.Zero);

        var backup = new ServerBackup
        {
            UserId = userId,
            SourceGuildId = guild.Id,
            SourceGuildName = guild.Name,
            Data = compressed,
            SizeBytes = compressed.Length,
            RoleCount = data.Roles.Count,
            ChannelCount = data.Channels.Count,
            EmojiCount = data.Emojis.Count,
            MessageCount = data.Messages.Sum(x => x.Messages.Count),
            DateAdded = DateTime.UtcNow
        };

        await using var db = await dbFactory.CreateConnectionAsync();
        backup.Id = await db.InsertWithInt32IdentityAsync(backup);
        return (BackupError.None, backup, TimeSpan.Zero);
    }

    /// <summary>
    ///     Compresses backup data into the form stored in the database.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns>The gzip compressed JSON.</returns>
    public static async Task<byte[]> CompressAsync(ServerBackupData data)
    {
        using var output = new MemoryStream();
        await using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true))
            await JsonSerializer.SerializeAsync(gzip, data);
        return output.ToArray();
    }

    /// <summary>
    ///     Reads a role into its backup form.
    /// </summary>
    /// <param name="guild">The server the role belongs to.</param>
    /// <param name="role">The role.</param>
    /// <param name="icon">The role icon image, if it should be kept.</param>
    /// <returns>The role.</returns>
    public static BackupRole CaptureRole(IGuild guild, IRole role, byte[]? icon)
    {
        return new BackupRole
        {
            Id = role.Id,
            IsEveryone = role.Id == guild.Id,
            Name = role.Name,
            Color = role.Color.RawValue,
            Permissions = role.Permissions.RawValue,
            Hoist = role.IsHoisted,
            Mentionable = role.IsMentionable,
            Position = role.Position,
            Icon = icon,
            UnicodeEmoji = role.Emoji?.Name
        };
    }

    /// <summary>
    ///     Reads a category or channel into its backup form.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns>The channel, or null if it is not a kind that is saved.</returns>
    public static BackupChannel? CaptureChannel(IGuildChannel channel)
    {
        var type = ChannelKind(channel);
        if (type is null)
            return null;

        var entry = new BackupChannel
        {
            Id = channel.Id,
            Type = type,
            Name = channel.Name,
            Position = channel.Position,
            Overwrites = channel.PermissionOverwrites.Select(x => new BackupOverwrite
            {
                IsRole = x.TargetType == PermissionTarget.Role,
                TargetId = x.TargetId,
                Allow = x.Permissions.AllowValue,
                Deny = x.Permissions.DenyValue
            }).ToList()
        };

        switch (channel)
        {
            case IStageChannel stage:
                entry.Topic = stage.Topic;
                entry.Bitrate = stage.Bitrate;
                entry.ParentId = stage.CategoryId;
                break;
            case IVoiceChannel voice:
                entry.Bitrate = voice.Bitrate;
                entry.UserLimit = voice.UserLimit;
                entry.ParentId = voice.CategoryId;
                break;
            case ITextChannel text:
                entry.Topic = text.Topic;
                entry.SlowMode = text.SlowModeInterval;
                entry.Nsfw = text.IsNsfw;
                entry.ParentId = text.CategoryId;
                break;
            case IForumChannel forum:
                entry.Topic = forum.Topic;
                entry.Nsfw = forum.IsNsfw;
                entry.ParentId = forum.CategoryId;
                break;
        }

        return entry;
    }

    /// <summary>
    ///     Creates a category or channel from its backup form.
    /// </summary>
    /// <param name="guild">The server to create it in.</param>
    /// <param name="backed">The channel to create.</param>
    /// <param name="parent">The ID of the category to put it in, or null.</param>
    /// <param name="overwrites">The permission overwrites to give it.</param>
    /// <returns>The new channel.</returns>
    public static async Task<IGuildChannel> CreateChannelAsync(IGuild guild, BackupChannel backed, ulong? parent,
        List<Overwrite> overwrites)
    {
        switch (backed.Type)
        {
            case "category":
                return await guild.CreateCategoryAsync(backed.Name, x => x.PermissionOverwrites = overwrites);
            case "voice":
                return await guild.CreateVoiceChannelAsync(backed.Name, x =>
                {
                    x.Bitrate = Math.Clamp(backed.Bitrate, 8000, guild.MaxBitrate);
                    x.UserLimit = backed.UserLimit;
                    x.CategoryId = parent;
                    x.PermissionOverwrites = overwrites;
                });
            case "stage":
                return await guild.CreateStageChannelAsync(backed.Name, x =>
                {
                    x.Bitrate = Math.Clamp(backed.Bitrate, 8000, guild.MaxBitrate);
                    x.CategoryId = parent;
                    x.PermissionOverwrites = overwrites;
                });
            case "forum":
                return await guild.CreateForumChannelAsync(backed.Name, x =>
                {
                    x.Topic = backed.Topic;
                    x.IsNsfw = backed.Nsfw;
                    x.CategoryId = parent;
                    x.PermissionOverwrites = overwrites;
                });
            default:
                return await guild.CreateTextChannelAsync(backed.Name, x =>
                {
                    x.Topic = backed.Topic;
                    x.SlowModeInterval = backed.SlowMode;
                    x.IsNsfw = backed.Nsfw;
                    x.CategoryId = parent;
                    x.PermissionOverwrites = overwrites;
                });
        }
    }

    /// <summary>
    ///     Checks whether a backup can be restored into a server by a member.
    /// </summary>

    /// <param name="guild">The server to restore into.</param>
    /// <param name="user">The member running the restore.</param>
    /// <param name="backup">The backup.</param>
    /// <param name="hasMessages">Whether the backup holds messages that need webhooks.</param>
    /// <returns>The error, if any, and the permissions the bot is missing.</returns>
    public async Task<(BackupError Error, string Detail)> CheckRestoreAsync(IGuild guild, IGuildUser user,
        ServerBackup backup, bool hasMessages)
    {
        if (running.ContainsKey(guild.Id))
            return (BackupError.RestoreRunning, "");

        if (!IsOwnerOrAdmin(guild, user))
            return (BackupError.NotAdmin, "");

        if (backup.SourceGuildId != guild.Id && client.GetGuild(backup.SourceGuildId) is { } source)
        {
            var inSource = await ((IGuild)source).GetUserAsync(user.Id);
            if (inSource is null || !IsOwnerOrAdmin(source, inSource))
                return (BackupError.NotAdminInSource, source.Name);
        }

        var me = await guild.GetCurrentUserAsync();
        if (!me.GuildPermissions.Administrator)
        {
            var needed = new List<GuildPermission>
            {
                GuildPermission.ManageRoles,
                GuildPermission.ManageChannels,
                GuildPermission.ManageGuild,
                GuildPermission.ManageEmojisAndStickers
            };
            if (hasMessages)
                needed.Add(GuildPermission.ManageWebhooks);

            var missing = needed.Where(x => !me.GuildPermissions.Has(x)).ToList();
            if (missing.Count > 0)
                return (BackupError.BotMissingPermissions, string.Join(", ", missing));
        }

        if (me.Hierarchy < 2)
            return (BackupError.BotRoleTooLow, "");

        return (BackupError.None, "");
    }

    /// <summary>
    ///     Pulls the IDs out of text such as channel mentions.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The IDs found.</returns>
    public static List<ulong> ParseIds(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? []
            : SnowflakePattern.Matches(text).Select(x => ulong.Parse(x.Value)).Distinct().ToList();
    }

    /// <summary>
    ///     Gets the message that explains why a backup could not be created or restored.
    /// </summary>
    /// <param name="guildId">The server ID, for the language.</param>
    /// <param name="error">The error.</param>
    /// <param name="detail">Extra detail from the check, such as the missing permissions.</param>
    /// <param name="wait">How long to wait when on cooldown.</param>
    /// <returns>The message.</returns>
    public string ErrorText(ulong guildId, BackupError error, string detail = "", TimeSpan wait = default)
    {
        return error switch
        {
            BackupError.LimitReached => strings.BackupLimit(guildId, MaxBackupsPerUser),
            BackupError.Cooldown => strings.BackupCooldown(guildId, wait.Humanize(2)),
            BackupError.TooLarge => strings.BackupTooLarge(guildId),
            BackupError.NotFound => strings.BackupNotFound(guildId),
            BackupError.NotAdmin => strings.BackupNotAdmin(guildId),
            BackupError.NotAdminInSource => strings.BackupNotAdminSource(guildId, detail),
            BackupError.BotMissingPermissions => strings.BackupBotPerms(guildId, detail),
            BackupError.BotRoleTooLow => strings.BackupBotRole(guildId),
            BackupError.RestoreRunning => strings.BackupRestoreRunning(guildId),
            _ => strings.BackupCorrupt(guildId)
        };
    }

    /// <summary>
    ///     Builds the embed that shows how far a restore has got.
    /// </summary>
    /// <param name="guildId">The server ID, for the language.</param>
    /// <param name="progress">The progress.</param>
    /// <param name="finished">Whether the restore is over.</param>
    /// <returns>The embed.</returns>
    public EmbedBuilder ProgressEmbed(ulong guildId, BackupRestoreProgress progress, bool finished)
    {
        var embed = new EmbedBuilder().WithOkColor(guildId);
        if (finished)
        {
            embed.WithTitle(strings.BackupDoneTitle(guildId))
                .WithDescription(strings.BackupDoneSummary(guildId, progress.RolesCreated, progress.ChannelsCreated,
                    progress.EmojisCreated, progress.MessagesRestored));
        }
        else
        {
            embed.WithDescription(strings.BackupProgress(guildId, progress.Step, progress.RolesCreated,
                progress.ChannelsCreated, progress.EmojisCreated, progress.MessagesRestored));
        }

        var failures = progress.Failures.ToList();
        if (failures.Count > 0)
        {
            var text = string.Join("\n", failures.Take(10));
            if (failures.Count > 10)
                text += "\n" + strings.BackupFailuresMore(guildId, failures.Count - 10);
            embed.AddField(strings.BackupFailures(guildId), text);
        }

        return embed;
    }

    /// <summary>
    ///     Runs a restore, keeps a status message up to date and delivers the final report. The report goes to the
    ///     status message, or to the user's DMs or a restored channel if the status message is gone.
    /// </summary>
    /// <param name="guild">The server to restore into.</param>
    /// <param name="user">The person who ran the restore.</param>
    /// <param name="channel">The channel the command was run in.</param>
    /// <param name="data">The backup data.</param>
    /// <param name="mode">Whether to replace everything or only add what is missing.</param>
    /// <returns>A task that completes when the restore is over.</returns>
    public async Task RunRestoreAsync(IGuild guild, IUser user, IMessageChannel channel, ServerBackupData data,
        BackupRestoreMode mode)
    {
        var progress = new BackupRestoreProgress();
        var status = await channel.SendMessageAsync(embed: ProgressEmbed(guild.Id, progress, false).Build());
        var restore = Task.Run(() => RestoreAsync(guild, data, mode, channel.Id, progress));

        while (!restore.IsCompleted)
        {
            await Task.WhenAny(restore, Task.Delay(4000));
            if (restore.IsCompleted)
                break;

            try
            {
                var embed = ProgressEmbed(guild.Id, progress, false).Build();
                await status.ModifyAsync(x => x.Embed = embed);
            }
            catch (Exception)
            {
            }
        }

        ulong? reportChannel = null;
        try
        {
            reportChannel = await restore;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backup restore failed in {Guild}", guild.Id);
            progress.Failures.Add(ex.Message);
        }

        var final = ProgressEmbed(guild.Id, progress, true).Build();
        try
        {
            await status.ModifyAsync(x => x.Embed = final);
            return;
        }
        catch (Exception)
        {
        }

        try
        {
            await user.SendMessageAsync(embed: final);
            return;
        }
        catch (Exception)
        {
        }

        if (reportChannel is { } id && await guild.GetTextChannelAsync(id) is { } text)
        {
            try
            {
                await text.SendMessageAsync(user.Mention, embed: final);
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>
    ///     Restores a backup into a server. Check it with <see cref="CheckRestoreAsync" /> first.
    /// </summary>
    /// <param name="guild">The server to restore into.</param>
    /// <param name="data">The backup data.</param>
    /// <param name="mode">Whether to replace everything or only add what is missing.</param>
    /// <param name="keepChannelId">The channel the command was run in, kept until the end.</param>
    /// <param name="progress">The progress object, updated as the restore goes.</param>
    /// <returns>The ID of a text channel to report in once the command channel is gone, or null.</returns>
    public async Task<ulong?> RestoreAsync(IGuild guild, ServerBackupData data, BackupRestoreMode mode,
        ulong keepChannelId, BackupRestoreProgress progress)
    {
        if (!running.TryAdd(guild.Id, 0))
            return null;

        var guildId = guild.Id;
        ulong? reportChannel = null;
        try
        {
            var me = await guild.GetCurrentUserAsync();
            var botAdmin = me.GuildPermissions.Administrator;
            var replace = mode == BackupRestoreMode.Replace;

            if (replace)
            {
                progress.Step = strings.BackupStepClearing(guildId);
                await ClearAsync(guild, me, keepChannelId, progress);
            }

            progress.Step = strings.BackupStepRoles(guildId);
            var roleMap = await RestoreRolesAsync(guild, me, botAdmin, data, replace, progress);

            progress.Step = strings.BackupStepChannels(guildId);
            var (channelMap, created) = await RestoreChannelsAsync(guild, data, roleMap, replace, progress);

            progress.Step = strings.BackupStepEmojis(guildId);
            await RestoreEmojisAsync(guild, data, replace, progress);

            if (replace)
            {
                progress.Step = strings.BackupStepSettings(guildId);
                await RestoreSettingsAsync(guild, data.Settings, channelMap, progress);
            }

            if (data.Messages.Count > 0)
            {
                progress.Step = strings.BackupStepMessages(guildId);
                await RestoreMessagesAsync(guild, data, channelMap, created, progress);
            }

            reportChannel = data.Channels
                .Where(x => x.Type == "text" && created.Contains(x.Id) && channelMap.ContainsKey(x.Id))
                .Select(x => (ulong?)channelMap[x.Id]).FirstOrDefault();

            if (replace && keepChannelId != 0 && !channelMap.Values.Contains(keepChannelId))
            {
                try
                {
                    var keep = await guild.GetChannelAsync(keepChannelId);
                    if (keep is not null)
                        await keep.DeleteAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not delete the command channel after a restore");
                }
            }

            progress.Step = strings.BackupStepDone(guildId);
            return reportChannel;
        }
        finally
        {
            running.TryRemove(guild.Id, out _);
        }
    }

    private async Task ClearAsync(IGuild guild, IGuildUser me, ulong keepChannelId, BackupRestoreProgress progress)
    {
        var guildId = guild.Id;

        foreach (var emote in guild.Emotes.ToList())
        {
            try
            {
                await guild.DeleteEmoteAsync(emote);
                await Task.Delay(RequestDelayMs);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Backup restore could not delete emoji {Emoji}", emote.Id);
                progress.Failures.Add(strings.BackupFailDeleteEmoji(guildId, emote.Name));
            }
        }

        foreach (var channel in (await guild.GetChannelsAsync()).Where(x => x.Id != keepChannelId)
                     .OrderBy(x => x is ICategoryChannel ? 1 : 0).ToList())
        {
            try
            {
                await channel.DeleteAsync();
                await Task.Delay(RequestDelayMs);
            }
            catch (HttpException ex) when (ex.HttpCode == System.Net.HttpStatusCode.NotFound)
            {
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Backup restore could not delete channel {Channel}", channel.Id);
                progress.Failures.Add(strings.BackupFailDeleteChannel(guildId, channel.Name));
            }
        }

        foreach (var role in guild.Roles.Where(x => x.Id != guild.Id && !x.IsManaged && x.Position < me.Hierarchy)
                     .ToList())
        {
            try
            {
                await role.DeleteAsync();
                await Task.Delay(RequestDelayMs);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Backup restore could not delete role {Role}", role.Id);
                progress.Failures.Add(strings.BackupFailDeleteRole(guildId, role.Name));
            }
        }
    }

    private async Task<Dictionary<ulong, ulong>> RestoreRolesAsync(IGuild guild, IGuildUser me, bool botAdmin,
        ServerBackupData data, bool replace, BackupRestoreProgress progress)
    {
        var guildId = guild.Id;
        var map = new Dictionary<ulong, ulong>();
        var botPerms = me.GuildPermissions.RawValue;

        var everyone = data.Roles.FirstOrDefault(x => x.IsEveryone);
        if (everyone is not null)
        {
            map[everyone.Id] = guild.Id;
            if (replace)
            {
                try
                {
                    var raw = botAdmin ? everyone.Permissions : everyone.Permissions & botPerms;
                    await guild.EveryoneRole.ModifyAsync(x => x.Permissions = new GuildPermissions(raw));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Backup restore could not edit @everyone");
                    progress.Failures.Add(strings.BackupFailRole(guildId, "@everyone"));
                }
            }
        }

        var existing = guild.Roles.Where(x => !x.IsManaged).ToList();
        foreach (var backed in data.Roles.Where(x => !x.IsEveryone).OrderByDescending(x => x.Position))
        {
            if (!replace)
            {
                var match = existing.FirstOrDefault(x => x.Name.Equals(backed.Name, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    map[backed.Id] = match.Id;
                    continue;
                }
            }

            try
            {
                var raw = backed.Permissions;
                if (!botAdmin && (raw & ~botPerms) != 0)
                {
                    raw &= botPerms;
                    progress.Failures.Add(strings.BackupFailRolePerms(guildId, backed.Name));
                }

                var role = await guild.CreateRoleAsync(backed.Name, new GuildPermissions(raw), new Color(backed.Color),
                    backed.Hoist, backed.Mentionable);
                map[backed.Id] = role.Id;
                progress.RolesCreated++;

                if (backed.Icon is not null || backed.UnicodeEmoji is not null)
                {
                    try
                    {
                        await role.ModifyAsync(x =>
                        {
                            if (backed.Icon is not null)
                                x.Icon = new Discord.Image(new MemoryStream(backed.Icon));
                            else if (backed.UnicodeEmoji is not null)
                                x.Emoji = new Emoji(backed.UnicodeEmoji);
                        });
                    }
                    catch (Exception)
                    {
                        progress.Failures.Add(strings.BackupFailRoleIcon(guildId, backed.Name));
                    }
                }

                await Task.Delay(RequestDelayMs);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Backup restore could not create role {Role}", backed.Name);
                progress.Failures.Add(strings.BackupFailRole(guildId, backed.Name));
            }
        }

        return map;
    }

    private async Task<(Dictionary<ulong, ulong> Map, HashSet<ulong> Created)> RestoreChannelsAsync(IGuild guild,
        ServerBackupData data, Dictionary<ulong, ulong> roleMap, bool replace, BackupRestoreProgress progress)
    {
        var guildId = guild.Id;
        var map = new Dictionary<ulong, ulong>();
        var created = new HashSet<ulong>();
        var existing = replace ? [] : (await guild.GetChannelsAsync()).Where(x => x is not IThreadChannel).ToList();

        var ordered = data.Channels.OrderBy(x => x.Type == "category" ? 0 : 1).ThenBy(x => x.Position).ToList();
        foreach (var backed in ordered)
        {
            if (!replace)
            {
                var match = existing.FirstOrDefault(x =>
                    ChannelKind(x) == backed.Type && x.Name.Equals(backed.Name, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    map[backed.Id] = match.Id;
                    continue;
                }
            }

            try
            {
                var overwrites = await MapOverwritesAsync(guild, backed.Overwrites, roleMap);
                ulong? parent = backed.ParentId is { } p && map.TryGetValue(p, out var mapped) ? mapped : null;
                var channel = await CreateChannelAsync(guild, backed, parent, overwrites);

                map[backed.Id] = channel.Id;
                created.Add(backed.Id);
                progress.ChannelsCreated++;
                await Task.Delay(RequestDelayMs);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Backup restore could not create channel {Channel}", backed.Name);
                progress.Failures.Add(strings.BackupFailChannel(guildId, backed.Name));
            }
        }

        return (map, created);
    }

    private static async Task<List<Overwrite>> MapOverwritesAsync(IGuild guild, List<BackupOverwrite> overwrites,
        Dictionary<ulong, ulong> roleMap)
    {
        var result = new List<Overwrite>();
        foreach (var overwrite in overwrites)
        {
            if (overwrite.IsRole)
            {
                if (roleMap.TryGetValue(overwrite.TargetId, out var role))
                {
                    result.Add(new Overwrite(role, PermissionTarget.Role,
                        new OverwritePermissions(overwrite.Allow, overwrite.Deny)));
                }
            }
            else if (await guild.GetUserAsync(overwrite.TargetId) is not null)
            {
                result.Add(new Overwrite(overwrite.TargetId, PermissionTarget.User,
                    new OverwritePermissions(overwrite.Allow, overwrite.Deny)));
            }
        }

        return result;
    }

    private async Task RestoreEmojisAsync(IGuild guild, ServerBackupData data, bool replace,
        BackupRestoreProgress progress)
    {
        var guildId = guild.Id;
        var existing = replace
            ? []
            : guild.Emotes.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var full = new HashSet<bool>();

        foreach (var emoji in data.Emojis)
        {
            if (existing.Contains(emoji.Name))
                continue;

            if (full.Contains(emoji.Animated))
                continue;

            try
            {
                await guild.CreateEmoteAsync(emoji.Name, new Discord.Image(new MemoryStream(emoji.Image)));
                progress.EmojisCreated++;
                await Task.Delay(RequestDelayMs);
            }
            catch (HttpException ex) when (ex.DiscordCode is { } code && (int)code is 30008 or 30018)
            {
                full.Add(emoji.Animated);
                progress.Failures.Add(strings.BackupFailEmojiSlots(guildId));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Backup restore could not create emoji {Emoji}", emoji.Name);
                progress.Failures.Add(strings.BackupFailEmoji(guildId, emoji.Name));
            }
        }
    }

    private async Task RestoreSettingsAsync(IGuild guild, BackupSettings settings, Dictionary<ulong, ulong> channelMap,
        BackupRestoreProgress progress)
    {
        try
        {
            ITextChannel? system = null;
            IVoiceChannel? afk = null;
            if (settings.SystemChannelId is { } systemId && channelMap.TryGetValue(systemId, out var newSystem))
                system = await guild.GetTextChannelAsync(newSystem);
            if (settings.AfkChannelId is { } afkId && channelMap.TryGetValue(afkId, out var newAfk))
                afk = await guild.GetVoiceChannelAsync(newAfk);

            await guild.ModifyAsync(x =>
            {
                x.Name = settings.Name;
                if (settings.Icon is not null)
                    x.Icon = new Discord.Image(new MemoryStream(settings.Icon));
                x.VerificationLevel = (VerificationLevel)settings.VerificationLevel;
                x.DefaultMessageNotifications = (DefaultMessageNotifications)settings.DefaultMessageNotifications;
                x.ExplicitContentFilter = (ExplicitContentFilterLevel)settings.ExplicitContentFilter;
                x.AfkTimeout = settings.AfkTimeout;
                x.SystemChannelFlags = (SystemChannelMessageDeny)settings.SystemChannelFlags;
                if (system is not null)
                    x.SystemChannel = new Optional<ITextChannel>(system);
                if (afk is not null)
                    x.AfkChannel = new Optional<IVoiceChannel>(afk);
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Backup restore could not edit the server settings");
            progress.Failures.Add(strings.BackupFailSettings(guild.Id));
        }
    }

    private async Task RestoreMessagesAsync(IGuild guild, ServerBackupData data, Dictionary<ulong, ulong> channelMap,
        HashSet<ulong> created, BackupRestoreProgress progress)
    {
        var guildId = guild.Id;
        foreach (var saved in data.Messages)
        {
            if (!created.Contains(saved.ChannelId) || !channelMap.TryGetValue(saved.ChannelId, out var newId))
                continue;

            var channel = await guild.GetTextChannelAsync(newId);
            if (channel is null)
                continue;

            IWebhook? webhook = null;
            try
            {
                webhook = await channel.CreateWebhookAsync("Mewdeko Backup");
                using var hook = new DiscordWebhookClient(webhook);
                foreach (var message in saved.Messages)
                {
                    try
                    {
                        var name = WebhookName.Replace(message.Author, "_").Trim();
                        if (name.Length < 2)
                            name = "User";
                        if (name.Length > 80)
                            name = name[..80];

                        await hook.SendMessageAsync(message.Content, username: name, avatarUrl: message.AvatarUrl,
                            allowedMentions: AllowedMentions.None);
                        progress.MessagesRestored++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Backup restore could not post a message in {Channel}", channel.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Backup restore could not use a webhook in {Channel}", channel.Id);
                progress.Failures.Add(strings.BackupFailMessages(guildId, channel.Name));
            }
            finally
            {
                if (webhook is not null)
                {
                    try
                    {
                        await webhook.DeleteAsync();
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }
    }

    private static string? ChannelKind(IGuildChannel channel)
    {
        return channel switch
        {
            ICategoryChannel => "category",
            IForumChannel => "forum",
            IStageChannel => "stage",
            IVoiceChannel => "voice",
            ITextChannel => "text",
            _ => null
        };
    }

    private static async Task<byte[]?> DownloadAsync(HttpClient http, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            return await http.GetByteArrayAsync(url);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
