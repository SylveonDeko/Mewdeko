namespace Mewdeko.Common.TypeReaders.Models;

/// <summary>
/// Represents an action that can be performed on a permission.
/// </summary>
public class PermissionAction
{
    /// <summary>
    /// Initializes a new instance of the PermissionAction class.
    /// </summary>
    /// <param name="value">The value of the action.</param>
    public PermissionAction(bool value) => Value = value;

    /// <summary>
    /// Gets an instance of the PermissionAction class that represents enabling a permission.
    /// </summary>
    public static PermissionAction Enable => new(true);

    /// <summary>
    /// Gets an instance of the PermissionAction class that represents disabling a permission.
    /// </summary>
    public static PermissionAction Disable => new(false);

    /// <summary>
    /// Gets the value of the action.
    /// </summary>
    public bool Value { get; }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType()) return false;

        return Value == ((PermissionAction)obj).Value;
    }

    /// <summary>
    /// Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode() => Value.GetHashCode();
}