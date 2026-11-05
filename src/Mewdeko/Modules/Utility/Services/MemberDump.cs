using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     A member as seen by the member dump.
/// </summary>
/// <param name="Id">The user ID.</param>
/// <param name="Name">The username.</param>
/// <param name="DisplayName">The display name in the guild.</param>
/// <param name="JoinedAt">When the member joined, if known.</param>
/// <param name="CreatedAt">When the account was created.</param>
/// <param name="IsBot">Whether the member is a bot.</param>
/// <param name="RoleIds">The IDs of the member's roles.</param>
/// <param name="RoleNames">The names of the member's roles.</param>
public record MemberDumpRow(
    ulong Id,
    string Name,
    string DisplayName,
    DateTimeOffset? JoinedAt,
    DateTimeOffset CreatedAt,
    bool IsBot,
    IReadOnlyList<ulong> RoleIds,
    IReadOnlyList<string> RoleNames);

/// <summary>
///     Builds member dump rows and resolves roles for dump options.
/// </summary>
public static class MemberDumpSource
{
    /// <summary>
    ///     Turns a guild member into a dump row, leaving out the everyone role.
    /// </summary>
    /// <param name="user">The guild member.</param>
    /// <param name="guild">The guild.</param>
    /// <returns>The dump row.</returns>
    public static MemberDumpRow ToRow(IGuildUser user, IGuild guild)
    {
        var roleIds = user.RoleIds.Where(id => id != guild.Id).ToList();
        return new MemberDumpRow(user.Id, user.Username, user.DisplayName, user.JoinedAt, user.CreatedAt, user.IsBot,
            roleIds, roleIds.Select(id => guild.GetRole(id)?.Name ?? id.ToString()).ToList());
    }

    /// <summary>
    ///     Filters the members and renders them as a file.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="users">The guild members.</param>
    /// <param name="options">The filters and format.</param>
    /// <returns>The file stream, its name and how many members it holds.</returns>
    public static (MemoryStream Stream, string FileName, int Count) Build(IGuild guild, IEnumerable<IGuildUser> users,
        MemberDumpOptions options)
    {
        var rows = options.Filter(users.Select(u => ToRow(u, guild)));
        var bytes = new UTF8Encoding(false).GetBytes(options.Render(rows));
        return (new MemoryStream(bytes), options.Csv ? "members.csv" : "members.txt", rows.Count);
    }

    /// <summary>
    ///     Finds a role by mention, ID or name.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="text">The role mention, ID or name.</param>
    /// <returns>The role ID, or null when no role matches.</returns>
    public static ulong? ResolveRole(IGuild guild, string text)
    {
        var digits = text.Trim('<', '@', '&', '>');
        if (ulong.TryParse(digits, out var id) && guild.GetRole(id) != null)
            return id;

        return guild.Roles.FirstOrDefault(r => r.Name.Equals(text, StringComparison.OrdinalIgnoreCase))?.Id;
    }
}

/// <summary>
///     Which kind of member a dump includes.
/// </summary>
public enum MemberDumpKind
{
    /// <summary>
    ///     Bots and humans.
    /// </summary>
    All,

    /// <summary>
    ///     Bots only.
    /// </summary>
    Bots,

    /// <summary>
    ///     Humans only.
    /// </summary>
    Humans
}

/// <summary>
///     The filters and format of a member dump.
/// </summary>
public class MemberDumpOptions
{
    /// <summary>
    ///     The columns a dump can contain, in output order.
    /// </summary>
    public static readonly string[] AllFields = ["id", "name", "display", "joined", "created", "roles"];

    private static readonly Regex OptionPattern = new(@"(?<key>[A-Za-z]+):(?<value>""[^""]*""|\S+)",
        RegexOptions.Compiled);

