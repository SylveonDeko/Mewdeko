//using NadekoBot.Core.Services;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text.RegularExpressions;

//namespace NadekoBot.Modules.Administration.Services
//{
//    public class PackagesService : INService
//    {
//        public IEnumerable<string> Packages { get; private set; }

//        public PackagesService()
//        {
//            ReloadAvailablePackages();
//        }

//        public void ReloadAvailablePackages()
//        {
//            Packages = Directory.GetDirectories(Path.Combine(Appctx.BaseDirectory, "modules\\"), "NadekoBot.Modules.*", SearchOption.AllDirectories)
//                   .SelectMany(x => Directory.GetFiles(x, "NadekoBot.Modules.*.dll"))
//                   .Select(x => Path.GetFileNameWithoutExtension(x))
//                   .Select(x =>
//                   {
//                       var m = Regex.Match(x, @"NadekoBot\.Modules\.(?<name>.*)");
//                       return m.Groups["name"].Value;
//                   });
//        }
//    }
//}
