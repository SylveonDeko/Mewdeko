using System.Linq.Expressions;
using System.Reflection;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Database.DbContextStuff;

namespace Mewdeko.Database.Extensions;

/// <summary>
/// </summary>
public static class WarnExtensions
{
    /// <summary>
    /// </summary>
    /// <param name="set"></param>
    /// <param name="db"></param>
    /// <param name="guildId"></param>
    /// <param name="userId"></param>
    /// <param name="mod"></param>
    public static Task ForgiveAll(this ITable<Warning> set, MewdekoDb db, ulong guildId, ulong userId, string mod)
    {
        return ForgiveAll<Warning>(set, db, guildId, userId, mod);
    }

    /// <summary>
    /// </summary>
    /// <param name="set"></param>
    /// <param name="db"></param>
    /// <param name="guildId"></param>
    /// <param name="userId"></param>
    /// <param name="mod"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    public static Task<bool> Forgive(this ITable<Warning> set, MewdekoDb db, ulong guildId, ulong userId, string mod,
        int index)
    {
        return Forgive<Warning>(set, db, guildId, userId, mod, index);
    }

    /// <summary>
    /// </summary>
    /// <param name="set"></param>
    /// <param name="db"></param>
    /// <param name="guildId"></param>
    /// <param name="userId"></param>
    /// <param name="mod"></param>
    public static Task ForgiveAll(this ITable<Warnings2> set, MewdekoDb db, ulong guildId, ulong userId, string mod)
    {
        return ForgiveAll<Warnings2>(set, db, guildId, userId, mod);
    }

    /// <summary>
    /// </summary>
    /// <param name="set"></param>
    /// <param name="db"></param>
    /// <param name="guildId"></param>
    /// <param name="userId"></param>
    /// <param name="mod"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    public static Task<bool> Forgive(this ITable<Warnings2> set, MewdekoDb db, ulong guildId, ulong userId, string mod,
        int index)
    {
        return Forgive<Warnings2>(set, db, guildId, userId, mod, index);
    }

    private static async Task ForgiveAll<TWarning>(ITable<TWarning> set, MewdekoDb db, ulong guildId, ulong userId,
        string moderator)
        where TWarning : class
    {
        var warnings = await set.Where(BuildPredicate<TWarning>(guildId, userId, true)).ToListAsync()
            .ConfigureAwait(false);
        foreach (var warning in warnings)
        {
            MarkForgiven(warning, moderator);
            await db.UpdateAsync(warning).ConfigureAwait(false);
        }
    }

    private static async Task<bool> Forgive<TWarning>(ITable<TWarning> set, MewdekoDb db, ulong guildId, ulong userId,
        string moderator, int index)
        where TWarning : class
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        var warn = await set.Where(BuildPredicate<TWarning>(guildId, userId, false))
            .OrderByDescending(BuildDateAddedSelector<TWarning>())
            .Skip(index)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (warn == null || (bool)GetProperty(warn, nameof(Warning.Forgiven)).GetValue(warn)!)
            return false;

        MarkForgiven(warn, moderator);
        await db.UpdateAsync(warn).ConfigureAwait(false);
        return true;
    }

    private static Expression<Func<TWarning, bool>> BuildPredicate<TWarning>(ulong guildId, ulong userId,
        bool onlyUnforgiven)
    {
        var warning = Expression.Parameter(typeof(TWarning), "warning");
        var guildMatches = Expression.Equal(Expression.Property(warning, nameof(Warning.GuildId)),
            Expression.Constant(guildId));
        var userMatches = Expression.Equal(Expression.Property(warning, nameof(Warning.UserId)),
            Expression.Constant(userId));
        var body = Expression.AndAlso(guildMatches, userMatches);

        if (onlyUnforgiven)
            body = Expression.AndAlso(body, Expression.Not(Expression.Property(warning, nameof(Warning.Forgiven))));

        return Expression.Lambda<Func<TWarning, bool>>(body, warning);
    }

    private static Expression<Func<TWarning, DateTime?>> BuildDateAddedSelector<TWarning>()
    {
        var warning = Expression.Parameter(typeof(TWarning), "warning");
        return Expression.Lambda<Func<TWarning, DateTime?>>(
            Expression.Property(warning, nameof(Warning.DateAdded)), warning);
    }

    private static void MarkForgiven<TWarning>(TWarning warning, string moderator)
    {
        GetProperty(warning, nameof(Warning.Forgiven)).SetValue(warning, true);
        GetProperty(warning, nameof(Warning.ForgivenBy)).SetValue(warning, moderator);
    }

    private static PropertyInfo GetProperty<TWarning>(TWarning warning, string propertyName)
    {
        return typeof(TWarning).GetProperty(propertyName) ??
               throw new InvalidOperationException($"{typeof(TWarning).Name} is missing {propertyName}.");
    }
}