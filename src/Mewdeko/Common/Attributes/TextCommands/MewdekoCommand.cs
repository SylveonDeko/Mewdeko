using System.Runtime.CompilerServices;
using Discord.Commands;

namespace Mewdeko.Common.Attributes.TextCommands;

/// <summary>
/// Attribute to define a command for a method.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class Cmd : CommandAttribute
{
    /// <summary>
    /// The name of the method this attribute is applied to.
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    /// Initializes a new instance of the Cmd class.
    /// </summary>
    /// <param name="memberName">The name of the member this attribute is applied to. This is automatically filled in by the compiler.</param>
    public Cmd([CallerMemberName] string memberName = "")
        : base(CommandNameLoadHelper.GetCommandNameFor(memberName))
    {
        MethodName = memberName.ToLowerInvariant();
    }
}