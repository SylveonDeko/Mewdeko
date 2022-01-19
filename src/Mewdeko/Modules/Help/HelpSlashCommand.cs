using Discord;
using Discord.Interactions;
using Mewdeko._Extensions;
using Mewdeko.Common;

namespace Mewdeko.Modules.Help;

public class HelpSlashCommand : MewdekoSlashCommandModule
{
    [SlashCommand("help", "Shows help on how to use the bot")]
    public async Task Modules()
    {
        var embed = new EmbedBuilder();
        embed.WithAuthor(new EmbedAuthorBuilder().WithIconUrl(Ctx.Client.CurrentUser.RealAvatarUrl().ToString())
            .WithName("Mewdeko Help Menu"));
        embed.WithColor(Mewdeko.Services.Mewdeko.OkColor);
        embed.WithAuthor(new EmbedAuthorBuilder().WithIconUrl(Ctx.Client.CurrentUser.RealAvatarUrl().ToString())
                                                 .WithName("Mewdeko Help Menu"));
        embed.WithColor(Mewdeko.Services.Mewdeko.OkColor);
        embed.WithDescription(
            $"\nDo `{Prefix}help command` to see a description of a command you need more info on! For example {Prefix}h afk" +
            $"\nJoin our partner server: https://discord.gg/nezuko");
        embed.AddField("**Categories**",
            $">  `{Prefix}cmds Administration`" +
            $"\n>  `{Prefix}cmds Moderation`" +
            $"\n>  `{Prefix}cmds Utility`" +
            $"\n>  `{Prefix}cmds Suggestions`" +
            $"\n>  `{Prefix}cmds Server Management`" +
            $"\n>  `{Prefix}cmds Permissions`" +
            $"\n>  `{Prefix}cmds Xp`" +
            $"\n>  `{Prefix}cmds Afk`",
            true);
        embed.AddField("_ _",
            $">  `{Prefix}cmds NSFW`" +
            $"\n>  `{Prefix}cmds Music`" +
            $"\n>  `{Prefix}cmds Gambling`" +
            $"\n>  `{Prefix}cmds Searches`" +
            $"\n>  `{Prefix}cmds Games`" +
            $"\n>  `{Prefix}cmds Help`" +
            $"\n>  `{Prefix}cmds Custom Reactions`" + 
            $"\n>  `{Prefix}cmds Giveaways`" + 
            $"\n>  `{Prefix}cmds MultiGreet`",
            true);
        embed.AddField(" Links",
            "[Documentation](https://mewdeko.tech) | [Support Server](https://discord.gg/wB9FBMreRk) | [Invite Me](https://discord.com/oauth2/authorize?client_id=752236274261426212&scope=bot&permissions=66186303&scope=bot%20applications.commands) | [Top.gg Listing](https://top.gg/bot/752236274261426212) | [Donate!](https://ko-fi.com/mewdeko) ");
        await Ctx.Interaction.RespondAsync(embed: embed.Build());
    }

    [SlashCommand("invite", "You should invite me to your server and check all my features!")]
    public async Task Invite()
    {
        var eb = new EmbedBuilder()
            .AddField("Invite Link",
                "[Click Here](https://discord.com/oauth2/authorize?client_id=752236274261426212&scope=bot&permissions=66186303&scope=bot%20applications.commands)")
            .AddField("Website/Docs", "https://mewdeko.tech")
            .AddField("Support Server", "https://discord.gg/wB9FBMreRk")
            .WithOkColor();
        await Ctx.Interaction.RespondAsync(embed: eb.Build());
    }
}