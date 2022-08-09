using System.Threading.Tasks;
#pragma warning disable CS0693


namespace Mewdeko.Services.Impl;

public class EventHandler 
{
    public delegate Task AsyncEventHandler<in TEventArgs>(TEventArgs args);

    public delegate Task AsyncEventHandler<in TEventArgs, in TArgs>(TEventArgs args, TArgs arsg2);
    
    public event AsyncEventHandler<IMessage>? MessageReceived;
    public event AsyncEventHandler<IGuildUser>? UserJoined;
    public event AsyncEventHandler<IGuild, IUser>? UserLeft;
    public event AsyncEventHandler<Cacheable<IMessage, ulong>, Cacheable<IMessageChannel, ulong>> MessageDeleted;
    public event AsyncEventHandler<Cacheable<SocketGuildUser, ulong>, SocketGuildUser> GuildMemberUpdated;

    public EventHandler(DiscordSocketClient client)
    {
        client.MessageReceived += ClientOnMessageReceived;
        client.UserJoined += ClientOnUserJoined;
        client.UserLeft += ClientOnUserLeft;
        client.MessageDeleted += ClientOnMessageDeleted;
        client.GuildMemberUpdated += ClientOnGuildMemberUpdated;
    }

    private async Task ClientOnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> arg1, SocketGuildUser arg2)
    {
        if (GuildMemberUpdated is not null)
            await GuildMemberUpdated(arg1, arg2);
    }

    private async Task ClientOnMessageDeleted(Cacheable<IMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        if (MessageDeleted is not null)
            await MessageDeleted(arg1, arg2);
    }

    private async Task ClientOnUserLeft(SocketGuild arg1, SocketUser arg2)
    {
        if (UserLeft is not null)
            await UserLeft(arg1, arg2);
    }

    private async Task ClientOnUserJoined(SocketGuildUser arg)
    {
        if (UserJoined is not null)
            await UserJoined(arg);
    }

    private async Task ClientOnMessageReceived(SocketMessage arg)
    {
        if (MessageReceived is not null)
            await MessageReceived(arg);
    }
}