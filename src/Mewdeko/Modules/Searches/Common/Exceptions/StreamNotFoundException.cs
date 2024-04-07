namespace Mewdeko.Modules.Searches.Common.Exceptions;

/// <summary>
/// Represents errors that occur when a specific stream cannot be found or accessed.
/// </summary>
public class StreamNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StreamNotFoundException"/> class.
    /// </summary>
    public StreamNotFoundException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamNotFoundException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StreamNotFoundException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamNotFoundException"/> class
    /// with a specified error message and a reference to the inner exception that
    /// is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception,
    /// or a null reference if no inner exception is specified.</param>
    public StreamNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}