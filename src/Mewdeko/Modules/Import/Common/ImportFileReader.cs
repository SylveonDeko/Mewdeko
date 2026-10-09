using System.Globalization;
using System.Text.Json;
using Mewdeko.Modules.Xp.Models;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Import.Common;

/// <summary>
///     Reads exported leaderboard files from other bots, and any JSON, CSV or text file with a user ID and an XP or
///     level per member.
/// </summary>
public static class ImportFileReader
{
    private static readonly string[] IdKeys =
        ["userid", "user_id", "id", "discordid", "discord_id", "memberid", "member_id", "user"];

    private static readonly string[] XpKeys =
        ["xp", "totalxp", "total_xp", "exp", "experience", "score", "points", "total xp"];

    private static readonly string[] LevelKeys = ["level", "lvl"];
    private static readonly string[] MessageKeys = ["messagecount", "message_count", "messages", "msgs"];
    private static readonly string[] NameKeys = ["username", "tag", "name", "displayname", "display_name"];
    private static readonly string[] CashKeys = ["cash", "wallet", "balance"];
    private static readonly string[] BankKeys = ["bank"];

    private static readonly string[] ListKeys =
        ["levels", "users", "players", "data", "rankings", "leaderboard", "members", "entries"];

    /// <summary>
    ///     Reads a file exported from <paramref name="source" />.
    /// </summary>
    /// <param name="source">The bot or format the file came from.</param>
    /// <param name="content">The file's text.</param>
    /// <returns>The members, role rewards and curve the file describes.</returns>
    /// <exception cref="ImportException">The file could not be read or has no members.</exception>
    public static ImportDataset Read(ImportSource source, string content)
    {
        content = content.Trim().TrimStart('﻿');
        if (content.Length == 0)
            throw new ImportException(ImportError.FileRequired);

        var dataset = content[0] is '[' or '{'
            ? ReadJson(source, content)
            : ReadDelimited(source, content);

        if (dataset.Members.Count == 0)
            throw new ImportException(ImportError.Empty);

        return dataset;
    }

