namespace DocuMind.Domain.Entities;

/// <summary>
/// Represents an uploaded document available for retrieval-augmented chat.
/// </summary>
public class Document
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public int PageCount { get; set; }

    public DateTime UploadedAtUtc { get; set; }

    /// <summary>
    /// The chunks of extracted text and embeddings produced when this document was ingested.
    /// </summary>
    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}
