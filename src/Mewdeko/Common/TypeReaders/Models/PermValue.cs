namespace Mewdeko.Common.TypeReaders.Models;

/// <summary>
/// Represents a permission value in the Mewdeko application.
/// </summary>
public class PermValue
{
    /// <summary>
    /// Initializes a new instance of the PermValue class.
    /// </summary>
    /// <param name="value">The value of the permission.</param>
    public PermValue(Discord.PermValue value) => Value = value;

    /// <summary>
    /// Gets an instance of the PermValue class that represents enabling a permission.
    /// </summary>
    public static PermValue Enable => new(Discord.PermValue.Allow);

    /// <summary>
    /// Gets an instance of the PermValue class that represents disabling a permission.
    /// </summary>
    public static PermValue Disable => new(Discord.PermValue.Deny);

    /// <summary>
    /// Gets an instance of the PermValue class that represents inheriting a permission.
    /// </summary>
    public static PermValue Inherit => new(Discord.PermValue.Inherit);

    /// <summary>
    /// Gets the value of the permission.
    /// </summary>
    public Discord.PermValue Value { get; }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType()) return false;

        return Value == ((PermValue)obj).Value;
    }

    /// <summary>
    /// Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode() => Value.GetHashCode();
}