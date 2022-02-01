using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Humanizer;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public class SlashSnipes : MewdekoSlashModuleBase<UtilityService>
{
    private readonly DiscordSocketClient _client;

    public SlashSnipes(DiscordSocketClient client) => _client = client;

    [SlashCommand("snipe", "Snipes deleted messages for the current or mentioned channel"), RequireContext(ContextType.Guild)]
    public async Task Snipe(IMessageChannel channel = null, IUser user = null)
    {
        channel ??= ctx.Channel;
        if (Service.GetSnipeSet(ctx.Guild.Id) == 0)
        {
            await ctx.Interaction.SendErrorAsync(
                $"Sniping is not enabled in this server! Use `{Prefix}snipeset enable` to enable it!");
            return;
        }

        
        var msg = Service.GetSnipes(ctx.Guild.Id).Result?.Where(x => x.Edited == 0)
                         .LastOrDefault(x => x.ChannelId == channel.Id);
        
        if (user is not null)
            msg = Service.GetSnipes(ctx.Guild.Id).Result?.Where(x => x.Edited == 0)
                         .LastOrDefault(x => x.ChannelId == channel.Id && x.UserId == user.Id);
        
        if (msg is null)
        {
            await ctx.Interaction.SendErrorAsync("There is nothing to snipe here!");
            return;
        }
        
        user = await ctx.Channel.GetUserAsync(msg.UserId) ?? await _client.Rest.GetUserAsync(msg.UserId);

        var em = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder
            {
                IconUrl = user.GetAvatarUrl(),
                Name = $"{user} said:"
            },
            Description = msg.Message,
            Footer = new EmbedFooterBuilder
            {
                IconUrl = ctx.User.GetAvatarUrl(),
                Text =
                    $"Snipe requested by {ctx.User} || Message deleted {(DateTime.UtcNow - msg.DateAdded.Value).Humanize()} ago"
            },
            Color = Mewdeko.Services.Mewdeko.OkColor
        };
        await ctx.Interaction.RespondAsync(embed: em.Build());
    }
    
    [SlashCommand("editsnipe", "Snipes edited messages for the current or mentioned channel"), RequireContext(ContextType.Guild)]
    public async Task EditSnipe(IMessageChannel channel = null, IUser user = null)
    {
        channel ??= ctx.Channel;
        if (Service.GetSnipeSet(ctx.Guild.Id) == 0)
        {
            await ctx.Interaction.SendErrorAsync(
                $"Sniping is not enabled in this server! Use `{Prefix}snipeset enable` to enable it!");
            return;
        }

        
        var msg = Service.GetSnipes(ctx.Guild.Id).Result?.Where(x => x.Edited == 0)
                         .LastOrDefault(x => x.ChannelId == channel.Id);
        
        if (user is not null)
            msg = Service.GetSnipes(ctx.Guild.Id).Result?.Where(x => x.Edited == 0)
                         .LastOrDefault(x => x.ChannelId == channel.Id && x.UserId == user.Id);
        
        if (msg is null)
        {
            await ctx.Interaction.SendErrorAsync("There is nothing to snipe here!");
            return;
        }
        
        user = await ctx.Channel.GetUserAsync(msg.UserId) ?? await _client.Rest.GetUserAsync(msg.UserId);

        var em = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder
            {
                IconUrl = user.GetAvatarUrl(),
                Name = $"{user} said:"
            },
            Description = msg.Message,
            Footer = new EmbedFooterBuilder
            {
                IconUrl = ctx.User.GetAvatarUrl(),
                Text =
                    $"Snipe requested by {ctx.User} || Message deleted {(DateTime.UtcNow - msg.DateAdded.Value).Humanize()} ago"
            },
            Color = Mewdeko.Services.Mewdeko.OkColor
        };
        await ctx.Interaction.RespondAsync(embed: em.Build());
    }
}