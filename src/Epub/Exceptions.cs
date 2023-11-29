namespace Devlooped.Epub;

/// <summary>
/// Exception thrown when an EPUB archive is invalid.
/// </summary>
public class InvalidArchiveException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidArchiveException"/> class.
    /// </summary>
    public InvalidArchiveException(string? message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidArchiveException"/> class.
    /// </summary>
    public InvalidArchiveException(string? message, Exception? innerException) : base(message, innerException) { }
}
