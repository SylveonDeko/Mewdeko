using System;

namespace Mewdeko.Core.Common
{
    public static class Helpers
    {
        public static void ReadErrorAndExit(int exitCode)
        {
            if (!Console.IsInputRedirected)
                Console.ReadKey();

            Environment.Exit(exitCode);
        }
    }
}