using System.Threading.Tasks;
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
        embed.WithAuthor(new EmbedAuthorBuilder().WithIconUrl(ctx.Client.CurrentUser.RealAvatarUrl().ToString())
            .WithName("Mewdeko Help Menu"));
        embed.WithColor(Mewdeko.Services.Mewdeko.OkColor);
        embed.WithDescription(
            $"Do {Prefix}cmds `category` to see more info on a command category! For example {Prefix}cmds Moderation" +
            $"\nDo {Prefix}help `command` to see a description of a command you need more info on! For example {Prefix}h afk" +
            $"\nJoin our partner server: https://discord.gg/nezuko");
        embed.AddField("<:Nekoha_Oooo:866320687810740234> **Categories**",
            "> <:nekohayay:866315028989739048> Administration" +
            "\n> <:Nekoha_ok:866616128443645952> Moderation" +
            "\n> <:Nekohacry:866615973834391553> Utility" +
            "\n> <:Nekoha_sleep:866321311886344202> Suggestions" +
            "\n> <:Nekoha_Yawn:866320872003076136> Server Management" +
            "\n> <:Nekoha_angy:866321279929024582> Permissions" +
            "\n> <:Nekoha_huh:866615758032994354> Xp",
            true);
        embed.AddField("_ _",
            "> <:Nekoha_Flushed:866321565393748008> NSFW" +
            "\n> <:Nekohacheer:866614949895077900> Music" +
            "\n> <:Nekohapoke:866613862468026368> Gambling" +
            "\n> <:nekoha_slam:866316199317864458> Searches" +
            "\n> <:Nekoha_wave:866321165538164776> Games" +
            "\n> <:Nekohaquestion:866616825750749184> Help" +
            "\n> <:nekoha_stare:866316293179572264> Custom Reactions",
            true);
        embed.AddField("<:Nekohapeek:866614585992937482> Links",
            "[Documentation](https://mewdeko.tech) | [Support Server](https://discord.gg/wB9FBMreRk) | [Invite Me](https://discord.com/oauth2/authorize?client_id=752236274261426212&scope=bot&permissions=66186303&scope=bot%20applications.commands) | [Top.gg Listing](https://top.gg/bot/752236274261426212) | [Donate!](https://ko-fi.com/mewdeko) ");
        await ctx.Interaction.RespondAsync(embed: embed.Build());
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
        await ctx.Interaction.RespondAsync(embed: eb.Build());
    }
}