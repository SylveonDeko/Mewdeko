using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Core.Services;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Help.Services
{
    public class HelpService : ILateExecutor, INService
    {
        public static HashSet<HelpInfo> list3 = new();
        private readonly BotConfigService _bss;
        private readonly CommandHandler _ch;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _cmds;
        private readonly DiscordPermOverrideService _dpos;
        private readonly IServiceProvider _services;
        private readonly IBotStrings _strings;

        public HelpService(CommandHandler ch, IBotStrings strings,
            DiscordPermOverrideService dpos, BotConfigService bss, IServiceProvider prov, CommandService cmds,
            DiscordSocketClient client)
        {
            _client = client;
            _cmds = cmds;
            _services = prov;
            _ch = ch;
            _strings = strings;
            _dpos = dpos;
            _bss = bss;
            _client.MessageReceived += HandlePing;
            _client.InteractionCreated += HandleModules;
        }

        public Task LateExecute(DiscordSocketClient client, IGuild guild, IUserMessage msg)
        {
            var settings = _bss.Data;
            if (guild == null)
            {
                if (string.IsNullOrWhiteSpace(settings.DmHelpText) || settings.DmHelpText == "-")
                    return Task.CompletedTask;

                if (CREmbed.TryParse(settings.DmHelpText, out var embed))
                    return msg.Channel.EmbedAsync(embed);

                return msg.Channel.SendMessageAsync(settings.DmHelpText);
            }

            return Task.CompletedTask;
        }

        public void UpdateHash(HelpInfo info)
        {
            list3.Add(info);
        }

        public async Task HandleModules(SocketInteraction ine)
        {
            if (ine is SocketMessageComponent parsedArg)
            {
                if (parsedArg.Data.Values == null)
                    return;
                await parsedArg.DeferAsync();
                var selectedValue = parsedArg.Data.Values?.First();
                if (!list3.Any()) return;
                var name = selectedValue.ToTitleCase();
                if (selectedValue == "custom") name = "Custom Reactions";
                if (selectedValue == "servermanage") name = "Server Management";
                var ta = list3.FirstOrDefault(x => x.chan == parsedArg.Channel);
                var selmens = ta.Builder.WithPlaceholder(name);
                var context = new CommandContext(_client, ta.msg);
                var module = selectedValue.Trim().ToUpperInvariant();
                var cmds = _cmds.Commands.Where(c =>
                        c.Module.GetTopLevelModule().Name.ToUpperInvariant()
                            .StartsWith(module, StringComparison.InvariantCulture))
                    .OrderBy(c => c.Aliases[0])
                    .Distinct(new CommandTextEqualityComparer());
                // check preconditions for all commands, but only if it's not 'all'
                // because all will show all commands anyway, no need to check
                var succ = new HashSet<CommandInfo>();
                succ = new HashSet<CommandInfo>((await Task.WhenAll(cmds.Select(async x =>
                    {
                        var pre = await x.CheckPreconditionsAsync(context, _services).ConfigureAwait(false);
                        return (Cmd: x, Succ: pre.IsSuccess);
                    })).ConfigureAwait(false))
                    .Where(x => x.Succ)
                    .Select(x => x.Cmd));

                var cmdsWithGroup = cmds
                    .GroupBy(c => c.Module.Name.Replace("Commands", "", StringComparison.InvariantCulture))
                    .OrderBy(x => x.Key == x.First().Module.Name ? int.MaxValue : x.Count());


                var i = 0;
                var groups = cmdsWithGroup.GroupBy(x => i++ / 48).ToArray();
                var embed = new EmbedBuilder().WithOkColor();
                foreach (var g in groups)
                {
                    var last = g.Count();
                    for (i = 0; i < last; i++)
                    {
                        var transformed = g.ElementAt(i).Select(x =>
                        {
                            //if cross is specified, and the command doesn't satisfy the requirements, cross it out
                            return
                                $"{(succ.Contains(x) ? "✅" : "❌")}{_ch.GetPrefix((parsedArg.Channel as ITextChannel).Guild) + x.Aliases.First(),-15} {"[" + x.Aliases.Skip(1).FirstOrDefault() + "]",-8}";
                        });

                        if (i == last - 1 && (i + 1) % 2 != 0)
                        {
                            var grp = 0;
                            var count = transformed.Count();
                            transformed = transformed
                                .GroupBy(x => grp++ % count / 2)
                                .Select(x =>
                                {
                                    if (x.Count() == 1)
                                        return $"{x.First()}";
                                    return string.Concat(x);
                                });
                        }

                        embed.AddField(g.ElementAt(i).Key, "```css\n" + string.Join("\n", transformed) + "\n```", true);
                    }
                }

                if (parsedArg.User.Id == ta.msg.Author.Id)
                    await parsedArg.Message.ModifyAsync(x =>
                    {
                        x.Embed = embed.Build();
                        x.Components = new ComponentBuilder().WithSelectMenu(selmens).Build();
                    });
                else
                    await parsedArg.FollowupAsync("This isnt your help embed but heres the result anyway",
                        embed: embed.Build(), ephemeral: true);
            }
        }

        private async Task HandlePing(SocketMessage msg)
        {
            if (msg.Content == _client.CurrentUser.Mention && !msg.Author.IsBot)
                if (msg.Channel is ITextChannel chan)
                {
                    var eb = new EmbedBuilder();
                    eb.WithOkColor();
                    eb.WithDescription(
                        $"Hi there! To see my command categories do `{_ch.GetPrefix(chan.Guild)}cmds`\n My current Prefix is `{_ch.GetPrefix(chan.Guild)}`\nIf you need help using the bot feel free to join the [Support Server](https://discord.gg/6n3aa9Xapf)!\n\n I hope you have a great day!");
                    eb.WithThumbnailUrl("https://cdn.discordapp.com/emojis/866321565393748008.png?size=2048");
                    eb.WithFooter(new EmbedFooterBuilder().WithText(_client.CurrentUser.Username)
                        .WithIconUrl(_client.CurrentUser.RealAvatarUrl().ToString()));
                    await chan.SendMessageAsync(embed: eb.Build());
                }
        }

        public EmbedBuilder GetCommandHelp(CommandInfo com, IGuild guild)
        {
            var prefix = _ch.GetPrefix(guild);

            var str = string.Format("**`{0}`**", prefix + com.Aliases.First());
            var alias = com.Aliases.Skip(1).FirstOrDefault();
            if (alias != null)
                str += string.Format(" **/ `{0}`**", prefix + alias);
            var em = new EmbedBuilder()
                .AddField(fb => fb.WithName(str)
                    .WithValue($"{com.RealSummary(_strings, guild?.Id, prefix)}")
                    .WithIsInline(true));

            _dpos.TryGetOverrides(guild?.Id ?? 0, com.Name, out var overrides);
            var reqs = GetCommandRequirements(com, overrides);
            if (reqs.Any())
                em.AddField(GetText("requires", guild),
                    string.Join("\n", reqs));

            em
                .AddField(fb => fb.WithName(GetText("usage", guild))
                    .WithValue(string.Join("\n", Array.ConvertAll(com.RealRemarksArr(_strings, guild?.Id, prefix),
                        arg => Format.Code(arg))))
                    .WithIsInline(false))
                .WithFooter(efb => efb.WithText(GetText("module", guild, com.Module.GetTopLevelModule().Name)))
                .WithImageUrl(com.GetCommandImage(_strings, guild?.Id, prefix))
                .WithColor(Mewdeko.OkColor);

            var opt = ((MewdekoOptionsAttribute)com.Attributes.FirstOrDefault(x => x is MewdekoOptionsAttribute))
                ?.OptionType;
            if (opt != null)
            {
                var hs = GetCommandOptionHelp(opt);
                if (!string.IsNullOrWhiteSpace(hs))
                    em.AddField(GetText("options", guild), hs);
            }

            return em;
        }

        public static string GetCommandOptionHelp(Type opt)
        {
            var strs = GetCommandOptionHelpList(opt);

            return string.Join("\n", strs);
        }

        public static List<string> GetCommandOptionHelpList(Type opt)
        {
            var strs = opt.GetProperties()
                .Select(x => x.GetCustomAttributes(true).FirstOrDefault(a => a is OptionAttribute))
                .Where(x => x != null)
                .Cast<OptionAttribute>()
                .Select(x =>
                {
                    var toReturn = $"`--{x.LongName}`";

                    if (!string.IsNullOrWhiteSpace(x.ShortName))
                        toReturn += $" (`-{x.ShortName}`)";

                    toReturn += $"   {x.HelpText}  ";
                    return toReturn;
                })
                .ToList();

            return strs;
        }


        public static string[] GetCommandRequirements(CommandInfo cmd, GuildPerm? overrides = null)
        {
            var toReturn = new List<string>();

            if (cmd.Preconditions.Any(x => x is OwnerOnlyAttribute))
                toReturn.Add("Bot Owner Only");

            var userPerm = (UserPermAttribute)cmd.Preconditions
                .FirstOrDefault(ca => ca is UserPermAttribute);

            var userPermString = string.Empty;
            if (!(userPerm is null))
            {
                if (userPerm.UserPermissionAttribute.ChannelPermission is ChannelPermission cPerm)
                    userPermString = GetPreconditionString((ChannelPerm)cPerm);
                if (userPerm.UserPermissionAttribute.GuildPermission is GuildPermission gPerm)
                    userPermString = GetPreconditionString((GuildPerm)gPerm);
            }

            if (overrides is null)
            {
                if (!string.IsNullOrWhiteSpace(userPermString))
                    toReturn.Add(userPermString);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(userPermString))
                    toReturn.Add(Format.Strikethrough(userPermString));

                toReturn.Add(GetPreconditionString(overrides.Value));
            }

            return toReturn.ToArray();
        }

        public static string GetPreconditionString(ChannelPerm perm)
        {
            return (perm + " Channel Permission")
                .Replace("Guild", "Server", StringComparison.InvariantCulture);
        }

        public static string GetPreconditionString(GuildPerm perm)
        {
            return (perm + " Server Permission")
                .Replace("Guild", "Server", StringComparison.InvariantCulture);
        }

        private string GetText(string text, IGuild guild, params object[] replacements)
        {
            return _strings.GetText(text, guild?.Id, replacements);
        }

        public record HelpInfo
        {
            public IUser user { get; set; }
            public IUserMessage msg { get; set; }
            public IChannel chan { get; set; }
            public SelectMenuBuilder Builder { get; set; }
        }
    }
}