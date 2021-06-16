//using Mewdeko.Core.Services;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text.RegularExpressions;

//namespace Mewdeko.Modules.Administration.Services
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
//            Packages = Directory.GetDirectories(Path.Combine(Appctx.BaseDirectory, "modules\\"), "Mewdeko.Modules.*", SearchOption.AllDirectories)
//                   .SelectMany(x => Directory.GetFiles(x, "Mewdeko.Modules.*.dll"))
//                   .Select(x => Path.GetFileNameWithoutExtension(x))
//                   .Select(x =>
//                   {
//                       var m = Regex.Match(x, @"Mewdeko\.Modules\.(?<name>.*)");
//                       return m.Groups["name"].Value;
//                   });
//        }
//    }
//}

