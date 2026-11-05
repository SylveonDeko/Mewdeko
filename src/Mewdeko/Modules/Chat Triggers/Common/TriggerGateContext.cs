namespace Mewdeko.Modules.Chat_Triggers.Common;

/// <summary>
///     The member and channel a trigger run is checked against by the <c>{require}</c> and <c>{blacklist}</c> blocks.
/// </summary>
public sealed class TriggerGateContext
{
    private readonly Dictionary<string, ulong> channelNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<ulong, string> channels;
    private readonly Dictionary<string, ulong> roleNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<ulong, string> roles;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TriggerGateContext" /> class.
    /// </summary>
    /// <param name="roles">Every role in the guild, by id.</param>
    /// <param name="channels">Every channel in the guild, by id.</param>
    /// <param name="memberRoleIds">The roles the member holds.</param>
    /// <param name="channelIds">The channel the trigger ran in, and its parent when it is a thread.</param>
    /// <param name="isStaff">Whether the member counts as staff, who can bypass the gates with <c>{override}</c>.</param>
    public TriggerGateContext(IReadOnlyDictionary<ulong, string> roles, IReadOnlyDictionary<ulong, string> channels,
        IReadOnlySet<ulong> memberRoleIds, IReadOnlySet<ulong> channelIds, bool isStaff)
    {
        this.roles = roles;
        this.channels = channels;
        MemberRoleIds = memberRoleIds;
        ChannelIds = channelIds;
        IsStaff = isStaff;

        foreach (var (id, name) in roles)
            roleNames.TryAdd(name, id);

        foreach (var (id, name) in channels)
            channelNames.TryAdd(name, id);
    }

    /// <summary>
    ///     Gets the roles the member holds.
    /// </summary>
    public IReadOnlySet<ulong> MemberRoleIds { get; }

    /// <summary>
    ///     Gets the channel the trigger ran in, and its parent when it is a thread.
    /// </summary>
    public IReadOnlySet<ulong> ChannelIds { get; }

    /// <summary>
    ///     Gets a value indicating whether the member counts as staff.
    /// </summary>
    public bool IsStaff { get; }

    /// <summary>
    ///     Works out whether a listed item is a role or a channel.
    /// </summary>
    /// <param name="item">A role or channel id, name, or mention.</param>
    /// <param name="isRole">Whether the item is a role.</param>
    /// <param name="id">The id of the role or channel.</param>
    /// <returns>True when the item matches a role or channel of the guild.</returns>
    public bool TryResolve(string item, out bool isRole, out ulong id)
    {
        item = item.Trim();
        var preferChannel = item.StartsWith('#') || item.StartsWith("<#", StringComparison.Ordinal);
        item = item.Trim('<', '>', '@', '&', '#', ' ');

        if (ulong.TryParse(item, out id))
        {
            isRole = roles.ContainsKey(id);
            return isRole || channels.ContainsKey(id);
        }

        var hasRole = roleNames.TryGetValue(item, out var roleId);
        var hasChannel = channelNames.TryGetValue(item, out var channelId);

        isRole = hasRole && !(hasChannel && preferChannel);
        id = isRole ? roleId : channelId;
        return hasRole || hasChannel;
    }
}
