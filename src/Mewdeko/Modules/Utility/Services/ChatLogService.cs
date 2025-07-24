using DataModel;
using LinqToDB;
using Mewdeko.Controllers.Common.Chat;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Service for managing chat logs
/// </summary>
public class ChatLogService(IDataConnectionFactory dbFactory, ILogger<ChatLogService> logger) : INService
{
    /// <summary>
    ///     Saves a chat log
    /// </summary>
    public async Task<string> SaveChatLogAsync(
        ulong guildId,
        ulong channelId,
        string channelName,
        string name,
        ulong createdBy,
        IEnumerable<ChatLogMessageDto> messages)
    {
        try
        {
            var messagesArray = messages.ToArray();
            var chatLog = new ChatLog
            {
                GuildId = guildId,
                ChannelId = channelId,
                ChannelName = channelName,
                Name = name,
                CreatedBy = createdBy,
                Messages = JsonConvert.SerializeObject(messagesArray),
                MessageCount = messagesArray.Length,
                DateAdded = DateTime.UtcNow
            };

            await using var context = await dbFactory.CreateConnectionAsync();
            await context.InsertAsync(chatLog);

            return chatLog.Id.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving chat log");
            throw;
        }
    }

    /// <summary>
    ///     Gets a chat log by id
    /// </summary>
    public async Task<ChatLog?> GetChatLogAsync(int logId)
    {
        try
        {
            await using var context = await dbFactory.CreateConnectionAsync();
            return await context.ChatLogs.FindAsync(logId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chat log");
            throw;
        }
    }

    /// <summary>
    ///     Gets all chat logs for a guild
    /// </summary>
    public async Task<IEnumerable<ChatLog>> GetChatLogsForGuildAsync(ulong guildId)
    {
        try
        {
            await using var context = await dbFactory.CreateConnectionAsync();
            return await context.ChatLogs
                .Where(l => l.GuildId == guildId)
                .OrderByDescending(l => l.DateAdded)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chat logs for guild");
            throw;
        }
    }

    /// <summary>
    ///     Updates a chat log's name
    /// </summary>
    public async Task UpdateChatLogNameAsync(int logId, string newName)
    {
        try
        {
            await using var context = await dbFactory.CreateConnectionAsync();
            var log = await context.ChatLogs.FindAsync(logId);

            if (log == null)
                throw new Exception("Chat log not found");

            log.Name = newName;
            await context.UpdateAsync(log);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating chat log name");
            throw;
        }
    }

    /// <summary>
    ///     Deletes a chat log
    /// </summary>
    public async Task DeleteChatLogAsync(int logId)
    {
        try
        {
            await using var context = await dbFactory.CreateConnectionAsync();
            var log = await context.ChatLogs.FindAsync(logId);

            if (log == null)
                return;

            await context.DeleteAsync(log);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting chat log");
            throw;
        }
    }
}