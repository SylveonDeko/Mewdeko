using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Administration.Common;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Saves a server's roles, categories and channels as a named template and applies templates to servers. Templates
///     use the server backup format without messages, emojis or settings, and are applied by adding what is missing.
/// </summary>
/// <param name="dbFactory">The database connection factory.</param>
/// <param name="backups">The server backup service, which holds the shared capture and restore code.</param>
public class ServerTemplateService(IDataConnectionFactory dbFactory, ServerBackupService backups) : INService
{
    /// <summary>
    ///     The longest a template name can be.
    /// </summary>
    public const int MaxNameLength = 50;

    /// <summary>
    ///     Cleans up a template name.
    /// </summary>
    /// <param name="name">The name as typed.</param>
    /// <returns>The name, or null if it is empty or too long.</returns>
    public static string? NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxNameLength ? null : trimmed;
    }

    /// <summary>
    ///     Checks whether a name belongs to a starter template.
    /// </summary>
    /// <param name="name">The name.</param>
    public static bool IsBuiltIn(string name)
    {
        return BuiltInServerTemplates.Names.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Reads a server's roles, categories and channels into template data.
    /// </summary>
    /// <param name="guild">The server.</param>
    /// <returns>The template data.</returns>
    public static async Task<ServerBackupData> CaptureAsync(IGuild guild)
    {
        var data = new ServerBackupData();
        foreach (var role in guild.Roles.Where(x => !x.IsManaged).OrderBy(x => x.Position))
            data.Roles.Add(ServerBackupService.CaptureRole(guild, role, null));

        var kept = data.Roles.Select(x => x.Id).ToHashSet();
        var channels = (await guild.GetChannelsAsync()).Where(x => x is not IThreadChannel).OrderBy(x => x.Position);
        foreach (var channel in channels)
        {
            if (ServerBackupService.CaptureChannel(channel) is not { } entry)
                continue;

            entry.Overwrites = entry.Overwrites.Where(x => x.IsRole && kept.Contains(x.TargetId)).ToList();
            data.Channels.Add(entry);
        }

        return data;
    }

    /// <summary>
    ///     Saves a server as a template, replacing the user's template with the same name.
    /// </summary>
    /// <param name="guild">The server to save.</param>
    /// <param name="userId">The user the template belongs to.</param>
    /// <param name="name">The template name.</param>
    /// <returns>The saved template.</returns>
    public async Task<ServerTemplate> SaveAsync(IGuild guild, ulong userId, string name)
    {
        var data = await CaptureAsync(guild);
        var compressed = await ServerBackupService.CompressAsync(data);
        var template = new ServerTemplate
        {
            UserId = userId,
            Name = name,
            Data = compressed,
            SizeBytes = compressed.Length,
            RoleCount = data.Roles.Count,
            ChannelCount = data.Channels.Count,
            DateAdded = DateTime.UtcNow
        };

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.ServerTemplates.Where(x => x.UserId == userId && x.Name == name).DeleteAsync();
        template.Id = await db.InsertWithInt32IdentityAsync(template);
        return template;
    }

    /// <summary>
    ///     Lists a user's templates without their data.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The templates, newest first.</returns>
    public async Task<List<ServerTemplate>> ListAsync(ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ServerTemplates
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.DateAdded)
            .Select(x => new ServerTemplate
            {
                Id = x.Id,
                UserId = x.UserId,
                Name = x.Name,
                SizeBytes = x.SizeBytes,
                RoleCount = x.RoleCount,
                ChannelCount = x.ChannelCount,
                DateAdded = x.DateAdded
            })
            .ToListAsync();
    }

    /// <summary>
    ///     Gets the data of a starter template or of one of a user's templates. Starter templates win on a name clash.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="name">The template name.</param>
    /// <returns>The data, or null if there is no such template or it cannot be read.</returns>
    public async Task<ServerBackupData?> GetDataAsync(ulong userId, string name)
    {
        if (BuiltInServerTemplates.Build(name) is { } builtIn)
            return builtIn;

        await using var db = await dbFactory.CreateConnectionAsync();
        var data = await db.ServerTemplates.Where(x => x.UserId == userId && x.Name == name)
            .Select(x => x.Data).FirstOrDefaultAsync();
        return data is null ? null : ServerBackupService.ReadData(data);
    }

    /// <summary>
    ///     Deletes one of a user's templates.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="name">The template name.</param>
    /// <returns>True if a template was deleted.</returns>
    public async Task<bool> DeleteAsync(ulong userId, string name)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.ServerTemplates.Where(x => x.UserId == userId && x.Name == name).DeleteAsync() > 0;
    }

    /// <summary>
    ///     Checks whether a member can apply a template to a server.
    /// </summary>
    /// <param name="guild">The server to apply it to.</param>
    /// <param name="user">The member.</param>
    /// <returns>The error, if any, and the permissions the bot is missing.</returns>
    public Task<(BackupError Error, string Detail)> CheckApplyAsync(IGuild guild, IGuildUser user)
    {
        return backups.CheckRestoreAsync(guild, user, new ServerBackup
        {
            SourceGuildId = guild.Id
        }, false);
    }

    /// <summary>
    ///     Applies a template to a server by adding the roles, categories and channels it is missing, and reports how it
    ///     went.
    /// </summary>
    /// <param name="guild">The server.</param>
    /// <param name="user">The person who ran the command.</param>
    /// <param name="channel">The channel the command was run in.</param>
    /// <param name="data">The template data.</param>
    /// <returns>A task that completes when the template has been applied.</returns>
    public Task ApplyAsync(IGuild guild, IUser user, IMessageChannel channel, ServerBackupData data)
    {
        return backups.RunRestoreAsync(guild, user, channel, data, BackupRestoreMode.AddMissing);
    }

    /// <summary>
    ///     Gets the message that explains why a template could not be applied.
    /// </summary>
    /// <param name="guildId">The server ID, for the language.</param>
    /// <param name="error">The error.</param>
    /// <param name="detail">Extra detail from the check.</param>
    /// <returns>The message.</returns>
    public string ErrorText(ulong guildId, BackupError error, string detail)
    {
        return backups.ErrorText(guildId, error, detail);
    }
}
