using System;
using Discord.Commands;

namespace Mewdeko.Common.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class DescriptionAttribute : SummaryAttribute
{
    // Localization.LoadCommand(memberName.ToLowerInvariant()).Desc
    public DescriptionAttribute(string text = "") : base(text)
    {
    }
}