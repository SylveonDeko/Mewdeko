using System.Runtime.CompilerServices;
using Discord.Commands;

namespace Mewdeko.Common.Attributes.TextCommands;

/// <summary>
/// Attribute to define aliases for a command or method.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AliasesAttribute : AliasAttribute
{
    /// <summary>
    /// Initializes a new instance of the AliasesAttribute class.
    /// </summary>
    /// <param name="memberName">The name of the member this attribute is applied to. This is automatically filled in by the compiler.</param>
    public AliasesAttribute([CallerMemberName] string memberName = "")
        : base(CommandNameLoadHelper.GetAliasesFor(memberName))
    {
    }
}