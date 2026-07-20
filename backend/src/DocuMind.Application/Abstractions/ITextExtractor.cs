namespace DocuMind.Application.Abstractions;

/// <summary>
/// Extracts text per page from a document stream (e.g., a PDF). Implementations MUST preserve the
/// originating page number for every extracted segment and MUST NOT perform OCR — an empty
/// <see cref="PageText.Text"/> for every page signals a scanned/image-only document, which the
/// upload pipeline reports as a warning rather than silent success.
/// </summary>
public interface ITextExtractor
{
    Task<IReadOnlyList<PageText>> ExtractAsync(Stream content, CancellationToken cancellationToken = default);
}

/// <summary>
/// The text extracted from a single page, with its 1-based page number preserved.
/// </summary>
/// <param name="PageNumber">The 1-based page number within the source document.</param>
/// <param name="Text">The extracted text, or empty if the page has no extractable text layer.</param>
public record PageText(int PageNumber, string Text);
