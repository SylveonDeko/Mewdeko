//using Discord.Commands;
//using NadekoBot.Common.Attributes;
//using NadekoBot.Modules.Administration.Services;
//using NadekoBot.Extensions;
//using System;
//using System.IO;
//using System.Reflection;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;
//using System.Linq;

//namespace NadekoBot.Modules.Administration
//{
//    public partial class Administration
//    {
//        [Group]
//        public class PackagesCommands : NadekoSubmodule<PackagesService>
//        {
//            private readonly NadekoBot _bot;

//            public PackagesCommands(NadekoBot bot)
//            {
//                _bot = bot;
//            }

//            [NadekoCommand, Usage, Description, Aliases]
//            [RequireContext(ContextType.Guild)]
//            public async Task PackageList()
//            {
//                _service.ReloadAvailablePackages();
//                await Context.Channel.SendConfirmAsync(
//                    string.Join(
//                        "\n", 
//                        _service.Packages
//                            .Select(x => _bot.LoadedPackages.Contains(x)
//                                ? "【✘】" + x
//                                : "【  】" + x)));
//            }

//            [NadekoCommand, Usage, Description, Aliases]
//            [RequireContext(ContextType.Guild)]
//            [OwnerOnly]
//            public async Task PackageUnload(string name)
//            {
//                if (name.Contains(":") || name.Contains(".") || name.Contains("\\") || name.Contains("/") || name.Contains("~"))
//                    return;
//                name = name.ToTitleCase();
//                var package = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory,
//                                                "modules",
//                                                $"NadekoBot.Modules.{name}",
//                                                $"NadekoBot.Modules.{name}.dll"));
                
//                await _bot.UnloadPackage(name).ConfigureAwait(false);
//                await ReplyAsync(":ok:");
//            }

//            [NadekoCommand, Usage, Description, Aliases]
//            [RequireContext(ContextType.Guild)]
//            [OwnerOnly]
//            public async Task PackageLoad(string name)
//            {
//                if (name.Contains(".") || name.Contains("\\") || name.Contains("/") || name.Contains("~"))
//                    return;
//                name = name.ToTitleCase();

//                if (await _bot.LoadPackage(name))
//                    await ReplyAsync(":ok:");
//                else
//                    await ReplyAsync(":x:");
//            }

//            [NadekoCommand, Usage, Description, Aliases]
//            [RequireContext(ContextType.Guild)]
//            [OwnerOnly]
//            public async Task PackageReload(string name)
//            {
//                if (name.Contains(".") || name.Contains("\\") || name.Contains("/") || name.Contains("~"))
//                    return;
//                name = name.ToTitleCase();

//                if (await _bot.UnloadPackage(name))
//                {
//                    await _bot.LoadPackage(name);
//                    await ReplyAsync(":ok:");
//                }
//                else
//                    await ReplyAsync(":x:");
//            }
//        }
//    }
//}