    private static readonly Dictionary<string, string> FieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "id",
        ["name"] = "name",
        ["username"] = "name",
        ["display"] = "display",
        ["displayname"] = "display",
        ["nick"] = "display",
        ["joined"] = "joined",
        ["created"] = "created",
        ["roles"] = "roles"
    };

    /// <summary>
    ///     Gets or sets the role members must have.
    /// </summary>
    public ulong? HasRole { get; set; }

    /// <summary>
    ///     Gets or sets the role members must not have.
    /// </summary>
    public ulong? LacksRole { get; set; }

    /// <summary>
    ///     Gets or sets the latest join date allowed.
    /// </summary>
    public DateTimeOffset? JoinedBefore { get; set; }

    /// <summary>
    ///     Gets or sets the earliest join date allowed.
    /// </summary>
    public DateTimeOffset? JoinedAfter { get; set; }

    /// <summary>
    ///     Gets or sets the latest account creation date allowed.
    /// </summary>
    public DateTimeOffset? CreatedBefore { get; set; }

    /// <summary>
    ///     Gets or sets the earliest account creation date allowed.
    /// </summary>
    public DateTimeOffset? CreatedAfter { get; set; }

    /// <summary>
    ///     Gets or sets which kind of member is included.
    /// </summary>
    public MemberDumpKind Kind { get; set; }

    /// <summary>
    ///     Gets or sets whether the dump is a CSV file instead of plain text.
    /// </summary>
    public bool Csv { get; set; }

    /// <summary>
    ///     Gets or sets the columns to include, in output order.
    /// </summary>
    public IReadOnlyList<string> Fields { get; set; } = AllFields;

    /// <summary>
    ///     Parses key:value options such as <c>role:Staff format:csv joinedbefore:2025-01-01</c>.
    /// </summary>
    /// <param name="input">The option text.</param>
    /// <param name="resolveRole">Resolves a role mention, ID or name to a role ID.</param>
    /// <param name="options">The parsed options.</param>
    /// <param name="badOption">The option that failed to parse, if any.</param>
    /// <returns>Whether every option parsed.</returns>
    public static bool TryParse(string? input, Func<string, ulong?> resolveRole, out MemberDumpOptions options,
        out string? badOption)
    {
        options = new MemberDumpOptions();
        return Parse(input, resolveRole, options, out badOption);
    }

    private static bool Parse(string? input, Func<string, ulong?> resolveRole, MemberDumpOptions options,
        out string? badOption)
    {
        badOption = null;
        if (string.IsNullOrWhiteSpace(input))
            return true;

        var leftover = OptionPattern.Replace(input, "").Trim();
        if (leftover.Length > 0)
        {
            badOption = leftover;
            return false;
        }

        foreach (Match match in OptionPattern.Matches(input))
        {
            var key = match.Groups["key"].Value.ToLowerInvariant();
            var value = match.Groups["value"].Value.Trim('"');
            var ok = key switch
            {
                "role" or "has" => SetRole(value, id => options.HasRole = id),
                "norole" or "lacks" => SetRole(value, id => options.LacksRole = id),
                "joinedbefore" => SetDate(value, d => options.JoinedBefore = d),
                "joinedafter" => SetDate(value, d => options.JoinedAfter = d),
                "createdbefore" => SetDate(value, d => options.CreatedBefore = d),
                "createdafter" => SetDate(value, d => options.CreatedAfter = d),
                "type" => SetKind(value),
                "format" => SetFormat(value),
                "fields" => SetFields(value),
                _ => false
            };

            if (ok)
                continue;

            badOption = match.Value;
            return false;
        }

        return true;

        bool SetRole(string value, Action<ulong> set)
        {
            var id = resolveRole(value);
            if (id == null)
                return false;

            set(id.Value);
            return true;
        }

        bool SetDate(string value, Action<DateTimeOffset> set)
        {
            if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
                return false;

            set(date);
            return true;
        }

        bool SetKind(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "bots":
                    options.Kind = MemberDumpKind.Bots;
                    return true;
                case "humans":
                    options.Kind = MemberDumpKind.Humans;
                    return true;
                case "all":
                    options.Kind = MemberDumpKind.All;
                    return true;
                default:
                    return false;
            }
        }

        bool SetFormat(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "csv":
                    options.Csv = true;
                    return true;
                case "txt" or "text":
                    options.Csv = false;
                    return true;
                default:
                    return false;
            }
        }

        bool SetFields(string value)
        {
            var fields = new List<string>();
            foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!FieldNames.TryGetValue(part, out var field))
                    return false;

                if (!fields.Contains(field))
                    fields.Add(field);
            }

            if (fields.Count == 0)
                return false;

            options.Fields = fields;
            return true;
        }
    }

    /// <summary>
    ///     Applies the filters to a set of members.
    /// </summary>
    /// <param name="rows">The members.</param>
    /// <returns>The members that pass every filter, ordered by join date.</returns>
    public List<MemberDumpRow> Filter(IEnumerable<MemberDumpRow> rows)
    {
        return rows
            .Where(r => HasRole == null || r.RoleIds.Contains(HasRole.Value))
            .Where(r => LacksRole == null || !r.RoleIds.Contains(LacksRole.Value))
            .Where(r => JoinedBefore == null || (r.JoinedAt != null && r.JoinedAt < JoinedBefore))
            .Where(r => JoinedAfter == null || (r.JoinedAt != null && r.JoinedAt > JoinedAfter))
            .Where(r => CreatedBefore == null || r.CreatedAt < CreatedBefore)
            .Where(r => CreatedAfter == null || r.CreatedAt > CreatedAfter)
            .Where(r => Kind == MemberDumpKind.All || r.IsBot == (Kind == MemberDumpKind.Bots))
            .OrderBy(r => r.JoinedAt ?? DateTimeOffset.MaxValue)
            .ThenBy(r => r.Id)
            .ToList();
    }

    /// <summary>
    ///     Renders members as CSV or plain text with the chosen columns.
    /// </summary>
    /// <param name="rows">The filtered members.</param>
    /// <returns>The file content.</returns>
    public string Render(IReadOnlyList<MemberDumpRow> rows)
    {
        var builder = new StringBuilder();
        if (Csv)
            builder.AppendLine(string.Join(",", Fields));

        foreach (var row in rows)
        {
            var values = Fields.Select(f => Value(row, f));
            builder.AppendLine(Csv ? string.Join(",", values.Select(Escape)) : string.Join(" | ", values));
        }

        return builder.ToString();
    }

    private static string Value(MemberDumpRow row, string field)
    {
        return field switch
        {
            "id" => row.Id.ToString(),
            "name" => row.Name,
            "display" => row.DisplayName,
            "joined" => row.JoinedAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "",
            "created" => row.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            "roles" => string.Join(", ", row.RoleNames),
            _ => ""
        };
    }

    private static string Escape(string value)
    {
        var text = value.Replace("\r", " ").Replace("\n", " ");
        if (text.Length > 0 && "=+-@".Contains(text[0]))
            text = $"'{text}";

        return text.Contains(',') || text.Contains('"') ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }
}
