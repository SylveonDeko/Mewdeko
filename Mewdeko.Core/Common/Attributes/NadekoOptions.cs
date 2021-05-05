using System;

namespace Mewdeko.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MewdekoOptionsAttribute : Attribute
    {
        public Type OptionType { get; set; }

        public MewdekoOptionsAttribute(Type t)
        {
            this.OptionType = t;
        }
    }
}
