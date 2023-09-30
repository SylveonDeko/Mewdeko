using System.Runtime.CompilerServices;
using Discord.Commands;

namespace Mewdeko.Common.Attributes.TextCommands;

[AttributeUsage(AttributeTargets.Method)]
public sealed class Cmd([CallerMemberName] string memberName = "") : CommandAttribute(
    CommandNameLoadHelper.GetCommandNameFor(memberName))
{
    public string MethodName { get; } = memberName.ToLowerInvariant();
}