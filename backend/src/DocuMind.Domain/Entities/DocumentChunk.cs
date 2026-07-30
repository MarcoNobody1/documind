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
    /// A denormalized copy of the owning <see cref="Document"/>'s <see cref="Document.OwnerId"/>.
    /// Kept on the chunk itself — not resolved via a join to <c>documents</c> — so the owner-scoped
    /// retrieval predicate in <c>EfChunkRepository.SearchAsync</c> is single-table on the same
    /// relation the HNSW index lives on. Filtering through a join would place the predicate above
    /// the ordered index scan as a semi-join, which is not a shape pgvector's iterative scan is
    /// built for. A composite foreign key to <c>documents (Id, OwnerId)</c> makes it structurally
    /// impossible for this value to disagree with the owning document's.
    /// </summary>
    public Guid OwnerId { get; set; }

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
