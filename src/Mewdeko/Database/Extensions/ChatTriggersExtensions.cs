using DataModel;
using LinqToDB;
using LinqToDB.Async;

namespace Mewdeko.Database.Extensions;

/// <summary>
///     Provides extension methods for ChatTriggers and related operations.
/// </summary>
public static class ChatTriggersExtensions
{
    /// <summary>
    ///     Gets the list of granted role IDs from a ChatTrigger instance.
    /// </summary>
    /// <param name="trigger">The ChatTrigger instance.</param>
    /// <returns>A list of ulong values representing granted role IDs.</returns>
    public static List<ulong> GetGrantedRoles(this ChatTrigger trigger)
    {
        return ParseUlongs(trigger.GrantedRoles) ?? [];
    }

    /// <summary>
    ///     Gets the list of removed role IDs from a ChatTrigger instance.
    /// </summary>
    /// <param name="trigger">The ChatTrigger instance.</param>
    /// <returns>A list of ulong values representing removed role IDs.</returns>
    public static List<ulong> GetRemovedRoles(this ChatTrigger trigger)
    {
        return ParseUlongs(trigger.RemovedRoles) ?? [];
    }

    /// <summary>
    ///     Parses a string of ulong values separated by "@@@" into a list of ulong values.
    /// </summary>
    /// <param name="inpt">The input string to parse.</param>
    /// <returns>A list of parsed ulong values, excluding any invalid or zero values.</returns>
    private static List<ulong> ParseUlongs(string inpt)
    {
        return inpt?.Split("@@@")
            .Select(x => ulong.TryParse(x, out var v) ? v : 0)
            .Where(x => x != 0)
            .Distinct()
            .ToList();
    }

    /// <summary>
    ///     Checks if a role ID is in the removed roles list of a ChatTrigger instance.
    /// </summary>
    /// <param name="trigger">The ChatTrigger instance.</param>
    /// <param name="roleId">The role ID to check.</param>
    /// <returns>True if the role ID is in the removed roles list, false otherwise.</returns>
    public static bool IsRemoved(this ChatTrigger trigger, ulong roleId)
    {
        return trigger.RemovedRoles?.Contains(roleId.ToString()) ?? false;
    }

    /// <summary>
    ///     Checks if a role ID is in the granted roles list of a ChatTrigger instance.
    /// </summary>
    /// <param name="trigger">The ChatTrigger instance.</param>
    /// <param name="roleId">The role ID to check.</param>
    /// <returns>True if the role ID is in the granted roles list, false otherwise.</returns>
    public static bool IsGranted(this ChatTrigger trigger, ulong roleId)
    {
        return trigger.GrantedRoles?.Contains(roleId.ToString()) ?? false;
    }

    /// <summary>
    ///     Checks if a role ID is both granted and removed in a ChatTrigger instance.
    /// </summary>
    /// <param name="trigger">The ChatTrigger instance.</param>
    /// <param name="roleId">The role ID to check.</param>
    /// <returns>True if the role ID is both granted and removed, false otherwise.</returns>
    public static bool IsToggled(this ChatTrigger trigger, ulong roleId)
    {
        return trigger.IsGranted(roleId) && trigger.IsRemoved(roleId);
    }

    /// <summary>
    ///     Clears all ChatTriggers associated with a specific guild.
    /// </summary>
    /// <param name="crs">The ITable of ChatTrigger.</param>
    /// <param name="guildId">The ID of the guild to clear triggers for.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the number of records deleted.</returns>
    public static async Task<int> ClearFromGuildAsync(this ITable<ChatTrigger> crs, ulong guildId)
    {
        return await crs
            .Where(x => x.GuildId == guildId)
            .DeleteAsync();
    }

    /// <summary>
    ///     Retrieves all ChatTriggers for a specific guild ID.
    /// </summary>
    /// <param name="crs">The ITable of ChatTrigger.</param>
    /// <param name="id">The ID of the guild to retrieve triggers for.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an IEnumerable of ChatTrigger.</returns>
    public static async Task<IEnumerable<ChatTrigger>> ForId(this ITable<ChatTrigger> crs, ulong id)
    {
        return await crs
            .Where(x => x.GuildId == id)
            .ToArrayAsync();
    }

    /// <summary>
    ///     Retrieves a ChatTrigger instance by guild ID and input trigger.
    /// </summary>
    /// <param name="crs">The ITable of ChatTrigger.</param>
    /// <param name="guildId">The ID of the guild to search in.</param>
    /// <param name="input">The input trigger to search for.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the matching ChatTrigger
    ///     instance, or null if not found.
    /// </returns>
    public static async Task<ChatTrigger> GetByGuildIdAndInput(this ITable<ChatTrigger> crs, ulong? guildId,
        string input)
    {
        return await crs
            .FirstOrDefaultAsync(x => x.GuildId == guildId &&
                                      Sql.Lower(x.Trigger) == Sql.Lower(input));
    }

    /// <summary>
    ///     Gets an array of reactions associated with this trigger.
    /// </summary>
    /// <returns>An array of reaction string?s.</returns>
    public static string?[] GetReactions(this ChatTrigger trigger)
    {
        return string.IsNullOrWhiteSpace(trigger.Reactions)
            ? []
            : trigger.Reactions.Split("@@@");
    }

    /// <summary>
    ///     Gets every response this trigger can send, with the primary response first.
    /// </summary>
    /// <param name="trigger">The ChatTrigger instance.</param>
    /// <returns>The trigger's responses, in the order they were defined.</returns>
    /// <remarks>
    ///     Additional responses share the "@@@" separator the rest of the trigger's list columns use.
    /// </remarks>
    public static List<string> GetResponses(this ChatTrigger trigger)
    {
        var responses = new List<string>();

        if (!string.IsNullOrWhiteSpace(trigger.Response))
            responses.Add(trigger.Response);

        if (!string.IsNullOrWhiteSpace(trigger.AdditionalResponses))
        {
            responses.AddRange(trigger.AdditionalResponses.Split("@@@")
                .Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        return responses;
    }

    /// <summary>
    ///     Gets the real name of the trigger, which is either the application command name or the trigger text.
    /// </summary>
    public static string? RealName(this ChatTrigger ct)
        => (string.IsNullOrEmpty(ct.ApplicationCommandName) ? ct.Trigger : ct.ApplicationCommandName).Trim();
}