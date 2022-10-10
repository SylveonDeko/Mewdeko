using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Utility.Services;

public class MessageDeleteService : INService
{
    public MessageDeleteService(DiscordSocketClient client)
    {
        _ = Task.Run(async () => await MessageDeleteLoop(client));
    }
    public static async Task MessageDeleteLoop(DiscordSocketClient client)
    {
        var guild = client.GetGuild(708154079695601685);
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