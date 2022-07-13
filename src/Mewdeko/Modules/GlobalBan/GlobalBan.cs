using Discord.Commands;
using Discord.Rest;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.GlobalBan.Services;
using System.Threading.Tasks;

namespace Mewdeko.Modules.GlobalBan;

public class GlobalBans : MewdekoModuleBase<GlobalBanService>
{
    private readonly DiscordSocketClient _client;
    private readonly Mewdeko _bot;

    public GlobalBans(DiscordSocketClient client, Mewdeko bot)
    {
        _client = client;
        _bot = bot;
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task GbRep()
    {
        var cancelled = new EmbedBuilder()
            .WithErrorColor()
            .WithDescription("Cancelled");
        var components = new ComponentBuilder()
            .WithButton("Scam Link", "scamlink")
            .WithButton("Scammer", "scammer")
            .WithButton("Raider", "raider")
            .WithButton("Perms Abuser", "abuser")
            .WithButton("Raid Bot", "raidbot")
            .WithButton("Karuta Scammer", "kscammer")
            .WithButton("Cancel", "cancel", ButtonStyle.Danger);
        var eb = new EmbedBuilder()
            .WithDescription("What type of user are you reporting?")
            .WithTitle("Global Ban Report")
            .WithOkColor();
        var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build(), components: components.Build()).ConfigureAwait(false);
        var input = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id).ConfigureAwait(false);
        if (input == "cancel")
        {
            await msg.ModifyAsync(x =>
            {
                x.Embed = cancelled.Build();
                x.Components = null;
            }).ConfigureAwait(false);
            return;
        }

