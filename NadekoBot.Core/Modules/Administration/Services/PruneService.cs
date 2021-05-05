using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using NadekoBot.Common.Collections;
using NadekoBot.Extensions;
using NadekoBot.Core.Services;

namespace NadekoBot.Modules.Administration.Services
{
    public class PruneService : INService
    {
        //channelids where prunes are currently occuring
        private ConcurrentHashSet<ulong> _pruningGuilds = new ConcurrentHashSet<ulong>();
        private readonly TimeSpan twoWeeks = TimeSpan.FromDays(14);
        private readonly LogCommandService _logService;

        public PruneService(LogCommandService logService)
        {
            this._logService = logService;
        }

        public async Task PruneWhere(ITextChannel channel, int amount, Func<IMessage, bool> predicate)
        {
            channel.ThrowIfNull(nameof(channel));
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            if (!_pruningGuilds.Add(channel.GuildId))
                return;

            try
            {
                IMessage[] msgs;
                IMessage lastMessage = null;
                msgs = (await channel.GetMessagesAsync(50).FlattenAsync().ConfigureAwait(false)).Where(predicate).Take(amount).ToArray();
                while (amount > 0 && msgs.Any())
                {
                    lastMessage = msgs[msgs.Length - 1];

                    var bulkDeletable = new List<IMessage>();
                    var singleDeletable = new List<IMessage>();
                    foreach (var x in msgs)
                    {
                        _logService.AddDeleteIgnore(x.Id);

                        if (DateTime.UtcNow - x.CreatedAt < twoWeeks)
                            bulkDeletable.Add(x);
                        else
                            singleDeletable.Add(x);
                    }

                    if (bulkDeletable.Count > 0)
                        await Task.WhenAll(Task.Delay(1000), channel.DeleteMessagesAsync(bulkDeletable)).ConfigureAwait(false);

                    var i = 0;
                    foreach (var group in singleDeletable.GroupBy(x => ++i / (singleDeletable.Count / 5)))
                        await Task.WhenAll(Task.Delay(1000), Task.WhenAll(group.Select(x => x.DeleteAsync()))).ConfigureAwait(false);

                    //this isn't good, because this still work as if i want to remove only specific user's messages from the last
                    //100 messages, Maybe this needs to be reduced by msgs.Length instead of 100
                    amount -= 50;
                    if(amount > 0)
                        msgs = (await channel.GetMessagesAsync(lastMessage, Direction.Before, 50).FlattenAsync().ConfigureAwait(false)).Where(predicate).Take(amount).ToArray();
                }
            }
            catch
            {
                //ignore
            }
            finally
            {
                _pruningGuilds.TryRemove(channel.GuildId);
            }
        }
    }
}
