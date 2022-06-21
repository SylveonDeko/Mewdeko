using Discord.Commands;
using System.Runtime.CompilerServices;

namespace Mewdeko.Common.Attributes.TextCommands;

[AttributeUsage(AttributeTargets.Method)]
public sealed class Cmd : CommandAttribute
{
    public Cmd([CallerMemberName] string memberName = "")
        : base(CommandNameLoadHelper.GetCommandNameFor(memberName)) =>
        MethodName = memberName.ToLowerInvariant();

    public string MethodName { get; }
}