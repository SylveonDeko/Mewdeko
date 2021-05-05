using System;
using Discord.Commands;

namespace Mewdeko.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    sealed class MewdekoModuleAttribute : GroupAttribute
    {
        public MewdekoModuleAttribute(string moduleName) : base(moduleName)
        {
        }
    }
}

