namespace Mewdeko.Modules.Utility.Common.Exceptions;

/// <summary>
///     Represents errors that occur when a stream role is not found within the application.
/// </summary>
public class StreamRoleNotFoundException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="StreamRoleNotFoundException" /> class with a default error message.
    /// </summary>
    public StreamRoleNotFoundException() : base("Stream role wasn't found.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="StreamRoleNotFoundException" /> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StreamRoleNotFoundException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="StreamRoleNotFoundException" /> class with a specified error message
    ///     and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">
    ///     The exception that is the cause of the current exception, or a null reference if no inner
    ///     exception is specified.
    /// </param>
    public StreamRoleNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}