using Fergun.Interactive;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Sorts and formats member lists ordered by account age or join date.
/// </summary>
public static class MemberAgeLists
{
    /// <summary>
    ///     The number of members shown on each page.
    /// </summary>
    public const int PageSize = 10;

    /// <summary>
    ///     Orders members by account creation or join date.
    /// </summary>
    /// <param name="users">The guild members.</param>
    /// <param name="ascending">Whether the oldest date comes first.</param>
    /// <param name="byJoin">Whether to order by join date instead of account creation.</param>
    /// <returns>The ordered members.</returns>
    public static List<IGuildUser> Sort(IEnumerable<IGuildUser> users, bool ascending, bool byJoin)
    {
        var members = byJoin ? users.Where(u => u.JoinedAt.HasValue) : users;
        Func<IGuildUser, DateTimeOffset> key = byJoin ? u => u.JoinedAt!.Value : u => u.CreatedAt;
        return (ascending ? members.OrderBy(key) : members.OrderByDescending(key)).ToList();
    }

    /// <summary>
    ///     Builds one page of a member list.
    /// </summary>
    /// <param name="title">The page title.</param>
    /// <param name="members">The ordered members.</param>
    /// <param name="page">The zero based page index.</param>
    /// <param name="byJoin">Whether the list is ordered by join date.</param>
    /// <returns>The page.</returns>
    public static PageBuilder Page(string title, List<IGuildUser> members, int page, bool byJoin)
    {
        var start = page * PageSize;
        var lines = members.Skip(start).Take(PageSize).Select((u, i) =>
        {
            var date = byJoin ? u.JoinedAt!.Value : u.CreatedAt;
            return $"{start + i + 1}. {Format.Sanitize(u.ToString())} `{u.Id}` {TimestampTag.FromDateTimeOffset(date, TimestampTagStyles.ShortDate)} ({TimestampTag.FromDateTimeOffset(date, TimestampTagStyles.Relative)})";
        });

        return new PageBuilder()
            .WithOkColor()
            .WithTitle(title)
            .WithDescription(string.Join("\n", lines));
    }
}
