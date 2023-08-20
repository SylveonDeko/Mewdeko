using Discord;
using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class DiscordUserExtensions
{
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

    //temp is only used in updatecurrencystate, so that i don't overwrite real usernames/discrims with Unknown
    public static async Task<DiscordUser> GetOrCreateUser(
        this MewdekoContext ctx,
        ulong userId,
        string username,
        string discrim,
        string avatarId)
    {
        await ctx.EnsureUserCreated(userId, username, discrim, avatarId);
        return await ctx.DiscordUser.Include(x => x.Club).FirstOrDefaultAsyncEF(u => u.UserId == userId).ConfigureAwait(false);
    }

    public static async Task<DiscordUser> GetOrCreateUser(this MewdekoContext ctx, IUser original) =>
        await ctx.GetOrCreateUser(original.Id, original.Username, original.Discriminator, original.AvatarId).ConfigureAwait(false);

    public static async Task<int> GetUserGlobalRank(this DbSet<DiscordUser> users, ulong id) =>
        await users.AsQueryable().CountAsyncEF(x => x.TotalXp > users.AsQueryable().Where(y => y.UserId == id).Select(y => y.TotalXp).FirstOrDefault()).ConfigureAwait(false)
        + 1;

    public static DiscordUser[] GetUsersXpLeaderboardFor(this DbSet<DiscordUser> users, int page) =>
        users.AsQueryable().OrderByDescending(x => x.TotalXp).Skip(page * 9).Take(9).AsEnumerable().ToArray();
}