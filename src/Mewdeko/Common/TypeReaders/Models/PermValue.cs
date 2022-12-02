namespace Mewdeko.Common.TypeReaders.Models;

public class PermValue
{
    public PermValue(Discord.PermValue value) => Value = value;

    public static PermValue Enable => new(Discord.PermValue.Allow);
    public static PermValue Disable => new(Discord.PermValue.Deny);
    public static PermValue Inherit => new(Discord.PermValue.Inherit);

    public Discord.PermValue Value { get; }

    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType()) return false;

        return Value == ((PermValue)obj).Value;
    }

    public override int GetHashCode() => Value.GetHashCode();
}