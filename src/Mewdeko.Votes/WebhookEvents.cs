using Mewdeko.Votes.Common;
using Mewdeko.Votes.Common.PubSub;
using System.Threading.Tasks;

namespace Mewdeko.Votes;

public class WebhookEvents
{
    private readonly TypedKey<CompoundVoteModal> _typedKey;
    private readonly IPubSub _pubSub;

    public WebhookEvents(IPubSub pubSub)
    {
        _pubSub = pubSub;
        _typedKey = new TypedKey<CompoundVoteModal>("uservoted");
    }

    public async Task InvokeTopGg(VoteModel data, string key)
    {
        var compoundModel = new CompoundVoteModal { VoteModel = data, Password = key };
        await _pubSub.Pub(_typedKey, compoundModel);
    }
}