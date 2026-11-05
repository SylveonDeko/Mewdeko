using System.Text.RegularExpressions;
using DataModel;
using Mewdeko.Common.TypeReaders.Models;

namespace Mewdeko.Modules.Bridge.Common;

/// <summary>
///     The per-bridge options one side of a bridge stores for itself.
/// </summary>
/// <param name="Direction">Which way messages flow on this side.</param>
/// <param name="AllowedRoleIds">Roles allowed to be relayed outward. Empty means everyone.</param>
/// <param name="BlockedRoleIds">Roles that are never relayed outward.</param>
/// <param name="BlockedUserIds">Users that are never relayed outward.</param>
/// <param name="MinMemberAgeMinutes">Minutes a member must have been in the community. 0 is off.</param>
/// <param name="BlockInvites">Whether outgoing messages with invite links are dropped.</param>
/// <param name="UseWordFilter">Whether outgoing messages caught by the word filter are dropped.</param>
/// <param name="NameFormat">How incoming authors are named on this side's webhook.</param>
public sealed record BridgeOptions(
    BridgeDirection Direction,
    IReadOnlyList<ulong> AllowedRoleIds,
    IReadOnlyList<ulong> BlockedRoleIds,
    IReadOnlyList<ulong> BlockedUserIds,
    int MinMemberAgeMinutes,
    bool BlockInvites,
    bool UseWordFilter,
    string NameFormat);

/// <summary>
///     Helpers for the per-bridge options.
/// </summary>
public static partial class BridgeSettings
{
    /// <summary>The name format that shows the author's name unchanged.</summary>
    public const string DefaultNameFormat = "{name}";

    /// <summary>The longest name format that is accepted.</summary>
    public const int MaxNameFormatLength = 80;

    /// <summary>The longest minimum member age that is accepted, in minutes (365 days).</summary>
    public const int MaxMinAgeMinutes = 525600;

    /// <summary>The error code for a name format without <c>{name}</c> or that is too long.</summary>
    public const string InvalidNameFormat = "invalid_name_format";

    /// <summary>The error code for a minimum member age outside its range.</summary>
    public const string InvalidMinAge = "invalid_min_age";

    /// <summary>Reads the options of a stored bridge.</summary>
    /// <param name="bridge">The bridge.</param>
    /// <returns>Its options.</returns>
    public static BridgeOptions OptionsOf(this ChannelBridge bridge) => new(
        (BridgeDirection)bridge.Direction, ParseIds(bridge.AllowedRoleIds), ParseIds(bridge.BlockedRoleIds),
        ParseIds(bridge.BlockedUserIds), bridge.MinMemberAgeMinutes, bridge.BlockInvites, bridge.UseWordFilter,
        bridge.NameFormat);

    /// <summary>Copies options onto a stored bridge.</summary>
    /// <param name="bridge">The bridge.</param>
    /// <param name="options">The options.</param>
    public static void Apply(this ChannelBridge bridge, BridgeOptions options)
    {
        bridge.Direction = (int)options.Direction;
        bridge.AllowedRoleIds = JoinIds(options.AllowedRoleIds);
        bridge.BlockedRoleIds = JoinIds(options.BlockedRoleIds);
        bridge.BlockedUserIds = JoinIds(options.BlockedUserIds);
        bridge.MinMemberAgeMinutes = options.MinMemberAgeMinutes;
        bridge.BlockInvites = options.BlockInvites;
        bridge.UseWordFilter = options.UseWordFilter;
        bridge.NameFormat = options.NameFormat;
    }

    /// <summary>Checks options.</summary>
    /// <param name="options">The options.</param>
    /// <returns>An error code, or null when they are valid.</returns>
    public static string? Validate(BridgeOptions options)
    {
        if (!IsValidNameFormat(options.NameFormat))
            return InvalidNameFormat;

        return options.MinMemberAgeMinutes is < 0 or > MaxMinAgeMinutes ? InvalidMinAge : null;
    }

