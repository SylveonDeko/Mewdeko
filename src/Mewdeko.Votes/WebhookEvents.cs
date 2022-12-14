using System.Threading.Tasks;
using Mewdeko.Votes.Common;
using Mewdeko.Votes.Common.PubSub;

namespace Mewdeko.Votes;

public class WebhookEvents
{
    private readonly TypedKey<CompoundVoteModal> typedKey;
    private readonly IPubSub pubSub;

    public WebhookEvents(IPubSub pubSub)
    {
        this.pubSub = pubSub;
        typedKey = new TypedKey<CompoundVoteModal>("uservoted");
    }

    public async Task InvokeTopGg(VoteModel data, string key)
    {
        var compoundModel = new CompoundVoteModal
        {
            VoteModel = data, Password = key
        };
        await pubSub.Pub(typedKey, compoundModel);
    }
}