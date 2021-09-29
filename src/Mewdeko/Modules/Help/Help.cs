using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Replacements;
using Mewdeko.Core.Common;
using Mewdeko.Core.Modules.Help.Common;
using Mewdeko.Services;
using Mewdeko.Extensions;
using Mewdeko.Interactive;
using Mewdeko.Modules.Help.Services;
using Mewdeko.Modules.Permissions.Services;
using Newtonsoft.Json;

namespace Mewdeko.Modules.Help
{
    public class Help : MewdekoModule<HelpService>
    {
        private readonly BotConfigService _bss;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _cmds;
        private readonly InteractiveService _interactive;
        private readonly AsyncLazy<ulong> _lazyClientId;
        private readonly GlobalPermissionService _perms;
        private readonly IServiceProvider _services;
        private readonly IBotStrings _strings;
        private readonly CommandHandler cmd;

        public Help(GlobalPermissionService perms, CommandService cmds, BotConfigService bss,
            IServiceProvider services, DiscordSocketClient client, IBotStrings strings, CommandHandler c,
            InteractiveService serv)
        {
            _interactive = serv;
            cmd = c;
            _cmds = cmds;
            _bss = bss;
            _perms = perms;
            _services = services;
            _client = client;
            _strings = strings;
            _lazyClientId = new AsyncLazy<ulong>(async () => (await _client.GetApplicationInfoAsync()).Id);
        }


