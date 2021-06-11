using Discord;
using Discord.Commands;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Replacements;
using Mewdeko.Core.Common;
using Mewdeko.Core.Modules.Help.Common;
using Mewdeko.Core.Services;
using Mewdeko.Extensions;
using Mewdeko.Modules.Help.Services;
using Mewdeko.Modules.Permissions.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Discord.WebSocket;
using Mewdeko.Core.Common.Attributes;

namespace Mewdeko.Modules.Help
{
    public class Help : MewdekoModule<HelpService>
    {
        public const string PatreonUrl = "https://patreon.com/Mewdeko";
        public const string PaypalUrl = "https://paypal.me/Kwoth";
        private readonly CommandService _cmds;
        private readonly BotConfigService _bss;
        private readonly GlobalPermissionService _perms;
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly IBotStrings _strings;

        private readonly AsyncLazy<ulong> _lazyClientId;

        public Help(GlobalPermissionService perms, CommandService cmds, BotConfigService bss,
            IServiceProvider services, DiscordSocketClient client, IBotStrings strings)
        {
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
                    .WithDescription(String.Format(botSettings.HelpText, clientId, Prefix));
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
            if (--page < 0)
                return;

            var topLevelModules = _cmds.Modules.GroupBy(m => m.GetTopLevelModule())
                .Where(m => !_perms.BlockedModules.Contains(m.Key.Name.ToLowerInvariant()))
                .Select(x => x.Key)
                .ToList();

            await ctx.SendPaginatedConfirmAsync(page, cur =>
            {
                var embed = new EmbedBuilder().WithOkColor()
                    .WithTitle(GetText("list_of_modules"))
                    .WithDescription(
                        "Invite me using this link:\nhttps://mewdeko.tech/invite\nIf you need help setting up the bot join the support server:\nhttps://mewdeko.tech/support");

                var localModules = topLevelModules.Skip(12 * cur)
                    .Take(12)
                    .ToList();

                if (!localModules.Any())
                {
                    embed = embed.WithOkColor()
                        .WithDescription(GetText("module_page_empty"));
                    return embed;
                }

                localModules
                    .OrderBy(module => module.Name)
                    .ForEach(module => embed.AddField($"{GetModuleEmoji(module.Name)} {module.Name}",
                        GetText($"module_description_{module.Name.ToLowerInvariant()}") + "\n" +
                        Format.Code(GetText("module_footer", Prefix, module.Name.ToLowerInvariant())),
                        true));

                return embed;
            }, topLevelModules.Count(), 12, false);
        }

        private string GetModuleEmoji(string moduleName)
        {
            moduleName = moduleName.ToLowerInvariant();
            switch (moduleName)
            {
                case "help":
                    return "❓";
                case "administration":
                    return "🛠️";
                case "customreactions":
                    return "🗣️";
                case "searches":
                    return "🔍";
                case "utility":
                    return "🔧";
                case "games":
                    return "🎲";
                case "gambling":
                    return "💰";
                case "music":
                    return "🎶";
                case "nsfw":
                    return "😳";
                case "permissions":
                    return "🚓";
                case "xp":
                    return "📝";
                default:
                    return "📖";
            }
        }

        [MewdekoCommand, Usage, Description, Aliases]
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
            var cmds = _cmds.Commands.Where(c => c.Module.GetTopLevelModule().Name.ToUpperInvariant().StartsWith(module, StringComparison.InvariantCulture))
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
                    var pre = (await x.CheckPreconditionsAsync(Context, _services).ConfigureAwait(false));
                    return (Cmd: x, Succ: pre.IsSuccess);
                })).ConfigureAwait(false))
                    .Where(x => x.Succ)
                    .Select(x => x.Cmd));

                if (opts.View == CommandsOptions.ViewType.Hide)
                {
                    // if hidden is specified, completely remove these commands from the list
                    cmds = cmds.Where(x => succ.Contains(x));
                }
            }

            var cmdsWithGroup = cmds.GroupBy(c => c.Module.Name.Replace("Commands", "", StringComparison.InvariantCulture))
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
                        {
                            return $"{(succ.Contains(x) ? "✅" : "❌")}{Prefix + x.Aliases.First(),-15} {"[" + x.Aliases.Skip(1).FirstOrDefault() + "]",-8}";
                        }
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
                                else
                                    return String.Concat(x);
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

        [MewdekoCommand, Usage, Description, Aliases]
        [Priority(1)]
        public async Task H([Leftover] CommandInfo com = null)
        {
            var channel = ctx.Channel;

            if (com == null)
            {
                IMessageChannel ch = channel is ITextChannel
                    ? await ((IGuildUser)ctx.User).GetOrCreateDMChannelAsync().ConfigureAwait(false)
                    : channel;
                try
                {
                    var data = await GetHelpStringEmbed();
                    if (data == default)
                        return;
                    var (plainText, helpEmbed) = data;
                    await ch.EmbedAsync(helpEmbed, msg: plainText ?? "").ConfigureAwait(false);
                    try{ await ctx.OkAsync(); } catch { } // ignore if bot can't react
                }
                catch (Exception)
                {
                    await ReplyErrorLocalizedAsync("cant_dm").ConfigureAwait(false);
                }
                return;
            }

            var embed = _service.GetCommandHelp(com, ctx.Guild);
            await channel.EmbedAsync(embed).ConfigureAwait(false);
        }
        
        [MewdekoCommand, Usage, Description, Aliases]
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
                            var opt = ((MewdekoOptionsAttribute)com.Attributes.FirstOrDefault(x => x is MewdekoOptionsAttribute))?.OptionType;
                            if (opt != null)
                            {
                                optHelpStr = HelpService.GetCommandOptionHelpList(opt);
                            }
                            
                            return new CommandJsonObject
                            {
                                Aliases = com.Aliases.Select(alias => Prefix + alias).ToArray(),
                                Description = com.RealSummary(_strings, ctx.Guild?.Id, Prefix),
                                Usage = com.RealRemarksArr(_strings, ctx.Guild?.Id, Prefix),
                                Submodule = com.Module.Name,
                                Module = com.Module.GetTopLevelModule().Name,
                                Options = optHelpStr,
                                Requirements = HelpService.GetCommandRequirements(com),
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
                var config = new AmazonS3Config {ServiceURL = serviceUrl};
                using (var client = new AmazonS3Client(accessKey, secretAcccessKey, config))
                {
                    var res = await client.PutObjectAsync(new PutObjectRequest()
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
                await ctx.Channel.SendFileAsync(rDataStream, "cmds.json", GetText("commandlist_regen")).ConfigureAwait(false);
            }
        }

        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Guide()
        {
            await ConfirmLocalizedAsync("guide",
                "https://Mewdeko.bot/commands",
                "http://Mewdeko.readthedocs.io/en/latest/").ConfigureAwait(false);
        }

        [MewdekoCommand, Usage, Description, Aliases]
        public async Task Donate()
        {
            await ReplyConfirmLocalizedAsync("donate", PatreonUrl, PaypalUrl).ConfigureAwait(false);
        }
    }

    public class CommandTextEqualityComparer : IEqualityComparer<CommandInfo>
    {
        public bool Equals(CommandInfo x, CommandInfo y) => x.Aliases[0] == y.Aliases[0];

        public int GetHashCode(CommandInfo obj) => obj.Aliases[0].GetHashCode(StringComparison.InvariantCulture);

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
