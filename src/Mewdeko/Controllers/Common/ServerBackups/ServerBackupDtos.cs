using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Controllers.Common.ServerBackups;

/// <summary>
///     A user's server backup, without its data.
/// </summary>
public class ServerBackupDto
{
    /// <summary>
    ///     The backup ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The ID of the server the backup came from.
    /// </summary>
    public ulong SourceGuildId { get; set; }

    /// <summary>
    ///     The name the server had when the backup was made.
    /// </summary>
    public string SourceGuildName { get; set; } = "";

    /// <summary>
    ///     The compressed size in bytes.
    /// </summary>
    public int SizeBytes { get; set; }

    /// <summary>
    ///     How many roles the backup holds.
    /// </summary>
    public int RoleCount { get; set; }

    /// <summary>
    ///     How many channels and categories the backup holds.
    /// </summary>
    public int ChannelCount { get; set; }

    /// <summary>
    ///     How many emojis the backup holds.
    /// </summary>
    public int EmojiCount { get; set; }

    /// <summary>
    ///     How many messages the backup holds.
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    ///     When the backup was made.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
///     A user's backups and the limits that apply to them.
/// </summary>
public class ServerBackupListResponse
{
    /// <summary>
    ///     The most backups a user can keep.
    /// </summary>
    public int MaxBackups { get; set; }

    /// <summary>
    ///     Minutes a user has to wait between creating backups.
    /// </summary>
    public int CooldownMinutes { get; set; }

    /// <summary>
    ///     The backups, newest first.
    /// </summary>
    public List<ServerBackupDto> Backups { get; set; } = [];
}

/// <summary>
///     Request to back up a server.
/// </summary>
public class CreateServerBackupRequest
{
    /// <summary>
    ///     How many recent messages to save from each chosen channel, up to 100.
    /// </summary>
    public int Messages { get; set; }

    /// <summary>
    ///     The channels to save messages from.
    /// </summary>
    public List<ulong> ChannelIds { get; set; } = [];
}

/// <summary>
///     Request to restore a backup into a server.
/// </summary>
public class RestoreServerBackupRequest
{
    /// <summary>
    ///     Replace or AddMissing.
    /// </summary>
    public string Mode { get; set; } = "Replace";
}

/// <summary>
///     A role in a backup or template preview.
/// </summary>
public class BackupPreviewRoleDto
{
    /// <summary>
    ///     The role name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The role color as an RGB value, 0 for none.
    /// </summary>
    public uint Color { get; set; }
}

/// <summary>
///     A channel in a backup or template preview.
/// </summary>
public class BackupPreviewChannelDto
{
    /// <summary>
    ///     The channel name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The channel type: text, voice, stage, forum or category.
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    ///     The name of the category the channel is in, or null.
    /// </summary>
    public string? Category { get; set; }
}

/// <summary>
///     What a backup or template holds.
/// </summary>
public class BackupPreviewDto
{
    /// <summary>
    ///     The server name saved in it.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The roles, lowest first.
    /// </summary>
    public List<BackupPreviewRoleDto> Roles { get; set; } = [];

    /// <summary>
    ///     The categories and channels in server order.
    /// </summary>
    public List<BackupPreviewChannelDto> Channels { get; set; } = [];

    /// <summary>
    ///     How many emojis it holds.
    /// </summary>
    public int EmojiCount { get; set; }

    /// <summary>
    ///     How many messages it holds.
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    ///     Builds the preview of some backup data.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns>The preview.</returns>
    public static BackupPreviewDto From(ServerBackupData data)
    {
        var names = data.Channels.ToDictionary(x => x.Id, x => x.Name);
        return new BackupPreviewDto
        {
            Name = data.Settings.Name,
            Roles = data.Roles.Where(x => !x.IsEveryone).OrderBy(x => x.Position)
                .Select(x => new BackupPreviewRoleDto
                {
                    Name = x.Name, Color = x.Color
                }).ToList(),
            Channels = data.Channels.OrderBy(x => x.Position).Select(x => new BackupPreviewChannelDto
            {
                Name = x.Name,
                Type = x.Type,
                Category = x.ParentId is { } parent && names.TryGetValue(parent, out var name) ? name : null
            }).ToList(),
            EmojiCount = data.Emojis.Count,
            MessageCount = data.Messages.Sum(x => x.Messages.Count)
        };
    }
}

/// <summary>
///     How far a restore into a server has got.
/// </summary>
public class RestoreStatusDto
{
    /// <summary>
    ///     Whether a restore has been started for the server since the bot started.
    /// </summary>
    public bool Started { get; set; }

    /// <summary>
    ///     Whether the restore is over.
    /// </summary>
    public bool Finished { get; set; }

    /// <summary>
    ///     What the restore is doing now.
    /// </summary>
    public string Step { get; set; } = "";

    /// <summary>
    ///     How many roles were created.
    /// </summary>
    public int RolesCreated { get; set; }

    /// <summary>
    ///     How many channels and categories were created.
    /// </summary>
    public int ChannelsCreated { get; set; }

    /// <summary>
    ///     How many emojis were created.
    /// </summary>
    public int EmojisCreated { get; set; }

    /// <summary>
    ///     How many messages were posted.
    /// </summary>
    public int MessagesRestored { get; set; }

    /// <summary>
    ///     The things that failed or were skipped.
    /// </summary>
    public List<string> Failures { get; set; } = [];
}
