using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Modules.GlobalBan.Services;

namespace Mewdeko.Modules.GlobalBan
{
    public class GlobalBans : MewdekoModule<GlobalBanService>
    {
        [MewdekoCommand]
        [Alias]
        [Description]
        [RequireContext(ContextType.Guild)]
        
        public async Task GBRep()
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
                .WithButton("Cancel", "cancel", ButtonStyle.Danger);
            var eb = new EmbedBuilder()
                .WithDescription("What type of user are you reporting?")
                .WithTitle("Global Ban Report")
                .WithOkColor();
            var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build(), component: components.Build());
            var input = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id).ConfigureAwait(false);
            if (input == "cancel")
            {
                await msg.ModifyAsync(x =>
                {
                    x.Embed = cancelled.Build();
                    x.Components = null;
                });
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
                    });
                    var next = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
                    if (next.ToLower() == "cancel")
                    {
                        msg.ModifyAsync(x =>
                        {
                            x.Embed = cancelled.Build();
                            x.Components = null;
                        });
                    }

                    if (!next.Contains(","))
                    {
                        
                    }
                    break;
                // case "scammer":
                //     ReportType = "Scammer";
                //     break;
                // case "raider":
                //     ReportType = "Raider";
                //     break;
                // case "abuser":
                //     ReportType = "Perms Abuser";
                //     break;
                // case "raidbot":
                //     ReportType = "Raid Bot";
                //     break;
            }
        }
    }
}