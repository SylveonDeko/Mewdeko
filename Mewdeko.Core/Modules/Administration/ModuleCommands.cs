//using Discord.Commands;
//using Mewdeko.Common.Attributes;
//using Mewdeko.Modules.Administration.Services;
//using Mewdeko.Extensions;
//using System;
//using System.IO;
//using System.Reflection;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;
//using System.Linq;

//namespace Mewdeko.Modules.Administration
//{
//    public partial class Administration
//    {
//        [Group]
//        public class PackagesCommands : MewdekoSubmodule<PackagesService>
//        {
//            private readonly Mewdeko _bot;

//            public PackagesCommands(Mewdeko bot)
//            {
//                _bot = bot;
//            }

//            [MewdekoCommand, Usage, Description, Aliases]
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

//            [MewdekoCommand, Usage, Description, Aliases]
//            [RequireContext(ContextType.Guild)]
//            [OwnerOnly]
//            public async Task PackageUnload(string name)
//            {
//                if (name.Contains(":") || name.Contains(".") || name.Contains("\\") || name.Contains("/") || name.Contains("~"))
//                    return;
//                name = name.ToTitleCase();
//                var package = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory,
//                                                "modules",
//                                                $"Mewdeko.Modules.{name}",
//                                                $"Mewdeko.Modules.{name}.dll"));
                
//                await _bot.UnloadPackage(name).ConfigureAwait(false);
//                await ReplyAsync(":ok:");
//            }

//            [MewdekoCommand, Usage, Description, Aliases]
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

//            [MewdekoCommand, Usage, Description, Aliases]
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
