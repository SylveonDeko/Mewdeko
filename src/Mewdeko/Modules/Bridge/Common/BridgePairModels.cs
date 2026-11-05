namespace Mewdeko.Modules.Bridge.Common;

/// <summary>
///     A community paired with a community on the other platform, as the dashboard sees it.
/// </summary>
public sealed class BridgePairResponse
{
    /// <summary>
    ///     The id both sides of the pair share.
    /// </summary>
    public Guid PairId { get; set; }

    /// <summary>
    ///     The paired community on the other platform.
    /// </summary>
    public ulong RemoteGuildId { get; set; }

    /// <summary>
    ///     The name of the paired community.
    /// </summary>
    public string RemoteGuildName { get; set; } = "";

    /// <summary>
    ///     How incoming proposals are handled: <c>Approve</c> or <c>Auto</c>.
    /// </summary>
    public string ApprovalMode { get; set; } = "Approve";

    /// <summary>
    ///     The channels hidden from the pair.
    /// </summary>
    public ulong[] HiddenChannelIds { get; set; } = [];

    /// <summary>
    ///     This community's text channels and whether each is shared with the pair.
    /// </summary>
    public List<BridgeLocalChannelResponse> LocalChannels { get; set; } = [];

    /// <summary>
    ///     The channels the other community shares with this one.
    /// </summary>
    public List<BridgeRemoteChannelResponse> RemoteChannels { get; set; } = [];

    /// <summary>
    ///     Proposals from the other side that wait for a decision here.
    /// </summary>
    public List<BridgeProposalResponse> IncomingProposals { get; set; } = [];

    /// <summary>
    ///     Proposals made here that wait for the other side.
    /// </summary>
    public List<BridgeProposalResponse> OutgoingProposals { get; set; } = [];

    /// <summary>
    ///     When the pair was made.
    /// </summary>
    public DateTime DateAdded { get; set; }
}

/// <summary>
///     One of this community's text channels.
/// </summary>
public sealed class BridgeLocalChannelResponse
{
    /// <summary>
    ///     The channel id.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     The channel name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The category the channel is in, or empty.
    /// </summary>
    public string Category { get; set; } = "";

    /// <summary>
    ///     Whether the channel is shared with the pair.
    /// </summary>
    public bool Shared { get; set; }
}

/// <summary>
///     A channel the other community shares.
/// </summary>
public sealed class BridgeRemoteChannelResponse
{
    /// <summary>
    ///     The channel id.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    ///     The channel name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     The category the channel is in, or empty.
    /// </summary>
    public string Category { get; set; } = "";
}

/// <summary>
///     A pending proposal to bridge two channels.
/// </summary>
public sealed class BridgeProposalResponse
{
    /// <summary>
    ///     The proposal id.
    /// </summary>
    public Guid ProposalId { get; set; }

    /// <summary>
    ///     The channel in this community.
    /// </summary>
    public ulong LocalChannelId { get; set; }

    /// <summary>
    ///     The name of the channel in this community.
    /// </summary>
    public string LocalChannelName { get; set; } = "";

    /// <summary>
    ///     The channel in the other community.
    /// </summary>
    public ulong RemoteChannelId { get; set; }

    /// <summary>
    ///     The name of the channel in the other community.
    /// </summary>
    public string RemoteChannelName { get; set; } = "";

    /// <summary>
    ///     When the proposal was made.
    /// </summary>
    public DateTime DateAdded { get; set; }
}

/// <summary>
///     Asks for a pair code.
/// </summary>
public sealed class BridgePairCodeRequest
{
    /// <summary>
    ///     The dashboard user asking, who must be allowed to manage the community.
    /// </summary>
    public ulong UserId { get; set; }
}

/// <summary>
///     Pairs the community with the one a code was made for.
/// </summary>
public sealed class BridgePairLinkRequest
{
    /// <summary>
    ///     The pair code from the other platform.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    ///     The dashboard user asking, who must be allowed to manage the community.
    /// </summary>
    public ulong UserId { get; set; }
}

/// <summary>
///     Replaces the settings of a pair. Both fields are required.
/// </summary>
public sealed class BridgePairSettingsRequest
{
    /// <summary>
    ///     <c>Approve</c> to approve each bridge or <c>Auto</c> to accept them automatically.
    /// </summary>
    public string? ApprovalMode { get; set; }

    /// <summary>
    ///     The channels to hide from the pair.
    /// </summary>
    public ulong[]? HiddenChannelIds { get; set; }
}

/// <summary>
///     Proposes bridging a local channel to a channel the other side shares.
/// </summary>
public sealed class BridgePairConnectRequest
{
    /// <summary>
    ///     The channel in this community.
    /// </summary>
    public ulong? LocalChannelId { get; set; }

    /// <summary>
    ///     The shared channel in the other community.
    /// </summary>
    public ulong? RemoteChannelId { get; set; }

    /// <summary>
    ///     The dashboard user asking, who must be allowed to manage the community.
    /// </summary>
    public ulong UserId { get; set; }
}

/// <summary>
///     The dashboard user accepting a proposal.
/// </summary>
public sealed class BridgeProposalAcceptRequest
{
    /// <summary>
    ///     The dashboard user asking, who must be allowed to manage the community.
    /// </summary>
    public ulong UserId { get; set; }
}

/// <summary>
///     The outcome of connecting two channels through a pair.
/// </summary>
public sealed class BridgePairConnectResponse
{
    /// <summary>
    ///     <c>connected</c> when the other side accepts automatically and the handshake was sent, otherwise <c>proposed</c>.
    /// </summary>
    public string Status { get; set; } = "proposed";

    /// <summary>
    ///     The proposal id.
    /// </summary>
    public Guid? ProposalId { get; set; }
}
