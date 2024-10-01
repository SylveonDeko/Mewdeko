namespace Mewdeko.Common.Attributes.TextCommands;

/// <summary>
///     Attribute to define options for a method.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MewdekoOptionsAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the MewdekoOptionsAttribute class.
    /// </summary>
    /// <param name="t">The type of the options for the method.</param>
    public MewdekoOptionsAttribute(Type t)
    {
        OptionType = t;
    }

    /// <summary>
    ///     The type of the options for the method.
    /// </summary>
    public Type OptionType { get; set; }
}