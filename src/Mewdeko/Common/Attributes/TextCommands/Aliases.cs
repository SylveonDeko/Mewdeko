using System.Runtime.CompilerServices;
using Discord.Commands;

namespace Mewdeko.Common.Attributes.TextCommands;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AliasesAttribute : AliasAttribute
{
    public AliasesAttribute([CallerMemberName] string memberName = "")
        : base(CommandNameLoadHelper.GetAliasesFor(memberName))
    {
    }
}