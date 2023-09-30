namespace Mewdeko.Common.Attributes.TextCommands;

[AttributeUsage(AttributeTargets.Method)]
public sealed class MewdekoOptionsAttribute(Type t) : Attribute
{
    public Type OptionType { get; set; } = t;
}