namespace Mewdeko.Modules.Games.Common.Hangman.Exceptions;

/// <summary>
///     Represents an exception thrown when a term of a specific type cannot be found.
/// </summary>
public class TermNotFoundException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TermNotFoundException" /> class with a default message.
    /// </summary>
    public TermNotFoundException() : base("Term of that type couldn't be found")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="TermNotFoundException" /> class with the specified message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public TermNotFoundException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="TermNotFoundException" /> class with the specified message and inner
    ///     exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public TermNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}