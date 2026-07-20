using Pgvector;

namespace DocuMind.Domain.Entities;

/// <summary>
/// A chunk of text extracted from a <see cref="Document"/> page, together with its embedding
/// vector, used for retrieval-augmented chat.
/// </summary>
public class DocumentChunk
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    /// <summary>
    /// The 1-based page number this chunk was extracted from. Preserved for exact citations.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// The 0-based position of this chunk within its source page, used to keep chunks ordered.
    /// </summary>
    public int Ordinal { get; set; }

    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// The embedding vector for <see cref="Content"/>, stored in the pgvector-backed column.
    /// </summary>
    public Vector Embedding { get; set; } = null!;
}
