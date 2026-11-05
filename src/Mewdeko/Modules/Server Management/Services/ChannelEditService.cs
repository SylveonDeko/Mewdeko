using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Server_Management.Services;

/// <summary>
///     Why a channel edit was refused.
/// </summary>
public enum ChannelEditError
{
    /// <summary>
    ///     Nothing is wrong.
    /// </summary>
    None,

    /// <summary>
    ///     A setting does not apply to this kind of channel.
    /// </summary>
    Unsupported,

    /// <summary>
    ///     The name is empty or too long.
    /// </summary>
    BadName,

    /// <summary>
    ///     The bitrate is out of range.
    /// </summary>
    BadBitrate,

    /// <summary>
    ///     The user limit is out of range.
    /// </summary>
    BadUserLimit,

    /// <summary>
    ///     The slowmode is out of range.
    /// </summary>
    BadSlowmode,

    /// <summary>
    ///     The position is out of range.
    /// </summary>
    BadPosition
}

/// <summary>
///     What to change on a channel. Settings left null stay as they are.
/// </summary>
/// <param name="Name">The new name.</param>
/// <param name="Topic">The new topic. An empty topic clears it.</param>
/// <param name="Nsfw">Whether the channel is age restricted.</param>
/// <param name="SlowMode">The slowmode in seconds.</param>
/// <param name="BitrateKbps">The voice bitrate in kbps.</param>
/// <param name="UserLimit">The voice user limit. Zero means no limit.</param>
/// <param name="CategoryId">The category to move the channel to.</param>
/// <param name="RemoveCategory">Whether to take the channel out of its category.</param>
/// <param name="Position">The position, counting from zero.</param>
public record ChannelEditRequest(
    string? Name = null,
    string? Topic = null,
    bool? Nsfw = null,
    int? SlowMode = null,
    int? BitrateKbps = null,
    int? UserLimit = null,
    ulong? CategoryId = null,
    bool RemoveCategory = false,
    int? Position = null);

/// <summary>
///     Edits, syncs and clones channels.
/// </summary>
public class ChannelEditService : INService
{
    /// <summary>
    ///     The longest slowmode Discord allows, in seconds.
    /// </summary>
    public const int MaxSlowMode = 21600;

    /// <summary>
    ///     The most users a voice channel can be limited to.
    /// </summary>
    public const int MaxUserLimit = 99;

    /// <summary>
    ///     Checks an edit against the kind of channel and the allowed ranges.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="request">The edit.</param>
    /// <returns>The first problem found, or none.</returns>
    public static ChannelEditError Validate(IGuildChannel channel, ChannelEditRequest request)
    {
        var voice = channel is IVoiceChannel;
        var text = !voice && channel is ITextChannel;
        var forum = channel is IForumChannel;
        var category = channel is ICategoryChannel;

        if (request.Name is { } name && (name.Trim().Length is 0 or > 100))
            return ChannelEditError.BadName;
        if (request.Topic is not null && !(text || forum))
            return ChannelEditError.Unsupported;
        if (request.Nsfw is not null && !(text || forum))
            return ChannelEditError.Unsupported;
        if (request.SlowMode is not null && !text)
            return ChannelEditError.Unsupported;
        if (request.BitrateKbps is not null && !voice)
            return ChannelEditError.Unsupported;
        if (request.UserLimit is not null && !voice)
            return ChannelEditError.Unsupported;
        if ((request.CategoryId is not null || request.RemoveCategory) && category)
            return ChannelEditError.Unsupported;

        if (request.SlowMode is < 0 or > MaxSlowMode)
            return ChannelEditError.BadSlowmode;
        if (request.BitrateKbps is { } kbps && channel is IVoiceChannel &&
            (kbps < 8 || kbps * 1000 > channel.Guild.MaxBitrate))
            return ChannelEditError.BadBitrate;
        if (request.UserLimit is < 0 or > MaxUserLimit)
            return ChannelEditError.BadUserLimit;
        if (request.Position is < 0)
            return ChannelEditError.BadPosition;

        return ChannelEditError.None;
    }

    /// <summary>
    ///     Applies an edit to a channel.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="request">The edit.</param>
    /// <returns>The problem if the edit was refused, otherwise none.</returns>
    public async Task<ChannelEditError> EditAsync(IGuildChannel channel, ChannelEditRequest request)
    {
        var error = Validate(channel, request);
        if (error != ChannelEditError.None)
            return error;

        var name = request.Name?.Trim();
        var topic = request.Topic is { } t ? (t.Length == 0 ? null : t) : null;
        var category = request.RemoveCategory ? null : request.CategoryId;

        switch (channel)
        {
            case IVoiceChannel voice:
                await voice.ModifyAsync(x =>
                {
                    if (name is not null)
                        x.Name = name;
                    if (request.BitrateKbps is { } kbps)
                        x.Bitrate = kbps * 1000;
                    if (request.UserLimit is { } limit)
                        x.UserLimit = limit == 0 ? null : limit;
                    if (request.CategoryId is not null || request.RemoveCategory)
                        x.CategoryId = category;
                    if (request.Position is { } position)
                        x.Position = position;
                });
                break;
            case IForumChannel forum:
                await forum.ModifyAsync(x =>
                {
                    if (name is not null)
                        x.Name = name;
                    if (request.Topic is not null)
                        x.Topic = topic;
                    if (request.Nsfw is { } nsfw)
                        x.IsNsfw = nsfw;
                    if (request.CategoryId is not null || request.RemoveCategory)
                        x.CategoryId = category;
                    if (request.Position is { } position)
                        x.Position = position;
                });
                break;
            case ITextChannel text:
                await text.ModifyAsync(x =>
                {
                    if (name is not null)
                        x.Name = name;
                    if (request.Topic is not null)
                        x.Topic = topic;
                    if (request.Nsfw is { } nsfw)
                        x.IsNsfw = nsfw;
                    if (request.SlowMode is { } slow)
                        x.SlowModeInterval = slow;
                    if (request.CategoryId is not null || request.RemoveCategory)
                        x.CategoryId = category;
                    if (request.Position is { } position)
                        x.Position = position;
                });
                break;
            default:
                await channel.ModifyAsync(x =>
                {
                    if (name is not null)
                        x.Name = name;
                    if (request.Position is { } position)
                        x.Position = position;
                });
                break;
        }

        return ChannelEditError.None;
    }

    /// <summary>
    ///     Makes a channel's permissions match its category.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns>False if the channel has no category to sync with.</returns>
    public async Task<bool> SyncPermissionsAsync(IGuildChannel channel)
    {
        if (channel is not INestedChannel { CategoryId: not null } nested)
            return false;

        await nested.SyncPermissionsAsync();
        return true;
    }

    /// <summary>
    ///     Creates a copy of a channel with the same settings and permissions, right below the original.
    /// </summary>
    /// <param name="channel">The channel to copy.</param>
    /// <param name="name">The name of the copy, or null to keep the name.</param>
    /// <returns>The copy, or null if this kind of channel cannot be copied.</returns>
    public async Task<IGuildChannel?> CloneAsync(IGuildChannel channel, string? name)
    {
        if (ServerBackupService.CaptureChannel(channel) is not { } entry)
            return null;

        if (!string.IsNullOrWhiteSpace(name))
            entry.Name = name.Trim();

        var overwrites = channel.PermissionOverwrites.ToList();
        var copy = await ServerBackupService.CreateChannelAsync(channel.Guild, entry, entry.ParentId, overwrites);
        try
        {
            await copy.ModifyAsync(x => x.Position = channel.Position + 1);
        }
        catch (Exception)
        {
        }

        return copy;
    }
}
