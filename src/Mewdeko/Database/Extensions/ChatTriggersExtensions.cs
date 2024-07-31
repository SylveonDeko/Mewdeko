using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
/// Provides extension methods for ChatTriggers and related operations.
/// </summary>
public static class ChatTriggersExtensions
{
    /// <summary>
    /// Gets the list of granted role IDs from a ChatTriggers instance.
    /// </summary>
    /// <param name="trigger">The ChatTriggers instance.</param>
    /// <returns>A list of ulong values representing granted role IDs.</returns>
    public static List<ulong> GetGrantedRoles(this ChatTriggers trigger)
        => ParseUlongs(trigger.GrantedRoles) ?? [];

    /// <summary>
    /// Gets the list of removed role IDs from a ChatTriggers instance.
    /// </summary>
    /// <param name="trigger">The ChatTriggers instance.</param>
    /// <returns>A list of ulong values representing removed role IDs.</returns>
    public static List<ulong> GetRemovedRoles(this ChatTriggers trigger)
        => ParseUlongs(trigger.RemovedRoles) ?? [];

    /// <summary>
    /// Parses a string of ulong values separated by "@@@" into a list of ulong values.
    /// </summary>
    /// <param name="inpt">The input string to parse.</param>
    /// <returns>A list of parsed ulong values, excluding any invalid or zero values.</returns>
    private static List<ulong> ParseUlongs(string inpt)
        => inpt?.Split("@@@")
            .Select(x => ulong.TryParse(x, out var v) ? v : 0)
            .Where(x => x != 0)
            .Distinct()
            .ToList();

    /// <summary>
    /// Checks if a role ID is in the removed roles list of a ChatTriggers instance.
    /// </summary>
    /// <param name="trigger">The ChatTriggers instance.</param>
    /// <param name="roleId">The role ID to check.</param>
    /// <returns>True if the role ID is in the removed roles list, false otherwise.</returns>
    public static bool IsRemoved(this ChatTriggers trigger, ulong roleId) =>
        trigger.RemovedRoles?.Contains(roleId.ToString()) ?? false;

    /// <summary>
    /// Checks if a role ID is in the granted roles list of a ChatTriggers instance.
    /// </summary>
    /// <param name="trigger">The ChatTriggers instance.</param>
    /// <param name="roleId">The role ID to check.</param>
    /// <returns>True if the role ID is in the granted roles list, false otherwise.</returns>
    public static bool IsGranted(this ChatTriggers trigger, ulong roleId) =>
        trigger.GrantedRoles?.Contains(roleId.ToString()) ?? false;

    /// <summary>
    /// Checks if a role ID is both granted and removed in a ChatTriggers instance.
    /// </summary>
    /// <param name="trigger">The ChatTriggers instance.</param>
    /// <param name="roleId">The role ID to check.</param>
    /// <returns>True if the role ID is both granted and removed, false otherwise.</returns>
    public static bool IsToggled(this ChatTriggers trigger, ulong roleId) =>
        trigger.IsGranted(roleId) && trigger.IsRemoved(roleId);

    /// <summary>
    /// Clears all ChatTriggers associated with a specific guild.
    /// </summary>
    /// <param name="crs">The DbSet of ChatTriggers.</param>
    /// <param name="guildId">The ID of the guild to clear triggers for.</param>
    /// <returns>The number of records deleted.</returns>
    public static int ClearFromGuild(this DbSet<ChatTriggers> crs, ulong guildId)
        => crs.Delete(x => x.GuildId == guildId);

    /// <summary>
    /// Retrieves all ChatTriggers for a specific guild ID.
    /// </summary>
    /// <param name="crs">The DbSet of ChatTriggers.</param>
    /// <param name="id">The ID of the guild to retrieve triggers for.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an IEnumerable of ChatTriggers.</returns>
    public static async Task<IEnumerable<ChatTriggers>> ForId(this DbSet<ChatTriggers> crs, ulong id) =>
        await crs
            .AsNoTracking()
            .AsQueryable()
            .Where(x => x.GuildId == id)
            .ToArrayAsyncEF();

    /// <summary>
    /// Retrieves a ChatTriggers instance by guild ID and input trigger.
    /// </summary>
    /// <param name="crs">The DbSet of ChatTriggers.</param>
    /// <param name="guildId">The ID of the guild to search in.</param>
    /// <param name="input">The input trigger to search for.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the matching ChatTriggers instance, or null if not found.</returns>
    public static async Task<ChatTriggers> GetByGuildIdAndInput(this DbSet<ChatTriggers> crs, ulong? guildId, string input) =>
        await crs.FirstOrDefaultAsyncLinqToDB(x => x.GuildId == guildId &&
                                                   Sql.Lower(x.Trigger) == Sql.Lower(input));
}