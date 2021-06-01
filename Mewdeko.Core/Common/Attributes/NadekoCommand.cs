using System;
using System.Runtime.CompilerServices;
using Discord.Commands;
using Mewdeko.Core.Services.Impl;

namespace Mewdeko.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MewdekoCommandAttribute : CommandAttribute
    {
        public MewdekoCommandAttribute([CallerMemberName] string memberName="") 
            : base(CommandNameLoadHelper.GetCommandNameFor(memberName))
        {
            this.MethodName = memberName.ToLowerInvariant();
        }

        public string MethodName { get; }
    }
}
