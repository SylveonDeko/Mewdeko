using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using CommandLine;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Extensions.Interactive;
using Mewdeko.Common.Extensions.Interactive.Entities.Page;
using Mewdeko.Common.Extensions.Interactive.Pagination;
using Mewdeko.Common.Extensions.Interactive.Pagination.Lazy;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services;
using Mewdeko.Modules.Administration.Services;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;

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
        private readonly IBotStrings _strings;

        public HelpService(CommandHandler ch, IBotStrings strings,
            DiscordPermOverrideService dpos, BotConfigService bss, IServiceProvider prov, CommandService cmds,
            DiscordSocketClient client, GlobalPermissionService gbs)
        {
            _client = client;
            _cmds = cmds;
            _ch = ch;
            _strings = strings;
            _dpos = dpos;
            _bss = bss;
            _client.MessageReceived += HandlePing;
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
        
        private async Task HandlePing(SocketMessage msg)
        {
            if (msg.Content == $"<@{_client.CurrentUser.Id}>" || msg.Content == $"<@!{_client.CurrentUser.Id}>" )
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
            var botReqs = GetCommandBotRequirements(com);
            if (reqs.Any())
                em.AddField("You Need",
                    string.Join("\n", reqs));
            if (botReqs.Any())
                em.AddField("Bot Needs", string.Join("\n", botReqs));

            em
                .AddField(fb => fb.WithName(GetText("usage", guild))
                    .WithValue(string.Join("\n", Array.ConvertAll(com.RealRemarksArr(_strings, guild?.Id, prefix),
                        arg => Format.Code(arg))))
                    .WithIsInline(false))
                .WithFooter(efb => efb.WithText(GetText("module", guild, com.Module.GetTopLevelModule().Name)))
                .WithImageUrl(com.GetCommandImage(_strings, guild?.Id, prefix))
                .WithColor(Mewdeko.Services.Mewdeko.OkColor);

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


        public static string[] GetCommandRequirements(CommandInfo cmd, GuildPermission? overrides = null)
        {
            var toReturn = new List<string>();

            if (cmd.Preconditions.Any(x => x is OwnerOnlyAttribute))
                toReturn.Add("Bot Owner Only");

            var userPerm = (UserPermAttribute)cmd.Preconditions
                .FirstOrDefault(ca => ca is UserPermAttribute);

            var userPermString = string.Empty;
            if (userPerm is not null)
            {
                if (userPerm.UserPermissionAttribute.ChannelPermission is { } cPerm)
                    userPermString = GetPreconditionString(cPerm);
                if (userPerm.UserPermissionAttribute.GuildPermission is { } gPerm)
                    userPermString = GetPreconditionString(gPerm);
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
        public static string[] GetCommandBotRequirements(CommandInfo cmd)
        {
            var toReturn = new List<string>();

            if (cmd.Preconditions.Any(x => x is OwnerOnlyAttribute))
                toReturn.Add("Bot Owner Only");

            var botPerm = (BotPermAttribute)cmd.Preconditions
                .FirstOrDefault(ca => ca is BotPermAttribute);

            var botPermString = string.Empty;
            if (botPerm is not null)
            {
                if (botPerm.ChannelPermission is { } cPerm)
                    botPermString = GetPreconditionString(cPerm);
                if (botPerm.GuildPermission is { } gPerm)
                    botPermString = GetPreconditionString(gPerm);
            }
            if (!string.IsNullOrWhiteSpace(botPermString))
                toReturn.Add(botPermString);

            return toReturn.ToArray();
        }

        public static string GetPreconditionString(ChannelPermission perm)
        {
            return (perm + " Channel Permission")
                .Replace("Guild", "Server", StringComparison.InvariantCulture);
        }

        public static string GetPreconditionString(GuildPermission perm)
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