    /// <summary>Checks a name format.</summary>
    /// <param name="format">The format.</param>
    /// <returns>True when it contains <c>{name}</c> and is short enough.</returns>
    public static bool IsValidNameFormat(string? format) =>
        format is not null && format.Length <= MaxNameFormatLength
                           && format.Contains("{name}", StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets the direction the other side must store.</summary>
    /// <param name="direction">This side's direction.</param>
    /// <returns>The inverse direction.</returns>
    public static BridgeDirection Inverse(this BridgeDirection direction) => direction switch
    {
        BridgeDirection.SendOnly => BridgeDirection.ReceiveOnly,
        BridgeDirection.ReceiveOnly => BridgeDirection.SendOnly,
        _ => BridgeDirection.Both
    };

    /// <summary>Splits a space-separated id list.</summary>
    /// <param name="text">The stored text.</param>
    /// <returns>The ids.</returns>
    public static List<ulong> ParseIds(string? text) =>
        (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => ulong.TryParse(x, out var id) ? id : 0)
        .Where(x => x != 0)
        .Distinct()
        .ToList();

    /// <summary>Joins ids into a space-separated list.</summary>
    /// <param name="ids">The ids.</param>
    /// <returns>The text to store.</returns>
    public static string JoinIds(IEnumerable<ulong> ids) => string.Join(' ', ids.Distinct());

    /// <summary>Names a platform for people.</summary>
    /// <param name="platform">A platform name from <see cref="BridgeProtocol" />.</param>
    /// <returns>Discord or Fluxer.</returns>
    public static string PlatformLabel(string platform) =>
        platform == BridgeProtocol.Fluxer ? "Fluxer" : "Discord";

    /// <summary>Builds the name an incoming author is posted under, before it is sanitised.</summary>
    /// <param name="format">The bridge's name format.</param>
    /// <param name="name">The author's display name.</param>
    /// <param name="originPlatform">The platform the message came from.</param>
    /// <param name="community">The origin community's name.</param>
    /// <returns>The formatted name.</returns>
    public static string FormatName(string? format, string name, string originPlatform, string community)
    {
        var template = string.IsNullOrWhiteSpace(format) ? DefaultNameFormat : format;
        return template
            .Replace("{name}", name, StringComparison.OrdinalIgnoreCase)
            .Replace("{platform}", PlatformLabel(originPlatform), StringComparison.OrdinalIgnoreCase)
            .Replace("{community}", community, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Reads a user from a mention or an id.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The user id, or null.</returns>
    public static ulong? ParseUserId(string? text)
    {
        var match = IdRegex().Match(text ?? "");
        return match.Success && ulong.TryParse(match.Value, out var id) && id != 0 ? id : null;
    }

    /// <summary>Reads roles from mentions, ids or names.</summary>
    /// <param name="text">The text, separated by spaces or commas.</param>
    /// <param name="roles">The roles of the community.</param>
    /// <returns>The roles that were found.</returns>
    public static List<ulong> ParseRoleIds(string? text, IReadOnlyCollection<(ulong Id, string Name)> roles)
    {
        var found = new List<ulong>();
        foreach (var token in (text ?? "").Split([' ', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            var id = ParseUserId(token);
            if (id is { } parsed && roles.Any(x => x.Id == parsed))
                found.Add(parsed);
            else if (roles.FirstOrDefault(x => x.Name.TrimStart('@').Equals(token.TrimStart('@'),
                         StringComparison.OrdinalIgnoreCase)) is { Id: not 0 } named)
                found.Add(named.Id);
        }

        return found.Distinct().ToList();
    }

    /// <summary>Reads a minimum member age such as 10m, 2h or 7d.</summary>
    /// <param name="text">The text, or off.</param>
    /// <param name="minutes">The age in minutes.</param>
    /// <returns>True when the text was a valid age.</returns>
    public static bool TryParseMinAge(string? text, out int minutes)
    {
        minutes = 0;
        var value = (text ?? "").Trim();
        if (value.Equals("off", StringComparison.OrdinalIgnoreCase) || value == "0")
            return true;

        try
        {
            var total = Math.Ceiling(StoopidTime.FromInput(value.ToLowerInvariant()).Time.TotalMinutes);
            if (total is < 0 or > MaxMinAgeMinutes)
                return false;

            minutes = (int)total;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"\d{5,20}")]
    private static partial Regex IdRegex();
}
