using Mewdeko.Controllers.Common.Bridge;
using Mewdeko.Modules.Bridge.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

public partial class BridgeController
{
    /// <summary>
    ///     Lists the communities this guild is paired with, with the channels each side shares and the pending proposals.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    [HttpGet("pairs")]
    public async Task<IActionResult> GetPairs(ulong guildId)
    {
        var result = new List<BridgePairResponse>();
        foreach (var pair in await bridges.Pairs.GetPairsAsync(guildId))
            result.Add(await bridges.Pairs.DescribeAsync(pair));

        return Ok(result);
    }

    /// <summary>
    ///     Creates a pair code for this guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The dashboard user.</param>
    [HttpPost("pairs/code")]
    public async Task<IActionResult> CreatePairCode(ulong guildId, [FromBody] BridgePairCodeRequest? request)
    {
        if (request is null)
            return PairFailure(BridgePairError.InvalidRequest);

        var (error, code, expiresAt) = await bridges.Pairs.CreateCodeAsync(guildId, request.UserId);
        return error == BridgePairError.None
            ? Ok(new BridgeCodeResponse
            {
                Code = code!, ExpiresAt = expiresAt
            })
            : PairFailure(error);
    }

    /// <summary>
    ///     Pairs this guild with the community a code was made for.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="request">The code and the dashboard user.</param>
    [HttpPost("pairs/link")]
    public async Task<IActionResult> LinkPair(ulong guildId, [FromBody] BridgePairLinkRequest? request)
    {
        if (request is null)
            return PairFailure(BridgePairError.InvalidRequest);

        var (error, pair) = await bridges.Pairs.AcceptCodeAsync(guildId, request.UserId, request.Code ?? "");
        if (error != BridgePairError.None || pair is null)
            return PairFailure(error);

        var response = await bridges.Pairs.DescribeAsync(pair);
        auditContext.RecordAfter(response);
        return Ok(response);
    }

    /// <summary>
    ///     Replaces the approval mode and hidden channels of a pair.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="pairId">The pair ID.</param>
    /// <param name="request">The new settings.</param>
    [HttpPost("pairs/{pairId:guid}/settings")]
    public async Task<IActionResult> PairSettings(ulong guildId, Guid pairId,
        [FromBody] BridgePairSettingsRequest? request)
    {
        if (await bridges.Pairs.GetPairAsync(guildId, pairId) is not { } before)
            return PairFailure(BridgePairError.NotFound);

        auditContext.RecordBefore(await bridges.Pairs.DescribeAsync(before));
        var (error, pair) = await bridges.Pairs.SetSettingsAsync(guildId, pairId, request?.ApprovalMode,
            request?.HiddenChannelIds);
        if (error != BridgePairError.None || pair is null)
            return PairFailure(error);

        var response = await bridges.Pairs.DescribeAsync(pair);
        auditContext.RecordAfter(response);
        return Ok(response);
    }

    /// <summary>
    ///     Proposes bridging a local channel to a channel the other community shares.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="pairId">The pair ID.</param>
    /// <param name="request">The channels and the dashboard user.</param>
    [HttpPost("pairs/{pairId:guid}/connect")]
    public async Task<IActionResult> ConnectPair(ulong guildId, Guid pairId,
        [FromBody] BridgePairConnectRequest? request)
    {
        if (request is not { LocalChannelId: { } local, RemoteChannelId: { } remote })
            return PairFailure(BridgePairError.InvalidRequest);

        var (error, proposalId, connected) =
            await bridges.Pairs.ConnectAsync(guildId, pairId, local, remote, request.UserId);
        return error == BridgePairError.None
            ? Ok(new BridgePairConnectResponse
            {
                Status = connected ? "connected" : "proposed", ProposalId = proposalId
            })
            : PairFailure(error);
    }

    /// <summary>
    ///     Accepts a proposal the other community made.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="pairId">The pair ID.</param>
    /// <param name="proposalId">The proposal ID.</param>
    /// <param name="request">The dashboard user.</param>
    [HttpPost("pairs/{pairId:guid}/proposals/{proposalId:guid}/accept")]
    public async Task<IActionResult> AcceptProposal(ulong guildId, Guid pairId, Guid proposalId,
        [FromBody] BridgeProposalAcceptRequest? request)
    {
        if (request is null)
            return PairFailure(BridgePairError.InvalidRequest);

        var error = await bridges.Pairs.AcceptProposalAsync(guildId, proposalId, request.UserId, pairId);
        return error == BridgePairError.None ? await PairResult(guildId, pairId) : PairFailure(error);
    }

    /// <summary>
    ///     Declines a proposal the other community made.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="pairId">The pair ID.</param>
    /// <param name="proposalId">The proposal ID.</param>
    [HttpPost("pairs/{pairId:guid}/proposals/{proposalId:guid}/decline")]
    public async Task<IActionResult> DeclineProposal(ulong guildId, Guid pairId, Guid proposalId)
    {
        var error = await bridges.Pairs.DeclineProposalAsync(guildId, proposalId, pairId);
        return error == BridgePairError.None ? await PairResult(guildId, pairId) : PairFailure(error);
    }

    /// <summary>
    ///     Cancels a proposal this guild made.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="pairId">The pair ID.</param>
    /// <param name="proposalId">The proposal ID.</param>
    [HttpDelete("pairs/{pairId:guid}/proposals/{proposalId:guid}")]
    public async Task<IActionResult> CancelProposal(ulong guildId, Guid pairId, Guid proposalId)
    {
        var error = await bridges.Pairs.CancelProposalAsync(guildId, proposalId, pairId);
        return error == BridgePairError.None ? await PairResult(guildId, pairId) : PairFailure(error);
    }

    /// <summary>
    ///     Removes a pair.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="pairId">The pair ID.</param>
    /// <param name="keepBridges">Whether the bridges made through the pair stay. They are removed when false.</param>
    [HttpDelete("pairs/{pairId:guid}")]
    public async Task<IActionResult> Unpair(ulong guildId, Guid pairId, [FromQuery] bool keepBridges = false)
    {
        if (await bridges.Pairs.GetPairAsync(guildId, pairId) is not { } before)
            return PairFailure(BridgePairError.NotFound);

        auditContext.RecordBefore(await bridges.Pairs.DescribeAsync(before));
        var error = await bridges.Pairs.UnpairAsync(guildId, pairId, keepBridges);
        return error == BridgePairError.None ? Ok() : PairFailure(error);
    }

    private async Task<IActionResult> PairResult(ulong guildId, Guid pairId) =>
        await bridges.Pairs.GetPairAsync(guildId, pairId) is { } pair
            ? Ok(await bridges.Pairs.DescribeAsync(pair))
            : PairFailure(BridgePairError.NotFound);

    private IActionResult PairFailure(BridgePairError error) => error == BridgePairError.NoPermission
        ? StatusCode(StatusCodes.Status403Forbidden, new { error = error.Code() })
        : BadRequest(new { error = error.Code() });
}
