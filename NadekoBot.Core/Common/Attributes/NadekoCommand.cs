using System;
using System.Runtime.CompilerServices;
using Discord.Commands;
using NadekoBot.Core.Services.Impl;

namespace NadekoBot.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class NadekoCommandAttribute : CommandAttribute
    {
        public NadekoCommandAttribute([CallerMemberName] string memberName="") 
            : base(CommandNameLoadHelper.GetCommandNameFor(memberName))
        {
            this.MethodName = memberName.ToLowerInvariant();
        }

        public string MethodName { get; }
    }
}
