using System;
using System.Runtime.CompilerServices;
using Discord.Commands;

namespace Mewdeko.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AliasesAttribute : AliasAttribute
    {
        public AliasesAttribute([CallerMemberName] string memberName = "")
            : base(CommandNameLoadHelper.GetAliasesFor(memberName))
        {
        }
    }
}