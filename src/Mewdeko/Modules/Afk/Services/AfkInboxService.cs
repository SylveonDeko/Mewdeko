using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Database.Extensions;

namespace Mewdeko.Modules.Afk.Services;

/// <summary>
///     The outcome of leaving a message for an AFK member.
/// </summary>
public enum AfkLeaveResult
{
    /// <summary>The message is waiting for them.</summary>
    Left,

    /// <summary>The community has AFK messages turned off.</summary>
    Disabled,

    /// <summary>The member turned AFK messages off.</summary>
    OptedOut,

    /// <summary>Their inbox is full.</summary>
    Full,

    /// <summary>The sender already left as many as allowed.</summary>
    TooMany
}

/// <summary>
///     Holds messages left for AFK members until they come back and read them.
/// </summary>
public class AfkInboxService(IDataConnectionFactory dbFactory, GuildSettingsService guildSettings) : INService
{
    /// <summary>
    ///     The longest message a member can leave.
    /// </summary>
    public const int MaxLength = 500;

    private const int MaxPending = 25;
    private const int MaxPerSender = 3;

    /// <summary>
    ///     Gets whether members can leave messages for AFK members in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public async Task<bool> IsEnabledAsync(ulong guildId)
    {
        return (await guildSettings.GetGuildConfig(guildId))?.AfkMessages ?? false;
    }

    /// <summary>
    ///     Sets whether members can leave messages for AFK members in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="enabled">Whether messages are allowed.</param>
    public async Task SetEnabledAsync(ulong guildId, bool enabled)
    {
        var guildConfig = await guildSettings.GetGuildConfig(guildId);
        if (guildConfig == null) return;
        guildConfig.AfkMessages = enabled;
        await guildSettings.UpdateGuildConfig(guildId, guildConfig);
    }

    /// <summary>
    ///     Gets whether a member turned AFK messages off.
    /// </summary>
    /// <param name="userId">The member.</param>
    public async Task<bool> IsOptedOutAsync(ulong userId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.DiscordUsers.Where(u => u.UserId == userId).Select(u => u.AfkMessagesOptOut)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Turns AFK messages off or on for a member.
    /// </summary>
    /// <param name="user">The member.</param>
    /// <param name="optOut">True to stop receiving them, false to allow them, null to flip.</param>
    /// <returns>Whether the member is now opted out.</returns>
    public async Task<bool> SetOptOutAsync(IUser user, bool? optOut = null)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var dbUser = await db.GetOrCreateUser(user);
        dbUser.AfkMessagesOptOut = optOut ?? !dbUser.AfkMessagesOptOut;
        await db.UpdateAsync(dbUser);
        return dbUser.AfkMessagesOptOut;
    }

    /// <summary>
    ///     Leaves a message for an AFK member. Messages from a sender they blocked are dropped without saying so.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="recipientId">The AFK member.</param>
    /// <param name="senderId">Who is leaving the message.</param>
    /// <param name="content">The message.</param>
    public async Task<AfkLeaveResult> LeaveAsync(ulong guildId, ulong recipientId, ulong senderId, string content)
    {
        if (!await IsEnabledAsync(guildId)) return AfkLeaveResult.Disabled;
        if (await IsOptedOutAsync(recipientId)) return AfkLeaveResult.OptedOut;

        await using var db = await dbFactory.CreateConnectionAsync();
        if (await db.AfkInboxBlocks.AnyAsync(b => b.UserId == recipientId && b.BlockedId == senderId))
            return AfkLeaveResult.Left;

        var senders = await db.AfkInbox.Where(m => m.GuildId == guildId && m.RecipientId == recipientId)
            .Select(m => m.SenderId).ToListAsync();
        if (senders.Count(s => s == senderId) >= MaxPerSender) return AfkLeaveResult.TooMany;
        if (senders.Count >= MaxPending) return AfkLeaveResult.Full;

        var text = content.Trim();
        await db.InsertAsync(new AfkInboxMessage
        {
            GuildId = guildId,
            RecipientId = recipientId,
            SenderId = senderId,
            Content = text.Length > MaxLength ? text[..MaxLength] : text,
            DateAdded = DateTime.UtcNow
        });
        return AfkLeaveResult.Left;
    }

    /// <summary>
    ///     Counts the messages waiting for a member.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="recipientId">The member.</param>
    public async Task<int> CountAsync(ulong guildId, ulong recipientId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.AfkInbox.CountAsync(m => m.GuildId == guildId && m.RecipientId == recipientId);
    }

    /// <summary>
    ///     Returns a member's waiting messages, oldest first, and clears them.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="recipientId">The member.</param>
    public async Task<List<AfkInboxMessage>> TakeAsync(ulong guildId, ulong recipientId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var messages = await db.AfkInbox.Where(m => m.GuildId == guildId && m.RecipientId == recipientId)
            .OrderBy(m => m.DateAdded).ToListAsync();
        if (messages.Count > 0)
        {
            var ids = messages.Select(m => m.Id).ToList();
            await db.AfkInbox.Where(m => ids.Contains(m.Id)).DeleteAsync();
        }

        return messages;
    }

    /// <summary>
    ///     Stops someone from leaving a member AFK messages, and drops any they already left.
    /// </summary>
    /// <param name="userId">The member.</param>
    /// <param name="blockedId">Who to block.</param>
    public async Task BlockAsync(ulong userId, ulong blockedId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.InsertOrReplaceAsync(new AfkInboxBlock { UserId = userId, BlockedId = blockedId });
        await db.AfkInbox.Where(m => m.RecipientId == userId && m.SenderId == blockedId).DeleteAsync();
    }

    /// <summary>
    ///     Lets someone leave a member AFK messages again.
    /// </summary>
    /// <param name="userId">The member.</param>
    /// <param name="blockedId">Who to unblock.</param>
    /// <returns>True when they were blocked.</returns>
    public async Task<bool> UnblockAsync(ulong userId, ulong blockedId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.AfkInboxBlocks.Where(b => b.UserId == userId && b.BlockedId == blockedId).DeleteAsync() > 0;
    }
}
