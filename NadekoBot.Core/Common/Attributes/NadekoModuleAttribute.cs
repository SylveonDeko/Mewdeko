using System;
using Discord.Commands;

namespace NadekoBot.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    sealed class NadekoModuleAttribute : GroupAttribute
    {
        public NadekoModuleAttribute(string moduleName) : base(moduleName)
        {
        }
    }
}

