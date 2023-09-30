using System.Runtime.CompilerServices;
using Discord.Commands;

namespace Mewdeko.Common.Attributes.TextCommands;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AliasesAttribute([CallerMemberName] string memberName = "") : AliasAttribute(
    CommandNameLoadHelper.GetAliasesFor(memberName));