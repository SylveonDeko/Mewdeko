using System.Text;
using LinqToDB;
using LinqToDB.Data;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     What a cleanup target points at.
/// </summary>
public enum CleanupKind
{
    /// <summary>
    ///     A channel.
    /// </summary>
    Channel,

    /// <summary>
    ///     A role.
    /// </summary>
    Role
}

/// <summary>
///     What the cleanup does with a setting that points at something deleted.
/// </summary>
public enum CleanupAction
{
    /// <summary>
    ///     Resets the setting to unset.
    /// </summary>
    Clear,

    /// <summary>
    ///     Deletes the row that holds the setting.
    /// </summary>
    DeleteRow,

    /// <summary>
    ///     Removes the deleted ID from a space separated list.
    /// </summary>
    ClearList,

    /// <summary>
    ///     Only reports the setting, because it cannot be fixed safely.
    /// </summary>
    Report
}

/// <summary>
///     A setting that stores a channel or role ID.
/// </summary>
/// <param name="Feature">The feature the setting belongs to.</param>
/// <param name="Table">The table that holds the setting.</param>
/// <param name="Column">The column that holds the ID or list of IDs.</param>
/// <param name="Kind">Whether the setting points at a channel or a role.</param>
/// <param name="Action">What to do when the target no longer exists.</param>
/// <param name="GuildFilter">SQL that limits rows to one guild, using the @guild parameter.</param>
public record CleanupTarget(
    string Feature,
    string Table,
    string Column,
    CleanupKind Kind,
    CleanupAction Action,
    string GuildFilter = "\"GuildId\" = @guild");

/// <summary>
///     A setting that points at a deleted channel or role.
/// </summary>
/// <param name="Target">The setting.</param>
/// <param name="MissingId">The ID that no longer exists.</param>
/// <param name="Rows">How many rows point at it.</param>
public record CleanupFinding(CleanupTarget Target, ulong MissingId, int Rows);

