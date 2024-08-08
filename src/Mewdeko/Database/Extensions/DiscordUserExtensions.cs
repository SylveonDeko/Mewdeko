using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

/// <summary>
/// Provides extension methods for working with DiscordUser entities in the MewdekoContext.
/// </summary>
public static class DiscordUserExtensions
{
    /// <summary>
    /// Ensures that a Discord user is created in the database. If the user already exists, updates the user information.
    /// </summary>
    /// <param name="ctx">The database context.</param>
    /// <param name="userId">The ID of the Discord user.</param>
    /// <param name="username">The username of the Discord user.</param>
    /// <param name="discrim">The discriminator of the Discord user.</param>
    /// <param name="avatarId">The avatar ID of the Discord user.</param>
    public static async Task EnsureUserCreated(
        this MewdekoContext ctx,
        ulong userId,
        string username,
        string discrim,
        string avatarId) =>
        await ctx.DiscordUser.ToLinqToDBTable().InsertOrUpdateAsync(() => new DiscordUser
        {
            UserId = userId,
            Username = username,
            Discriminator = discrim,
            AvatarId = avatarId,
            TotalXp = 0
        }, old => new DiscordUser
        {
            Username = username, Discriminator = discrim, AvatarId = avatarId
        }, () => new DiscordUser
        {
            UserId = userId
        }).ConfigureAwait(false);

    /// <summary>
    /// Retrieves or creates a Discord user in the database.
    /// </summary>
    /// <param name="ctx">The database context.</param>
    /// <param name="userId">The ID of the Discord user.</param>
    /// <param name="username">The username of the Discord user.</param>
    /// <param name="discrim">The discriminator of the Discord user.</param>
    /// <param name="avatarId">The avatar ID of the Discord user.</param>
    /// <returns>The Discord user entity.</returns>
    public static async Task<DiscordUser> GetOrCreateUser(
        this MewdekoContext ctx,
        ulong userId,
        string username,
        string discrim,
        string avatarId)
    {
        await ctx.EnsureUserCreated(userId, username, discrim, avatarId);
        return await ctx.DiscordUser.FirstOrDefaultAsyncEF(u => u.UserId == userId)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves or creates a Discord user in the database from an IUser instance.
    /// </summary>
    /// <param name="ctx">The database context.</param>
    /// <param name="original">The IUser instance representing the Discord user.</param>
    /// <returns>The Discord user entity.</returns>
    public async static Task<DiscordUser> GetOrCreateUser(this MewdekoContext ctx, IUser original) =>
        await ctx.GetOrCreateUser(original.Id, original.Username, original.Discriminator, original.AvatarId);

    /// <summary>
    /// Retrieves the global rank of a Discord user based on their total XP.
    /// </summary>
    /// <param name="users">The DbSet of DiscordUser entities.</param>
    /// <param name="id">The ID of the Discord user.</param>
    /// <returns>The global rank of the Discord user.</returns>
    public static async Task<int> GetUserGlobalRank(this DbSet<DiscordUser> users, ulong id) =>
        await users.AsQueryable().CountAsyncEF(x =>
                x.TotalXp > users.AsQueryable().Where(y => y.UserId == id).Select(y => y.TotalXp).FirstOrDefault())
            .ConfigureAwait(false)
        + 1;

    /// <summary>
    /// Retrieves the XP leaderboard for Discord users for a specific page.
    /// </summary>
    /// <param name="users">The DbSet of DiscordUser entities.</param>
    /// <param name="page">The page number of the leaderboard.</param>
    /// <returns>An array of DiscordUser entities sorted by total XP.</returns>
    public async static Task<DiscordUser[]> GetUsersXpLeaderboardFor(this DbSet<DiscordUser> users, int page) =>
        (await users.ToListAsyncEF()).OrderByDescending(x => x.TotalXp).Skip(page * 9).Take(9).ToArray();
}