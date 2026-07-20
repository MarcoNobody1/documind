using DocuMind.Domain.Entities;

namespace DocuMind.Application.Abstractions;

/// <summary>
/// Persists ingested documents and their chunks to the shared document store.
/// </summary>
/// <remarks>
/// Slice A (ingestion) only requires <see cref="AddDocumentAsync"/>. Retrieval (e.g. a
/// <c>SearchAsync</c> method for cosine-similarity search) is added in Slice B (chat).
/// </remarks>
public interface IChunkRepository
{
    Task AddDocumentAsync(Document document, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default);
}
