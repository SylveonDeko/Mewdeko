using System;
using System.Diagnostics;

namespace Mewdeko.Coordinator.Shared;

public sealed record ShardStatus(
    int ShardId,
    DateTime LastUpdate,
    int GuildCount = 0,
    ConnState State = ConnState.Disconnected,
    bool ShouldRestart = false,
    Process Process = null,
    int StateCounter = 0,
    int UserCount = 0
);