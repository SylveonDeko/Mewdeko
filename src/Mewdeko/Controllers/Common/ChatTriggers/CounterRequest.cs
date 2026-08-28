namespace Mewdeko.Controllers.Common.ChatTriggers;

/// <summary>
///     Request model for setting a chat trigger counter's value.
/// </summary>
public class CounterRequest
{
    /// <summary>
    ///     The counter's name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     The value to set the counter to.
    /// </summary>
    public long Value { get; set; }
}