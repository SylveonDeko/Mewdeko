using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mewdeko.Modules.Bridge.Common;

/// <summary>
///     The wire format the Discord and Fluxer bots use to relay bridged channels to each other through the shared
///     Redis. Both bots carry an identical copy of this file; change both together and bump <see cref="Version" />
///     when a change is not backwards compatible.
/// </summary>
public static class BridgeProtocol
{
    /// <summary>The protocol version stamped on every envelope.</summary>
    public const int Version = 1;

    /// <summary>The platform name of the Discord bot.</summary>
    public const string Discord = "discord";

    /// <summary>The platform name of the Fluxer bot.</summary>
    public const string Fluxer = "fluxer";

    /// <summary>Shared Redis namespace. Deliberately not prefixed per bot token, since both bots read it.</summary>
    public const string KeyPrefix = "mewdeko_bridge";

    /// <summary>The consumer group each bot reads its inbound stream with.</summary>
    public const string ConsumerGroup = "bridge";

    /// <summary>The single stream entry field holding the serialised envelope.</summary>
    public const string StreamField = "data";

    /// <summary>How long a link code stays valid.</summary>
    public static readonly TimeSpan LinkCodeLifetime = TimeSpan.FromMinutes(15);

    /// <summary>How long a pair code stays valid.</summary>
    public static readonly TimeSpan PairCodeLifetime = TimeSpan.FromMinutes(15);

    /// <summary>How long a bridge proposal waits for an answer before it expires.</summary>
    public static readonly TimeSpan ProposalLifetime = TimeSpan.FromDays(7);

    /// <summary>How long shared channel lists are held back after a change so a burst of changes is published once.</summary>
    public static readonly TimeSpan ChannelsDebounce = TimeSpan.FromSeconds(4);

    /// <summary>How long one side keeps the other side's channel list before reading it again.</summary>
    public static readonly TimeSpan ChannelsCacheLifetime = TimeSpan.FromSeconds(60);

    /// <summary>How long the original-to-copy message map is kept, which bounds how old a message can still be edited, deleted or replied to across the bridge.</summary>
    public static readonly TimeSpan MessageMapLifetime = TimeSpan.FromDays(7);

    /// <summary>Envelopes older than this when they are read are dropped, so a bot coming back from downtime does not replay a flood.</summary>
    public static readonly TimeSpan MaxEnvelopeAge = TimeSpan.FromMinutes(10);

