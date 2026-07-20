namespace DocuMind.Application.Exceptions;

/// <summary>
/// Thrown when an uploaded document cannot be parsed as a valid PDF (e.g. a corrupt or
/// truncated file). Callers should translate this into a client error (HTTP 400) rather than
/// letting it surface as an unhandled server error.
/// </summary>
public sealed class InvalidDocumentException : Exception
{
    public InvalidDocumentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