    private static ImportDataset ReadJson(ImportSource source, string content)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content, new JsonDocumentOptions
            {
                AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip
            });
        }
        catch (JsonException)
        {
            throw new ImportException(ImportError.UnreadableFile);
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("settings", out var settings) &&
                root.TryGetProperty("users", out var polarisUsers) && polarisUsers.ValueKind == JsonValueKind.Object)
                return ReadPolarisEverything(settings, polarisUsers);

            var list = FindList(root);
            if (list is null)
                throw new ImportException(ImportError.UnreadableFile);

            var members = Deduplicate(list.Value.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.Object)
                .Select(ReadMember)
                .OfType<ImportedMember>());

            var rewards = root.ValueKind == JsonValueKind.Object ? ReadRewards(root) : [];

            return BuildDataset(source, members, rewards, null);
        }
    }

    private static ImportDataset ReadPolarisEverything(JsonElement settings, JsonElement users)
    {
        var members = new List<ImportedMember>();
        foreach (var user in users.EnumerateObject())
        {
            if (!ulong.TryParse(user.Name, out var userId) || user.Value.ValueKind != JsonValueKind.Object)
                continue;
            var xp = ReadLong(user.Value, XpKeys);
            if (xp is > 0)
                members.Add(new ImportedMember(userId, xp, null, null, null, null, null));
        }

        var rewards = new List<ImportedRoleReward>();
        if (settings.TryGetProperty("rewards", out var rewardList) && rewardList.ValueKind == JsonValueKind.Array)
        {
            foreach (var reward in rewardList.EnumerateArray())
            {
                if (ReadUlong(reward, ["id", "roleid", "role"]) is { } roleId && ReadInt(reward, LevelKeys) is { } level)
                    rewards.Add(new ImportedRoleReward(level, roleId));
            }
        }

        return BuildDataset(ImportSource.Polaris, Deduplicate(members), rewards, PolarisCurve(settings));
    }

    /// <summary>
    ///     Builds Polaris' level formula from its settings: the sum of each coefficient times the level to its power,
    ///     rounded to the nearest multiple of the rounding setting.
    /// </summary>
    private static Func<int, long> PolarisCurve(JsonElement? settings)
    {
        var curve = new Dictionary<int, double>
        {
            [3] = 1, [2] = 50, [1] = 100
        };
        var rounding = 100;
        var maxLevel = 1000;

        if (settings is { ValueKind: JsonValueKind.Object } s)
        {
            if (s.TryGetProperty("curve", out var curveElement) && curveElement.ValueKind == JsonValueKind.Object)
            {
                curve.Clear();
                foreach (var term in curveElement.EnumerateObject())
                {
                    if (int.TryParse(term.Name, out var power) && term.Value.TryGetDouble(out var coefficient))
                        curve[power] = coefficient;
                }
            }

            if (ReadInt(s, ["rounding"]) is > 0 and var r)
                rounding = r;
            if (ReadInt(s, ["maxlevel"]) is > 0 and var m)
                maxLevel = m;
        }

        return level =>
        {
            level = Math.Min(level, maxLevel);
            var xp = curve.Sum(term => term.Value * Math.Pow(level, term.Key));
            var rounded = rounding > 1
                ? rounding * Math.Round(xp / rounding, MidpointRounding.AwayFromZero)
                : Math.Round(xp, MidpointRounding.AwayFromZero);
            return (long)rounded;
        };
    }

    private static JsonElement? FindList(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root;

        if (root.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array &&
                ListKeys.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                return property.Value;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array &&
                property.Value.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.Object))
                return property.Value;
        }

        return null;
    }

    private static ImportedMember? ReadMember(JsonElement element)
    {
        var userId = ReadUlong(element, IdKeys);
        string? name = null;

        if (element.TryGetPropertyIgnoreCase("user", out var user) && user.ValueKind == JsonValueKind.Object)
        {
            userId ??= ReadUlong(user, ["id"]);
            name = ReadString(user, NameKeys);
        }

        if (userId is null or 0)
            return null;

        return new ImportedMember(
            userId.Value,
            ReadLong(element, XpKeys),
            ReadInt(element, LevelKeys),
            ReadLong(element, MessageKeys),
            ReadLong(element, CashKeys),
            ReadLong(element, BankKeys),
            ReadString(element, NameKeys) ?? name);
    }

    private static List<ImportedRoleReward> ReadRewards(JsonElement root)
    {
        var rewards = new List<ImportedRoleReward>();

        foreach (var key in new[] { "roleRewards", "role_rewards", "rewards", "levelRoles", "level_roles" })
        {
            if (!root.TryGetPropertyIgnoreCase(key, out var list) || list.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var reward in list.EnumerateArray())
            {
                if (reward.ValueKind != JsonValueKind.Object || ReadInt(reward, ["level", "rank"]) is not { } level)
                    continue;

                if (reward.TryGetPropertyIgnoreCase("roleIds", out var roleIds) && roleIds.ValueKind == JsonValueKind.Array)
                {
                    foreach (var roleId in roleIds.EnumerateArray())
                    {
                        if (ParseUlong(roleId) is { } id)
                            rewards.Add(new ImportedRoleReward(level, id));
                    }

                    continue;
                }

                if (reward.TryGetPropertyIgnoreCase("role", out var role) && role.ValueKind == JsonValueKind.Object &&
                    ReadUlong(role, ["id"]) is { } nestedId)
                {
                    rewards.Add(new ImportedRoleReward(level, nestedId));
                    continue;
                }

                if (ReadUlong(reward, ["roleId", "role_id", "role", "id"]) is { } flatId)
                    rewards.Add(new ImportedRoleReward(level, flatId));
            }

            break;
        }

        return rewards;
    }

    private static ImportDataset ReadDelimited(ImportSource source, string content)
    {
        var lines = content.Split('\n')
            .Select(x => x.TrimEnd('\r'))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (lines.Count == 0)
            throw new ImportException(ImportError.UnreadableFile);

        if (lines.All(x => x.Contains(" - ")) && !lines[0].Contains(','))
        {
            var dashed = lines
                .Select(x => x.Split(" - ", 2))
                .Where(x => x.Length == 2 && ulong.TryParse(x[0].Trim(), out _) && long.TryParse(x[1].Trim(), out _))
                .Select(x => new ImportedMember(ulong.Parse(x[0].Trim()), long.Parse(x[1].Trim()), null, null, null,
                    null, null));
            return BuildDataset(source, Deduplicate(dashed), [], null);
        }

        var delimiter = lines[0].Count(x => x == ';') > lines[0].Count(x => x == ',') ? ';' :
            lines[0].Contains('\t') && !lines[0].Contains(',') ? '\t' : ',';

        var header = SplitRow(lines[0], delimiter).Select(Normalize).ToList();
        var hasHeader = !header.Any(x => ulong.TryParse(x, out _));

        int idColumn, xpColumn, levelColumn, messageColumn, cashColumn, bankColumn, nameColumn;
        if (hasHeader)
        {
            idColumn = FindColumn(header, IdKeys);
            xpColumn = FindColumn(header, XpKeys);
            levelColumn = FindColumn(header, LevelKeys);
            messageColumn = FindColumn(header, MessageKeys);
            cashColumn = FindColumn(header, CashKeys);
            bankColumn = FindColumn(header, BankKeys);
            nameColumn = FindColumn(header, NameKeys);
        }
        else
        {
            idColumn = 0;
            xpColumn = 1;
            levelColumn = header.Count > 2 ? 2 : -1;
            messageColumn = cashColumn = bankColumn = nameColumn = -1;
        }

        if (idColumn < 0 || xpColumn < 0 && levelColumn < 0 && cashColumn < 0 && bankColumn < 0)
            throw new ImportException(ImportError.UnreadableFile);

        var members = new List<ImportedMember>();
        foreach (var line in lines.Skip(hasHeader ? 1 : 0))
        {
            var cells = SplitRow(line, delimiter);
            if (Cell(cells, idColumn) is not { } idText || !ulong.TryParse(idText, out var userId) || userId == 0)
                continue;

            members.Add(new ImportedMember(
                userId,
                ParseLong(Cell(cells, xpColumn)),
                (int?)ParseLong(Cell(cells, levelColumn)),
                ParseLong(Cell(cells, messageColumn)),
                ParseLong(Cell(cells, cashColumn)),
                ParseLong(Cell(cells, bankColumn)),
                Cell(cells, nameColumn)));
        }

        return BuildDataset(source, Deduplicate(members), [], null);
    }

    private static ImportDataset BuildDataset(ImportSource source, List<ImportedMember> members,
        List<ImportedRoleReward> rewards, Func<int, long>? polarisCurve)
    {
        var isCurrency = members.Count > 0 && members.All(x => x.Xp is null && x.Level is null) &&
                         members.Any(x => x.Cash is not null || x.Bank is not null);

        XpCurveType? native = source switch
        {
            ImportSource.Lurkr => XpCurveType.Lurkr,
            ImportSource.Mee6 => XpCurveType.Mee6,
            ImportSource.Amari => XpCurveType.Amari,
            _ => null
        };

        Func<int, long>? sourceCurve = native is { } curve
            ? level => XpCalculator.CalculateXpForLevel(level, curve)
            : source == ImportSource.Polaris
                ? polarisCurve ?? PolarisCurve(null)
                : null;

        return new ImportDataset
        {
            Kind = isCurrency ? ImportKind.Currency : ImportKind.Xp,
            Members = members,
            RoleRewards = rewards.DistinctBy(x => x.Level).ToList(),
            NativeCurve = isCurrency ? null : native,
            SourceXpForLevel = isCurrency ? null : sourceCurve
        };
    }

    /// <summary>
    ///     Keeps one entry per user, the one with the most XP or level, since some exports list a member twice.
    /// </summary>
    internal static List<ImportedMember> Deduplicate(IEnumerable<ImportedMember> members)
    {
        return members
            .GroupBy(x => x.UserId)
            .Select(g => g.OrderByDescending(x => x.Xp ?? 0).ThenByDescending(x => x.Level ?? 0)
                .ThenByDescending(x => (x.Cash ?? 0) + (x.Bank ?? 0)).First())
            .ToList();
    }

    private static string Normalize(string header)
    {
        return header.Trim().Trim('"').ToLowerInvariant();
    }

    private static int FindColumn(List<string> header, string[] keys)
    {
        foreach (var key in keys)
        {
            var index = header.FindIndex(x => x == key || x.Replace(" ", "").Replace("_", "") == key.Replace("_", ""));
            if (index >= 0)
                return index;
        }

        return -1;
    }

    private static string? Cell(IReadOnlyList<string> cells, int index)
    {
        if (index < 0 || index >= cells.Count)
            return null;
        var value = cells[index].Trim().Trim('"').Trim();
        return value.Length == 0 ? null : value;
    }

    private static List<string> SplitRow(string line, char delimiter)
    {
        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (c == delimiter && !quoted)
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        cells.Add(current.ToString());
        return cells;
    }

    private static bool TryGetPropertyIgnoreCase(this JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    continue;
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static ulong? ReadUlong(JsonElement element, string[] keys)
    {
        foreach (var key in keys)
        {
            if (element.TryGetPropertyIgnoreCase(key, out var value) && ParseUlong(value) is { } parsed)
                return parsed;
        }

        return null;
    }

    private static ulong? ParseUlong(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String when ulong.TryParse(value.GetString(), out var s) && s > 0 => s,
            JsonValueKind.Number when value.TryGetUInt64(out var n) && n > 0 => n,
            _ => null
        };
    }

    private static long? ReadLong(JsonElement element, string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetPropertyIgnoreCase(key, out var value))
                continue;

            switch (value.ValueKind)
            {
                case JsonValueKind.Number when value.TryGetInt64(out var n):
                    return n;
                case JsonValueKind.Number when value.TryGetDouble(out var d):
                    return ClampToLong(d);
                case JsonValueKind.String when ParseLong(value.GetString()) is { } s:
                    return s;
            }
        }

        return null;
    }

    private static int? ReadInt(JsonElement element, string[] keys)
    {
        return ReadLong(element, keys) is { } value ? (int)Math.Clamp(value, 0, int.MaxValue) : null;
    }

    private static string? ReadString(JsonElement element, string[] keys)
    {
        foreach (var key in keys)
        {
            if (element.TryGetPropertyIgnoreCase(key, out var value) && value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
                return value.GetString();
        }

        return null;
    }

    /// <summary>
    ///     Reads a whole number from text, accepting thousands separators, decimals and "Infinity".
    /// </summary>
    internal static long? ParseLong(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Trim().Replace(",", "").Replace("_", "");
        if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole))
            return whole;

        if (text.Equals("Infinity", StringComparison.OrdinalIgnoreCase))
            return long.MaxValue / 2;

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? ClampToLong(d)
            : null;
    }

    private static long ClampToLong(double value)
    {
        if (double.IsNaN(value))
            return 0;
        return (long)Math.Clamp(value, long.MinValue / 2, long.MaxValue / 2);
    }
}