/// <summary>
///     Finds settings that point at deleted channels or roles and fixes them.
/// </summary>
public class ConfigCleanupService(
    IDataConnectionFactory dbFactory,
    DiscordShardedClient client,
    GuildSettingsService guildSettings) : INService
{
    private static readonly string[] LoggingColumns =
    [
        "LogOtherId", "MessageUpdatedId", "MessageDeletedId", "ThreadCreatedId", "ThreadDeletedId", "ThreadUpdatedId",
        "UsernameUpdatedId", "NicknameUpdatedId", "AvatarUpdatedId", "UserLeftId", "UserBannedId", "UserUnbannedId",
        "UserUpdatedId", "UserJoinedId", "UserRoleAddedId", "UserRoleRemovedId", "UserMutedId", "LogUserPresenceId",
        "LogVoicePresenceId", "LogVoicePresenceTtsId", "ServerUpdatedId", "RoleUpdatedId", "RoleDeletedId",
        "EventCreatedId", "RoleCreatedId", "ChannelCreatedId", "ChannelDestroyedId", "ChannelUpdatedId",
        "InviteCreatedId", "InviteDeletedId", "MessagesBulkDeletedId", "ReactionEventsId", "EmojiCreatedId",
        "EmojiUpdatedId", "EmojiDeletedId", "StickerCreatedId", "StickerUpdatedId", "StickerDeletedId",
        "UserTimedOutId", "UserTimeoutRemovedId", "InviteLinkPostedId"
    ];

    /// <summary>
    ///     Gets every setting the cleanup looks at.
    /// </summary>
    public static IReadOnlyList<CleanupTarget> Targets { get; } = BuildTargets();

    /// <summary>
    ///     Finds the settings in a guild that point at deleted channels or roles.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The findings, or null when the guild is not loaded well enough to judge.</returns>
    public async Task<List<CleanupFinding>?> ScanAsync(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null || guild.Channels.Count == 0 || guild.Roles.Count == 0)
            return null;

        var findings = new List<CleanupFinding>();
        await using var db = await dbFactory.CreateConnectionAsync();
        foreach (var target in Targets)
        {
            var guildParameter = new DataParameter("guild", (decimal)guildId, DataType.Decimal);
            if (target.Action == CleanupAction.ClearList)
            {
                var rows = await db.QueryToListAsync<ListRow>(
                    $"""SELECT "{target.Column}" AS "Value" FROM "{target.Table}" WHERE {target.GuildFilter} AND "{target.Column}" IS NOT NULL""",
                    guildParameter);
                var counts = new Dictionary<ulong, int>();
                foreach (var id in rows.SelectMany(r => ParseList(r.Value)).Where(id => !Exists(guild, target, id)))
                    counts[id] = counts.GetValueOrDefault(id) + 1;

                findings.AddRange(counts.Select(c => new CleanupFinding(target, c.Key, c.Value)));
                continue;
            }

            var found = await db.QueryToListAsync<ValueRow>(
                $"""
                 SELECT "{target.Column}"::numeric AS "Value", COUNT(*) AS "Count"
                 FROM "{target.Table}"
                 WHERE {target.GuildFilter} AND "{target.Column}" IS NOT NULL AND "{target.Column}" > 0
                 GROUP BY "{target.Column}"
                 """, guildParameter);
            findings.AddRange(found
                .Where(f => !Exists(guild, target, (ulong)f.Value))
                .Select(f => new CleanupFinding(target, (ulong)f.Value, (int)f.Count)));
        }

        return findings;
    }

    /// <summary>
    ///     Fixes the given findings.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="findings">The findings from <see cref="ScanAsync" />.</param>
    /// <returns>How many findings were fixed.</returns>
    public async Task<int> ApplyAsync(ulong guildId, IEnumerable<CleanupFinding> findings)
    {
        var fixedCount = 0;
        await using var db = await dbFactory.CreateConnectionAsync();
        foreach (var finding in findings.Where(f => f.Target.Action != CleanupAction.Report))
        {
            var target = finding.Target;
            var guildParameter = new DataParameter("guild", (decimal)guildId, DataType.Decimal);
            var idParameter = new DataParameter("id", (decimal)finding.MissingId, DataType.Decimal);

            switch (target.Action)
            {
                case CleanupAction.DeleteRow:
                    await db.ExecuteAsync(
                        $"""DELETE FROM "{target.Table}" WHERE {target.GuildFilter} AND "{target.Column}" = @id""",
                        guildParameter, idParameter);
                    break;
                case CleanupAction.Clear:
                    var unset = await IsNullableAsync(db, target) ? "NULL" : "0";
                    await db.ExecuteAsync(
                        $"""UPDATE "{target.Table}" SET "{target.Column}" = {unset} WHERE {target.GuildFilter} AND "{target.Column}" = @id""",
                        guildParameter, idParameter);
                    break;
                case CleanupAction.ClearList:
                    await ClearListAsync(db, target, guildParameter, finding.MissingId);
                    break;
            }

            fixedCount++;
        }

        guildSettings.ClearCacheForGuild(guildId);
        return fixedCount;
    }

    /// <summary>
    ///     Writes the findings as lines grouped by feature, cut to fit an embed.
    /// </summary>
    /// <param name="strings">The localized strings.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="findings">The findings.</param>
    /// <returns>The report text.</returns>
    public static string Describe(GeneratedBotStrings strings, ulong guildId, IReadOnlyList<CleanupFinding> findings)
    {
        var lines = findings
            .OrderBy(f => f.Target.Feature)
            .Select(f => strings.CleanupLine(guildId, f.Target.Feature, f.Target.Column,
                f.Target.Kind == CleanupKind.Channel
                    ? strings.CleanupKindChannel(guildId)
                    : strings.CleanupKindRole(guildId),
                f.MissingId,
                f.Target.Action switch
                {
                    CleanupAction.Clear => strings.CleanupActionClear(guildId),
                    CleanupAction.DeleteRow => strings.CleanupActionDelete(guildId),
                    CleanupAction.ClearList => strings.CleanupActionList(guildId),
                    _ => strings.CleanupActionReport(guildId)
                }))
            .ToList();

        var builder = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            if (builder.Length + lines[i].Length > 3800)
            {
                builder.Append(strings.CleanupMore(guildId, lines.Count - i));
                break;
            }

            builder.AppendLine(lines[i]);
        }

        return builder.ToString();
    }

    private static async Task ClearListAsync(DataConnection db, CleanupTarget target, DataParameter guildParameter,
        ulong missingId)
    {
        var rows = await db.QueryToListAsync<ListRow>(
            $"""SELECT "Id"::bigint AS "RowId", "{target.Column}" AS "Value" FROM "{target.Table}" WHERE {target.GuildFilter} AND "{target.Column}" IS NOT NULL""",
            guildParameter);
        foreach (var row in rows.Where(r => ParseList(r.Value).Contains(missingId)))
        {
            var kept = string.Join(' ', ParseList(row.Value).Where(id => id != missingId));
            await db.ExecuteAsync($"""UPDATE "{target.Table}" SET "{target.Column}" = @value WHERE "Id" = @rowId""",
                new DataParameter("value", kept, DataType.Text),
                new DataParameter("rowId", row.RowId, DataType.Int64));
        }
    }

    private static async Task<bool> IsNullableAsync(DataConnection db, CleanupTarget target)
    {
        var result = await db.QueryToListAsync<string>(
            """
            SELECT is_nullable FROM information_schema.columns
            WHERE table_schema = current_schema() AND table_name = @table AND column_name = @column
            """,
            new DataParameter("table", target.Table, DataType.Text),
            new DataParameter("column", target.Column, DataType.Text));
        return result.FirstOrDefault() == "YES";
    }

    private static bool Exists(SocketGuild guild, CleanupTarget target, ulong id)
    {
        return target.Kind == CleanupKind.Channel
            ? guild.GetChannel(id) != null
            : guild.GetRole(id) != null;
    }

    private static List<ulong> ParseList(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => ulong.TryParse(part, out var id) ? id : 0)
                .Where(id => id != 0)
                .ToList();
    }

    private static List<CleanupTarget> BuildTargets()
    {
        var targets = new List<CleanupTarget>();
        targets.AddRange(LoggingColumns.Select(column =>
            new CleanupTarget("Logging", "LoggingV2", column, CleanupKind.Channel, CleanupAction.Clear)));

        void Config(string feature, CleanupKind kind, params string[] columns)
        {
            targets.AddRange(columns.Select(column =>
                new CleanupTarget(feature, "GuildConfigs", column, kind, CleanupAction.Clear)));
        }

        Config("Greets", CleanupKind.Channel, "ByeMessageChannelId", "BoostMessageChannelId");
        Config("Suggestions", CleanupKind.Channel, "VotesChannel", "AcceptChannel", "DenyChannel", "ConsiderChannel",
            "ImplementChannel", "SuggestButtonChannel");
        Config("Tickets", CleanupKind.Channel, "TicketChannel");
        Config("Confessions", CleanupKind.Channel, "ConfessionChannel", "ConfessionLogChannel");
        Config("Moderation", CleanupKind.Channel, "WarnlogChannelId", "MiniWarnlogChannelId", "CommandLogChannel");
        Config("Patreon", CleanupKind.Channel, "PatreonChannelId", "PatreonGoalChannel", "PatreonStatsChannel");
        Config("Games", CleanupKind.Channel, "GameVoiceChannel");
        Config("Roles", CleanupKind.Role, "StaffRole", "MemberRole", "GiveawayPingRole");

        targets.AddRange([
            new CleanupTarget("Autoroles", "GuildConfigs", "AutoAssignRoleId", CleanupKind.Role,
                CleanupAction.ClearList),
            new CleanupTarget("Autoroles", "GuildConfigs", "AutoBotRoleIds", CleanupKind.Role,
                CleanupAction.ClearList),
            new CleanupTarget("Autoroles", "AutoBanRoles", "RoleId", CleanupKind.Role, CleanupAction.DeleteRow),
            new CleanupTarget("Self-assignable roles", "SelfAssignableRoles", "RoleId", CleanupKind.Role,
                CleanupAction.DeleteRow),
            new CleanupTarget("Greets", "MultiGreets", "ChannelId", CleanupKind.Channel, CleanupAction.DeleteRow),
            new CleanupTarget("Greets", "RoleGreets", "ChannelId", CleanupKind.Channel, CleanupAction.DeleteRow),
            new CleanupTarget("Greets", "RoleGreets", "RoleId", CleanupKind.Role, CleanupAction.DeleteRow),
            new CleanupTarget("Starboard", "Starboards", "StarboardChannelId", CleanupKind.Channel,
                CleanupAction.Clear),
            new CleanupTarget("Tickets", "GuildTicketSettings", "LogChannelId", CleanupKind.Channel,
                CleanupAction.Clear),
            new CleanupTarget("Tickets", "GuildTicketSettings", "TranscriptChannelId", CleanupKind.Channel,
                CleanupAction.Clear),
            new CleanupTarget("Tickets", "TicketPanels", "ChannelId", CleanupKind.Channel, CleanupAction.Report),
            new CleanupTarget("Role menus", "RoleMenus", "ChannelId", CleanupKind.Channel, CleanupAction.Report),
            new CleanupTarget("Role menus", "RoleMenus", "RequiredRoleId", CleanupKind.Role, CleanupAction.Clear),
            new CleanupTarget("Role menus", "RoleMenuOptions", "RoleId", CleanupKind.Role, CleanupAction.DeleteRow,
                "\"RoleMenuId\" IN (SELECT \"Id\" FROM \"RoleMenus\" WHERE \"GuildId\" = @guild)"),
            new CleanupTarget("Auto reactions", "AutoReactions", "ChannelId", CleanupKind.Channel,
                CleanupAction.DeleteRow),
            new CleanupTarget("XP", "XpRoleRewards", "RoleId", CleanupKind.Role, CleanupAction.DeleteRow),
            new CleanupTarget("XP", "XpRoleMultipliers", "RoleId", CleanupKind.Role, CleanupAction.DeleteRow),
            new CleanupTarget("XP", "XpChannelMultipliers", "ChannelId", CleanupKind.Channel,
                CleanupAction.DeleteRow),
            new CleanupTarget("XP", "GuildXpSettings", "LevelUpChannel", CleanupKind.Channel, CleanupAction.Clear),
            new CleanupTarget("Reputation", "RepRoleRewards", "RoleId", CleanupKind.Role, CleanupAction.DeleteRow),
            new CleanupTarget("Voice roles", "VcRoles", "VoiceChannelId", CleanupKind.Channel,
                CleanupAction.DeleteRow),
            new CleanupTarget("Voice roles", "VcRoles", "RoleId", CleanupKind.Role, CleanupAction.DeleteRow),
            new CleanupTarget("Votes", "VoteRoles", "RoleId", CleanupKind.Role, CleanupAction.DeleteRow),
            new CleanupTarget("Birthdays", "BirthdayConfigs", "BirthdayChannelId", CleanupKind.Channel,
                CleanupAction.Clear),
            new CleanupTarget("Birthdays", "BirthdayConfigs", "BirthdayRoleId", CleanupKind.Role,
                CleanupAction.Clear),
            new CleanupTarget("Birthdays", "BirthdayConfigs", "BirthdayPingRoleId", CleanupKind.Role,
                CleanupAction.Clear),
            new CleanupTarget("Word of the day", "WordOfTheDayConfigs", "ChannelId", CleanupKind.Channel,
                CleanupAction.Clear),
            new CleanupTarget("Word of the day", "WordOfTheDayConfigs", "PingRoleId", CleanupKind.Role,
                CleanupAction.Clear),
            new CleanupTarget("Join role timeout", "JoinRoleTimeoutSettings", "ActionRoleId", CleanupKind.Role,
                CleanupAction.Clear),
            new CleanupTarget("Join role timeout", "JoinRoleTimeoutSettings", "LogChannelId", CleanupKind.Channel,
                CleanupAction.Clear)
        ]);

        return targets;
    }

    private class ValueRow
    {
        public decimal Value { get; set; }

        public long Count { get; set; }
    }

    private class ListRow
    {
        public long RowId { get; set; }

        public string? Value { get; set; }
    }
}