    /// <summary>Approximate length cap for each inbound stream.</summary>
    public const int StreamMaxLength = 10_000;

    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>JSON options shared by both bots.</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
    };

    /// <summary>The other platform's name.</summary>
    /// <param name="platform">A platform name.</param>
    /// <returns>The opposite platform.</returns>
    public static string Other(string platform) => platform == Discord ? Fluxer : Discord;

    /// <summary>The stream a platform reads its inbound envelopes from.</summary>
    /// <param name="platform">The reading platform.</param>
    /// <returns>The stream key.</returns>
    public static string InboundStream(string platform) => $"{KeyPrefix}:stream:{platform}";

    /// <summary>The pub/sub channel used to wake a platform's reader as soon as something is appended for it.</summary>
    /// <param name="platform">The reading platform.</param>
    /// <returns>The channel name.</returns>
    public static string WakeChannel(string platform) => $"{KeyPrefix}:wake:{platform}";

    /// <summary>The key a link code is stored under.</summary>
    /// <param name="code">A normalised code.</param>
    /// <returns>The key.</returns>
    public static string LinkCodeKey(string code) => $"{KeyPrefix}:code:{code}";

    /// <summary>The key a pair code is stored under. Pair codes never share keys with link codes.</summary>
    /// <param name="code">A normalised code.</param>
    /// <returns>The key.</returns>
    public static string PairCodeKey(string code) => $"{KeyPrefix}:paircode:{code}";

    /// <summary>The key holding the channels a community shares with one pair, as a JSON list of <see cref="BridgeSharedChannel" />.</summary>
    /// <param name="platform">The platform that owns the community.</param>
    /// <param name="guildId">The sharing community.</param>
    /// <param name="pairId">The pair.</param>
    /// <returns>The key.</returns>
    public static string ChannelsKey(string platform, ulong guildId, Guid pairId)
        => $"{KeyPrefix}:channels:{platform}:{guildId}:{pairId}";

    /// <summary>The key holding a community's approval mode for one pair, <c>0</c> to approve each bridge or <c>1</c> for automatic.</summary>
    /// <param name="platform">The platform that owns the community.</param>
    /// <param name="guildId">The community.</param>
    /// <param name="pairId">The pair.</param>
    /// <returns>The key.</returns>
    public static string PairModeKey(string platform, ulong guildId, Guid pairId)
        => $"{KeyPrefix}:pairmode:{platform}:{guildId}:{pairId}";

    /// <summary>The key mapping a message on one side of a bridge to its copy on the other.</summary>
    /// <param name="bridgeId">The bridge.</param>
    /// <param name="platform">The platform the message id belongs to.</param>
    /// <param name="messageId">The message id.</param>
    /// <returns>The key.</returns>
    public static string MessageMapKey(Guid bridgeId, string platform, ulong messageId)
        => $"{KeyPrefix}:map:{bridgeId:N}:{platform}:{messageId}";

    /// <summary>Generates an 8 character link code from an unambiguous alphabet.</summary>
    /// <returns>The code.</returns>
    public static string NewLinkCode()
        => string.Create(8, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        });

    /// <summary>Uppercases a typed code and strips spaces and dashes.</summary>
    /// <param name="code">The code as typed.</param>
    /// <returns>The normalised code.</returns>
    public static string NormalizeCode(string code)
        => new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    /// <summary>Serialises an envelope.</summary>
    public static string Serialize(BridgeEnvelope envelope) => JsonSerializer.Serialize(envelope, Json);

    /// <summary>Parses an envelope, or returns null when it is malformed or from a newer protocol version.</summary>
    public static BridgeEnvelope? Deserialize(string json)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<BridgeEnvelope>(json, Json);
            return envelope is { Version: <= Version } ? envelope : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>The kinds of envelope sent across a bridge.</summary>
public static class BridgeEnvelopeType
{
    /// <summary>A new message to relay.</summary>
    public const string Message = "message";

    /// <summary>A relayed message was edited.</summary>
    public const string Edit = "edit";

    /// <summary>A relayed message was deleted.</summary>
    public const string Delete = "delete";

    /// <summary>A reaction was added to a relayed message.</summary>
    public const string ReactionAdd = "reaction_add";

    /// <summary>A reaction was removed from a relayed message.</summary>
    public const string ReactionRemove = "reaction_remove";

    /// <summary>The other side accepted a link code; the creating side stores its half of the bridge.</summary>
    public const string LinkAccepted = "link_accepted";

    /// <summary>A bridge was removed on one side; the other side removes its half.</summary>
    public const string Unlinked = "unlinked";

    /// <summary>A bridge was paused or resumed on one side; the other side mirrors the state.</summary>
    public const string PauseChanged = "pause_changed";

    /// <summary>The direction of a bridge was changed on one side; the other side stores the inverse.</summary>
    public const string DirectionChanged = "direction_changed";

    /// <summary>The other side accepted a pair code; the creating side stores its half of the pair.</summary>
    public const string PairAccepted = "pair_accepted";

    /// <summary>A pair was removed on one side; the other side removes its half.</summary>
    public const string Unpaired = "unpaired";

    /// <summary>One side proposes bridging one of its channels to a channel the other side shares.</summary>
    public const string BridgeProposed = "bridge_proposed";

    /// <summary>The target side declined a proposal, or could not complete it.</summary>
    public const string BridgeProposalDeclined = "bridge_proposal_declined";

    /// <summary>The proposing side withdrew a proposal.</summary>
    public const string BridgeProposalCancelled = "bridge_proposal_cancelled";

