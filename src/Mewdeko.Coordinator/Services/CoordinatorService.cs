using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Mewdeko.Coordinator.Shared;

namespace Mewdeko.Coordinator.Services;

public sealed class CoordinatorService : Coordinator.CoordinatorBase
{
    private readonly CoordinatorRunner runner;

    public CoordinatorService(CoordinatorRunner runner) => this.runner = runner;

    public override Task<HeartbeatReply> Heartbeat(HeartbeatRequest request, ServerCallContext context)
    {
        var gracefulImminent = runner.Heartbeat(request.ShardId, request.GuildCount, request.State, request.UserCount);
        return Task.FromResult(new HeartbeatReply
        {
            GracefulImminent = gracefulImminent
        });
    }

    public override Task<ReshardReply> Reshard(ReshardRequest request, ServerCallContext context)
    {
        runner.SetShardCount(request.Shards);
        return Task.FromResult(new ReshardReply());
    }

    public override Task<RestartShardReply> RestartShard(RestartShardRequest request, ServerCallContext context)
    {
        runner.RestartShard(request.ShardId);
        return Task.FromResult(new RestartShardReply());
    }

    public override Task<ReloadReply> Reload(ReloadRequest request, ServerCallContext context)
    {
        runner.ReloadConfig();
        return Task.FromResult(new ReloadReply());
    }

    public override Task<GetStatusReply> GetStatus(GetStatusRequest request, ServerCallContext context)
    {
        var status = runner.GetShardStatus(request.ShardId);


        return Task.FromResult(StatusToStatusReply(status));
    }

    public override Task<GetAllStatusesReply> GetAllStatuses(GetAllStatusesRequest request,
        ServerCallContext context)
    {
        var statuses = runner
            .GetAllStatuses();

        var reply = new GetAllStatusesReply();
        foreach (var status in statuses)
            reply.Statuses.Add(StatusToStatusReply(status));

        return Task.FromResult(reply);
    }

    private static GetStatusReply StatusToStatusReply(ShardStatus status)
    {
        DateTime startTime;
        try
        {
            startTime = status.Process is null or { HasExited: true }
                ? DateTime.MinValue.ToUniversalTime()
                : status.Process.StartTime.ToUniversalTime();
        }
        catch
        {
            startTime = DateTime.MinValue.ToUniversalTime();
        }

        return new GetStatusReply
        {
            State = status.State,
            GuildCount = status.GuildCount,
            ShardId = status.ShardId,
            LastUpdate = Timestamp.FromDateTime(status.LastUpdate),
            ScheduledForRestart = status.ShouldRestart,
            StartedAt = Timestamp.FromDateTime(startTime),
            UserCount = status.UserCount
        };
    }

    public override Task<RestartAllReply> RestartAllShards(RestartAllRequest request, ServerCallContext context)
    {
        runner.RestartAll(request.Nuke);
        return Task.FromResult(new RestartAllReply());
    }

    public override async Task<DieReply> Die(DieRequest request, ServerCallContext context)
    {
        if (request.Graceful)
        {
            runner.PrepareGracefulShutdown();
            await Task.Delay(10_000).ConfigureAwait(false);
        }

        runner.SaveState();
        _ = Task.Run(async () =>
        {
            await Task.Delay(250).ConfigureAwait(false);
            Environment.Exit(0);
        });

        return new DieReply();
    }

    public override async Task<SetConfigTextReply> SetConfigText(SetConfigTextRequest request,
        ServerCallContext context)
    {
        await Task.Yield();
        var error = string.Empty;
        var success = true;
        try
        {
            runner.SetConfigText(request.ConfigYml);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            success = false;
        }

        return new SetConfigTextReply(new SetConfigTextReply
        {
            Success = success, Error = error
        });
    }

    public override Task<GetConfigTextReply> GetConfigText(GetConfigTextRequest request,
        ServerCallContext context)
    {
        var text = CoordinatorRunner.GetConfigText();
        return Task.FromResult(new GetConfigTextReply
        {
            ConfigYml = text
        });
    }
}