using Mewdeko.Votes.Common;
using System;
using System.Threading.Tasks;

namespace Mewdeko.Votes;

public class WebhookEvents
{
    public event EventHandler<DiscordsVoteWebhookModel> UserVotedDiscords;
    public event EventHandler<TopggVoteWebhookModel> UserVotedTopGg;

    public Task InvokeTopGg(TopggVoteWebhookModel data)
    {
        UserVotedTopGg?.Invoke(this, data);
        return Task.CompletedTask;
    }

    public Task InvokeDiscords(DiscordsVoteWebhookModel data)
    {
        UserVotedDiscords?.Invoke(this, data);
        return Task.CompletedTask;
    }
}