    /// <summary>The channels one side shares with a pair changed; the other side drops any list it kept.</summary>
    public const string ChannelsUpdated = "channels_updated";

    /// <summary>Whether an envelope is about a pair rather than a bridge, so it carries no bridge id.</summary>
    /// <param name="type">The envelope type.</param>
    /// <returns>True for pair, proposal and channel list envelopes.</returns>
    public static bool IsPairLevel(string type)
        => type is PairAccepted or Unpaired or BridgeProposed or BridgeProposalDeclined or BridgeProposalCancelled
            or ChannelsUpdated;
}

/// <summary>Which way messages flow on one side of a bridge.</summary>
public enum BridgeDirection
{
    /// <summary>Messages are sent and received.</summary>
    Both = 0,

    /// <summary>This side's messages go out and nothing comes in.</summary>
    SendOnly = 1,

    /// <summary>Messages come in and nothing goes out.</summary>
    ReceiveOnly = 2
}

/// <summary>One unit sent across the bridge. Only the payload matching <see cref="Type" /> is set.</summary>
public sealed class BridgeEnvelope
{
    /// <summary>Protocol version of the sender.</summary>
    public int Version { get; set; } = BridgeProtocol.Version;

    /// <summary>One of <see cref="BridgeEnvelopeType" />.</summary>
    public string Type { get; set; } = "";

    /// <summary>The bridge this envelope belongs to.</summary>
    public Guid BridgeId { get; set; }

    /// <summary>The platform that sent it.</summary>
    public string Origin { get; set; } = "";

    /// <summary>When the sender created it.</summary>
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The message on the origin side this envelope is about, for message, edit, delete and reaction envelopes.</summary>
    public ulong OriginMessageId { get; set; }

    /// <summary>Set for <see cref="BridgeEnvelopeType.Message" /> and <see cref="BridgeEnvelopeType.Edit" />.</summary>
    public BridgeMessage? Message { get; set; }

    /// <summary>Set for reaction envelopes.</summary>
    public BridgeReaction? Reaction { get; set; }

    /// <summary>Set for <see cref="BridgeEnvelopeType.LinkAccepted" />.</summary>
    public BridgeLinkAccepted? Link { get; set; }

    /// <summary>Set for <see cref="BridgeEnvelopeType.PauseChanged" />.</summary>
    public bool? Paused { get; set; }

    /// <summary>Set for <see cref="BridgeEnvelopeType.DirectionChanged" />: the sender's new direction.</summary>
    public BridgeDirection? Direction { get; set; }

    /// <summary>
    ///     The pair the envelope is about. Set on every pair-level envelope, and on <see cref="BridgeEnvelopeType.LinkAccepted" />
    ///     when the bridge was made through a pair.
    /// </summary>
    public Guid? PairId { get; set; }

    /// <summary>
    ///     The proposal the envelope is about. Set for proposal envelopes, and on <see cref="BridgeEnvelopeType.LinkAccepted" />
    ///     when it finishes a proposal.
    /// </summary>
    public Guid? ProposalId { get; set; }

    /// <summary>Set for <see cref="BridgeEnvelopeType.Unpaired" />: whether the bridges made through the pair stay.</summary>
    public bool? KeepBridges { get; set; }

    /// <summary>Set for <see cref="BridgeEnvelopeType.BridgeProposalDeclined" />: why it was declined, such as <c>declined</c> or <c>missing_permission</c>.</summary>
    public string? Reason { get; set; }

    /// <summary>Set for <see cref="BridgeEnvelopeType.PairAccepted" />.</summary>
    public BridgePairAccepted? Pair { get; set; }

    /// <summary>Set for <see cref="BridgeEnvelopeType.BridgeProposed" />.</summary>
    public BridgeProposalInfo? Proposal { get; set; }
}

/// <summary>A message in platform-neutral form.</summary>
public sealed class BridgeMessage
{
    /// <summary>The name to post under: the member's display name on the origin side.</summary>
    public string AuthorName { get; set; } = "";

