using System;
using Discord.Commands;

namespace Mewdeko.Common.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class UsageAttribute : RemarksAttribute
{
    // public static string GetUsage(string memberName)
    // {
    //     var usage = Localization.LoadCommand(memberName.ToLowerInvariant()).Usage;
    //     return JsonConvert.SerializeObject(usage);
    // }
    public UsageAttribute(string text = "") : base(text)
    {
    }
}