        public async Task<(string plainText, EmbedBuilder embed)> GetHelpStringEmbed()
        {
            var botSettings = _bss.Data;
            if (string.IsNullOrWhiteSpace(botSettings.HelpText) || botSettings.HelpText == "-")
                return default;

            var clientId = await _lazyClientId.Value;
            var r = new ReplacementBuilder()
                .WithDefault(Context)
                .WithOverride("{0}", () => clientId.ToString())
                .WithOverride("{1}", () => Prefix)
                .WithOverride("%prefix%", () => Prefix)
                .WithOverride("%bot.prefix%", () => Prefix)
                .Build();

            var app = await _client.GetApplicationInfoAsync();

            if (!CREmbed.TryParse(botSettings.HelpText, out var embed))
            {
                var eb = new EmbedBuilder().WithOkColor()
                    .WithDescription(string.Format(botSettings.HelpText, clientId, Prefix));
                return ("", eb);
            }

            r.Replace(embed);

            return (embed.PlainText, embed.ToEmbed());
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Modules(int page = 1)
        {
            var builder = new SelectMenuBuilder()
                .WithCustomId("id_2")
                .WithPlaceholder("Select your category here")
                .WithOptions(new List<SelectMenuOptionBuilder>
                {
                    new SelectMenuOptionBuilder()
                        .WithLabel("Administration")
                        .WithEmote(Emote.Parse("<:nekohayay:866315028989739048>"))
                        .WithDescription("Prefix, Autoroles, and other admin related stuff.")
                        .WithValue("administration"),
                    new SelectMenuOptionBuilder()
                        .WithLabel("Moderation")
                        .WithEmote(Emote.Parse("<:Nekoha_ok:866616128443645952>"))
                        .WithDescription("Warns, Purging, and Banning stAuffs")
                        .WithValue("moderation"),
                    new SelectMenuOptionBuilder()
                        .WithLabel("Utility")
                        .WithDescription("Sniping, Starboard and other useful stuff.")
                        .WithEmote(Emote.Parse("<:Nekohacry:866615973834391553>"))
                        .WithValue("utility"),
                    new SelectMenuOptionBuilder()
                        .WithLabel("Suggestions")
                        .WithEmote(Emote.Parse("<:Nekoha_sleep:866321311886344202>"))
                        .WithDescription("The most cusomizable suggestions you'll find.")
                        .WithValue("suggestions"),
                    new SelectMenuOptionBuilder()
                        .WithLabel("Server Management")
                        .WithEmote(Emote.Parse("<:Nekoha_Yawn:866320872003076136>"))
                        .WithDescription("Mass role, channel perms, and vc stuffs.")
                        .WithValue("servermanage"),
                    new SelectMenuOptionBuilder()
                        .WithLabel("Permissions")
                        .WithEmote(Emote.Parse("<:Nekoha_angy:866321279929024582>"))
                        .WithDescription("Manage command and category perms.")
                        .WithValue("permissions"),
                    new SelectMenuOptionBuilder()
                        .WithLabel("Xp")
                        .WithEmote(Emote.Parse("<:Nekoha_huh:866615758032994354>"))
                        .WithDescription("View ranks and set xp config.")
                        .WithValue("xp"),
                    new SelectMenuOptionBuilder()
                        .WithLabel("NSFW")
                        .WithEmote(Emote.Parse("<:Nekoha_Flushed:866321565393748008>"))
                        .WithDescription("Read NHentai in discord, no incognito!")
                        .WithValue("nsfw"),
                    new SelectMenuOptionBuilder()
                        .WithLabel("Music")
                        .WithEmote(Emote.Parse("<:Nekohacheer:866614949895077900>"))
                        .WithDescription("What is love, baby dont hurt me..")
                        .WithValue("Music"),
                    new SelectMenuOptionBuilder()
                        .WithLabel("Gambling")
                        .WithEmote(Emote.Parse("<:Nekohapoke:866613862468026368>"))
                        .WithDescription("Currency based games, these are global.")
                        .WithValue("gambling"),
                    new SelectMenuOptionBuilder()
                        .WithLabel("Searches")
                        .WithEmote(Emote.Parse("<:nekoha_slam:866316199317864458>"))
                        .WithDescription("Huggin, anime searches, and memes.")
                        .WithValue("searches"),
                    new SelectMenuOptionBuilder()
                        .WithLabel("Games")
                        .WithEmote(Emote.Parse("<:nekohayay:866315028989739048>"))
                        .WithDescription("What do you expect, legend of zelda?")
                        .WithValue("games"),
                    new SelectMenuOptionBuilder()
                        .WithLabel("Help")
                        .WithEmote(Emote.Parse("<:Nekoha_wave:866321165538164776>"))
                        .WithDescription("pls send help")
                        .WithValue("help"),
                    new SelectMenuOptionBuilder()
                        .WithLabel("Custom Reactions")
                        .WithEmote(Emote.Parse("<:nekoha_stare:866316293179572264>"))
                        .WithDescription("Make the bot say stuff based on triggers.")
                        .WithValue("custom")
                });
            var toadd = new HelpService.HelpInfo
            {
                user = ctx.User,
                msg = ctx.Message,
                chan = ctx.Channel,
                Builder = builder
            };
            _service.UpdateHash(toadd);
            var builder2 = new ComponentBuilder().WithSelectMenu(builder);
            var embed = new EmbedBuilder();
            embed.WithAuthor(new EmbedAuthorBuilder().WithIconUrl(ctx.Client.CurrentUser.RealAvatarUrl().ToString())
                .WithName("Mewdeko Help Menu"));
            embed.WithColor(Mewdeko.OkColor);
            embed.WithDescription(
                $"{Prefix}cmds `category` to see whats in that category.\n{Prefix}help `command` to see a description of that command\nYou can also click one of the buttons below to see the full unpaginated list of commands for each category!");
            embed.AddField("<:Nekoha_Oooo:866320687810740234> **Categories**",
                "> <:nekohayay:866315028989739048> Administration\n> <:Nekoha_ok:866616128443645952> Moderation\n> <:Nekohacry:866615973834391553> Utility\n> <:Nekoha_sleep:866321311886344202> Suggestions\n> <:Nekoha_Yawn:866320872003076136> Server Management\n> <:Nekoha_angy:866321279929024582> Permissions\n> <:Nekoha_huh:866615758032994354> Xp",
                true);
            embed.AddField("_ _",
                "> <:Nekoha_Flushed:866321565393748008> NSFW\n> <:Nekohacheer:866614949895077900> Music\n> <:Nekohapoke:866613862468026368> Gambling\n> <:nekoha_slam:866316199317864458> Searches\n> <:Nekoha_wave:866321165538164776> Games\n> <:Nekohaquestion:866616825750749184> Help\n> <:nekoha_stare:866316293179572264> Custom Reactions",
                true);
            embed.AddField("<:Nekohapeek:866614585992937482> Links",
                "[Website](https://mewdeko.tech) | [Support](https://discord.gg/6n3aa9Xapf) | [Invite Me](https://discord.com/oauth2/authorize?client_id=752236274261426212&scope=bot&permissions=66186303&scope=bot%20applications.commands) | [Top.gg Listing](https://top.gg/bot/752236274261426212) | [Ko-Fi](https://ko-fi.com/mewdeko) | [Patreon](https://patreon.com/mewdeko)");
            await ctx.Channel.SendMessageAsync(embed: embed.Build(), component: builder2.Build());
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Donate()
        {
            await ctx.Channel.SendConfirmAsync(
                "If you would like to support the project, heres how:\nKo-Fi: https://ko-fi.com/mewdeko \nPatreon: https://patreon.com/mewdeko \nI appreciate any donations as they will help improve Mewdeko for the better!");
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [MewdekoOptions(typeof(CommandsOptions))]
        public async Task Commands(string module = null, params string[] args)
        {
            var channel = ctx.Channel;


            module = module?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(module))
            {
                await Modules();
                return;
            }

            var (opts, _) = OptionsParser.ParseFrom(new CommandsOptions(), args);

            // Find commands for that module
            // don't show commands which are blocked
            // order by name
            var cmds = _cmds.Commands.Where(c =>
                    c.Module.GetTopLevelModule().Name.ToUpperInvariant()
                        .StartsWith(module, StringComparison.InvariantCulture))
                .Where(c => !_perms.BlockedCommands.Contains(c.Aliases[0].ToLowerInvariant()))
                .OrderBy(c => c.Aliases[0])
                .Distinct(new CommandTextEqualityComparer());


            // check preconditions for all commands, but only if it's not 'all'
            // because all will show all commands anyway, no need to check
            var succ = new HashSet<CommandInfo>();
            if (opts.View != CommandsOptions.ViewType.All)
            {
                succ = new HashSet<CommandInfo>((await Task.WhenAll(cmds.Select(async x =>
                    {
                        var pre = await x.CheckPreconditionsAsync(Context, _services).ConfigureAwait(false);
                        return (Cmd: x, Succ: pre.IsSuccess);
                    })).ConfigureAwait(false))
                    .Where(x => x.Succ)
                    .Select(x => x.Cmd));

                if (opts.View == CommandsOptions.ViewType.Hide)
                    // if hidden is specified, completely remove these commands from the list
                    cmds = cmds.Where(x => succ.Contains(x));
            }

            var cmdsWithGroup = cmds
                .GroupBy(c => c.Module.Name.Replace("Commands", "", StringComparison.InvariantCulture))
                .OrderBy(x => x.Key == x.First().Module.Name ? int.MaxValue : x.Count());

            if (!cmds.Any())
            {
                if (opts.View != CommandsOptions.ViewType.Hide)
                    await ReplyErrorLocalizedAsync("module_not_found").ConfigureAwait(false);
                else
                    await ReplyErrorLocalizedAsync("module_not_found_or_cant_exec").ConfigureAwait(false);
                return;
            }

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
                        if (opts.View == CommandsOptions.ViewType.Cross)
                            return
                                $"{(succ.Contains(x) ? "✅" : "❌")}{Prefix + x.Aliases.First(),-15} {"[" + x.Aliases.Skip(1).FirstOrDefault() + "]",-8}";
                        return $"{Prefix + x.Aliases.First(),-15} {"[" + x.Aliases.Skip(1).FirstOrDefault() + "]",-8}";
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

            embed.WithFooter(GetText("commands_instr", Prefix));
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [Priority(0)]
        public async Task H([Leftover] string fail)
        {
            var prefixless =
                _cmds.Commands.FirstOrDefault(x => x.Aliases.Any(cmdName => cmdName.ToLowerInvariant() == fail));
            if (prefixless != null)
            {
                await H(prefixless).ConfigureAwait(false);
                return;
            }

            await ReplyErrorLocalizedAsync("command_not_found").ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [Priority(1)]
        public async Task H([Leftover] CommandInfo com = null)
        {
            var channel = ctx.Channel;

            if (com == null)
            {
                await Modules();
                return;
            }

            var embed = _service.GetCommandHelp(com, ctx.Guild);
            await channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        [OwnerOnly]
        public async Task GenCmdList([Leftover] string path = null)
        {
            _ = ctx.Channel.TriggerTypingAsync();

            // order commands by top level module name
            // and make a dictionary of <ModuleName, Array<JsonCommandData>>
            var cmdData = _cmds
                .Commands
                .GroupBy(x => x.Module.GetTopLevelModule().Name)
                .OrderBy(x => x.Key)
                .ToDictionary(
                    x => x.Key,
                    x => x.Distinct(x => x.Aliases.First())
                        .Select(com =>
                        {
                            var module = com.Module.GetTopLevelModule();
                            List<string> optHelpStr = null;
                            var opt = ((MewdekoOptionsAttribute)com.Attributes.FirstOrDefault(x =>
                                x is MewdekoOptionsAttribute))?.OptionType;
                            if (opt != null) optHelpStr = HelpService.GetCommandOptionHelpList(opt);

                            return new CommandJsonObject
                            {
                                Aliases = com.Aliases.Select(alias => Prefix + alias).ToArray(),
                                Description = com.RealSummary(_strings, ctx.Guild?.Id, Prefix),
                                Usage = com.RealRemarksArr(_strings, ctx.Guild?.Id, Prefix),
                                Submodule = com.Module.Name,
                                Module = com.Module.GetTopLevelModule().Name,
                                Options = optHelpStr,
                                Requirements = HelpService.GetCommandRequirements(com)
                            };
                        })
                        .ToList()
                );

            var readableData = JsonConvert.SerializeObject(cmdData, Formatting.Indented);
            var uploadData = JsonConvert.SerializeObject(cmdData, Formatting.None);

            // for example https://nyc.digitaloceanspaces.com (without your space name)
            var serviceUrl = Environment.GetEnvironmentVariable("do_spaces_address");

            // generate spaces access key on https://cloud.digitalocean.com/account/api/tokens
            // you will get 2 keys, first, shorter one is id, longer one is secret
            var accessKey = Environment.GetEnvironmentVariable("do_access_key_id");
            var secretAcccessKey = Environment.GetEnvironmentVariable("do_access_key_secret");

            // if all env vars are set, upload the unindented file (to save space) there
            if (!(serviceUrl is null || accessKey is null || secretAcccessKey is null))
            {
                var config = new AmazonS3Config { ServiceURL = serviceUrl };
                using (var client = new AmazonS3Client(accessKey, secretAcccessKey, config))
                {
                    var res = await client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = "Mewdeko-pictures",
                        ContentType = "application/json",
                        ContentBody = uploadData,
                        // either use a path provided in the argument or the default one for public Mewdeko, other/cmds.json
                        Key = path ?? "other/cmds.json",
                        CannedACL = S3CannedACL.PublicRead
                    });
                }
            }

            // also send the file, but indented one, to chat
            using (var rDataStream = new MemoryStream(Encoding.ASCII.GetBytes(readableData)))
            {
                await ctx.Channel.SendFileAsync(rDataStream, "cmds.json", GetText("commandlist_regen"))
                    .ConfigureAwait(false);
            }
        }

        [MewdekoCommand]
        [Usage]
        [Description]
        [Aliases]
        public async Task Guide()
        {
            await ctx.Channel.SendConfirmAsync("You can find the website at https://mewdeko.tech");
        }
    }

    public class CommandTextEqualityComparer : IEqualityComparer<CommandInfo>
    {
        public bool Equals(CommandInfo x, CommandInfo y)
        {
            return x.Aliases[0] == y.Aliases[0];
        }

        public int GetHashCode(CommandInfo obj)
        {
            return obj.Aliases[0].GetHashCode(StringComparison.InvariantCulture);
        }
    }

    internal class CommandJsonObject
    {
        public string[] Aliases { get; set; }
        public string Description { get; set; }
        public string[] Usage { get; set; }
        public string Submodule { get; set; }
        public string Module { get; set; }
        public List<string> Options { get; set; }
        public string[] Requirements { get; set; }
    }
}