    /// <summary>A public avatar URL for the author, if any.</summary>
    public string? AuthorAvatarUrl { get; set; }

    /// <summary>Whether the author is a bot.</summary>
    public bool AuthorIsBot { get; set; }

    /// <summary>Text with mentions already turned into plain names and custom emoji into <c>:name:</c>.</summary>
    public string Content { get; set; } = "";

    /// <summary>Files to re-upload or link.</summary>
    public List<BridgeAttachment> Attachments { get; set; } = [];

    /// <summary>Rich embeds sent by bots or integrations.</summary>
    public List<BridgeEmbed> Embeds { get; set; } = [];

    /// <summary>Sticker names, shown as text since stickers do not carry over.</summary>
    public List<string> Stickers { get; set; } = [];

    /// <summary>What this message replies to, if anything.</summary>
    public BridgeReply? ReplyTo { get; set; }
}

/// <summary>A file attached to a bridged message.</summary>
public sealed class BridgeAttachment
{
    /// <summary>A URL the receiving bot can download it from.</summary>
    public string Url { get; set; } = "";

    /// <summary>The file name.</summary>
    public string Filename { get; set; } = "";

    /// <summary>Size in bytes, used to decide between re-uploading and linking.</summary>
    public long Size { get; set; }

    /// <summary>MIME type when known.</summary>
    public string? ContentType { get; set; }

    /// <summary>Whether it was marked as a spoiler.</summary>
    public bool Spoiler { get; set; }
}

/// <summary>A simplified embed.</summary>
public sealed class BridgeEmbed
{
    /// <summary>Title.</summary>
    public string? Title { get; set; }

    /// <summary>Description.</summary>
    public string? Description { get; set; }

    /// <summary>Title link.</summary>
    public string? Url { get; set; }

    /// <summary>Side colour as RGB.</summary>
    public uint? Color { get; set; }

    /// <summary>Author line.</summary>
    public string? AuthorName { get; set; }

    /// <summary>Author icon.</summary>
    public string? AuthorIconUrl { get; set; }

    /// <summary>Footer text.</summary>
    public string? Footer { get; set; }

    /// <summary>Footer icon.</summary>
    public string? FooterIconUrl { get; set; }

    /// <summary>Large image.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Thumbnail.</summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>Timestamp shown in the footer.</summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>Fields.</summary>
    public List<BridgeEmbedField> Fields { get; set; } = [];
}

/// <summary>An embed field.</summary>
public sealed class BridgeEmbedField
{
    /// <summary>Name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Value.</summary>
    public string Value { get; set; } = "";

    /// <summary>Whether it sits inline.</summary>
    public bool Inline { get; set; }
}

/// <summary>The message a bridged message replies to.</summary>
public sealed class BridgeReply
{
    /// <summary>The replied-to message on the origin side; the receiver looks up its copy through the message map.</summary>
    public ulong OriginMessageId { get; set; }

    /// <summary>The replied-to author's name, for the quote fallback.</summary>
    public string AuthorName { get; set; } = "";

    /// <summary>A short plain-text snippet of the replied-to message, for the quote fallback.</summary>
    public string Snippet { get; set; } = "";
}

/// <summary>A reaction on a bridged message.</summary>
public sealed class BridgeReaction
{
    /// <summary>A unicode emoji, or the custom emoji's name.</summary>
    public string Emoji { get; set; } = "";

    /// <summary>The custom emoji id, null for unicode emoji.</summary>
    public ulong? CustomEmojiId { get; set; }

    /// <summary>Whether the custom emoji is animated.</summary>
    public bool Animated { get; set; }

    /// <summary>
    ///     How many people on the origin side now have this reaction. The receiver adds the bot's reaction when this
    ///     goes from zero and removes it when it returns to zero.
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
///     Sent to the side that created a link code once the other side accepted it. The accepting side removes the
///     code from Redis atomically when it accepts, so it carries the original request back to the creator.
/// </summary>
public sealed class BridgeLinkAccepted
{
    /// <summary>The creator's original request: which community and channel to bridge and who asked.</summary>
    public BridgeLinkCode Request { get; set; } = new();

