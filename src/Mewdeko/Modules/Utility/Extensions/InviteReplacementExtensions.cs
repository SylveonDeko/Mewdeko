using DataModel;
using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.Utility.Extensions;

/// <summary>
///     Adds invite tracking and join metadata placeholders to greet and leave messages.
/// </summary>
public static class InviteReplacementExtensions
{
    private static readonly (string Suffix, TimestampTagStyles Style)[] TimestampStyles =
    [
        ("t", TimestampTagStyles.ShortTime),
        ("T", TimestampTagStyles.LongTime),
        ("d", TimestampTagStyles.ShortDate),
        ("D", TimestampTagStyles.LongDate),
        ("f", TimestampTagStyles.ShortDateTime),
        ("F", TimestampTagStyles.LongDateTime),
        ("R", TimestampTagStyles.Relative)
    ];

    /// <summary>
    ///     Adds inviter, invite code and join metadata placeholders. Every placeholder resolves to a sensible fallback
    ///     when the join could not be attributed, so templates never leak raw tokens.
    /// </summary>
    /// <param name="builder">The replacement builder.</param>
    /// <param name="result">The join attribution, or null when unknown.</param>
    /// <param name="inviterCounts">The inviter's tally, or null.</param>
    /// <param name="joinCount">How many times the member has joined, including this time.</param>
    /// <param name="leaveCount">How many times the member has left before.</param>
    /// <returns>The builder, for chaining.</returns>
    public static ReplacementBuilder WithInviteInfo(this ReplacementBuilder builder, InviteJoinResult? result,
        InviteCount? inviterCounts, int joinCount, int leaveCount)
    {
        var inviter = result?.Inviter;
        var unknown = "Unknown";

        builder.WithOverride("%inviter.username%", () => inviter?.Username.EscapeWeirdStuff() ?? unknown);
        builder.WithOverride("%inviter.name%", () => inviter?.Username.EscapeWeirdStuff() ?? unknown);
        builder.WithOverride("%inviter.fullname%", () => inviter?.ToString()?.EscapeWeirdStuff() ?? unknown);
        builder.WithOverride("%inviter.mention%", () => inviter?.Mention ?? unknown);
        builder.WithOverride("%inviter.id%", () => inviter?.Id.ToString() ?? unknown);
        builder.WithOverride("%inviter.avatar%", () => inviter?.RealAvatarUrl().ToString() ?? unknown);
        builder.WithOverride("%inviter.count%", () => (inviterCounts?.Count ?? 0).ToString());
        builder.WithOverride("%inviter.invites%", () => (inviterCounts?.Count ?? 0).ToString());
        builder.WithOverride("%inviter.regular%", () => (inviterCounts?.Regular ?? 0).ToString());
        builder.WithOverride("%inviter.left%", () => (inviterCounts?.Left ?? 0).ToString());
        builder.WithOverride("%inviter.fake%", () => (inviterCounts?.Fake ?? 0).ToString());
        builder.WithOverride("%inviter.bonus%", () => (inviterCounts?.Bonus ?? 0).ToString());

        builder.WithOverride("%invite.code%", () => result?.Code ?? unknown);
        builder.WithOverride("%invite.uses%", () => (result?.Uses ?? 0).ToString());
        builder.WithOverride("%invite.url%",
            () => result?.Code is { } code && code != "vanity" ? $"https://discord.gg/{code}" : unknown);
        builder.WithOverride("%invite.label%", () => result?.Label ?? result?.Code ?? unknown);
        builder.WithOverride("%invite.type%", () => (result?.JoinType ?? InviteJoinType.Unknown).ToString());
        builder.WithOverride("%invite.fake%",
            () => result is { IsFake: true } ? result.FakeReason.ToString() : "");

        builder.WithOverride("%user.joincount%", () => joinCount.ToString());
        builder.WithOverride("%user.leavecount%", () => leaveCount.ToString());
        return builder;
    }

    /// <summary>
    ///     Adds Discord timestamp tag placeholders for the member's join and account creation times, in every style
    ///     Discord renders: <c>%user.joined.R%</c>, <c>%user.created.F%</c> and so on.
    /// </summary>
    /// <param name="builder">The replacement builder.</param>
    /// <param name="user">The member.</param>
    /// <returns>The builder, for chaining.</returns>
    public static ReplacementBuilder WithJoinTimestamps(this ReplacementBuilder builder, IGuildUser user)
    {
        foreach (var (suffix, style) in TimestampStyles)
        {
            builder.WithOverride($"%user.created.{suffix}%",
                () => TimestampTag.FromDateTimeOffset(user.CreatedAt, style).ToString());
            builder.WithOverride($"%user.joined.{suffix}%",
                () => user.JoinedAt.HasValue
                    ? TimestampTag.FromDateTimeOffset(user.JoinedAt.Value, style).ToString()
                    : "-");
        }

        builder.WithOverride("%user.created.ago%", () => Ago(DateTimeOffset.UtcNow - user.CreatedAt));
        builder.WithOverride("%user.joined.ago%",
            () => user.JoinedAt.HasValue ? Ago(DateTimeOffset.UtcNow - user.JoinedAt.Value) : "-");
        return builder;
    }

    /// <summary>
    ///     Adds the member count as an ordinal, so greets can say "you are our 1,204th member".
    /// </summary>
    /// <param name="builder">The replacement builder.</param>
    /// <param name="guild">The guild.</param>
    /// <returns>The builder, for chaining.</returns>
    public static ReplacementBuilder WithMemberOrdinal(this ReplacementBuilder builder, IGuild guild)
    {
        builder.WithOverride("%server.members.ordinal%", () => Ordinal(GetMemberCount(guild)));
        builder.WithOverride("%server.members.count%", () => GetMemberCount(guild).ToString("N0"));
        return builder;
    }

    private static int GetMemberCount(IGuild guild)
    {
        return guild is SocketGuild sg ? sg.MemberCount : guild.ApproximateMemberCount ?? 0;
    }

    /// <summary>
    ///     Formats a number as an English ordinal with thousands separators.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <returns>The ordinal, such as 1st, 22nd or 1,113th.</returns>
    public static string Ordinal(int number)
    {
        var suffix = number % 100 is >= 11 and <= 13
            ? "th"
            : (number % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };
        return number.ToString("N0") + suffix;
    }

    private static string Ago(TimeSpan span)
    {
        if (span.TotalDays >= 365)
            return $"{(int)(span.TotalDays / 365)} years ago";
        if (span.TotalDays >= 30)
            return $"{(int)(span.TotalDays / 30)} months ago";
        if (span.TotalDays >= 1)
            return $"{(int)span.TotalDays} days ago";
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours} hours ago";
        return $"{Math.Max(1, (int)span.TotalMinutes)} minutes ago";
    }
}