using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Outcome of sticking a message to a channel.
/// </summary>
public enum StickyResult
{
    /// <summary>
    ///     A new sticky was created.
    /// </summary>
    Created,

    /// <summary>
    ///     The channel's existing sticky got new content.
    /// </summary>
    Replaced,

    /// <summary>
    ///     The sticky could not be created.
    /// </summary>
    Failed
}

/// <summary>
///     One-step sticky messages built on the repeater system, plus reusable sticky templates.
///     A sticky is an after-messages repeater that re-posts after N new messages or T of new chat, whichever comes first.
/// </summary>
public class StickyService(MessageRepeaterService repeaters, IDataConnectionFactory dbFactory) : INService
{
    /// <summary>
    ///     Gets whether the repeater system has finished loading.
    /// </summary>
    public bool Ready => repeaters.RepeaterReady;

    /// <summary>
    ///     Gets the stickies in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The sticky runners, ordered by channel.</returns>
    public IReadOnlyList<RepeatRunner> GetStickies(ulong guildId)
    {
        return repeaters.GetGuildRepeaters(guildId)
            .Where(IsSticky)
            .OrderBy(r => r.Repeater.ChannelId)
            .ToList();
    }

    /// <summary>
    ///     Gets the sticky in a channel, if there is one.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>The sticky runner, or null.</returns>
    public RepeatRunner? GetSticky(ulong guildId, ulong channelId)
    {
        return GetStickies(guildId).FirstOrDefault(r => r.Repeater.ChannelId == channelId);
    }

    /// <summary>
    ///     Sticks a message to a channel, replacing the channel's current sticky content if there is one.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="content">The message text or embed json.</param>
    /// <returns>Whether a sticky was created, replaced or could not be made.</returns>
    public async Task<StickyResult> StickAsync(ulong guildId, ulong channelId, string content)
    {
        var existing = GetSticky(guildId, channelId);
        if (existing != null)
            return await EditAsync(guildId, channelId, content) ? StickyResult.Replaced : StickyResult.Failed;

        var runner = await repeaters.CreateRepeaterAsync(guildId, channelId, StickyDefaults.Window, content,
            triggerMode: StickyTriggerMode.AfterMessages, activityThreshold: StickyDefaults.Messages,
            activityTimeWindow: StickyDefaults.Window);
        return runner == null ? StickyResult.Failed : StickyResult.Created;
    }

    /// <summary>
    ///     Changes the content of a channel's sticky and re-posts it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="content">The new message text or embed json.</param>
    /// <returns>Whether the channel had a sticky.</returns>
    public async Task<bool> EditAsync(ulong guildId, ulong channelId, string content)
    {
        var sticky = GetSticky(guildId, channelId);
        if (sticky == null || !await repeaters.UpdateRepeaterMessageAsync(guildId, sticky.Repeater.Id, content, false))
            return false;

        await sticky.TriggerInternal(true);
        return true;
    }

    /// <summary>
    ///     Removes a channel's sticky and deletes its last posted copy.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>Whether the channel had a sticky.</returns>
    public async Task<bool> UnstickAsync(ulong guildId, ulong channelId)
    {
        var sticky = GetSticky(guildId, channelId);
        if (sticky == null)
            return false;

        await RemoveAsync(sticky);
        return true;
    }

    /// <summary>
    ///     Removes every sticky in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>How many stickies were removed.</returns>
    public async Task<int> ClearAsync(ulong guildId)
    {
        var stickies = GetStickies(guildId);
        foreach (var sticky in stickies)
            await RemoveAsync(sticky);

        return stickies.Count;
    }

    /// <summary>
    ///     Sets how many messages or how much time brings a channel's sticky back to the bottom.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="messages">The message count.</param>
    /// <param name="window">The time window.</param>
    /// <returns>Whether the channel had a sticky.</returns>
    public async Task<bool> TuneAsync(ulong guildId, ulong channelId, int messages, TimeSpan window)
    {
        var sticky = GetSticky(guildId, channelId);
        if (sticky == null)
            return false;

        return await repeaters.UpdateRepeaterActivityThresholdAsync(guildId, sticky.Repeater.Id, messages, window);
    }

    /// <summary>
    ///     Gets a guild's sticky templates.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The templates, ordered by name.</returns>
    public async Task<List<StickyTemplate>> GetTemplatesAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.StickyTemplates.Where(t => t.GuildId == guildId).OrderBy(t => t.Name).ToListAsync();
    }

    /// <summary>
    ///     Gets a sticky template by name.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The template name, case insensitive.</param>
    /// <returns>The template, or null.</returns>
    public async Task<StickyTemplate?> GetTemplateAsync(ulong guildId, string name)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var lower = name.ToLowerInvariant();
        return await db.StickyTemplates.FirstOrDefaultAsync(t => t.GuildId == guildId && t.Name.ToLower() == lower);
    }

    /// <summary>
    ///     Saves a sticky template, replacing the content of one with the same name.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The template name.</param>
    /// <param name="content">The message text or embed json.</param>
    /// <returns>True when a new template was added, false when an existing one was updated.</returns>
    public async Task<bool> SaveTemplateAsync(ulong guildId, string name, string content)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var lower = name.ToLowerInvariant();
        var existing = await db.StickyTemplates.FirstOrDefaultAsync(t => t.GuildId == guildId && t.Name.ToLower() == lower);
        if (existing != null)
        {
            await db.StickyTemplates.Where(t => t.Id == existing.Id)
                .Set(t => t.Content, content)
                .UpdateAsync();
            return false;
        }

        await db.InsertAsync(new StickyTemplate
        {
            GuildId = guildId,
            Name = name,
            Content = content
        });
        return true;
    }

    /// <summary>
    ///     Deletes a sticky template.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="name">The template name, case insensitive.</param>
    /// <returns>Whether a template was deleted.</returns>
    public async Task<bool> DeleteTemplateAsync(ulong guildId, string name)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var lower = name.ToLowerInvariant();
        return await db.StickyTemplates.Where(t => t.GuildId == guildId && t.Name.ToLower() == lower).DeleteAsync() > 0;
    }

    private static bool IsSticky(RepeatRunner runner)
    {
        return runner.Repeater.TriggerMode == (int)StickyTriggerMode.AfterMessages && !runner.Repeater.ThreadOnlyMode;
    }

    private async Task RemoveAsync(RepeatRunner sticky)
    {
        await sticky.DeleteLastMessageAsync();
        await repeaters.RemoveRepeater(sticky.Repeater);
    }
}
