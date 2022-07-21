using Mewdeko.Votes.Common;
using Mewdeko.Votes.Common.PubSub;
using System.Threading.Tasks;

namespace Mewdeko.Votes;

public class WebhookEvents
{
    private readonly TypedKey<VoteModel> _typedKey;
    private readonly IPubSub _pubSub;

    public WebhookEvents(IPubSub pubSub)
    {
        _pubSub = pubSub;
        _typedKey = new TypedKey<VoteModel>("uservoted");
    }

    public async Task InvokeTopGg(VoteModel data) 
        => await _pubSub.Pub(_typedKey, data);
}