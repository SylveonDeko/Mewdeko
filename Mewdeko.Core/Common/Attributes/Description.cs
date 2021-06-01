using System;
using System.Runtime.CompilerServices;
using Discord.Commands;
using Mewdeko.Core.Services.Impl;

namespace Mewdeko.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DescriptionAttribute : SummaryAttribute
    {
        // Localization.LoadCommand(memberName.ToLowerInvariant()).Desc
        public DescriptionAttribute(string text = "") : base(text)
        {
        }
    }
}
