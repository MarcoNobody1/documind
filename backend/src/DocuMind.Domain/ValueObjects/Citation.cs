namespace DocuMind.Domain.ValueObjects;

/// <summary>
/// Identifies the source document and page number a retrieved chunk of content came from.
/// Always derived from stored chunk metadata, never from model-generated text.
/// </summary>
/// <param name="DocumentName">The file name of the source document.</param>
/// <param name="PageNumber">The 1-based page number within the source document.</param>
public sealed record Citation(string DocumentName, int PageNumber);
