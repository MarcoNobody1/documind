using DocuMind.Domain.Entities;

namespace DocuMind.Application.Abstractions;

/// <summary>
/// Persists ingested documents and their chunks to the shared document store, and retrieves the
/// chunks most relevant to a question.
/// </summary>
public interface IChunkRepository
{
    Task AddDocumentAsync(Document document, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the <paramref name="k"/> chunks (across ALL documents in the shared store) whose
    /// embeddings are closest to <paramref name="queryEmbedding"/> by cosine distance, ordered
    /// nearest-first.
    /// </summary>
    /// <remarks>
    /// Implementations MUST order by cosine distance using the same distance operator as the
    /// HNSW index's operator class (<c>vector_cosine_ops</c>, the <c>&lt;=&gt;</c> operator in
    /// PostgreSQL/pgvector). A mismatched distance operator (e.g. L2) causes PostgreSQL to
    /// silently fall back to a sequential scan instead of using the index — no error, no warning,
    /// just a much slower query as the table grows.
    /// </remarks>
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(ReadOnlyMemory<float> queryEmbedding, int k, CancellationToken cancellationToken = default);
}

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
