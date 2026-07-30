using DocuMind.Domain.Entities;

namespace DocuMind.Application.Abstractions;

/// <summary>
/// Persists ingested documents and their chunks to the document store, and retrieves the
/// chunks most relevant to a question.
/// </summary>
public interface IChunkRepository
{
    Task AddDocumentAsync(Document document, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the <paramref name="k"/> chunks owned by <paramref name="ownerId"/> whose embeddings
    /// are closest to <paramref name="queryEmbedding"/> by cosine distance, ordered nearest-first.
    /// A chunk owned by any other user MUST NEVER be returned, even when it is the best semantic
    /// match (security property, not a tuning concern).
    /// </summary>
    /// <remarks>
    /// Implementations MUST order by cosine distance using the same distance operator as the
    /// HNSW index's operator class (<c>vector_cosine_ops</c>, the <c>&lt;=&gt;</c> operator in
    /// PostgreSQL/pgvector). A mismatched distance operator (e.g. L2) causes PostgreSQL to
    /// silently fall back to a sequential scan instead of using the index — no error, no warning,
    /// just a much slower query as the table grows.
    ///
    /// Implementations MUST filter by <paramref name="ownerId"/> on <c>document_chunks</c> itself
    /// (the relation the HNSW index lives on), not via a join to <c>documents</c>. A join-shaped
    /// filter places the owner predicate above the ordered index scan as a semi-join — the least
    /// predictable plan shape available, and the class of "compiles, runs, silently wrong plan"
    /// failure this filter exists to prevent, one layer up. The owner-scoped filter also depends on
    /// the runtime GUC asserted by <c>RetrievalPrerequisiteCheck</c>: without
    /// <c>hnsw.iterative_scan = strict_order</c>, PostgreSQL's post-scan filtering can silently
    /// return fewer than <paramref name="k"/> rows instead of continuing the scan — see ADR-I.
    /// </remarks>
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(Guid ownerId, ReadOnlyMemory<float> queryEmbedding, int k, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the documents owned by <paramref name="ownerId"/>, newest metadata only — never the
    /// owner id itself, which must not leave this layer through this projection.
    /// </summary>
    Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(Guid ownerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The caller-facing shape of an owned document, deliberately excluding <c>OwnerId</c> — the
/// listing endpoint that projects this MUST NOT expose ownership information in its payload.
/// </summary>
/// <param name="Id">The document's identifier.</param>
/// <param name="FileName">The original uploaded file name.</param>
/// <param name="PageCount">The number of pages extracted from the document.</param>
/// <param name="UploadedAtUtc">When the document was uploaded.</param>
public record DocumentSummary(Guid Id, string FileName, int PageCount, DateTime UploadedAtUtc);

/// <summary>
/// A chunk retrieved for a question, with the source metadata needed to build a citation. Always
/// derived from stored chunk/document rows — never from model-generated text.
/// </summary>
/// <param name="ChunkId">The identifier of the retrieved <see cref="DocumentChunk"/>.</param>
/// <param name="DocumentName">The file name of the source document.</param>
/// <param name="PageNumber">The 1-based page number within the source document.</param>
/// <param name="Content">The chunk's text content, used to build the answer's context.</param>
/// <param name="Distance">The cosine distance between the chunk's embedding and the query embedding (lower is closer).</param>
public record RetrievedChunk(Guid ChunkId, string DocumentName, int PageNumber, string Content, double Distance);
