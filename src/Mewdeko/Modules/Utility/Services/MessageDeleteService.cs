using System.Threading;
using System.Threading.Tasks;
using Mewdeko.Common.ModuleBehaviors;

namespace Mewdeko.Modules.Utility.Services;

public class MessageDeleteService : INService, IReadyExecutor
{
    private readonly DiscordSocketClient _client;
    public MessageDeleteService(DiscordSocketClient client)
    {
        _client = client;
    }
    public async Task OnReadyAsync()
    {
        var guild = _client.GetGuild(708154079695601685);
        var channel = guild.GetTextChannel(991163300903669821);
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync())
        {
            IEnumerable<IMessage> messages;
            try
            {
                messages = await channel.GetMessagesAsync(SnowflakeUtils.ToSnowflake(DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(10))), Direction.Before, 300).FlattenAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                continue;
            }

            try
            {
                await channel.DeleteMessagesAsync(messages);
            }
            catch
            {
                // ignored
            }
        }
    }
}