using System.Reflection;
using LinqToDB.Mapping;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Tests;

/// <summary>
///     Tests for the member dump, sticky formatting, message links and the config cleanup targets.
/// </summary>
[TestFixture]
public class UtilityExtrasTests
{
    private static readonly MemberDumpRow[] Rows =
    [
        new(1, "alice", "Alice", new DateTimeOffset(2024, 1, 10, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2020, 5, 1, 0, 0, 0, TimeSpan.Zero), false, [100, 200], ["Staff", "Mod, Lead"]),
        new(2, "bob", "Bobby", new DateTimeOffset(2025, 3, 2, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2023, 8, 9, 0, 0, 0, TimeSpan.Zero), false, [200], ["Mod, Lead"]),
        new(3, "helper", "Helper", new DateTimeOffset(2023, 6, 6, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero), true, [], [])
    ];

    private static ulong? ResolveRole(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "staff" => 100,
            "mod" => 200,
            _ => null
        };
    }

    /// <summary>
    ///     Role filters keep members with a role and drop members with the other role.
    /// </summary>
    [Test]
    public void RoleFiltersApply()
    {
        Assert.That(MemberDumpOptions.TryParse("role:Staff", ResolveRole, out var has, out _), Is.True);
        Assert.That(has.Filter(Rows).Select(r => r.Id), Is.EqualTo(new ulong[] { 1 }));

        Assert.That(MemberDumpOptions.TryParse("role:mod norole:staff", ResolveRole, out var lacks, out _), Is.True);
        Assert.That(lacks.Filter(Rows).Select(r => r.Id), Is.EqualTo(new ulong[] { 2 }));
    }

    /// <summary>
    ///     Date and kind filters combine.
    /// </summary>
    [Test]
    public void DateAndKindFiltersApply()
    {
        Assert.That(MemberDumpOptions.TryParse("joinedbefore:2025-01-01 type:humans", ResolveRole, out var options,
            out _), Is.True);
        Assert.That(options.Filter(Rows).Select(r => r.Id), Is.EqualTo(new ulong[] { 1 }));

        Assert.That(MemberDumpOptions.TryParse("createdafter:2022-01-01", ResolveRole, out var created, out _),
            Is.True);
        Assert.That(created.Filter(Rows).Select(r => r.Id), Is.EqualTo(new ulong[] { 2 }));

        Assert.That(MemberDumpOptions.TryParse("type:bots", ResolveRole, out var bots, out _), Is.True);
        Assert.That(bots.Filter(Rows).Select(r => r.Id), Is.EqualTo(new ulong[] { 3 }));
    }

    /// <summary>
    ///     Unknown options and roles are reported instead of ignored.
    /// </summary>
    [Test]
    public void BadOptionsAreReported()
    {
        Assert.That(MemberDumpOptions.TryParse("role:Nobody", ResolveRole, out _, out var bad), Is.False);
        Assert.That(bad, Is.EqualTo("role:Nobody"));
        Assert.That(MemberDumpOptions.TryParse("color:red", ResolveRole, out _, out _), Is.False);
        Assert.That(MemberDumpOptions.TryParse("joinedbefore:never", ResolveRole, out _, out _), Is.False);
        Assert.That(MemberDumpOptions.TryParse("stray", ResolveRole, out _, out _), Is.False);
    }

    /// <summary>
    ///     CSV output keeps the chosen columns in order and quotes values that need it.
    /// </summary>
    [Test]
    public void CsvRendersChosenFields()
    {
        Assert.That(MemberDumpOptions.TryParse("format:csv fields:id,roles,joined", ResolveRole, out var options, out _),
            Is.True);
        var lines = options.Render(options.Filter(Rows)).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r')).ToArray();

        Assert.That(lines[0], Is.EqualTo("id,roles,joined"));
        Assert.That(lines[1], Is.EqualTo("3,,2023-06-06 00:00:00"));
        Assert.That(lines[2], Is.EqualTo("1,\"Staff, Mod, Lead\",2024-01-10 00:00:00"));
    }

    /// <summary>
    ///     Text output has no header and separates columns with bars.
    /// </summary>
    [Test]
    public void TextRendersWithoutHeader()
    {
        Assert.That(MemberDumpOptions.TryParse("fields:id,name", ResolveRole, out var options, out _), Is.True);
        var lines = options.Render(options.Filter(Rows)).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r')).ToArray();

        Assert.That(lines, Is.EqualTo(new[] { "3 | helper", "1 | alice", "2 | bob" }));
    }

    /// <summary>
    ///     Windows read as short durations.
    /// </summary>
    [Test]
    public void StickyWindowFormats()
    {
        Assert.That(StickyFormat.Window("00:00:15"), Is.EqualTo("15s"));
        Assert.That(StickyFormat.Window("00:05:00"), Is.EqualTo("5m"));
        Assert.That(StickyFormat.Window("02:00:00"), Is.EqualTo("2h"));
        Assert.That(StickyFormat.Window(null), Is.EqualTo("15s"));
    }

    /// <summary>
    ///     Message links of every Discord host parse and anything else does not.
    /// </summary>
    [Test]
    public void MessageLinksParse()
    {
        Assert.That(BotMessageEditService.TryParseLink("https://discord.com/channels/1/22/333", out var g, out var c,
            out var m), Is.True);
        Assert.That((g, c, m), Is.EqualTo((1UL, 22UL, 333UL)));
        Assert.That(BotMessageEditService.TryParseLink("https://ptb.discord.com/channels/1/22/333", out _, out _,
            out _), Is.True);
        Assert.That(BotMessageEditService.TryParseLink("https://discord.com/channels/1/22", out _, out _, out _),
            Is.False);
        Assert.That(BotMessageEditService.TryParseLink("nonsense", out _, out _, out _), Is.False);
    }

    /// <summary>
    ///     Every cleanup target names a real table and column, and none repeat.
    /// </summary>
    [Test]
    public void CleanupTargetsMatchTheSchema()
    {
        var tables = typeof(ConfigCleanupService).Assembly.GetTypes()
            .Select(t => (Type: t, Table: t.GetCustomAttribute<TableAttribute>()?.Name))
            .Where(x => x.Table != null)
            .GroupBy(x => x.Table!)
            .ToDictionary(g => g.Key, g => g.First().Type);

        var failures = new List<string>();
        foreach (var target in ConfigCleanupService.Targets)
        {
            if (!tables.TryGetValue(target.Table, out var type))
            {
                failures.Add($"No model for table {target.Table}");
                continue;
            }

            var columns = type.GetProperties()
                .Select(p => p.GetCustomAttribute<ColumnAttribute>()?.Name)
                .Where(n => n != null)
                .ToHashSet();

            if (!columns.Contains(target.Column))
                failures.Add($"{target.Table} has no column {target.Column}");

            if (target.GuildFilter == "\"GuildId\" = @guild" && !columns.Contains("GuildId"))
                failures.Add($"{target.Table} has no GuildId column");
        }

        var duplicates = ConfigCleanupService.Targets.GroupBy(t => (t.Table, t.Column)).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Table}.{g.Key.Column}");
        failures.AddRange(duplicates.Select(d => $"Duplicate target {d}"));

        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }
}
