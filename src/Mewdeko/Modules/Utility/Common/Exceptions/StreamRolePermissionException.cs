namespace Mewdeko.Modules.Utility.Common.Exceptions;

/// <summary>
///     Represents errors that occur due to insufficient permissions when attempting to apply a stream role within the
///     application.
/// </summary>
public class StreamRolePermissionException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="StreamRolePermissionException" /> class with a default error message
    ///     indicating the stream role could not be applied.
    /// </summary>
    public StreamRolePermissionException() : base(
        "Stream role was unable to be applied due to insufficient permissions.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="StreamRolePermissionException" /> class with a specified error
    ///     message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StreamRolePermissionException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="StreamRolePermissionException" /> class with a specified error message
    ///     and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">
    ///     The exception that is the cause of the current exception, or a null reference if no inner
    ///     exception is specified.
    /// </param>
    public StreamRolePermissionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}