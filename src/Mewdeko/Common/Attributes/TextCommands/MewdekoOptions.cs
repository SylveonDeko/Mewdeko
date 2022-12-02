namespace Mewdeko.Common.Attributes.TextCommands;

[AttributeUsage(AttributeTargets.Method)]
public sealed class MewdekoOptionsAttribute : Attribute
{
    public MewdekoOptionsAttribute(Type t) => OptionType = t;

    public Type OptionType { get; set; }
}