        switch (input)
        {
            case "scamlink":
                var neweb = new EmbedBuilder()
                    .WithDescription(
                        "Please type the userid ([How to get userid](https://cdn.discordapp.com/attachments/866308739334406174/905112166535933992/a4iMvBVWkn.gif)) with proof separated from the userid with a `,` (preferably screenshots hosted on imgur or prnt.sc)")
                    .WithOkColor();
                await msg.ModifyAsync(x =>
                {
                    x.Embed = neweb.Build();
                    x.Components = null;
                }).ConfigureAwait(false);
                var next = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
                if (next.ToLower() == "cancel")
                {
                    await msg.ModifyAsync(x => x.Embed = cancelled.Build()).ConfigureAwait(false);
                    return;
                }

                if (!next.Contains(","))
                {
                    var eb1 = new EmbedBuilder()
                        .WithErrorColor()
                        .WithDescription("You didn't provide info in the correct format. Please start over.");
                    await msg.ModifyAsync(x => x.Embed = eb1.Build()).ConfigureAwait(false);
                }
                else
                {
                    var split = next.Split(",");
                    ulong uid;
                    try
                    {
                        uid = ulong.Parse(split[0]);
                    }
                    catch
                    {
                        var eb2 = new EmbedBuilder()
                            .WithErrorColor()
                            .WithDescription("The User ID you provided wasn't valid, please start over.");
                        await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
                        return;
                    }

                    if (((DiscordSocketClient)ctx.Client).Rest.GetUserAsync(uid) == null)
                    {
                        var eb2 = new EmbedBuilder()
                            .WithErrorColor()
                            .WithDescription("The User ID you provided wasn't valid, please start over.");
                        await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
                        return;
                    }
                    var user = await ((DiscordSocketClient)ctx.Client).Rest.GetUserAsync(uid).ConfigureAwait(false);
                    var channel =
                        await ((DiscordSocketClient)ctx.Client).Rest.GetChannelAsync(_bot.Credentials.GlobalBanReportChannelId).ConfigureAwait(false) as
                        RestTextChannel;
                    var eb1 = new EmbedBuilder()
                        .WithTitle("New Global Ban Report Received!")
                        .AddField("Global Ban Type", "Scam Links")
                        .AddField("Reported By", $"{ctx.User} {ctx.User.Id}")
                        .AddField("Reported In", $"{ctx.Guild.Name} {ctx.Guild.Id}")
                        .AddField("User Reported", $"{user} {user.Id}")
                        .AddField("Proof", string.Join("\n", split, 1, split.Length - 1))
                        .WithOkColor();
                    await channel.SendMessageAsync(embed: eb1.Build()).ConfigureAwait(false);
                    var eb3 = new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription(
                            "Scam Link Report submitted! Please join the support server below in case we need to contact you.");
                    var component1 = new ComponentBuilder().WithButton("Mewdeko Official",
                        url: "https://discord.gg/mewdeko", style: ButtonStyle.Link);
                    await msg.ModifyAsync(x =>
                    {
                        x.Embed = eb3.Build();
                        x.Components = component1.Build();
                    }).ConfigureAwait(false);
                }

                break;
            case "scammer":
                var neweb1 = new EmbedBuilder()
                    .WithDescription(
                        "Please type the userid ([How to get userid](https://cdn.discordapp.com/attachments/866308739334406174/905112166535933992/a4iMvBVWkn.gif)) with proof separated from the userid with a `,` (preferably screenshots hosted on imgur or prnt.sc)")
                    .WithOkColor();
                await msg.ModifyAsync(x =>
                {
                    x.Embed = neweb1.Build();
                    x.Components = null;
                }).ConfigureAwait(false);
                var next1 = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
                if (next1.ToLower() == "cancel")
                {
                    await msg.ModifyAsync(x => x.Embed = cancelled.Build()).ConfigureAwait(false);
                    return;
                }

                if (!next1.Contains(','))
                {
                    var eb1 = new EmbedBuilder()
                        .WithErrorColor()
                        .WithDescription("You didn't provide info in the correct format. Please start over.");
                    await msg.ModifyAsync(x => x.Embed = eb1.Build()).ConfigureAwait(false);
                }
                else
                {
                    var split = next1.Split(",");
                    ulong uid;
                    try
                    {
                        uid = ulong.Parse(split[0]);
                    }
                    catch
                    {
                        var eb2 = new EmbedBuilder()
                            .WithErrorColor()
                            .WithDescription("The User ID you provided wasn't valid, please start over.");
                        await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
                        return;
                    }

                    if (((DiscordSocketClient)ctx.Client).Rest.GetUserAsync(uid) == null)
                    {
                        var eb2 = new EmbedBuilder()
                            .WithErrorColor()
                            .WithDescription("The User ID you provided wasn't valid, please start over.");
                        await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
                        return;
                    }

                    var eb4 = new EmbedBuilder()
                        .WithDescription("Please provide a reason as to why you think they are a scammer.")
                        .WithOkColor();
                    await msg.ModifyAsync(x => x.Embed = eb4.Build()).ConfigureAwait(false);
                    var reasoning = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
                    var user = await ((DiscordSocketClient)ctx.Client).Rest.GetUserAsync(uid).ConfigureAwait(false);
                    var channel =
                        await ((DiscordSocketClient)ctx.Client).Rest.GetChannelAsync(_bot.Credentials.GlobalBanReportChannelId).ConfigureAwait(false) as
                        RestTextChannel;
                    var eb1 = new EmbedBuilder()
                        .WithTitle("New Global Ban Report Received!")
                        .AddField("Global Ban Type", "Scammer")
                        .AddField("Reported By", $"{ctx.User} {ctx.User.Id}")
                        .AddField("Reported In", $"{ctx.Guild.Name} {ctx.Guild.Id}")
                        .AddField("User Reported", $"{user} {user.Id}")
                        .AddField("Reasoning", reasoning)
                        .AddField("Proof", string.Join("\n", split, 1, split.Length - 1))
                        .WithOkColor();
                    await channel.SendMessageAsync(embed: eb1.Build()).ConfigureAwait(false);
                    var eb3 = new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription(
                            "Scammer Report submitted! Please join the support server below in case we need to contact you.");
                    var component1 = new ComponentBuilder().WithButton("Mewdeko Official",
                        url: "https://discord.gg/mewdeko", style: ButtonStyle.Link);
                    await msg.ModifyAsync(x =>
                    {
                        x.Embed = eb3.Build();
                        x.Components = component1.Build();
                    }).ConfigureAwait(false);
                }

                break;
            case "raider":
                var neweb2 = new EmbedBuilder()
                    .WithDescription(
                        "Please type the userid ([How to get userid](https://cdn.discordapp.com/attachments/866308739334406174/905112166535933992/a4iMvBVWkn.gif)) with proof separated from the userid with a `,` (preferably screenshots hosted on imgur or prnt.sc)")
                    .WithOkColor();
                await msg.ModifyAsync(x =>
                {
                    x.Embed = neweb2.Build();
                    x.Components = null;
                }).ConfigureAwait(false);
                var next2 = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
                if (next2.ToLower() == "cancel")
                {
                    await msg.ModifyAsync(x => x.Embed = cancelled.Build()).ConfigureAwait(false);
                    return;
                }

                if (!next2.Contains(','))
                {
                    var eb1 = new EmbedBuilder()
                        .WithErrorColor()
                        .WithDescription("You didn't provide info in the correct format. Please start over.");
                    await msg.ModifyAsync(x => x.Embed = eb1.Build()).ConfigureAwait(false);
                }
                else
                {
                    var split = next2.Split(",");
                    ulong uid;
                    try
                    {
                        uid = ulong.Parse(split[0]);
                    }
                    catch
                    {
                        var eb2 = new EmbedBuilder()
                            .WithErrorColor()
                            .WithDescription("The User ID you provided wasn't valid, please start over.");
                        await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
                        return;
                    }

                    if (((DiscordSocketClient)ctx.Client).Rest.GetUserAsync(uid) == null)
                    {
                        var eb2 = new EmbedBuilder()
                            .WithErrorColor()
                            .WithDescription("The User ID you provided wasn't valid, please start over.");
                        await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
                        return;
                    }

                    var eb4 = new EmbedBuilder()
                        .WithDescription("How did they raid?")
                        .WithOkColor();
                    await msg.ModifyAsync(x => x.Embed = eb4.Build()).ConfigureAwait(false);
                    var reasoning = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
                    new EmbedBuilder()
                        .WithDescription(
                            "Were there any other users? If so please separate IDs with `,` otherwise just say no or none")
                        .WithOkColor();
                    await msg.ModifyAsync(x => x.Embed = eb4.Build()).ConfigureAwait(false);
                    var otherUsers = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
                    var user = await ((DiscordSocketClient)ctx.Client).Rest.GetUserAsync(uid).ConfigureAwait(false);
                    var channel =
                        await ((DiscordSocketClient)ctx.Client).Rest.GetChannelAsync(_bot.Credentials.GlobalBanReportChannelId).ConfigureAwait(false) as
                        RestTextChannel;
                    var eb1 = new EmbedBuilder()
                        .WithTitle("New Global Ban Report Received!")
                        .AddField("Global Ban Type", "Raider")
                        .AddField("Reported By", $"{ctx.User} {ctx.User.Id}")
                        .AddField("Reported In", $"{ctx.Guild.Name} {ctx.Guild.Id}")
                        .AddField("User Reported", $"{user} {user.Id}")
                        .AddField("Reasoning", reasoning)
                        .AddField("Other Users", otherUsers)
                        .AddField("Proof", string.Join("\n", split, 1, split.Length - 1))
                        .WithOkColor();
                    await channel.SendMessageAsync(embed: eb1.Build()).ConfigureAwait(false);
                    var eb3 = new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription(
                            "Raid Report submitted! Please join the support server below in case we need to contact you.");
                    var component1 = new ComponentBuilder().WithButton("Mewdeko Official",
                        url: "https://discord.gg/mewdeko", style: ButtonStyle.Link);
                    await msg.ModifyAsync(x =>
                    {
                        x.Embed = eb3.Build();
                        x.Components = component1.Build();
                    }).ConfigureAwait(false);
                }

                break;
            case "abuser":
                var neweb3 = new EmbedBuilder()
                    .WithDescription(
                        "Please type the userid ([How to get userid](https://cdn.discordapp.com/attachments/866308739334406174/905112166535933992/a4iMvBVWkn.gif)) with proof separated from the userid with a `,` (preferably screenshots hosted on imgur or prnt.sc)")
                    .WithOkColor();
                await msg.ModifyAsync(x =>
                {
                    x.Embed = neweb3.Build();
                    x.Components = null;
                }).ConfigureAwait(false);
                var next3 = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
                if (next3.ToLower() == "cancel")
                {
                    await msg.ModifyAsync(x => x.Embed = cancelled.Build()).ConfigureAwait(false);
                    return;
                }

                if (!next3.Contains(","))
                {
                    var eb1 = new EmbedBuilder()
                        .WithErrorColor()
                        .WithDescription("You didn't provide info in the correct format. Please start over.");
                    await msg.ModifyAsync(x => x.Embed = eb1.Build()).ConfigureAwait(false);
                }
                else
                {
                    var split = next3.Split(",");
                    ulong uid;
                    try
                    {
                        uid = ulong.Parse(split[0]);
                    }
                    catch
                    {
                        var eb2 = new EmbedBuilder()
                            .WithErrorColor()
                            .WithDescription("The User ID you provided wasn't valid, please start over.");
                        await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
                        return;
                    }

                    if (((DiscordSocketClient)ctx.Client).Rest.GetUserAsync(uid) == null)
                    {
                        var eb2 = new EmbedBuilder()
                            .WithErrorColor()
                            .WithDescription("The User ID you provided wasn't valid, please start over.");
                        await msg.ModifyAsync(x => x.Embed = eb2.Build()).ConfigureAwait(false);
                        return;
                    }

                    var eb4 = new EmbedBuilder()
                        .WithDescription("How did they abuse?")
                        .WithOkColor();
                    await msg.ModifyAsync(x => x.Embed = eb4.Build()).ConfigureAwait(false);
                    var reasoning = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
                    new EmbedBuilder()
                        .WithDescription("How did this happen? Did they use an exploit to get perms? Explain")
                        .WithOkColor();
                    await msg.ModifyAsync(x => x.Embed = eb4.Build()).ConfigureAwait(false);
                    var otherUsers = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
                    var user = await ((DiscordSocketClient)ctx.Client).Rest.GetUserAsync(uid).ConfigureAwait(false);
                    var channel =
                        await ((DiscordSocketClient)ctx.Client).Rest.GetChannelAsync(_bot.Credentials.GlobalBanReportChannelId).ConfigureAwait(false) as
                        RestTextChannel;
                    var eb1 = new EmbedBuilder()
                        .WithTitle("New Global Ban Report Received!")
                        .AddField("Global Ban Type", "Perms Abuser")
                        .AddField("Reported By", $"{ctx.User} {ctx.User.Id}")
                        .AddField("Reported In", $"{ctx.Guild.Name} {ctx.Guild.Id}")
                        .AddField("User Reported", $"{user} {user.Id}")
                        .AddField("Reasoning", reasoning)
                        .AddField("How did they abuse perms?", otherUsers)
                        .AddField("Proof", string.Join("\n", split, 1, split.Length - 1))
                        .WithOkColor();
                    await channel.SendMessageAsync(embed: eb1.Build()).ConfigureAwait(false);
                    var eb3 = new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription(
                            "Perms Abuse Report submitted! Please join the support server below in case we need to contact you.");
                    var component1 = new ComponentBuilder().WithButton("Mewdeko Official",
                        url: "https://discord.gg/mewdeko", style: ButtonStyle.Link);
                    await msg.ModifyAsync(x =>
                    {
                        x.Embed = eb3.Build();
                        x.Components = component1.Build();
                    }).ConfigureAwait(false);
                }

                break;
            case "raidbot":
                await ctx.Channel.SendConfirmAsync("Please provide the bot ID.").ConfigureAwait(false);
                var raidReport = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
                if (!ulong.TryParse(raidReport, out var id))
                {
                    await ctx.Channel.SendErrorAsync("That's not a correct ID, please start over.").ConfigureAwait(false);
                    return;
                }

                var reportedUser = await _client.GetUserAsync(id).ConfigureAwait(false);
                if (reportedUser is null)
                {
                    await ctx.Channel.SendErrorAsync("That user is invalid, please make sure you didn't copy message ID.").ConfigureAwait(false);
                    return;
                }

                await ctx.Channel.SendConfirmAsync("Please provide image links to proof.").ConfigureAwait(false);
                var raidProof = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
                break;
        }
    }
}
