using System;

namespace Mewdeko.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MewdekoOptionsAttribute : Attribute
    {
        public MewdekoOptionsAttribute(Type t)
        {
            OptionType = t;
        }

        public Type OptionType { get; set; }
    }
}