using System.Runtime.CompilerServices;
using Discord.Commands;

namespace Mewdeko.Common.Attributes.TextCommands;

[AttributeUsage(AttributeTargets.Method)]
public sealed class Cmd : CommandAttribute
{
    public Cmd([CallerMemberName] string memberName = "")
        : base(CommandNameLoadHelper.GetCommandNameFor(memberName)) =>
        MethodName = memberName.ToLowerInvariant();

    public string MethodName { get; }
}