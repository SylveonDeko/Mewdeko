using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    /// Commands for message counts
    /// </summary>
    public class MessageCountCommands : MewdekoSubmodule<MessageCountService>
    {
        /// <summary>
        /// Gets the count of messages for that query type
        /// </summary>
        /// <param name="queryType"></param>
        /// <param name="snowflakeId"></param>
        [Cmd, Aliases]
        public async Task Messages(
            MessageCountService.CountQueryType queryType = MessageCountService.CountQueryType.User,
            ulong snowflakeId = 0)
        {
            snowflakeId = queryType switch
            {
                MessageCountService.CountQueryType.Guild => ctx.Guild.Id,
                MessageCountService.CountQueryType.User when snowflakeId == 0 => ctx.User.Id,
                MessageCountService.CountQueryType.Channel when snowflakeId == 0 => ctx.Channel.Id,
                _ => snowflakeId
            };

            var count = await Service.GetMessageCount(queryType, ctx.Guild.Id, snowflakeId);

            switch (queryType)
            {
                case MessageCountService.CountQueryType.User:
                    await ctx.Channel.SendConfirmAsync($"The message count for that user is {count}");
                    break;
                case MessageCountService.CountQueryType.Guild:
                    await ctx.Channel.SendConfirmAsync($"The total message count in this server is {count}");
                    break;
                case MessageCountService.CountQueryType.Channel:
                    await ctx.Channel.SendConfirmAsync($"The total message count in that channel is {count}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(queryType), queryType, null);
            }
        }
    }
}