    /// <summary>The accepting community.</summary>
    public ulong GuildId { get; set; }

    /// <summary>The accepting channel.</summary>
    public ulong ChannelId { get; set; }

    /// <summary>The accepting community's name, for display.</summary>
    public string GuildName { get; set; } = "";

    /// <summary>The accepting channel's name, for display.</summary>
    public string ChannelName { get; set; } = "";

    /// <summary>Who accepted it.</summary>
    public ulong AcceptedByUserId { get; set; }
}

/// <summary>A pending link code, stored under <see cref="BridgeProtocol.LinkCodeKey" />.</summary>
public sealed class BridgeLinkCode
{
    /// <summary>The code.</summary>
    public string Code { get; set; } = "";

    /// <summary>The platform that created it.</summary>
    public string Platform { get; set; } = "";

    /// <summary>The creating community.</summary>
    public ulong GuildId { get; set; }

    /// <summary>The channel to bridge on the creating side.</summary>
    public ulong ChannelId { get; set; }

    /// <summary>The creating community's name, for display on the accepting side.</summary>
    public string GuildName { get; set; } = "";

    /// <summary>The creating channel's name, for display on the accepting side.</summary>
    public string ChannelName { get; set; } = "";

    /// <summary>Who created it; they were checked for permission on the creating side.</summary>
    public ulong CreatedByUserId { get; set; }

    /// <summary>When it was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A pending pair code, stored under <see cref="BridgeProtocol.PairCodeKey" />.</summary>
public sealed class BridgePairCode
{
    /// <summary>The code.</summary>
    public string Code { get; set; } = "";

    /// <summary>The platform that created it.</summary>
    public string Platform { get; set; } = "";

    /// <summary>The creating community.</summary>
    public ulong GuildId { get; set; }

    /// <summary>The creating community's name, for display on the accepting side.</summary>
    public string GuildName { get; set; } = "";

    /// <summary>Who created it; they were checked for permission on the creating side.</summary>
    public ulong CreatedByUserId { get; set; }

    /// <summary>When it was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When it stops working.</summary>
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow + BridgeProtocol.PairCodeLifetime;
}

/// <summary>
///     Sent to the side that created a pair code once the other side accepted it. The accepting side removes the code
///     from Redis atomically when it accepts, so it carries the original request back to the creator.
/// </summary>
public sealed class BridgePairAccepted
{
    /// <summary>The creator's original request.</summary>
    public BridgePairCode Request { get; set; } = new();

    /// <summary>The accepting community.</summary>
    public ulong GuildId { get; set; }

    /// <summary>The accepting community's name, for display.</summary>
    public string GuildName { get; set; } = "";

    /// <summary>Who accepted it.</summary>
    public ulong AcceptedByUserId { get; set; }
}

/// <summary>A proposal to bridge one of the proposer's channels to a channel the target side shares.</summary>
public sealed class BridgeProposalInfo
{
    /// <summary>The proposer's channel.</summary>
    public ulong ChannelId { get; set; }

    /// <summary>The proposer's channel name, for display.</summary>
    public string ChannelName { get; set; } = "";

    /// <summary>The channel on the target side to bridge to.</summary>
    public ulong TargetChannelId { get; set; }

    /// <summary>The proposing community's name, for display.</summary>
    public string GuildName { get; set; } = "";

    /// <summary>Who proposed it.</summary>
    public ulong ProposedByUserId { get; set; }
}

/// <summary>A channel a community shares with a pair.</summary>
public sealed class BridgeSharedChannel
{
    /// <summary>The channel id.</summary>
    public ulong Id { get; set; }

    /// <summary>The channel name.</summary>
    public string Name { get; set; } = "";

    /// <summary>The name of the category the channel is in, or empty.</summary>
    public string Category { get; set; } = "";
}
