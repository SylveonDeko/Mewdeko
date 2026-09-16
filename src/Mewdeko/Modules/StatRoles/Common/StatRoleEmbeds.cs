using DataModel;
using Mewdeko.Modules.StatRoles.Services;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.StatRoles.Common;

/// <summary>
///     Builds the embeds shared by the stat role text and slash commands.
/// </summary>
public static class StatRoleEmbeds
{
    /// <summary>
    ///     Describes a stat role's condition in one line.
    /// </summary>
    /// <param name="role">The stat role.</param>
    /// <returns>The description.</returns>
    public static string Condition(StatRole role)
    {
        var stat = (StatRoleStat)role.StatType;
        var limit = (StatRoleLimit)role.LimitType;
        var window = role.LookbackDays == 0 ? "all time" : $"last {role.LookbackDays}d";
        var activity = string.IsNullOrWhiteSpace(role.ActivityName) ? "any game" : role.ActivityName;
        var unit = stat switch
        {
            StatRoleStat.Messages => "messages",
            StatRoleStat.VoiceMinutes => "voice minutes",
            StatRoleStat.Invites => "invites",
            StatRoleStat.JoinedDays => "days in server",
            StatRoleStat.ActivityMinutes => $"minutes in {activity}",
            _ => "days on Discord"
        };

        var core = limit switch
        {
            StatRoleLimit.TopRank => $"top #{role.TopStart}-{role.TopEnd} by {unit}",
            StatRoleLimit.TopPercent => $"top {role.TopStart}-{role.TopEnd}% by {unit}",
            StatRoleLimit.DailyStreak => $"{role.Minimum:N0}+ {unit} on {role.RequiredDays}+ days",
            _ => role.Maximum.HasValue
                ? $"{role.Minimum:N0}-{role.Maximum:N0} {unit}"
                : $"{role.Minimum:N0}+ {unit}"
        };

        var scoped = stat is StatRoleStat.Messages or StatRoleStat.VoiceMinutes or StatRoleStat.ActivityMinutes
            ? $"{core} ({window})"
            : core;
        return role.Invert ? $"NOT {scoped}" : scoped;
    }

    /// <summary>
    ///     Builds the list embed.
    /// </summary>
    public static EmbedBuilder List(GeneratedBotStrings strings, ulong guildId, IReadOnlyList<StatRole> roles)
    {
        var eb = new EmbedBuilder().WithOkColor().WithTitle(strings.StatRoleListTitle(guildId));
        foreach (var role in roles.Take(25))
        {
            var flags = new List<string>();
            if (!role.Enabled) flags.Add("disabled");
            if (role.Permanent) flags.Add("permanent");
            if (!string.IsNullOrWhiteSpace(role.GroupName)) flags.Add($"group: {role.GroupName}");
            var suffix = flags.Count > 0 ? $" ({string.Join(", ", flags)})" : "";
            eb.AddField($"#{role.Id} {role.Name}", $"<@&{role.RoleId}>: {Condition(role)}{suffix}");
        }

        return eb;
    }

    /// <summary>
    ///     Builds the detail embed.
    /// </summary>
    public static EmbedBuilder Info(GeneratedBotStrings strings, ulong guildId, StatRole role)
    {
        static string Ids(string? json, string prefix)
        {
            var ids = StatRoleService.ReadIds(json);
            return ids.Count == 0 ? "-" : string.Join(", ", ids.Take(15).Select(x => $"<{prefix}{x}>"));
        }

        return new EmbedBuilder()
            .WithOkColor()
            .WithTitle(strings.StatRoleInfoTitle(guildId, role.Id, role.Name))
            .AddField(strings.StatRoleRole(guildId), $"<@&{role.RoleId}>", true)
            .AddField(strings.StatRoleStatus(guildId), role.Enabled ? "Enabled" : "Disabled", true)
            .AddField(strings.StatRoleCondition(guildId), Condition(role))
            .AddField(strings.StatRoleInterval(guildId), $"{role.IntervalMinutes}m", true)
            .AddField(strings.StatRoleLastRun(guildId),
                role.LastRunAt.HasValue
                    ? TimestampTag.FromDateTime(role.LastRunAt.Value, TimestampTagStyles.Relative).ToString()
                    : "-", true)
            .AddField(strings.StatRoleFlags(guildId),
                $"permanent: {role.Permanent}, invert: {role.Invert}, bots: {role.ApplyToBots}" +
                (string.IsNullOrWhiteSpace(role.GroupName) ? "" : $", group: {role.GroupName}"))
            .AddField(strings.StatRoleChannels(guildId), Ids(role.ChannelFilter, "#"), true)
            .AddField(strings.StatRoleRoleWhitelist(guildId), Ids(role.RoleWhitelist, "@&"), true)
            .AddField(strings.StatRoleRoleBlacklist(guildId), Ids(role.RoleBlacklist, "@&"), true)
            .AddField(strings.StatRoleIgnored(guildId), Ids(role.IgnoredUsers, "@"), true)
            .AddField(strings.StatRoleNotify(guildId),
                (role.NotifyChannelId.HasValue ? $"<#{role.NotifyChannelId}>" : "-") +
                (role.NotifyDm ? " + DM" : ""), true)
            .AddField(strings.StatRoleMessage(guildId),
                string.IsNullOrWhiteSpace(role.NotifyMessage)
                    ? StatRoleService.DefaultNotifyMessage
                    : role.NotifyMessage);
    }

    /// <summary>
    ///     Builds the run or preview result embed.
    /// </summary>
    public static EmbedBuilder Result(GeneratedBotStrings strings, ulong guildId, StatRole role,
        StatRoleRunResult result, bool preview)
    {
        var unit = (StatRoleStat)role.StatType == StatRoleStat.VoiceMinutes ? "m" : "";

        string Sample(IReadOnlyList<StatRoleMember> members)
        {
            return members.Count == 0
                ? "-"
                : string.Join(", ", members.Take(10).Select(m => $"<@{m.UserId}> ({m.Value:N0}{unit})")) +
                  (members.Count > 10 ? $" +{members.Count - 10}" : "");
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(preview
                ? strings.StatRolePreviewTitle(guildId, role.Name)
                : strings.StatRoleRunTitle(guildId, role.Name))
            .WithDescription(Condition(role))
            .AddField(strings.StatRoleQualifying(guildId), result.Qualifying.Count.ToString("N0"), true)
            .AddField(strings.StatRoleWouldGrant(guildId), result.ToGrant.Count.ToString("N0"), true)
            .AddField(strings.StatRoleWouldRemove(guildId), result.ToRemove.Count.ToString("N0"), true)
            .AddField(strings.StatRoleGrantList(guildId), Sample(result.ToGrant))
            .AddField(strings.StatRoleRemoveList(guildId), Sample(result.ToRemove));

        if (!preview)
        {
            eb.AddField(strings.StatRoleApplied(guildId),
                strings.StatRoleAppliedValue(guildId, result.Granted, result.Removed, result.Failed));
        }

        return eb;
    }
}