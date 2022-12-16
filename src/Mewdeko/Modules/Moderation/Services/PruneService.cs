using System.Threading.Tasks;
using Mewdeko.Common.Collections;

namespace Mewdeko.Modules.Moderation.Services;

public class PurgeService : INService
{
    //channelids where Purges are currently occuring
    private readonly ConcurrentHashSet<ulong> pruningGuilds = new();

    private readonly TimeSpan twoWeeks = TimeSpan.FromDays(14);

    public async Task PurgeWhere(ITextChannel channel, ulong amount, Func<IMessage, bool> predicate, ulong messageId = 0)
    {
        channel.ThrowIfNull(nameof(channel));
        if (amount <= 0 && messageId is 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        if (!pruningGuilds.Add(channel.GuildId))
            return;
        if (amount > int.MaxValue)
            return;
        try
        {
            IMessage[] msgs;
            if (messageId is 0)
                msgs = (await channel.GetMessagesAsync().FlattenAsync().ConfigureAwait(false)).Where(predicate)
                    .Take((int)amount).ToArray();
            else
            {
                var msg = await channel.GetMessageAsync(messageId);
                if (msg.Timestamp.Subtract(DateTimeOffset.Now).Days == 14)
                    return;
                msgs = (await channel.GetMessagesAsync(messageId, Direction.After).FlattenAsync()).ToArray();
                amount = Convert.ToUInt64(msgs.Length);
            }

            while (amount > 0 && msgs.Length > 0)
            {
                var lastMessage = msgs[^1];

                var bulkDeletable = new List<IMessage>();
                var singleDeletable = new List<IMessage>();
                foreach (var x in msgs)
                {
                    if (DateTime.UtcNow - x.CreatedAt < twoWeeks)
                        bulkDeletable.Add(x);
                    else
                        singleDeletable.Add(x);
                }

                if (bulkDeletable.Count > 0)
                {
                    await Task.WhenAll(Task.Delay(1000), channel.DeleteMessagesAsync(bulkDeletable))
                        .ConfigureAwait(false);
                }

                var i = 0;
                foreach (var group in singleDeletable.GroupBy(_ => ++i / (singleDeletable.Count / 5)))
                {
                    await Task.WhenAll(Task.Delay(1000), Task.WhenAll(group.Select(x => x.DeleteAsync())))
                        .ConfigureAwait(false);
                }

                //this isn't good, because this still work as if i want to remove only specific user's messages from the last
                //100 messages, Maybe this needs to be reduced by msgs.Length instead of 100
                amount -= 50;
                if (amount > 0)
                {
                    msgs = (await channel.GetMessagesAsync(lastMessage, Direction.Before).FlattenAsync()
                        .ConfigureAwait(false)).Where(predicate).Take((int)amount).ToArray();
                }
            }
        }
        catch
        {
            //ignore
        }
        finally
        {
            pruningGuilds.TryRemove(channel.GuildId);
        }
    }
}